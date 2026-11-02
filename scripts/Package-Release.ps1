# Builds and packages a framework-dependent release folder for end users.
# Usage: .\scripts\Package-Release.ps1 [-Configuration Release] [-Version 1.0]

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0"
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$LauncherProject = Join-Path $Root "TidalAudioSwitcher.Launcher\TidalAudioSwitcher.Launcher.csproj"
$BuildOut = Join-Path $Root "TidalAudioSwitcher.Launcher\bin\$Configuration\net8.0-windows"
$DistName = "TidalAudioSwitcher-v$Version"
$DistRoot = Join-Path $Root "release\$DistName"

Write-Host "Building $Configuration ..."
dotnet build $LauncherProject -c $Configuration --no-incremental | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path $BuildOut)) {
    throw "Build output not found: $BuildOut"
}

Write-Host "Packaging -> $DistRoot"
if (Test-Path $DistRoot) {
    Remove-Item $DistRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $DistRoot | Out-Null

$releaseFiles = @(
    "TidalAudioSwitcher.exe",
    "TidalAudioSwitcher.dll",
    "TidalAudioSwitcher.deps.json",
    "TidalAudioSwitcher.runtimeconfig.json",
    "TidalAudioSwitcher.UI.exe",
    "TidalAudioSwitcher.UI.dll",
    "TidalAudioSwitcher.UI.deps.json",
    "TidalAudioSwitcher.UI.runtimeconfig.json",
    "TidalAudioSwitcher.Shared.dll",
    "NAudio.dll",
    "NAudio.Asio.dll",
    "NAudio.Core.dll",
    "NAudio.Midi.dll",
    "NAudio.Wasapi.dll",
    "NAudio.WinForms.dll",
    "NAudio.WinMM.dll"
)

foreach ($file in $releaseFiles) {
    $source = Join-Path $BuildOut $file
    if (-not (Test-Path $source)) {
        throw "Required release file missing: $file"
    }
    Copy-Item $source $DistRoot
}

$assetsSource = Join-Path $Root "Assets"
$assetsTarget = Join-Path $DistRoot "Assets"
if (Test-Path $assetsSource) {
    Copy-Item $assetsSource $assetsTarget -Recurse
}
else {
    New-Item -ItemType Directory -Path $assetsTarget | Out-Null
}

Copy-Item (Join-Path $Root "README.md") $DistRoot

$launcherExe = Join-Path $DistRoot "TidalAudioSwitcher.exe"
$shortcutPath = Join-Path $DistRoot "Tidal Audio Switcher.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $launcherExe
$shortcut.WorkingDirectory = $DistRoot
$shortcut.IconLocation = "$launcherExe,0"
$shortcut.Description = "Tidal Audio Switcher"
$shortcut.Save()
Write-Host "Shortcut: $shortcutPath -> TidalAudioSwitcher.exe"

@'
@echo off
cd /d "%~dp0"
start "" "TidalAudioSwitcher.exe"
'@ | Set-Content -Path (Join-Path $DistRoot "Start Tidal Audio Switcher.bat") -Encoding ASCII

$zipPath = Join-Path $Root "release\$DistName.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path $DistRoot -DestinationPath $zipPath

Write-Host "Done."
Write-Host "  Folder: $DistRoot"
Write-Host "  Zip:    $zipPath"
