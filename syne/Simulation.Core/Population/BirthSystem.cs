using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Population;

/// <summary>Événement de naissance par fusion consentie (SYNE-062, SYSTEM_SPEC.md §8).</summary>
public sealed record BirthObservation(
    ulong Tick,
    ulong MotherId,
    ulong FatherId,
    ulong ChildId,
    string Species,
    Position Position,
    TraitSet Traits);

/// <summary>
/// Cycle de vie — naissance par fusion consentie (SYNE-062, décisions n°17/16,
/// SYSTEM_SPEC.md §8) : une fusion reste côté cycle de vie, l'entité née a un état
/// cognitif vierge qui réinitialise la boucle de vie.
///
/// Exécuté <b>après</b> la boucle des entités et la communication de masse (ordre
/// causal strict, DETERMINISM.md §5) : la population ne mute jamais pendant
/// l'itération.
///
/// V0.2 : mécanique déterministe sans PRNG —
/// <list type="number">
/// <item>une tentative de fusion au plus tous les <c>intervalTicks</c> (rareté,
///  V0.1 → V0.2) ;</item>
/// <item>le consentement est la confiance <b>réciproque</b> (min des deux sens)
///  ≥ <c>consentTrustThreshold</c> (§6.6.2) ;</item>
/// <item>première paire qualifiante dans l'ordre trié des identifiants (moindre
///  id = mère, suivant = père) — aucun choix stochastique ;</item>
/// <item>l'enfant naît au point médian des parents (borné au monde), id = plus
///  grand id existant + 1, traits fusionnés (dominance/mutation configurables,
///  SYNE-063) et les parents sont conservés (couple non dissous).</item>
/// </list>
/// </summary>
public sealed class BirthSystem
{
    private readonly Dictionary<ulong, MindState> _newbornMinds = new();
    private readonly List<BirthObservation> _lastBirths = new();

    public BirthSystem(ReproductionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    public ReproductionSettings Settings { get; }

    /// <summary>Naissances du tick courant (au plus <c>maxBirthsPerTick</c>, ordre d'identifiant).</summary>
    public IReadOnlyList<BirthObservation> LastBirths => _lastBirths;

    /// <summary>Esprits des nouveaux-nés du tick (à fusionner dans le pipeline).</summary>
    public IReadOnlyDictionary<ulong, MindState> NewbornMinds => _newbornMinds;

    public void Step(
        ulong tick,
        Simulation.Core.World.World world,
        IReadOnlyDictionary<ulong, MindState> minds,
        SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(minds);
        ArgumentNullException.ThrowIfNull(options);
        _newbornMinds.Clear();
        _lastBirths.Clear();

        ReproductionSettings reproduction = options.Reproduction;
        if (!reproduction.Enabled || reproduction.MaxBirthsPerTick <= 0)
        {
            return;
        }

        if (tick == 0 || tick % (ulong)reproduction.IntervalTicks != 0)
        {
            return;
        }

        List<Entity> ordered = world.Entities.OrderBy(entity => entity.Id.Value).ToList();
        for (int i = 0; i < ordered.Count - 1 && _lastBirths.Count < reproduction.MaxBirthsPerTick; i++)
        {
            Entity mother = ordered[i];
            if (!minds.TryGetValue(mother.Id.Value, out MindState? motherMind))
            {
                continue;
            }

            for (int j = i + 1; j < ordered.Count; j++)
            {
                Entity father = ordered[j];
                if (!minds.TryGetValue(father.Id.Value, out MindState? fatherMind))
                {
                    continue;
                }

                double consent = Math.Min(
                    motherMind.Trust.TrustWith(father.Id.Value),
                    fatherMind.Trust.TrustWith(mother.Id.Value));
                if (consent < reproduction.ConsentTrustThreshold)
                {
                    continue;
                }

                ulong childId = ordered.Aggregate(0UL, (max, entity) => Math.Max(max, entity.Id.Value)) + 1;
                Position position = Midpoint(mother, father, world);
                TraitSet traits = Inheritance.FuseTraits(
                    mother.Traits,
                    father.Traits,
                    options.Agents.Inheritance,
                    seed: (childId * 0x9E3779B97F4A7C15UL) ^ mother.Id.Value);

                var child = new Entity(
                    new EntityId(childId),
                    mother.Species,
                    name: null,
                    position,
                    traits,
                    bornAt: tick);
                world.AddEntity(child);

                MindState childMind = MindState.Born(options, motherMind, fatherMind, tick);
                _newbornMinds[childId] = childMind;
                _lastBirths.Add(new BirthObservation(
                    tick,
                    mother.Id.Value,
                    father.Id.Value,
                    childId,
                    child.Species,
                    position,
                    traits));

                break;
            }
        }
    }

    private static Position Midpoint(Entity mother, Entity father, Simulation.Core.World.World world)
    {
        double x = (mother.Position.X + father.Position.X) / 2.0;
        double y = (mother.Position.Y + father.Position.Y) / 2.0;
        return new Position(
            Math.Clamp(x, 0.0, world.Size.Width),
            Math.Clamp(y, 0.0, world.Size.Height));
    }
}