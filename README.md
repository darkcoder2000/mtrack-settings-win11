# M-Track Settings for Windows 11

A replacement for the settings panel (`MAPanel.exe`) of the **M-Audio M-Track / M-Track Plus**
driver 1.0.6. The original Qt 4 panel renders incorrectly on Windows 11: its sample-rate and
buffer-size dropdowns are empty, and it doesn't show the current values.

This app talks to the existing M-Audio kernel driver directly. The driver itself is not changed.

> **Unofficial project.** It is not affiliated with, endorsed by, or supported by M-Audio or inMusic Brands.
> "M-Audio" and "M-Track" are trademarks of their respective owners and are used here only to describe
> which hardware this software works with. Use at your own risk.

## Features

- Sample rate (44.1 / 48 kHz) and ASIO buffer size (128–4096 samples, with latency in ms)
- Live connection status, plus driver, firmware and package versions
- Windows 11 Fluent look; follows light and dark mode; per-monitor DPI aware
- Runs in the tray like the original (left-click opens it, right-click shows a menu); closing the window hides it
- Opens from your DAW's **ASIO Control Panel** button (same hand-off as the original panel)
- Single instance: launching it again brings the running window to the front

## Install

Requirements: the M-Audio M-Track driver 1.0.6 must be installed. The published exe is
self-contained, so no .NET runtime is needed.

```
dotnet publish src\MTrackPanel -c Release -o publish
install\Install.cmd
```

`Install.cmd` asks for administrator rights. It then:

- copies `MTrackPanel.exe` to `C:\Program Files (x86)\M-Audio\M-Track\`
- sets `ControlPanelApp` = `MTrackPanel.exe` in `HKLM\SOFTWARE\WOW6432Node\MAudio\Audio\Drivers\{CE869237-...}`
- changes the `M-Audio Panel Launcher` autostart entry to `MTrackPanel.exe 0` (start in the tray)
- saves the original values in `install\backup.json`

`install\Uninstall.cmd` restores the original panel. The old files are never deleted.

## Project layout

| Path | Contents |
|------|----------|
| `src/MTrack.Core` | KS property interop (`Interop/KsDevice.cs`) and the device API (`MTrackDevice.cs`) |
| `src/MTrackPanel` | WPF app: window, tray icon, ASIO activation window |
| `tools/MTrackProbe` | Console tool: dumps driver properties (`MTrackProbe`), sets a property (`MTrackProbe set 5 256`), tests FindWindow (`MTrackProbe find`) |
| `install/` | Install and uninstall scripts |
| `docs/protocol.md` | Reverse-engineered driver protocol |
