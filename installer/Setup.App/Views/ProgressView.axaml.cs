// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Setup.App.Views;

public partial class ProgressView : UserControl
{
    public ProgressView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
