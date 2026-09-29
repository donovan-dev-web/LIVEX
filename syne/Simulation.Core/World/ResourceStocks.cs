namespace Simulation.Core.World;

/// <summary>Types de ressources du moteur (décision n°2.4, DATA_MODEL.md §8).</summary>
public enum ResourceKind
{
    Food = 0,
    Water = 1,
    Wood = 2,
    Mineral = 3,
}

/// <summary>
/// Réserves globales de ressources (SYNE-042, décision n°4) : quantité courante
/// de chaque ressource, initialisée depuis <c>resources.*</c> (CONFIGURATION.md §1),
/// décrémentée par les actions terminales Eat/Drink (« réserves mises à jour »).
///
/// Déterminisme : aucune consommation du PRNG ; les opérations de réserves sont
/// des soustractions et additions pures (DETERMINISM.md §3). V0.1 : réserves
/// globales partagées, les sources spatiales restent au jalon ph7 (SYNE-070).
///
/// Cycle de vie (SYNE-070) : appliqué par le moteur en fin de tick, après toute la
/// cognition — les entités consomment pendant le tick, puis le monde régénère et/ou
/// se dégrade. La régénération ajoute `RegenerationRate` par tick ; la dégradation
/// retire, à chaque période `DegradationTick` (défaut inactif), la régénération
/// cumulée de la période — les deux opérations restent pures (0 tirage PRNG).
/// </summary>
public sealed class ResourceStocks
{
    private readonly Dictionary<ResourceKind, double> _stocks;

    public ResourceStocks(Configuration.ResourceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _stocks = settings.ToStocks();
    }

    /// <summary>Quantité courante (jamais négative).</summary>
    public double Stock(ResourceKind kind) => _stocks[kind];

    /// <summary>Réserve épuisée : quantité nulle, négative ou corrompue.</summary>
    public bool IsEmpty(ResourceKind kind) => !(double.IsFinite(_stocks[kind]) && _stocks[kind] > 0.0);

    /// <summary>
    /// Consomme <paramref name="amount"/> unités de <paramref name="kind"/>
    /// <b>si et seulement si</b> la réserve couvre exactement cette quantité :
    /// opération tout-ou-rien. Renvoie <c>false</c> (et n'écrit rien) si la réserve
    /// est vide ou insuffisante — la sémantique <c>TryConsume</c> est atomique,
    /// donc un appelant ne peut jamais obtenir le bénéfice d'un achat non payé.
    /// La quantité est validée (finie, non négative) : un <c>NaN</c> rendrait
    /// la réserve définitivement <c>NaN</c> et corromprait toutes les comparaisons
    /// ultérieures.
    /// </summary>
    public bool TryConsume(ResourceKind kind, double amount)
    {
        if (!double.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "La consommation doit être une valeur finie.");
        }

        if (amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La consommation ne peut être négative.");
        }

        double stock = _stocks[kind];
        if (!double.IsFinite(stock) || stock < amount)
        {
            return false;
        }

        _stocks[kind] = stock - amount;
        return true;
    }

    /// <summary>État des réserves (ordre stable du type, déterminisme d'émission).</summary>
    public IReadOnlyDictionary<ResourceKind, double> Snapshot() => new Dictionary<ResourceKind, double>(_stocks);

    /// <summary>
    /// Cycle de vie à la fin du tick (SYNE-070) : régénération puis dégradation,
    /// exclusivement additives/multiplicatives pures — 0 tirage PRNG (DETERMINISM.md §3).
    /// Régénération : + <c>RegenerationRate × facteur</c> à chaque tick. Dégradation : à chaque
    /// tick multiple de <c>DegradationTick</c> (&gt; 0), la réserve perd la régénération
    /// cumulée de la période (<c>RegenerationRate × DegradationTick × facteur</c>) ; sans taux de
    /// régénération, la dégradation est nulle (mécanisme activable, inerte par défaut).
    /// <paramref name="seasonFactors"/> module la régénération d'une saison (SYNE-072,
    /// <c>world.seasons.enabled</c>) ; <c>null</c> = facteurs nominaux (×1, cycle désactivé).
    /// Toutes les réserves sont bornées à 0.
    /// </summary>
    public void ApplyLifecycle(ulong currentTick, Configuration.ResourceSettings settings, Configuration.SeasonFactors? seasonFactors = null)
    {
        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            Configuration.ResourceSpec spec = settings.Spec(kind);
            double factor = seasonFactors?.For(kind) ?? 1.0;
            double current = double.IsFinite(_stocks[kind]) ? _stocks[kind] : 0.0;
            double rate = double.IsFinite(spec.RegenerationRate) ? spec.RegenerationRate : 0.0;
            double next = current + (rate * factor);

            if (spec.DegradationTick is { } degradationTick
                && degradationTick > 0
                && currentTick % (ulong)degradationTick == 0)
            {
                double loss = rate * degradationTick * factor;
                next -= loss;
            }

            // Un facteur de saison corrompu ne doit pas pouvoir injecter NaN/inf.
            _stocks[kind] = double.IsFinite(next) ? Math.Max(0.0, next) : 0.0;
        }
    }

    /// <summary>État de calibration (tests et V0.1) — accès interne.</summary>
    internal static ResourceStocks FromState(double food, double water, double wood, double mineral = 0.0)
    {
        var stocks = new ResourceStocks(new Configuration.ResourceSettings())
        {
            _stocks =
            {
                [ResourceKind.Food] = food,
                [ResourceKind.Water] = water,
                [ResourceKind.Wood] = wood,
                [ResourceKind.Mineral] = mineral,
            },
        };
        return stocks;
    }

    /// <summary>
    /// Restauration bit-à-bit (SYNE-111, PERSISTENCE.md §4) : remet les réserves
    /// aux niveaux sauvegardés **en place**, sans remplacer l'objet — le pipeline
    /// et le catalogue d'actions gardent leur référence et voient les bonnes valeurs.
    /// </summary>
    internal void RestoreState(double food, double water, double wood, double mineral = 0.0)
    {
        _stocks[ResourceKind.Food] = food;
        _stocks[ResourceKind.Water] = water;
        _stocks[ResourceKind.Wood] = wood;
        _stocks[ResourceKind.Mineral] = mineral;
    }
}