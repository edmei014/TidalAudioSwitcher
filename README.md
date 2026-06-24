# Tidal Audio Switcher

Kleine Windows-WPF-App zum Umschalten zwischen TIDAL **exclusive/shared** und dem Windows-Standard-Audiogerät.

## Screenshots

### Main Window

![Main Window](audioswitcher.png)

### Compact Mode

![Compact Mode](compactmode.png)

## Einstiegspunkt für Endnutzer

Nach der Installation starten Endnutzer **nur**:

```text
TidalAudioSwitcher.exe
```

Alternativ: `Start Tidal Audio Switcher.bat` im gleichen Ordner.

Der Launcher (`TidalAudioSwitcher.exe`) übernimmt:

1. Prüfen, ob TIDAL läuft
2. Falls TIDAL ohne CDP läuft → Dialog mit **TIDAL schließen und neu starten** / **Abbrechen**
3. Falls TIDAL nicht läuft → TIDAL mit `--remote-debugging-port=9222` starten
4. Warten auf `http://127.0.0.1:9222/json/list`
5. UI starten (`TidalAudioSwitcher.UI.exe`, intern)
6. Warten, bis das UI-Fenster sichtbar ist
7. Launcher beendet sich

---

## Why?

TIDAL Exclusive Mode provides the best possible audio path to a DAC,
but it prevents normal desktop audio routing.

Tidal Audio Switcher makes it possible to switch between:

- Dedicated music listening (Exclusive Mode)
- Everyday desktop usage (Shared Mode)

with a single click.

## Installation

### Installationsort

Die Anwendung kann in **beliebigen Ordnern** installiert werden, z. B.:

```text
C:\Program Files\TidalAudioSwitcher
D:\Tools\TidalAudioSwitcher
```

Sie muss **nicht** im TIDAL-Ordner liegen. TIDAL wird separat unter `%LocalAppData%\TIDAL\` gesucht.

**Wichtig:** `TidalAudioSwitcher.exe` und `TidalAudioSwitcher.UI.exe` müssen **im selben Ordner** bleiben (inkl. aller `.dll`- und `.json`-Dateien aus dem Release-Paket).

### Voraussetzungen

| Anforderung | Details |
|-------------|---------|
| **Betriebssystem** | Windows 10 oder 11 (64-bit) |
| **.NET 8 Desktop Runtime** | [Download .NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — Runtime, nicht SDK |
| **TIDAL Desktop** | Offizielle Desktop-App, installiert unter `%LocalAppData%\TIDAL\app-*\TIDAL.exe` |
| **Audiogeräte** | Mindestens ein Ausgabegerät; für Music Mode ein zweites Gerät als Silent Device |

### Release-Paket (Endnutzer)

Entpacken von `TidalAudioSwitcher-v1.0.zip`:

```text
TidalAudioSwitcher-v1.0/
├── TidalAudioSwitcher.exe          ← starten
├── TidalAudioSwitcher.UI.exe       ← intern, nicht manuell starten
├── TidalAudioSwitcher*.dll / *.json
├── NAudio*.dll
├── Assets/
├── README.md
└── Start Tidal Audio Switcher.bat
```

Details zur Distribution: siehe [DISTRIBUTION.md](DISTRIBUTION.md).

### Silent Device: VB-Audio Virtual Cable (empfohlen)

Für **Music Mode** wird ein zweites Audiogerät benötigt, das als **Silent Device** dient (Windows-Standard wird darauf umgeschaltet, während TIDAL exclusive auf das TIDAL Device spielt).

**VB-Audio Virtual Cable** eignet sich dafür gut:

- Offizieller Download: [https://vb-audio.com/Cable/](https://vb-audio.com/Cable/)
- Nach der Installation erscheint ein Gerät wie **CABLE Input** / **VB-Audio Virtual Cable** in der Geräteliste
- Dieses Gerät als **Silent Device** wählen

Die App bevorzugt beim ersten Start automatisch Geräte mit „CABLE“ oder „VB-Audio“ im Namen als Silent Device, sofern vorhanden.

### Exclusive Mode in TIDAL

Im **Music Mode** schaltet die App TIDAL per CDP auf **exclusive** Wiedergabe auf dem gewählten TIDAL Device.

TIDAL zeigt den Exclusive-/Shared-Status **nicht immer zuverlässig** in der eigenen UI an. Das bedeutet:

- Exclusive Mode kann **aktiv sein**, auch wenn TIDAL dies nicht sichtbar anzeigt
- Maßgeblich ist das Verhalten der App (Music Mode = exclusive, Normal Mode = shared)
- Bei Problemen: `%TEMP%\TidalAudioSwitcher.log` prüfen

---

## Build & Release (Entwickler)

```powershell
cd TidalAudioSwitcher
dotnet build -c Release
.\scripts\Package-Release.ps1 -Configuration Release -Version 1.0
```

Ausgabe: `release/TidalAudioSwitcher-v1.0/` und `.zip` — **nicht** der rohe `bin/`-Ordner.

Version und Metadaten: `Directory.Build.props`. Optional: `Assets/app.ico` für Anwendungsicons.

---

## Warum der Debug-Port?

Der Audio Switcher steuert TIDAL über **Chrome DevTools Protocol (CDP)**. TIDAL aktiviert CDP nur mit:

```text
--remote-debugging-port=9222
```

Ein normaler TIDAL-Start funktioniert nicht mit Music/Normal Mode.

## Was passiert bei normalem TIDAL-Start?

| Situation | Verhalten |
|-----------|-----------|
| TIDAL nicht gestartet | Launcher startet TIDAL automatisch |
| TIDAL läuft ohne Debug-Port | Dialog: TIDAL schließen und neu starten |
| TIDAL mit Debug-Port bereit | UI startet sofort |
| UI läuft, CDP fehlt später | Fallback-Dialog **Automatisch starten** in der UI |

Diagnose-Log: `%TEMP%\TidalAudioSwitcher.log`

Einstellungen: `%LocalAppData%\TidalAudioSwitcher\settings.json`

---

## Entwicklung

```powershell
# Produktions-Flow (Launcher + UI)
dotnet run --project TidalAudioSwitcher.Launcher -c Release

# Nur UI (ohne Launcher, für Entwicklung)
dotnet run --project TidalAudioSwitcher.App -c Release
```

---

## Bedienung

1. **TIDAL Device** und **Silent Device** wählen
2. **Music Mode** — Silent Device wird Windows-Standard, TIDAL exclusive
3. **Normal Mode** — TIDAL shared, Windows-Standard auf TIDAL Device
4. **Compact Mode** — kleines Always-on-Top-Fenster mit Music-Toggle

---

## Projektstruktur

```text
TidalAudioSwitcher/
├── Directory.Build.props
├── DISTRIBUTION.md
├── scripts/Package-Release.ps1
├── Assets/                          # optional app.ico
├── TidalAudioSwitcher.sln
├── TidalAudioSwitcher.Launcher/     # TidalAudioSwitcher.exe
└── TidalAudioSwitcher.App/          # TidalAudioSwitcher.UI.exe
```

## Architektur

```text
TidalAudioSwitcher.exe (Launcher)
  → TIDAL.exe (--remote-debugging-port=9222)   [Pfad: %LocalAppData%\TIDAL\]
  → TidalAudioSwitcher.UI.exe                  [Pfad: gleicher Ordner wie Launcher]
        └── AudioSwitcherService → CDP :9222
```


## Disclaimer

This project is not affiliated with, endorsed by, or maintained by TIDAL.
TIDAL is a trademark of its respective owners.