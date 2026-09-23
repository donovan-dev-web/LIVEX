using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Population;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Mortalité par épuisement (SYNE-074, SYSTEM_SPEC.md §8) : une entité dont
/// l'énergie atteint 0 meurt — retrait du monde, des esprits et des groupes,
/// événement <c>agent_died</c>. Mécanisme déterministe (aucun PRNG).
/// </summary>
public class DeathSystemTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    [Fact]
    public void EntityAtZeroEnergy_IsRemovedFromWorldAndMinds()
    {
        // SYNE-074 : énergie sous le seuil fatal → mort au tick suivant.
        SimulationOptions options = Options();
        options.Agents.Life.DeathEnergyThreshold = 50;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        world.AddEntity(MakeEntity(2, new Position(80, 50)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);

        loop.AdvanceOneTick();
        Assert.Equal(2, loop.Cognition.Minds.Count);

        // Epuise fortement l'entité 1 (même si Rest récupère 0,5, elle reste ≤ 50).
        loop.Cognition.MindOf(1).Needs.ExertEnergy(200);
        loop.AdvanceOneTick();

        Assert.Single(loop.Cognition.Minds);
        Assert.Single(world.Entities);
        Assert.False(loop.Cognition.HasMind(1));
        Assert.True(loop.Cognition.HasMind(2));
    }

    [Fact]
    public void Death_EmitsAgentDiedObservation_DeterministicByIdOrder()
    {
        SimulationOptions options = Options();
        options.Agents.Life.DeathEnergyThreshold = 50;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        world.AddEntity(MakeEntity(2, new Position(80, 50)));
        world.AddEntity(MakeEntity(3, new Position(300, 300)));
        world.AddEntity(MakeEntity(4, new Position(50, 90)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);

        loop.AdvanceOneTick();
        loop.Cognition.MindOf(2).Needs.ExertEnergy(200);
        loop.Cognition.MindOf(4).Needs.ExertEnergy(200);
        loop.AdvanceOneTick();

        // Deux morts au même tick, rapportées par id croissant (déterminisme §5).
        Assert.Equal(2, loop.Cognition.Death.LastDeaths.Count);
        Assert.Equal(2UL, loop.Cognition.Death.LastDeaths[0].Tick);
        Assert.Equal(2UL, loop.Cognition.Death.LastDeaths[0].EntityId);
        Assert.Equal(4UL, loop.Cognition.Death.LastDeaths[1].EntityId);
        Assert.Equal(options.Agents.Life.EnergyExhaustionCause, loop.Cognition.Death.LastDeaths[0].Cause);

        Assert.Equal(2, loop.Cognition.Minds.Count);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void DeathDisabled_KeepsPopulationImmortal()
    {
        SimulationOptions options = Options();
        options.Agents.Life.DeathEnabled = false;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);
        loop.Run(300);

        Assert.True(loop.Cognition.HasMind(1));
        Assert.Single(loop.Cognition.Minds);
        Assert.Empty(loop.Cognition.Death.LastDeaths);
    }

    [Fact]
    public void DeceasedMember_PurgedFromGroup_OrGroupDissolves()
    {
        // SYNE-074 : les groupes émergents ne conservent jamais un membre mort.
        SimulationOptions options = Options();
        options.Groups.MinGroupSize = 2;
        options.Agents.Life.DeathEnergyThreshold = 50;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        world.AddEntity(MakeEntity(2, new Position(55, 55)));
        world.AddEntity(MakeEntity(3, new Position(60, 60)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);

        // Force une cohésion forte (trust partagé + croyance commune) pour former
        // un groupe de 3 au tick 10.
        loop.AdvanceOneTick();
        foreach (Entity entity in world.Entities)
        {
            MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            foreach (Entity peer in world.Entities.Where(o => o.Id.Value != entity.Id.Value))
            {
                for (int i = 0; i < 25; i++)
                {
                    mind.Trust.Interact(peer.Id.Value);
                }
            }

            mind.Beliefs.ApplyEvidence(
                new Fact("entity-9", "position", "10.00,10.00"),
                0.9,
                "perception-9",
                options.Agents.Beliefs,
                tick: 9);
        }

        loop.Cognition.Groups.Step(10, world.Entities.ToDictionary(e => e.Id.Value, e => loop.Cognition.MindOf(e.Id.Value)));
        Assert.True(loop.Cognition.Groups.Active.Count >= 1);

        loop.Cognition.MindOf(1).Needs.ExertEnergy(200);
        loop.AdvanceOneTick();

        foreach (Group group in loop.Cognition.Groups.Active)
        {
            Assert.DoesNotContain(1UL, group.Members);
        }

        if (loop.Cognition.Groups.Active.Count > 0)
        {
            // Le groupe survivant a redimensionné ses membres (2 restants)…
            Assert.All(loop.Cognition.Groups.Active, group => Assert.Equal(2, group.Members.Count));
        }
    }
}