// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ActionCatalogSettings).

namespace Simulation.Core.Configuration;

public sealed class ActionCatalogSettings
{
    /// <summary>Entrées déclaratives par action (clé = nom de l'<c>DesireKind</c>, camelCase).</summary>
    public Dictionary<string, ActionEntrySettings> Entries { get; set; } = new(StringComparer.Ordinal)
    {
        ["idle"] = new ActionEntrySettings(),
        ["seekFood"] = new ActionEntrySettings { Movement = true },
        ["seekWater"] = new ActionEntrySettings { Movement = true },
        ["eat"] = new ActionEntrySettings { EnergyCost = 0.2, HungerRecovery = 30.0, Reserve = World.ResourceKind.Food, EnergyRecovery = 2.0 },
        ["drink"] = new ActionEntrySettings { EnergyCost = 0.2, ThirstRecovery = 30.0, Reserve = World.ResourceKind.Water, EnergyRecovery = 1.0 },
        ["rest"] = new ActionEntrySettings(),
        // Exutoires de besoin (réalignement, S-13/B-08) : Socialize et Explore
        // étaient déclarés « mouvement » sans qu'aucune entrée du catalogue ne
        // réduise Social/Curiosity — le besoin entretenait un manque sans le
        // réduire. Les satisfactions sont déclarées ici et appliquées sur
        // l'arrivée à la cible connue (pair cru / repère cru).
        ["flee"] = new ActionEntrySettings { Movement = true, SafetyRecovery = 0.30 },
        ["socialize"] = new ActionEntrySettings { Movement = true, SocialRecovery = 0.35 },
        ["explore"] = new ActionEntrySettings { Movement = true, CuriosityRecovery = 0.50 },
        // Primitives atomiques D7 (engineVersion 0.14.0) :
        // déclarées pour la complétude du catalogue (chaque DesireKind DOIT avoir une
        // entrée), mais inertes tant que agents.actions.plans.enabled = false (D3) —
        // elles ne sont jamais générées par la délibération par défaut.
        ["take"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["give"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["trade"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["attack"] = new ActionEntrySettings { EnergyCost = 0.5 },
        ["defend"] = new ActionEntrySettings(),
    };
}
