using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Entities;

/// <summary>
/// Fabrique d'entités déterministe : mêmes (seed, template, nombre, monde) →
/// mêmes entités (ids séquentiels, positions, traits tirés dans les plages du
/// paramétrage). L'état du PRNG est restitué pour enchaîner les créations.
/// </summary>
public static class EntityFactory
{
    public static (Entity Entity, Xoshiro256StarStar Next) CreateNext(
        EntityTemplate template,
        World.World world,
        Xoshiro256StarStar rng,
        ulong entitySequence,
        ulong bornAt)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(world);

        Xoshiro256StarStar next = rng;

        var traits = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            (double min, double max) = template.TraitRanges[name];
            next = next.NextDouble(out double u);
            traits[name] = Math.Clamp(min + ((max - min) * u), TraitSet.Min, TraitSet.Max);
        }

        (Position position, next) = world.SamplePosition(next);

        var entity = new Entity(
            new EntityId(entitySequence),
            template.Species,
            name: null,
            position,
            new TraitSet(traits),
            bornAt);

        return (entity, next);
    }
}