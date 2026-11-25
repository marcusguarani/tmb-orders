using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TmbOrders.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pela ferramenta "dotnet ef" para gerar migrations.
/// Não abre conexão com o banco; a string abaixo é só um valor local de exemplo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=tmb_orders;Username=design;Password=design",
                npgsql => npgsql.MigrationsHistoryTable(DependencyInjection.MigrationsHistoryTable))
            .Options;

        return new OrdersDbContext(options);
    }
}
