namespace TmbOrders.Infrastructure.Messaging.Contracts;

public static class EventTypes
{
    public const string OrderCreated = "OrderCreated";

    /// <summary>Routing key usada no exchange para cada tipo de evento.</summary>
    public static string RoutingKeyFor(string eventType) => eventType switch
    {
        OrderCreated => "order.created",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Tipo de evento desconhecido.")
    };
}
