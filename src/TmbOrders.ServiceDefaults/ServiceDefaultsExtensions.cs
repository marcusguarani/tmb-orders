using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace TmbOrders.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    /// <summary>
    /// Configurações comuns a todos os serviços .NET do sistema.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // "self": indica apenas que o processo está de pé (liveness).
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Processo em execução."), [LiveTag]);

        return builder;
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
