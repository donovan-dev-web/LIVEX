// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de l'étape cognitive du mode couplé (P9) : délibération seule,
//        aucune exécution locale, décisions retournées au monde externe.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

public sealed class CoupledStepTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    private static (WorldType World, SimulationLoop Loop, Entity[] Entities) BuildLoop()
    {
        var world = new WorldType(new WorldSize(500, 500));
        Entity[] entities =
        [
            MakeEntity(1, new Position(50, 50)),
            MakeEntity(2, new Position(80, 50)),
            MakeEntity(3, new Position(300, 300)),
        ];
        foreach (Entity entity in entities)
        {
            world.AddEntity(entity);
        }

        return (world, new SimulationLoop(world, Xoshiro256StarStar.Create(7), Options()), entities);
    }

    [Fact]
    public void StepCoupled_ReturnsOneDecisionPerAgent()
    {
        (_, SimulationLoop loop, Entity[] entities) = BuildLoop();
        Assert.Equal(entities.Length, loop.World.Entities.Count);

        // Délibération à fréquence configurable — 10 ticks, (tick + id) % 10 == 0 —
        // donc chaque agent délibère une fois sur la fenêtre.
        var decisions = new List<DecisionRecord>();
        for (ulong tick = 1; tick <= 10; tick++)
        {
            decisions.AddRange(loop.Cognition.StepCoupled(tick));
        }

        Assert.Equal(
            [1UL, 2UL, 3UL],
            decisions.Select(decision => decision.EntityId).Distinct().Order().ToArray());
        Assert.All(decisions, decision => Assert.False(decision.Missing));
        Assert.All(decisions, decision => Assert.True(decision.Tick is >= 1 and <= 10));
    }

    [Fact]
    public void StepCoupled_ReportsNoDecisionOnAHoldoverTick()
    {
        // Un tick où l'agent ne délibère pas et n'a aucune décision antérieure est
        // une ABSENCE de décision, pas une commande : rien à envoyer à PRISM.
        (_, SimulationLoop loop, _) = BuildLoop();

        Assert.Empty(loop.Cognition.StepCoupled(currentTick: 1));
        Assert.True(loop.Cognition.HasMind(1));
    }

    [Fact]
    public void StepCoupled_NeverMovesAnythingLocally()
    {
        // L'action appartient à PRISM : SYNE décide, il ne déplace pas une seconde
        // fois les agents dans un monde miroir.
        (_, SimulationLoop loop, Entity[] entities) = BuildLoop();
        Position[] before = entities.Select(entity => entity.Position).ToArray();

        for (ulong tick = 1; tick <= 20; tick++)
        {
            loop.Cognition.StepCoupled(tick);
        }

        Assert.Equal(before, entities.Select(entity => entity.Position).ToArray());
    }

    [Fact]
    public void StepCoupled_AdvancesBodyNeeds()
    {
        (_, SimulationLoop loop, _) = BuildLoop();

        loop.Cognition.StepCoupled(currentTick: 1);
        double hungerAfterFirstTick = loop.Cognition.MindOf(1).Needs.Hunger;
        loop.Cognition.StepCoupled(currentTick: 2);

        Assert.True(loop.Cognition.MindOf(1).Needs.Hunger > hungerAfterFirstTick);
    }

    [Fact]
    public void StepCoupled_DecisionsCarryTheExecutedAction()
    {
        // La décision publiée porte l'action retenue (étape de plan comprise),
        // pas seulement l'intention délibérée : c'est elle que le write-back
        // traduit en commande.
        (_, SimulationLoop loop, _) = BuildLoop();

        var decisions = new List<DecisionRecord>();
        for (ulong tick = 1; tick <= 10; tick++)
        {
            decisions.AddRange(loop.Cognition.StepCoupled(tick));
        }

        Assert.NotEmpty(decisions);
        foreach (DecisionRecord decision in decisions)
        {
            MindState mind = loop.Cognition.MindOf(decision.EntityId);
            Assert.Equal(mind.LastDecision!.Value.Kind, decision.Chosen);
        }
    }

    [Fact]
    public void StepCoupled_KeepsTheLocalTrajectoryUntouched()
    {
        // Un run local ne doit pas changer parce que le chemin couplé existe :
        // même graine, mêmes positions, mêmes besoins après le même nombre de
        // ticks de la boucle locale.
        (WorldType worldA, SimulationLoop loopA, _) = BuildLoop();
        (_, SimulationLoop loopB, _) = BuildLoop();

        loopA.AdvanceOneTick();
        loopA.AdvanceOneTick();
        loopB.AdvanceOneTick();
        loopB.AdvanceOneTick();

        Assert.Equal(
            worldA.Entities.Select(entity => entity.Position).ToArray(),
            loopB.World.Entities.Select(entity => entity.Position).ToArray());
        Assert.Equal(loopA.Cognition.MindOf(1).Needs.Hunger, loopB.Cognition.MindOf(1).Needs.Hunger);
    }
}
