namespace TmbOrders.Worker.Processing;

/// <summary>
/// Erro permanente: a mensagem referencia um pedido que não existe.
/// Tentar de novo não resolve, então a mensagem vai direto para a DLQ.
/// </summary>
public sealed class OrderNotFoundException(Guid orderId)
    : Exception($"Pedido {orderId} não encontrado.")
{
    public Guid OrderId { get; } = orderId;
}
