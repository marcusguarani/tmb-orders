using TmbOrders.Domain.Orders;

namespace TmbOrders.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTime T0 = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder() => Order.Create("Maria Silva", "Notebook", 4599.90m, T0);

    [Fact]
    public void Create_DeveIniciarComoPendenteERegistrarHistorico()
    {
        var order = NewOrder();

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(OrderStatus.Pendente, order.Status);
        Assert.Equal(T0, order.CreatedAt);
        Assert.Null(order.UpdatedAt);

        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Pendente, history.Status);
        Assert.Equal(T0, history.ChangedAt);
    }

    [Fact]
    public void Create_DeveRemoverEspacosDoClienteEDoProduto()
    {
        var order = Order.Create("  Maria Silva  ", "  Notebook ", 10m, T0);

        Assert.Equal("Maria Silva", order.Customer);
        Assert.Equal("Notebook", order.Product);
    }

    [Theory]
    [InlineData("", "Notebook", 10)]
    [InlineData("   ", "Notebook", 10)]
    [InlineData("Maria", "", 10)]
    [InlineData("Maria", "Notebook", 0)]
    [InlineData("Maria", "Notebook", -1)]
    public void Create_ComDadosInvalidos_DeveLancarExcecao(string customer, string product, int amount)
    {
        Assert.ThrowsAny<ArgumentException>(() => Order.Create(customer, product, amount, T0));
    }

    [Fact]
    public void Create_DeveGerarIdsDiferentes()
    {
        Assert.NotEqual(NewOrder().Id, NewOrder().Id);
    }

    [Fact]
    public void TryAdvanceTo_SequenciaCompleta_DeveRegistrarTodasAsEtapas()
    {
        var order = NewOrder();

        Assert.True(order.TryAdvanceTo(OrderStatus.Processando, T0.AddSeconds(1)));
        Assert.True(order.TryAdvanceTo(OrderStatus.Finalizado, T0.AddSeconds(6)));

        Assert.Equal(OrderStatus.Finalizado, order.Status);
        Assert.Equal(T0.AddSeconds(6), order.UpdatedAt);
        Assert.Equal(
            new[] { OrderStatus.Pendente, OrderStatus.Processando, OrderStatus.Finalizado },
            order.StatusHistory.Select(h => h.Status));
    }

    [Fact]
    public void TryAdvanceTo_MesmoStatus_DeveSerIgnorado_Idempotencia()
    {
        var order = NewOrder();
        order.TryAdvanceTo(OrderStatus.Processando, T0.AddSeconds(1));

        var changed = order.TryAdvanceTo(OrderStatus.Processando, T0.AddSeconds(2));

        Assert.False(changed);
        Assert.Equal(OrderStatus.Processando, order.Status);
        Assert.Equal(2, order.StatusHistory.Count);
        Assert.Equal(T0.AddSeconds(1), order.UpdatedAt);
    }

    [Fact]
    public void TryAdvanceTo_StatusAnterior_DeveSerIgnorado()
    {
        var order = NewOrder();
        order.TryAdvanceTo(OrderStatus.Processando, T0.AddSeconds(1));
        order.TryAdvanceTo(OrderStatus.Finalizado, T0.AddSeconds(6));

        Assert.False(order.TryAdvanceTo(OrderStatus.Processando, T0.AddSeconds(7)));
        Assert.False(order.TryAdvanceTo(OrderStatus.Pendente, T0.AddSeconds(7)));
        Assert.Equal(OrderStatus.Finalizado, order.Status);
        Assert.Equal(3, order.StatusHistory.Count);
    }

    [Fact]
    public void TryAdvanceTo_PulandoEtapa_DeveLancarExcecao()
    {
        var order = NewOrder();

        var ex = Assert.Throws<InvalidStatusTransitionException>(
            () => order.TryAdvanceTo(OrderStatus.Finalizado, T0.AddSeconds(1)));

        Assert.Equal(OrderStatus.Pendente, ex.Current);
        Assert.Equal(OrderStatus.Finalizado, ex.Requested);
        Assert.Equal(OrderStatus.Pendente, order.Status);
        Assert.Single(order.StatusHistory);
    }
}
