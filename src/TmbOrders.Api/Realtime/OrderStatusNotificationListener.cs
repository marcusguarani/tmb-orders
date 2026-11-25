using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;

namespace TmbOrders.Api.Realtime;

/// <summary>
/// Escuta os eventos OrderStatusChanged publicados pelo worker e repassa
/// para os navegadores conectados via SignalR.
///
/// Cada instância da API cria a SUA fila temporária (exclusiva, apagada ao desconectar).
/// Assim, se houver várias instâncias da API, todas recebem todos os eventos
/// e cada uma avisa os seus próprios clientes conectados.
/// </summary>
public sealed class OrderStatusNotificationListener(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> rabbitOptions,
    OrderNotifier notifier,
    ILogger<OrderStatusNotificationListener> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartListeningAsync(stoppingToken);

                while (_channel is { IsOpen: true })
                    await Task.Delay(RetryDelay, stoppingToken);

                logger.LogWarning("Canal de notificações fechado. Reconectando...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no listener de notificações. Nova tentativa em {Delay}s.", RetryDelay.TotalSeconds);
                await DisposeChannelAsync();

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task StartListeningAsync(CancellationToken stoppingToken)
    {
        var options = rabbitOptions.Value;
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);

        await DisposeChannelAsync();
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _channel = channel;

        await RabbitMqTopology.DeclareAsync(channel, options, stoppingToken);

        // Fila com nome gerado pelo servidor, exclusiva desta conexão e apagada automaticamente.
        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue.QueueName,
            options.Exchange,
            EventTypes.RoutingKeyFor(EventTypes.OrderStatusChanged),
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(queue.QueueName, delivery, stoppingToken);

        // autoAck: notificações são efêmeras; se uma se perder, o frontend se recupera via polling.
        await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

        logger.LogInformation("Escutando mudanças de status para notificação em tempo real (fila {Queue}).", queue.QueueName);
    }

    private async Task HandleAsync(string queueName, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        // Continua o trace do pedido: a notificação em tempo real aparece no mesmo trace do Jaeger.
        using var activity = MessagingTelemetry.StartProcess(queueName, delivery.BasicProperties);

        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope<OrderStatusChangedEvent>>(
                delivery.Body.Span, MessagingJson.Options);

            if (envelope?.Data is not { } data
                || !Enum.TryParse<OrderStatus>(data.Status, out var status)
                || !Enum.TryParse<OrderStatus>(data.PreviousStatus, out var previousStatus))
            {
                logger.LogWarning("Notificação de status inválida ignorada (MessageId {MessageId}).",
                    delivery.BasicProperties.MessageId);
                return;
            }

            await notifier.StatusChangedAsync(
                new OrderStatusChangedNotification(data.OrderId, previousStatus, status, data.ChangedAt),
                cancellationToken);
        }
        catch (JsonException ex)
        {
            MessagingTelemetry.RecordError(activity, ex);
            logger.LogWarning(ex, "Notificação com JSON inválido ignorada.");
        }
    }

    private async Task DisposeChannelAsync()
    {
        if (_channel is null)
            return;

        try
        {
            await _channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Erro ao descartar o canal de notificações.");
        }

        _channel = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await DisposeChannelAsync();
    }
}
