// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (CommitmentSettings).

namespace Simulation.Core.Configuration;

public sealed class CommitmentSettings
{
    /// <summary>Engagements actifs (D5).</summary>
    public bool Enabled { get; set; }

    /// <summary>Durée de validité d'un engagement : non honoré au-delà → Broken (expiryTick).</summary>
    public ulong ExpiryTicks { get; set; } = 100;
}
