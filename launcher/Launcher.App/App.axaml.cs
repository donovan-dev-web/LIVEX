using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Launcher.App.Composition;
using Launcher.App.Views;

namespace Launcher.App;

/// <summary>Application Avalonia : composition injectée, fenêtre principale, paquet ouvert par --package.</summary>
public sealed class App : Avalonia.Application
{
    private readonly LauncherComposition _composition;
    private readonly string? _packageToOpen;

    /// <summary>Initialise l'application avec sa composition. Constructeur aussi requis par le designer.</summary>
    public App()
        : this(new LauncherComposition(), null)
    {
    }

    /// <summary>Initialise l'application avec une composition et un paquet à ouvrir (tests, CLI).</summary>
    public App(LauncherComposition composition, string? packageToOpen)
    {
        _composition = composition;
        _packageToOpen = packageToOpen;
    }

    /// <summary>Composition racine.</summary>
    public LauncherComposition Composition => _composition;

    /// <summary>Chemin du paquet passé par --package, s'il y en a un.</summary>
    public string? PackageToOpen => _packageToOpen;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async Task RunToggleAsync(Presentation.ViewModel.MainWindowViewModel viewModel, string componentId, bool start)
    {
        try
        {
            var error = await _composition.Facade.ToggleComponentAsync(componentId, start).ConfigureAwait(false);
            if (error is not null)
            {
                _composition.Journal.Warn("ComponentToggle", $"composant {componentId} : {error}");
            }
        }
        catch (Exception exception)
        {
            _composition.Journal.Fail("ComponentToggle", $"composant {componentId} : {exception.Message}");
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(viewModel.RefreshFromRegistry);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var facade = _composition.Facade;
            var viewModel = new Presentation.ViewModel.MainWindowViewModel(facade);
            facade.CampaignProgressChanged += (_, progress) =>
                Dispatcher.UIThread.Post(() => viewModel.UpdateCampaignProgress(progress));

            // Consoles de composant (USER_INTERFACE.md §9) : une fenêtre native par instance supervisée,
            // ouverte automatiquement au démarrage et à la demande depuis les cartes.
            var consoles = new ConsoleWindowHost(_composition.Logs, facade.LogsDirectoryFor);
            facade.ComponentStarted += (_, started) =>
                Dispatcher.UIThread.Post(() => consoles.Open(started.InstanceId));
            viewModel.SetConsoleHandler(instanceId => consoles.Open(instanceId));

            // Fenêtre d'analyse native (ADR-007) : une seule fenêtre, alimentée par l'API
            // REST d'ECHOS — l'interface web/Electron d'ECHOS a été supprimée.
            var analysis = new AnalysisWindowHost(_composition.EchosTelemetry);
            viewModel.SetAnalysisHandler(analysis.Open);

            // Cycle de vie réel des cartes (INTEGRATION_CONTRACT.md §5) : l'erreur éventuelle
            // est journalisée puis rafraîchie, jamais masquée. Le rafraîchissement revient
            // toujours sur le fil d'interface (Avalonia : collections liées non thread-safe).
            viewModel.SetLifecycleHandler((componentId, start) =>
            {
                _ = RunToggleAsync(viewModel, componentId, start);
            });

            // Profil sélectionné (COMPONENTS.md §9, G7) : démarrage dans l'ordre de résolution,
            // arrêt en ordre inverse strict. Chaque erreur est journalisée, jamais masquée.
            viewModel.SetProfileLifecycleHandler(async (order, start) =>
            {
                try
                {
                    foreach (var componentId in order)
                    {
                        var error = await facade.ToggleComponentAsync(componentId, start).ConfigureAwait(false);
                        if (error is not null)
                        {
                            _composition.Journal.Warn("ProfileToggle",
                                $"profil {viewModel.SelectedProfile} : {componentId} : {error}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    _composition.Journal.Fail("ProfileToggle",
                        $"profil {viewModel.SelectedProfile} : {exception.Message}");
                }

                await Dispatcher.UIThread.InvokeAsync(viewModel.RefreshFromRegistry);
            });

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // Une console ouverte ne doit pas prolonger la vie de l'application après la
            // fermeture de la fenêtre principale.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            viewModel.RefreshFromRegistry();
            viewModel.RefreshExperiences();

            // Échantillonnage périodique (GUI.md §11) : état, ressources, journaux.
            var sampler = new DispatcherTimer(TimeSpan.FromMilliseconds(800), DispatcherPriority.Background,
                (_, _) => viewModel.RefreshFromRegistry());
            sampler.Start();

            // --package : le paquet est ouvert en lecture au démarrage (USER_INTERFACE.md §3.1,
            // paquet scellé consultable, paquet vivant avec avertissement).
            if (_packageToOpen is not null)
            {
                viewModel.OpenPackage(_packageToOpen);
            }

            // La surface HTTP et les processus lancés sont libérés à la fermeture (NETWORK.md §4.1).
            desktop.ShutdownRequested += (_, _) =>
            {
                analysis.Dispose();
                consoles.Dispose();
                _composition.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
