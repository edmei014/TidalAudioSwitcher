# Tidal Audio Switcher

A Windows utility that coordinates **TIDAL playback mode** and the **Windows default audio device** in one click—helping you enjoy TIDAL Exclusive Mode on your DAC while reducing many common desktop audio conflicts.

![Main window](Assets/audioswitcher.png) · ![Compact mode](Assets/compactmode.png)

**More detail:** [Audio workflow](AUDIO_WORKFLOW.md) · [Technical documentation](TECHNICAL_DOCUMENTATION.md)

---

## Why this exists

TIDAL **Exclusive Mode** can provide direct access to a DAC, but on Windows it may block or disrupt other applications that use the same audio endpoint. Depending on driver, app, and how each program picks its output, that can show up as browser playback issues, apps that hang when opening audio, or recovery that requires manual device changes—and occasionally a full Windows restart.

Tidal Audio Switcher addresses this problem with a **coordinated one-click audio workflow**.

Instead of manually switching TIDAL's playback mode and Windows audio devices, the application manages both together. Music Mode keeps TIDAL on your DAC while redirecting applications that follow the Windows default audio device to a separate silent endpoint.

The result is a more convenient way to enjoy TIDAL Exclusive Mode while avoiding many common Windows audio conflicts.

---

## Music Mode and Normal Mode

| | **Music Mode** | **Normal Mode** |
|---|----------------|-----------------|
| TIDAL | Exclusive on your **TIDAL Device** (usually the DAC) | Shared on **TIDAL Device** |
| Windows default | **Silent Device** (e.g. virtual cable) | **TIDAL Device** |
| Typical result | TIDAL owns the DAC; most “default” apps go to the silent sink | Desktop audio back on the DAC |

**Music Mode:** Windows default → Silent Device, then TIDAL → exclusive on the DAC.  
**Normal Mode:** TIDAL → shared on the DAC, then Windows default → DAC.

See [Audio workflow](AUDIO_WORKFLOW.md) for diagrams, limits, and a DAC + YouTube example.

---

## Features

- Launcher (`TidalAudioSwitcher.exe`) starts TIDAL with CDP when needed, then opens the UI
- One-click Music / Normal Mode (TIDAL via CDP + Windows default switching)
- Configurable **TIDAL Device** and **Silent Device** (must differ)
- Auto **Normal Mode** when you change TIDAL Device after startup
- **Compact Mode** — small always-on-top toggle (144×54)
- Persists devices, window position, Compact Mode, and last mode label
- **Restart TIDAL** from the UI
- Taskbar grouping and relaunch via the launcher executable
- Diagnostics log (see [Paths and data](#paths-and-data))

---

## Requirements

| Requirement | Details |
|-------------|---------|
| **Operating system** | **Windows 11** (tested). **Windows 10** is not verified by the maintainer; it may work but is unsupported until confirmed. |
| **.NET runtime** | [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) — official release builds are **framework-dependent** and require this runtime |
| **TIDAL** | Official TIDAL desktop app (see [Paths and data](#paths-and-data)) |
| **Audio** | At least one playback device; **Music Mode** needs two distinct devices (DAC + silent sink) |
| **Silent device (typical)** | [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) or any second endpoint used as a discard sink |

Unofficial utility—not affiliated with TIDAL.

---

## Installation

1. Download **`TidalAudioSwitcher-v*.zip`** from the project’s GitHub Releases page (current release: **v1.1.3**, e.g. `TidalAudioSwitcher-v1.1.3.zip`).
2. Extract to any folder—for example `C:\Program Files\TidalAudioSwitcher` or `D:\Tools\TidalAudioSwitcher`.
3. Install the **.NET 8 Desktop Runtime** if it is not already on the PC.
4. Do **not** place the app inside the TIDAL install folder; the two products are independent.

**Run the application**

- Start **`TidalAudioSwitcher.exe`** (recommended entry point).
- Or use **`Start Tidal Audio Switcher.bat`** or **`Tidal Audio Switcher.lnk`** in the release folder.

**Layout rule:** keep **`TidalAudioSwitcher.exe`** and **`TidalAudioSwitcher.UI.exe`** in the **same directory**, together with the bundled `.dll` and `.json` files from the ZIP. The launcher starts the UI from that folder.

---

## Configuration and usage

1. Install a silent sink if you only have one physical output (VB-Audio Virtual Cable is a common choice).
2. Start **`TidalAudioSwitcher.exe`**. The launcher prepares TIDAL and opens the UI.
3. **TIDAL Device** — your DAC or main listening output (Windows playback list).
4. **Silent Device** — virtual cable or second device (must differ from TIDAL Device).
5. **Music Mode** for exclusive listening; **Normal Mode** for everyday desktop audio.

**Compact Mode:** use the title-bar control for a floating overlay; drag the header to reposition.

---

## Paths and data

| What | Location |
|------|----------|
| **Your settings** (devices, mode label, window, Compact Mode) | `%LocalAppData%\TidalAudioSwitcher\settings.json` |
| **Diagnostic log** | `%TEMP%\TidalAudioSwitcher.log` |
| **TIDAL executable** (discovered automatically; not modified) | `%LocalAppData%\TIDAL\app-*\TIDAL.exe` (newest `app-*` folder) |
| **Application install** | Any folder you choose; not tied to TIDAL’s path |

Settings and logs are **not** stored next to the executables.

---

## Limitations

- Mode switching requires a local connection to the TIDAL desktop client. The launcher starts TIDAL with the parameters the app needs; if you opened TIDAL yourself first, use the launcher’s restart prompt or **Restart TIDAL** in the UI (see [Technical documentation — TIDAL launch and CDP](TECHNICAL_DOCUMENTATION.md#tidal-launch-and-chrome-devtools-protocol-cdp)).
- Saved **Active Mode** is shown in the UI but **not re-applied** automatically on launch; click the mode again if routing drifted.
- TIDAL’s settings screen may not reflect Exclusive/Shared after programmatic changes; use the in-app log if unsure.
- Device lists come from **Windows**, not TIDAL’s in-app device picker.
- Some applications pin a specific device and ignore the Windows default.

---

## Troubleshooting

| Issue | What to do |
|-------|------------|
| TIDAL not found | Install TIDAL desktop; confirm `%LocalAppData%\TIDAL\app-*\TIDAL.exe` exists |
| Music Mode / CDP errors | Close TIDAL; start **`TidalAudioSwitcher.exe`**; wait up to 45 seconds; try **Restart TIDAL** |
| TIDAL already open without CDP | Accept the launcher restart prompt, or use **Restart TIDAL** in the UI |
| Missing device | Reconnect hardware; reselect devices (only **active** endpoints are listed) |
| Default device unchanged | Check the in-app log and `%TEMP%\TidalAudioSwitcher.log`; retry the mode switch |
| Missing .NET runtime | Install [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Wrong taskbar pin | Pin **`TidalAudioSwitcher.exe`**, not `TidalAudioSwitcher.UI.exe` |

For implementation-level causes, see [Technical documentation — Troubleshooting](TECHNICAL_DOCUMENTATION.md#troubleshooting).

---

## Disclaimer

This repository does not include a license. All rights are reserved by default; contact the maintainer for permission to redistribute or reuse the software.
