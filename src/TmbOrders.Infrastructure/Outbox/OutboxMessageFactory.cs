using System.Diagnostics;
using System.Text.Json;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;

namespace TmbOrders.Infrastructure.Outbox;

public static class OutboxMessageFactory
{
    /// <summary>
    /// Cria a mensagem de outbox com o envelope completo já serializado.
    /// O Id da mensagem do outbox é o mesmo MessageId enviado ao RabbitMQ,
    /// o que permite ao consumidor deduplicar com segurança.
    /// </summary>
    public static OutboxMessage Create<T>(string eventType, Guid correlationId, T data, DateTime nowUtc)
    {
        var messageId = Guid.CreateVersion7();
        var envelope = new MessageEnvelope<T>(messageId, eventType, correlationId, nowUtc, data);

        return new OutboxMessage
        {
            Id = messageId,
            EventType = eventType,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(envelope, MessagingJson.Options),
            TraceParent = Activity.Current?.Id,
            OccurredAt = nowUtc
        };
    }
}
