// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ResourceSettings).

namespace Simulation.Core.Configuration;

public sealed class ResourceSettings
{
    /// <summary>
    /// Gisements <b>localisés</b> actifs — décision de modèle du réalignement
    /// (Phase 0) : les ressources sont des sources spatiales que l'agent doit
    /// découvrir, mémoriser, croire et rejoindre, et non un stock mondial abstrait.
    /// Les réserves globales (<c>resources.food/water/wood/mineral</c>) ne sont plus
    /// qu'un <b>agrégat publié</b> (somme des gisements) pour l'observabilité : la
    /// consommation et la régénération se font au gisement.
    /// <para>
    /// Faux = modèle historique (réserve mondiale unique) : aucune source
    /// spatiale n'est posée et Eat/Drink prélèvent la réserve globale. Les deux
    /// modèles sont testés ; le profil de référence porte le modèle localisé.
    /// </para>
    /// </summary>
    public bool Localized { get; set; } = true;

    /// <summary>Nombre de gisements par type (doit suivre la description du monde : 3).</summary>
    public int NodesPerType { get; set; } = 3;

    /// <summary>Quantité initiale d'un gisement (égale à la quantité publiée par la description du monde).</summary>
    public double NodeInitialAmount { get; set; } = 100.0;

    /// <summary>Capacité d'un gisement (plafond de régénération).</summary>
    public double NodeCapacity { get; set; } = 100.0;

    /// <summary>Régénération d'un gisement par tick (module saisonnier appliqué).</summary>
    public double NodeRegenerationRate { get; set; } = 1.0;

    /// <summary>
    /// Rayon d'interaction avec un gisement : distance maximale à laquelle une
    /// action terminale (Eat/Drink/Take) prélève la source, et à laquelle
    /// l'arrivée à une destination est considérée atteinte.
    /// </summary>
    public double InteractionRadius { get; set; } = 2.0;

    /// <summary>
    /// Nourriture — <b>calibration B1 </b> : réserve initiale 20 000 et
    /// régénération nette 20/tick, **sans** dégradation (« degradationTick »
    /// neutralisé) : avec une dégradation de période, la régénération est
    /// intégralement annulée en fin de période (apport net nul) et la réserve se
    /// réduit à sa valeur initiale — 100 agents épuisent alors les 10 000 de D1
    /// avant t2000. L'horizon de calibration est 2500 ticks.
    /// </summary>
    public ResourceSpec Food { get; set; } = new() { Initial = 20_000, RegenerationRate = 20, DegradationTick = null };

    /// <summary>Eau — calibration B1 : réserve 20 000, régénération nette 10/tick.</summary>
    public ResourceSpec Water { get; set; } = new() { Initial = 20_000, RegenerationRate = 10 };
    public ResourceSpec Wood { get; set; } = new() { Initial = 50, RegenerationRate = 0.1 };
    public ResourceSpec Mineral { get; set; } = new() { Initial = 0, RegenerationRate = 0 };

    /// <summary>Niveaux initiaux des quatre réserves (SYNE-070), ordre stable du type.</summary>
    internal Dictionary<World.ResourceKind, double> ToStocks()
    {
        return new Dictionary<World.ResourceKind, double>
        {
            [World.ResourceKind.Food] = Food.Initial,
            [World.ResourceKind.Water] = Water.Initial,
            [World.ResourceKind.Wood] = Wood.Initial,
            [World.ResourceKind.Mineral] = Mineral.Initial,
        };
    }

    /// <summary>Spécification d'une ressource par type (SYNE-070) — résolution garantie.</summary>
    internal ResourceSpec Spec(World.ResourceKind kind)
    {
        return kind switch
        {
            World.ResourceKind.Food => Food,
            World.ResourceKind.Water => Water,
            World.ResourceKind.Wood => Wood,
            World.ResourceKind.Mineral => Mineral,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Type de ressource inconnu."),
        };
    }
}
