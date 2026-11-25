using TmbOrders.Infrastructure.Messaging.Contracts;

namespace TmbOrders.UnitTests.Api;

public sealed class EventTypesTests
{
    [Fact]
    public void RoutingKeys_DevemSerEstaveis()
    {
        Assert.Equal("order.created", EventTypes.RoutingKeyFor(EventTypes.OrderCreated));
        Assert.Equal("order.status-changed", EventTypes.RoutingKeyFor(EventTypes.OrderStatusChanged));
    }

    [Fact]
    public void OrderCreated_DeveSerObrigatorio_NotificacaoNao()
    {
        // OrderCreated precisa chegar à fila do worker; notificação de tela é "melhor esforço".
        Assert.True(EventTypes.IsMandatory(EventTypes.OrderCreated));
        Assert.False(EventTypes.IsMandatory(EventTypes.OrderStatusChanged));
    }

    [Fact]
    public void TipoDesconhecido_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EventTypes.RoutingKeyFor("Qualquer"));
    }
}
