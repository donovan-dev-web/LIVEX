using Avalonia.Controls;
using Launcher.Application;
using Launcher.Presentation.ViewModel;

namespace Launcher.App.Views;

/// <summary>
/// Gestionnaire de la fenêtre d'analyse (ADR-007) : une seule fenêtre à la fois.
/// Réouvrir l'analyse alors qu'elle est déjà affichée la ramène au premier plan au lieu
/// d'en créer une seconde, comme pour les consoles de composant.
/// </summary>
public sealed class AnalysisWindowHost : IDisposable
{
    private readonly IEchosTelemetrySource _source;
    private AnalysisWindow? _window;
    private bool _disposed;

    /// <summary>Initialise le gestionnaire sur la source de télémétrie ECHOS.</summary>
    public AnalysisWindowHost(IEchosTelemetrySource source) => _source = source;

    /// <summary>Ouvre la fenêtre d'analyse, ou la ramène au premier plan si elle est déjà ouverte.</summary>
    public void Open()
    {
        if (_disposed)
        {
            return;
        }

        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Activate();
            return;
        }

        var window = new AnalysisWindow(new AnalysisWindowViewModel(_source));
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;
        window.Show();
    }

    /// <summary>Ferme la fenêtre ouverte (libération de l'application).</summary>
    public void Dispose()
    {
        var window = _window;
        _window = null;
        _disposed = true;
        if (window is null)
        {
            return;
        }

        try
        {
            window.Close();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            // Une fenêtre déjà en cours de fermeture ne doit pas bloquer la libération.
        }
    }
}
