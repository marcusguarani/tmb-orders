using TmbOrders.Domain.Orders;

namespace TmbOrders.Api.Orders;

// O JSON da API usa snake_case (cliente, produto, valor, status, data_criacao),
// seguindo os nomes de atributos do enunciado. Configurado em Program.cs.

/// <summary>Corpo do POST /orders.</summary>
public sealed record CreateOrderRequest(string? Cliente, string? Produto, decimal? Valor);

public sealed record OrderResponse(
    Guid Id,
    string Cliente,
    string Produto,
    decimal Valor,
    OrderStatus Status,
    DateTime DataCriacao,
    DateTime? DataAtualizacao)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.Status,
        order.CreatedAt,
        order.UpdatedAt);
}

public sealed record OrderStatusHistoryResponse(OrderStatus Status, DateTime Data);

public sealed record OrderDetailsResponse(
    Guid Id,
    string Cliente,
    string Produto,
    decimal Valor,
    OrderStatus Status,
    DateTime DataCriacao,
    DateTime? DataAtualizacao,
    IReadOnlyList<OrderStatusHistoryResponse> Historico,
    string? TraceId)
{
    public static OrderDetailsResponse From(Order order, string? traceId) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.Status,
        order.CreatedAt,
        order.UpdatedAt,
        order.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Status)
            .Select(h => new OrderStatusHistoryResponse(h.Status, h.ChangedAt))
            .ToList(),
        traceId);
}
