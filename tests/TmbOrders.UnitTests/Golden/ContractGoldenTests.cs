using System.Text.Json;
using TmbOrders.Api;
using TmbOrders.Api.Orders;
using TmbOrders.Api.Realtime;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging;
using TmbOrders.Infrastructure.Messaging.Contracts;

namespace TmbOrders.UnitTests.Golden;

/// <summary>
/// Congela o formato JSON dos contratos públicos do sistema:
///  - respostas HTTP da API (consumidas pelo frontend);
///  - mensagens publicadas no RabbitMQ (consumidas pelo worker e pela API);
///  - eventos enviados via SignalR (consumidos pelo frontend).
/// Valores fixos (ids, datas) garantem uma saída determinística.
/// </summary>
public sealed class ContractGoldenTests
{
    private static readonly Guid OrderId = Guid.Parse("01938b2a-7c00-7000-8000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("01938b2a-7c00-7000-8000-0000000000aa");
    private static readonly DateTime CreatedAt = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ProcessingAt = CreatedAt.AddSeconds(1);
    private static readonly DateTime FinishedAt = CreatedAt.AddSeconds(6);
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly JsonSerializerOptions ApiJson = Indented(ApiJsonOptions.Create());
    private static readonly JsonSerializerOptions MessageJson = Indented(MessagingJson.Options);

    private static JsonSerializerOptions Indented(JsonSerializerOptions source) =>
        new(source) { WriteIndented = true };

    [Fact]
    public void Api_OrderResponse()
    {
        var response = new OrderResponse(
            OrderId, "João da Silva", "Notebook Dell Inspiron 15", 4599.90m,
            OrderStatus.Pendente, CreatedAt, null);

        GoldenFile.AssertMatches("api-order-response", JsonSerializer.Serialize(response, ApiJson));
    }

    [Fact]
    public void Api_OrderDetailsResponse()
    {
        var response = new OrderDetailsResponse(
            OrderId, "João da Silva", "Notebook Dell Inspiron 15", 4599.90m,
            OrderStatus.Finalizado, CreatedAt, FinishedAt,
            [
                new OrderStatusHistoryResponse(OrderStatus.Pendente, CreatedAt),
                new OrderStatusHistoryResponse(OrderStatus.Processando, ProcessingAt),
                new OrderStatusHistoryResponse(OrderStatus.Finalizado, FinishedAt)
            ],
            TraceId);

        GoldenFile.AssertMatches("api-order-details-response", JsonSerializer.Serialize(response, ApiJson));
    }

    [Fact]
    public void Api_ValidationErrors()
    {
        var errors = CreateOrderRequestValidator.Validate(new CreateOrderRequest("", null, -5m));

        GoldenFile.AssertMatches("api-validation-errors", JsonSerializer.Serialize(errors, ApiJson));
    }

    [Fact]
    public void Message_OrderCreated()
    {
        var envelope = new MessageEnvelope<OrderCreatedEvent>(
            MessageId, EventTypes.OrderCreated, OrderId, CreatedAt,
            new OrderCreatedEvent(OrderId, "João da Silva", "Notebook Dell Inspiron 15", 4599.90m, "Pendente", CreatedAt));

        GoldenFile.AssertMatches("message-order-created", JsonSerializer.Serialize(envelope, MessageJson));
    }

    [Fact]
    public void Message_OrderStatusChanged()
    {
        var envelope = new MessageEnvelope<OrderStatusChangedEvent>(
            MessageId, EventTypes.OrderStatusChanged, OrderId, FinishedAt,
            new OrderStatusChangedEvent(OrderId, "Processando", "Finalizado", FinishedAt));

        GoldenFile.AssertMatches("message-order-status-changed", JsonSerializer.Serialize(envelope, MessageJson));
    }

    [Fact]
    public void SignalR_OrderStatusChangedNotification()
    {
        var notification = new OrderStatusChangedNotification(
            OrderId, OrderStatus.Processando, OrderStatus.Finalizado, FinishedAt);

        GoldenFile.AssertMatches("signalr-order-status-changed", JsonSerializer.Serialize(notification, ApiJson));
    }
}
