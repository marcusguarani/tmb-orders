using System.Text.Json;

namespace TmbOrders.Infrastructure.Messaging;

/// <summary>
/// Opções de serialização compartilhadas por quem publica e por quem consome,
/// garantindo que o contrato JSON seja sempre o mesmo dos dois lados.
/// </summary>
public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
