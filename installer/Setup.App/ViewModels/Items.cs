// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Setup.App.Localization;
using Setup.App.Services;
using Setup.Core.Distribution;

namespace Setup.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>Étape de la barre latérale.</summary>
public sealed class StepItem : ViewModelBase
{
    private static readonly IBrush DoneBackground = new SolidColorBrush(Color.Parse("#E2A44A"));
    private static readonly IBrush CurrentBackground = new SolidColorBrush(Color.Parse("#8B7BE8"));
    private static readonly IBrush PendingBackground = new SolidColorBrush(Color.Parse("#261F3D"));
    private static readonly IBrush DoneText = new SolidColorBrush(Color.Parse("#1B1330"));
    private static readonly IBrush CurrentText = new SolidColorBrush(Color.Parse("#F3EEE4"));
    private static readonly IBrush PendingText = new SolidColorBrush(Color.Parse("#8F87A5"));
    private static readonly IBrush CurrentLabel = new SolidColorBrush(Color.Parse("#F3EEE4"));
    private static readonly IBrush OtherLabel = new SolidColorBrush(Color.Parse("#A79FBB"));

    public required int Index { get; init; }

    public required string LabelKey { get; init; }

    public string Label => Strings.Instance[LabelKey];

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { if (SetField(ref _isCurrent, value)) RaiseVisuals(); }
    }

    private bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set { if (SetField(ref _isDone, value)) RaiseVisuals(); }
    }

    public IBrush MarkerBackground => IsDone ? DoneBackground : IsCurrent ? CurrentBackground : PendingBackground;
    public IBrush MarkerForeground => IsDone ? DoneText : IsCurrent ? CurrentText : PendingText;
    public IBrush LabelForeground => IsCurrent ? CurrentLabel : OtherLabel;
    public string Marker => IsDone ? "✓" : Index.ToString();

    public void RefreshLanguage() => OnPropertyChanged(nameof(Label));

    private void RaiseVisuals()
    {
        OnPropertyChanged(nameof(MarkerBackground));
        OnPropertyChanged(nameof(MarkerForeground));
        OnPropertyChanged(nameof(LabelForeground));
        OnPropertyChanged(nameof(Marker));
    }
}

/// <summary>Composant sélectionnable, généré depuis le manifeste.</summary>
public sealed class ComponentItem : ViewModelBase
{
    private readonly Action _selectionChanged;

    public ComponentItem(
        ComponentDescriptor descriptor, string language, Action selectionChanged,
        bool? selected = null)
    {
        Descriptor = descriptor;
        _selectionChanged = selectionChanged;
        // Par défaut, le socle et PRISM (avertissement associé) sont cochés.
        // La gestion des composants impose la sélection réellement installée.
        _selected = selected ?? (descriptor.Mandatory || descriptor.Id == "prism");
    }

    public ComponentDescriptor Descriptor { get; }

    public string Id => Descriptor.Id;

    public bool Required => Descriptor.Mandatory;

    public bool IsBase => !Descriptor.Mandatory && Descriptor.Acquisition.Kind == "payload";

    public bool Optional => !Descriptor.Mandatory && Descriptor.Acquisition.Kind != "payload";

    public bool Unavailable =>
        Descriptor.Acquisition.Kind == "unavailable" ||
        (Descriptor.Acquisition.Kind == "release-asset" && Descriptor.Acquisition.Url is null);

    public bool CanToggle => !Required && !Unavailable;

    public string AcquisitionKind => Descriptor.Acquisition.Kind;

    public string Name => string.Equals(Strings.Instance.Language, "en", StringComparison.OrdinalIgnoreCase)
        ? Descriptor.NameEn
        : Descriptor.NameFr;

    public string Description => string.Equals(Strings.Instance.Language, "en", StringComparison.OrdinalIgnoreCase)
        ? Descriptor.DescriptionEn
        : Descriptor.DescriptionFr;

    public string VersionText => $"v{Descriptor.Version}";

    public string RequiresText => Descriptor.Requires.Count == 0
        ? string.Empty
        : Strings.Instance.Format("componentsRequires", string.Join(", ", Descriptor.Requires));

    public bool HasRequires => Descriptor.Requires.Count > 0;

    public string SizeText
    {
        get
        {
            var size = Descriptor.Acquisition.Kind == "release-asset"
                ? Descriptor.Acquisition.Size ?? 0
                : Descriptor.Files.Sum(f => f.Size);
            return size <= 0 ? string.Empty : SizeFormatter.Format(size);
        }
    }

    public bool HasSize => SizeText.Length > 0;

    public string BadgeText => Unavailable
        ? Strings.Instance["badgeUnavailable"]
        : Required
            ? Strings.Instance["badgeRequired"]
            : IsBase
                ? Strings.Instance["badgeBase"]
                : Strings.Instance["badgeOptional"];

    public string AcquisitionText => Unavailable
        ? Descriptor.Acquisition.Reason ?? string.Empty
        : Descriptor.Acquisition.Kind == "release-asset"
            ? Strings.Instance["badgeDownload"]
            : Strings.Instance["badgeBundled"];

    public string UnavailableText => Strings.Instance.Format(
        "componentsUnavailable",
        Descriptor.Acquisition.Reason ?? Strings.Instance["badgeUnavailable"]);

    public IBrush BadgeBackground => Unavailable
        ? new SolidColorBrush(Color.Parse("#3A2530"))
        : Required
            ? new SolidColorBrush(Color.Parse("#2A3A32"))
            : IsBase
                ? new SolidColorBrush(Color.Parse("#3A301E"))
                : new SolidColorBrush(Color.Parse("#2B2545"));

    public IBrush BadgeForeground => Unavailable
        ? new SolidColorBrush(Color.Parse("#E5646B"))
        : Required
            ? new SolidColorBrush(Color.Parse("#4FBF8B"))
            : IsBase
                ? new SolidColorBrush(Color.Parse("#E2A44A"))
                : new SolidColorBrush(Color.Parse("#B8AEEF"));

    private bool _selected;
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value))
            {
                return;
            }

            _selectionChanged();
        }
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RequiresText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(HasSize));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(AcquisitionText));
        OnPropertyChanged(nameof(UnavailableText));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
    }
}

public enum StageState
{
    Pending,
    Running,
    Done,
}

/// <summary>Étape de progression affichée pendant l'écriture.</summary>
public sealed class StageItem : ViewModelBase
{
    public StageItem(string labelKey, string label)
    {
        LabelKey = labelKey;
        _label = label;
    }

    public string LabelKey { get; }

    private string _label;
    public string Label
    {
        get => _label;
        private set => SetField(ref _label, value);
    }

    private StageState _state;
    public StageState State
    {
        get => _state;
        set
        {
            if (!SetField(ref _state, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(Marker));
            OnPropertyChanged(nameof(MarkerBackground));
            OnPropertyChanged(nameof(MarkerForeground));
            OnPropertyChanged(nameof(LabelForeground));
        }
    }

    public string StateText => State switch
    {
        StageState.Done => Strings.Instance["progressDone"],
        StageState.Running => Strings.Instance["progressRunning"],
        _ => Strings.Instance["progressWaiting"],
    };

    public string Marker => State switch
    {
        StageState.Done => "✓",
        StageState.Running => "•",
        _ => "○",
    };

    public IBrush MarkerBackground => State switch
    {
        StageState.Done => new SolidColorBrush(Color.Parse("#E2A44A")),
        StageState.Running => new SolidColorBrush(Color.Parse("#8B7BE8")),
        _ => new SolidColorBrush(Color.Parse("#261F3D")),
    };

    public IBrush MarkerForeground => State == StageState.Pending
        ? new SolidColorBrush(Color.Parse("#8F87A5"))
        : new SolidColorBrush(Color.Parse("#1B1330"));

    public IBrush LabelForeground => State == StageState.Pending
        ? new SolidColorBrush(Color.Parse("#8F87A5"))
        : new SolidColorBrush(Color.Parse("#F3EEE4"));

    public void RefreshLanguage()
    {
        Label = Strings.Instance[LabelKey];
        OnPropertyChanged(nameof(StateText));
    }
}

/// <summary>Ligne du récapitulatif.</summary>
public sealed record SummaryRow(string Label, string Value, bool Warning = false, bool Mono = false);

/// <summary>Contrôle de préflight affiché à l'écran.</summary>
public sealed class CheckItem
{
    public CheckItem(string label, string status, string? message)
    {
        Label = label;
        Status = status;
        Message = message;
    }

    public string Label { get; }

    public string Status { get; }

    public bool Passed => Status == "Pass";

    public bool Simulated => Status == "Simulated";

    public string? Message { get; }

    public string Marker => Status switch
    {
        "Pass" => "✓",
        "Simulated" => "≈",
        "Warn" => "!",
        "Info" => "i",
        _ => "✕",
    };

    public IBrush MarkerBrush => Status switch
    {
        "Pass" => new SolidColorBrush(Color.Parse("#4FBF8B")),
        "Simulated" => new SolidColorBrush(Color.Parse("#8B7BE8")),
        "Warn" => new SolidColorBrush(Color.Parse("#E8B44A")),
        "Info" => new SolidColorBrush(Color.Parse("#A79FBB")),
        _ => new SolidColorBrush(Color.Parse("#E5646B")),
    };

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
}
