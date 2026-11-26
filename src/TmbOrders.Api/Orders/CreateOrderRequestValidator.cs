using System.Text.RegularExpressions;
using TmbOrders.Domain.Orders;

namespace TmbOrders.Api.Orders;

/// <summary>
/// Validação de entrada do POST /orders.
/// Retorna erros no formato do ValidationProblemDetails (RFC 9457),
/// com as chaves iguais aos nomes dos campos do JSON.
/// </summary>
public static partial class CreateOrderRequestValidator
{
    /// <summary>
    /// Nome do cliente: letras (inclusive acentuadas), espaço, apóstrofo e hífen — ex.: "João", "Ana-Maria", "D'Ávila".
    /// Números e símbolos não são permitidos. Precisa conter ao menos uma letra.
    /// </summary>
    [GeneratedRegex(@"^(?=.*\p{L})[\p{L}\p{M}' \-]+$")]
    private static partial Regex CustomerNamePattern();

    public const decimal MaxAmount = 1_000_000_000m;

    public static Dictionary<string, string[]> Validate(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Cliente))
            errors["cliente"] = ["O cliente é obrigatório."];
        else if (request.Cliente.Trim().Length > Order.CustomerMaxLength)
            errors["cliente"] = [$"O cliente deve ter no máximo {Order.CustomerMaxLength} caracteres."];
        else if (!CustomerNamePattern().IsMatch(request.Cliente.Trim()))
            errors["cliente"] = ["O nome do cliente deve conter apenas letras."];

        if (string.IsNullOrWhiteSpace(request.Produto))
            errors["produto"] = ["O produto é obrigatório."];
        else if (request.Produto.Trim().Length > Order.ProductMaxLength)
            errors["produto"] = [$"O produto deve ter no máximo {Order.ProductMaxLength} caracteres."];

        if (request.Valor is null)
            errors["valor"] = ["O valor é obrigatório."];
        else if (request.Valor <= 0)
            errors["valor"] = ["O valor deve ser maior que zero."];
        else if (request.Valor > MaxAmount)
            errors["valor"] = [$"O valor deve ser no máximo {MaxAmount:N2}."];
        else if (decimal.Round(request.Valor.Value, 2) != request.Valor.Value)
            errors["valor"] = ["O valor deve ter no máximo 2 casas decimais."];

        return errors;
    }
}
