namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Registro das mensagens já consumidas (Inbox / deduplicação).
/// A chave (MessageId, Consumer) impede que a mesma mensagem
/// seja processada duas vezes pelo mesmo consumidor.
/// </summary>
public sealed class ProcessedMessage
{
    public Guid MessageId { get; set; }

    public string Consumer { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}
