// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (DesireSettings) — désirs instrumentaux (SYNE-222).

namespace Simulation.Core.Configuration;

/// <summary>
/// Désirs (section <c>agents.desires</c>) : les désirs physiologiques restent
/// déclenchés par les seuils de besoin (<c>agents.needs</c>) ; cette section
/// porte les désirs <b>instrumentaux</b> qui visent un moyen plutôt qu'un état.
/// </summary>
public sealed class DesireSettings
{
    /// <summary>Désirs instrumentaux et sécurité alimentaire (SYNE-222) — désactivé par défaut.</summary>
    public InstrumentalSettings Instrumental { get; set; } = new();
}
