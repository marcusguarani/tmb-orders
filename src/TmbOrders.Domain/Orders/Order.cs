namespace TmbOrders.Domain.Orders;

/// <summary>
/// Agregado Pedido. Concentra as regras de negócio:
/// - criação sempre com status Pendente;
/// - transições somente na sequência Pendente → Processando → Finalizado;
/// - toda mudança de status gera um registro no histórico.
/// </summary>
public sealed class Order
{
    public const int CustomerMaxLength = 150;
    public const int ProductMaxLength = 150;

    private readonly List<OrderStatusHistory> _statusHistory = [];

    // Construtor usado pelo EF Core.
    private Order() { }

    private Order(Guid id, string customer, string product, decimal amount, DateTime createdAtUtc)
    {
        Id = id;
        Customer = customer;
        Product = product;
        Amount = amount;
        Status = OrderStatus.Pendente;
        CreatedAt = createdAtUtc;
        _statusHistory.Add(new OrderStatusHistory(id, OrderStatus.Pendente, createdAtUtc));
    }

    public Guid Id { get; private set; }

    public string Customer { get; private set; } = string.Empty;

    public string Product { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Order Create(string customer, string product, decimal amount, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(customer))
            throw new ArgumentException("O cliente é obrigatório.", nameof(customer));

        if (string.IsNullOrWhiteSpace(product))
            throw new ArgumentException("O produto é obrigatório.", nameof(product));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "O valor deve ser maior que zero.");

        return new Order(Guid.CreateVersion7(), customer.Trim(), product.Trim(), amount, nowUtc);
    }

    /// <summary>
    /// Avança o pedido para o próximo status.
    /// Retorna <c>false</c> quando o pedido já está nesse status ou além dele
    /// (operação repetida → ignorada, o que garante idempotência).
    /// Lança <see cref="InvalidStatusTransitionException"/> se tentar pular etapa.
    /// </summary>
    public bool TryAdvanceTo(OrderStatus next, DateTime nowUtc)
    {
        if (next <= Status)
            return false;

        if (next != Status + 1)
            throw new InvalidStatusTransitionException(Id, Status, next);

        Status = next;
        UpdatedAt = nowUtc;
        _statusHistory.Add(new OrderStatusHistory(Id, next, nowUtc));
        return true;
    }
}
