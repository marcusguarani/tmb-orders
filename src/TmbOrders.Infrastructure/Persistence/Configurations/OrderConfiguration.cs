using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmbOrders.Domain.Orders;

namespace TmbOrders.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento da tabela "pedidos". As colunas seguem os nomes do enunciado
/// (id, cliente, produto, valor, status, data_criacao).
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("pedidos");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(o => o.Customer)
            .HasColumnName("cliente")
            .HasMaxLength(Order.CustomerMaxLength)
            .IsRequired();

        builder.Property(o => o.Product)
            .HasColumnName("produto")
            .HasMaxLength(Order.ProductMaxLength)
            .IsRequired();

        builder.Property(o => o.Amount)
            .HasColumnName("valor")
            .HasPrecision(18, 2);

        // Status gravado como texto ("Pendente", "Processando", "Finalizado"):
        // mais legível no banco, no pgAdmin e nas consultas do módulo de IA.
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.CreatedAt).HasColumnName("data_criacao");
        builder.Property(o => o.UpdatedAt).HasColumnName("data_atualizacao");

        // Id do trace (OpenTelemetry) da requisição que criou o pedido.
        // "Shadow property": dado técnico de observabilidade, fica fora da entidade de domínio.
        builder.Property<string?>(OrderShadowProperties.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(32);

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => o.Status).HasDatabaseName("ix_pedidos_status");
        builder.HasIndex(o => o.CreatedAt).HasDatabaseName("ix_pedidos_data_criacao");
    }
}
