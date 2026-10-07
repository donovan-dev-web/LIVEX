namespace Simulation.Console.Observability;

/// <summary>
/// Répartit chaque trame d'observabilité sur plusieurs cibles (API_CONTRACTS.md §2).
/// Utilisé lorsqu'un run doit être à la fois observable en direct et archivé :
/// les deux cibles reçoivent la même trame, dans le même ordre, sans que l'une
/// puisse filtrer ce que l'autre voit.
/// </summary>
public sealed class CompositeObservabilitySink : IObservabilitySink, IObservabilityDemand
{
    private readonly IObservabilitySink[] _sinks;

    /// <summary>Composite les cibles fournies, dans l'ordre de diffusion.</summary>
    public CompositeObservabilitySink(params IObservabilitySink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Length == 0)
        {
            throw new ArgumentException("Au moins une cible est requise.", nameof(sinks));
        }

        _sinks = sinks;
    }

    /// <summary>
    /// Vrai dès qu'une cible écoute : une cible d'archive (fichier) compte
    /// toujours, faute de quoi l'émetteur court-circuiterait la capture du
    /// snapshot et n'écrirait rien.
    /// </summary>
    public bool HasSubscribers => _sinks.Any(sink =>
        sink is not IObservabilityDemand demand || demand.HasSubscribers);

    /// <summary>
    /// Diffuse la trame à toutes les cibles. Une cible en échec n'empêche pas
    /// les autres d'être servies : chaque cible applique sa propre politique
    /// (le fichier échoue explicitement, le WebSocket est meilleur effort).
    /// </summary>
    public async Task BroadcastAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (IObservabilitySink sink in _sinks)
        {
            await sink.BroadcastAsync(text).ConfigureAwait(false);
        }
    }
}