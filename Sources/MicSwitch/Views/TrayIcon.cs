using System.ComponentModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using MicSwitch.ViewModels;

namespace MicSwitch.Views;

/// <summary>Notification-area icon: click toggles mute, double-click opens the window.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon icon;
    private readonly MainViewModel viewModel;

    public TrayIcon(MainViewModel viewModel, Action showWindow, Action exit)
    {
        this.viewModel = viewModel;
        var toggleItem = new MenuItem { Command = viewModel.ToggleMuteCommand };
        icon = new TaskbarIcon
        {
            NoLeftClickDelay = true,
            LeftClickCommand = viewModel.ToggleMuteCommand,
            DoubleClickCommand = new RelayCommand(showWindow),
            ContextMenu = new ContextMenu
            {
                Items =
                {
                    new MenuItem { Header = "Open MicSwitch", Command = new RelayCommand(showWindow) },
                    toggleItem,
                    new Separator(),
                    new MenuItem { Header = "Exit", Command = new RelayCommand(exit) },
                },
            },
        };
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Update();
        icon.ContextMenu.Opened += (_, _) => toggleItem.Header = viewModel.IsMuted == true ? "Unmute microphone" : "Mute microphone";
        icon.ForceCreate(enablesEfficiencyMode: false);
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
        icon.IconSource = viewModel.CurrentIcon;
        icon.ToolTipText = $"MicSwitch – {viewModel.MicrophoneStatus}";
    }
}
