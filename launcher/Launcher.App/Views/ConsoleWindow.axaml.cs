using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Launcher.Domain;
using Launcher.Presentation.ViewModel;

namespace Launcher.App.Views;

/// <summary>
/// Fenêtre console d'un composant (USER_INTERFACE.md §9) : affiche en direct la sortie du processus
/// supervisé, sans interprétation.
///
/// La sonde est une horloge d'interface (250 ms) qui appelle <see cref="ConsoleViewModel.Poll"/> :
/// le tampon est lu depuis le fil d'exécution et les lignes reçues sont ajoutées sur le fil
/// d'interface — aucune collection n'est touchée depuis le fil de pompe des flux.
/// </summary>
public sealed partial class ConsoleWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConsoleViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    /// <summary>Constructeur requis par le chargement XAML et les concepteurs.</summary>
    public ConsoleWindow()
        : this(new ConsoleViewModel(new EmptyLogSource(), string.Empty, "Console", string.Empty))
    {
    }

    /// <summary>Initialise la fenêtre sur sa vue modèle et arme la sonde de lecture.</summary>
    public ConsoleWindow(ConsoleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _timer = new DispatcherTimer(PollInterval, DispatcherPriority.Background, OnPollTick);
        Closed += (_, _) => _timer.Stop();
        _timer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (!_viewModel.Poll())
        {
            return;
        }

        if (_viewModel.AutoScroll && _viewModel.Lines.Count > 0)
        {
            LinesList.ScrollIntoView(_viewModel.Lines[^1]);
        }
    }

    private void OnOpenLogsDirectoryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var directory = _viewModel.LogsDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            // L'ouverture du dossier est un confort : son échec ne touche pas à l'affichage.
            System.Diagnostics.Debug.WriteLine($"console : ouverture du dossier impossible — {exception.Message}");
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _timer.Stop();
        base.OnClosing(e);
    }

    /// <summary>Source vide, réservée au constructeur de conception.</summary>
    private sealed class EmptyLogSource : IComponentLogSource
    {
        /// <inheritdoc />
        public IReadOnlyList<ComponentLogLine> ReadSince(string instanceId, long afterSequence) => [];

        /// <inheritdoc />
        public long LatestSequence(string instanceId) => 0;
    }
}
