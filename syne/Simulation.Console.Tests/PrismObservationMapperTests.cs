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
        // SYNE-222 : un chantier est typé « site » (croyances dédiées), plus « node ».
        Assert.Contains(result, o => o.EntityType == "site" && o.Attributes["label"] == "site-1" && o.Attributes["amount"] == "5");
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

    [Fact]
    public void MapsTheNumericEnumKindsPrismActuallyWrites()
    {
        // PRISM sérialise PerceptionChangeKind / PerceptionEntityKind en nombres
        // (aucun convertisseur d'enum dans ProtocolJson) : attendre un libellé
        // faisait ignorer toutes les observations réelles sans aucune erreur.
        var changes = new object[]
        {
            new { change = 0, entry = new { kind = 0, x = 3, y = 4, z = 5, id = 0UL, materialId = 2, state = 1 } },
            new { change = 1, entry = new { kind = 2, x = 1, y = 2, z = 3, id = 42UL } },
            new { change = 2, entry = new { kind = 0, x = 9, y = 9, z = 9, id = 0UL, materialId = 2, state = 1 } },
        };
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            9,
            1,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        var result = PrismObservationMapper.ToObservations(observation);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.EntityType == "obstacle" && o.Position.X == 3 && o.Position.Y == 4);
        Assert.Contains(result, o => o.EntityType == "entity" && o.EntityId == 42UL);
    }

    [Fact]
    public void ToSpatialChunks_MapsRegionSummariesAsChunkCoordinates()
    {
        var changes = new object[]
        {
            new { change = 0, entry = new { kind = 3, x = 2, y = 1, z = 0, id = 7UL, tagMask = 5u } },
            new { change = 1, entry = new { kind = 3, x = 0, y = 4, z = 0, id = 12UL, tagMask = 1u } },
            new { change = 2, entry = new { kind = 3, x = 9, y = 9, z = 0, id = 30UL, tagMask = 7u } },
            new { change = 0, entry = new { kind = 0, x = 3, y = 4, z = 5, id = 0UL, materialId = 2, state = 1 } },
        };
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            4,
            1,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        IReadOnlyList<SpatialChunkObservation> chunks = PrismObservationMapper.ToSpatialChunks(observation);

        // Les résumés disparus sont ignorés et les blocs ne sont pas des chunks.
        Assert.Equal(2, chunks.Count);
        Assert.Contains(new SpatialChunkObservation(2, 1, 5u), chunks);
        Assert.Contains(new SpatialChunkObservation(0, 4, 1u), chunks);
    }

    [Fact]
    public void ToSpatialChunks_AcceptsAnEnvelopeAndSkipsEmptyMasks()
    {
        var changes = new object[]
        {
            new { change = 0, entry = new { kind = 3, x = 1, y = 1, z = 0, id = 3UL, tagMask = 0u } },
            new { change = 0, entry = new { kind = 3, x = 2, y = 2, z = 0, id = 4UL, tagMask = 2u } },
        };
        var envelope = new ObservationEnvelope(
            ProtocolVersion.Current.ToString(),
            5,
            9,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        IReadOnlyList<SpatialChunkObservation> chunks = PrismObservationMapper.ToSpatialChunks(envelope);

        Assert.Equal([new SpatialChunkObservation(2, 2, 2u)], chunks);
    }

    [Fact]
    public void ToCarriedItemIds_ReadsTheObservedInventory()
    {
        // `self.inventory[]` publie l'identifiant et la quantité, jamais les tags :
        // c'est le catalogue de contenu qui dira si l'objet se mange (étape 3b).
        var envelope = new ObservationEnvelope(
            ProtocolVersion.Current.ToString(),
            12,
            4,
            JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(
                new
                {
                    position = new { x = 1, y = 2, z = 3 },
                    inventory = new object[]
                    {
                        new { itemTypeId = 9, quantity = 2, maxStackSize = 64 },
                        new { itemTypeId = 2, quantity = 0, maxStackSize = 64 },
                        new { itemTypeId = 5, quantity = 1, maxStackSize = 16 },
                    },
                },
                ProtocolJson.Options));

        IReadOnlyList<ushort> carried = PrismObservationMapper.ToCarriedItemIds(envelope);

        // Trié, sans l'entrée vide, sans doublon.
        Assert.Equal([5, 9], carried);
    }

    [Fact]
    public void ToCarriedItemIds_WithoutInventory_IsEmpty()
    {
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            12,
            4,
            JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        Assert.Empty(PrismObservationMapper.ToCarriedItemIds(observation));
    }

    [Fact]
    public void MapsAnEdibleGroundItemToANamedFoodTarget()
    {
        // Sans nom reconnaissable, un tas de blé reste « l'objet 4 » et la faim ne
        // trouve jamais de cible : le catalogue de contenu donne le nom canonique
        // que `Knowledge.KindFromLabel` sait relire (étape 3b).
        var changes = new object[]
        {
            new { change = "Appeared", entry = new { kind = "GroundItem", x = 6, y = 7, z = 2, id = 9UL, itemTypeId = 4, quantity = 3 } },
            new { change = "Appeared", entry = new { kind = "GroundItem", x = 8, y = 9, z = 2, id = 10UL, itemTypeId = 5, quantity = 1 } },
        };
        var observation = new PrismAgentObservation(
            ProtocolVersion.Current.ToString(),
            9,
            1,
            JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

        IReadOnlyList<Observation> mapped = PrismObservationMapper.ToObservations(
            observation,
            resourceSites: null,
            itemCatalog: PrismItemCatalog.FromIds([4]));

        Observation wheat = Assert.Single(mapped, item => item.EntityId == 9UL);
        Assert.Equal("resource-food-9", wheat.Attributes["label"]);
        Assert.Equal("3", wheat.Attributes["amount"]);

        // L'objet non comestible garde son étiquette brute : aucune invention.
        Observation stone = Assert.Single(mapped, item => item.EntityId == 10UL);
        Assert.Equal("5", stone.Attributes["label"]);
    }
}
