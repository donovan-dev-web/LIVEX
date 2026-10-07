using System.Collections.ObjectModel;
using System.Globalization;
using Launcher.Domain;

namespace Launcher.Presentation.ViewModel;

/// <summary>
/// Vue modèle d'une console de composant (USER_INTERFACE.md §9) : les lignes produites par un
/// processus supervisé sont lues en direct depuis <see cref="IComponentLogSource"/> et
/// affichées, sans interprétation — la console montre la sortie telle que le composant
/// l'a émise.
///
/// La lecture est **par petites touches** : la vue appelle <see cref="Poll"/> sur une
/// horloge d'interface et reçoit ce qui est arrivé depuis le dernier appel. Chaque ligne
/// reçue est retenue dans un tampon brut borné, indépendamment des filtres : basculer un
/// canal restitue l'historique déjà lu au lieu de le rendre une seule fois puis de le
/// perdre. Le relevé est lui aussi plafonné, pour qu'une console ouverte sur un flux déjà
/// long rattrape en plusieurs coups plutôt que de figer l'interface sur un seul.
///
/// Aucune valeur scientifique n'est produite ici : une console n'affiche que des faits
/// d'exécution (ADR-003 Launcher).
/// </summary>
public sealed class ConsoleViewModel : ObservableObject
{
    /// <summary>Nombre maximal de lignes affichées ; au-delà, les plus anciennes sont retirées.</summary>
    private const int MaxVisibleLines = 5000;

    /// <summary>
    /// Nombre maximal de lignes lues par coup de sonde : ouvrir une console sur un flux déjà
    /// long ne doit pas figer l'interface sur un déversement unique — le rattrapage se fait
    /// sur les sondes suivantes.
    /// </summary>
    private const int MaxLinesPerPoll = 2000;

    private readonly IComponentLogSource _source;
    private readonly List<ComponentLogLine> _raw = new(MaxVisibleLines);
    private long _cursor;
    private bool _isPaused;
    private bool _autoScroll = true;
    private bool _showStdout = true;
    private bool _showStderr = true;
    private long _receivedCount;
    private string _statusText;

    /// <summary>Initialise la console sur une instance et son titre affichable.</summary>
    public ConsoleViewModel(IComponentLogSource source, string instanceId, string title, string logsDirectory)
    {
        _source = source;
        InstanceId = instanceId;
        Title = title;
        LogsDirectory = logsDirectory;
        // Curseur à zéro : la console ouverte en cours de route montre l'historique retenu
        // par le tampon, pas seulement la suite — c'est un journal, pas un flux éphémère.
        _cursor = 0;
        _statusText = BuildStatus();
        ActionCommand = new RelayCommand<string>(ExecuteAction);
    }

    /// <summary>Identifiant d'instance supervisée (« echos-0001 », « syne-mock-0002 »).</summary>
    public string InstanceId { get; }

    /// <summary>Titre de la fenêtre : composant et instance.</summary>
    public string Title { get; }

    /// <summary>Dossier des journaux persistants de l'instance, ouvert par la fenêtre.</summary>
    public string LogsDirectory { get; }

    /// <summary>Lignes visibles, filtrées et bornées.</summary>
    public ObservableCollection<ConsoleLineViewModel> Lines { get; } = new();

    /// <summary>Commande d'action de la barre d'outils : pause, effacer, filtres, défilement.</summary>
    public RelayCommand<string> ActionCommand { get; }

    /// <summary>Vrai tant que la lecture est en pause : les lignes continuent d'arriver dans le tampon.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseLabel));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>Libellé du bouton de pause : « Pause » ou « Reprendre ».</summary>
    public string PauseLabel => IsPaused ? "Reprendre" : "Pause";

    /// <summary>Vrai si le défilement suit automatiquement la dernière ligne.</summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    /// <summary>Vrai si les lignes de sortie standard sont affichées.</summary>
    public bool ShowStdout
    {
        get => _showStdout;
        set
        {
            if (SetProperty(ref _showStdout, value))
            {
                RebuildVisible();
            }
        }
    }

    /// <summary>Vrai si les lignes d'erreur sont affichées.</summary>
    public bool ShowStderr
    {
        get => _showStderr;
        set
        {
            if (SetProperty(ref _showStderr, value))
            {
                RebuildVisible();
            }
        }
    }

    /// <summary>Compte-rendu factuel de la console : lignes affichées, reçues, instance.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Vrai si la dernière lecture a ajouté au moins une ligne (la vue défile alors).</summary>
    public bool Poll()
    {
        if (IsPaused)
        {
            return false;
        }

        var batch = _source.ReadSince(InstanceId, _cursor);
        if (batch.Count == 0)
        {
            return false;
        }

        var appended = false;
        var take = Math.Min(batch.Count, MaxLinesPerPoll);
        for (var index = 0; index < take; index++)
        {
            var line = batch[index];
            _cursor = line.Sequence;
            _receivedCount++;
            _raw.Add(line);
            if (_raw.Count > MaxVisibleLines)
            {
                _raw.RemoveAt(0);
            }

            if (IsVisible(line))
            {
                Lines.Add(ToViewModel(line));
                appended = true;
            }
        }

        while (Lines.Count > MaxVisibleLines)
        {
            Lines.RemoveAt(0);
        }

        StatusText = BuildStatus();
        return appended;
    }

    /// <summary>Repart de zéro : les lignes déjà reçues ne sont pas rejouées.</summary>
    public void Clear()
    {
        _raw.Clear();
        Lines.Clear();
        _cursor = _source.LatestSequence(InstanceId);
        _receivedCount = 0;
        StatusText = BuildStatus();
    }

    private void ExecuteAction(string? action)
    {
        switch (action)
        {
            case "pause":
                IsPaused = !IsPaused;
                break;
            case "clear":
                Clear();
                break;
            case "autoscroll":
                AutoScroll = !AutoScroll;
                break;
            case "stdout":
                ShowStdout = !ShowStdout;
                break;
            case "stderr":
                ShowStderr = !ShowStderr;
                break;
        }
    }

    private bool IsVisible(ComponentLogLine line) =>
        line.Stream == "stderr" ? ShowStderr : ShowStdout;

    private void RebuildVisible()
    {
        Lines.Clear();
        foreach (var line in _raw)
        {
            if (IsVisible(line))
            {
                Lines.Add(ToViewModel(line));
            }
        }

        StatusText = BuildStatus();
    }

    private string BuildStatus() =>
        $"{Lines.Count} ligne(s) affichée(s) — {_receivedCount} reçue(s) — {InstanceId}";

    private static ConsoleLineViewModel ToViewModel(ComponentLogLine line) => new()
    {
        Timestamp = line.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture),
        Stream = line.Stream,
        IsError = string.Equals(line.Stream, "stderr", StringComparison.Ordinal),
        Text = line.Text,
    };
}

/// <summary>Une ligne de console affichée : horodatage, canal, texte, couleur du canal.</summary>
public sealed class ConsoleLineViewModel
{
    /// <summary>Horodatage local, millisecondes comprises.</summary>
    public string Timestamp { get; init; } = string.Empty;

    /// <summary>Canal émetteur : « stdout » ou « stderr ».</summary>
    public string Stream { get; init; } = string.Empty;

    /// <summary>Vrai pour une ligne de stderr, affichée en rouge (GUI.md §3.1).</summary>
    public bool IsError { get; init; }

    /// <summary>Texte de la ligne, tel que produit par le composant.</summary>
    public string Text { get; init; } = string.Empty;
}
