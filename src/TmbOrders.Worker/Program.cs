using TmbOrders.Infrastructure;
using TmbOrders.ServiceDefaults;
using TmbOrders.Worker;
using TmbOrders.Worker.Consumers;
using TmbOrders.Worker.Outbox;
using TmbOrders.Worker.Processing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(o => o.ProcessingDelaySeconds >= 0, "Worker:ProcessingDelaySeconds não pode ser negativo.")
    .Validate(o => o.OutboxPollingIntervalMs > 0, "Worker:OutboxPollingIntervalMs deve ser maior que zero.")
    .Validate(o => o.OutboxBatchSize > 0, "Worker:OutboxBatchSize deve ser maior que zero.")
    .ValidateOnStart();

builder.Services.AddSingleton<OutboxSignal>();
builder.Services.AddScoped<OrderProcessor>();

// 1) Lê o outbox e publica no RabbitMQ.
builder.Services.AddHostedService<OutboxPublisher>();
// 2) Consome OrderCreated e avança o status do pedido.
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "TMB Orders Worker", health = "/health" }));

app.Run();
