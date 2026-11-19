# M-Track driver control protocol

Reverse-engineered from the M-Audio M-Track driver package 1.0.6 (driver `MAudioMTrack.sys`
5.10.0.6020, panel `MAPanel.exe`, ASIO `MAudioMTrack_Asio64.dll`). Verified against an M-Track
(USB `VID_0763&PID_4000`, firmware 1.0.3) on Windows 11.

## Opening the device

The driver exposes a KS topology filter:

- Interface class: `KSCATEGORY_TOPOLOGY` `{DDA54A40-1E4C-11D1-A050-405705C10000}`
- Reference string: `{CE869237-0350-49E7-BE8D-57C8EA29C0FD}` (`KSNAME_TopoFilter` in the INF)
- Example path:
  `\\?\USB#VID_0763&PID_4000&MI_00#<instance-id>#{dda54a40-...}\{CE869237-...}`

Open it with `CreateFileW(GENERIC_READ|GENERIC_WRITE, FILE_SHARE_READ|FILE_SHARE_WRITE, OPEN_EXISTING)`.
No administrator rights are needed.

## Property set "standard properties": `{55A8C0F3-67A0-4AF8-9C0D-EC86AE3F6789}`

All requests use `IOCTL_KS_PROPERTY` (`0x2F0003`). The input is a 24-byte `KSPROPERTY`
`{ GUID Set; ULONG Id; ULONG Flags }`. For both GET and SET the value goes in the **output** buffer.

| Id | Access  | Size | Meaning |
|---:|---------|-----:|---------|
| 0  | GET     | 344  | Device descriptor blob (contains "MTrack"); not needed |
| 1  | GET     | 36   | Versions: `u32[0..3]` driver a.b.c.d, `u32[4]` reserved, `u32[5..7]` firmware a.b.c, `u32[8]` reserved |
| 3  | GET/SET | 4    | Unknown (0). Not exposed by the original panel UI |
| 4  | GET/SET | 4    | **Sample rate** in Hz (44100, 48000) |
| 5  | GET/SET | 4    | **Buffer size** in samples (128 … 4096) |
| 9  | GET     | 4    | Unknown (0) |
| 10 | GET     | 4    | Clock locked (1 = locked) |

Other ids return `ERROR_NOT_FOUND` (1168).

The driver saves the sample rate and buffer size itself, in
`HKLM\SYSTEM\CurrentControlSet\Enum\USB\VID_0763&PID_4000&MI_00\<instance>\Device Parameters\DEVICE SETTINGS`
(bytes 0–3 = rate, 4–7 = buffer).

The original panel offers fixed lists: sample rates 44100 and 48000, and buffer sizes 128, 256,
512, 1024, 2048 and 4096.

## Property set "command channel": `{3961234E-B801-4B28-8FAA-5BB5DC0EF610}`

Id 0 GET/SET carries a generic request: a 0x60-byte input of `KSPROPERTY` + `u32 command` + payload,
and a 0x80-byte output. The original panel uses it for its internal parameter framework. It is not
needed to change sample rate or buffer size, so it was not decoded further.

## Events

The original panel enables `KSEVENT` Id 0 on both sets (`IOCTL_KS_ENABLE_EVENT`, `0x2F0007`) to be
notified of changes. The new panel polls once every 1.5 s while its window is visible instead.

## How the ASIO driver opens the control panel

`MAudioMTrack_Asio(64).dll`, on `ASIOControlPanel()`:

1. `FindWindowA("{CE869237-0350-49E7-BE8D-57C8EA29C0FD}_DDPanel", NULL)`. If a window is found:
   `ShowWindow(SW_SHOW)` + `SetForegroundWindow`.
2. Otherwise it reads `SoftwarePath` and `ControlPanelApp` from
   `HKLM\SOFTWARE\MAudio\Audio\Drivers\{CE869237-...}` (opened with `KEY_WOW64_32KEY`, so the
   WOW6432Node view, even from the 64-bit DLL) and runs `WinExec(SoftwarePath + "\" + ControlPanelApp)`.

MTrackPanel registers a hidden top-level window with that class name and shows its settings
window when it receives `WM_SHOWWINDOW`.

## Other components (unchanged)

- `AudioDevMon.exe`: LocalSystem service `MTrackAudioDevMon` ("Manages device settings and hot
  plugging"). It persists per-instance parameters under
  `HKLM\SOFTWARE\WOW6432Node\MAudio\Audio\Drivers\{CE869237-...}\<hw>\Instances\...\Param0000N`.
- `DeviceInterface.dll`: the `AADI_*` C API used by the ASIO DLLs. Its device-list output is a C++
  `MA::VariantEx` object, so it is not convenient to call from other languages.
