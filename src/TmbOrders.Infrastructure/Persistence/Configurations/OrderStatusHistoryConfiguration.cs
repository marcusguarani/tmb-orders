using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmbOrders.Domain.Orders;

namespace TmbOrders.Infrastructure.Persistence.Configurations;

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("pedidos_historico_status");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(h => h.OrderId).HasColumnName("pedido_id");

        builder.Property(h => h.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.ChangedAt).HasColumnName("data_alteracao");

        // Cada status só pode aparecer UMA vez por pedido.
        // Segunda barreira de idempotência, direto no banco.
        builder.HasIndex(h => new { h.OrderId, h.Status })
            .IsUnique()
            .HasDatabaseName("ux_pedidos_historico_status_pedido_status");
    }
}
