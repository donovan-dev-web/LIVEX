// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Fabrique d'entités déterministe : mêmes (seed, template, nombre, monde) → mêmes entités (ids séquentiels,
//        positions, traits tirés dans les plages du paramétrage).

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
        IWorld world,
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

        next = next.NextDouble(out double uu);
        next = next.NextDouble(out double vv);
        Position position = new Position(uu * world.Size.Width, vv * world.Size.Height);

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
