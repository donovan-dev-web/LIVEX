using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Launcher.Presentation.ViewModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Launcher.App.Views;

/// <summary>Fenêtre principale du Launcher (GUI.md : grille 1536 × 1024, trois colonnes).</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initialise la fenêtre. Toute la logique de vue vit dans le ViewModel.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsInteractiveTitleBarSource(e.Source))
        {
            return;
        }

        e.Handled = true;
        BeginMoveDrag(e);
    }

    private static bool IsInteractiveTitleBarSource(object? source)
    {
        for (var current = source as Control; current is not null; current = current.GetVisualParent() as Control)
        {
            if (current is Button or ComboBox)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Démarre un redimensionnement par une poignée d'arête ou de coin.
    /// SystemDecorations=None retire le cadre natif : BeginResizeDrag (WindowEdge)
    /// demande à la plateforme de prendre le relais, comme pour BeginMoveDrag.
    /// </summary>
    private void OnResizeHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || sender is not Control { Tag: string edgeName })
        {
            return;
        }

        // Double-clic sur le bord supérieur : agrandir/restaurer, comme une fenêtre native.
        if (e.ClickCount == 2 && edgeName == "North")
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        if (Enum.TryParse<WindowEdge>(edgeName, out var edge))
        {
            e.Handled = true;
            BeginResizeDrag(edge, e);
        }
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void OnOpenPackageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Ouvrir une expérience LIVEX",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Paquet LIVEX") { Patterns = ["*.livexp"] }],
            });
            if (files.Count == 0)
            {
                return;
            }

            var path = files[0].TryGetLocalPath();
            if (path is null)
            {
                viewModel.SetReportStatus("Le paquet doit être accessible comme fichier local.");
                return;
            }

            viewModel.OpenPackage(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetReportStatus($"Ouverture du paquet impossible : {exception.Message}");
        }
    }

    private async void OnSaveReportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { EmergenceReport.Length: > 0 } viewModel)
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Enregistrer le rapport ECHOS",
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(viewModel.OpenedPackagePath)}-rapport.md",
                FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
            });
            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath();
            if (path is null)
            {
                viewModel.SetReportStatus("La destination doit être un fichier local.");
                return;
            }

            await File.WriteAllTextAsync(path, viewModel.EmergenceReport, new UTF8Encoding(false));
            viewModel.SetReportStatus($"Rapport ECHOS enregistré sans modification : {path}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetReportStatus($"Enregistrement du rapport impossible : {exception.Message}");
        }
    }

    private async void OnExportLogsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exporter les journaux de session",
                SuggestedFileName = "livex-session.ndjson",
                FileTypeChoices = [new FilePickerFileType("Journal NDJSON") { Patterns = ["*.ndjson"] }],
            });
            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath();
            if (path is null)
            {
                viewModel.SetLogStatus("La destination doit être un fichier local.");
                return;
            }

            await viewModel.ExportSessionLogsAsync(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetLogStatus($"Export des journaux impossible : {exception.Message}");
        }
    }

    private void OnOpenLogsDirectoryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            if (!Directory.Exists(viewModel.SessionLogDirectory))
            {
                viewModel.SetLogStatus("Le répertoire des journaux n'existe pas encore.");
                return;
            }

            Process.Start(new ProcessStartInfo(viewModel.SessionLogDirectory) { UseShellExecute = true });
            viewModel.SetLogStatus($"Emplacement des journaux : {viewModel.SessionLogDirectory}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            viewModel.SetLogStatus($"Ouverture de l'emplacement impossible : {exception.Message}");
        }
    }

    private async void OnOpenRunLogsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Ouvrir les journaux d'un paquet LIVEX",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Paquet LIVEX") { Patterns = ["*.livexp"] }],
            });
            if (files.Count == 0)
            {
                return;
            }

            var path = files[0].TryGetLocalPath();
            if (path is null)
            {
                viewModel.SetLogStatus("Le paquet doit être accessible comme fichier local.");
                return;
            }

            viewModel.LoadPackageRunLogs(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetLogStatus($"Ouverture des journaux du paquet impossible : {exception.Message}");
        }
    }

    private async void OnExportRunLogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.SelectedRunLog is null)
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exporter le journal de run",
                SuggestedFileName = SanitizeFileName($"{viewModel.SelectedRunLog.RunId}-{viewModel.SelectedRunLog.Name}"),
                FileTypeChoices = [new FilePickerFileType("Journal texte") { Patterns = ["*.log", "*.txt"] }],
            });
            var path = file?.TryGetLocalPath();
            if (path is not null)
            {
                await viewModel.ExportSelectedRunLogAsync(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetLogStatus($"Export du journal du run impossible : {exception.Message}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().Concat(['/', '\\']).ToHashSet();
        return string.Concat(fileName.Select(character => invalidCharacters.Contains(character) ? '_' : character));
    }

    private async void OnAddComponentInstallationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Sélectionner le dossier contenant component.json",
                AllowMultiple = false,
            });
            if (folders.Count == 0)
            {
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (path is null)
            {
                viewModel.SetLogStatus("Le dossier doit être accessible localement.");
                return;
            }

            viewModel.AddComponentInstallation(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            viewModel.SetLogStatus($"Ajout du composant impossible : {exception.Message}");
        }
    }
}
