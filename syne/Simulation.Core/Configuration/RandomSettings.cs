// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (RandomSettings).

namespace Simulation.Core.Configuration;

public sealed class RandomSettings
{
    public ulong Seed { get; set; } = 12_345;
    public string Engine { get; set; } = "xoshiro256**";
}
