namespace Simulation.Core.World;

/// <summary>Types de ressources du moteur (décision n°2.4, DATA_MODEL.md §8).</summary>
public enum ResourceKind
{
    Food = 0,
    Water = 1,
    Wood = 2,
}

/// <summary>
/// Réserves globales de ressources (SYNE-042, décision n°4) : quantité courante
/// de chaque ressource, initialisée depuis <c>resources.*</c> (CONFIGURATION.md §1),
/// décrémentée par les actions terminales Eat/Drink (« réserves mises à jour »).
///
/// Déterminisme : aucune consommation du PRNG ; les opérations de réserves sont
/// des soustractions pures (DETERMINISM.md §3). V0.1 : réserves globales partagées,
/// les sources spatiales restent au jalon ph7 (SYNE-070).
/// </summary>
public sealed class ResourceStocks
{
    private readonly Dictionary<ResourceKind, double> _stocks;

    public ResourceStocks(Configuration.ResourceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _stocks = new Dictionary<ResourceKind, double>
        {
            [ResourceKind.Food] = settings.Food.Initial,
            [ResourceKind.Water] = settings.Water.Initial,
            [ResourceKind.Wood] = settings.Wood.Initial,
        };
    }

    /// <summary>Quantité courante (jamais négative).</summary>
    public double Stock(ResourceKind kind) => _stocks[kind];

    public bool IsEmpty(ResourceKind kind) => _stocks[kind] <= 0.0;

    /// <summary>
    /// Consomme une quantité si disponible : décrémente la réserve et renvoie
    /// <c>true</c>. Ne descend jamais sous zéro (réserve finie, décision n°4).
    /// </summary>
    public bool TryConsume(ResourceKind kind, double amount)
    {
        if (amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La consommation ne peut être négative.");
        }

        double stock = _stocks[kind];
        if (stock <= 0.0)
        {
            return false;
        }

        _stocks[kind] = Math.Max(0.0, stock - amount);
        return true;
    }

    /// <summary>État des réserves (ordre stable du type, déterminisme d'émission).</summary>
    public IReadOnlyDictionary<ResourceKind, double> Snapshot() => new Dictionary<ResourceKind, double>(_stocks);

    /// <summary>État de calibration (tests et V0.1) — accès interne.</summary>
    internal static ResourceStocks FromState(double food, double water, double wood)
    {
        var stocks = new ResourceStocks(new Configuration.ResourceSettings())
        {
            _stocks =
            {
                [ResourceKind.Food] = food,
                [ResourceKind.Water] = water,
                [ResourceKind.Wood] = wood,
            },
        };
        return stocks;
    }
}