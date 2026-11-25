namespace TmbOrders.Infrastructure.Messaging.Contracts;

/// <summary>
/// Evento publicado pelo worker a cada mudança de status.
/// A API escuta esse evento e repassa ao navegador via SignalR (tempo real).
/// </summary>
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string PreviousStatus,
    string Status,
    DateTime ChangedAt);
