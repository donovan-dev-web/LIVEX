using Avalonia.Controls;
using Launcher.Domain;
using Launcher.Presentation.ViewModel;

namespace Launcher.App.Views;

/// <summary>
/// Gestionnaire des fenêtres console du Launcher (USER_INTERFACE.md §9). Une fenêtre par instance
/// supervisée : rouvrir la console d'un composant déjà affiché la ramène au premier plan
/// au lieu d'en créer une seconde.
///
/// Toute ouverture passe par <see cref="Open"/> sur le fil d'interface : l'événement de
/// démarrage d'un composant est lui émis depuis le fil qui a lancé le processus.
/// </summary>
public sealed class ConsoleWindowHost : IDisposable
{
    private readonly IComponentLogSource _source;
    private readonly Func<string, string> _logsDirectory;
    private readonly Dictionary<string, ConsoleWindow> _open = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Initialise le gestionnaire sur la source de lignes et la résolution des journaux.</summary>
    public ConsoleWindowHost(IComponentLogSource source, Func<string, string> logsDirectory)
    {
        _source = source;
        _logsDirectory = logsDirectory;
    }

    /// <summary>Ouvre la console d'une instance, ou la ramène au premier plan si elle est déjà ouverte.</summary>
    public void Open(string instanceId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        if (_open.TryGetValue(instanceId, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        var window = new ConsoleWindow(new ConsoleViewModel(
            _source,
            instanceId,
            TitleFor(instanceId),
            _logsDirectory(instanceId)));
        window.Closed += (_, _) =>
        {
            lock (_open)
            {
                _open.Remove(instanceId);
            }
        };

        lock (_open)
        {
            _open[instanceId] = window;
        }

        window.Show();
    }

    /// <summary>Ferme toutes les consoles ouvertes (libération de l'application).</summary>
    public void Dispose()
    {
        List<ConsoleWindow> windows;
        lock (_open)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            windows = _open.Values.ToList();
            _open.Clear();
        }

        foreach (var window in windows)
        {
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

    /// <summary>
    /// Titre d'une console : le composant identifié par l'instance, puis l'instance elle-même,
    /// pour distinguer deux consoles SYNE successives.
    /// </summary>
    public static string TitleFor(string instanceId)
    {
        var name = instanceId switch
        {
            _ when instanceId.StartsWith("syne-mock", StringComparison.Ordinal) => "SYNE (émulé)",
            _ when instanceId.StartsWith("syne", StringComparison.Ordinal) => "SYNE",
            _ when instanceId.StartsWith("echos", StringComparison.Ordinal) => "ECHOS",
            _ when instanceId.StartsWith("prism", StringComparison.Ordinal) => "PRISM",
            _ => instanceId,
        };
        return $"Console — {name} · {instanceId}";
    }
}
