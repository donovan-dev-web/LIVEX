// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (PerceptionSettings).

namespace Simulation.Core.Configuration;

public sealed class PerceptionSettings
{
    /// <summary>Rayon de perception — : défaut 50 (plage 30–70).</summary>
    public int Radius { get; set; } = 50;

    /// <summary>
    /// Portée de perception des <b>gisements</b> (repères statiques) — décision de
    /// modèle du réalignement : les gisements sont des repères du paysage, perçus
    /// de plus loin que les entités mobiles (défaut 200 contre 50). Sans cette
    /// portée, une source localisée dans un monde 500 × 500 est invisible depuis un
    /// point de départ aléatoire et la navigation dirigée n'a aucun objet à
    /// connaître : la boucle perception → mémoire → croyance → but serait vide.
    /// La perception reste <b>partielle</b> (rayon fini, masquage par obstacle) :
    /// la portée décide de ce qui est découvrable, pas de ce qui est vrai.
    /// </summary>
    public int ResourceRadius { get; set; } = 200;
    public double ConfidenceFalloff { get; set; } = 0.3;
    /// <summary>Perception étagée : rotation en groupes de <c>RotationInterval</c>.</summary>
    public int RotationInterval { get; set; } = 4;
    /// <summary>Ligne de vue : un obstacle masque la perception (SYNE-011).</summary>
    public bool LineOfSight { get; set; } = true;

    /// <summary>
    /// Taille des cellules de la grille spatiale, en unités monde. Volontairement
    /// <b>distincte</b> de <see cref="Radius"/> : la taille de cellule décide de
    /// l'ordre dans lequel <c>SpatialGrid.QueryCircle</c> parcourt les entités
    /// voisines, donc de l'ordre de consideration des perceptions — et par là de
    /// la trajectoire. Elle ne doit donc pas être un effet de bord d'un réglage
    /// de perception. C'est un paramètre de performance, à figer par graine.
    /// Valeur par défaut alignée sur le rayon de perception historique, afin de
    /// préserver la trajectoire de référence.
    /// </summary>
    public double SpatialCellSize { get; set; } = 50;
}
