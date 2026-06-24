@echo off
setlocal
cd /d "%~dp0"
if exist "TidalAudioSwitcher.Launcher\bin\Release\net8.0-windows\TidalAudioSwitcher.exe" (
  start "" "TidalAudioSwitcher.Launcher\bin\Release\net8.0-windows\TidalAudioSwitcher.exe"
  exit /b 0
)
if exist "TidalAudioSwitcher.exe" (
  start "" "TidalAudioSwitcher.exe"
  exit /b 0
)
echo Tidal Audio Switcher nicht gefunden. Bitte zuerst bauen:
echo   dotnet build -c Release
pause
