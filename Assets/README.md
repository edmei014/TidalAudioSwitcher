# Assets

`app.ico` — offizielles Anwendungsicon (Quelle: `audioswitcher.ico` im Projektroot).

Wird projektweit über `Directory.Build.props` eingebunden:

- `ApplicationIcon` → in `TidalAudioSwitcher.exe` und `TidalAudioSwitcher.UI.exe` **eingebettet** (Win32-Ressource)
- WPF-`Resource` → Fenster-Icon in XAML (`Icon="/Assets/app.ico"`)

Das Release-Skript kopiert diesen Ordner nach `Assets/` im Distributionspaket (Referenz für Nutzer/Dokumentation; Laufzeit benötigt die `.ico`-Datei nicht).
