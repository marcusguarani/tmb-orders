using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmbOrders.Api.Orders;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.IntegrationTests.Infrastructure;

namespace TmbOrders.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class OrdersApiTests(IntegrationTestEnvironment env)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_DeveCriarPedidoPendente_ComHistoricoEOutboxNaMesmaTransacao()
    {
        var created = await env.CreateOrderAsync("Ana Costa", "Teclado mecânico", 689.50m);

        Assert.Equal(OrderStatus.Pendente, created.Status);
        Assert.Equal("Ana Costa", created.Cliente);
        Assert.Equal(689.50m, created.Valor);

        await using var db = env.CreateDbContext();

        var order = await db.Orders.Include(o => o.StatusHistory).SingleAsync(o => o.Id == created.Id, Ct);
        Assert.Contains(order.StatusHistory, h => h.Status == OrderStatus.Pendente);

        // Requisitos da mensagem: CorrelationId = OrderId e EventType = OrderCreated.
        var outbox = await db.OutboxMessages
            .SingleAsync(m => m.CorrelationId == created.Id && m.EventType == EventTypes.OrderCreated, Ct);

        var envelope = JsonSerializer.Deserialize<MessageEnvelope<OrderCreatedEvent>>(outbox.Payload, MessagingJson.Options)!;
        Assert.Equal(outbox.Id, envelope.MessageId);
        Assert.Equal(created.Id, envelope.CorrelationId);
        Assert.Equal(EventTypes.OrderCreated, envelope.EventType);
        Assert.Equal(created.Id, envelope.Data.OrderId);
    }

    [Fact]
    public async Task Post_ComDadosInvalidos_DeveRetornar400ComErrosPorCampo()
    {
        var response = await env.Client.PostAsJsonAsync("/orders", new { cliente = "", valor = -1 }, env.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.NotNull(problem);
        Assert.Contains("cliente", problem.Errors.Keys);
        Assert.Contains("produto", problem.Errors.Keys);
        Assert.Contains("valor", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetPorId_Inexistente_DeveRetornar404()
    {
        var response = await env.Client.GetAsync($"/orders/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLista_DeveConterOPedidoCriado()
    {
        var created = await env.CreateOrderAsync("Lista Teste", "Mouse", 99.90m);

        var orders = await env.Client.GetFromJsonAsync<List<OrderResponse>>("/orders", env.Json, Ct);

        Assert.NotNull(orders);
        Assert.Contains(orders, o => o.Id == created.Id);
    }

    [Fact]
    public async Task Health_DeveReportarApiBancoEMensageriaSaudaveis()
    {
        var response = await env.Client.GetAsync("/health", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"postgres\"", body);
        Assert.Contains("\"rabbitmq\"", body);
    }
}
