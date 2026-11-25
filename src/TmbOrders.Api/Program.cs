using TmbOrders.Infrastructure;
using TmbOrders.Infrastructure.Persistence;
using TmbOrders.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// Migrations automáticas: o banco é criado/atualizado na subida da API.
await app.ApplyMigrationsAsync();

app.MapOpenApi();
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = "TMB Orders API",
    health = "/health",
    openApi = "/openapi/v1.json"
}))
.ExcludeFromDescription();

// Endpoints de pedidos (/orders) entram na Fase 2.

app.Run();
