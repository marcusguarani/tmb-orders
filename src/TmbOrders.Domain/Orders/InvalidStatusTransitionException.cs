namespace TmbOrders.Domain.Orders;

/// <summary>
/// Lançada quando alguém tenta pular uma etapa da sequência de status
/// (por exemplo, Pendente → Finalizado).
/// </summary>
public sealed class InvalidStatusTransitionException(Guid orderId, OrderStatus current, OrderStatus requested)
    : InvalidOperationException(
        $"Transição de status inválida no pedido {orderId}: {current} → {requested}. " +
        "A sequência obrigatória é Pendente → Processando → Finalizado.")
{
    public Guid OrderId { get; } = orderId;

    public OrderStatus Current { get; } = current;

    public OrderStatus Requested { get; } = requested;
}
