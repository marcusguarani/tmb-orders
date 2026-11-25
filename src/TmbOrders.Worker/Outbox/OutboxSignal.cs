namespace TmbOrders.Worker.Outbox;

/// <summary>
/// "Acorda" o publicador do outbox imediatamente, sem esperar o próximo ciclo de polling.
/// Usado quando o próprio worker grava um evento (mudança de status), para a
/// notificação chegar à interface quase instantaneamente.
/// </summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Já havia um aviso pendente: nada a fazer.
        }
    }

    /// <summary>Espera um aviso ou o tempo limite (o que vier primeiro).</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        await _signal.WaitAsync(timeout, cancellationToken);
}
