namespace TmbOrders.Api.Analytics;

/// <summary>
/// Configuração do módulo de IA (seção "Ai"; no Docker: Ai__BaseUrl, Ai__ApiKey, Ai__Model).
/// Qualquer provedor com API compatível com a OpenAI funciona: OpenAI, Google Gemini, Groq, Ollama (local)...
/// </summary>
public sealed class AnalyticsOptions
{
    public const string SectionName = "Ai";

    /// <summary>Endereço base da API (ex.: https://api.openai.com/v1).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Chave da API. Opcional para provedores locais (Ollama).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo a ser usado. Sem modelo configurado, o módulo fica desativado.</summary>
    public string? Model { get; set; }

    /// <summary>Temperatura (opcional; alguns modelos não aceitam esse parâmetro).</summary>
    public double? Temperature { get; set; } = 0;

    /// <summary>
    /// Nível de raciocínio para modelos que "pensam" antes de responder (ex.: Gemini 3: "low", "medium", "high").
    /// Opcional: só é enviado quando definido, pois modelos sem raciocínio recusam o parâmetro.
    /// Para consultas simples, "low" deixa as respostas bem mais rápidas.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>Fuso horário usado para interpretar "hoje", "este mês" etc.</summary>
    public string TimeZone { get; set; } = "America/Sao_Paulo";

    /// <summary>Máximo de rodadas de chamadas de ferramenta por pergunta (evita loops).</summary>
    public int MaxToolRounds { get; set; } = 5;

    /// <summary>Novas tentativas automáticas em falhas temporárias do provedor (429, 5xx).</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Tempo máximo de espera por chamada ao provedor.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(BaseUrl);

    public string ProviderName =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : BaseUrl;
}