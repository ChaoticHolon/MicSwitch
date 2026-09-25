using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
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
        UpdateIcon();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (settings.Current.Window.Bounds is { } b)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Width, Height) = (b.Width, b.Height);
            Position = new PixelPoint((int)b.Left, (int)b.Top);
            Opened += (_, _) =>
            {
                if (!WindowPlacement.IsOnScreen(this, Bounds.Size))
                {
                    Position = WindowPlacement.CenterOfPrimary(this, Bounds.Size);
                }
            };
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            viewModel.IsWindowVisible = IsVisible && WindowState != WindowState.Minimized;
        }
        else if (change.Property == WindowStateProperty)
        {
            if (WindowState == WindowState.Minimized && viewModel.StartInTray)
            {
                // "Start in tray" users expect minimize to tuck the window away too.
                WindowState = WindowState.Normal;
                Hide();
            }

            viewModel.IsWindowVisible = IsVisible && WindowState != WindowState.Minimized;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            settings.Current.Window.Bounds = new WindowBounds(Position.X, Position.Y, Width, Height);
            settings.ScheduleSave();
        }

        if (!IsExiting)
        {
            // Closing hides to the tray; exit from the tray icon's menu.
            e.Cancel = true;
            Hide();
            if (viewModel.ConsumeTrayHint())
            {
                new TrayHintWindow().Show();
            }
        }

        base.OnClosing(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentIcon))
        {
            UpdateIcon();
        }
    }

    private void UpdateIcon() => Icon = new WindowIcon(viewModel.CurrentIcon);
}
