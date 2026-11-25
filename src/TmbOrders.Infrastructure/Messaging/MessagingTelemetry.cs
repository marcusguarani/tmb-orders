using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;

namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Tracing distribuído da mensageria (OpenTelemetry / W3C Trace Context).
///
/// O contexto do trace atravessa a fila assim:
///   requisição HTTP (API) → traceparent salvo no outbox → span "publish" (Producer)
///   → header "traceparent" na mensagem AMQP → span "process" (Consumer) no worker
///   → eventos gravados pelo worker herdam o mesmo trace → e assim por diante.
/// Resultado: um único trace no Jaeger, do POST /orders até o status Finalizado.
/// </summary>
public static class MessagingTelemetry
{
    public const string SourceName = "TmbOrders.Messaging";
    public const string TraceParentHeader = "traceparent";

    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>Span de publicação, filho do contexto salvo no outbox (ex.: a requisição HTTP original).</summary>
    public static Activity? StartPublish(
        string eventType, string exchange, string routingKey, Guid messageId, Guid correlationId, string? parentTraceParent)
    {
        ActivityContext.TryParse(parentTraceParent, null, out var parent);

        var activity = Source.StartActivity($"{routingKey} publish", ActivityKind.Producer, parent);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "publish");
        activity.SetTag("messaging.destination.name", exchange);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity.SetTag("messaging.message.id", messageId.ToString());
        activity.SetTag("messaging.message.conversation_id", correlationId.ToString());
        activity.SetTag("tmb.event_type", eventType);
        activity.SetTag("tmb.order_id", correlationId.ToString());
        return activity;
    }

    /// <summary>Span de consumo, filho do contexto recebido no header "traceparent" da mensagem.</summary>
    public static Activity? StartProcess(string queue, IReadOnlyBasicProperties properties)
    {
        ActivityContext.TryParse(ReadHeader(properties, TraceParentHeader), null, out var parent);

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "process");
        activity.SetTag("messaging.destination.name", queue);
        activity.SetTag("messaging.message.id", properties.MessageId);
        activity.SetTag("messaging.message.conversation_id", properties.CorrelationId);
        activity.SetTag("tmb.event_type", properties.Type);
        activity.SetTag("tmb.order_id", properties.CorrelationId);
        return activity;
    }

    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
    }

    // No consumo, o RabbitMQ.Client entrega headers de texto como byte[].
    private static string? ReadHeader(IReadOnlyBasicProperties properties, string name) =>
        properties.Headers is not null && properties.Headers.TryGetValue(name, out var value)
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => null
            }
            : null;
}
