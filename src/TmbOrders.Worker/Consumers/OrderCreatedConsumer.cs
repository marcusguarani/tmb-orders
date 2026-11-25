using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.Worker.Processing;

namespace TmbOrders.Worker.Consumers;

/// <summary>
/// Consome a fila orders.order-created.
///
/// Estratégia de confirmação (ack manual):
///  - sucesso ou duplicada         → ack (remove da fila)
///  - mensagem inválida / pedido inexistente → nack sem requeue (vai direto para a DLQ)
///  - erro temporário (ex.: banco fora) → nack com requeue (nova tentativa);
///    após MaxDeliveryAttempts entregas, o RabbitMQ move para a DLQ.
/// </summary>
public sealed class OrderCreatedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);

                // Mantém o serviço vivo enquanto o canal estiver aberto.
                // (A reconexão automática do client recupera quedas rápidas da conexão.)
                while (_channel is { IsOpen: true })
                    await Task.Delay(RetryDelay, stoppingToken);

                logger.LogWarning("Canal do consumidor fechado. Reconectando...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no consumidor. Nova tentativa em {Delay}s.", RetryDelay.TotalSeconds);
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

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var options = rabbitOptions.Value;
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);

        await DisposeChannelAsync();
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _channel = channel;

        await RabbitMqTopology.DeclareAsync(channel, options, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: options.OrderCreatedQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Consumindo a fila {Queue} (prefetch {Prefetch}, paralelismo {Concurrency}).",
            options.OrderCreatedQueue, options.PrefetchCount, options.ConsumerConcurrency);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        MessageEnvelope<OrderCreatedEvent>? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<MessageEnvelope<OrderCreatedEvent>>(delivery.Body.Span, MessagingJson.Options);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Mensagem com JSON inválido (MessageId {MessageId}). Enviando para a DLQ.",
                delivery.BasicProperties.MessageId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        if (envelope is null || envelope.EventType != EventTypes.OrderCreated)
        {
            logger.LogError("Mensagem inesperada (MessageId {MessageId}, tipo {Type}). Enviando para a DLQ.",
                delivery.BasicProperties.MessageId, envelope?.EventType);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = envelope.MessageId,
            ["CorrelationId"] = envelope.CorrelationId,
            ["EventType"] = envelope.EventType
        });

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();

            await processor.ProcessAsync(envelope, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OrderNotFoundException ex)
        {
            logger.LogError(ex, "Pedido {OrderId} não existe. Enviando mensagem para a DLQ.", ex.OrderId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Worker desligando: sem ack, o RabbitMQ reentrega a mensagem para outra instância/na próxima subida.
            logger.LogInformation("Processamento interrompido pelo desligamento do worker.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar o pedido {OrderId}. A mensagem será reentregue.", envelope.CorrelationId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
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
            logger.LogDebug(ex, "Erro ao descartar o canal do consumidor.");
        }

        _channel = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await DisposeChannelAsync();
    }
}
