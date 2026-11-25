using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TmbOrders.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Aplica as migrations pendentes ao iniciar a aplicação (migrations automáticas).
    /// Somente a API chama este método, evitando que dois processos migrem ao mesmo tempo.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Banco de dados já está atualizado.");
            return;
        }

        logger.LogInformation("Aplicando {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migrations aplicadas com sucesso.");
    }
}
