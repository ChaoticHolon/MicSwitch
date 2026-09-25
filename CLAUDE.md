# MicSwitch

System-wide microphone mute/unmute with hotkeys, an always-on-top overlay, sounds and a tray icon.
.NET 10, Avalonia 12.1 UI, C# latest. Solution: `Sources/MicSwitch.slnx`.

## Platform scope
- **Windows 10 (2004+) is the only supported platform right now.** Windows 11 gets tested later in a VM.
- **Linux is next: KDE Plasma 6 on Wayland only** (no X11, no other desktops for now). Plan: GlobalShortcuts portal for hotkeys, PipeWire for audio, layer-shell overlay, XDG autostart, Flatpak.
- macOS is a distant stretch goal. Don't use MAUI.
- The user works in **Visual Studio Enterprise** only; never suggest VS Code or JetBrains tooling.

## Commands (run from `Sources/`)
- Build: `dotnet build MicSwitch.slnx`. Warnings are errors, and the analyzers are strict (`AnalysisLevel=latest-recommended`).
- Test: `dotnet test --project MicSwitch.Tests` (Microsoft Testing Platform via `global.json`; the old `dotnet test <proj>` form fails).
- Run (Windows): `dotnet run --project MicSwitch`. The app asks for elevation (`highestAvailable`), so run from an elevated terminal or Visual Studio started as administrator.
- Tests run on Windows and Linux. The Windows projects also build on Linux (`EnableWindowsTargeting`).

## Layout
| Project | Contents | Rules |
| --- | --- | --- |
| `MicSwitch.Core` (net10.0) | Settings model and JSON store (`Settings/`), 1.x config import, `HotkeyGesture` text format, `MuteRules`, `NotificationSounds`, platform contracts (`Platform/PlatformServices.cs`) | No UI or OS dependencies |
| `MicSwitch.UI` (net10.0) | Avalonia views (`Views/`), view models (`ViewModels/`, CommunityToolkit.Mvvm), controls, theme (`Themes/`), `App.axaml.cs` composition (`RegisterCommonServices`) | Talk to the OS only through the `MicSwitch.Platform` interfaces |
| `MicSwitch.Windows` | Core Audio via NAudio, low-level keyboard/mouse hooks (`GlobalHotkeyService`, `VirtualKeys`), WASAPI playback, autostart (Run key or scheduled task), overlay click-through | Windows-only code goes here |
| `MicSwitch` | Windows exe: `Program.cs` (single instance, DI registration of Windows services), Velopack `UpdateService`, `app.manifest` | |
| `MicSwitch.Tests` | xUnit v3: core tests plus headless Avalonia UI tests (`Ui/`, with fakes in `Ui/Fakes.cs`) | |

## Conventions
- Package versions live only in `Sources/Directory.Packages.props`; shared build settings are in `Directory.Build.props`, style in `.editorconfig`.
- XAML uses compiled bindings (`x:DataType`). For a ComboBox, use `DisplayMemberBinding` and `SelectedValueBinding` with `x:DataType`. Selected values must never be `null`: use `""` for "None" or "Default".
- Theme colors are our own light/dark `ThemeDictionaries` in `Themes/Theme.axaml`, so Windows 10 and 11 look identical. Icons use the `IconFont` resource (Segoe Fluent Icons, falling back to Segoe MDL2 Assets). Linux will need a bundled icon font.
- Settings: `%APPDATA%\MicSwitch\settings.json` (camelCase, hotkeys stored as text like `"Ctrl+F1"`); logs in `%APPDATA%\MicSwitch\logs`. Keep 1.x import working (`LegacyConfigMigrator`), and keep hotkey strings compatible with 1.x.
- Every new platform capability gets an interface in `MicSwitch.Core/Platform`, a Windows implementation, a fake in the tests, and a flag in `PlatformCapabilities` if some platforms can't support it.
- Add or extend tests for behavior changes; UI behavior can be tested headlessly with `[AvaloniaFact]`.
