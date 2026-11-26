using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TmbOrders.Api.Analytics;
using TmbOrders.Domain.Orders;
using TmbOrders.IntegrationTests.Infrastructure;

namespace TmbOrders.IntegrationTests;

/// <summary>
/// Testa as ferramentas do módulo de IA contra o PostgreSQL real e o "agent loop" com um modelo falso
/// (sem chamar nenhum provedor externo: o teste é determinístico e não gasta créditos).
///
/// Os pedidos de teste são criados em uma data fixa no passado (15/01/2020), que nenhum outro teste usa,
/// para que as contagens sejam exatas mesmo com o banco compartilhado.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AnalyticsTests(IntegrationTestEnvironment env) : IAsyncLifetime
{
    private const string Day = "2020-01-15";

    // 15/01/2020 às 12:00 em Brasília (UTC-3) = 15:00 UTC.
    private static readonly DateTime BaseUtc = new(2020, 1, 15, 15, 0, 0, DateTimeKind.Utc);

    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _seeded;

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Massa de dados (15/01/2020):
    ///  - Ana: 100,00 Finalizado (fila 1 s, execução 5 s → total 6 s)
    ///  - Ana: 300,00 Finalizado (fila 2 s, execução 8 s → total 10 s)
    ///  - Bruno: 50,00 Processando
    ///  - Carla: 20,00 Pendente
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            if (_seeded)
                return;

            await using var db = env.CreateDbContext();
            db.Orders.AddRange(
                Finished("Ana Analytics", 100m, BaseUtc, queueSeconds: 1, runSeconds: 5),
                Finished("Ana Analytics", 300m, BaseUtc.AddMinutes(10), queueSeconds: 2, runSeconds: 8),
                Processing("Bruno Analytics", 50m, BaseUtc.AddMinutes(20)),
                Order.Create("Carla Analytics", "Produto", 20m, BaseUtc.AddMinutes(30)));
            await db.SaveChangesAsync();

            _seeded = true;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ContarPedidos_PorDiaEStatus()
    {
        Assert.Equal(4, (await Run("contar_pedidos", new { data_inicio = Day, data_fim = Day })).GetProperty("total").GetInt32());
        Assert.Equal(2, (await Run("contar_pedidos", new { status = "Finalizado", data_inicio = Day, data_fim = Day })).GetProperty("total").GetInt32());
        Assert.Equal(1, (await Run("contar_pedidos", new { status = "Pendente", data_inicio = Day, data_fim = Day })).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ValorDosPedidos_Finalizados()
    {
        var result = await Run("valor_dos_pedidos", new { status = "Finalizado", data_inicio = Day, data_fim = Day });

        Assert.Equal(2, result.GetProperty("quantidade").GetInt32());
        Assert.Equal(400m, result.GetProperty("valor_total").GetDecimal());
        Assert.Equal(200m, result.GetProperty("ticket_medio").GetDecimal());
        Assert.Equal(300m, result.GetProperty("maior_valor").GetDecimal());
    }

    [Fact]
    public async Task TempoMedioProcessamento_CalculadoPeloHistorico()
    {
        var result = await Run("tempo_medio_processamento", new { data_inicio = Day, data_fim = Day });

        Assert.Equal(2, result.GetProperty("pedidos_finalizados").GetInt32());
        Assert.Equal(8, result.GetProperty("total").GetProperty("media").GetDouble());          // (6 + 10) / 2
        Assert.Equal(1.5, result.GetProperty("espera_na_fila").GetProperty("media").GetDouble()); // (1 + 2) / 2
        Assert.Equal(6.5, result.GetProperty("execucao").GetProperty("media").GetDouble());       // (5 + 8) / 2
    }

    [Fact]
    public async Task RankingClientes_OrdenadoPorValor()
    {
        var result = await Run("ranking_clientes", new { data_inicio = Day, data_fim = Day, limite = 3 });
        var first = result.GetProperty("clientes")[0];

        Assert.Equal("Ana Analytics", first.GetProperty("cliente").GetString());
        Assert.Equal(400m, first.GetProperty("valor_total").GetDecimal());
        Assert.Equal(2, first.GetProperty("pedidos").GetInt32());
    }

    [Fact]
    public async Task ListarPedidos_FiltroPorClienteSemDiferenciarMaiusculas()
    {
        var result = await Run("listar_pedidos", new { cliente = "bruno analytics", data_inicio = Day, data_fim = Day });

        Assert.Equal(1, result.GetProperty("total_encontrado").GetInt32());
        Assert.Equal("Processando", result.GetProperty("pedidos")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task PedidosPorDia_AgrupaPeloDiaLocal()
    {
        var result = await Run("pedidos_por_dia", new { data_inicio = "2020-01-14", data_fim = "2020-01-16" });
        var days = result.GetProperty("dias");

        Assert.Equal(3, days.GetArrayLength());
        Assert.Equal(0, days[0].GetProperty("quantidade").GetInt32());
        Assert.Equal(4, days[1].GetProperty("quantidade").GetInt32());
        Assert.Equal(0, days[2].GetProperty("quantidade").GetInt32());
    }

    [Theory]
    [InlineData("contar_pedidos", """{"status":"Cancelado"}""")]
    [InlineData("contar_pedidos", """{"data_inicio":"15/01/2020"}""")]
    [InlineData("contar_pedidos", """{"data_inicio":"2020-01-20","data_fim":"2020-01-10"}""")]
    [InlineData("pedidos_por_dia", """{"data_inicio":"2020-01-01","data_fim":"2020-12-31"}""")]
    [InlineData("ferramenta_inexistente", "{}")]
    [InlineData("contar_pedidos", "isto não é json")]
    public async Task ParametrosInvalidos_RetornamErroParaOModeloCorrigir(string tool, string arguments)
    {
        var result = await RunRaw(tool, arguments);

        Assert.True(result.TryGetProperty("erro", out _), $"Esperava 'erro' em: {result}");
    }

    [Fact]
    public async Task Assistente_ExecutaFerramentaPedidaPeloModeloERespondeComOResultado()
    {
        await using var scope = env.Api.Services.CreateAsyncScope();
        var tools = scope.ServiceProvider.GetRequiredService<OrderAnalyticsTools>();
        var model = new ScriptedChatModel(
            // 1ª resposta do modelo: pede a ferramenta.
            new ChatMessage("assistant", null,
            [
                new ChatToolCall("call_1", "function",
                    new ChatFunctionCall("contar_pedidos", $$"""{"status":"Pendente","data_inicio":"{{Day}}","data_fim":"{{Day}}"}"""))
            ]),
            // 2ª resposta: texto final.
            new ChatMessage("assistant", "Há 1 pedido pendente nesse dia."));

        var assistant = new OrderAssistant(
            model, tools, Options.Create(new AnalyticsOptions { Model = "fake" }), NullLogger<OrderAssistant>.Instance);

        var answer = await assistant.AskAsync("Quantos pedidos pendentes em 15/01/2020?", Ct);

        Assert.Equal("Há 1 pedido pendente nesse dia.", answer.Resposta);
        var call = Assert.Single(answer.Consultas);
        Assert.Equal("contar_pedidos", call.Ferramenta);
        Assert.Equal(1, call.Resultado.GetProperty("total").GetInt32());

        // O resultado REAL do banco foi enviado ao modelo na segunda chamada.
        var toolMessage = Assert.Single(model.Received[1], m => m.Role == "tool");
        Assert.Equal("call_1", toolMessage.ToolCallId);
        Assert.Contains("\"total\":1", toolMessage.Content);
    }

    [Fact]
    public async Task Assistente_LimitaONumeroDeRodadasDeFerramentas()
    {
        await using var scope = env.Api.Services.CreateAsyncScope();
        var tools = scope.ServiceProvider.GetRequiredService<OrderAnalyticsTools>();
        var looping = new ChatMessage("assistant", null,
            [new ChatToolCall("c", "function", new ChatFunctionCall("contar_pedidos", "{}"))]);
        var model = new ScriptedChatModel(Enumerable.Repeat(looping, 10).ToArray());

        var assistant = new OrderAssistant(
            model, tools, Options.Create(new AnalyticsOptions { Model = "fake", MaxToolRounds = 3 }), NullLogger<OrderAssistant>.Instance);

        var answer = await assistant.AskAsync("pergunta que entra em loop", Ct);

        Assert.Equal(3, model.Received.Count);
        Assert.Equal(3, answer.Consultas.Count);
    }

    // ------------------------------------------------------------------ helpers

    private Task<JsonElement> Run(string tool, object arguments) => RunRaw(tool, JsonSerializer.Serialize(arguments));

    private async Task<JsonElement> RunRaw(string tool, string arguments)
    {
        await using var scope = env.Api.Services.CreateAsyncScope();
        var tools = scope.ServiceProvider.GetRequiredService<OrderAnalyticsTools>();
        var result = await tools.ExecuteAsync(tool, arguments, Ct);
        return JsonSerializer.SerializeToElement(result);
    }

    private static Order Finished(string customer, decimal amount, DateTime createdAt, int queueSeconds, int runSeconds)
    {
        var order = Order.Create(customer, "Produto", amount, createdAt);
        order.TryAdvanceTo(OrderStatus.Processando, createdAt.AddSeconds(queueSeconds));
        order.TryAdvanceTo(OrderStatus.Finalizado, createdAt.AddSeconds(queueSeconds + runSeconds));
        return order;
    }

    private static Order Processing(string customer, decimal amount, DateTime createdAt)
    {
        var order = Order.Create(customer, "Produto", amount, createdAt);
        order.TryAdvanceTo(OrderStatus.Processando, createdAt.AddSeconds(1));
        return order;
    }

    /// <summary>Modelo falso: devolve respostas pré-definidas e registra o que recebeu.</summary>
    private sealed class ScriptedChatModel(params ChatMessage[] replies) : IChatModel
    {
        private int _index;

        public List<IReadOnlyList<ChatMessage>> Received { get; } = [];

        public string ModelName => "fake";

        public Task<ChatMessage> CompleteAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ChatTool> tools, CancellationToken cancellationToken)
        {
            Received.Add(messages.ToList());
            return Task.FromResult(replies[Math.Min(_index++, replies.Length - 1)]);
        }
    }
}
