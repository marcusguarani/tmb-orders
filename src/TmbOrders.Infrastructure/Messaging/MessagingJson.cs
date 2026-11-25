using System.Text.Encodings.Web;
using System.Text.Json;

namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Opções de serialização compartilhadas por quem publica e por quem consome,
/// garantindo que o contrato JSON seja sempre o mesmo dos dois lados.
/// </summary>
public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // Mantém acentos legíveis ("João" em vez de "João"), inclusive no painel do RabbitMQ.
        // As mensagens nunca são embutidas em HTML, então não há risco de XSS com esse encoder.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
