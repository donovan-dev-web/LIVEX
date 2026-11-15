using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Content.Tests;

/// <summary>
/// Acceptance of the A1 content/generation decision (gate G4): the minimal pack must generate a
/// coast where the grass reaches the waterline — no sand row between them — so the irrigation
/// radius declared by the pack is satisfied by the terrain itself, and a wheat crop sown on that
/// coast really ripens and yields wheat. This is the terrain precondition the agriculture chain
/// was missing; before it, no tillable column of the generated world had water in reach and every
/// <c>Cultiver</c> ended in <c>crop.water_required</c>.
/// </summary>
public sealed class ShorelineAgricultureAcceptanceTests
{
    private const ulong AgentId = 1;
    private const ulong RunSeed = 20261108;

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void WheatRipensOnTheGeneratedCoastWhereGrassTouchesTheWater(ulong seed)
    {
        var pack = CompileMinimal();
        var grassId = pack.Registries.Materials.GetId("grass");
        var waterId = pack.Registries.Materials.GetId("water");
        var farmlandId = pack.Registries.Materials.GetId("farmland");
        var hoeId = pack.Registries.Items.GetId("stone_hoe");
        var seedId = pack.Registries.Items.GetId("wheat_seed");
        var wheatId = pack.Registries.Items.GetId("wheat");
        var world = Generate(seed, pack);

        var shore = FindShoreParcel(world, grassId, waterId);
        Assert.True(
            shore is not null,
            $"Seed {seed}: no grass column of the generated world touches a water block at its own height.");
        var (soil, standing) = shore!.Value;

        var cropRules = CropActionRuleRuntimeCompiler.Compile(pack);
        var growthRule = Assert.Single(cropRules.CropRules).Growth;

        // The pack keeps the real irrigation radius: the terrain has to satisfy it, not the other
        // way round (a world-wide radius would make the coast irrelevant).
        Assert.Equal(4, growthRule.WaterRadius);

        var lifecycle = new AgentLifecycleSystem(
            world,
            MovementRuleRuntimeCompiler.Compile(pack),
            PerceptionRuleRuntimeCompiler.Compile(pack));
        var agent = lifecycle.Incarnate(
            AgentId,
            AgentPosition.AtCell(standing.X, standing.Y, standing.Z),
            new AgentBodyProfile(CarrySlots: 8));
        var ledger = new MatterLedger();

        Assert.True(agent.Inventory.TryAdd(hoeId, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(seedId, 1, 64, out remaining));
        Assert.Equal(0, remaining);

        var tilled = CropActionSystem.Till(world, agent, cropRules, ledger, soil);
        Assert.True(tilled.Succeeded, $"Seed {seed}: till refused with '{tilled.ErrorCode}'.");
        Assert.True(world.TryGetBlockOverride(soil, out var farmland));
        Assert.Equal(farmlandId, farmland.MaterialId);

        var wheel = new EventWheel();
        var crop = new BlockCoordinate(soil.X, soil.Y, soil.Z + 1);
        var planted = CropActionSystem.Plant(
            world,
            agent,
            wheel,
            cropRules,
            ledger,
            RunSeed,
            tick: 0,
            crop,
            seedId,
            WorldSeason.Spring);
        Assert.True(planted.Succeeded, $"Seed {seed}: plant refused with '{planted.ErrorCode}'.");
        Assert.Equal(0, agent.Inventory.Count(seedId));

        var ripeTick = -1L;
        for (var tick = 1L; tick <= 2_000; tick++)
        {
            var dueEvents = wheel.AdvanceTo(tick);
            var growthEvents = CropGrowthSystem.ProcessDueEvents(
                world,
                wheel,
                RunSeed,
                tick,
                WorldSeason.Spring,
                growthRule,
                dueEvents);
            if (growthEvents.Any(static entry => entry.Kind == CropGrowthEventKind.Ripe))
            {
                ripeTick = tick;
                break;
            }
        }

        Assert.True(ripeTick > 0, $"Seed {seed}: the crop sown at {crop} never ripened.");
        Assert.True(world.TryGetBlockOverride(crop, out var ripe));
        Assert.Equal((byte)(growthRule.Stages - 1), ripe.State);

        var harvested = CropActionSystem.Harvest(
            world, agent, cropRules, ledger, RunSeed, ripeTick, crop);
        Assert.True(harvested.Succeeded, $"Seed {seed}: harvest refused with '{harvested.ErrorCode}'.");
        var wheat = harvested.Produced
            .Where(stack => stack.ItemTypeId == wheatId)
            .Sum(stack => stack.Quantity);
        Assert.InRange(wheat, 2, 3);
        Assert.Equal(wheat, agent.Inventory.Count(wheatId));
    }

    /// <summary>
    /// First grass column (deterministic scan) that has a water block at its own height in a
    /// four-neighbour column, i.e. the two blocks share a face; returns the soil block and the
    /// standing cell next to it, at the same surface height.
    /// </summary>
    private static (BlockCoordinate Soil, BlockCoordinate Standing)? FindShoreParcel(
        WorldGrid world,
        ushort grassId,
        ushort waterId)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.GetSurfaceColumn(x, y);
                if (column.MaterialId != grassId ||
                    !HasWaterAtHeight(world, x, y, column.SurfaceZ, waterId))
                {
                    continue;
                }

                foreach (var (dx, dy) in NeighbourOffsets)
                {
                    var standingX = x + dx;
                    var standingY = y + dy;
                    if (!world.TryGetSurfaceColumn(standingX, standingY, out var neighbour) ||
                        neighbour.SurfaceZ != column.SurfaceZ)
                    {
                        continue;
                    }

                    var standing = new BlockCoordinate(standingX, standingY, column.SurfaceZ + 1);
                    if (world.TryGetBlockOverride(standing, out var occupant) && !occupant.IsAir)
                    {
                        continue;
                    }

                    return (new BlockCoordinate(x, y, column.SurfaceZ), standing);
                }
            }
        }

        return null;
    }

    private static bool HasWaterAtHeight(WorldGrid world, int x, int y, int z, ushort waterId)
    {
        foreach (var (dx, dy) in NeighbourOffsets)
        {
            if (world.TryGetBlockOverride(new BlockCoordinate(x + dx, y + dy, z), out var block) &&
                !block.IsAir &&
                block.MaterialId == waterId)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly (int Dx, int Dy)[] NeighbourOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

    private static WorldGrid Generate(ulong seed, CompiledContentPack pack)
    {
        var config = WorldGenerationConfigCompiler.Compile(pack);
        return WorldGenerator.Generate(seed, config).Grid;
    }

    private static CompiledContentPack CompileMinimal()
    {
        var compilation = new ContentPackCompiler().CompileDirectory(
            Path.Combine(FindPrismRoot(), "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        return compilation.Pack!;
    }

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the content test output directory.");
    }
}
