using RabbitMQ.Client;
using TmbOrders.Infrastructure.Messaging.Contracts;

namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Declara exchanges e filas. As declarações são idempotentes:
/// podem ser executadas por publicador e consumidor sem conflito.
///
///   orders.events (topic) --order.created--> orders.order-created (quorum)
///                                                  │  após N falhas
///                                                  ▼
///   orders.events.dlx (topic) --order.created--> orders.order-created.dlq
/// </summary>
public static class RabbitMqTopology
{
    public static string DeadLetterExchange(RabbitMqOptions options) => $"{options.Exchange}.dlx";

    public static string DeadLetterQueue(RabbitMqOptions options) => $"{options.OrderCreatedQueue}.dlq";

    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        var routingKey = EventTypes.RoutingKeyFor(EventTypes.OrderCreated);
        var deadLetterExchange = DeadLetterExchange(options);
        var deadLetterQueue = DeadLetterQueue(options);

        await channel.ExchangeDeclareAsync(
            exchange: options.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: deadLetterExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Fila de mensagens mortas: guarda o que não pôde ser processado, para análise.
        await channel.QueueDeclareAsync(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(deadLetterQueue, deadLetterExchange, routingKey, cancellationToken: cancellationToken);

        // Fila principal do tipo "quorum" (replicada e durável, padrão recomendado no RabbitMQ 4).
        // x-delivery-limit: após N entregas sem ack a mensagem vai para a DLQ, evitando loop infinito.
        await channel.QueueDeclareAsync(
            queue: options.OrderCreatedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = options.MaxDeliveryAttempts,
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = routingKey
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(options.OrderCreatedQueue, options.Exchange, routingKey, cancellationToken: cancellationToken);
    }
}
