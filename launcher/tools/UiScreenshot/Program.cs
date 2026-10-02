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
        new() { ExperimentId = "e1", Name = "Test Emergence 03", Date = "27 sept. 2026 14:32", Duration = "10 000 ticks", Status = "Terminée" },
        new() { ExperimentId = "e2", Name = "Campagne Biodiversite", Date = "26 sept. 2026 22:17", Duration = "5 runs", Status = "Terminée" },
        new() { ExperimentId = "e3", Name = "Exploration Comportements", Date = "25 sept. 2026 16:03", Duration = "8 000 ticks", Status = "En cours" },
        new() { ExperimentId = "e4", Name = "Test Performance", Date = "24 sept. 2026 11:48", Duration = "3 000 ticks", Status = "Récupérable" },
        new() { ExperimentId = "e5", Name = "Campagne Longue Duree", Date = "22 sept. 2026 09:12", Duration = "15 runs", Status = "Terminée" },
    };

    public Task<string> RunCampaignAsync(Launcher.Domain.Model.ExperimentDefinition definition, CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);

    public Task<string> ResumeCampaignAsync(string packagePath, CancellationToken cancellationToken) =>
        Task.FromResult(packagePath);

    public IReadOnlyList<DocumentationItemViewModel> ListDocumentation() => [];

    public string ReadDocumentation(string relativePath) => string.Empty;
}
