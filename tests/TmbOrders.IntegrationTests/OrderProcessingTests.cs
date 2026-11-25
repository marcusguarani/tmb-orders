using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TmbOrders.Api.Orders;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.IntegrationTests.Infrastructure;
using TmbOrders.Worker.Processing;

namespace TmbOrders.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class OrderProcessingTests(IntegrationTestEnvironment env)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PedidoCriado_DevePassarPorTodoOFluxo_AteFinalizado()
    {
        // API → outbox → RabbitMQ → Worker → banco, tudo real.
        var created = await env.CreateOrderAsync("Fluxo Completo", "Monitor", 1349m);

        var details = await WaitForStatusAsync(created.Id, OrderStatus.Finalizado);

        Assert.Equal(
            new[] { OrderStatus.Pendente, OrderStatus.Processando, OrderStatus.Finalizado },
            details.Historico.Select(h => h.Status));

        // A sequência de datas também precisa ser crescente.
        var dates = details.Historico.Select(h => h.Data).ToList();
        Assert.Equal(dates.Order(), dates);

        await using var db = env.CreateDbContext();

        // OrderCreated + 2 notificações de mudança de status, todas registradas no outbox.
        var outbox = await db.OutboxMessages.Where(m => m.CorrelationId == created.Id).ToListAsync(Ct);
        var orderCreated = Assert.Single(outbox, m => m.EventType == EventTypes.OrderCreated);
        Assert.Equal(2, outbox.Count(m => m.EventType == EventTypes.OrderStatusChanged));

        // O consumidor registrou a mensagem OrderCreated como processada (idempotência).
        Assert.True(await db.ProcessedMessages.AnyAsync(
            m => m.MessageId == orderCreated.Id && m.Consumer == OrderProcessor.ConsumerName, Ct));

        // E o publicador acabou enviando todas as mensagens do outbox para o RabbitMQ.
        await WaitUntilAsync(async () =>
        {
            await using var check = env.CreateDbContext();
            return !await check.OutboxMessages.AnyAsync(m => m.CorrelationId == created.Id && m.ProcessedAt == null, Ct);
        });
    }

    [Fact]
    public async Task MensagemDuplicada_DeveSerProcessadaUmaUnicaVez()
    {
        var (orderId, envelope) = await CreateOrderDirectlyAsync();

        await using (var scope = env.Worker.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
            Assert.Equal(ProcessingResult.Processed, await processor.ProcessAsync(envelope, Ct));
        }

        // Mesma mensagem entregue de novo (ex.: redelivery do RabbitMQ).
        await using (var scope = env.Worker.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
            Assert.Equal(ProcessingResult.Duplicate, await processor.ProcessAsync(envelope, Ct));
        }

        await AssertProcessedExactlyOnceAsync(orderId);
    }

    [Fact]
    public async Task MensagemDuplicadaEmParalelo_SomenteUmaInstanciaDeveProcessar()
    {
        var (orderId, envelope) = await CreateOrderDirectlyAsync();

        // Duas "instâncias" do worker recebendo a mesma mensagem ao mesmo tempo.
        async Task<ProcessingResult> ProcessAsync()
        {
            await using var scope = env.Worker.Services.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
            return await processor.ProcessAsync(envelope, Ct);
        }

        var results = await Task.WhenAll(ProcessAsync(), ProcessAsync());

        Assert.Equal(1, results.Count(r => r == ProcessingResult.Processed));
        Assert.Equal(1, results.Count(r => r == ProcessingResult.Duplicate));
        await AssertProcessedExactlyOnceAsync(orderId);
    }

    /// <summary>
    /// Cria o pedido direto no banco (sem outbox), para o teste controlar
    /// manualmente quantas vezes a mensagem é entregue ao processador.
    /// </summary>
    private async Task<(Guid OrderId, MessageEnvelope<OrderCreatedEvent> Envelope)> CreateOrderDirectlyAsync()
    {
        var order = Order.Create("Idempotência", "Cadeira", 2190m, DateTime.UtcNow);

        await using (var db = env.CreateDbContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(Ct);
        }

        var envelope = new MessageEnvelope<OrderCreatedEvent>(
            Guid.CreateVersion7(), EventTypes.OrderCreated, order.Id, order.CreatedAt,
            new OrderCreatedEvent(order.Id, order.Customer, order.Product, order.Amount, order.Status.ToString(), order.CreatedAt));

        return (order.Id, envelope);
    }

    private async Task AssertProcessedExactlyOnceAsync(Guid orderId)
    {
        await using var db = env.CreateDbContext();
        var order = await db.Orders.Include(o => o.StatusHistory).SingleAsync(o => o.Id == orderId, Ct);

        Assert.Equal(OrderStatus.Finalizado, order.Status);
        Assert.Equal(3, order.StatusHistory.Count);
        Assert.Equal(3, order.StatusHistory.Select(h => h.Status).Distinct().Count());
        Assert.Equal(2, await db.OutboxMessages.CountAsync(
            m => m.CorrelationId == orderId && m.EventType == EventTypes.OrderStatusChanged, Ct));
    }

    private async Task<OrderDetailsResponse> WaitForStatusAsync(Guid id, OrderStatus expected)
    {
        OrderDetailsResponse? details = null;

        await WaitUntilAsync(async () =>
        {
            details = await env.Client.GetFromJsonAsync<OrderDetailsResponse>($"/orders/{id}", env.Json, Ct);
            return details?.Status == expected;
        });

        return details!;
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(250, Ct);
        }

        Assert.Fail($"A condição não foi atendida em {Timeout.TotalSeconds} s.");
    }
}
