using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TmbOrders.Api.Analytics;

// ---------------------------------------------------------------------------------------------
// Contrato do protocolo "Chat Completions" (formato da OpenAI, adotado por vários provedores).
// ---------------------------------------------------------------------------------------------

public sealed record ChatMessage(
    string Role,
    string? Content = null,
    IReadOnlyList<ChatToolCall>? ToolCalls = null,
    string? ToolCallId = null)
{
    /// <summary>Dados extras do provedor (ex.: Gemini) que precisam voltar intactos na próxima chamada.</summary>
    public JsonElement? ExtraContent { get; init; }

    public static ChatMessage System(string content) => new("system", content);
    public static ChatMessage User(string content) => new("user", content);
    public static ChatMessage Tool(string toolCallId, string content) => new("tool", content, ToolCallId: toolCallId);
}

public sealed record ChatToolCall(string? Id, string Type, ChatFunctionCall Function)
{
    /// <summary>
    /// Campo não padronizado usado pelo Gemini 3+: "extra_content.google.thought_signature".
    /// O modelo assina o raciocínio que gerou a chamada da ferramenta e exige que essa assinatura
    /// seja devolvida junto do resultado; sem ela a API responde 400. Por isso é preservado e reenviado como veio.
    /// </summary>
    public JsonElement? ExtraContent { get; init; }
}

public sealed record ChatFunctionCall(string Name, string? Arguments);

public sealed record ChatTool(string Type, ChatFunction Function)
{
    public static ChatTool Create(string name, string description, string parametersJsonSchema) =>
        new("function", new ChatFunction(name, description, JsonDocument.Parse(parametersJsonSchema).RootElement));
}

public sealed record ChatFunction(string Name, string Description, JsonElement Parameters);

/// <summary>Abstração do modelo de linguagem (permite trocar de provedor e usar um modelo falso nos testes).</summary>
public interface IChatModel
{
    string ModelName { get; }

    Task<ChatMessage> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken);
}

/// <summary>Falha ao falar com o provedor de LLM (sem expor a chave nem detalhes sensíveis).</summary>
public sealed class ChatModelException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>
/// Cliente HTTP para qualquer API compatível com o "Chat Completions" da OpenAI.
/// </summary>
public sealed class OpenAiCompatibleChatModel(
    HttpClient httpClient,
    IOptions<AnalyticsOptions> options,
    ILogger<OpenAiCompatibleChatModel> logger) : IChatModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ModelName => options.Value.Model ?? string.Empty;

    public async Task<ChatMessage> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var request = new ChatCompletionRequest(
            settings.Model!,
            messages,
            tools.Count > 0 ? tools : null,
            tools.Count > 0 ? "auto" : null,
            settings.Temperature,
            string.IsNullOrWhiteSpace(settings.ReasoningEffort) ? null : settings.ReasoningEffort);

        // Corpo serializado de antemão (com Content-Length): mais compatível com provedores e proxies
        // do que o envio em "chunked transfer encoding".
        var payload = JsonSerializer.Serialize(request, Json);
        var url = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";

        var stopwatch = Stopwatch.StartNew();
        var response = await SendWithRetryAsync(url, payload, settings, cancellationToken);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Provedor de IA respondeu {Status}: {Body}", (int)response.StatusCode, Truncate(body, 1000));

                var reason = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "chave de API inválida ou sem permissão",
                    HttpStatusCode.NotFound => "modelo ou endereço não encontrado",
                    HttpStatusCode.TooManyRequests => "limite de uso do provedor atingido",
                    HttpStatusCode.ServiceUnavailable => "modelo sobrecarregado no momento, mesmo após novas tentativas",
                    _ => $"erro {(int)response.StatusCode}"
                };

                // Inclui o motivo informado pelo provedor (ex.: "API key not valid", "model not found"),
                // que é o que realmente ajuda a corrigir a configuração. Não contém a chave.
                var providerMessage = ExtractErrorMessage(body);
                var detail = providerMessage is null ? string.Empty : $" Detalhe do provedor: {Truncate(providerMessage, 300)}";

                throw new ChatModelException($"O provedor de IA recusou a requisição ({reason}).{detail}", response.StatusCode);
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(Json, cancellationToken);
            var message = completion?.Choices?.FirstOrDefault()?.Message
                ?? throw new ChatModelException("O provedor de IA retornou uma resposta vazia.");

            logger.LogInformation(
                "Resposta do modelo {Model} em {Elapsed} ms ({ToolCalls} chamada(s) de ferramenta).",
                settings.Model, stopwatch.ElapsedMilliseconds, message.ToolCalls?.Count ?? 0);

            return message;
        }
    }

    /// <summary>
    /// Envia a requisição com novas tentativas automáticas em falhas temporárias do provedor
    /// (429 limite, 500/502/503 sobrecarga, 504 timeout), com espera crescente (backoff exponencial + jitter)
    /// e respeitando o cabeçalho Retry-After quando o provedor informa quanto esperar.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string url, string payload, AnalyticsOptions settings, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Uma HttpRequestMessage não pode ser reenviada: cria uma nova a cada tentativa.
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ChatModelException(
                    $"O provedor de IA demorou mais de {settings.TimeoutSeconds} s para responder. " +
                    "Para modelos com raciocínio (ex.: Gemini 3), defina AI_REASONING_EFFORT=low.",
                    HttpStatusCode.GatewayTimeout, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ChatModelException("Não foi possível conectar ao provedor de IA.", inner: ex);
            }

            if (!IsTransient(response.StatusCode) || attempt >= settings.MaxRetries + 1)
                return response;

            var delay = GetRetryDelay(response, attempt);
            logger.LogWarning(
                "Provedor de IA respondeu {Status} (tentativa {Attempt} de {Max}). Nova tentativa em {Delay} ms.",
                (int)response.StatusCode, attempt, settings.MaxRetries + 1, (int)delay.TotalMilliseconds);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode status) => status is
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.InternalServerError or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var maxDelay = TimeSpan.FromSeconds(10);

        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
            return retryAfter < maxDelay ? retryAfter : maxDelay;

        // 1 s, 2 s, 4 s... + até 250 ms aleatórios (evita que vários clientes tentem no mesmo instante).
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        return backoff < maxDelay ? backoff : maxDelay;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>
    /// Lê a mensagem de erro do provedor. Formatos aceitos:
    /// OpenAI/Groq/Ollama: { "error": { "message": "..." } } — Gemini: [ { "error": { "message": "..." } } ].
    /// </summary>
    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                root = root[0];

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();

                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Corpo não é JSON: ignora.
        }

        return null;
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        IReadOnlyList<ChatTool>? Tools,
        string? ToolChoice,
        double? Temperature,
        string? ReasoningEffort);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(ChatMessage? Message, string? FinishReason);
}