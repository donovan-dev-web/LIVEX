// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SimulationSettings).

namespace Simulation.Core.Configuration;

public sealed class SimulationSettings
{
    public int WorldWidth { get; set; } = 500;
    public int WorldHeight { get; set; } = 500;
    /// <summary>Taille des cellules de la description Unreal (unités monde).</summary>
    public double WorldCellSize { get; set; } = 10;
    public int MaxTicks { get; set; } = 1_000_000;
    public int TicksPerSecond { get; set; } = 10;

    /// <summary>
    /// Secondes simulées par tick (« échelle temporelle configurable »,
    ///— le second levier de temporalité, à côté de <see cref="TicksPerSecond"/>.
    /// Défaut <b>60</b> : 1 tick = 1 minute simulée (temporalité historique,
    ///— neutre par défaut, checksums dorés inchangés. Le profil
    /// <c>prism</c> porte 5 (1 tick = 5 s simulées, ratio R = 30 à 6 TPS).
    /// Plage validée [1, 3600]. Les quantités de classe A (taux de besoins,
    /// décroissances, régénérations, durées) sont mises à l'échelle une fois au
    /// démarrage du run (<see cref="TemporalScale"/>, <see cref="SimulationClock"/>).
    /// </summary>
    public int SimulatedSecondsPerTick { get; set; } = SimulationClock.DefaultSimulatedSecondsPerTick;
    public int AutoSaveEveryNTicks { get; set; } = 1000;
    public int MaxBackups { get; set; } = 5;
}
