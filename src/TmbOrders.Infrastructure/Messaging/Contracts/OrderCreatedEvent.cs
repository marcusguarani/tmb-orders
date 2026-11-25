namespace TmbOrders.Infrastructure.Messaging.Contracts;

/// <summary>
/// Evento publicado quando um pedido é criado.
/// </summary>
public sealed record OrderCreatedEvent(
    Guid OrderId,
    string Cliente,
    string Produto,
    decimal Valor,
    string Status,
    DateTime DataCriacao);
