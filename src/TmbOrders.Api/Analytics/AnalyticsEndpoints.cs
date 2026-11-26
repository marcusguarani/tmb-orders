using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace TmbOrders.Api.Analytics;

public sealed record AskRequest(string? Pergunta);

public sealed record AnalyticsStatusResponse(bool Configurado, string? Modelo, string? Provedor);

public static class AnalyticsEndpoints
{
    public const string RateLimitPolicy = "ai";

    /// <summary>Registra o módulo de IA: opções, cliente HTTP do provedor, ferramentas e limite de uso.</summary>
    public static IServiceCollection AddAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnalyticsOptions>()
            .Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .Validate(o => o.MaxToolRounds is > 0 and <= 10, "Ai:MaxToolRounds deve estar entre 1 e 10.");

        services.AddHttpClient<IChatModel, OpenAiCompatibleChatModel>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<AnalyticsOptions>>().Value.TimeoutSeconds));

        services.AddScoped<OrderAnalyticsTools>();
        services.AddScoped<OrderAssistant>();

        // Protege o custo da LLM: até 10 perguntas por minuto por IP.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics").WithTags("IA / Analytics");

        group.MapGet("/status", (IOptions<AnalyticsOptions> options) =>
            {
                var settings = options.Value;
                return TypedResults.Ok(new AnalyticsStatusResponse(
                    settings.IsConfigured,
                    settings.IsConfigured ? settings.Model : null,
                    settings.IsConfigured ? settings.ProviderName : null));
            })
            .WithName("StatusDaIA")
            .WithSummary("Indica se o módulo de IA está configurado");

        group.MapPost("/ask", AskAsync)
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("PerguntarSobrePedidos")
            .WithSummary("Responde perguntas em linguagem natural sobre os pedidos")
            .WithDescription("A LLM interpreta a pergunta, escolhe consultas seguras (function calling) e responde com dados reais do banco.");

        return app;
    }

    private static async Task<Results<Ok<AssistantAnswer>, ValidationProblem, ProblemHttpResult>> AskAsync(
        AskRequest request,
        IOptions<AnalyticsOptions> options,
        OrderAssistant assistant,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Módulo de IA não configurado.",
                detail: "Defina AI_BASE_URL, AI_API_KEY e AI_MODEL no arquivo .env e reinicie a API.");
        }

        var question = request.Pergunta?.Trim();
        if (string.IsNullOrWhiteSpace(question))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["pergunta"] = ["Digite uma pergunta."] });

        if (question.Length > OrderAssistant.MaxQuestionLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pergunta"] = [$"A pergunta deve ter no máximo {OrderAssistant.MaxQuestionLength} caracteres."]
            });
        }

        try
        {
            return TypedResults.Ok(await assistant.AskAsync(question, cancellationToken));
        }
        catch (ChatModelException ex)
        {
            return TypedResults.Problem(
                statusCode: ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                    ? StatusCodes.Status504GatewayTimeout
                    : StatusCodes.Status502BadGateway,
                title: "Falha ao consultar o provedor de IA.",
                detail: ex.Message);
        }
    }
}
