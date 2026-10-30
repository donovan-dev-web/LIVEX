// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Setup.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryApplyIcon();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void TryApplyIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://livex-setup/Assets/livex-app-icon.ico"));
            Icon = new WindowIcon(stream);
        }
        catch
        {
            // Icône best effort : son absence ne doit pas empêcher l'assistant de démarrer.
        }
    }
}
