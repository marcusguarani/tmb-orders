namespace TmbOrders.Worker;

/// <summary>Configurações do worker (seção "Worker").</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Tempo entre "Processando" e "Finalizado" (o enunciado pede 5 segundos).</summary>
    public int ProcessingDelaySeconds { get; set; } = 5;

    /// <summary>Intervalo de leitura da tabela de outbox.</summary>
    public int OutboxPollingIntervalMs { get; set; } = 1000;

    /// <summary>Máximo de mensagens do outbox publicadas por ciclo.</summary>
    public int OutboxBatchSize { get; set; } = 50;
}
