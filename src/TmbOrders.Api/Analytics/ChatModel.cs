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
    public static ChatMessage System(string content) => new("system", content);
    public static ChatMessage User(string content) => new("user", content);
    public static ChatMessage Tool(string toolCallId, string content) => new("tool", content, ToolCallId: toolCallId);
}

public sealed record ChatToolCall(string? Id, string Type, ChatFunctionCall Function);

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
            settings.Temperature);

        // Corpo serializado de antemão (com Content-Length): mais compatível com provedores e proxies
        // do que o envio em "chunked transfer encoding".
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, Json), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChatModelException("O provedor de IA demorou demais para responder.", HttpStatusCode.GatewayTimeout, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatModelException("Não foi possível conectar ao provedor de IA.", inner: ex);
        }

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
                    _ => $"erro {(int)response.StatusCode}"
                };

                throw new ChatModelException($"O provedor de IA recusou a requisição ({reason}).", response.StatusCode);
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

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        IReadOnlyList<ChatTool>? Tools,
        string? ToolChoice,
        double? Temperature);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(ChatMessage? Message, string? FinishReason);
}
