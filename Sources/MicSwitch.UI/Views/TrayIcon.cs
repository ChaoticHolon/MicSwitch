using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using MicSwitch.ViewModels;

namespace MicSwitch.Views;

/// <summary>Notification-area icon. Click opens the window; right-click offers the everyday controls.</summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly HashSet<string> MenuProperties =
    [
        nameof(MainViewModel.IsMuted), nameof(MainViewModel.MuteMode), nameof(MainViewModel.SelectedMicrophoneId),
        nameof(MainViewModel.OverlayVisibility), nameof(MainViewModel.SoundsEnabled), nameof(MainViewModel.RunAtStartup),
        nameof(MainViewModel.StartInTray), nameof(MainViewModel.IsOverlayEditing),
    ];

    private readonly Avalonia.Controls.TrayIcon icon;
    private readonly MainViewModel viewModel;
    private readonly Action showWindow;
    private readonly Action exit;

    public TrayIcon(Application application, MainViewModel viewModel, Action showWindow, Action exit)
    {
        this.viewModel = viewModel;
        this.showWindow = showWindow;
        this.exit = exit;
        icon = new Avalonia.Controls.TrayIcon { Command = new RelayCommand(showWindow), IsVisible = true };
        UpdateIcon();
        RebuildMenu();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Microphones.CollectionChanged += (_, _) => RebuildMenu();
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
            UpdateIcon();
        }

        if (e.PropertyName is not null && MenuProperties.Contains(e.PropertyName))
        {
            RebuildMenu();
        }
    }

    private void UpdateIcon()
    {
        icon.Icon = new WindowIcon(viewModel.CurrentIcon);
        icon.ToolTipText = $"{MainViewModel.AppName}: {viewModel.MicrophoneStatus}";
    }

    private void RebuildMenu()
    {
        var vm = viewModel;
        icon.Menu =
        [
            Item(vm.IsMuted == true ? "Unmute microphone" : "Mute microphone", vm.ToggleMuteCommand.Execute),
            new NativeMenuItemSeparator(),
            Submenu("Mode", vm.MuteModes.Select(m => Radio(m.Name, Equals(m.Value, vm.MuteMode), () => vm.MuteMode = (MuteMode)m.Value!))),
            Submenu("Microphone", vm.Microphones.Select(d => Radio(d.Name, d.Id == vm.SelectedMicrophoneId, () => vm.SelectedMicrophoneId = d.Id))),
            Submenu("Overlay",
            [
                .. vm.OverlayVisibilityModes.Select(m => Radio(m.Name, Equals(m.Value, vm.OverlayVisibility), () => vm.OverlayVisibility = (OverlayVisibilityMode)m.Value!)),
                new NativeMenuItemSeparator(),
                Check("Edit position", vm.IsOverlayEditing, () => vm.IsOverlayEditing = !vm.IsOverlayEditing),
            ]),
            Check("Play sounds", vm.SoundsEnabled, () => vm.SoundsEnabled = !vm.SoundsEnabled),
            new NativeMenuItemSeparator(),
            Check("Start with Windows", vm.RunAtStartup, () => vm.RunAtStartup = !vm.RunAtStartup),
            Check("Start in tray", vm.StartInTray, () => vm.StartInTray = !vm.StartInTray),
            new NativeMenuItemSeparator(),
            Item($"Open {MainViewModel.AppName}", showWindow),
            Item("Exit", exit),
        ];
    }

    private static NativeMenuItem Item(string header, Action action) => new(header) { Command = new RelayCommand(action) };

    private static NativeMenuItem Item(string header, Action<object?> action) => new(header) { Command = new RelayCommand(() => action(null)) };

    private static NativeMenuItem Radio(string header, bool isChecked, Action action) => new(header)
    {
        ToggleType = MenuItemToggleType.Radio,
        IsChecked = isChecked,
        Command = new RelayCommand(action),
    };

    private static NativeMenuItem Check(string header, bool isChecked, Action action) => new(header)
    {
        ToggleType = MenuItemToggleType.CheckBox,
        IsChecked = isChecked,
        Command = new RelayCommand(action),
    };

    private static NativeMenuItem Submenu(string header, IEnumerable<NativeMenuItemBase> items)
    {
        var menu = new NativeMenu();
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        return new NativeMenuItem(header) { Menu = menu };
    }
}
