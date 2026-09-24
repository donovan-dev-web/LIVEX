using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Rapport inter-entités (décision n°10, COMMUNICATION_PROTOCOL.md §3,
/// DATA_MODEL.md — table « relationships ») : niveau de confiance 0-1 envers
/// chaque pair connu.
///
/// <list type="bullet">
/// <item><b>Décroissance</b> : en l'absence d'interaction dans la tickée, la
///  confiance est multipliée par <c>decayFactor</c> (défaut 0.9, trustDecay) ;</item>
/// <item><b>Vérité constatée</b> : la confiance augmente (bonus de véracité) ;</item>
/// <item><b>Mensonge constaté</b> : la confiance est pénalisée (pénalité de tromperie).</item>
/// </list>
///
/// Déterminisme : toutes les opérations sont commutatives et les itérations
/// s'effectuent sur des listes triées par identifiant de pair.
/// </summary>
public sealed class Relationships
{
    private readonly Dictionary<ulong, double> _byPeer = new();
    private readonly HashSet<ulong> _interactedThisCycle = new();

    public Relationships()
    {
    }

    public Relationships(TrustSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>Configuration de confiance (décision n°10, Table Annexe H).</summary>
    public TrustSettings Settings { get; } = new();

    public int Count => _byPeer.Count;

    /// <summary>Confiance envers le pair <paramref name="peerId"/> (0.0 si inconnu).</summary>
    public double TrustWith(ulong peerId) => _byPeer.TryGetValue(peerId, out double trust) ? trust : 0.0;

    /// <summary>L'entité a-t-elle une relation établie avec ce pair ?</summary>
    public bool Knows(ulong peerId) => _byPeer.ContainsKey(peerId);

    /// <summary>
    /// Interaction : première rencontre → confiance initiale ; relation connue →
    /// renforcement (bonus de véracité, plafonné à 1.0) puis marquage pour la
    /// tickée courante (pas de décroissance ce tick).
    /// </summary>
    public double Interact(ulong peerId)
    {
        double updated;
        if (Knows(peerId))
        {
            updated = Math.Min(1.0, _byPeer[peerId] + Settings.TruthBonus);
        }
        else
        {
            updated = Settings.InitialTrust;
        }

        _byPeer[peerId] = updated;
        _interactedThisCycle.Add(peerId);
        return updated;
    }

    /// <summary>
    /// Mensonge constaté (COMMUNICATION_PROTOCOL.md §3) : pénalité appliquée,
    /// plancher 0.0. Le pair reste marqué comme « interagi » ce cycle : une
    /// tromperie constitue bien une interaction.
    /// </summary>
    public double ObserveDeception(ulong peerId)
    {
        double updated = Math.Max(0.0, TrustWith(peerId) - Settings.LiePenalty);
        _byPeer[peerId] = updated;
        _interactedThisCycle.Add(peerId);
        return updated;
    }

    /// <summary>
    /// Fin de tickée : décroissance <c>trust × decayFactor</c> de chaque relation
    /// sans interaction à ce cycle, puis remise à zéro du marqueur de cycle.
    /// </summary>
    public void Tick()
    {
        foreach (ulong peer in SortedPeers())
        {
            if (_interactedThisCycle.Contains(peer))
            {
                continue;
            }

            _byPeer[peer] = Math.Max(0.0, _byPeer[peer] * Settings.DecayFactorPerTick);
        }

        _interactedThisCycle.Clear();
    }

    /// <summary>Relations (pair, confiance) triées par identifiant de pair — ordre d'émission stable.</summary>
    public IReadOnlyList<(ulong PeerId, double Trust)> Snapshot()
    {
        var result = new List<(ulong PeerId, double Trust)>(_byPeer.Count);
        foreach (ulong peer in SortedPeers())
        {
            result.Add((peer, _byPeer[peer]));
        }

        return result;
    }

    /// <summary>Restauration des relations établies (persistance bit-à-bit, PERSISTENCE.md §4).</summary>
    internal void RestoreState(IEnumerable<(ulong PeerId, double Trust)> relations)
    {
        ArgumentNullException.ThrowIfNull(relations);
        _byPeer.Clear();
        foreach ((ulong peerId, double trust) in relations)
        {
            _byPeer[peerId] = trust;
        }
    }

    private List<ulong> SortedPeers()
    {
        var peers = new List<ulong>(_byPeer.Keys);
        peers.Sort();
        return peers;
    }
}