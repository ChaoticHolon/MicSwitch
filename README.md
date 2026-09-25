![](https://img.shields.io/github/release-date/iXab3r/MicSwitch.svg) ![](https://img.shields.io/github/downloads/iXab3r/MicSwitch/total.svg) ![](https://img.shields.io/github/last-commit/iXab3r/MicSwitch.svg)
[![Discord Chat](https://img.shields.io/discord/513749321162686471.svg)](https://discord.gg/BExRm22)  

# Intro
There are dozens of different audio chat apps like Discord, TeamSpeak, Ventrilo, Skype, in-game audio chats, etc. And all of them have DIFFERENT ways of handling push-to-talk and always-on microphone functionality. I bet many of you know how distracting it could be when someone forgets to turn off a microphone. I will try to explain what I mean using a feature matrix.

| App  | Microphone status overlay | Keyboard support | Mouse buttons support | Audio notification |
| -------------: | :-------------: | :-------------: | :-------------: | :-------------: |
| MicSwitch |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")
| Discord  |  In-game only  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")   |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported") |
| TeamSpeak  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")   |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |
| Ventrilo  | ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  [Has a bug dating 2012](http://forum.ventrilo.com/showthread.php?t=61203 "Has a bug dating 2012")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |
| Skype  | ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |  Hard-coded Ctrl+M  |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported") |

MicSwitch allows you to mute/unmute your system microphone using a predefined system-wide hotkey which will affect any program that uses microphone (no more heavy breathing during Skype conferences, hooray!)
Also it supports configurable mute/unmute sounds(similar to TeamSpeak/Ventrilo) and a configurable overlay with scaling/transparency support. All these features allow you to seamlessly switch between chat apps and use THE SAME input system with overlay and notifications support.

# Features / Bugfixes priority (click to vote or post feature/bug request)
[![Requests](https://feathub.com/iXab3r/MicSwitch?format=svg)](https://feathub.com/iXab3r/MicSwitch)

# Installation
- You can download the latest version of installer here - [download](https://github.com/ChaoticHolon/MicSwitch/releases/latest).
- After initial installation application will periodically check Github for updates

## Features
- Multiple microphones support (useful for streamers) - ALL microphones in your system could be muted/unmuted by a single key press
- System-wide hotkeys (supports mouse XButtons)
- Always-on-top configurable (scale, transparency) Overlay - could be disable if not needed
- Mute/unmute audio notification (with custom audio files support)
- Customizable tray and overlay icons
- Multiple hotkeys support
- Auto-startup (could be Minimized by default)
- Three Audio modes: Push-to-talk, Push-to-mute and Toggle mute
- Overlay visibility could be linked to microphone state, i.e. it will be shown only when Muted/Unmuted
- Auto-updates via GitHub Releases
- Light and dark theme (follows Windows by default) on Windows 10 and 11

## Media
![UI](https://i.imgur.com/Fz0nTZP.png)

### Overlay with configurable size/opacity
![Overlay with configurable size/opacity](https://i.imgur.com/1Jf1RrH.gif)

### Configurable Audio notification when microphone is muted/unmuted
![Configurable Audio notification when microphone is muted/unmuted](https://i.imgur.com/TmvJizg.png) 

### Customizable overlay/tray icons
![Customizable overlay/tray icons](https://i.imgur.com/Bq0yHnK.png)

### Auto-update via Github
![Auto-update via Github](https://i.imgur.com/O4SIuDy.gif)

## How to build

Requirements: Windows 10 (version 2004 or later) or Windows 11, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
git clone https://github.com/ChaoticHolon/MicSwitch.git
cd MicSwitch/Sources
dotnet build MicSwitch.slnx
dotnet test --project MicSwitch.Tests
dotnet run --project MicSwitch
```

No submodules or symlinks are needed any more. `MicSwitch.Core` (settings, hotkey parsing, mute rules) is plain .NET, so its tests also run on Linux and macOS.

### Project layout
| Project | Contents |
| --- | --- |
| `MicSwitch` | WPF app (.NET 10, Fluent theme): view models (CommunityToolkit.Mvvm), low-level keyboard/mouse hooks, Core Audio control (NAudio), tray icon (H.NotifyIcon), updates (Velopack) |
| `MicSwitch.Core` | UI-independent logic: settings model and JSON storage, MicSwitch 1.x config migration, hotkey text format, mute rules |
| `MicSwitch.Tests` | xUnit v3 tests for `MicSwitch.Core` |

### Settings
Settings are stored in `%APPDATA%\MicSwitch\settings.json`, logs in `%APPDATA%\MicSwitch\logs`. On first start, settings from MicSwitch 1.x (`%APPDATA%\MicSwitch\release\config.cfg`) are imported automatically; custom sounds in `%APPDATA%\MicSwitch\Resources\Notifications` keep working.

### Releasing
Push a tag like `v2.0.0`. The `Release` workflow publishes a self-contained build and uses [Velopack](https://velopack.io) to create the installer and the update feed on GitHub Releases, which installed copies check for updates.
