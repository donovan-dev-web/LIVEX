// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Setup.App.CommandLine;
using Setup.App.Localization;
using Setup.App.Services;
using Setup.App.ViewModels;

namespace Setup.App;

public partial class App : Application
{
    public static AppOptions Options { get; set; } = AppOptions.Parse([]);

    public static InstallerSession? Session { get; set; }

    public static Window? MainWindow { get; set; }

    /// <summary>Écran de départ demandé par la ligne de commande (<c>--maintenance</c>).</summary>
    public static WizardStep InitialStep { get; set; } = WizardStep.Welcome;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = Session ?? InstallerSession.Create(Options);
            var viewModel = new WizardViewModel(session, Options);
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            desktop.MainWindow = window;

        }

        base.OnFrameworkInitializationCompleted();
    }
}
