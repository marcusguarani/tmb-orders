namespace TmbOrders.Infrastructure.Outbox;

/// <summary>
/// Mensagem gravada na MESMA transação do pedido (Outbox Pattern).
/// Um publicador em background lê as pendentes e envia ao RabbitMQ,
/// garantindo que nenhum evento se perca se a fila estiver fora do ar.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Tipo do evento (ex.: OrderCreated).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Id de correlação (= OrderId).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Conteúdo do evento serializado em JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Contexto de tracing W3C (traceparent) para rastreio ponta a ponta.</summary>
    public string? TraceParent { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
