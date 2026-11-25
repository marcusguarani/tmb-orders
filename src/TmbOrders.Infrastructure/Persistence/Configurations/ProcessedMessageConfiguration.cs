using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmbOrders.Infrastructure.Messaging;

namespace TmbOrders.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("mensagens_processadas");

        builder.HasKey(m => new { m.MessageId, m.Consumer });
        builder.Property(m => m.MessageId).HasColumnName("message_id");
        builder.Property(m => m.Consumer).HasColumnName("consumidor").HasMaxLength(100);
        builder.Property(m => m.ProcessedAt).HasColumnName("data_processamento");
    }
}
