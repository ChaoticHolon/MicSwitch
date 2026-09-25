using System.ComponentModel;
using System.Windows;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;

namespace MicSwitch.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly SettingsService settings;

    public MainWindow(MainViewModel viewModel, SettingsService settings)
    {
        this.viewModel = viewModel;
        this.settings = settings;
        DataContext = viewModel;
        InitializeComponent();
        if (settings.Current.Window.Bounds is { } b && WindowPlacement.IsOnScreen(b))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Left, Top, Width, Height) = (b.Left, b.Top, b.Width, b.Height);
        }
    }

    /// <summary>Set while the app is shutting down so closing isn't turned into hiding.</summary>
    public bool IsExiting { get; set; }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        settings.Current.Window.Bounds = WindowState == WindowState.Normal
            ? new WindowBounds(Left, Top, Width, Height)
            : new WindowBounds(RestoreBounds.Left, RestoreBounds.Top, RestoreBounds.Width, RestoreBounds.Height);
        settings.ScheduleSave();

        if (!IsExiting && viewModel.MinimizeOnClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
