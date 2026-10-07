using Launcher.Domain;

namespace Launcher.Infrastructure;

/// <summary>
/// Tampon des lignes de sortie des composants (USER_INTERFACE.md §9). Chaque ligne publiée par le
/// gestionnaire de processus est conservée en mémoire, par instance et dans l'ordre de sa
/// séquence, pour être lue par les fenêtres console du Launcher.
///
/// Le tampon est **borné** : une simulation longue produirait des millions de lignes, et une
/// console n'affiche que les dernières. Au débordement, les lignes les plus anciennes sont
/// retirées ; une lecture antérieure à la limite est alors satisfaites à partir de la plus
/// ancienne ligne conservée, sans que la console perde pour autant la fin du flux.
///
/// Le fichier de journal reste la source durable : ce tampon ne fait que servir l'affichage.
/// </summary>
public sealed class ComponentLogBuffer : IComponentLogSource, IDisposable
{
    /// <summary>Nombre maximal de lignes conservées par instance.</summary>
    private const int MaxLinesPerInstance = 20000;

    private readonly IProcessManager _processes;
    private readonly Dictionary<string, List<ComponentLogLine>> _byInstance = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _latest = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>Initialise le tampon et s'abonne aux lignes du gestionnaire de processus.</summary>
    public ComponentLogBuffer(IProcessManager processes)
    {
        _processes = processes;
        _processes.LineEmitted += OnLineEmitted;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentLogLine> ReadSince(string instanceId, long afterSequence)
    {
        lock (_gate)
        {
            if (!_byInstance.TryGetValue(instanceId, out var lines) || lines.Count == 0)
            {
                return [];
            }

            var index = 0;
            while (index < lines.Count && lines[index].Sequence <= afterSequence)
            {
                index++;
            }

            return index >= lines.Count ? [] : lines.GetRange(index, lines.Count - index);
        }
    }

    /// <inheritdoc />
    public long LatestSequence(string instanceId)
    {
        lock (_gate)
        {
            return _latest.GetValueOrDefault(instanceId);
        }
    }

    private void OnLineEmitted(object? sender, ComponentLogLineEventArgs args)
    {
        var line = args.Line;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (!_byInstance.TryGetValue(line.InstanceId, out var lines))
            {
                lines = new List<ComponentLogLine>();
                _byInstance[line.InstanceId] = lines;
            }

            lines.Add(line);
            if (lines.Count > MaxLinesPerInstance)
            {
                lines.RemoveRange(0, lines.Count - MaxLinesPerInstance);
            }

            _latest[line.InstanceId] = line.Sequence;
        }
    }

    /// <summary>Désabonne : après libération, plus aucune ligne n'est retenue.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _byInstance.Clear();
            _latest.Clear();
        }

        _processes.LineEmitted -= OnLineEmitted;
    }
}
