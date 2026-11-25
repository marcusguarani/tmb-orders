namespace TmbOrders.Infrastructure.Messaging.Contracts;

public static class EventTypes
{
    /// <summary>Pedido criado → processado pelo worker (fila durável).</summary>
    public const string OrderCreated = "OrderCreated";

    /// <summary>Status mudou → notificação para a interface (tempo real).</summary>
    public const string OrderStatusChanged = "OrderStatusChanged";

    /// <summary>Routing key usada no exchange para cada tipo de evento.</summary>
    public static string RoutingKeyFor(string eventType) => eventType switch
    {
        OrderCreated => "order.created",
        OrderStatusChanged => "order.status-changed",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Tipo de evento desconhecido.")
    };

    /// <summary>
    /// Eventos obrigatórios precisam ser roteados para alguma fila; se não forem, a publicação falha
    /// e o outbox tenta de novo. Notificações de interface são "melhor esforço": se nenhuma API
    /// estiver escutando, podem ser descartadas (o frontend tem polling como fallback).
    /// </summary>
    public static bool IsMandatory(string eventType) => eventType == OrderCreated;
}
