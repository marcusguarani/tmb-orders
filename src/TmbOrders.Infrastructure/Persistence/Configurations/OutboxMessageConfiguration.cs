using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmbOrders.Infrastructure.Outbox;

namespace TmbOrders.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_mensagens");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.EventType).HasColumnName("tipo_evento").HasMaxLength(100).IsRequired();
        builder.Property(m => m.CorrelationId).HasColumnName("correlation_id");
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.TraceParent).HasColumnName("trace_parent").HasMaxLength(100);
        builder.Property(m => m.OccurredAt).HasColumnName("data_ocorrencia");
        builder.Property(m => m.ProcessedAt).HasColumnName("data_processamento");
        builder.Property(m => m.Attempts).HasColumnName("tentativas");
        builder.Property(m => m.LastError).HasColumnName("ultimo_erro").HasMaxLength(2000);

        // Índice parcial: o publicador só busca mensagens ainda não enviadas.
        builder.HasIndex(m => m.OccurredAt)
            .HasDatabaseName("ix_outbox_mensagens_pendentes")
            .HasFilter("data_processamento IS NULL");
    }
}
