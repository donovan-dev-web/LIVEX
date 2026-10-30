// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Simulation;

/// <summary>
/// Scénario du monde fictif. Permet de parcourir l'assistant sans installer
/// quoi que ce soit, y compris les cas dégradés (installation existante,
/// PRISM absent, échec en cours d'écriture).
/// </summary>
public enum DemoScenario
{
    /// <summary>Machine vierge : installation nominale.</summary>
    FreshInstall,

    /// <summary>Une installation valide existe déjà : parcours de maintenance.</summary>
    ExistingInstall,

    /// <summary>Installation nominale avec PRISM désélectionné (avertissement).</summary>
    PrismAbsent,

    /// <summary>Échec injecté à l'étape d'écriture, pour vérifier l'écran d'erreur.</summary>
    FailureDuringWrite
}
