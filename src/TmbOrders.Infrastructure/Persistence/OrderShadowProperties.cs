namespace TmbOrders.Infrastructure.Persistence;

/// <summary>Propriedades mapeadas no banco que não fazem parte da entidade de domínio.</summary>
public static class OrderShadowProperties
{
    public const string TraceId = "TraceId";
}
