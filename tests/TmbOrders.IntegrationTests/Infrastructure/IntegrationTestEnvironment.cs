using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using TmbOrders.Api;
using TmbOrders.Api.Orders;
using TmbOrders.Infrastructure.Persistence;
using TmbOrders.Worker;

namespace TmbOrders.IntegrationTests.Infrastructure;

/// <summary>
/// Ambiente de integração isolado e descartável:
///  - PostgreSQL e RabbitMQ REAIS em containers (Testcontainers), criados no início e destruídos no fim;
///  - a API e o Worker reais, rodando em memória (WebApplicationFactory), com a mesma configuração de produção.
/// Requer apenas o Docker em execução.
/// </summary>
public sealed class IntegrationTestEnvironment : IAsyncLifetime
{
    private const string RabbitUser = "tmb";
    private const string RabbitPassword = "tmb-test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tmb_orders_tests")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine")
        .WithUsername(RabbitUser)
        .WithPassword(RabbitPassword)
        .Build();

    public WebApplicationFactory<OrderResponse> Api { get; private set; } = null!;

    public WebApplicationFactory<WorkerOptions> Worker { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public JsonSerializerOptions Json { get; } = ApiJsonOptions.Create();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        // A API e o Worker leem a configuração de variáveis de ambiente, igual ao docker-compose.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RabbitMq__HostName", _rabbitMq.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__UserName", RabbitUser);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitPassword);

        // Acelera os testes: 1 s de processamento e outbox lido a cada 200 ms.
        Environment.SetEnvironmentVariable("Worker__ProcessingDelaySeconds", "1");
        Environment.SetEnvironmentVariable("Worker__OutboxPollingIntervalMs", "200");

        // A API sobe primeiro: ela aplica as migrations.
        Api = new WebApplicationFactory<OrderResponse>();
        Client = Api.CreateClient();

        Worker = new WebApplicationFactory<WorkerOptions>();
        _ = Worker.Services; // força a inicialização do host (BackgroundServices: outbox + consumidor)
    }

    public OrdersDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    public async Task<OrderResponse> CreateOrderAsync(string cliente = "Maria Silva", string produto = "Notebook", decimal valor = 4599.90m)
    {
        var response = await Client.PostAsJsonAsync("/orders", new { cliente, produto, valor }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Json))!;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        if (Worker is not null)
            await Worker.DisposeAsync();

        if (Api is not null)
            await Api.DisposeAsync();

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationTestEnvironment>
{
    public const string Name = "Integração";
}
