using System.Text.Json;
using System.Text.Json.Nodes;
using Prism.Contracts;
using Prism.Content;
using Prism.Core.World;
using Prism.Host.Hosting;

namespace Prism.Host.Tests;

/// <summary>
/// Acceptance scenario for PRISM-096: a completed well grants <c>water_access</c> in its
/// volume, so an agent standing inside it can drink through the ordinary <c>Consommer</c>
/// command, even with no adjacent water block.
/// </summary>
public sealed class WellDrinkingAcceptanceTests
{
    [Fact]
    public void ACompletedWellMakesItsVolumeDrinkable()
    {
        var sourceRoot = Path.Combine(FindPrismRoot(), "content", "minimal");
        var directory = Path.Combine(Path.GetTempPath(), $"prism-well-drinking-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var contentRoot = Path.Combine(directory, "content");
            CopyContentDirectory(sourceRoot, contentRoot);
            ConfigureWellContent(contentRoot);
            var compilation = new ContentPackCompiler().CompileDirectory(contentRoot);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
            var pack = compilation.Pack!;

            var store = new Prism.Host.Persistence.WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            runtime.Prepare(new PrepareWorldRequest(123, pack.ContentHash, contentRoot));
            runtime.Start(new StartWorldRequest());

            var tick = 0L;
            CommandEnvelope Command(string commandId, string kind, JsonElement payload) =>
                new(ProtocolVersion.Current.ToString(), tick, commandId, 42, kind, payload.Clone());
            TickResult Step(params CommandEnvelope[] commands) =>
                runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick++, commands));

            var origin = FindFlatWellOrigin(runtime.WorldSnapshot!);
            var volumeCenter = new BlockCoordinate(origin.X, origin.Y, origin.Z);

            // Build the well through the host command flow.
            using var startPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { siteId = "well-1", templateId = "well", position = new { x = origin.X, y = origin.Y, z = origin.Z } },
                ProtocolJson.Options));
            var started = Step(Command("start-well", "DemarrerChantier", startPayload.RootElement));
            if (started.Events.FirstOrDefault()?.Kind != "action_completed")
            {
                throw new InvalidOperationException($"DemarrerChantier failed: {started.Events.FirstOrDefault()?.Payload}");
            }

            for (var i = 0; i < 100; i++)
            {
                using var contributePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                    new { siteId = "well-1" }, ProtocolJson.Options));
                Step(Command($"contribute-well-{i}", "Contribuer", contributePayload.RootElement));
            }

            Assert.Contains(runtime.WorldSnapshot!.ActiveStructureEffects,
                effect => effect.Type == "water_access");

            // Incarnate the agent inside the well volume.
            using var incarnatePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { position = new { x = volumeCenter.X, y = volumeCenter.Y, z = volumeCenter.Z } },
                ProtocolJson.Options));
            var incarnated = Step(Command("incarnate-well", "Incarner", incarnatePayload.RootElement));
            if (incarnated.Events.FirstOrDefault()?.Kind != "action_completed")
            {
                throw new InvalidOperationException($"Incarner failed: {incarnated.Events.FirstOrDefault()?.Payload}");
            }

            // Let perception run twice so the chosen target is in the agent's observed set.
            Step();
            Step();

            // Drinking falls back to the well volume because the target itself is not water.
            using var drinkPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { target = new { x = origin.X + 1, y = origin.Y, z = origin.Z } },
                ProtocolJson.Options));
            var drank = Step(Command("drink-well", "Consommer", drinkPayload.RootElement));
            var consumed = Assert.Single(drank.Events, e => e.Kind == "item_consumed");
            Assert.Equal("source", consumed.Payload.GetProperty("source").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BlockCoordinate FindFlatWellOrigin(GeneratedWorldSnapshot world)
    {
        var overrides = world.Overrides.Select(e => e.Key).ToHashSet();
        for (var y = 0; y + 3 <= world.Height; y++)
        {
            for (var x = 0; x + 3 <= world.Width; x++)
            {
                var z = world.SurfaceColumns[x + world.Width * y].SurfaceZ;
                var flat = true;
                for (var dy = 0; dy < 3 && flat; dy++)
                {
                    for (var dx = 0; dx < 3; dx++)
                    {
                        var column = world.SurfaceColumns[(x + dx) + world.Width * (y + dy)];
                        if (column.SurfaceZ != z ||
                            overrides.Contains(new BlockCoordinate(x + dx, y + dy, z + 1)))
                        {
                            flat = false;
                            break;
                        }
                    }
                }

                if (flat)
                {
                    return new BlockCoordinate(x, y, z + 1);
                }
            }
        }

        throw new InvalidOperationException("The generated test world has no flat 3x3 area for a well.");
    }

    private static void ConfigureWellContent(string root)
    {
        var structuresPath = Path.Combine(root, "templates", "structures.json");
        var json = JsonNode.Parse(File.ReadAllText(structuresPath))!.AsArray();
        foreach (var template in json.Select(static node => node!.AsObject()))
        {
            if (template["id"]?.GetValue<string>() != "well")
            {
                continue;
            }

            template["materials"] = new JsonArray();
        }

        File.WriteAllText(structuresPath, JsonSerializer.Serialize(json, ProtocolJson.Options));
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
}