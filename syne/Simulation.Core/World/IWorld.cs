// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

/// <summary>
/// Abstraction minimale d'un monde pour la phase d'extraction SYNE-220
/// (étape 1, mode parallèle) : SYNE ne dépend plus d'une implémentation
/// concrète pour lire la taille, les entités, les obstacles, les gisements,
/// les territoires et les livres. Une implémentation locale (<see cref="World"/>)
/// reste le défaut ; une implémentation adossée au flux PRISM pourra la
/// remplacer lors de la bascule par profil (étape 2) puis de la suppression
/// (étape 3).
/// </summary>
public interface IWorld
{
    WorldSize Size { get; }

    SpatialGrid Grid { get; }

    IReadOnlyList<Simulation.Core.Entities.Entity> Entities { get; }

    IReadOnlyList<Obstacle> Obstacles { get; }

    IReadOnlyList<Territory> Territories { get; }

    IReadOnlyList<Book> Books { get; }

    IReadOnlyList<ResourceNode> ResourceNodes { get; }

    ulong ObstacleRevision { get; }

    (Simulation.Core.World.Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng);

    IReadOnlyList<EnvironmentChange> LastEnvironmentChanges { get; }

    void AddBook(Book book);

    void ReplaceResourceNodes(IEnumerable<ResourceNode> nodes);

    void ApplyResourceLayout(Simulation.Core.Configuration.ResourceSettings settings, ulong seed, double cellSize);
}