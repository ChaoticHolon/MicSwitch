using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MicControlNG.Views;

/// <summary>Shown once, the first time the main window is closed.</summary>
public partial class TrayHintWindow : Window
{
    public TrayHintWindow() => InitializeComponent();

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
