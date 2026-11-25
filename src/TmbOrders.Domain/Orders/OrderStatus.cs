namespace TmbOrders.Domain.Orders;

/// <summary>
/// Status possíveis de um pedido. A ordem numérica representa a sequência
/// obrigatória: Pendente → Processando → Finalizado.
/// </summary>
public enum OrderStatus
{
    Pendente = 0,
    Processando = 1,
    Finalizado = 2
}
