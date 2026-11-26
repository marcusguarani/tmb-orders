using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TmbOrders.Domain.Orders;
using TmbOrders.Infrastructure.Persistence;

namespace TmbOrders.Api.Analytics;

/// <summary>
/// Ferramentas (function calling) que a LLM pode usar para consultar os pedidos.
///
/// Segurança por design:
///  - a LLM NUNCA escreve SQL: ela só escolhe uma destas funções e informa parâmetros;
///  - todas as consultas são somente leitura, parametrizadas (EF Core) e com limites;
///  - parâmetros inválidos voltam como { "erro": ... } para o modelo corrigir, sem derrubar a requisição.
/// </summary>
public sealed class OrderAnalyticsTools(OrdersDbContext db, TimeProvider timeProvider, IOptions<AnalyticsOptions> options)
{
    private const int MaxListLimit = 20;
    private const int MaxDailyRangeDays = 92;

    // O projeto usa InvariantGlobalization: datas são formatadas com padrão fixo (dd/MM/yyyy), sem depender de cultura.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly TimeZoneInfo _timeZone = ResolveTimeZone(options.Value.TimeZone);

    public static readonly IReadOnlyList<ChatTool> Definitions =
    [
        ChatTool.Create(
            "contar_pedidos",
            "Conta quantos pedidos existem, com filtros opcionais de status e de período de criação.",
            """
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": ["Pendente", "Processando", "Finalizado"], "description": "Filtra por status." },
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." }
              }
            }
            """),
        ChatTool.Create(
            "resumo_por_status",
            "Quantidade e valor total de pedidos agrupados por status, com filtro opcional de período.",
            """
            {
              "type": "object",
              "properties": {
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." }
              }
            }
            """),
        ChatTool.Create(
            "valor_dos_pedidos",
            "Soma, ticket médio, maior e menor valor dos pedidos, com filtros opcionais de status e período.",
            """
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": ["Pendente", "Processando", "Finalizado"], "description": "Filtra por status." },
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." }
              }
            }
            """),
        ChatTool.Create(
            "tempo_medio_processamento",
            "Tempo médio, mínimo e máximo de processamento dos pedidos finalizados (aprovados), calculado pelo histórico de status: total (Pendente até Finalizado), espera na fila (Pendente até Processando) e execução (Processando até Finalizado).",
            """
            {
              "type": "object",
              "properties": {
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." }
              }
            }
            """),
        ChatTool.Create(
            "listar_pedidos",
            "Lista pedidos com filtros opcionais, ordenados por data ou valor (no máximo 20 por consulta).",
            """
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": ["Pendente", "Processando", "Finalizado"], "description": "Filtra por status." },
                "cliente": { "type": "string", "description": "Parte do nome do cliente (sem diferenciar maiúsculas)." },
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." },
                "ordenar_por": { "type": "string", "enum": ["mais_recentes", "mais_antigos", "maior_valor", "menor_valor"], "description": "Ordenação. Padrão: mais_recentes." },
                "limite": { "type": "integer", "minimum": 1, "maximum": 20, "description": "Quantidade máxima de pedidos. Padrão: 10." }
              }
            }
            """),
        ChatTool.Create(
            "ranking_clientes",
            "Clientes que mais compraram, por valor total, com quantidade de pedidos (no máximo 20).",
            """
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": ["Pendente", "Processando", "Finalizado"], "description": "Filtra por status." },
                "data_inicio": { "type": "string", "description": "Data inicial de criação (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final de criação (inclusive), formato AAAA-MM-DD." },
                "limite": { "type": "integer", "minimum": 1, "maximum": 20, "description": "Quantidade de clientes. Padrão: 5." }
              }
            }
            """),
        ChatTool.Create(
            "pedidos_por_dia",
            "Quantidade e valor total de pedidos criados por dia, em um período de até 92 dias.",
            """
            {
              "type": "object",
              "properties": {
                "data_inicio": { "type": "string", "description": "Data inicial (inclusive), formato AAAA-MM-DD." },
                "data_fim": { "type": "string", "description": "Data final (inclusive), formato AAAA-MM-DD." }
              },
              "required": ["data_inicio", "data_fim"]
            }
            """)
    ];

    /// <summary>Data e hora atuais no fuso configurado (informadas ao modelo no prompt).</summary>
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, _timeZone);

    public string TimeZoneId => options.Value.TimeZone;

    /// <summary>Executa a ferramenta pedida pelo modelo e devolve o resultado (serializável em JSON).</summary>
    public async Task<object> ExecuteAsync(string name, string? argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var args = document.RootElement;

            return name switch
            {
                "contar_pedidos" => await CountAsync(args, cancellationToken),
                "resumo_por_status" => await SummaryByStatusAsync(args, cancellationToken),
                "valor_dos_pedidos" => await AmountsAsync(args, cancellationToken),
                "tempo_medio_processamento" => await ProcessingTimeAsync(args, cancellationToken),
                "listar_pedidos" => await ListAsync(args, cancellationToken),
                "ranking_clientes" => await TopCustomersAsync(args, cancellationToken),
                "pedidos_por_dia" => await PerDayAsync(args, cancellationToken),
                _ => Error($"Ferramenta desconhecida: {name}.")
            };
        }
        catch (JsonException)
        {
            return Error("Os argumentos não são um JSON válido.");
        }
        catch (ToolArgumentException ex)
        {
            return Error(ex.Message);
        }
    }

    // ------------------------------------------------------------------ ferramentas

    private async Task<object> CountAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: true);
        var total = await query.CountAsync(ct);
        return new { total, periodo = period };
    }

    private async Task<object> SummaryByStatusAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: false);

        var groups = await query
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Quantidade = g.Count(), ValorTotal = g.Sum(o => o.Amount) })
            .ToListAsync(ct);

        var porStatus = Enum.GetValues<OrderStatus>()
            .Select(status =>
            {
                var group = groups.FirstOrDefault(g => g.Status == status);
                return new { status = status.ToString(), quantidade = group?.Quantidade ?? 0, valor_total = group?.ValorTotal ?? 0m };
            })
            .ToList();

        return new { periodo = period, total = porStatus.Sum(s => s.quantidade), por_status = porStatus };
    }

    private async Task<object> AmountsAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: true);

        var quantidade = await query.CountAsync(ct);
        if (quantidade == 0)
            return new { periodo = period, quantidade = 0, valor_total = 0m };

        var total = await query.SumAsync(o => o.Amount, ct);
        var maior = await query.MaxAsync(o => o.Amount, ct);
        var menor = await query.MinAsync(o => o.Amount, ct);

        return new
        {
            periodo = period,
            quantidade,
            valor_total = total,
            ticket_medio = Math.Round(total / quantidade, 2),
            maior_valor = maior,
            menor_valor = menor
        };
    }

    private async Task<object> ProcessingTimeAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: false);

        var histories = await query
            .Where(o => o.Status == OrderStatus.Finalizado)
            .SelectMany(o => o.StatusHistory)
            .Select(h => new { h.OrderId, h.Status, h.ChangedAt })
            .ToListAsync(ct);

        var durations = histories
            .GroupBy(h => h.OrderId)
            .Select(g => new
            {
                Pendente = g.FirstOrDefault(h => h.Status == OrderStatus.Pendente)?.ChangedAt,
                Processando = g.FirstOrDefault(h => h.Status == OrderStatus.Processando)?.ChangedAt,
                Finalizado = g.FirstOrDefault(h => h.Status == OrderStatus.Finalizado)?.ChangedAt
            })
            .Where(d => d.Pendente is not null && d.Processando is not null && d.Finalizado is not null)
            .Select(d => new
            {
                Total = (d.Finalizado!.Value - d.Pendente!.Value).TotalSeconds,
                Fila = (d.Processando!.Value - d.Pendente!.Value).TotalSeconds,
                Execucao = (d.Finalizado!.Value - d.Processando!.Value).TotalSeconds
            })
            .ToList();

        if (durations.Count == 0)
            return new { periodo = period, pedidos_finalizados = 0, observacao = "Nenhum pedido finalizado no período." };

        return new
        {
            periodo = period,
            pedidos_finalizados = durations.Count,
            unidade = "segundos",
            total = Stats(durations.Select(d => d.Total)),
            espera_na_fila = Stats(durations.Select(d => d.Fila)),
            execucao = Stats(durations.Select(d => d.Execucao))
        };

        static object Stats(IEnumerable<double> values)
        {
            var list = values.ToList();
            return new { media = Math.Round(list.Average(), 2), minimo = Math.Round(list.Min(), 2), maximo = Math.Round(list.Max(), 2) };
        }
    }

    private async Task<object> ListAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: true);

        var customer = GetString(args, "cliente");
        if (!string.IsNullOrWhiteSpace(customer))
        {
            var pattern = $"%{customer.Trim().Replace("%", string.Empty).Replace("_", string.Empty)}%";
            query = query.Where(o => EF.Functions.ILike(o.Customer, pattern));
        }

        query = GetString(args, "ordenar_por") switch
        {
            null or "mais_recentes" => query.OrderByDescending(o => o.CreatedAt),
            "mais_antigos" => query.OrderBy(o => o.CreatedAt),
            "maior_valor" => query.OrderByDescending(o => o.Amount),
            "menor_valor" => query.OrderBy(o => o.Amount),
            var other => throw new ToolArgumentException($"Ordenação inválida: {other}.")
        };

        var limit = GetLimit(args, defaultValue: 10);
        var total = await query.CountAsync(ct);
        var orders = await query.Take(limit)
            .Select(o => new { o.Id, o.Customer, o.Product, o.Amount, o.Status, o.CreatedAt })
            .ToListAsync(ct);

        return new
        {
            periodo = period,
            total_encontrado = total,
            exibindo = orders.Count,
            pedidos = orders.Select(o => new
            {
                numero = o.Id.ToString()[^8..].ToUpperInvariant(),
                cliente = o.Customer,
                produto = o.Product,
                valor = o.Amount,
                status = o.Status.ToString(),
                criado_em = FormatLocal(o.CreatedAt)
            })
        };
    }

    private async Task<object> TopCustomersAsync(JsonElement args, CancellationToken ct)
    {
        var (query, period) = Filter(args, allowStatus: true);
        var limit = GetLimit(args, defaultValue: 5);

        var ranking = await query
            .GroupBy(o => o.Customer)
            .Select(g => new { Cliente = g.Key, Pedidos = g.Count(), ValorTotal = g.Sum(o => o.Amount) })
            .OrderByDescending(g => g.ValorTotal)
            .Take(limit)
            .ToListAsync(ct);

        return new
        {
            periodo = period,
            clientes = ranking.Select(r => new { cliente = r.Cliente, pedidos = r.Pedidos, valor_total = r.ValorTotal })
        };
    }

    private async Task<object> PerDayAsync(JsonElement args, CancellationToken ct)
    {
        var start = GetDate(args, "data_inicio") ?? throw new ToolArgumentException("Informe data_inicio.");
        var end = GetDate(args, "data_fim") ?? throw new ToolArgumentException("Informe data_fim.");

        if (end < start)
            throw new ToolArgumentException("data_fim deve ser igual ou posterior a data_inicio.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDailyRangeDays)
            throw new ToolArgumentException($"O período máximo é de {MaxDailyRangeDays} dias.");

        var (fromUtc, toUtc) = (ToUtc(start), ToUtc(end.AddDays(1)));

        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new { o.CreatedAt, o.Amount })
            .ToListAsync(ct);

        // Agrupa pelo dia LOCAL (fuso configurado), não pelo dia UTC.
        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(r.CreatedAt, _timeZone)))
            .ToDictionary(g => g.Key, g => (Quantidade: g.Count(), Valor: g.Sum(r => r.Amount)));

        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(offset => start.AddDays(offset))
            .Select(day => byDay.TryGetValue(day, out var value)
                ? new { data = day.ToString("dd/MM/yyyy", Invariant), quantidade = value.Quantidade, valor_total = value.Valor }
                : new { data = day.ToString("dd/MM/yyyy", Invariant), quantidade = 0, valor_total = 0m })
            .ToList();

        return new { periodo = DescribePeriod(start, end), dias = days };
    }

    // ------------------------------------------------------------------ filtros e utilitários

    private (IQueryable<Order> Query, string Period) Filter(JsonElement args, bool allowStatus)
    {
        var query = db.Orders.AsNoTracking();

        if (allowStatus && GetString(args, "status") is { } statusText)
        {
            if (!Enum.TryParse<OrderStatus>(statusText, ignoreCase: true, out var status) || !Enum.IsDefined(status))
                throw new ToolArgumentException($"Status inválido: {statusText}. Use Pendente, Processando ou Finalizado.");

            query = query.Where(o => o.Status == status);
        }

        var start = GetDate(args, "data_inicio");
        var end = GetDate(args, "data_fim");

        if (start is not null && end is not null && end < start)
            throw new ToolArgumentException("data_fim deve ser igual ou posterior a data_inicio.");

        if (start is not null)
        {
            var fromUtc = ToUtc(start.Value);
            query = query.Where(o => o.CreatedAt >= fromUtc);
        }

        if (end is not null)
        {
            var toUtc = ToUtc(end.Value.AddDays(1));
            query = query.Where(o => o.CreatedAt < toUtc);
        }

        return (query, DescribePeriod(start, end));
    }

    private static string DescribePeriod(DateOnly? start, DateOnly? end) => (start, end) switch
    {
        (null, null) => "todo o período",
        ({ } s, null) => $"a partir de {s.ToString("dd/MM/yyyy", Invariant)}",
        (null, { } e) => $"até {e.ToString("dd/MM/yyyy", Invariant)}",
        ({ } s, { } e) when s == e => $"dia {s.ToString("dd/MM/yyyy", Invariant)}",
        ({ } s, { } e) => $"de {s.ToString("dd/MM/yyyy", Invariant)} a {e.ToString("dd/MM/yyyy", Invariant)}"
    };

    /// <summary>Meia-noite do dia informado, no fuso local, convertida para UTC (como gravado no banco).</summary>
    private DateTime ToUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _timeZone);

    private string FormatLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone).ToString("dd/MM/yyyy HH:mm", Invariant);

    private static string? GetString(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static DateOnly? GetDate(JsonElement args, string name)
    {
        var text = GetString(args, name);
        if (text is null)
            return null;

        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ToolArgumentException($"{name} inválida: '{text}'. Use o formato AAAA-MM-DD.");
    }

    private static int GetLimit(JsonElement args, int defaultValue)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("limite", out var value))
            return defaultValue;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var limit)
            ? Math.Clamp(limit, 1, MaxListLimit)
            : defaultValue;
    }

    private static object Error(string message) => new { erro = message };

    private static TimeZoneInfo ResolveTimeZone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)
            ? zone
            // Sem base de fusos no sistema: usa UTC-3 (Brasília não tem horário de verão desde 2019).
            : TimeZoneInfo.CreateCustomTimeZone("UTC-03", TimeSpan.FromHours(-3), "UTC-03", "UTC-03");

    private sealed class ToolArgumentException(string message) : Exception(message);
}
