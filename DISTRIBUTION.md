# Distribution — Tidal Audio Switcher

## Kurzantworten (Pfad & Installation)

| Frage | Antwort |
|-------|---------|
| Muss die App im TIDAL-Ordner liegen? | **Nein.** Der Installationsort ist frei wählbar. |
| Wie wird TIDAL.exe gefunden? | Über `%LocalAppData%\TIDAL\app-*\TIDAL.exe` (neueste `app-*`-Version). |
| Werden relative Pfade für TIDAL verwendet? | **Nein** für TIDAL. **Ja** für die eigene UI: `AppContext.BaseDirectory\TidalAudioSwitcher.UI.exe`. |
| Beliebiger Installationsordner? | **Ja**, z. B. `C:\Program Files\TidalAudioSwitcher` oder `D:\Tools\TidalAudioSwitcher`. |

### Was an den Installationsort gebunden ist

- `TidalAudioSwitcher.exe` und `TidalAudioSwitcher.UI.exe` müssen **im selben Ordner** liegen (plus zugehörige `.dll` / `.json`).
- Benutzereinstellungen werden **nicht** im Installationsordner gespeichert, sondern unter  
  `%LocalAppData%\TidalAudioSwitcher\settings.json`.
- Diagnose-Log: `%TEMP%\TidalAudioSwitcher.log`.

### Was unabhängig vom Installationsort ist

- Suche nach TIDAL Desktop (immer unter `%LocalAppData%\TIDAL\`).
- Windows-Audiogeräte (systemweit).
- CDP-Verbindung zu `http://127.0.0.1:9222`.

---

## Release erstellen

```powershell
cd TidalAudioSwitcher
.\scripts\Package-Release.ps1 -Configuration Release -Version 1.0
```

Erzeugt:

```text
release/
├── TidalAudioSwitcher-v1.0/          ← Ordner für Endnutzer
│   ├── TidalAudioSwitcher.exe
│   ├── TidalAudioSwitcher.UI.exe
│   ├── *.dll
│   ├── *.deps.json
│   ├── *.runtimeconfig.json
│   ├── Assets/
│   ├── README.md
│   ├── DISTRIBUTION.md
│   └── Start Tidal Audio Switcher.bat
└── TidalAudioSwitcher-v1.0.zip       ← GitHub-Release-Anhang
```

---

## Dateien für Endnutzer (verteilen)

| Datei | Zweck |
|-------|--------|
| `TidalAudioSwitcher.exe` | **Einstiegspunkt** — Launcher |
| `TidalAudioSwitcher.UI.exe` | UI-Prozess (vom Launcher gestartet) |
| `TidalAudioSwitcher.dll` | Launcher-Assembly |
| `TidalAudioSwitcher.UI.dll` | UI-Assembly |
| `TidalAudioSwitcher.deps.json` | Abhängigkeitsmanifest Launcher |
| `TidalAudioSwitcher.UI.deps.json` | Abhängigkeitsmanifest UI |
| `TidalAudioSwitcher.runtimeconfig.json` | .NET-Runtime-Konfiguration Launcher |
| `TidalAudioSwitcher.UI.runtimeconfig.json` | .NET-Runtime-Konfiguration UI |
| `NAudio*.dll` (7 Dateien) | Audio-Geräte-Steuerung |
| `Assets/` | Optional: `app.ico` und sonstige statische Dateien |
| `README.md` | Bedienung & Voraussetzungen |

**Nicht** ins Release-Paket:

| Datei / Ordner | Grund |
|----------------|--------|
| `*.pdb` | Nur Debugging |
| `obj/`, `bin/` (Quell-build) | Build-Artefakte |
| Quellcode (`.cs`, `.xaml`, `.csproj`) | Entwicklung |
| `PolicyProbe/`, `scripts/` | Intern / Entwicklung |

---

## Laufzeitvoraussetzung

Das Release ist **framework-dependent**: Endnutzer benötigen die  
[.NET 8 Desktop Runtime (Windows x64)](https://dotnet.microsoft.com/download/dotnet/8.0).

Ohne installierte Runtime erscheint beim Start eine Meldung, dass die Runtime fehlt.

Optional für später: self-contained publish (größeres Paket, keine separate Runtime nötig).

---

## Installer sinnvoll?

| Variante | v1.0 | Später |
|----------|------|--------|
| **ZIP entpacken** | ✅ Empfohlen — einfach, transparent | Weiterhin möglich |
| **Portable Ordner** | ✅ Nutzer wählt Pfad selbst | — |
| **Installer (Inno Setup / MSI)** | Optional | Sinnvoll für `Program Files`, Startmenü, Deinstaller, Runtime-Check |

Für GitHub Release v1.0 reicht **ZIP + README**. Ein Installer lohnt sich ab v1.x, wenn Häufigkeit der Installation steigt oder Program Files / Autostart gewünscht sind.

---

## GitHub Release Checkliste

1. `.\scripts\Package-Release.ps1 -Version 1.0` ausführen
2. `release/TidalAudioSwitcher-v1.0.zip` als Release-Asset hochladen
3. Release Notes: .NET 8 Runtime, TIDAL Desktop, VB-Audio optional
4. Tag `v1.0.0` (siehe `Directory.Build.props`)
