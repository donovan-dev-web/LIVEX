// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Setup.App.CommandLine;
using Setup.Core.Simulation;
using Setup.App.Localization;
using Setup.App.Services;
using Setup.App.Views;
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Planning;
using Setup.Core.Preflight;

namespace Setup.App.ViewModels;

public enum WizardStep
{
    Welcome,
    Language,
    License,
    Components,
    Paths,
    Summary,
    Progress,
    Result,
    Maintenance,
    Uninstall,
}

public enum FlowKind
{
    Install,
    Maintenance,
    Uninstall,
}

public enum ResultKind
{
    Success,
    Warning,
    Failure,
}

public sealed class WizardViewModel : ViewModelBase
{
    private readonly InstallerSession _session;
    private readonly AppOptions _options;
    private readonly Dictionary<WizardStep, object> _views = new();

    private CancellationTokenSource? _cts;
    private bool _operationRunning;
    private InstallPlan? _plan;
    private FlowKind _flow = FlowKind.Install;
    private MaintenanceActionKind _maintenanceAction = MaintenanceActionKind.Update;
    private UninstallScope _uninstallScope = UninstallScope.Software;
    private ResultKind _resultKind = ResultKind.Success;
    private string _resultTitle = string.Empty;
    private string _resultBody = string.Empty;
    private string _resultLog = string.Empty;
    private string _resultResiduals = string.Empty;

    public WizardViewModel(InstallerSession session, AppOptions options)
    {
        _session = session;
        _options = options;

        Strings.Instance.SetLanguage(options.Language);

        InstallDir = session.DefaultInstallDir;
        WorkspaceDir = session.DefaultWorkspaceDir;
        DataRoot = session.DefaultDataRoot;

        BuildComponents();
        BuildViews();
        GoTo(App.InitialStep);

        _ = InitializeAsync();
    }

    public InstallerSession Session => _session;

    public DistributionManifest Manifest => _session.Manifest;

    // ---------------------------------------------------------------- étapes

    private WizardStep _step;
    public WizardStep Step
    {
        get => _step;
        private set
        {
            if (!SetField(ref _step, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(StepSubtitle));
            RaiseButtons();
        }
    }

    private object? _currentView;
    public object? CurrentView
    {
        get => _currentView;
        private set => SetField(ref _currentView, value);
    }

    public string StepTitle => IsWorkspaceChange
        ? Strings.Instance["maintenanceWorkspaceTitle"]
        : Strings.Instance[StepTitleKey(Step)];

    public string StepSubtitle => Strings.Instance[StepSubtitleKey(Step)];

    public ObservableCollection<StepItem> Steps { get; } = [];

    private void BuildViews()
    {
        _views[WizardStep.Welcome] = new WelcomeView { DataContext = this };
        _views[WizardStep.Language] = new LanguageView { DataContext = this };
        _views[WizardStep.License] = new LicenseView { DataContext = this };
        _views[WizardStep.Components] = new ComponentsView { DataContext = this };
        _views[WizardStep.Paths] = new PathsView { DataContext = this };
        _views[WizardStep.Summary] = new SummaryView { DataContext = this };
        _views[WizardStep.Progress] = new ProgressView { DataContext = this };
        _views[WizardStep.Result] = new ResultView { DataContext = this };
        _views[WizardStep.Maintenance] = new MaintenanceView { DataContext = this };
        _views[WizardStep.Uninstall] = new UninstallView { DataContext = this };
    }

    public void GoTo(WizardStep step)
    {
        Step = step;
        CurrentView = _views[step];
        RebuildSteps();

        if (step == WizardStep.Summary)
        {
            StartPreflight();
        }

        if (step == WizardStep.Progress)
        {
            _ = RunOperationAsync();
        }

        RaiseButtons();
    }

    private void RebuildSteps()
    {
        var keys = _flow switch
        {
            FlowKind.Maintenance =>
                new[] { "stepWelcome", "stepMaintenance", "stepSummary", "stepProgress", "stepResult" },
            FlowKind.Uninstall =>
                new[] { "stepWelcome", "stepMaintenance", "stepUninstall", "stepProgress", "stepResult" },
            _ =>
                new[] { "stepWelcome", "stepLanguage", "stepLicense", "stepComponents", "stepPaths", "stepSummary", "stepProgress", "stepResult" },
        };

        var stepKeys = _flow switch
        {
            FlowKind.Maintenance => new[] { WizardStep.Welcome, WizardStep.Maintenance, WizardStep.Summary, WizardStep.Progress, WizardStep.Result },
            FlowKind.Uninstall => new[] { WizardStep.Welcome, WizardStep.Maintenance, WizardStep.Uninstall, WizardStep.Progress, WizardStep.Result },
            _ => new[] { WizardStep.Welcome, WizardStep.Language, WizardStep.License, WizardStep.Components, WizardStep.Paths, WizardStep.Summary, WizardStep.Progress, WizardStep.Result },
        };

        var currentIndex = Array.IndexOf(stepKeys, Step);
        Steps.Clear();
        for (var index = 0; index < keys.Length; index++)
        {
            Steps.Add(new StepItem
            {
                Index = index + 1,
                LabelKey = keys[index],
                IsCurrent = index == currentIndex,
                IsDone = currentIndex >= 0 && index < currentIndex,
            });
        }
    }

    private static string StepTitleKey(WizardStep step) => step switch
    {
        WizardStep.Welcome => "welcomeTitle",
        WizardStep.Language => "languageTitle",
        WizardStep.License => "licenseTitle",
        WizardStep.Components => "componentsTitle",
        WizardStep.Paths => "pathsTitle",
        WizardStep.Summary => "summaryTitle",
        WizardStep.Progress => "progressTitle",
        WizardStep.Result => "resultTitle",
        WizardStep.Maintenance => "maintenanceTitle",
        _ => "uninstallTitle",
    };

    private static string StepSubtitleKey(WizardStep step) => step switch
    {
        WizardStep.Welcome => "welcomeSubtitle",
        WizardStep.Language => "languageSubtitle",
        WizardStep.License => "licenseSubtitle",
        WizardStep.Components => "componentsSubtitle",
        WizardStep.Paths => "pathsSubtitle",
        WizardStep.Summary => "summarySubtitle",
        WizardStep.Progress => "progressSubtitle",
        WizardStep.Result => "resultSubtitle",
        WizardStep.Maintenance => "maintenanceSubtitle",
        _ => "uninstallSubtitle",
    };

    // ------------------------------------------------------------ composants

    public ObservableCollection<ComponentItem> CoreComponents { get; } = [];
    public ObservableCollection<ComponentItem> BaseComponents { get; } = [];
    public ObservableCollection<ComponentItem> OptionalComponents { get; } = [];

    private void BuildComponents(bool useInstalledSelection = false)
    {
        CoreComponents.Clear();
        BaseComponents.Clear();
        OptionalComponents.Clear();

        var installed = useInstalledSelection
            ? _session.InstalledComponentIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var descriptor in _session.Manifest.Components)
        {
            var item = new ComponentItem(
                descriptor, Strings.Instance.Language, OnComponentSelectionChanged,
                installed is null ? null : installed.Contains(descriptor.Id));
            if (descriptor.Mandatory)
            {
                CoreComponents.Add(item);
            }
            else if (!item.Optional)
            {
                BaseComponents.Add(item);
            }
            else
            {
                OptionalComponents.Add(item);
            }
        }

        OnPropertyChanged(nameof(PrismComponent));
        OnPropertyChanged(nameof(PrismWarningVisible));
        RefreshTotals();
    }

    public ComponentItem? PrismComponent => BaseComponents.FirstOrDefault(c => c.Id == "prism");

    public bool PrismWarningVisible => PrismComponent is { IsSelected: false };

    private void OnComponentSelectionChanged()
    {
        OnPropertyChanged(nameof(PrismWarningVisible));
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(PayloadSizeText));
        OnPropertyChanged(nameof(DownloadSizeText));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(HasOptionalModules));
    }

    public int SelectedCount => AllComponents.Count(c => c.IsSelected);

    public string SelectedCountText => Strings.Instance.Format("componentsSelected", SelectedCount);

    public string PayloadSizeText => Strings.Instance.Format("componentsPayload", SizeFormatter.Format(PayloadBytes));

    public string DownloadSizeText => Strings.Instance.Format("componentsDownload", SizeFormatter.Format(DownloadBytes));

    public long PayloadBytes => AllComponents.Where(c => c.IsSelected && c.AcquisitionKind != "release-asset")
        .SelectMany(c => c.Descriptor.Files).Sum(f => f.Size);

    public long DownloadBytes => AllComponents.Where(c => c.IsSelected)
        .Sum(c => c.Descriptor.Acquisition.Kind == "release-asset" ? c.Descriptor.Acquisition.Size ?? 0 : 0);

    public string SelectedSummary => string.Join(" · ", AllComponents.Where(c => c.IsSelected).Select(c => c.Name));

    public bool HasOptionalModules => OptionalComponents.Count > 0;

    private IEnumerable<ComponentItem> AllComponents => CoreComponents.Concat(BaseComponents).Concat(OptionalComponents);

    // ---------------------------------------------------------------- langue

    public bool IsFrench
    {
        get => Strings.Instance.Language == "fr";
        set { if (value) SelectLanguage("fr"); }
    }

    public bool IsEnglish
    {
        get => Strings.Instance.Language == "en";
        set { if (value) SelectLanguage("en"); }
    }

    private void SelectLanguage(string language)
    {
        Strings.Instance.SetLanguage(language);
        foreach (var component in AllComponents)
        {
            component.RefreshLanguage();
        }

        foreach (var step in Steps)
        {
            step.RefreshLanguage();
        }

        foreach (var stage in Stages)
        {
            stage.RefreshLanguage();
        }

        if (Step == WizardStep.Summary)
        {
            BuildSummaryRows();
        }

        RaiseLocalizedProperties();
    }

    private void RaiseLocalizedProperties()
    {
        OnPropertyChanged(nameof(IsFrench));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepSubtitle));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(PayloadSizeText));
        OnPropertyChanged(nameof(DownloadSizeText));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(PrismWarningVisible));
        OnPropertyChanged(nameof(ShortcutLabel));
        OnPropertyChanged(nameof(LaunchAfterLabel));
        OnPropertyChanged(nameof(AcceptLicenseLabel));
        OnPropertyChanged(nameof(ResultTitle));
        OnPropertyChanged(nameof(ResultBody));
        OnPropertyChanged(nameof(ResultWarningsTitle));
        OnPropertyChanged(nameof(PreflightTitle));
        OnPropertyChanged(nameof(PathsValidText));
        OnPropertyChanged(nameof(DemoBannerText));
        OnPropertyChanged(nameof(DemoScenarioLabel));
        OnPropertyChanged(nameof(WorkspaceSummaryText));
        OnPropertyChanged(nameof(InstalledSummaryText));
        OnPropertyChanged(nameof(MaintenanceIntroText));
        OnPropertyChanged(nameof(UninstallWorkspaceText));
        OnPropertyChanged(nameof(ProgressStatusText));
    }

    public string ShortcutLabel => Strings.Instance["pathsShortcut"];
    public string LaunchAfterLabel => Strings.Instance["pathsLaunchAfter"];
    public string AcceptLicenseLabel => Strings.Instance["licenseAccept"];
    public string ResultWarningsTitle => Strings.Instance["resultWarnings"];
    public string PreflightTitle => Strings.Instance["preflightTitle"];
    public string PathsValidText => Strings.Instance["pathsValid"];

    // -------------------------------------------------------------- licence

    private bool _acceptLicense;
    public bool AcceptLicense
    {
        get => _acceptLicense;
        set
        {
            if (SetField(ref _acceptLicense, value))
            {
                OnPropertyChanged(nameof(AcceptLicenseError));
                RefreshNavigation();
            }
        }
    }

    public bool AcceptLicenseError => !AcceptLicense;

    public string LicenseText { get; } = LoadLicenseText();

    private static string LoadLicenseText()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://livex-setup/Assets/LICENSE.txt"));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "GNU GENERAL PUBLIC LICENSE — Version 3, 29 June 2007\nhttps://www.gnu.org/licenses/gpl-3.0.txt";
        }
    }

    // ---------------------------------------------------------------- chemins

    private string _installDir = string.Empty;
    public string InstallDir
    {
        get => _installDir;
        set { if (SetField(ref _installDir, value)) ValidatePaths(); }
    }

    private string _workspaceDir = string.Empty;
    public string WorkspaceDir
    {
        get => _workspaceDir;
        set { if (SetField(ref _workspaceDir, value)) ValidatePaths(); }
    }

    public string DataRoot { get; }

    private string? _pathError;
    public string? PathError
    {
        get => _pathError;
        private set
        {
            if (SetField(ref _pathError, value))
            {
                OnPropertyChanged(nameof(HasPathError));
                OnPropertyChanged(nameof(IsPathsValid));
                RefreshNavigation();
            }
        }
    }

    public bool HasPathError => !string.IsNullOrEmpty(PathError);

    public bool IsPathsValid => string.IsNullOrEmpty(PathError);

    public string DiskAvailableText
    {
        get
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(InstallDir));
                if (string.IsNullOrEmpty(root))
                {
                    return string.Empty;
                }

                var drive = new DriveInfo(root);
                return drive.IsReady
                    ? Strings.Instance.Format("pathsDisk", SizeFormatter.Format(drive.AvailableFreeSpace))
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private void ValidatePaths()
    {
        OnPropertyChanged(nameof(DiskAvailableText));
        PathError = ComputePathError();
        _ = RefreshValidationAsync();
    }

    private string? ComputePathError()
    {
        var install = InstallDir?.Trim() ?? string.Empty;
        var workspace = WorkspaceDir?.Trim() ?? string.Empty;

        if (install.Length == 0 || workspace.Length == 0)
        {
            return Strings.Instance["errEmpty"];
        }

        try
        {
            var policy = Setup.Core.Platform.PlatformServices.CreatePathPolicy();
            var fullInstall = Path.GetFullPath(install);
            var fullWorkspace = Path.GetFullPath(workspace);
            var fullData = Path.GetFullPath(DataRoot);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.Equals(fullInstall, home, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.Instance["errHome"];
            }

            if (policy.IsForbiddenInstallDir(fullInstall))
            {
                return Strings.Instance["errForbidden"];
            }

            if (policy.Overlaps(fullInstall, fullWorkspace))
            {
                return Strings.Instance["errOverlap"];
            }

            if (string.Equals(fullWorkspace, fullData, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.Instance["errWorkspaceData"];
            }

            if (policy.Overlaps(fullWorkspace, fullData))
            {
                return Strings.Instance["errOverlap"];
            }

            return null;
        }
        catch (ArgumentException)
        {
            return Strings.Instance["errInvalid"];
        }
        catch (NotSupportedException)
        {
            return Strings.Instance["errInvalid"];
        }
    }

    private async Task RefreshValidationAsync()
    {
        // Rien d'autre à faire : la validation de droits appartient au préflight,
        // qui est le seul à tester l'écriture réelle (et à écrire).
        await Task.CompletedTask;
    }

    private bool _desktopShortcut;
    public bool DesktopShortcut
    {
        get => _desktopShortcut;
        set => SetField(ref _desktopShortcut, value);
    }

    private bool _launchAfterInstall = true;
    public bool LaunchAfterInstall
    {
        get => _launchAfterInstall;
        set => SetField(ref _launchAfterInstall, value);
    }

    public ICommand BrowseInstallDirCommand => _browseInstall ??= new RelayCommand(() => _ = BrowseAsync(false));
    public ICommand BrowseWorkspaceDirCommand => _browseWorkspace ??= new RelayCommand(() => _ = BrowseAsync(true));

    private RelayCommand? _browseInstall;
    private RelayCommand? _browseWorkspace;

    private async Task BrowseAsync(bool workspace)
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return;
        }

        var current = workspace ? WorkspaceDir : InstallDir;
        IStorageFolder? start = null;
        try
        {
            if (Directory.Exists(current))
            {
                start = await window.StorageProvider.TryGetFolderFromPathAsync(current);
            }
        }
        catch
        {
            // Chemin invalide : le sélecteur s'ouvre à l'emplacement par défaut.
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.Instance[workspace ? "pathsWorkspace" : "pathsInstallDir"],
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (workspace)
        {
            WorkspaceDir = path;
        }
        else
        {
            InstallDir = path;
        }
    }

    // -------------------------------------------------------------- résumé

    public ObservableCollection<SummaryRow> SummaryRows { get; } = [];

    public ObservableCollection<CheckItem> PreflightChecks { get; } = [];

    private bool _preflightRunning;
    public bool PreflightRunning
    {
        get => _preflightRunning;
        private set
        {
            if (SetField(ref _preflightRunning, value))
            {
                RefreshNavigation();
            }
        }
    }

    private bool _preflightFailed;
    public bool PreflightFailed
    {
        get => _preflightFailed;
        private set
        {
            if (SetField(ref _preflightFailed, value))
            {
                RefreshNavigation();
            }
        }
    }

    private async void StartPreflight()
    {
        BuildSummaryRows();
        PreflightChecks.Clear();
        PreflightFailed = false;
        PreflightRunning = true;

        try
        {
            var preflight = await _session.PreflightAsync(BuildRequest());
            foreach (var check in preflight.Checks)
            {
                PreflightChecks.Add(new CheckItem(CheckLabel(check.Name), check.Status, check.Message));
            }

            PreflightFailed = !preflight.Success;
        }
        catch (Exception ex)
        {
            PreflightChecks.Add(new CheckItem(Strings.Instance["preflightTitle"], "Fail", ex.Message));
            PreflightFailed = true;
        }
        finally
        {
            PreflightRunning = false;
            RefreshNavigation();
        }
    }

    private static string CheckLabel(string name) => Strings.Instance[name switch
    {
        "platform" => "checkPlatform",
        "path-policy" => "checkPathPolicy",
        "writable" => "checkWritable",
        "disk" => "checkDisk",
        "process" => "checkProcess",
        "prerequisites" => "checkPrerequisites",
        "manifest" => "checkManifest",
        "overlap" or "workspace" => "checkPathPolicy",
        _ => "checkManifest",
    }];

    private void BuildSummaryRows()
    {
        SummaryRows.Clear();

        if (IsWorkspaceChange)
        {
            SummaryRows.Add(new SummaryRow(Strings.Instance["summaryOperation"],
                Strings.Instance["summaryOperationWorkspace"]));
            SummaryRows.Add(new SummaryRow(Strings.Instance["summaryWorkspace"], WorkspaceDir, Mono: true));
            SummaryRows.Add(new SummaryRow(Strings.Instance["summaryDataRoot"], DataRoot, Mono: true));
            SummaryRows.Add(new SummaryRow(Strings.Instance["summaryWorkspaceKeepLabel"],
                Strings.Instance["summaryWorkspaceKeep"], Warning: true));
            return;
        }

        var operation = _flow == FlowKind.Maintenance
            ? Strings.Instance["summaryOperationMaintenance"]
            : Strings.Instance["summaryOperationInstall"];

        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryOperation"], operation));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryProductVersion"], _session.Manifest.Version));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryInstallDir"], InstallDir, Mono: true));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryWorkspace"], WorkspaceDir, Mono: true));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryDataRoot"], DataRoot, Mono: true));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryComponents"], SelectedSummary));
        SummaryRows.Add(new SummaryRow(Strings.Instance["summaryPayload"], SizeFormatter.Format(PayloadBytes)));

        if (DownloadBytes > 0)
        {
            SummaryRows.Add(new SummaryRow(Strings.Instance["summaryDownload"], SizeFormatter.Format(DownloadBytes)));
        }

        if (DesktopShortcut)
        {
            SummaryRows.Add(new SummaryRow(Strings.Instance["pathsShortcut"], "✓"));
        }

        if (PrismWarningVisible)
        {
            SummaryRows.Add(new SummaryRow(Strings.Instance["componentsTitle"], Strings.Instance["summaryWarningPrism"], Warning: true));
        }
    }

    // ------------------------------------------------------------ progression

    public ObservableCollection<StageItem> Stages { get; } = [];

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (SetField(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }
    }

    public string ProgressPercentText => $"{ProgressPercent} %";

    private string _progressStatus = string.Empty;
    public string ProgressStatusText
    {
        get => _progressStatus;
        private set => SetField(ref _progressStatus, value);
    }

    private string _progressStepText = string.Empty;
    public string ProgressStepText
    {
        get => _progressStepText;
        private set => SetField(ref _progressStepText, value);
    }

    private bool _canCancel = true;
    public bool CanCancel
    {
        get => _canCancel;
        private set => SetField(ref _canCancel, value);
    }

    private void BuildStages(InstallPlan plan)
    {
        Stages.Clear();

        var keys = new List<string> { "stagePlan" };
        foreach (var action in plan.Actions)
        {
            var key = StageKeyForAction(action);
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        if (!keys.Contains("stageFinalize"))
        {
            keys.Add("stageFinalize");
        }

        foreach (var key in keys)
        {
            Stages.Add(new StageItem(key, Strings.Instance[key]));
        }
    }

    private static string StageKeyForAction(PlanAction action) => action switch
    {
        CreateDirectoryAction => "stageDirectories",
        WriteFileAction or ReplaceFileAction or DeleteFileAction => "stageCopy",
        DownloadAssetAction => "stageDownload",
        ExtractArchiveAction => "stageExtract",
        MergeSettingsAction => "stageSettings",
        WriteRegistrationAction => "stageRegister",
        CreateShortcutAction => "stageShortcuts",
        _ => "stageFinalize",
    };

    private void UpdateStages(string actionTypeName, int percent)
    {
        if (Stages.Count == 0)
        {
            return;
        }

        var index = 0;
        for (var i = 0; i < Stages.Count; i++)
        {
            if (Stages[i].LabelKey == StageKeyForActionName(actionTypeName))
            {
                index = i;
                break;
            }
        }

        // Les étapes franchies (ou sautées) sont marquées terminées.
        for (var i = 0; i < Stages.Count; i++)
        {
            Stages[i].State = i < index ? StageState.Done : i == index ? StageState.Running : StageState.Pending;
        }

        if (percent >= 100)
        {
            foreach (var stage in Stages)
            {
                stage.State = StageState.Done;
            }
        }
    }

    private static string StageKeyForActionName(string step) => step switch
    {
        // Clés stables émises par Setup.Core (PlanActionLabel).
        "create-directory" => "stageDirectories",
        "write-file" or "replace-file" or "delete-file" or "delete-directory"
            or "remove-shortcuts" or "remove-association" or "unregister"
            or "remove-settings" or "inventory" => "stageCopy",
        "download" => "stageDownload",
        "extract" => "stageExtract",
        "settings" => "stageSettings",
        "register" => "stageRegister",
        "shortcut" or "associate-package" => "stageShortcuts",
        // Repli : noms de types d'action (tests, anciens moteurs).
        nameof(CreateDirectoryAction) => "stageDirectories",
        nameof(WriteFileAction) or nameof(ReplaceFileAction) or nameof(DeleteFileAction) => "stageCopy",
        nameof(DownloadAssetAction) => "stageDownload",
        nameof(ExtractArchiveAction) => "stageExtract",
        nameof(MergeSettingsAction) => "stageSettings",
        nameof(WriteRegistrationAction) => "stageRegister",
        nameof(CreateShortcutAction) => "stageShortcuts",
        _ => "stageFinalize",
    };

    // ---------------------------------------------------------------- résultat

    public ResultKind ResultKind => _resultKind;

    public bool IsSuccess => _resultKind == ResultKind.Success;
    public bool IsWarning => _resultKind == ResultKind.Warning;
    public bool IsFailure => _resultKind == ResultKind.Failure;

    public string ResultTitle => _resultTitle.Length > 0 ? _resultTitle : Strings.Instance["resultTitle"];

    public string ResultBody => _resultBody;

    public string ResultLog => string.IsNullOrEmpty(_resultLog)
        ? string.Empty
        : Strings.Instance.Format("resultLog", _resultLog);

    public string ResultResiduals => string.IsNullOrEmpty(_resultResiduals)
        ? string.Empty
        : Strings.Instance.Format("resultResiduals", _resultResiduals);

    public ObservableCollection<string> ResultWarnings { get; } = [];

    public bool HasResultWarnings => ResultWarnings.Count > 0;

    public bool HasLog => ResultLog.Length > 0;

    public bool HasResiduals => ResultResiduals.Length > 0;

    public bool ShowLaunch => IsSuccess && !_session.IsDemo && LaunchAfterInstall;

    public bool ShowOpenFolder => IsSuccess && !_session.IsDemo;

    private void SetResult(ResultKind kind, string title, string body, ExecutionResult? execution = null)
    {
        _resultKind = kind;
        _resultTitle = title;
        _resultBody = body;
        _resultLog = execution?.LogPath ?? string.Empty;
        _resultResiduals = execution is null ? string.Empty : string.Join(" · ", execution.Residuals);

        ResultWarnings.Clear();
        if (execution is not null)
        {
            foreach (var warning in execution.Warnings)
            {
                ResultWarnings.Add(warning);
            }
        }

        OnPropertyChanged(nameof(ResultKind));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsFailure));
        OnPropertyChanged(nameof(ResultTitle));
        OnPropertyChanged(nameof(ResultBody));
        OnPropertyChanged(nameof(ResultLog));
        OnPropertyChanged(nameof(ResultResiduals));
        OnPropertyChanged(nameof(HasResultWarnings));
        OnPropertyChanged(nameof(HasLog));
        OnPropertyChanged(nameof(HasResiduals));
        OnPropertyChanged(nameof(ShowLaunch));
        OnPropertyChanged(nameof(ShowOpenFolder));
    }

    // --------------------------------------------------------------- exécution

    private InstallRequest BuildRequest() => new(
        _session.Manifest,
        InstallDir,
        DataRoot,
        WorkspaceDir,
        AllComponents.Where(c => c.IsSelected).Select(c => c.Id).ToList(),
        AcceptLicense,
        DesktopShortcut,
        LaunchAfterInstall,
        DryRun: _session.IsDemo,
        Simulated: _session.IsDemo,
        OfflineAssetsDir: _options.Core.OfflineAssetsDir,
        NoNetwork: _options.NoNetwork,
        PayloadRoot: Directory.Exists(Path.Combine(AppContext.BaseDirectory, "payload"))
            ? Path.Combine(AppContext.BaseDirectory, "payload")
            : null,
        Scenario: _session.Scenario);

    private async Task RunOperationAsync()
    {
        if (_operationRunning)
        {
            return;
        }

        _operationRunning = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        CanCancel = true;
        ProgressPercent = 0;
        ProgressStepText = string.Empty;
        ProgressStatusText = Strings.Instance["progressPreparing"];

        try
        {
            var request = BuildRequest();

            if (_flow == FlowKind.Uninstall)
            {
                BuildUninstallStages();
                var result = await _session.ExecuteUninstallAsync(
                    _uninstallScope, PurgeResearch, ProgressReporter(), token);
                ApplyResult(result, Strings.Instance["resultUninstallSuccess"],
                    Strings.Instance["resultUninstallBody"]);
            }
            else if (_flow == FlowKind.Maintenance)
            {
                IReadOnlyList<string>? add = null;
                IReadOnlyList<string>? remove = null;

                if (_maintenanceAction == MaintenanceActionKind.ManageComponents)
                {
                    var installed = _session.InstalledComponentIds
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    add = AllComponents
                        .Where(c => c.IsSelected && !installed.Contains(c.Id))
                        .Select(c => c.Id).ToList();
                    remove = AllComponents
                        .Where(c => !c.IsSelected && !c.Required && installed.Contains(c.Id))
                        .Select(c => c.Id).ToList();
                }

                BuildMaintenanceStages();
                var result = await _session.ExecuteMaintenanceAsync(
                    _maintenanceAction, request, add, remove, ProgressReporter(), token);
                ApplyResult(result, Strings.Instance["resultMaintenanceSuccess"],
                    Strings.Instance["resultMaintenanceBody"]);
            }
            else
            {
                _plan = await _session.PlanAsync(request, token);
                BuildStages(_plan);
                var result = await _session.ExecuteAsync(_plan, request, ProgressReporter(), token);
                ApplyInstallResult(result);
            }
        }
        catch (OperationCanceledException)
        {
            SetResult(ResultKind.Failure, Strings.Instance["resultCancelTitle"],
                Strings.Instance["resultCancelBody"]);
            GoTo(WizardStep.Result);
            return;
        }
        catch (Exception ex)
        {
            SetResult(ResultKind.Failure, Strings.Instance["resultFailTitle"],
                Strings.Instance.Format("resultFailBody", ex.Message));
            GoTo(WizardStep.Result);
            return;
        }
        finally
        {
            _operationRunning = false;
            _cts?.Dispose();
            _cts = null;
        }

        GoTo(WizardStep.Result);
    }

    private IProgress<ProgressReport> ProgressReporter() => new Progress<ProgressReport>(report =>
    {
        ProgressPercent = report.Total > 0 ? (int)Math.Round(100.0 * report.Current / report.Total) : 0;
        ProgressStepText = Strings.Instance.Format("progressStepOf", report.Current, report.Total);
        var actionKey = report.Kind ?? report.Step;
        var stage = Strings.Instance[StageKeyForActionName(actionKey)];
        ProgressStatusText = report.ComponentId is null ? stage : $"{stage} — {report.ComponentId}";
        UpdateStages(actionKey, ProgressPercent);
    });

    private void BuildUninstallStages()
    {
        Stages.Clear();
        foreach (var key in new[] { "stagePlan", "stageCopy", "stageSettings", "stageRegister", "stageFinalize" })
        {
            Stages.Add(new StageItem(key, Strings.Instance[key]));
        }
    }

    private void BuildMaintenanceStages()
    {
        Stages.Clear();
        var keys = _maintenanceAction switch
        {
            MaintenanceActionKind.Update => new[] { "stagePlan", "stageCopy", "stageSettings", "stageRegister", "stageFinalize" },
            MaintenanceActionKind.Repair => new[] { "stagePlan", "stageVerify", "stageCopy", "stageFinalize" },
            MaintenanceActionKind.ChangeWorkspace => new[] { "stagePlan", "stageSettings", "stageFinalize" },
            _ => new[] { "stagePlan", "stageDownload", "stageExtract", "stageRegister", "stageFinalize" },
        };

        foreach (var key in keys)
        {
            Stages.Add(new StageItem(key, Strings.Instance[key]));
        }
    }

    private void ApplyInstallResult(ExecutionResult result)
    {
        if (!result.Success)
        {
            SetResult(ResultKind.Failure, Strings.Instance["resultFailTitle"],
                Strings.Instance.Format("resultFailBody",
                    result.Residuals.Count > 0 ? result.Residuals[0] : Strings.Instance["resultFailTitle"]),
                result);
            return;
        }

        var kind = result.Warnings.Count > 0 ? ResultKind.Warning : ResultKind.Success;
        var title = _session.IsDemo
            ? Strings.Instance["resultDemoTitle"]
            : kind == ResultKind.Warning
                ? Strings.Instance["resultWarnTitle"]
                : Strings.Instance["resultSuccessTitle"];
        var body = _session.IsDemo
            ? Strings.Instance["resultDemoBody"]
            : Strings.Instance.Format("resultSuccessBody", _session.Manifest.Version, InstallDir);

        SetResult(kind, title, body, result);
    }

    private void ApplyResult(ExecutionResult result, string successTitle, string successBody)
    {
        if (!result.Success)
        {
            SetResult(ResultKind.Failure, Strings.Instance["resultFailTitle"],
                Strings.Instance.Format("resultFailBody",
                    result.Residuals.Count > 0 ? result.Residuals[0] : Strings.Instance["resultFailTitle"]),
                result);
            return;
        }

        var kind = result.Warnings.Count > 0 ? ResultKind.Warning : ResultKind.Success;
        SetResult(kind, successTitle, successBody, result);
    }

    // ------------------------------------------------------------ maintenance

    public bool IsMaintenanceMode => _flow == FlowKind.Maintenance;

    public string MaintenanceVersion => _session.Detected?.Version ?? _session.Manifest.Version;

    public string InstalledVersion => MaintenanceVersion;

    public string InstalledOnText => _session.Detected is { } record
        ? Strings.Instance.Format("maintenanceInstalledOn", record.InstalledAt.ToLocalTime().ToString("d"))
        : string.Empty;

    public string InstalledSummaryText => Strings.Instance.Format(
        "maintenanceDetected", InstalledVersion, InstallDir);

    public string WorkspaceSummaryText => Strings.Instance.Format("maintenanceWorkspace", WorkspaceDir);

    public string MaintenanceIntroText => Strings.Instance["maintenanceIntro"];

    public string UpdateBodyText => Strings.Instance.Format("maintenanceUpdateBody", _session.Manifest.Version);

    public ICommand UpdateCommand => _updateCommand ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Maintenance;
        _maintenanceAction = MaintenanceActionKind.Update;
        AcceptLicense = true;
        GoTo(WizardStep.Summary);
    });

    public ICommand RepairCommand => _repairCommand ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Maintenance;
        _maintenanceAction = MaintenanceActionKind.Repair;
        AcceptLicense = true;
        GoTo(WizardStep.Summary);
    });

    public ICommand ManageComponentsCommand => _manageCommand ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Maintenance;
        _maintenanceAction = MaintenanceActionKind.ManageComponents;
        AcceptLicense = true;
        BuildComponents(useInstalledSelection: true);
        GoTo(WizardStep.Components);
    });

    public ICommand ChangeWorkspaceCommand => _changeWorkspaceCommand ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Maintenance;
        _maintenanceAction = MaintenanceActionKind.ChangeWorkspace;
        AcceptLicense = true;
        OnPropertyChanged(nameof(IsWorkspaceChange));
        OnPropertyChanged(nameof(IsInstallPathVisible));
        OnPropertyChanged(nameof(StepTitle));
        GoTo(WizardStep.Paths);
    });

    private RelayCommand? _changeWorkspaceCommand;

    /// <summary>Maintenance limitée au déplacement de l'espace de travail.</summary>
    public bool IsWorkspaceChange => _flow == FlowKind.Maintenance
        && _maintenanceAction == MaintenanceActionKind.ChangeWorkspace;

    public bool IsInstallPathVisible => !IsWorkspaceChange;

    public ICommand UninstallCommand => _uninstallCommand ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Uninstall;
        GoTo(WizardStep.Uninstall);
    });

    private RelayCommand? _updateCommand;
    private RelayCommand? _repairCommand;
    private RelayCommand? _manageCommand;
    private RelayCommand? _uninstallCommand;

    public ICommand OpenMaintenanceCommand => _openMaintenance ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Maintenance;
        GoTo(WizardStep.Maintenance);
    });

    private RelayCommand? _openMaintenance;

    // -------------------------------------------------------- désinstallation

    public bool IsScopeA
    {
        get => _uninstallScope == UninstallScope.Software;
        set { if (value) SetUninstallScope(UninstallScope.Software); }
    }

    public bool IsScopeB
    {
        get => _uninstallScope == UninstallScope.SoftwareAndConfig;
        set { if (value) SetUninstallScope(UninstallScope.SoftwareAndConfig); }
    }

    public bool IsScopeC
    {
        get => _uninstallScope == UninstallScope.SoftwareConfigAndWorkspace;
        set { if (value) SetUninstallScope(UninstallScope.SoftwareConfigAndWorkspace); }
    }

    private void SetUninstallScope(UninstallScope scope)
    {
        _uninstallScope = scope;
        if (scope != UninstallScope.SoftwareConfigAndWorkspace)
        {
            PurgeResearch = false;
        }

        OnPropertyChanged(nameof(IsScopeA));
        OnPropertyChanged(nameof(IsScopeB));
        OnPropertyChanged(nameof(IsScopeC));
        OnPropertyChanged(nameof(IsPurgeAvailable));
        RefreshNavigation();
        UninstallConfirmed = false;
    }

    public bool IsPurgeAvailable => _uninstallScope == UninstallScope.SoftwareConfigAndWorkspace;

    private bool _purgeResearch;
    public bool PurgeResearch
    {
        get => _purgeResearch;
        set => SetField(ref _purgeResearch, value);
    }

    private bool _uninstallConfirmed;
    public bool UninstallConfirmed
    {
        get => _uninstallConfirmed;
        set
        {
            if (SetField(ref _uninstallConfirmed, value))
            {
                RefreshNavigation();
            }
        }
    }

    public bool CanConfirmUninstall => UninstallConfirmed;

    public string UninstallWorkspaceText
    {
        get
        {
            if (_session.IsDemo)
            {
                return Strings.Instance.Format("uninstallWorkspaceSummary",
                    WorkspaceDir, DemoWorkspaceFileCount, SizeFormatter.Format(DemoWorkspaceSizeBytes));
            }

            try
            {
                if (!Directory.Exists(WorkspaceDir))
                {
                    return string.Empty;
                }

                var files = Directory.EnumerateFiles(WorkspaceDir, "*", SearchOption.AllDirectories).Count();
                return Strings.Instance.Format("uninstallWorkspaceSummary", WorkspaceDir, files, "—");
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    // ------------------------------------------------------------------ démo

    public bool IsDemo => _session.IsDemo;

    private const int DemoWorkspaceFileCount = 1_284;
    private const long DemoWorkspaceSizeBytes = 7_982_284_800;

    public string DemoBannerText => Strings.Instance["demoBanner"];

    public string DemoScenarioLabel => Strings.Instance[_session.Scenario switch
    {
        DemoScenario.ExistingInstall => "demoScenarioExisting",
        DemoScenario.FailureDuringWrite => "demoScenarioFailure",
        DemoScenario.PrismAbsent => "demoScenarioPrism",
        _ => "demoScenarioFresh",
    }];

    public bool IsDemoFresh => _session.Scenario == DemoScenario.FreshInstall;
    public bool IsDemoExisting => _session.Scenario == DemoScenario.ExistingInstall;
    public bool IsDemoFailure => _session.Scenario == DemoScenario.FailureDuringWrite;
    public bool IsDemoPrismAbsent => _session.Scenario == DemoScenario.PrismAbsent;

    public ICommand DemoScenarioCommand => _demoScenarioCommand ??= new RelayCommand<string>(scenario =>
    {
        var target = scenario switch
        {
            "existing" => DemoScenario.ExistingInstall,
            "failure" => DemoScenario.FailureDuringWrite,
            "prism" => DemoScenario.PrismAbsent,
            _ => DemoScenario.FreshInstall,
        };

        _session.SetDemoScenario(target);
        ResetForDemo();
    });

    /// <summary>Vrai quand une distribution réelle est chargée : on peut basculer en monde fictif.</summary>
    public bool CanEnableDemo => !_session.IsDemo;

    public ICommand EnableDemoCommand => _enableDemoCommand ??= new RelayCommand(() =>
    {
        if (_session.IsDemo)
        {
            return;
        }

        _session.EnterDemo(DemoScenario.FreshInstall);
        ResetForDemo();
    });

    private RelayCommand? _enableDemoCommand;

    /// <summary>Remet l'assistant dans un état de départ propre après un changement de monde fictif.</summary>
    private void ResetForDemo()
    {
        _flow = FlowKind.Install;
        _resultKind = ResultKind.Success;
        _resultTitle = string.Empty;
        _resultBody = string.Empty;
        _resultLog = string.Empty;
        _resultResiduals = string.Empty;
        ResultWarnings.Clear();
        AcceptLicense = false;
        BuildComponents();

        OnPropertyChanged(nameof(IsDemo));
        OnPropertyChanged(nameof(CanEnableDemo));
        OnPropertyChanged(nameof(DemoBannerText));
        OnPropertyChanged(nameof(DemoScenarioLabel));
        OnPropertyChanged(nameof(IsDemoFresh));
        OnPropertyChanged(nameof(IsDemoExisting));
        OnPropertyChanged(nameof(IsDemoFailure));
        OnPropertyChanged(nameof(IsDemoPrismAbsent));
        OnPropertyChanged(nameof(Manifest));
        GoTo(WizardStep.Welcome);
        _ = InitializeAsync();
    }

    private RelayCommand<string>? _demoScenarioCommand;

    // ------------------------------------------------------------- détection

    private bool _isInstalled;
    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (SetField(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(HasExistingInstallation));
                OnPropertyChanged(nameof(DetectedInstallText));
            }
        }
    }

    public bool HasExistingInstallation => IsInstalled;

    public string DetectedInstallText => Strings.Instance.Format(
        "welcomeDetectedBody", InstalledVersion, _detectionDir ?? InstallDir);

    private string? _detectionDir;

    private async Task InitializeAsync()
    {
        try
        {
            var detection = await _session.DetectAsync();
            _detectionDir = detection.InstallDir;
            IsInstalled = detection.IsInstalled && detection.IsValid;
        }
        catch
        {
            IsInstalled = false;
        }
    }

    // ------------------------------------------------------------------ boutons

    public bool ShowBack => Step is WizardStep.Language or WizardStep.License or WizardStep.Components
        or WizardStep.Paths or WizardStep.Summary or WizardStep.Maintenance or WizardStep.Uninstall;

    public bool ShowNext => Step is WizardStep.Welcome or WizardStep.Language or WizardStep.License
        or WizardStep.Components or WizardStep.Paths;

    public bool ShowInstall => Step == WizardStep.Summary;

    public bool ShowFinish => Step == WizardStep.Result;

    public bool ShowCancel => Step == WizardStep.Progress;

    public bool ShowQuit => Step == WizardStep.Welcome;

    public bool ShowUninstallConfirm => Step == WizardStep.Uninstall;

    public bool CanGoBack => ShowBack;

    public bool CanGoNext => ShowNext;

    public bool CanGoNextEnabled => Step switch
    {
        WizardStep.License => AcceptLicense,
        WizardStep.Paths => IsPathsValid,
        _ => true,
    };

    public bool CanInstallEnabled => AcceptLicense && !PreflightFailed && !PreflightRunning;

    public bool CanCancelEnabled => CanCancel;

    public string InstallButtonLabel => _flow == FlowKind.Maintenance
        ? Strings.Instance[_maintenanceAction switch
        {
            MaintenanceActionKind.Repair => "btnRepair",
            MaintenanceActionKind.ManageComponents => "btnApply",
            _ => "btnUpdate",
        }]
        : Strings.Instance["btnInstall"];

    private RelayCommand? _back;
    private RelayCommand? _next;
    private RelayCommand? _install;
    private RelayCommand? _finish;
    private RelayCommand? _cancel;
    private RelayCommand? _quit;
    private RelayCommand? _confirmUninstall;
    private RelayCommand? _retry;

    public ICommand BackCommand => _back ??= new RelayCommand(() => GoTo(PreviousStep()), () => CanGoBack);

    public ICommand NextCommand => _next ??= new RelayCommand(() => GoTo(NextStep()), () => CanGoNext && CanGoNextEnabled);

    public ICommand InstallCommand => _install ??= new RelayCommand(() => GoTo(WizardStep.Progress), () => CanInstallEnabled);

    public ICommand FinishCommand => _finish ??= new RelayCommand(() => App.MainWindow?.Close(), () => ShowFinish);

    public ICommand CancelCommand => _cancel ??= new RelayCommand(() =>
    {
        CanCancel = false;
        ProgressStatusText = Strings.Instance["progressCancelling"];
        _cts?.Cancel();
    }, () => CanCancelEnabled);

    public ICommand QuitCommand => _quit ??= new RelayCommand(() => App.MainWindow?.Close());

    public ICommand ConfirmUninstallCommand => _confirmUninstall ??= new RelayCommand(() =>
    {
        GoTo(WizardStep.Progress);
    }, () => CanConfirmUninstall);

    public ICommand RetryCommand => _retry ??= new RelayCommand(() =>
    {
        _flow = FlowKind.Install;
        _maintenanceAction = MaintenanceActionKind.Update;
        AcceptLicense = false;
        _resultKind = ResultKind.Success;
        _resultTitle = string.Empty;
        _resultBody = string.Empty;
        _resultLog = string.Empty;
        _resultResiduals = string.Empty;
        ResultWarnings.Clear();
        GoTo(WizardStep.Welcome);
    });

    public ICommand OpenFolderCommand => new RelayCommand(() =>
    {
        try
        {
            if (Directory.Exists(InstallDir))
            {
                Process.Start(new ProcessStartInfo(InstallDir) { UseShellExecute = true });
            }
        }
        catch
        {
            // Ouverture best effort : aucune erreur à afficher sur un résultat réussi.
        }
    });

    public ICommand LaunchCommand => new RelayCommand(() =>
    {
        try
        {
            var exe = Path.Combine(InstallDir, OperatingSystem.IsWindows() ? "livex-launcher.exe" : "livex-launcher");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
        }
        catch
        {
            // Lancement best effort.
        }
    });

    private WizardStep NextStep() => Step switch
    {
        WizardStep.Welcome => WizardStep.Language,
        WizardStep.Language => WizardStep.License,
        WizardStep.License => WizardStep.Components,
        WizardStep.Components => _flow == FlowKind.Maintenance ? WizardStep.Summary : WizardStep.Paths,
        WizardStep.Paths => WizardStep.Summary,
        _ => Step,
    };

    private WizardStep PreviousStep() => Step switch
    {
        WizardStep.Language => WizardStep.Welcome,
        WizardStep.License => WizardStep.Language,
        WizardStep.Components => _flow == FlowKind.Maintenance ? WizardStep.Maintenance : WizardStep.License,
        WizardStep.Paths => WizardStep.Components,
        WizardStep.Summary => _flow == FlowKind.Maintenance ? WizardStep.Maintenance : WizardStep.Paths,
        WizardStep.Maintenance => WizardStep.Welcome,
        WizardStep.Uninstall => WizardStep.Maintenance,
        _ => Step,
    };

    /// <summary>
    /// Les boutons Suivant, Installer et Désinstaller dépendent d'un état
    /// calculé (licence acceptée, chemins valides, préflight, confirmation de
    /// désinstallation). Un changement de cet état doit rafraîchir la propriété
    /// liée à <c>IsEnabled</c> ET la commande : sans le second, le bouton reste
    /// grisé même quand la condition est remplie.
    /// </summary>
    private void RefreshNavigation()
    {
        OnPropertyChanged(nameof(CanGoNextEnabled));
        OnPropertyChanged(nameof(CanInstallEnabled));
        OnPropertyChanged(nameof(CanConfirmUninstall));
        _next?.RaiseCanExecuteChanged();
        _install?.RaiseCanExecuteChanged();
        _confirmUninstall?.RaiseCanExecuteChanged();
    }

    private void RaiseButtons()
    {
        foreach (var command in new RelayCommand?[] { _back, _next, _install, _finish, _cancel, _quit, _confirmUninstall, _retry })
        {
            command?.RaiseCanExecuteChanged();
        }

        OnPropertyChanged(nameof(ShowBack));
        OnPropertyChanged(nameof(ShowNext));
        OnPropertyChanged(nameof(ShowInstall));
        OnPropertyChanged(nameof(ShowFinish));
        OnPropertyChanged(nameof(ShowCancel));
        OnPropertyChanged(nameof(ShowQuit));
        OnPropertyChanged(nameof(ShowUninstallConfirm));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoNextEnabled));
        OnPropertyChanged(nameof(CanInstallEnabled));
        OnPropertyChanged(nameof(CanCancelEnabled));
        OnPropertyChanged(nameof(InstallButtonLabel));
        OnPropertyChanged(nameof(IsMaintenanceMode));
        OnPropertyChanged(nameof(WorkspaceSummaryText));
        OnPropertyChanged(nameof(InstalledSummaryText));
        OnPropertyChanged(nameof(MaintenanceIntroText));
        OnPropertyChanged(nameof(UpdateBodyText));
        OnPropertyChanged(nameof(UninstallWorkspaceText));
        OnPropertyChanged(nameof(InstalledOnText));
    }
}
