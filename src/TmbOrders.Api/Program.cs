using TmbOrders.Api;
using TmbOrders.Api.Analytics;
using TmbOrders.Api.Orders;
using TmbOrders.Api.Realtime;
using TmbOrders.Infrastructure;
using TmbOrders.Infrastructure.Persistence;
using TmbOrders.ServiceDefaults;

const string FrontendCorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);

// JSON da API: snake_case (data_criacao...) e enums como texto ("Pendente").
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Configure(options.SerializerOptions));

// Erros padronizados no formato ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddOpenApi();

// Tempo real: hub SignalR + listener que recebe as mudanças de status do worker via RabbitMQ.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => ApiJsonOptions.Configure(options.PayloadSerializerOptions));
builder.Services.AddSingleton<OrderNotifier>();
builder.Services.AddHostedService<OrderStatusNotificationListener>();

// Módulo de IA: perguntas em linguagem natural sobre os pedidos (LLM + function calling).
builder.Services.AddAnalytics(builder.Configuration);

var app = builder.Build();

// Migrations automáticas: o banco é criado/atualizado na subida da API.
await app.ApplyMigrationsAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapOrderEndpoints();
app.MapHub<OrdersHub>(OrdersHub.Route);
app.MapAnalyticsEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = "TMB Orders API",
    orders = "/orders",
    realtime = OrdersHub.Route,
    analytics = "/analytics/ask",
    health = "/health",
    openApi = "/openapi/v1.json"
}))
.ExcludeFromDescription();

app.Run();
