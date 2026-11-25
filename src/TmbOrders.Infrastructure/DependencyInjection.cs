using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Persistence;

namespace TmbOrders.Infrastructure;

public static class DependencyInjection
{
    public const string MigrationsHistoryTable = "__ef_migrations_historico";

    /// <summary>
    /// Registra banco de dados, mensageria e os health checks de "readiness"
    /// (postgres + rabbitmq), usados tanto pela API quanto pelo Worker.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' não configurada. Defina a variável ConnectionStrings__Postgres.");

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable);
                npgsql.EnableRetryOnFailure(maxRetryCount: 5);
            }));

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HostName), "RabbitMq:HostName é obrigatório.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName), "RabbitMq:UserName é obrigatório.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "RabbitMq:Password é obrigatório.")
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();

        // Relógio injetável: permite controlar o tempo nos testes.
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddDbContextCheck<OrdersDbContext>("postgres", HealthStatus.Unhealthy, ["ready", "db"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", HealthStatus.Unhealthy, ["ready", "messaging"]);

        return services;
    }
}
