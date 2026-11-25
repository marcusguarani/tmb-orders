using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.Infrastructure.Persistence;

namespace TmbOrders.Worker.Processing;

public enum ProcessingResult
{
    Processed,
    Duplicate
}

/// <summary>
/// Regra de processamento do evento OrderCreated:
/// Pendente → Processando → (5 segundos) → Finalizado.
///
/// Idempotência em três camadas:
///  1. Tabela mensagens_processadas: MessageId já visto → ignora.
///  2. Regra de domínio: Order.TryAdvanceTo ignora transições repetidas.
///  3. Índice único (pedido_id, status) no histórico: duas instâncias
///     processando a mesma mensagem ao mesmo tempo → só uma grava.
/// </summary>
public sealed class OrderProcessor(
    OrdersDbContext db,
    TimeProvider timeProvider,
    IOptions<WorkerOptions> options,
    ILogger<OrderProcessor> logger)
{
    public const string ConsumerName = "order-created-processor";

    public async Task<ProcessingResult> ProcessAsync(
        MessageEnvelope<OrderCreatedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var messageId = envelope.MessageId;
        var orderId = envelope.CorrelationId;

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == ConsumerName, cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation("Mensagem {MessageId} já processada anteriormente. Ignorando.", messageId);
            return ProcessingResult.Duplicate;
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        try
        {
            // Etapa 1: Pendente → Processando
            if (order.TryAdvanceTo(OrderStatus.Processando, Now()))
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Pedido {OrderId}: Pendente → Processando.", orderId);
            }

            // Simula o processamento (só espera se o pedido ainda não estiver finalizado).
            if (order.Status != OrderStatus.Finalizado)
            {
                var delay = TimeSpan.FromSeconds(options.Value.ProcessingDelaySeconds);
                await Task.Delay(delay, timeProvider, cancellationToken);
            }

            // Etapa 2: Processando → Finalizado + registro de mensagem processada,
            // no mesmo SaveChanges (mesma transação).
            var finished = order.TryAdvanceTo(OrderStatus.Finalizado, Now());

            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                Consumer = ConsumerName,
                ProcessedAt = Now()
            });

            await db.SaveChangesAsync(cancellationToken);

            if (finished)
                logger.LogInformation("Pedido {OrderId}: Processando → Finalizado.", orderId);

            return ProcessingResult.Processed;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Outra instância processou a mesma mensagem ao mesmo tempo.
            logger.LogInformation("Mensagem {MessageId} processada em paralelo por outra instância. Ignorando.", messageId);
            return ProcessingResult.Duplicate;
        }
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
