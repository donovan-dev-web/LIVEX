using System.Text.Json;
using System.Text.Json.Nodes;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Host.Persistence;

namespace Prism.Host.Tests;

/// <summary>
/// Host-level half of PRISM-094: a mined block can be placed as a workbench, and the deferred
/// <c>Fabriquer</c> command then produces the stone pickaxe at that station.
/// </summary>
public sealed class ToolChainHostTests
{
    [Fact]
    public void PlacingAWorkbenchUnlocksTheStonePickaxeThroughDeferredCrafting()
    {
        var sourceRoot = Path.Combine(FindPrismRoot(), "content", "minimal");
        var directory = Path.Combine(Path.GetTempPath(), $"prism-tool-chain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var contentRoot = Path.Combine(directory, "content");
            CopyContentDirectory(sourceRoot, contentRoot);
            ConfigureToolChainContent(contentRoot);
            var compilation = new ContentPackCompiler().CompileDirectory(contentRoot);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
            var pack = compilation.Pack!;
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            runtime.Prepare(new PrepareWorldRequest(
                123,
                pack.ContentHash,
                contentRoot));

            var mineableMaterials = pack.Materials
                .Where(static material => material.Solid && material.Perceivable)
                .Select(material => pack.Registries.Materials.GetId(material.Id))
                .ToHashSet();
            var (origin, target) = FindClearMineableSurfaceAndAdjacentTarget(
                runtime.WorldSnapshot!,
                mineableMaterials);
            runtime.Start(new StartWorldRequest());

            var tick = 0L;
            CommandEnvelope Command(string commandId, string kind, JsonElement payload) =>
                new(ProtocolVersion.Current.ToString(), tick, commandId, 42, kind, payload.Clone());
            TickResult Step(params CommandEnvelope[] commands) =>
                runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick++, commands));

            using var incarnatePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { position = new { x = origin.X, y = origin.Y, z = origin.Z } },
                ProtocolJson.Options));
            Assert.Equal("action_completed", Assert.Single(Step(Command(
                "incarnate-tool-chain",
                "Incarner",
                incarnatePayload.RootElement)).Events).Kind);

            EventEnvelope AdvanceUntil(Func<EventEnvelope, bool> predicate, int maxTicks = 200)
            {
                for (var attempt = 0; attempt < maxTicks; attempt++)
                {
                    var advanced = Step();
                    if (advanced.Events.FirstOrDefault(predicate) is { } match)
                    {
                        return match;
                    }
                }

                throw new InvalidOperationException("The expected event never arrived.");
            }

            using var minePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { target },
                ProtocolJson.Options));
            Step(Command("mine-tool-stone", "Miner", minePayload.RootElement));
            var mined = AdvanceUntil(static entry =>
                entry.Kind == "action_completed" &&
                entry.Payload.GetProperty("commandId").GetString() == "mine-tool-stone");
            Assert.Equal(42UL, mined.AgentId);

            var workbench = FindAdjacentClearCell(runtime.WorldSnapshot!, origin);
            using var placePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { target = workbench, item = "workbench", material = "workbench" },
                ProtocolJson.Options));
            var placed = Step(Command("place-tool-workbench", "Poser", placePayload.RootElement));
            Assert.Equal("action_completed", Assert.Single(placed.Events).Kind);

            using var craftPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { recipe = "craft_stone_pickaxe", station = workbench },
                ProtocolJson.Options));
            var started = Step(Command("craft-tool-pickaxe", "Fabriquer", craftPayload.RootElement));
            Assert.Equal("action_started", Assert.Single(started.Events).Kind);

            var crafted = AdvanceUntil(static entry =>
                entry.Kind == "action_completed" &&
                entry.Payload.GetProperty("commandId").GetString() == "craft-tool-pickaxe");
            var produced = crafted.Payload
                .GetProperty("result")
                .GetProperty("produced");
            var stack = Assert.Single(produced.EnumerateArray());
            Assert.Equal(
                pack.Registries.Items.GetId("stone_pickaxe"),
                stack.GetProperty("itemTypeId").GetUInt16());
            Assert.Equal(1, stack.GetProperty("quantity").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ConfigureToolChainContent(string root)
    {
        var materialsPath = Path.Combine(root, "materials", "base.json");
        var materials = JsonNode.Parse(File.ReadAllText(materialsPath))!.AsArray();
        foreach (var material in materials.Select(static material => material!.AsObject()))
        {
            if (material["solid"]?.GetValue<bool>() != true ||
                material["perceivable"]?.GetValue<bool>() != true ||
                material["id"]!.GetValue<string>() == "iron_ore")
            {
                continue;
            }

            material["drops"] = new JsonArray(
                new JsonObject
                {
                    ["item"] = "handle",
                    ["qty"] = 1,
                },
                new JsonObject
                {
                    ["item"] = "stone_head",
                    ["qty"] = 1,
                },
                new JsonObject
                {
                    ["item"] = "workbench",
                    ["qty"] = 1,
                });
        }

        File.WriteAllText(materialsPath, JsonSerializer.Serialize(materials, ProtocolJson.Options));

        var parametersPath = Path.Combine(root, "params.json");
        var parameters = JsonNode.Parse(File.ReadAllText(parametersPath))!.AsObject();
        if (parameters["mining"] is JsonObject mining)
        {
            mining["baseTicksPerHardness"] = 1;
        }
        else
        {
            parameters["mining"] = new JsonObject { ["baseTicksPerHardness"] = 1 };
        }

        File.WriteAllText(parametersPath, JsonSerializer.Serialize(parameters, ProtocolJson.Options));
    }

    private static (BlockCoordinate Origin, BlockCoordinate Target) FindClearMineableSurfaceAndAdjacentTarget(
        GeneratedWorldSnapshot world,
        IReadOnlySet<ushort> mineableMaterials)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[x + world.Width * y];
                var origin = new BlockCoordinate(x, y, column.SurfaceZ + 1);
                if (!mineableMaterials.Contains(column.MaterialId) ||
                    origin.Z >= world.VerticalSize ||
                    overrides.Contains(origin))
                {
                    continue;
                }

                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var targetX = x + dx;
                    var targetY = y + dy;
                    if ((uint)targetX >= (uint)world.Width ||
                        (uint)targetY >= (uint)world.Height)
                    {
                        continue;
                    }

                    var targetColumn = world.SurfaceColumns[targetX + world.Width * targetY];
                    var target = new BlockCoordinate(targetX, targetY, targetColumn.SurfaceZ);
                    if (targetColumn.SurfaceZ == origin.Z &&
                        mineableMaterials.Contains(targetColumn.MaterialId) &&
                        !overrides.Contains(target))
                    {
                        return (origin, target);
                    }
                }
            }
        }

        throw new InvalidOperationException("The generated test world has no clear surface with an adjacent mineable block.");
    }

    private static BlockCoordinate FindAdjacentClearCell(
        GeneratedWorldSnapshot world,
        BlockCoordinate origin)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if ((uint)x >= (uint)world.Width ||
                (uint)y >= (uint)world.Height)
            {
                continue;
            }

            var surface = world.SurfaceColumns[x + world.Width * y].SurfaceZ;
            var target = new BlockCoordinate(x, y, surface + 1);
            if (target.Z == origin.Z && !overrides.Contains(target))
            {
                return target;
            }
        }

        throw new InvalidOperationException("The generated test world has no adjacent clear cell.");
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

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the host test output directory.");
    }

    private static void CopyContentDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile);
        }
    }
}
