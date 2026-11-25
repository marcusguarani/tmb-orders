using Microsoft.AspNetCore.SignalR;
using TmbOrders.Api.Orders;
using TmbOrders.Domain.Orders;

namespace TmbOrders.Api.Realtime;

/// <summary>
/// Hub de tempo real. O servidor apenas envia eventos; o cliente só escuta.
/// Rota: /hubs/orders (no frontend: /api/hubs/orders, via proxy do Nginx/Vite).
/// </summary>
public sealed class OrdersHub : Hub
{
    public const string Route = "/hubs/orders";

    /// <summary>Nomes dos eventos recebidos pelo cliente.</summary>
    public static class Events
    {
        public const string OrderCreated = "OrderCreated";
        public const string OrderStatusChanged = "OrderStatusChanged";
    }
}

/// <summary>Payload enviado ao navegador quando o status de um pedido muda.</summary>
public sealed record OrderStatusChangedNotification(
    Guid Id,
    OrderStatus PreviousStatus,
    OrderStatus Status,
    DateTime ChangedAt);

/// <summary>Envio das notificações para todos os clientes conectados.</summary>
public sealed class OrderNotifier(IHubContext<OrdersHub> hub, ILogger<OrderNotifier> logger)
{
    public Task OrderCreatedAsync(OrderResponse order, CancellationToken cancellationToken) =>
        SendAsync(OrdersHub.Events.OrderCreated, order, cancellationToken);

    public Task StatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken) =>
        SendAsync(OrdersHub.Events.OrderStatusChanged, notification, cancellationToken);

    private async Task SendAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.All.SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Notificação é "melhor esforço": uma falha aqui nunca pode quebrar a requisição.
            // O frontend se recupera sozinho via polling.
            logger.LogWarning(ex, "Falha ao enviar o evento {Event} via SignalR.", eventName);
        }
    }
}
