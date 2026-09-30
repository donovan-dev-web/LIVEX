using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Inventaire d'une entité (ADR « Système d'inventaire », D8, engineVersion
/// 0.14.0) : une entrée par type de ressource détenu, bornée par une capacité
/// de poids totale (<c>capacitePoids</c> de l'ADR). Les slots (contrainte
/// secondaire de l'ADR, alternative 3) ne sont pas activés en V0.1.
///
/// <para>
/// Opérations atomiques : <see cref="TryTake"/> et <see cref="TryGive"/>
/// (utilisées aussi par l'échange) échouent sans écrire si la capacité ne couvre
/// pas le transfert — un agent ne peut jamais porter plus que sa capacité. Toutes
/// les opérations sont des additions/soustractions pures : 0 tirage PRNG
/// (DETERMINISM.md §3).
/// </para>
/// </summary>
public sealed class Inventory
{
    private readonly Dictionary<ResourceKind, double> _byKind = new();

    /// <summary>Capacité de poids totale (unités de ressource transportables).</summary>
    public double CapacityWeight { get; }

    /// <summary>Poids total porté (somme des quantités — poids unitaire 1 V0.1, l'ADR le paramètre par ressource plus tard).</summary>
    public double CurrentWeight { get; private set; }

    public Inventory(double capacityWeight)
    {
        if (!double.IsFinite(capacityWeight) || capacityWeight < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityWeight), "La capacité de poids doit être finie et >= 0.");
        }

        CapacityWeight = capacityWeight;
    }

    /// <summary>Quantité détenue d'un type (0 si aucun).</summary>
    public double Amount(ResourceKind kind) => _byKind.TryGetValue(kind, out double amount) ? amount : 0.0;

    /// <summary>Capacité restante (jamais négative).</summary>
    public double FreeWeight => Math.Max(0.0, CapacityWeight - CurrentWeight);

    /// <summary>
    /// Transfert réserve monde → inventaire, tout-ou-rien : échoue (sans effet)
    /// si la capacité ne couvre pas la quantité. Renvoie la quantité réellement
    /// ajoutée (min(quantité, capacité restante)) ou null si aucun transfert.
    /// </summary>
    public double? TryTake(ResourceKind kind, double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La quantité doit être finie et > 0.");
        }

        double storable = Math.Min(amount, FreeWeight);
        if (storable <= 0.0)
        {
            return null;
        }

        _byKind[kind] = Amount(kind) + storable;
        CurrentWeight += storable;
        return storable;
    }

    /// <summary>
    /// Transfert inventaire → destinataire, tout-ou-rien : échoue (sans effet)
    /// si l'entité détient moins que la quantité demandée ou si la capacité du
    /// destinataire ne couvre pas le transfert.
    /// </summary>
    public bool TryGive(ResourceKind kind, double amount, Inventory recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (!double.IsFinite(amount) || amount <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La quantité doit être finie et > 0.");
        }

        if (Amount(kind) < amount || recipient.FreeWeight < amount)
        {
            return false;
        }

        _byKind[kind] = Amount(kind) - amount;
        CurrentWeight -= amount;
        recipient._byKind[kind] = recipient.Amount(kind) + amount;
        recipient.CurrentWeight += amount;
        return true;
    }

    /// <summary>Quantités détenues (ordre stable du type — déterminisme d'émission).</summary>
    public IReadOnlyDictionary<ResourceKind, double> Snapshot()
    {
        var copy = new Dictionary<ResourceKind, double>();
        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            if (_byKind.TryGetValue(kind, out double amount) && amount > 0.0)
            {
                copy[kind] = amount;
            }
        }

        return copy;
    }

    /// <summary>Restauration d'état (persistance bit-à-bit, PERSISTENCE.md §4).</summary>
    internal void RestoreState(IReadOnlyDictionary<ResourceKind, double> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        _byKind.Clear();
        CurrentWeight = 0.0;
        foreach ((ResourceKind kind, double amount) in amounts)
        {
            if (amount > 0.0)
            {
                _byKind[kind] = amount;
                CurrentWeight += amount;
            }
        }
    }

    /// <summary>Contenu complet (persistance bit-à-bit, PERSISTENCE.md §4), ordre stable du type.</summary>
    internal IReadOnlyDictionary<ResourceKind, double> StateSnapshot()
    {
        var copy = new Dictionary<ResourceKind, double>();
        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            copy[kind] = Amount(kind);
        }

        return copy;
    }
}
