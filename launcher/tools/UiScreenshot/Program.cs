using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Launcher.Application;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Launcher.App.Views;
using Launcher.Domain.Model;
using Launcher.Presentation.ViewModel;
using Launcher.Protocol.Model;

// Capture headless de la MainWindow (GUI.md) : dotnet run -- <sortie.png> [nav-id] [--dump]
var outputPath = args.Length > 0 ? args[0] : "screenshot.png";
var navId = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : MainWindowViewModel.NavAccueilId;
var dump = args.Any(argument => argument == "--dump");

AppBuilder.Configure<HarnessApplication>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// Capture de la fenêtre d'analyse native (ADR-007) : UiScreenshot <section> <sortie.png> --analysis
if (args.Any(argument => argument == "--analysis"))
{
    var section = navId is AnalysisWindowViewModel.SectionViability
        or AnalysisWindowViewModel.SectionStatistics
        or AnalysisWindowViewModel.SectionRelations
        or AnalysisWindowViewModel.SectionWorld
        or AnalysisWindowViewModel.SectionAgents
        or AnalysisWindowViewModel.SectionCompare
        or AnalysisWindowViewModel.SectionOverview
            ? navId
            : AnalysisWindowViewModel.SectionOverview;
    return CaptureAnalysis(outputPath, section);
}

// Test : fenêtre SANS aucun style d'application ( FluentTheme retiré puis remis ).
var styles = Application.Current!.Styles;
var fluent = styles[0];
styles.RemoveAt(0);
var unthemed = new Window { Width = 400, Height = 120, Content = new Button { Content = "U" } };
unthemed.Show();
Dispatcher.UIThread.RunJobs();
foreach (var child in unthemed.GetVisualDescendants().OfType<Button>())
{
    Console.WriteLine($"SANS aucun thème : Button '{child.Content}' ha={child.HorizontalAlignment} va={child.VerticalAlignment} w={child.Bounds.Width}");
}

unthemed.Close();
styles.Insert(0, fluent);

// Reproduction minimaliste de la carte : colonne « * » qui se dimensionnerait au contenu.
var card = new ProbeBorder
{
    Width = 278,
    BorderThickness = new Thickness(1.5),
    ClipToBounds = true,
};
var inner = new ProbeBorder { ClipToBounds = true };
var rows = new ProbeGrid
{
    RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
    ColumnDefinitions = new ColumnDefinitions("*"),
};
var header = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(14, 12, 12, 0) };
header.Children.Add(new Border { Width = 66, Height = 66 });
header.Children.Add(new TextBlock { Text = "Moteur de simulation multi-agents", TextWrapping = TextWrapping.Wrap });
Grid.SetRow(header, 0);
var description = new TextBlock
{
    Text = "Fait vivre des entités autonomes dans un monde déterministe, tick après tick.",
    TextWrapping = TextWrapping.Wrap,
    Margin = new Thickness(16, 8),
    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
};
Grid.SetRow(description, 1);
var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Margin = new Thickness(14, 0, 14, 12) };
buttons.Children.Add(new Button { Content = "Arrêter" });
var b2 = new Button { Content = "Configurer" };
Grid.SetColumn(b2, 2);
buttons.Children.Add(b2);
Grid.SetRow(buttons, 3);
rows.Children.Add(header);
rows.Children.Add(description);
rows.Children.Add(buttons);
inner.Child = rows;
card.Child = inner;
var repro = new Window { Width = 400, Height = 320, Content = card };
repro.Show();
Dispatcher.UIThread.RunJobs();
foreach (var child in rows.GetVisualDescendants().OfType<Control>().Where(control => control is StackPanel or TextBlock or Grid or Button).Take(8))
{
    Console.WriteLine($"repro {child.GetType().Name} x={child.Bounds.X:0.0} w={child.Bounds.Width:0.0} ha={child.HorizontalAlignment}");
}

Console.WriteLine($"repro card w={card.Bounds.Width:0.0} inner w={inner.Bounds.Width:0.0} rows w={rows.Bounds.Width:0.0} headerDesired={header.DesiredSize.Width:0.0} gridDesired={rows.DesiredSize.Width:0.0}");
repro.Close();

var bare = new Window
{
    Width = 400,
    Height = 120,
    Content = new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("*,*"),
        Children = { new Button { Content = "A" }, new Button { Content = "B" } },
    },
};
bare.Show();
Dispatcher.UIThread.RunJobs();
foreach (var child in bare.GetVisualDescendants().OfType<Button>())
{
    Console.WriteLine($"bare Button '{child.Content}' ha={child.HorizontalAlignment} va={child.VerticalAlignment} w={child.Bounds.Width}");
}

bare.Close();

var viewModel = new MainWindowViewModel(new StubFacade());
var window = new MainWindow { DataContext = viewModel };
window.Show();
viewModel.NavigateCommand.Execute(navId);
viewModel.RefreshFromRegistry();
viewModel.RefreshExperiences();
Dispatcher.UIThread.RunJobs();

using var frame = window.CaptureRenderedFrame();
if (frame is null)
{
    Console.Error.WriteLine("capture impossible : frame nulle");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
frame.Save(outputPath);
Console.WriteLine($"écrit : {outputPath}");

if (dump)
{
    DumpLayout(window, window, 0);
}

return 0;

/// <summary>Capture headless de la fenêtre d'analyse, alimentée par une source mémoire.</summary>
static int CaptureAnalysis(string path, string section)
{
    var viewModel = new AnalysisWindowViewModel(new StubTelemetry())
    {
        SelectedSectionId = section,
    };
    var window = new AnalysisWindow(viewModel);
    window.Show();
    Dispatcher.UIThread.RunJobs();
    viewModel.RefreshAsync().GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs();

    // Les séries LiveCharts s'animent au premier rendu : on laisse l'animation
    // se terminer avant la capture, sinon la courbe est saisie à mi-parcours.
    for (var step = 0; step < 8; step++)
    {
        Thread.Sleep(250);
        Dispatcher.UIThread.RunJobs();
    }

    using var frame = window.CaptureRenderedFrame();
    if (frame is null)
    {
        Console.Error.WriteLine("capture impossible : frame nulle");
        return 1;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    frame.Save(path);
    window.Close();
    Console.WriteLine($"écrit : {path}");
    return 0;
}

/// <summary>Imprime les bornes (repère fenêtre) des contrôles utiles au diagnostic de mise en page.</summary>
static void DumpLayout(Window window, Control control, int depth)
{
    var origin = control.TranslatePoint(default, window) ?? default;
    var classes = string.Join('.', control.Classes);
    var alignment = control.HorizontalAlignment != HorizontalAlignment.Stretch
        ? $" ha={control.HorizontalAlignment} va={control.VerticalAlignment}"
        : control.VerticalAlignment != VerticalAlignment.Stretch
            ? $" va={control.VerticalAlignment}"
            : string.Empty;
    var columns = control is Grid grid
        ? $" cols=[{string.Join(",", grid.ColumnDefinitions.Select(column => column.Width))}]"
        : string.Empty;
    var label = control switch
    {
        Button button => $" [{button.Content}] enabled={button.IsEnabled}",
        Avalonia.Svg.Svg svg => $" svg={svg.Path}",
        TextBlock text => $" '{text.Text}'",
        _ => string.Empty,
    };
    Console.WriteLine($"{new string(' ', depth * 2)}{control.GetType().Name} '{classes}' " +
                      $"x={origin.X:0.0} y={origin.Y:0.0} w={control.Bounds.Width:0.0} h={control.Bounds.Height:0.0}" +
                      $"{alignment}{columns}{label}");

    foreach (var child in control.GetVisualChildren().OfType<Control>())
    {
        DumpLayout(window, child, depth + 1);
    }
}

/// <summary>Observateur minimal pour tracer les changements de propriété stylée.</summary>
public sealed class TraceObserver<T>(Action<T> onNext) : IObserver<T>{
    /// <inheritdoc />
    public void OnNext(T value) => onNext(value);

    /// <inheritdoc />
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
    }
}

/// <summary>Border qui affiche la contrainte de mesure reçue.</summary>
public sealed class ProbeBorder : Border
{
    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Console.WriteLine($"probe Border '{GetType().Name}' available={availableSize}");
        return base.MeasureOverride(availableSize);
    }
}

/// <summary>Grid qui affiche la contrainte de mesure reçue.</summary>
public sealed class ProbeGrid : Grid
{
    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        Console.WriteLine($"probe Grid constraint={constraint}");
        return base.MeasureOverride(constraint);
    }
}

/// <summary>Source mémoire de capture : mêmes formes que l'API REST d'ECHOS.</summary>
public sealed class StubTelemetry : Launcher.Application.IEchosTelemetrySource
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Launcher.Application.EchosRunSummary>> ReadRunsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Launcher.Application.EchosRunSummary>>(
            [new Launcher.Application.EchosRunSummary("EXP-A-RUN-0001", 42, 20, 1, 20, "extinction", 20)]);

    /// <inheritdoc />
    public Task<Launcher.Application.EchosSeries> ReadSeriesAsync(string? runId, int every, CancellationToken cancellationToken)
    {
        var ticks = Enumerable.Range(1, 20).ToArray();
        var cluster = ticks.Select(tick => (double?)Math.Round(0.2 + (tick / 40d), 3)).ToArray();
        var modularity = ticks.Select(tick => (double?)Math.Round(0.8 - (tick / 50d), 3)).ToArray();
        var formation = ticks.Select(tick => (double?)(90 - (tick * 3))).ToArray();
        var diversity = ticks.Select(tick => (double?)Math.Round(0.5 + (Math.Sin(tick) / 6), 3)).ToArray();
        return Task.FromResult(new Launcher.Application.EchosSeries
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Ticks = ticks,
            Values = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["ClusteringCoefficient"] = cluster,
                    ["Modularity"] = modularity,
                    ["AveragePathLength"] = ticks.Select(tick => (double?)(2d + (tick / 10d))).ToArray(),
                },
                ["GroupDynamicsMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["GroupFormationRate"] = formation,
                },
                ["CognitiveDiversityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["PerspectiveCount"] = diversity,
                },
            },
            Latest = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, double>
                {
                    ["ClusteringCoefficient"] = 1.15,
                    ["Modularity"] = 0.4,
                    ["AveragePathLength"] = 4,
                },
                ["GroupDynamicsMetrics"] = new Dictionary<string, double> { ["GroupFormationRate"] = 31 },
                ["CognitiveDiversityMetrics"] = new Dictionary<string, double> { ["PerspectiveCount"] = 0.62 },
            },
            Measured = new Dictionary<string, IReadOnlyDictionary<string, bool>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, bool>
                {
                    ["ClusteringCoefficient"] = true,
                    ["Modularity"] = true,
                    ["AveragePathLength"] = false,
                },
                ["GroupDynamicsMetrics"] = new Dictionary<string, bool> { ["GroupFormationRate"] = true },
                ["CognitiveDiversityMetrics"] = new Dictionary<string, bool> { ["PerspectiveCount"] = true },
            },
            LatestTick = 20,
        });
    }

    /// <inheritdoc />
    public Task<Launcher.Application.EchosMetricCatalog> ReadCatalogAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosMetricCatalog
        {
            Version = "2.0.0",
            Metrics =
            [
                new Launcher.Application.EchosMetricDoc(
                    "ClusteringCoefficient",
                    "SocialComplexityMetrics",
                    "Coefficient de clustering",
                    "ratio",
                    "réseau",
                    "Part des liens observés qui referment un triplet.",
                    "triangles observés / triplets possibles",
                    "entités vivantes liées",
                    "fenêtre publiée",
                    "plus haut = plus dense",
                    "measured",
                    [],
                    "fenêtre absente = repli neutre",
                    "courbe",
                    []),
            ],
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosViability> ReadViabilityAsync(
        string runId, int every, CancellationToken cancellationToken)
    {
        var population = new List<Launcher.Application.EchosPoint>();
        var energy = new List<Launcher.Application.EchosPoint>();
        var hunger = new List<Launcher.Application.EchosPoint>();
        for (var tick = 1; tick <= 20; tick++)
        {
            population.Add(new Launcher.Application.EchosPoint(tick, Math.Max(0, 12 - (tick / 3))));
            energy.Add(new Launcher.Application.EchosPoint(tick, Math.Round(70 - (tick * 1.5), 1)));
            hunger.Add(new Launcher.Application.EchosPoint(tick, Math.Round(20 + (tick * 1.2), 1)));
        }

        return Task.FromResult(new Launcher.Application.EchosViability
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Outcome = "extinct",
            ExtinctionTick = 20,
            PopulationInitial = 12,
            PopulationFinal = 0,
            PopulationMinimum = 0,
            PopulationSeries = population,
            EnergySeries = energy,
            HungerSeries = hunger,
            FoodSeries =
            [
                new Launcher.Application.EchosPoint(1, 80),
                new Launcher.Application.EchosPoint(10, 35),
                new Launcher.Application.EchosPoint(20, 0),
            ],
            MissingTickCount = 4,
            ConservationLevel = "high_fidelity",
            CalibrationAvailable = true,
            EnergySlopePerTick = -1.5,
            HungryDecisions = 37,
            ExtinctionChronology =
            [
                new Launcher.Application.EchosEventRow(17, "death", "6", "collapse", "hunger=96"),
                new Launcher.Application.EchosEventRow(18, "death", "4", "collapse", "thirst=93"),
                new Launcher.Application.EchosEventRow(20, "death", "1", "collapse", "hunger=99"),
            ],
        });
    }

    /// <inheritdoc />
    public Task<Launcher.Application.EchosExperimentSummary> ReadExperimentSummaryAsync(
        IReadOnlyList<string> runIds, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosExperimentSummary
        {
            Runs = runIds.Select((id, index) => new Launcher.Application.EchosCompareRun(
                id,
                "0.1.0",
                (42 + index).ToString(System.Globalization.CultureInfo.InvariantCulture),
                index == 0 ? "extinct" : "surviving",
                20,
                1,
                20,
                index == 0 ? 20 : null,
                index == 0 ? "high_fidelity" : "base")).ToList(),
            Metrics =
            [
                new Launcher.Application.EchosCompareMetric(
                    "SocialComplexityMetrics",
                    "ClusteringCoefficient",
                    runIds.Distinct().Select((id, index) => new { id, value = index == 0 ? 0.41d : 0.53d })
                        .ToDictionary(item => item.id, item => (double?)item.value),
                    runIds.Count,
                    0.41,
                    0.53,
                    0.47,
                    0.12),
                new Launcher.Application.EchosCompareMetric(
                    "GroupDynamicsMetrics",
                    "GroupFormationRate",
                    runIds.Distinct().Select((id, index) => new { id, value = index == 0 ? 31d : 44d })
                        .ToDictionary(item => item.id, item => (double?)item.value),
                    runIds.Count,
                    31,
                    44,
                    37.5,
                    13),
            ],
            Note = "Dispersion publiée par ECHOS sur les runs sélectionnés ; aucune valeur n'est recalculée ici.",
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosNetwork> ReadNetworkAsync(string? runId, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosNetwork
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Tick = 20,
            Groups =
            [
                new Launcher.Application.EchosGroup("3", ["3", "7", "11", "15"], 4),
                new Launcher.Application.EchosGroup("8", ["1", "8", "12"], 3),
                new Launcher.Application.EchosGroup("21", ["2", "4", "5", "6", "9", "10"], 6),
                new Launcher.Application.EchosGroup("33", ["13", "14"], 2),
            ],
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosWorld> ReadWorldAsync(string? runId, int? tick, CancellationToken cancellationToken)
    {
        var agents = new List<Launcher.Application.EchosAgentSnapshot>
        {
            new("1", 40, 60, 62, 12, 18, "SeekFood", "3"),
            new("2", 120, 90, 48, 30, 22, "Explore", "3"),
            new("3", 200, 140, 71, 8, 9, "Rest", "3"),
            new("4", 320, 220, 35, 44, 40, "SeekWater", "8"),
            new("5", 410, 300, 55, 20, 16, "Explore", "8"),
            new("6", 160, 360, 12, 60, 58, "Flee", null),
        };
        return Task.FromResult(new Launcher.Application.EchosWorld
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Tick = tick ?? 20,
            WorldTick = 0,
            Description = new Launcher.Application.EchosWorldDescription(
                500, 500, 10,
                BuildCells(),
                [new Launcher.Application.EchosObstacle("obs-1", 250, 180, 24)],
                [new Launcher.Application.EchosWorldResource("res-1", "food", 90, 120, 80)],
                [new Launcher.Application.EchosRegion("spawn", 10, 10, 80, 60)]),
            Agents = agents,
            Groups =
            [
                new Launcher.Application.EchosGroup("3", ["1", "2", "3"], 3),
                new Launcher.Application.EchosGroup("8", ["4", "5"], 2),
            ],
            Resources = [new Launcher.Application.EchosTickResource("food", 90, 120, 42, 80)],
        });
    }

    private static List<Launcher.Application.EchosWorldCell> BuildCells()
    {
        var cells = new List<Launcher.Application.EchosWorldCell>();
        for (var x = 0; x < 25; x++)
        {
            for (var y = 0; y < 25; y++)
            {
                var walkable = ((x * 7) + (y * 3)) % 11 != 0;
                cells.Add(new Launcher.Application.EchosWorldCell(x, y, walkable ? "grass" : "rock", walkable));
            }
        }

        return cells;
    }

    /// <inheritdoc />
    public Task<Launcher.Application.EchosTrustGraph> ReadTrustGraphAsync(string? runId, int? tick, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosTrustGraph
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Tick = 20,
            Nodes =
            [
                new Launcher.Application.EchosTrustNode("1", 40, 60, 62, 12, 18, "SeekFood", "3"),
                new Launcher.Application.EchosTrustNode("2", 120, 90, 48, 30, 22, "Explore", "3"),
                new Launcher.Application.EchosTrustNode("3", 200, 140, 71, 8, 9, "Rest", "3"),
                new Launcher.Application.EchosTrustNode("4", 320, 220, 35, 44, 40, "SeekWater", "8"),
                new Launcher.Application.EchosTrustNode("5", 410, 300, 55, 20, 16, "Explore", "8"),
                new Launcher.Application.EchosTrustNode("6", 160, 360, 12, 60, 58, "Flee", null),
            ],
            Edges =
            [
                new Launcher.Application.EchosTrustEdge("1", "2", 0.9),
                new Launcher.Application.EchosTrustEdge("1", "3", 0.6),
                new Launcher.Application.EchosTrustEdge("2", "3", 0.75),
                new Launcher.Application.EchosTrustEdge("4", "5", 0.85),
                new Launcher.Application.EchosTrustEdge("4", "6", 0.15),
                new Launcher.Application.EchosTrustEdge("6", "1", 0.3),
            ],
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosRunDetail> ReadRunDetailAsync(string runId, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosRunDetail
        {
            RunId = runId,
            Seed = 42,
            TicksCount = 20,
            FirstTick = 1,
            LastTick = 20,
            Outcome = "surviving",
            Metrics = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, double>
                {
                    ["ClusteringCoefficient"] = 0.412,
                    ["AveragePathLength"] = 3.8,
                    ["Modularity"] = 0.27,
                },
                ["ResourceSustainabilityMetrics"] = new Dictionary<string, double>
                {
                    ["RecoveryTime"] = 14.5,
                },
                ["CognitiveDiversityMetrics"] = new Dictionary<string, double>
                {
                    ["PerspectiveCount"] = 6,
                },
            },
            Measured = new Dictionary<string, IReadOnlyDictionary<string, bool>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, bool>
                {
                    ["ClusteringCoefficient"] = true,
                    ["AveragePathLength"] = false,
                    ["Modularity"] = true,
                },
                ["ResourceSustainabilityMetrics"] = new Dictionary<string, bool> { ["RecoveryTime"] = true },
                ["CognitiveDiversityMetrics"] = new Dictionary<string, bool> { ["PerspectiveCount"] = true },
            },
            Disclaimer = "Replis neutres signalés comme non mesurés ; aucune valeur n'est estimée.",
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosAgentProfile> ReadAgentProfileAsync(string runId, string agentId, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosAgentProfile
        {
            AgentId = agentId,
            Tick = 20,
            Beliefs =
            [
                new Launcher.Application.EchosBelief("food", "location", "north", 0.82),
                new Launcher.Application.EchosBelief("water", "scarcity", "high", 0.55),
            ],
            Trust =
            [
                new Launcher.Application.EchosTrustRelation("2", 0.9),
                new Launcher.Application.EchosTrustRelation("3", 0.6),
            ],
        });

    /// <inheritdoc />
    public Task<IReadOnlyList<Launcher.Application.EchosDecision>> ReadDecisionsAsync(string runId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Launcher.Application.EchosDecision>>(
            [
                new Launcher.Application.EchosDecision(18, "1", "SeekFood", 0.72, "hunger=30,thirst=12"),
                new Launcher.Application.EchosDecision(19, "1", "Explore", 0.41, "food=absent"),
                new Launcher.Application.EchosDecision(20, "1", "Rest", 0.63, "fatigue=22"),
            ]);

    /// <inheritdoc />
    public Task<Launcher.Application.EchosEventFeed> ReadEventsAsync(string runId, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosEventFeed
        {
            RunId = runId,
            Total = 4,
            Limit = 500,
            Types =
            [
                new Launcher.Application.EchosEventKind("agent_died", 2),
                new Launcher.Application.EchosEventKind("decision_made", 2),
            ],
            Events =
            [
                new Launcher.Application.EchosEventRow(5, "decision_made", "2", "SeekFood", "hunger=71"),
                new Launcher.Application.EchosEventRow(9, "agent_died", "4", null, "energy=0"),
                new Launcher.Application.EchosEventRow(14, "decision_made", "1", "Rest", "fatigue=80"),
                new Launcher.Application.EchosEventRow(18, "agent_died", "3", null, "thirst=100"),
            ],
        });

    /// <inheritdoc />
    public Task<Launcher.Application.EchosPhenomena> ReadPhenomenaAsync(string? runId, CancellationToken cancellationToken) =>
        Task.FromResult(new Launcher.Application.EchosPhenomena
        {
            RunId = runId ?? "EXP-A-RUN-0001",
            Tick = 20,
            Detected =
            [
                new Launcher.Application.EchosPhenomenon("ResourceDepletion", 4, 6, 19),
                new Launcher.Application.EchosPhenomenon("CommunityFormation", 2, 3, 11),
                new Launcher.Application.EchosPhenomenon("InformationBottleneck", 1, 15, 15),
            ],
            Disclaimer = "Consommation de ressources observée sur ce run ; les replis neutres ne sont pas des mesures.",
        });
}

/// <summary>Application minimale : thème Fluent, sans composition ni processus.</summary>
public sealed class HarnessApplication : Application
{
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentTheme());
}

/// <summary>Façade factice : les données de référence de la maquette (GUI.md §7–§9).</summary>
public sealed class StubFacade : IOrchestrationFacade
{
    public event EventHandler<CampaignProgress>? CampaignProgressChanged { add { } remove { } }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRowViewModel> SnapshotComponents()
    {
        var rows = new List<ComponentRowViewModel>
        {
            new()
            {
                Id = "syne",
                Name = "SYNE",
                Version = "0.1.0",
                Subtitle = "Moteur de simulation multi-agents",
                Technology = ".NET 10 — Simulation.Console",
                Description = "Fait vivre des entités autonomes dans un monde déterministe, tick après tick.",
                Accent = "#1fa8e8",
                IsAvailable = true,
            },
            new()
            {
                Id = "echos",
                Name = "ECHOS",
                Version = "0.1.0",
                Subtitle = "Analyse et observation des mondes simulés",
                Technology = "Python — API REST",
                Description = "Collecte les flux de simulation et produit analyses et rapports d'émergence.",
                Accent = "#a04cf0",
                IsAvailable = true,
            },
            new()
            {
                Id = "prism",
                Name = "PRISM",
                Version = "0.1.0",
                Subtitle = "Immersion dans le monde simulé",
                Technology = "Unreal Engine — PRISM-LDK",
                Description = "Rend le monde en immersion. Non implémenté — verrouillé au jalon G7 (ADR-006).",
                Accent = "#f0902d",
                IsAvailable = false,
            },
        };

        rows[0].Update("Actif", string.Empty, 4821);
        rows[1].Update("Actif", string.Empty, 4937);
        rows[2].Update("Arrêté", string.Empty, null);
        return rows;
    }

    /// <inheritdoc />
    public (string GlobalState, string MainCause) AggregateHealth(IReadOnlyList<string> requiredComponents) =>
        ("Sain", "SYNE et ECHOS démarrés, PRISM arrêté (verrouillé G7)");

    /// <inheritdoc />
    public Task<PrismEngineStart> StartEnginePrismAsync() =>
        Task.FromResult(new PrismEngineStart(null, "syne", "http://127.0.0.1:5181", "ws://127.0.0.1:5180/"));

    /// <inheritdoc />
    public ProfileResolution ResolveProfile(string profileId, SessionKind session, IReadOnlyCollection<string>? extraComponents = null) => new() { Satisfiable = true, StartupOrder = { "syne" } };

    /// <inheritdoc />
    public ReportReadResult ReadEmergenceReport(string packagePath) => new(null, null);

    /// <inheritdoc />
    public Task<string?> ToggleComponentAsync(string componentId, bool start) => Task.FromResult<string?>(null);

    /// <inheritdoc />
    public ResourceSnapshot SampleResources() => new(42, 28, 31);

    /// <inheritdoc />
    public IReadOnlyList<LogEntryViewModel> RecentLogs() => new List<LogEntryViewModel>
    {
        new() { Level = "Info", Message = "SYNE démarré (PID 4821)", Timestamp = "27 sept. 2026 14:32:17" },
        new() { Level = "Info", Message = "ECHOS démarré (PID 4937)", Timestamp = "27 sept. 2026 14:32:21" },
        new() { Level = "Info", Message = "PRISM arrêté", Timestamp = "27 sept. 2026 12:17:03" },
        new() { Level = "Warn", Message = "Tentative de démarrage de PRISM ignorée (mode Standard)", Timestamp = "27 sept. 2026 12:16:59" },
        new() { Level = "Info", Message = "Campagne « Test Emergence 03 » terminée", Timestamp = "27 sept. 2026 11:48:32" },
    };

    public string SessionLogDirectory => Path.Combine(Path.GetTempPath(), "livex-screenshot-logs");

    public Task ExportSessionLogsAsync(string destinationPath) => Task.CompletedTask;
    public IReadOnlyList<PackageLogFileViewModel> ListPackageRunLogs(string packagePath) => [];
    public string ReadPackageRunLog(string packagePath, string entryPath) => string.Empty;
    public Task ExportPackageRunLogAsync(string packagePath, string entryPath, string destinationPath) => Task.CompletedTask;
    public IReadOnlyList<ComponentInstallationOption> ListComponentInstallations() => [];
    public void SetActiveInstallation(string componentId, string location) => throw new NotSupportedException();
    public ComponentInstallationOption RegisterComponentInstallation(string location) => throw new NotSupportedException();

    /// <inheritdoc />
    public IReadOnlyList<ExperienceRowViewModel> ListExperiences() => new List<ExperienceRowViewModel>
    {
        new() { ExperimentId = "e1", PackagePath = "e1.livexp", Name = "Test Emergence 03", Date = "27 sept. 2026 14:32", Duration = "10 000 ticks", Status = "Terminée" },
        new() { ExperimentId = "e2", PackagePath = "e2.livexp", Name = "Campagne Biodiversite", Date = "26 sept. 2026 22:17", Duration = "5 runs", Status = "Terminée" },
        new() { ExperimentId = "e3", PackagePath = "e3.livexp", Name = "Exploration Comportements", Date = "25 sept. 2026 16:03", Duration = "8 000 ticks", Status = "En cours" },
        new() { ExperimentId = "e4", PackagePath = "e4.livexp", Name = "Test Performance", Date = "24 sept. 2026 11:48", Duration = "3 000 ticks", Status = "Récupérable" },
        new() { ExperimentId = "e5", PackagePath = "e5.livexp", Name = "Campagne Longue Duree", Date = "22 sept. 2026 09:12", Duration = "15 runs", Status = "Terminée" },
    };

    public Task<string> RunCampaignAsync(Launcher.Domain.Model.ExperimentDefinition definition, CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);

    public Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken) =>
        Task.FromResult(packagePath);

    public IReadOnlyList<DocumentationItemViewModel> ListDocumentation() => [];

    public string ReadDocumentation(string relativePath) => string.Empty;
}
