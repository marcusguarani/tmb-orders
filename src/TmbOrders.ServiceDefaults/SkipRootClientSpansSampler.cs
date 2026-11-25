using System.Diagnostics;
using OpenTelemetry.Trace;

namespace TmbOrders.ServiceDefaults;

/// <summary>
/// Descarta spans de "cliente" (ex.: comandos no banco) que não pertencem a nenhum trace.
///
/// Sem isso, o polling do outbox (uma consulta por segundo) e as consultas dos health checks
/// gerariam milhares de traces soltos no Jaeger, escondendo os traces que interessam.
/// Consultas feitas DENTRO de uma requisição ou do processamento de uma mensagem continuam sendo registradas.
/// </summary>
internal sealed class SkipRootClientSpansSampler : Sampler
{
    private static readonly SamplingResult Drop = new(SamplingDecision.Drop);
    private static readonly SamplingResult Sample = new(SamplingDecision.RecordAndSample);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var isRoot = samplingParameters.ParentContext.TraceId == default;
        return isRoot && samplingParameters.Kind == ActivityKind.Client ? Drop : Sample;
    }
}
