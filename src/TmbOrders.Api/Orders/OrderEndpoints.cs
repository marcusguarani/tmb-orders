using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Messaging.Contracts;
using TmbOrders.Infrastructure.Outbox;
using TmbOrders.Infrastructure.Persistence;

namespace TmbOrders.Api.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Pedidos");

        group.MapPost("/", CreateAsync)
            .WithName("CriarPedido")
            .WithSummary("Cria um novo pedido")
            .WithDescription("Grava o pedido com status Pendente e registra o evento OrderCreated no outbox, na mesma transação.");

        group.MapGet("/", ListAsync)
            .WithName("ListarPedidos")
            .WithSummary("Lista todos os pedidos")
            .WithDescription("Ordenados do mais recente para o mais antigo. Aceita filtro opcional ?status=Pendente|Processando|Finalizado.");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("ObterPedido")
            .WithSummary("Obtém os detalhes de um pedido, incluindo o histórico de status");

        return app;
    }

    private static async Task<Results<Created<OrderResponse>, ValidationProblem>> CreateAsync(
        CreateOrderRequest request,
        OrdersDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = CreateOrderRequestValidator.Validate(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors, title: "Dados do pedido inválidos.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var order = Order.Create(request.Cliente!, request.Produto!, request.Valor!.Value, now);

        var orderCreated = new OrderCreatedEvent(
            order.Id,
            order.Customer,
            order.Product,
            order.Amount,
            order.Status.ToString(),
            order.CreatedAt);

        db.Orders.Add(order);
        db.OutboxMessages.Add(OutboxMessageFactory.Create(EventTypes.OrderCreated, order.Id, orderCreated, now));

        // Um único SaveChanges = uma única transação: pedido + histórico + outbox.
        // Ou tudo é gravado, ou nada é. O evento nunca se perde, nem é publicado sem o pedido existir.
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/orders/{order.Id}", OrderResponse.From(order));
    }

    private static async Task<Ok<List<OrderResponse>>> ListAsync(
        OrderStatus? status,
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();

        if (status is not null)
            query = query.Where(o => o.Status == status);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderResponse(o.Id, o.Customer, o.Product, o.Amount, o.Status, o.CreatedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(orders);
    }

    private static async Task<Results<Ok<OrderDetailsResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Pedido não encontrado.",
                detail: $"Não existe pedido com o id {id}.");
        }

        return TypedResults.Ok(OrderDetailsResponse.From(order));
    }
}
