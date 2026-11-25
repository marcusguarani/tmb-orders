namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Configurações do RabbitMQ, lidas da seção "RabbitMq"
/// (no Docker: variáveis de ambiente RabbitMq__HostName, RabbitMq__Password etc.).
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    /// <summary>Exchange principal onde os eventos de pedido são publicados.</summary>
    public string Exchange { get; set; } = "orders.events";

    /// <summary>Fila consumida pelo worker.</summary>
    public string OrderCreatedQueue { get; set; } = "orders.order-created";

    /// <summary>Quantas mensagens o consumidor pode receber sem ainda ter confirmado (ack).</summary>
    public ushort PrefetchCount { get; set; } = 20;

    /// <summary>Quantas mensagens o worker processa em paralelo.</summary>
    public ushort ConsumerConcurrency { get; set; } = 5;

    /// <summary>
    /// Após esse número de entregas sem sucesso, o RabbitMQ move a mensagem
    /// para a fila de mensagens mortas (DLQ).
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;
}
