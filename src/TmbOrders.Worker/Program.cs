using TmbOrders.Infrastructure;
using TmbOrders.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);

// Fase 2: publicador do Outbox + consumidor OrderCreated (BackgroundServices).

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "TMB Orders Worker", health = "/health" }));

app.Run();
