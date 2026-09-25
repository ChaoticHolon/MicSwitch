using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MicSwitch.Platform;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;

namespace MicSwitch.Views;

/// <summary>Always-on-top microphone indicator. When locked it is click-through; unlocked it can be dragged and resized.</summary>
public partial class OverlayWindow : Window
{
    private const double DefaultSize = 96;
    private readonly MainViewModel viewModel;
    private readonly SettingsService settings;
    private readonly IWindowInterop interop;

    public OverlayWindow(MainViewModel viewModel, SettingsService settings, IWindowInterop interop)
    {
        this.viewModel = viewModel;
        this.settings = settings;
        this.interop = interop;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.OverlayResetRequested += (_, _) => PlaceAtDefault();
        Frame.PointerPressed += OnFramePointerPressed;
        Grip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
                e.Handled = true;
            }
        };
        PositionChanged += (_, _) => SaveBounds();
        Opened += (_, _) =>
        {
            if (settings.Current.Overlay.Bounds is { } b)
            {
                (Width, Height) = (b.Width, b.Width);
                Position = new PixelPoint((int)b.Left, (int)b.Top);
            }

            if (settings.Current.Overlay.Bounds is null || !WindowPlacement.IsOnScreen(this, new Size(Width, Height)))
            {
                PlaceAtDefault();
            }

            ApplyLockState();
        };
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClientSizeProperty)
        {
            // Keep the overlay square.
            if (Math.Abs(Height - Width) > 0.5)
            {
                Height = Width;
            }

            SaveBounds();
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
            ApplyLockState();
        }
    }

    private void OnFramePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            viewModel.IsOverlayLocked = true;
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private void ApplyLockState() => interop.ConfigureOverlay(TryGetPlatformHandle()?.Handle ?? 0, viewModel.IsOverlayLocked);

    private void PlaceAtDefault()
    {
        (Width, Height) = (DefaultSize, DefaultSize);
        var area = WindowPlacement.PrimaryWorkingArea(this);
        var size = PixelSize.FromSize(new Size(DefaultSize, DefaultSize), RenderScaling);
        var margin = (int)(24 * RenderScaling);
        Position = new PixelPoint(area.Right - size.Width - margin, area.Y + margin);
    }

    private void SaveBounds()
    {
        if (!IsVisible)
        {
            return;
        }

        settings.Current.Overlay.Bounds = new WindowBounds(Position.X, Position.Y, Width, Height);
        settings.ScheduleSave();
    }
}
