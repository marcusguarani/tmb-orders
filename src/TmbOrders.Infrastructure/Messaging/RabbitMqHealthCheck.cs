using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Health check da mensageria: verifica se existe conexão aberta com o RabbitMQ.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            return connection.IsOpen
                ? HealthCheckResult.Healthy("Conexão com o RabbitMQ aberta.")
                : new HealthCheckResult(context.Registration.FailureStatus, "Conexão com o RabbitMQ fechada.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Não foi possível conectar ao RabbitMQ.",
                ex);
        }
    }
}
