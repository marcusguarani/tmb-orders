namespace TmbOrders.Infrastructure.Messaging.Contracts;

/// <summary>
/// Envelope padrão de todas as mensagens publicadas.
/// Os metadados (MessageId, EventType, CorrelationId) também vão nas
/// propriedades/headers AMQP, mas ficam no corpo para facilitar auditoria.
/// </summary>
public sealed record MessageEnvelope<T>(
    Guid MessageId,
    string EventType,
    Guid CorrelationId,
    DateTime OccurredAt,
    T Data);
