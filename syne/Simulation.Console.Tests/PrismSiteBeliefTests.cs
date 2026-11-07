using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Perception;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Console.Tests;

/// <summary>
/// SYNE-222 : les chantiers et les structures perçues par PRISM deviennent des
/// croyances dédiées (<c>site-&lt;id&gt;</c>, <c>structure-&lt;id&gt;</c>), distinctes
/// des croyances de gisement.
/// </summary>
public sealed class PrismSiteBeliefTests
{
    private static (SimulationLoop Loop, Entity Entity) Build()
    {
        var world = new WorldType(new WorldSize(2240, 2240), spatialCellSize: 32);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(7);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 7, bornAt: 0);
        world.AddEntity(entity);
        return (new SimulationLoop(world, next, ConfigLoader.LoadDefaults()), entity);
    }

    private static PrismAgentObservation Observation(object[] changes, object? self = null)
    {
        JsonElement selfElement = self is null
            ? JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(self, ProtocolJson.Options);
        return new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            5,
            7,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            selfElement);
    }

    [Fact]
    public void ConstructionSite_BecomesSiteStateAndProgressBeliefs()
    {
        (SimulationLoop loop, Entity entity) = Build();
        var envelope = new ObservationEnvelope(
            ProtocolVersion.Current.ToString(),
            5,
            7,
            JsonSerializer.SerializeToElement(
                new
                {
                    changes = new object[]
                    {
                        new
                        {
                            change = 0,
                            entry = new
                            {
                                kind = 4,
                                x = 120,
                                y = 80,
                                z = 0,
                                id = 0UL,
                                siteId = "atelier",
                                state = 2,
                                quantity = 3,
                                workRequired = 10,
                            },
                        },
                    },
                },
                ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        IReadOnlyList<Observation> mapped = PrismObservationMapper.ToObservations(envelope);
        loop.Cognition.ApplyExternalObservations(entity, mapped, currentTick: 5);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.True(beliefs.TryGetCurrent("site-atelier", KnowledgeBuilder.SiteStatePredicate, out Belief state));
        Assert.StartsWith("120.00,80.00,", state.Fact.Value, StringComparison.Ordinal);
        Assert.EndsWith(",2", state.Fact.Value, StringComparison.Ordinal);

        Assert.True(beliefs.TryGetCurrent("site-atelier", KnowledgeBuilder.SiteProgressPredicate, out Belief progress));
        Assert.Equal("3,10", progress.Fact.Value);

        // Un chantier n'est pas une cible de ressource : aucun sujet « node- » créé.
        Assert.DoesNotContain(beliefs.All, belief => belief.Fact.Subject.StartsWith("node-", StringComparison.Ordinal));
    }

    [Fact]
    public void ShelteredSelfObservation_BecomesAStructureBelief()
    {
        (SimulationLoop loop, Entity entity) = Build();
        var observation = Observation(
            Array.Empty<object>(),
            new
            {
                position = new { x = 120, y = 80, z = 0 },
                sheltered = true,
                shelterSiteId = "grenier-nord",
                fatigueRecoveryBonusPermille = 250,
                inventory = Array.Empty<object>(),
            });
        var envelope = new ObservationEnvelope(
            ProtocolVersion.Current.ToString(),
            5,
            7,
            observation.Changes,
            observation.Self);

        IReadOnlyList<Observation> mapped = PrismObservationMapper.ToObservations(envelope);
        loop.Cognition.ApplyExternalObservations(entity, mapped, currentTick: 5);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.True(beliefs.TryGetCurrent("structure-grenier-nord", KnowledgeBuilder.StructureShelterPredicate, out Belief shelter));
        Assert.Equal("250", shelter.Fact.Value);
        Assert.Equal(1.0, shelter.Confidence, precision: 6);
    }

    [Fact]
    public void UnshelteredSelfObservation_CreatesNoStructureBelief()
    {
        (SimulationLoop loop, Entity entity) = Build();
        var observation = Observation(
            Array.Empty<object>(),
            new
            {
                position = new { x = 10, y = 10, z = 0 },
                sheltered = false,
                inventory = Array.Empty<object>(),
            });
        var envelope = new ObservationEnvelope(
            ProtocolVersion.Current.ToString(),
            5,
            7,
            observation.Changes,
            observation.Self);

        IReadOnlyList<Observation> mapped = PrismObservationMapper.ToObservations(envelope);
        loop.Cognition.ApplyExternalObservations(entity, mapped, currentTick: 5);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.DoesNotContain(beliefs.All, belief => belief.Fact.Subject.StartsWith("structure-", StringComparison.Ordinal));
    }

    [Fact]
    public void SiteBelief_IsRevisedWhenTheSiteAdvances()
    {
        (SimulationLoop loop, Entity entity) = Build();
        void Apply(int completed, ulong tick)
        {
            var envelope = new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                (long)tick,
                7,
                JsonSerializer.SerializeToElement(
                    new
                    {
                        changes = new object[]
                        {
                            new
                            {
                                change = 1,
                                entry = new
                                {
                                    kind = 4,
                                    x = 120,
                                    y = 80,
                                    z = 0,
                                    id = 0UL,
                                    siteId = "atelier",
                                    state = 2,
                                    quantity = completed,
                                    workRequired = 10,
                                },
                            },
                        },
                    },
                    ProtocolJson.Options),
                JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

            loop.Cognition.ApplyExternalObservations(
                entity,
                PrismObservationMapper.ToObservations(envelope),
                tick);
        }

        Apply(2, 5);
        Apply(9, 6);

        BeliefSet beliefs = loop.Cognition.MindOf(entity.Id.Value).Beliefs;
        Assert.True(beliefs.TryGetCurrent("site-atelier", KnowledgeBuilder.SiteProgressPredicate, out Belief progress));
        Assert.Equal("9,10", progress.Fact.Value);
        Assert.Equal(6UL, progress.UpdatedTick);
    }
}
