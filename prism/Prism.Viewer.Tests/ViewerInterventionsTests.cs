using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerInterventionsTests
{
    [Fact]
    public void JournalIsAppendOnlyAndReloadsTheInterventions()
    {
        var directory = Directory.CreateTempSubdirectory("prism-viewer-interventions");
        var path = Path.Combine(directory.FullName, "interventions.jsonl");
        try
        {
            var first = new ViewerIntervention(
                1, 10, ViewerInterventionKind.Place, new BlockCoordinate(1, 2, 3), 1, 3);
            var second = new ViewerIntervention(
                2, 11, ViewerInterventionKind.Remove, new BlockCoordinate(4, 5, 6), 1, 3);

            ViewerInterventionJournal.Append(path, first);
            ViewerInterventionJournal.Append(path, second);

            var lines = File.ReadAllLines(path);
            Assert.Contains("\"interventions\":true", lines[0]);
            var reloaded = ViewerInterventionJournal.Read(path);
            Assert.Equal(2, reloaded.Count);
            Assert.Equal(first, reloaded[0]);
            Assert.Equal(second, reloaded[1]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void InterventionBecomesAContractCommandWithExistingKinds()
    {
        var place = new ViewerIntervention(
            7, 12, ViewerInterventionKind.Place, new BlockCoordinate(3, 4, 5), 2, 6);
        var remove = new ViewerIntervention(
            8, 13, ViewerInterventionKind.Remove, new BlockCoordinate(3, 4, 5), 2, 6);

        var placeCommand = ViewerInterventionJournal.CreateCommand(place, 13, agentId: 1);
        var removeCommand = ViewerInterventionJournal.CreateCommand(remove, 14, agentId: 1);

        Assert.Contains(placeCommand.Kind, CommandKinds.All);
        Assert.Contains(removeCommand.Kind, CommandKinds.All);
        Assert.Equal("Poser", placeCommand.Kind);
        Assert.Equal("Retirer", removeCommand.Kind);
        Assert.Equal(13, placeCommand.Tick);
        Assert.Equal(1UL, placeCommand.AgentId);
        Assert.True(CommandPayloadValidator.TryValidate(
            placeCommand.Kind,
            placeCommand.Payload,
            out var detail), detail);
        Assert.True(CommandPayloadValidator.TryValidate(
            removeCommand.Kind,
            removeCommand.Payload,
            out detail), detail);
        Assert.Equal(3, placeCommand.Payload.GetProperty("position").GetProperty("x").GetInt32());
    }

    [Fact]
    public void HostQueuesAnInterventionForTheNextTickOnly()
    {
        var request = new InterventionRequest(
            AgentId: 1,
            Kind: "Poser",
            X: 2,
            Y: 3,
            Z: 4,
            ItemTypeId: 1,
            MaterialId: 6);
        var command = request.ToCommand(ProtocolVersion.Current.ToString());

        Assert.Equal("Poser", command.Kind);
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out var detail), detail);
        Assert.Throws<ArgumentException>(() => (request with { Kind = "Miner" }).ToCommand("1.0.0"));

        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        using var factory = new HostFactory();
        var (runtimePrepared, contentRoot) = factory.Prepare(runtime);
        Assert.True(runtimePrepared);

        // Hors run, le host refuse une intervention : rien n'est appliqué en silence.
        Assert.Throws<HostLifecycleException>(() => runtime.QueueIntervention(command));
        runtime.Start(new StartWorldRequest());
        Assert.Equal(1, runtime.QueueIntervention(command));
        Assert.Equal(2, runtime.QueueIntervention(command));
    }

    private sealed class HostFactory : IDisposable
    {
        public (bool Prepared, string ContentRoot) Prepare(PrismHostRuntime runtime)
        {
            var contentRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "content", "minimal"));
            var compilation = new Prism.Content.ContentPackCompiler().CompileDirectory(contentRoot);
            if (!compilation.Succeeded)
            {
                return (false, contentRoot);
            }

            runtime.Prepare(new PrepareWorldRequest(1UL, compilation.Pack!.ContentHash, contentRoot));
            return (true, contentRoot);
        }

        public void Dispose()
        {
        }
    }
}
