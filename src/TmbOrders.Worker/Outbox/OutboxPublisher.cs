using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.Infrastructure.Outbox;
using TmbOrders.Infrastructure.Persistence;

namespace TmbOrders.Worker.Outbox;

/// <summary>
/// Publicador do Outbox Pattern.
/// A cada ciclo lê as mensagens pendentes, publica no RabbitMQ (com confirmação do broker)
/// e marca como processadas. Se o RabbitMQ estiver fora do ar, as mensagens continuam
/// no banco e são enviadas quando ele voltar: nenhum evento se perde.
///
/// Garantia: "at-least-once" (pelo menos uma vez). Em uma falha entre publicar e
/// marcar como enviada, a mensagem pode ser publicada de novo; por isso o consumidor é idempotente.
/// </summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<WorkerOptions> workerOptions,
    OutboxSignal outboxSignal,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(workerOptions.Value.OutboxPollingIntervalMs);

        logger.LogInformation("Publicador do outbox iniciado (intervalo de {Interval} ms).", interval.TotalMilliseconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var published = await PublishPendingAsync(stoppingToken);
                    if (published > 0)
                        logger.LogInformation("{Count} mensagem(ns) do outbox publicada(s).", published);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Falha ao publicar mensagens do outbox. Nova tentativa no próximo ciclo.");
                    await ResetChannelAsync();
                }

                // Próximo ciclo: no intervalo configurado, ou antes, se o worker avisar que gravou um evento.
                await outboxSignal.WaitAsync(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do serviço.
        }
    }

    private async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var batchSize = workerOptions.Value.OutboxBatchSize;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        // Transações explícitas precisam rodar dentro da estratégia de retry do EF.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // FOR UPDATE SKIP LOCKED: se houver mais de um worker rodando, cada um
            // pega um lote diferente, sem publicar a mesma mensagem em paralelo.
            var messages = await db.OutboxMessages
                .FromSql($"""
                    SELECT * FROM outbox_mensagens
                    WHERE data_processamento IS NULL
                    ORDER BY data_ocorrencia
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            var published = 0;

            foreach (var message in messages)
            {
                try
                {
                    await PublishAsync(channel, message, cancellationToken);
                    message.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
                    published++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Attempts++;
                    message.LastError = Truncate(ex.Message, 2000);

                    logger.LogWarning(ex,
                        "Falha ao publicar a mensagem {MessageId} (tentativa {Attempt}).",
                        message.Id, message.Attempts);

                    // Provavelmente o broker caiu: interrompe o lote e tenta no próximo ciclo.
                    await ResetChannelAsync();
                    break;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return published;
        });
    }

    private async Task PublishAsync(IChannel channel, OutboxMessage message, CancellationToken cancellationToken)
    {
        var exchange = rabbitOptions.Value.Exchange;
        var routingKey = EventTypes.RoutingKeyFor(message.EventType);

        // Span "publish" continua o trace de quem gravou o evento (ex.: a requisição POST /orders).
        using var activity = MessagingTelemetry.StartPublish(
            message.EventType, exchange, routingKey, message.Id, message.CorrelationId, message.TraceParent);

        var occurredAtUtc = DateTime.SpecifyKind(message.OccurredAt, DateTimeKind.Utc);

        // Requisito do desafio: CorrelationId = OrderId e EventType = OrderCreated.
        var headers = new Dictionary<string, object?>
        {
            ["EventType"] = message.EventType,
            ["CorrelationId"] = message.CorrelationId.ToString()
        };

        // Propaga o contexto do trace para o consumidor (padrão W3C Trace Context).
        var traceParent = activity?.Id ?? message.TraceParent;
        if (!string.IsNullOrEmpty(traceParent))
            headers[MessagingTelemetry.TraceParentHeader] = traceParent;

        var properties = new BasicProperties
        {
            MessageId = message.Id.ToString(),
            CorrelationId = message.CorrelationId.ToString(),
            Type = message.EventType,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(occurredAtUtc).ToUnixTimeSeconds()),
            Headers = headers
        };

        try
        {
            // Com "publisher confirms" ativado, este await só termina quando o
            // RabbitMQ confirma que recebeu e gravou a mensagem (ou lança exceção).
            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: EventTypes.IsMandatory(message.EventType),
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            MessagingTelemetry.RecordError(activity, ex);
            throw;
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclareAsync(_channel, rabbitOptions.Value, cancellationToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is null)
            return;

        try
        {
            await _channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Erro ao descartar o canal do publicador.");
        }

        _channel = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await ResetChannelAsync();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
