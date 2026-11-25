using TmbOrders.Api.Orders;

namespace TmbOrders.UnitTests.Api;

public sealed class CreateOrderRequestValidatorTests
{
    [Fact]
    public void Validate_RequisicaoValida_NaoDeveRetornarErros()
    {
        var errors = CreateOrderRequestValidator.Validate(new CreateOrderRequest("Maria", "Notebook", 4599.90m));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_CamposAusentes_DeveRetornarErroEmCadaCampo()
    {
        var errors = CreateOrderRequestValidator.Validate(new CreateOrderRequest(null, " ", null));

        Assert.Equal(new[] { "cliente", "produto", "valor" }, errors.Keys.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1_000_000_000.01)]
    [InlineData(10.123)]
    public void Validate_ValorInvalido_DeveRetornarErroNoValor(double valor)
    {
        // Atributos C# não aceitam decimal; o valor é convertido aqui.
        var errors = CreateOrderRequestValidator.Validate(new CreateOrderRequest("Maria", "Notebook", (decimal)valor));

        Assert.Equal(new[] { "valor" }, errors.Keys);
    }

    [Fact]
    public void Validate_TextoMuitoLongo_DeveRetornarErro()
    {
        var longText = new string('x', 151);

        var errors = CreateOrderRequestValidator.Validate(new CreateOrderRequest(longText, longText, 10m));

        Assert.Equal(new[] { "cliente", "produto" }, errors.Keys.Order());
    }
}
