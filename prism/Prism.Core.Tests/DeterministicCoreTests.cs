using Prism.Core.Content;
using Prism.Core.Determinism;
using Prism.Core.Accounting;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class StableIdRegistryTests
{
    [Fact]
    public void RegistryUsesCanonicalOrdinalIdsAndSupportsRoundTrips()
    {
        var registry = new StableIdRegistry(["wood", "stone", "iron"]);

        Assert.Equal(3, registry.Count);
        Assert.Equal((ushort)0, registry.GetId("iron"));
        Assert.Equal((ushort)1, registry.GetId("stone"));
        Assert.Equal((ushort)2, registry.GetId("wood"));
        Assert.Equal("stone", registry.GetKey(1));
        Assert.True(registry.TryGetId("wood", out var woodId));
        Assert.Equal((ushort)2, woodId);
        Assert.False(registry.TryGetId("unknown", out _));
        Assert.False(registry.TryGetKey(3, out var missingKey));
        Assert.Null(missingKey);
    }

    [Fact]
    public void RegistryIdsDoNotDependOnInputOrder()
    {
        var first = new StableIdRegistry(["wood", "stone", "iron"]);
        var second = new StableIdRegistry(["iron", "wood", "stone"]);

        Assert.Equal(first.GetId("stone"), second.GetId("stone"));
        Assert.Equal(first.GetId("iron"), second.GetId("iron"));
        Assert.Equal(first.GetId("wood"), second.GetId("wood"));
    }

    [Fact]
    public void ContentRegistriesKeepTypeIdsInSeparateNamespaces()
    {
        var registries = new ContentRegistries(
            ["wood"],
            ["wood"],
            ["wood"],
            ["wood"]);

        Assert.Equal((ushort)0, registries.Materials.GetId("wood"));
        Assert.Equal((ushort)0, registries.Items.GetId("wood"));
        Assert.Equal((ushort)0, registries.Tags.GetId("wood"));
        Assert.Equal((ushort)0, registries.Recipes.GetId("wood"));
    }

    [Theory]
    [InlineData("stone", "stone")]
    [InlineData("stone", "")]
    [InlineData("stone", " ")]
    public void RegistryRejectsInvalidKeys(string firstKey, string secondKey)
    {
        Assert.Throws<ArgumentException>(() => new StableIdRegistry([firstKey, secondKey]));
    }

    [Fact]
    public void RegistrySupportsEntireUnsignedSixteenBitIdSpace()
    {
        var registry = new StableIdRegistry(Enumerable.Range(0, ushort.MaxValue + 1)
            .Select(index => $"key-{index:D5}"));

        Assert.Equal(ushort.MaxValue + 1, registry.Count);
        Assert.Equal("key-65535", registry.GetKey(ushort.MaxValue));
    }
}

public sealed class DeterministicRandomTests
{
    [Fact]
    public void Mix64MatchesFixedVector()
    {
        Assert.Equal(0x5692161D100B05E5UL, DeterministicRandom.Mix64(1));
    }

    [Fact]
    public void NamedXoshiroStreamMatchesFixedVector()
    {
        var streams = new NamedRandomStreams(12_345, [RandomStreamNames.Terrain]);

        Assert.Equal(0x9700C4ED272DFA9AUL, streams.GetStream(RandomStreamNames.Terrain).NextUInt64());
        Assert.Equal(0x025511703239EBE6UL, streams.GetStream(RandomStreamNames.Terrain).NextUInt64());
        Assert.Equal(0xE893D3CB071C4FC7UL, streams.GetStream(RandomStreamNames.Terrain).NextUInt64());
    }

    [Fact]
    public void AddingOrAdvancingAnotherNamedStreamDoesNotChangeTerrainSequence()
    {
        var original = new NamedRandomStreams(7, [RandomStreamNames.Terrain, RandomStreamNames.Ores]);
        var extended = new NamedRandomStreams(7, [RandomStreamNames.Ores, "newStream", RandomStreamNames.Terrain]);
        _ = original.GetStream(RandomStreamNames.Ores).NextUInt64();
        _ = extended.GetStream("newStream").NextUInt64();
        _ = extended.GetStream(RandomStreamNames.Ores).NextUInt64();

        Assert.Equal(
            original.GetStream(RandomStreamNames.Terrain).NextUInt64(),
            extended.GetStream(RandomStreamNames.Terrain).NextUInt64());
    }

    [Fact]
    public void CounterValueIsStatelessAndSeparatedByStream()
    {
        var streams = new NamedRandomStreams(99);

        var first = streams.GetCounterValue(RandomStreamNames.RandomTicks, 12, 34);
        _ = streams.GetStream(RandomStreamNames.Terrain).NextUInt64();

        Assert.Equal(first, streams.GetCounterValue(RandomStreamNames.RandomTicks, 12, 34));
        Assert.NotEqual(first, streams.GetCounterValue(RandomStreamNames.Conflicts, 12, 34));
    }

    [Fact]
    public void XoshiroRangeIsExclusiveAndRejectsInvalidBounds()
    {
        var random = new Xoshiro256StarStar(123);

        for (var index = 0; index < 100; index++)
        {
            Assert.InRange(random.NextInt(-4, 9), -4, 8);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(2, 2));
    }

    [Fact]
    public void FixedPointNoiseProducesStableValuesForPositiveAndNegativeCoordinates()
    {
        Assert.Equal(16016, FixedPointNoise.SampleValue2D(0x1234, "terrain", 37, 91, 16));
        Assert.Equal(6248, FixedPointNoise.SampleValue2D(0x1234, "terrain", -37, -91, 16));
        Assert.Equal(14114, FixedPointNoise.SampleFractal2D(0x1234, "terrain", 37, 91, 32, 4, 500));
        Assert.Equal(14902, FixedPointNoise.SampleWarpedFractal2D(0x1234, "terrain", 37, 91, 32, 4, 500, 8, 3));
    }

    [Fact]
    public void FixedPointNoiseIsBoundedAndIndependentOfSamplingOrder()
    {
        const ulong seed = 99;
        var first = FixedPointNoise.SampleFractal2D(seed, "terrain", 80, 120, 24, 5, 550);
        _ = FixedPointNoise.SampleFractal2D(seed, "ores", 80, 120, 12, 3, 400);
        var second = FixedPointNoise.SampleFractal2D(seed, "terrain", 80, 120, 24, 5, 550);

        Assert.Equal(first, second);
        Assert.InRange(first, short.MinValue, short.MaxValue);
        Assert.InRange(FixedPointNoise.SampleWarpedFractal2D(seed, "terrain", -100, 46, 24, 5, 550, 12, 5),
            short.MinValue, short.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixedPointNoise.SampleFractal2D(seed, "terrain", 0, 0, 16, 0, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FixedPointNoise.SampleFractal2D(seed, "terrain", 0, 0, 16, 2, 1001));
    }

    [Fact]
    public void FixedPointNoiseSamplerProducesStableThreeDimensionalValues()
    {
        var noise = FixedPointNoise.CreateSampler(0x1234, "ores.iron");

        Assert.Equal(-9730, noise.SampleValue3D(17, 29, 11, 8));
        Assert.Equal(-27154, noise.SampleValue3D(-17, -29, -11, 8));
    }
}

public sealed class WorldGridTests
{
    [Fact]
    public void EmptyGridHashesMatchFixedLittleEndianVectors()
    {
        var world = new WorldGrid(1, 1, regionSize: 1);

        Assert.Equal(0xD2F3777ECD053574UL, WorldGridHasher.ComputeRegionHash(world, 0));
        Assert.Equal(0xC2DD49B093117952UL, WorldGridHasher.ComputeWorldHash(world));
    }

    [Fact]
    public void SurfaceHeightGenerationIsRepeatableBoundedAndAppliesCliffs()
    {
        var config = new SurfaceHeightConfig(
            BaseHeight: 30,
            Amplitude: 12,
            BaseScale: 16,
            Octaves: 3,
            PersistencePermille: 500,
            WarpScale: 32,
            WarpStrength: 3,
            BorderWidth: 2);
        var first = SurfaceHeightGenerator.Generate(20, 18, 64, 42, config);
        var second = SurfaceHeightGenerator.Generate(20, 18, 64, 42, config);
        var otherSeed = SurfaceHeightGenerator.Generate(20, 18, 64, 43, config);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherSeed);
        Assert.Equal(56, first[0]);
        Assert.Equal(32, first[2 + 20 * 2]);
        for (var y = 0; y < 18; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var height = first[x + 20 * y];
                if (x < 2 || y < 2 || x >= 18 || y >= 16)
                {
                    Assert.Equal(56, height);
                }
                else
                {
                    Assert.InRange(height, 18, 42);
                }
            }
        }
    }

    [Fact]
    public void SurfaceHeightGenerationRejectsInvalidWorldAndBorderConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurfaceHeightGenerator.Generate(12, 12, 64, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurfaceHeightGenerator.Generate(16, 16, 64, 1, new SurfaceHeightConfig(BaseHeight: 64)));
    }

    [Fact]
    public void WorldZonesAreDeterministicAndUseShoreRockForestAndPlainRules()
    {
        byte[] heights = [8, 20, 45, 20, 8, 20, 20, 20];
        var forestZones = WorldZoneGenerator.Generate(
            4,
            2,
            heights,
            64,
            123,
            new WorldZoneConfig(
                SeaLevelZ: 10,
                ShoreBand: 0,
                ForestThresholdPermille: -1000,
                RockyThresholdPermille: 1000,
                RockyMinHeight: 40));
        var repeat = WorldZoneGenerator.Generate(
            4,
            2,
            heights,
            64,
            123,
            new WorldZoneConfig(
                SeaLevelZ: 10,
                ShoreBand: 0,
                ForestThresholdPermille: -1000,
                RockyThresholdPermille: 1000,
                RockyMinHeight: 40));
        var plainZones = WorldZoneGenerator.Generate(
            4,
            2,
            heights,
            64,
            123,
            new WorldZoneConfig(
                SeaLevelZ: 10,
                ShoreBand: 0,
                ForestThresholdPermille: 1000,
                RockyThresholdPermille: 1000,
                RockyMinHeight: 40));

        Assert.Equal(forestZones, repeat);
        Assert.Equal(WorldZone.Shore, forestZones[0]);
        Assert.Equal(WorldZone.Rocky, forestZones[2]);
        Assert.Equal(WorldZone.Forest, forestZones[1]);
        Assert.Equal(WorldZone.Plains, plainZones[1]);
    }

    [Fact]
    public void GeneratedSurfaceStoresZoneMaterialAndResolvesStrata()
    {
        var generated = WorldSurfaceGenerator.Generate(
            20,
            18,
            8,
            64,
            42,
            new SurfaceHeightConfig(BorderWidth: 2),
            new WorldZoneConfig(
                SeaLevelZ: 10, ShoreBand: 0, RockyThresholdPermille: 1000, RockyMinHeight: 63),
            new WorldSurfaceMaterialIds(Grass: 10, Sand: 11, Stone: 12, Soil: 13, Bedrock: 14));
        var column = generated.Grid.GetSurfaceColumn(10, 10);
        var profile = generated.Profiles.Single(value => value.Id == column.StrataProfileId);

        Assert.Equal(WorldZone.Plains, generated.Zones[10 + 20 * 10]);
        Assert.Equal((ushort)10, column.MaterialId);
        Assert.Equal((ushort)14, WorldStrataResolver.ResolveMaterialId(column, 0, profile));
        Assert.Equal((ushort)10, WorldStrataResolver.ResolveMaterialId(column, column.SurfaceZ, profile));
        Assert.Equal((ushort)13, WorldStrataResolver.ResolveMaterialId(column, column.SurfaceZ - 1, profile));
        Assert.Equal((ushort)12, WorldStrataResolver.ResolveMaterialId(column, column.SurfaceZ - 5, profile));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldStrataResolver.ResolveMaterialId(column, column.SurfaceZ + 1, profile));
    }

    [Fact]
    public void OreDepositsAreDeterministicClusteredAndStayBelowSurface()
    {
        var first = CreateFlatWorld(32, 24, 40);
        var second = CreateFlatWorld(32, 24, 40);
        var vein = new OreVeinDefinition("iron", 25, 3, 16, 5, 6000);

        var firstCount = OreDepositGenerator.Generate(first, 9123, [vein]);
        var secondCount = OreDepositGenerator.Generate(second, 9123, [vein]);

        Assert.True(firstCount > 0);
        Assert.Equal(firstCount, secondCount);
        Assert.Equal(WorldGridHasher.ComputeWorldHash(first), WorldGridHasher.ComputeWorldHash(second));
        Assert.All(first.EnumerateOverrides(), entry =>
        {
            Assert.Equal((ushort)25, entry.Value.MaterialId);
            Assert.InRange(entry.Key.Z, 24, 37);
        });
        Assert.True(firstCount < 32 * 24 * 14);
    }

    [Fact]
    public void WaterFillsLowColumnsToSeaLevelWithoutOverwritingExistingBlocks()
    {
        var world = new WorldGrid(3, 2, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(9, 1, 0, 0));
        world.SetSurfaceColumn(2, 0, new SurfaceColumn(12, 1, 0, 0));
        world.SetSurfaceColumn(0, 1, new SurfaceColumn(12, 1, 0, 0));
        world.SetSurfaceColumn(1, 1, new SurfaceColumn(12, 1, 0, 0));
        world.SetSurfaceColumn(2, 1, new SurfaceColumn(12, 1, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(0, 0, 5), new BlockOverride(22, 0, false));

        var count = WaterBodyGenerator.FillBelowSeaLevel(world, waterMaterialId: 99, seaLevelZ: 10);

        Assert.Equal(6, count);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(0, 0, 5), out var preserved));
        Assert.Equal((ushort)22, preserved.MaterialId);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(0, 0, 6), out var water));
        Assert.Equal((ushort)99, water.MaterialId);
        Assert.False(world.TryGetBlockOverride(new BlockCoordinate(2, 0, 13), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WaterBodyGenerator.FillBelowSeaLevel(world, 99, 16));
    }

    [Fact]
    public void ForestTreesUseStableCellJitterAndZoneDensity()
    {
        var first = CreateFlatWorld(20, 20, 20);
        var second = CreateFlatWorld(20, 20, 20);
        var zones = Enumerable.Repeat(WorldZone.Forest, 20 * 20).ToArray();
        var templates = new[]
        {
            new WorldTreeTemplate("oak_small",
            [
                new WorldTreeBlock(0, 0, 0, 50),
                new WorldTreeBlock(0, 0, 1, 51),
            ]),
            new WorldTreeTemplate("oak_tall",
            [
                new WorldTreeBlock(0, 0, 0, 50),
                new WorldTreeBlock(0, 0, 1, 50),
                new WorldTreeBlock(0, 0, 2, 51),
            ]),
        };
        var config = new ForestPlacementConfig(
            CellSize: 5, JitterRadius: 1, ForestDensityPermille: 1000, PlainsDensityPermille: 0);

        var firstTrees = TreePlacementGenerator.Place(first, zones, 50, templates, config);
        var secondTrees = TreePlacementGenerator.Place(second, zones, 50, templates.Reverse(), config);
        var noTrees = TreePlacementGenerator.Place(
            CreateFlatWorld(20, 20, 20),
            Enumerable.Repeat(WorldZone.Shore, 20 * 20).ToArray(),
            50,
            templates,
            config);

        Assert.Equal(16, firstTrees.Count);
        Assert.Equal(firstTrees, secondTrees);
        Assert.Empty(noTrees);
        Assert.Equal(WorldGridHasher.ComputeWorldHash(first), WorldGridHasher.ComputeWorldHash(second));
        Assert.InRange(first.OverrideCount, firstTrees.Count * 2, firstTrees.Count * 3);
        Assert.All(firstTrees, tree => Assert.Equal(21, tree.Origin.Z));
    }

    [Fact]
    public void CompleteTerrainPipelineProducesRepeatableWorldAndMetadata()
    {
        var config = new WorldGenerationConfig(
            Width: 24,
            Height: 20,
            VerticalSize: 64,
            RegionSize: 8,
            WorldGenVersion: 1,
            HeightConfig: new SurfaceHeightConfig(
                BaseHeight: 12,
                Amplitude: 4,
                BaseScale: 12,
                Octaves: 3,
                WarpStrength: 0,
                BorderWidth: 2),
            Zones: new WorldZoneConfig(
                SeaLevelZ: 10,
                ShoreBand: 2,
                ForestThresholdPermille: -1000,
                RockyThresholdPermille: 1000,
                RockyMinHeight: 50),
            Materials: new WorldSurfaceMaterialIds(10, 11, 12, 13, 14),
            OreVeins: [new OreVeinDefinition("coal", 15, 3, 10, 6, 10000)],
            WaterMaterialId: 16,
            TreeTemplates:
            [
                new WorldTreeTemplate("oak", [new WorldTreeBlock(0, 0, 0, 17)]),
            ],
            Forest: new ForestPlacementConfig(5, 1, 500, 0));

        var first = WorldGenerator.Generate(4242, config);
        var second = WorldGenerator.Generate(4242, config);

        Assert.Equal(1, first.WorldGenVersion);
        Assert.Equal((ulong)4242, first.Seed);
        Assert.Equal(0, first.Attempt);
        Assert.Empty(first.SpawnSites);
        Assert.Equal(string.Empty, first.ContentHash);
        Assert.Equal(first.WorldHash, second.WorldHash);
        Assert.NotEqual(WorldGridHasher.ComputeWorldHash(first.Grid), first.WorldHash);
        Assert.Equal(
            WorldGridHasher.ComputeWorldHash(
                first.Grid,
                first.WorldGenVersion,
                first.Seed,
                first.Attempt,
                first.Zones,
                first.SpawnSites,
                first.ContentHash),
            first.WorldHash);
        Assert.Equal(first.Zones, second.Zones);
        Assert.Equal(first.Trees, second.Trees);
        Assert.Equal(first.OreBlocksPlaced, second.OreBlocksPlaced);
        Assert.Equal(first.WaterBlocksPlaced, second.WaterBlocksPlaced);
    }

    [Fact]
    public void WorldGenerationRetriesEightDerivedSeedsAndReturnsStructuredFailure()
    {
        var config = new WorldGenerationConfig(
            Width: 20,
            Height: 20,
            VerticalSize: 64,
            RegionSize: 8,
            WorldGenVersion: 1,
            HeightConfig: new SurfaceHeightConfig(BorderWidth: 2),
            Zones: new WorldZoneConfig(RockyMinHeight: 63),
            Materials: new WorldSurfaceMaterialIds(10, 11, 12, 13, 14),
            OreVeins: [],
            WaterMaterialId: 16,
            TreeTemplates: [],
            Forest: new ForestPlacementConfig(),
            SpawnResources: new WorldResourceMaterials(
                Water: 16,
                Wood: new HashSet<ushort>(),
                Stone: 12,
                Coal: 17,
                IronOre: 18,
                Farmland: 19,
                SolidMaterials: new HashSet<ushort> { 10, 11, 12, 13, 14 }),
            SpawnConfig: new SpawnSiteConfig());

        var exception = Assert.Throws<WorldGenerationException>(() => WorldGenerator.Generate(123, config));

        // This world cannot hold wood at all, so the failure is now reported by the global
        // resource floors (PRISM-036) rather than by the spawn-site search that follows them.
        Assert.Equal("world.resources.below_floor", exception.Code);
        Assert.Equal(8, exception.Attempts);
    }

    [Fact]
    public void SpawnSiteFloorsRejectWorldsBelowGlobalResourceMinimums()
    {
        var world = CreateFlatWorld(9, 9, 20);
        world.SetSurfaceColumn(3, 3, new SurfaceColumn(20, 3, 0, 0));
        world.SetSurfaceColumn(2, 4, new SurfaceColumn(20, 4, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(4, 3, 21), new BlockOverride(99, 0, false));
        world.SetBlockOverride(new BlockCoordinate(4, 5, 21), new BlockOverride(5, 0, false));
        world.SetBlockOverride(new BlockCoordinate(5, 4, 12), new BlockOverride(6, 0, false));
        world.SetBlockOverride(new BlockCoordinate(6, 4, 20), new BlockOverride(7, 0, false));
        var zones = Enumerable.Repeat(WorldZone.Plains, 9 * 9).ToArray();
        var materials = new WorldResourceMaterials(
            Water: 99,
            Wood: new HashSet<ushort> { 5 },
            Stone: 3,
            Coal: 6,
            IronOre: 7,
            Farmland: 4,
            SolidMaterials: new HashSet<ushort> { 1, 3, 4, 5, 6, 7 });

        var passing = SpawnSiteSelector.Select(
            world,
            zones,
            materials,
            77,
            new SpawnSiteConfig(Count: 1, MinimumSpacing: 4, MaximumResourceDistance: 12, MaximumCandidates: 81));
        var raised = SpawnSiteSelector.Select(
            world,
            zones,
            materials,
            77,
            new SpawnSiteConfig(
                Count: 1,
                MinimumSpacing: 4,
                MaximumResourceDistance: 12,
                MaximumCandidates: 81,
                Floors: new SpawnResourceFloors(Coal: 2)));

        Assert.True(passing.Succeeded, passing.Error);
        Assert.False(raised.Succeeded);
        Assert.Equal("world.resources.below_floor", raised.ErrorCode);
        Assert.Contains("coal 1 < 2", raised.Error);
        Assert.Empty(raised.Sites);
    }

    [Fact]
    public void ReferenceWorldSeedsProduceGoldenHashes()
    {
        var config = new WorldGenerationConfig(
            Width: 32,
            Height: 32,
            VerticalSize: 64,
            RegionSize: 8,
            WorldGenVersion: 1,
            HeightConfig: new SurfaceHeightConfig(BorderWidth: 2),
            Zones: new WorldZoneConfig(),
            Materials: new WorldSurfaceMaterialIds(10, 11, 12, 13, 14),
            OreVeins: [],
            WaterMaterialId: 16,
            TreeTemplates: [],
            Forest: new ForestPlacementConfig(),
            ContentHash: "minimal-content-v0");

        var hashes = new[] { 12_345UL, 424_242UL, 999UL }
            .Select(seed => WorldGenerator.Generate(seed, config).WorldHash)
            .ToArray();

        Assert.Equal(
            new ulong[]
            {
                7_090_205_665_110_659_518UL,
                5_276_140_206_464_966_917UL,
                14_547_522_915_312_224_719UL
            },
            hashes);
        Assert.NotEqual(
            hashes[0],
            WorldGenerator.Generate(12_345, config with { ContentHash = "different-content-v0" }).WorldHash);
    }

    [Fact]
    public void SpawnSitesRequireReachableResourcesAndAreSelectedDeterministically()
    {
        var first = CreateFlatWorld(9, 9, 20);
        var second = CreateFlatWorld(9, 9, 20);
        foreach (var world in new[] { first, second })
        {
            world.SetSurfaceColumn(3, 3, new SurfaceColumn(20, 3, 0, 0));
            world.SetSurfaceColumn(2, 4, new SurfaceColumn(20, 4, 0, 0));
            world.SetBlockOverride(new BlockCoordinate(4, 3, 21), new BlockOverride(99, 0, false));
            world.SetBlockOverride(new BlockCoordinate(4, 5, 21), new BlockOverride(5, 0, false));
            world.SetBlockOverride(new BlockCoordinate(5, 4, 12), new BlockOverride(6, 0, false));
            world.SetBlockOverride(new BlockCoordinate(6, 4, 20), new BlockOverride(7, 0, false));
        }

        var zones = Enumerable.Repeat(WorldZone.Plains, 9 * 9).ToArray();
        var materials = new WorldResourceMaterials(
            Water: 99,
            Wood: new HashSet<ushort> { 5 },
            Stone: 3,
            Coal: 6,
            IronOre: 7,
            Farmland: 4,
            SolidMaterials: new HashSet<ushort> { 1, 3, 4, 5, 6, 7 });
        var config = new SpawnSiteConfig(
            Count: 1, MinimumSpacing: 4, MaximumResourceDistance: 12, MaximumCandidates: 81);

        var firstResult = SpawnSiteSelector.Select(first, zones, materials, 77, config);
        var secondResult = SpawnSiteSelector.Select(second, zones, materials, 77, config);
        var missing = SpawnSiteSelector.Select(
            CreateFlatWorld(9, 9, 20), zones, materials, 77, config);

        Assert.True(firstResult.Succeeded, firstResult.Error);
        Assert.Equal(firstResult.Sites, secondResult.Sites);
        Assert.Single(firstResult.Sites);
        Assert.Equal(21, firstResult.Sites[0].Z);
        Assert.False(missing.Succeeded);
        Assert.Empty(missing.Sites);
    }

    public sealed class EventWheelTests
    {
        [Fact]
        public void EventsAreReturnedByDueTickThenInsertionOrderAcrossWheelRotations()
        {
            var wheel = new EventWheel(bucketCount: 4);
            var first = wheel.Schedule(5, "growth", new BlockCoordinate(1, 2, 3));
            var second = wheel.Schedule(1, "craft", new BlockCoordinate(4, 5, 6));
            var third = wheel.Schedule(5, "spoil", new BlockCoordinate(7, 8, 9));

            Assert.Equal([second, first, third], wheel.AdvanceTo(5));
            Assert.Equal(0, wheel.Count);
            Assert.Equal(5, wheel.CurrentTick);
        }

        [Fact]
        public void CancellingEventDoesNotRemoveOtherEventsInSameBucket()
        {
            var wheel = new EventWheel(bucketCount: 2);
            var cancelled = wheel.Schedule(3, "growth", new BlockCoordinate(0, 0, 1));
            var remaining = wheel.Schedule(5, "spoil", new BlockCoordinate(0, 0, 2));

            Assert.True(wheel.Cancel(cancelled.Sequence));
            Assert.False(wheel.Cancel(cancelled.Sequence));
            Assert.Equal([remaining], wheel.AdvanceTo(5));
        }

        [Fact]
        public void AdvancingDoesNotConsumeEventsDueInTheFuture()
        {
            var wheel = new EventWheel(bucketCount: 2);
            var eventDueNow = wheel.Schedule(0, "now", new BlockCoordinate(0, 0, 0));
            var eventDueLater = wheel.Schedule(4, "later", new BlockCoordinate(0, 0, 1));

            Assert.Equal([eventDueNow], wheel.AdvanceTo(1));
            Assert.Equal(1, wheel.Count);
            Assert.Equal([eventDueLater], wheel.AdvanceTo(4));
        }

        [Fact]
        public void WheelRejectsPastSchedulingAndBackwardAdvancement()
        {
            var wheel = new EventWheel(initialTick: 3);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                wheel.Schedule(2, "past", new BlockCoordinate(0, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.AdvanceTo(2));
            Assert.Throws<ArgumentException>(() =>
                wheel.Schedule(3, "", new BlockCoordinate(0, 0, 0)));
        }
    }

    public sealed class MatterLedgerTests
    {
        [Fact]
        public void AuditBalancesInitialCreatedAndDestroyedQuantities()
        {
            var ledger = new MatterLedger();
            ledger.SetInitialCount(1, 10);
            ledger.RecordCreated(1, 5, "mine");
            ledger.RecordDestroyed(1, 3, "consume");

            Assert.Empty(ledger.Audit(new Dictionary<ushort, long> { [1] = 12 }));
        }

        [Fact]
        public void AuditReportsDeterministicallyOrderedMismatches()
        {
            var ledger = new MatterLedger();
            ledger.SetInitialCount(2, 3);
            ledger.SetInitialCount(1, 5);
            ledger.RecordCreated(2, 2, "generator");

            Assert.Equal(
                [
                    new MatterAuditViolation(1, 5, 4),
                    new MatterAuditViolation(2, 5, 6),
                ],
                ledger.Audit(new Dictionary<ushort, long> { [1] = 4, [2] = 6 }));
        }

        [Fact]
        public void LedgerEntriesAreOrderedByItemAndRule()
        {
            var ledger = new MatterLedger();
            ledger.RecordDestroyed(2, 1, "consume");
            ledger.RecordCreated(1, 2, "z-rule");
            ledger.RecordCreated(1, 1, "a-rule");

            Assert.Equal(
            [
                new MatterLedgerEntry(1, "a-rule", 1, 0),
                new MatterLedgerEntry(1, "z-rule", 2, 0),
                new MatterLedgerEntry(2, "consume", 0, 1),
            ],
                ledger.EnumerateEntries());
        }

        [Fact]
        public void LedgerRejectsInvalidQuantitiesAndRuleIds()
        {
            var ledger = new MatterLedger();

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.RecordCreated(1, 0, "mine"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.SetInitialCount(1, -1));
            Assert.Throws<ArgumentException>(() => ledger.RecordDestroyed(1, 1, ""));
        }
    }

    [Fact]
    public void GridMapsCoordinatesToCanonicalRegionsAndStoresSurfaceColumns()
    {
        var world = new WorldGrid(width: 19, height: 18, regionSize: 16);
        var column = new SurfaceColumn(12, 31, 2, 4);

        Assert.Equal(4, world.RegionCount);
        Assert.True(world.TryGetRegionId(18, 17, out var regionId));
        Assert.Equal(3, regionId);
        Assert.Equal(default, world.GetSurfaceColumn(18, 17));
        world.SetSurfaceColumn(18, 17, column);

        Assert.Equal(column, world.GetSurfaceColumn(18, 17));
        Assert.True(world.IsRegionDirty(regionId));
        world.ClearRegionDirty(regionId);
        Assert.False(world.IsRegionDirty(regionId));
    }

    [Fact]
    public void UniformRegionsMaterializeOnlyWhenAColumnChanges()
    {
        var world = new WorldGrid(width: 16, height: 16);
        var column = new SurfaceColumn(3, 5, 1, 2);

        world.SetSurfaceColumn(0, 0, default);
        Assert.False(world.IsRegionDirty(0));
        Assert.Equal(default, world.GetSurfaceColumn(15, 15));

        world.SetSurfaceColumn(0, 0, column);
        Assert.Equal(column, world.GetSurfaceColumn(0, 0));
        Assert.Equal(default, world.GetSurfaceColumn(15, 15));
    }

    [Fact]
    public void BlockOverridesAreSparseDirtyAndEnumeratedInCanonicalOrder()
    {
        var world = new WorldGrid(width: 32, height: 16);
        var later = new BlockCoordinate(3, 0, 4);
        var earlier = new BlockCoordinate(9, 1, 2);
        var block = new BlockOverride(7, 1, IsAir: false);

        world.SetBlockOverride(later, block);
        world.SetBlockOverride(earlier, block);

        Assert.Equal(2, world.OverrideCount);
        Assert.Equal(2, world.GetRegionOverrideCount(0));
        Assert.True(world.TryGetBlockOverride(earlier, out var stored));
        Assert.Equal(block, stored);
        Assert.Equal([earlier, later], world.EnumerateOverrides().Select(entry => entry.Key));
        Assert.True(world.IsRegionDirty(0));
    }

    [Fact]
    public void RemovingNonexistentOverrideDoesNotDirtyRegion()
    {
        var world = new WorldGrid(width: 16, height: 16);

        Assert.False(world.RemoveBlockOverride(new BlockCoordinate(4, 5, 6)));
        Assert.False(world.IsRegionDirty(0));
        Assert.Equal(0, world.GetRegionOverrideCount(0));
    }

    [Fact]
    public void GridRejectsOutOfBoundsWritesAndInvalidRegionIds()
    {
        var world = new WorldGrid(width: 16, height: 16);

        Assert.False(world.TryGetSurfaceColumn(-1, 0, out _));
        Assert.False(world.TryGetRegionId(16, 0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetSurfaceColumn(16, 0, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetSurfaceColumn(0, 0, new SurfaceColumn(64, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.IsRegionDirty(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetBlockOverride(
            new BlockCoordinate(0, 0, 64), new BlockOverride(1, 0, false)));
    }

    [Fact]
    public void WorldHashesAreIndependentOfOverrideInsertionOrder()
    {
        var first = CreateWorldWithOverrides([(8, 3, 5), (1, 2, 4), (7, 9, 1)]);
        var second = CreateWorldWithOverrides([(7, 9, 1), (8, 3, 5), (1, 2, 4)]);

        Assert.Equal(WorldGridHasher.ComputeWorldHash(first), WorldGridHasher.ComputeWorldHash(second));
        Assert.Equal(WorldGridHasher.ComputeRegionHash(first, 0), WorldGridHasher.ComputeRegionHash(second, 0));
    }

    [Fact]
    public void GeneratedGridWritesRoundTripAndHashIndependentlyOfInsertionOrder()
    {
        const int width = 37;
        const int height = 23;
        var random = new Xoshiro256StarStar(0xD37E_028UL);
        var columns = new (int X, int Y, SurfaceColumn Value)[500];
        var overrides = new (BlockCoordinate Coordinate, BlockOverride Value)[500];
        for (var index = 0; index < columns.Length; index++)
        {
            var x = index % width;
            var y = index / width;
            columns[index] = (x, y, new SurfaceColumn(
                (byte)random.NextInt(0, 64),
                (ushort)random.NextInt(0, ushort.MaxValue + 1),
                (byte)random.NextInt(0, byte.MaxValue + 1),
                (ushort)random.NextInt(0, ushort.MaxValue + 1)));
            overrides[index] = (
                new BlockCoordinate(x, y, random.NextInt(0, 64)),
                new BlockOverride(
                    (ushort)random.NextInt(0, ushort.MaxValue + 1),
                    (byte)random.NextInt(0, byte.MaxValue + 1),
                    random.NextInt(0, 2) == 1));
        }

        var first = new WorldGrid(width, height, regionSize: 8);
        var second = new WorldGrid(width, height, regionSize: 8);
        foreach (var column in columns)
        {
            first.SetSurfaceColumn(column.X, column.Y, column.Value);
        }

        foreach (var column in columns.Reverse())
        {
            second.SetSurfaceColumn(column.X, column.Y, column.Value);
        }

        foreach (var block in overrides)
        {
            first.SetBlockOverride(block.Coordinate, block.Value);
        }

        foreach (var block in overrides.Reverse())
        {
            second.SetBlockOverride(block.Coordinate, block.Value);
        }

        Assert.Equal(WorldGridHasher.ComputeWorldHash(first), WorldGridHasher.ComputeWorldHash(second));
        Assert.Equal(first.EnumerateOverrides(), second.EnumerateOverrides());
        foreach (var column in columns)
        {
            Assert.Equal(column.Value, first.GetSurfaceColumn(column.X, column.Y));
            Assert.Equal(column.Value, second.GetSurfaceColumn(column.X, column.Y));
        }

        foreach (var block in overrides)
        {
            Assert.True(first.TryGetBlockOverride(block.Coordinate, out var firstValue));
            Assert.True(second.TryGetBlockOverride(block.Coordinate, out var secondValue));
            Assert.Equal(block.Value, firstValue);
            Assert.Equal(block.Value, secondValue);
        }
    }

    [Fact]
    public void WorldHashChangesWhenAColumnOrOverrideChanges()
    {
        var world = new WorldGrid(16, 16);
        var initialHash = WorldGridHasher.ComputeWorldHash(world);

        world.SetSurfaceColumn(3, 4, new SurfaceColumn(2, 1, 0, 5));
        var changedColumnHash = WorldGridHasher.ComputeWorldHash(world);
        world.SetBlockOverride(new BlockCoordinate(3, 4, 1), new BlockOverride(9, 2, IsAir: false));

        Assert.NotEqual(initialHash, changedColumnHash);
        Assert.NotEqual(changedColumnHash, WorldGridHasher.ComputeWorldHash(world));
    }

    [Fact]
    public void RegionHashOnlyIncludesOverridesFromThatRegion()
    {
        var world = new WorldGrid(32, 16);
        var initialHash = WorldGridHasher.ComputeRegionHash(world, 0);

        world.SetBlockOverride(new BlockCoordinate(17, 0, 2), new BlockOverride(3, 0, IsAir: false));

        Assert.Equal(initialHash, WorldGridHasher.ComputeRegionHash(world, 0));
        Assert.NotEqual(initialHash, WorldGridHasher.ComputeRegionHash(world, 1));
    }

    private static WorldGrid CreateWorldWithOverrides((int X, int Y, int Z)[] coordinates)
    {
        var world = new WorldGrid(16, 16);
        foreach (var (x, y, z) in coordinates)
        {
            world.SetBlockOverride(new BlockCoordinate(x, y, z), new BlockOverride(2, 1, IsAir: false));
        }

        return world;
    }

    private static WorldGrid CreateFlatWorld(int width, int height, byte surfaceZ)
    {
        var world = new WorldGrid(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(surfaceZ, 1, 0, 0));
            }
        }

        return world;
    }
}
