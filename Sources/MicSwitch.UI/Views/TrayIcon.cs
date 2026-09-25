using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using MicSwitch.ViewModels;

namespace MicSwitch.Views;

/// <summary>Notification-area icon: click toggles mute; the menu opens the window or exits.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Avalonia.Controls.TrayIcon icon;
    private readonly NativeMenuItem toggleItem;
    private readonly MainViewModel viewModel;

    public TrayIcon(Application application, MainViewModel viewModel, Action showWindow, Action exit)
    {
        this.viewModel = viewModel;
        toggleItem = new NativeMenuItem { Command = viewModel.ToggleMuteCommand };
        icon = new Avalonia.Controls.TrayIcon
        {
            Command = viewModel.ToggleMuteCommand,
            Menu =
            [
                new NativeMenuItem { Header = "Open MicSwitch", Command = new RelayCommand(showWindow) },
                toggleItem,
                new NativeMenuItemSeparator(),
                new NativeMenuItem { Header = "Exit", Command = new RelayCommand(exit) },
            ],
            IsVisible = true,
        };
        Update();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Avalonia.Controls.TrayIcon.SetIcons(application, [icon]);
    }

    public void Dispose()
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        icon.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentIcon) or nameof(MainViewModel.MicrophoneStatus))
        {
            Update();
        }
    }

    private void Update()
    {
        icon.Icon = new WindowIcon(viewModel.CurrentIcon);
        icon.ToolTipText = $"MicSwitch – {viewModel.MicrophoneStatus}";
        toggleItem.Header = viewModel.IsMuted == true ? "Unmute microphone" : "Mute microphone";
    }
}
