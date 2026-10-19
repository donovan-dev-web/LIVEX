// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Boucle de simulation (SYNE-002).

using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

namespace Simulation.Core.Loop;

/// <summary>
/// Boucle de simulation (SYNE-002).
/// <list type="bullet">
/// <item>1 tick = <c>simulatedSecondsPerTick</c> secondes simulées (défaut 60 →
/// 1 minute, /) — horloge instanciée <see cref="Clock"/> ; les
/// quantités de classe A sont mises à l'échelle une fois à la construction
/// (<see cref="Configuration.TemporalScale"/>), neutre par défaut.</item>
/// <item>Respect de <c>maxTicks</c> : la boucle s'arrête après le tick n° <c>maxTicks</c>.</item>
/// <item>L'état du PRNG avance d'un tirage par tick (flux ancré au tick index).</item>
/// <item>Depuis U1 : pipeline cognitif BDI (perception, mémoire, croyances, besoins,
///  objectifs, utilité, intention, action) exécuté à chaque tick (SYNE-010).</item>
/// </list>
/// </summary>
public sealed partial class SimulationLoop
{
    private readonly Simulation.Core.Cognition.CognitionPipeline _cognition;
    private readonly TickBudgetCollector? _budget;
    private readonly int _autoSaveEveryNTicks;
    private readonly Action<SimulationLoop>? _autosaveHandler;
    private readonly Simulation.Core.Configuration.SimulationOptions _options;
    private readonly Simulation.Core.Configuration.SimulationClock _clock;
    private readonly Simulation.Core.Configuration.ResourceSettings _effectiveResources;
    private readonly Simulation.Core.Configuration.SeasonSettings _effectiveSeasons;
    private readonly List<Simulation.Core.Configuration.SeasonChange> _seasonChanges = new();
    private readonly List<World.TerritoryMembershipChange> _territoryChanges = new();
    private readonly List<Simulation.Core.World.BookChange> _bookChanges = new();
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<ulong>> _territoryMembership =
        new(System.StringComparer.Ordinal);
    private Xoshiro256StarStar _rng;
    private bool _localizedResources;
    private double _resourceDt = 1.0;

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng)
        : this(world, initialRng, new Simulation.Core.Configuration.SimulationOptions())
    {
    }

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng, Simulation.Core.Configuration.SimulationOptions options)
        : this(world, initialRng, options, null)
    {
    }

    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        _options = options;
        _clock = Simulation.Core.Configuration.SimulationClock.From(options.Simulation);
        double dt = _clock.SimulatedMinutesPerTick;
        _effectiveResources = Simulation.Core.Configuration.TemporalScale.ScaleResources(options.Resources, dt);
        _effectiveSeasons = Simulation.Core.Configuration.TemporalScale.ScaleSeasons(options.World.Seasons, dt);
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        InitializeResourceModel(world, options, dt);
        _autoSaveEveryNTicks = options.Simulation.AutoSaveEveryNTicks;
        _autosaveHandler = null;
    }

    /// <summary>
    /// Boucle avec autosave branche : <paramref name="autosaveHandler"/> est appelé
    /// après chaque <c>n</c>-ième tick. Le handler ne doit
    /// consommer aucun tirage du PRNG — le déterminisme bit-à-bit reste inchangé.
    /// </summary>
    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget,
        int autoSaveEveryNTicks,
        Action<SimulationLoop>? autosaveHandler)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        _options = options;
        _clock = Simulation.Core.Configuration.SimulationClock.From(options.Simulation);
        double dt = _clock.SimulatedMinutesPerTick;
        _effectiveResources = Simulation.Core.Configuration.TemporalScale.ScaleResources(options.Resources, dt);
        _effectiveSeasons = Simulation.Core.Configuration.TemporalScale.ScaleSeasons(options.World.Seasons, dt);
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        InitializeResourceModel(world, options, dt);
        _autoSaveEveryNTicks = Math.Max(1, autoSaveEveryNTicks);
        _autosaveHandler = autosaveHandler;
    }
}
