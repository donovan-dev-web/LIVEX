using Prism.Content;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots.Tests;

public sealed class BotRunnerTests
{
    private static readonly BotContentMap Content = LoadContent();

    [Fact]
    public void RandomBotIsDeterministicForTheSameSeed()
    {
        var first = RunDecisions(new RandomBot(7, 42, Content), ticks: 40);
        var second = RunDecisions(new RandomBot(7, 42, Content), ticks: 40);

        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void RandomBotDiffersAcrossSeeds()
    {
        var first = RunDecisions(new RandomBot(7, 42, Content), ticks: 40);
        var second = RunDecisions(new RandomBot(99, 42, Content), ticks: 40);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GreedyBotWalksToTheNearestBlockThenMinesIt()
    {
        var host = new FakeWorldHost(new BotCoordinate(6, 0, 3), Content.MaterialId("stone"));
        var runner = new BotRunner(host, new Dictionary<ulong, IPrismBot>
        {
            [1] = new GreedyBot(Content),
        });

        runner.Run(0, [new BotSpawn(1, new BotCoordinate(0, 0, 3))], ticks: 4);

        Assert.Equal("Incarner", host.SentCommands[0].Kind);
        Assert.Contains(host.SentCommands, command => command.Kind == "Aller");
        Assert.Contains(host.SentCommands, command => command.Kind == "Miner");
        // The bot walks above the block (feet on top of it) before mining it.
        Assert.Equal(new BotCoordinate(6, 0, 4), new BotCoordinate(
            host.PositionOf(1).CellX,
            host.PositionOf(1).CellY,
            host.PositionOf(1).FeetZ));
    }

    [Fact]
    public void PerceptionMemoryAppliesDifferentialFrames()
    {
        var memory = new BotPerceptionMemory();
        var block = new PerceptionEntry(PerceptionEntityKind.Block, 1, 2, 3, 0, 5, 0, 0, 0, 0);
        memory.Merge(1, [new PerceptionChange(PerceptionChangeKind.Appeared, block)]);
        Assert.Single(memory.Entries);

        var changed = block with { MaterialId = 6 };
        memory.Merge(2, [new PerceptionChange(PerceptionChangeKind.Changed, changed)]);
        Assert.Equal((ushort)6, Assert.Single(memory.Entries).MaterialId);

        memory.Merge(3, [new PerceptionChange(PerceptionChangeKind.Disappeared, changed)]);
        Assert.Empty(memory.Entries);
        Assert.Equal(3, memory.LastMergedTick);
    }

    [Fact]
    public void PlannerExpandsTheRecipeGraphIntoObtainAndCraftSteps()
    {
        var planner = new PlannerBot(Content, [("plank", 4)]);

        // make_plank: 1 log -> 4 plank, so a 4-plank goal is one obtain then one craft.
        Assert.Collection(
            planner.Plan,
            step => Assert.Equal(Content.ItemId("log"), Assert.IsType<ObtainItemStep>(step).ItemId),
            step =>
            {
                var craft = Assert.IsType<CraftItemStep>(step);
                Assert.Equal(Content.RecipeId("make_plank"), craft.RecipeId);
                Assert.Equal(Content.ItemId("plank"), craft.OutputItemId);
                Assert.Equal(4, craft.OutputQuantity);
            });
    }

    [Fact]
    public void PlannerCraftsOnceForTheWholeOutputRange()
    {
        var planner = new PlannerBot(Content, [("plank", 8)]);

        // 8 planks need two runs of make_plank, hence two logs.
        Assert.Collection(
            planner.Plan,
            step => Assert.Equal(2, Assert.IsType<ObtainItemStep>(step).Quantity),
            step => Assert.Equal(8, Assert.IsType<CraftItemStep>(step).OutputQuantity));
    }

    [Fact]
    public void RunnerForwardsRefusalsToTheBotThatSentTheCommand()
    {
        var host = new FakeWorldHost(new BotCoordinate(2, 0, 3), Content.MaterialId("stone"))
        {
            FailCommandKind = "Miner",
            FailReason = "out_of_range",
        };
        var recorder = new RecordingBot();
        var runner = new BotRunner(host, new Dictionary<ulong, IPrismBot> { [5] = recorder });

        var result = runner.Run(0, [new BotSpawn(5, new BotCoordinate(0, 0, 3))], ticks: 4);

        Assert.True(
            result.FailureReasons["out_of_range"] >= 1,
            "The refusal must be counted in the run summary.");
        Assert.Contains(
            recorder.ObservedFailures,
            failure => failure.Kind == "Miner" && failure.Reason == "out_of_range");
    }

    [Fact]
    public void RunnerIsDeterministicForTheSameInputs()
    {
        var first = RunFake();
        var second = RunFake();

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.CommandsSent, second.CommandsSent);
        Assert.NotEqual(RunFingerprint.Empty, first.Fingerprint);
    }

    [Fact]
    public void FingerprintChangesWhenTheWorldAnswerChanges()
    {
        var first = RunFake();
        var second = RunFake(failReason: "another_reason");

        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    private static BotRunResult RunFake(string failReason = "refused")
    {
        var host = new FakeWorldHost(new BotCoordinate(3, 0, 3), Content.MaterialId("stone"))
        {
            FailCommandKind = "Miner",
            FailReason = failReason,
        };
        var runner = new BotRunner(host, new Dictionary<ulong, IPrismBot> { [1] = new GreedyBot(Content) });
        return runner.Run(0, [new BotSpawn(1, new BotCoordinate(0, 0, 3))], ticks: 4);
    }

    private static List<string> RunDecisions(IPrismBot bot, int ticks)
    {
        var commands = new List<string>();
        var senses = new BotSenses(
            0,
            AgentPosition.AtCell(4, 4, 3),
            Array.Empty<AgentItemStack>(),
            [
                new PerceptionEntry(PerceptionEntityKind.Block, 5, 4, 3, 0, 4, 0, 0, 0, 0),
                new PerceptionEntry(PerceptionEntityKind.Block, 4, 5, 3, 0, 5, 0, 0, 0, 0),
                new PerceptionEntry(PerceptionEntityKind.Block, 4, 4, 2, 0, 6, 0, 0, 0, 0),
            ],
            []);
        for (var tick = 0; tick < ticks; tick++)
        {
            foreach (var command in bot.Decide(senses with { Tick = tick }))
            {
                commands.Add($"{tick}:{command.Kind}:{command.Payload.GetRawText()}");
            }
        }

        return commands;
    }

    private static BotContentMap LoadContent()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        return BotContentMap.FromContent(compilation.Pack!);
    }

    internal static string FindPrismRoot()
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

        throw new InvalidOperationException("Prism.sln was not found above the test assembly.");
    }

    private sealed class RecordingBot : IPrismBot
    {
        private readonly GreedyBot _inner = new(Content);

        public List<BotCommandFailure> ObservedFailures { get; } = [];

        public string Kind => "recording";

        public IReadOnlyList<BotCommand> Decide(BotSenses senses)
        {
            ObservedFailures.AddRange(senses.Failures);
            return _inner.Decide(senses);
        }
    }
}
