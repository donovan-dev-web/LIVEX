using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Population;

/// <summary>Événement de mort par épuisement (SYNE-074, SYSTEM_SPEC.md §8).</summary>
public sealed record DeathObservation(
    ulong Tick,
    ulong EntityId,
    string Species,
    string Cause);

/// <summary>
/// Cycle de vie — mortalité (SYNE-074, SYSTEM_SPEC.md §8, Monographie §6.2.4-6.2.5) :
/// une entité dont l'énergie atteint le seuil fatal (défaut 0 — épuisement) meurt :
/// son corps est retiré du monde (grille spatiale + index) et les esprits sont
/// purgés de la cognition et des groupes par l'appelant.
///
/// Exécuté <b>après</b> la boucle des entités, la communication de masse et
/// naissances (ordre causal strict, DETERMINISM.md §5) : la population ne mute
/// jamais pendant l'itération. Déterminisme : itération par ordre d'identifiant,
/// aucun tirage du PRNG.
/// </summary>
public sealed class DeathSystem
{
    private readonly LifeSettings _settings;
    private readonly List<DeathObservation> _lastDeaths = new();

    public DeathSystem(LifeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public LifeSettings Settings => _settings;

    /// <summary>Morts du tick courant (ordre d'identifiant).</summary>
    public IReadOnlyList<DeathObservation> LastDeaths => _lastDeaths;

    /// <summary>
    /// Retire du monde les entités mortes par épuisement ; renvoie les identifiants
    /// à retirer de la cognition (ordre croissant, déterministe).
    /// </summary>
    public IReadOnlyList<ulong> Step(
        ulong tick,
        Simulation.Core.World.World world,
        IReadOnlyDictionary<ulong, MindState> minds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(minds);
        _lastDeaths.Clear();

        if (!_settings.DeathEnabled)
        {
            return Array.Empty<ulong>();
        }

        var deadIds = new List<ulong>();
        List<Entity> ordered = world.Entities.OrderBy(entity => entity.Id.Value).ToList();
        foreach (Entity entity in ordered)
        {
            if (!minds.TryGetValue(entity.Id.Value, out MindState? mind))
            {
                continue;
            }

            if (mind.Needs.Energy <= _settings.DeathEnergyThreshold)
            {
                world.RemoveEntity(entity);
                deadIds.Add(entity.Id.Value);
                _lastDeaths.Add(new DeathObservation(
                    tick,
                    entity.Id.Value,
                    entity.Species,
                    _settings.EnergyExhaustionCause));
            }
        }

        return deadIds;
    }
}