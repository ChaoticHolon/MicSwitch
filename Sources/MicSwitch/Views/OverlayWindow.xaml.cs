using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;
using static MicSwitch.Native.NativeMethods;

namespace MicSwitch.Views;

/// <summary>Always-on-top microphone indicator. When locked it is click-through; unlocked it can be dragged and resized.</summary>
public partial class OverlayWindow : Window
{
    private const double DefaultSize = 96;
    private readonly MainViewModel viewModel;
    private readonly SettingsService settings;

    public OverlayWindow(MainViewModel viewModel, SettingsService settings)
    {
        this.viewModel = viewModel;
        this.settings = settings;
        DataContext = viewModel;
        InitializeComponent();
        ApplyBounds(settings.Current.Overlay.Bounds);

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.OverlayResetRequested += (_, _) => ApplyBounds(null);
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        SizeChanged += (_, e) =>
        {
            // Keep the overlay square.
            if (e.WidthChanged && Math.Abs(Height - Width) > 0.5)
            {
                Height = Width;
            }

            SaveBounds();
        };
        LocationChanged += (_, _) => SaveBounds();
        SourceInitialized += (_, _) => UpdateLockState();
    }

    public void SyncVisibility()
    {
        if (viewModel.IsOverlayVisible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsOverlayVisible))
        {
            SyncVisibility();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsOverlayLocked))
        {
            UpdateLockState();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (viewModel.IsOverlayLocked)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            viewModel.IsOverlayLocked = true;
        }
        else if (e.OriginalSource != Grip)
        {
            DragMove();
        }
    }

    private void UpdateLockState()
    {
        var locked = viewModel.IsOverlayLocked;
        ResizeMode = locked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return;
        }

        var style = (int)GetWindowLongPtr(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
        style = locked ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, style);
    }

    private void ApplyBounds(WindowBounds? bounds)
    {
        if (bounds is null || !WindowPlacement.IsOnScreen(bounds))
        {
            var area = SystemParameters.WorkArea;
            bounds = new WindowBounds(area.Right - DefaultSize - 24, area.Top + 24, DefaultSize, DefaultSize);
        }

        (Left, Top, Width, Height) = (bounds.Left, bounds.Top, bounds.Width, bounds.Width);
    }

    private void SaveBounds()
    {
        if (!IsLoaded)
        {
            return;
        }

        settings.Current.Overlay.Bounds = new WindowBounds(Left, Top, Width, Height);
        settings.ScheduleSave();
    }
}
