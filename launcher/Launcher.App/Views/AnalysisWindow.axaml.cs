using Avalonia.Controls;
using Avalonia.Threading;
using Launcher.Application;
using Launcher.Presentation.ViewModel;

namespace Launcher.App.Views;

/// <summary>
/// Fenêtre d'analyse native (ADR-007) : présente les séries, le profil de viabilité,
/// les communautés et les phénomènes publiés par l'API REST d'ECHOS — à la place de
/// l'interface web/Electron supprimée.
///
/// L'horloge d'interface (1 s) fait deux choses distinctes : elle avance la relecture
/// d'un run stocké (curseur de tick, sans effet sur SYNE) et n'appelle
/// <see cref="AnalysisWindowViewModel.RefreshAsync"/> que si la fenêtre est en mode
/// direct et si le relevé précédent a rendu la main : un seul relevé à la fois,
/// jamais empilé.
/// </summary>
public sealed partial class AnalysisWindow : Window
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private readonly AnalysisWindowViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    /// <summary>Constructeur requis par le chargement XAML et les concepteurs.</summary>
    public AnalysisWindow()
        : this(new AnalysisWindowViewModel(new EmptyTelemetrySource()))
    {
    }

    /// <summary>Initialise la fenêtre sur sa vue modèle et arme l'horloge de relecture.</summary>
    public AnalysisWindow(AnalysisWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Background, OnRefreshTick);

        // Le sélecteur de run : tant que la liste est ouverte, aucun relevé ne la
        // reconstruit (une reconstruction referme la liste — « impossible de
        // sélectionner une expérience »). Le relevé différé reprend juste après.
        RunSelector.DropDownOpened += (_, _) => _viewModel.IsRunSelectorOpen = true;
        RunSelector.DropDownClosed += (_, _) =>
        {
            _viewModel.IsRunSelectorOpen = false;
            if (_viewModel.IsLive && !_viewModel.IsBusy)
            {
                _ = _viewModel.RefreshAsync();
            }
        };

        Opened += (_, _) =>
        {
            _ = _viewModel.RefreshAsync();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        // Relecture : avance la fenêtre de lecture sur les séries déjà publiées.
        _viewModel.AdvanceReplay();

        if (!_viewModel.IsLive || _viewModel.IsBusy || _viewModel.IsRunSelectorOpen)
        {
            return;
        }

        _ = _viewModel.RefreshAsync();
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _timer.Stop();
        base.OnClosing(e);
    }

    /// <summary>Source vide, réservée au constructeur de conception.</summary>
    private sealed class EmptyTelemetrySource : IEchosTelemetrySource
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<EchosRunSummary>> ReadRunsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EchosRunSummary>>([]);

        /// <inheritdoc />
        public Task<EchosSeries> ReadSeriesAsync(string? runId, int every, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosSeries { RunId = runId ?? string.Empty });

        /// <inheritdoc />
        public Task<EchosMetricCatalog> ReadCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(EchosMetricCatalog.Empty);

        /// <inheritdoc />
        public Task<EchosViability> ReadViabilityAsync(
            string runId, int every, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosViability { RunId = runId });

        /// <inheritdoc />
        public Task<EchosExperimentSummary> ReadExperimentSummaryAsync(
            IReadOnlyList<string> runIds, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosExperimentSummary());

        /// <inheritdoc />
        public Task<EchosNetwork> ReadNetworkAsync(string? runId, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosNetwork { RunId = runId ?? string.Empty });

        /// <inheritdoc />
        public Task<EchosPhenomena> ReadPhenomenaAsync(string? runId, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosPhenomena { RunId = runId ?? string.Empty });

        /// <inheritdoc />
        public Task<EchosWorld> ReadWorldAsync(string? runId, int? tick, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosWorld { RunId = runId ?? string.Empty });

        /// <inheritdoc />
        public Task<EchosTrustGraph> ReadTrustGraphAsync(
            string? runId, int? tick, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosTrustGraph { RunId = runId ?? string.Empty });

        /// <inheritdoc />
        public Task<EchosRunDetail> ReadRunDetailAsync(string runId, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosRunDetail { RunId = runId });

        /// <inheritdoc />
        public Task<EchosAgentProfile> ReadAgentProfileAsync(
            string runId, string agentId, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosAgentProfile { AgentId = agentId });

        /// <inheritdoc />
        public Task<IReadOnlyList<EchosDecision>> ReadDecisionsAsync(string runId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EchosDecision>>([]);

        /// <inheritdoc />
        public Task<EchosEventFeed> ReadEventsAsync(string runId, CancellationToken cancellationToken) =>
            Task.FromResult(new EchosEventFeed { RunId = runId });
    }
}
