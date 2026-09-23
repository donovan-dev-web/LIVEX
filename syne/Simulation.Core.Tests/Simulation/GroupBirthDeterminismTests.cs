using System.Text;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Régression de déterminisme des systèmes du jalon ph6 (SYNE-060/061/062) :
/// deux exécutions du même scénario doivent produire exactement la même séquence
/// d'événements de groupes et de naissances (DETERMINISM.md §3, §6). Aucun tirage
/// PRNG n'est consommé par ces systèmes.
/// </summary>
public class GroupBirthDeterminismTests
{
    private static SimulationLoop BuildScenario(ulong populationSeed, ulong entityCount, SimulationOptions options)
    {
        var world = new WorldType(WorldSize.From(options), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(populationSeed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    private static string EventSequence(SimulationOptions options, int ticks, ulong seed = 12345)
    {
        SimulationLoop loop = BuildScenario(seed, entityCount: 25, options);
        var log = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();

            foreach (Social.GroupFormation formed in loop.Cognition.Groups.LastFormed)
            {
                log.Append("G+").Append(formed.GroupId).Append(':')
                    .Append(string.Join(',', formed.Members)).Append('@').Append(tick).AppendLine();
            }

            foreach (Social.GroupDissolution dissolved in loop.Cognition.Groups.LastDissolved)
            {
                log.Append("G-").Append(dissolved.GroupId)
                    .Append(dissolved.Success ? ":ok" : ":ko")
                    .Append('@').Append(tick).AppendLine();
            }

            foreach (Social.GroupDecision decision in loop.Cognition.Groups.LastDecisions)
            {
                log.Append("GD").Append(decision.GroupId).Append(':')
                    .Append(decision.Decision).Append('@').Append(tick).AppendLine();
            }

            foreach (Population.BirthObservation birth in loop.Cognition.Birth.LastBirths)
            {
                log.Append("B").Append(birth.ChildId).Append(':')
                    .Append(birth.MotherId).Append('x').Append(birth.FatherId)
                    .Append('@').Append(tick).AppendLine();
            }
        }

        return log.ToString();
    }

    [Fact]
    public void SameOptions_SameSeed_ProduceIdenticalGroupAndBirthSequence()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        string first = EventSequence(options, ticks: 300);
        string second = EventSequence(options, ticks: 300);

        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        string first = EventSequence(options, ticks: 300, seed: 12345);
        string second = EventSequence(options, ticks: 300, seed: 12346);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SnapshotGroupEvents_AreEmittedThroughContractTypes()
    {
        // Les événements suivent la nomenclature API_CONTRACTS §2.2.
        Assert.Equal("group_formed", ObservabilityContract.GroupFormed);
        Assert.Equal("group_dissolved", ObservabilityContract.GroupDissolved);
        Assert.Equal("group_decision", ObservabilityContract.GroupDecision);
        Assert.Equal("agent_spawned", ObservabilityContract.AgentSpawned);
        Assert.Equal("0.6.0", ObservabilityContract.EngineVersion);
    }
}