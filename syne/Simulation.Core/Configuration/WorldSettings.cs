// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (WorldSettings).

namespace Simulation.Core.Configuration;

public sealed class WorldSettings
{
    /// <summary>
    /// Mètres par unité SYNE — clé <b>informative</b>, jamais
    /// utilisée dans un calcul du moteur : elle est seulement transmise aux
    /// clients (PRISM : échelle k = 100 uu / unité, 1 unité = 1 m).
    /// Défaut 1,0 (unité = mètre) ; doit être &gt; 0.
    /// </summary>
    public double MetersPerUnit { get; set; } = 1.0;

    /// <summary>
    /// Cycle de saisons (SYNE-072) : objet <c>world.seasons</c> — l'ancien drapeau
    /// booléen homonyme (mort, jamais consommé) est remplacé par ce bloc actif.
    /// Voir <see cref="SeasonSettings"/>.
    /// </summary>
    public SeasonSettings Seasons { get; set; } = new();
    public bool Events { get; set; }

    /// <summary>
    /// Active le placement des obstacles statiques configurés (SYNE-071).
    /// Les constructions sont des obstacles statiques de la grille :
    /// disposés par <see cref="ObstacleLayout"/> à l'init, poés/retirés en cours de run
    /// par <c>World.PlaceConstruction</c>/<c>RemoveConstruction</c> (modification
    /// d'environnement tracée, événement <c>world.construction_placed</c>).
    /// </summary>
    public bool Obstacles { get; set; }

    /// <summary>
    /// Layout initial des obstacles statiques (disques) posés à l'init quand
    /// <see cref="Obstacles"/> est vrai (SYNE-071).
    /// </summary>
    public List<StaticObstacleSettings> ObstacleLayout { get; set; } = [];

    /// <summary>
    /// Zones de territoire (SYNE-073) :
    /// bloc <c>world.territories</c>. V0.1 : concept d'**observation** — la
    /// présence des entités dans une zone délimite le territoire effectif,
    /// suivi par la boucle et émis sans aucun comportement agentique.
    /// Désactivé par défaut ⇒ trajectoire du scénario de référence inchangée.
    /// </summary>
    public TerritorySettings Territories { get; set; } = new();

    /// <summary>
    /// Livres (SYNE-121) : bloc
    /// <c>world.books</c>. V0.1 : écriture/consultation via l'API de la boucle
    /// (coût en énergie payé par l'auteur ; bénéfice de lecture
    /// posé en principe —), activités seulement si
    /// <c>enabled</c>. Désactivé par défaut ⇒ trajectoire du scénario de référence
    /// inchangée (re-pin contractuel).
    /// </summary>
    public BookSettings Books { get; set; } = new();
}
