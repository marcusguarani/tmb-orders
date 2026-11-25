namespace TmbOrders.Domain.Orders;

/// <summary>
/// Registro de cada mudança de status de um pedido (linha do tempo).
/// </summary>
public sealed class OrderStatusHistory
{
    // Construtor usado pelo EF Core.
    private OrderStatusHistory() { }

    internal OrderStatusHistory(Guid orderId, OrderStatus status, DateTime changedAtUtc)
    {
        OrderId = orderId;
        Status = status;
        ChangedAt = changedAtUtc;
    }

    public long Id { get; private set; }

    public Guid OrderId { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime ChangedAt { get; private set; }
}
