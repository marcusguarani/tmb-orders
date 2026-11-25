using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSingleton<OrderNotifier>();
builder.Services.AddHostedService<OrderStatusNotificationListener>();

var app = builder.Build();

// Migrations automáticas: o banco é criado/atualizado na subida da API.
await app.ApplyMigrationsAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(FrontendCorsPolicy);

app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapOrderEndpoints();
app.MapHub<OrdersHub>(OrdersHub.Route);

app.MapGet("/", () => Results.Ok(new
{
    service = "TMB Orders API",
    orders = "/orders",
    realtime = OrdersHub.Route,
    health = "/health",
    openApi = "/openapi/v1.json"
}))
.ExcludeFromDescription();

app.Run();
