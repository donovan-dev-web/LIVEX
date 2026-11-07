// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de l'empreinte de fin de run couplé (PRISM-082, run R2).

using Simulation.Console.Integration;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class CoupledFingerprintTests
{
    private static (SimulationLoop Loop, Entity Entity) Build()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(31);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        var loop = new SimulationLoop(world, next, options);
        loop.AdvanceOneTick();
        return (loop, entity);
    }

    [Fact]
    public void TheSameStateProducesTheSameFingerprint()
    {
        (SimulationLoop loop, _) = Build();

        string first = CoupledFingerprint.Compute(loop, lastTick: 1);
        string second = CoupledFingerprint.Compute(loop, lastTick: 1);

        Assert.Equal(first, second);
        Assert.Equal(16, first.Length);
    }

    [Fact]
    public void TheLastTickIsPartOfTheFingerprint()
    {
        (SimulationLoop loop, _) = Build();

        Assert.NotEqual(
            CoupledFingerprint.Compute(loop, lastTick: 1),
            CoupledFingerprint.Compute(loop, lastTick: 2));
    }

    [Fact]
    public void MovingAnEntityChangesTheFingerprint()
    {
        (SimulationLoop loop, Entity entity) = Build();
        string before = CoupledFingerprint.Compute(loop, lastTick: 1);

        loop.World.Grid.Move(entity, new Position(entity.Position.X + 10.0, entity.Position.Y));

        Assert.NotEqual(before, CoupledFingerprint.Compute(loop, lastTick: 1));
    }

    [Fact]
    public void ANeedChangeChangesTheFingerprint()
    {
        (SimulationLoop loop, Entity entity) = Build();
        string before = CoupledFingerprint.Compute(loop, lastTick: 1);

        loop.Cognition.MindOf(entity.Id.Value).Needs.ExertEnergy(5.0);

        Assert.NotEqual(before, CoupledFingerprint.Compute(loop, lastTick: 1));
    }
}
