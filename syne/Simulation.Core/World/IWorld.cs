// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

/// <summary>
/// Monde <b>observé</b> (SYNE-220 étape 3c) : SYNE ne possède plus de géométrie
/// locale. Il ne connaît que les entités (et leurs positions observées), les
/// territoires (analyse sociale) et les livres (savoir), plus la référence de
/// contenu du monde (dimensions, cellule, empreinte). Toute géométrie
/// (grille, obstacles, gisements, roue temporelle locale) a migré vers PRISM.
/// </summary>
public interface IWorld
{
    WorldSize Size { get; }

    /// <summary>Référence de contenu du monde observé (empreinte), null si inconnue.</summary>
    string? ContentHash { get; }

    IReadOnlyList<Simulation.Core.Entities.Entity> Entities { get; }

    IReadOnlyList<Territory> Territories { get; }

    IReadOnlyList<Book> Books { get; }

    // --- Shims de compatibilité (géométrie retirée du moteur) ---
    SpatialGrid Grid { get; }
    IReadOnlyList<Obstacle> Obstacles { get; }
    ulong ObstacleRevision { get; }
    IReadOnlyList<EnvironmentChange> LastEnvironmentChanges { get; }
    void ClearEnvironmentChanges();

    void AddBook(Book book);

    /// <summary>Ajoute une entité au monde (naissance, peuplement initial).</summary>
    void AddEntity(Simulation.Core.Entities.Entity entity);

    /// <summary>Retire une entité du monde (mortalité). Lève si elle n'était pas présente.</summary>
    void RemoveEntity(Simulation.Core.Entities.Entity entity);
}