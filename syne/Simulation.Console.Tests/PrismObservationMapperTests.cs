using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.Perception;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismObservationMapperTests
{
    [Fact]
    public void EmptyChangesReturnsEmpty()
    {
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            7,
            1,
            JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        var result = PrismObservationMapper.ToObservations(observation);

        Assert.Empty(result);
    }

    [Fact]
    public void MapsBlockGroundItemAgentAndConstructionSite()
    {
        var changes = new object[]
        {
            new { change = "Appeared", entry = new { kind = "Block", x = 3, y = 4, z = 5, id = 0, materialId = 2, state = 1 } },
            new { change = "Appeared", entry = new { kind = "GroundItem", x = 6, y = 7, z = 2, id = 9UL, itemTypeId = 4, quantity = 3 } },
            new { change = "Appeared", entry = new { kind = "Agent", x = 1, y = 2, z = 3, id = 42UL } },
            new { change = "Appeared", entry = new { kind = "ConstructionSite", x = 8, y = 9, z = 1, id = 7UL, siteId = "site-1", quantity = 5, workRequired = 10 } },
        };
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            9,
            1,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        var result = PrismObservationMapper.ToObservations(observation);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, o => o.EntityType == "obstacle" && o.Position.X == 3 && o.Position.Y == 4);
        Assert.Contains(result, o => o.EntityType == "node" && o.Attributes["label"] == "4" && o.Attributes["amount"] == "3");
        Assert.Contains(result, o => o.EntityType == "entity" && o.EntityId == 42UL);
        Assert.Contains(result, o => o.EntityType == "node" && o.Attributes["label"] == "site-1" && o.Attributes["amount"] == "5");
        Assert.All(result, o => Assert.Equal(9UL, o.Tick));
    }

    [Fact]
    public void DisappearedEntriesAreSkipped()
    {
        var changes = new object[]
        {
            new { change = "Disappeared", entry = new { kind = "Block", x = 1, y = 1, z = 1, id = 0, materialId = 1, state = 0 } },
            new { change = "RegionSummary", entry = new { kind = "RegionSummary", x = 0, y = 0, z = 0, id = 0u, tagMask = 7u } },
        };
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            3,
            1,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        var result = PrismObservationMapper.ToObservations(observation);

        Assert.Empty(result);
    }
}
