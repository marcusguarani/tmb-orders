using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TmbOrders.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    /// <summary>Fontes de tracing próprias da aplicação (ex.: TmbOrders.Messaging).</summary>
    private const string ApplicationSources = "TmbOrders.*";

    /// <summary>
    /// Configurações comuns a todos os serviços .NET do sistema.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // "self": indica apenas que o processo está de pé (liveness).
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Processo em execução."), [LiveTag]);

        builder.AddTracing();

        return builder;
    }

    /// <summary>
    /// Tracing distribuído com OpenTelemetry:
    ///  - requisições HTTP recebidas (ASP.NET Core), exceto health checks;
    ///  - comandos no PostgreSQL (Npgsql);
    ///  - publicação e consumo de mensagens (spans próprios em TmbOrders.Messaging).
    /// Exporta via OTLP quando OTEL_EXPORTER_OTLP_ENDPOINT está definido (no Docker: o Jaeger).
    /// </summary>
    private static void AddTracing(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        // Logs passam a carregar TraceId/SpanId: dá para ir do log direto ao trace no Jaeger.
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new SkipRootClientSpansSampler())
                    .AddSource(ApplicationSources)
                    // Chamadas HTTP de saída (ex.: provedor de IA), instrumentadas nativamente no .NET 9+.
                    .AddSource("System.Net.Http")
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddNpgsql();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter();
            });
    }

    /// <summary>
    /// Expõe os endpoints de health check:
    ///  /health/live  → o processo está vivo?
    ///  /health/ready → as dependências (postgres, rabbitmq) estão ok?
    ///  /health       → relatório completo
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        return app;
    }
}
