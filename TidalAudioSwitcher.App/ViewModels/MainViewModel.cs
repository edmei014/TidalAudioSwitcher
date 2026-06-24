using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using TidalAudioSwitcher.App;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services;

namespace TidalAudioSwitcher.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAudioSwitcherService _switcher;
    private readonly SettingsService _settings;
    private readonly ITidalCdpService _tidalCdp;
    private readonly TidalLaunchService _tidalLaunch;
    private readonly CollectionViewSource _tidalDevicesSource;
    private readonly CollectionViewSource _silentDevicesSource;
    private AudioEndpoint? _selectedTidalDevice;
    private AudioEndpoint? _selectedSilentDevice;
    private AudioMode _activeMode = AudioMode.Normal;
    private string _statusText = "Bereit.";
    private string _logText = string.Empty;
    private bool _isBusy;
    private bool _initialized;
    private bool _suppressSettingsSave;
    private bool _isCompactView;

    public MainViewModel(
        IAudioSwitcherService switcher,
        SettingsService settings,
        ITidalCdpService tidalCdp,
        TidalLaunchService tidalLaunch)
    {
        _switcher = switcher;
        _settings = settings;
        _tidalCdp = tidalCdp;
        _tidalLaunch = tidalLaunch;
        WindowsDevices = new ObservableCollection<AudioEndpoint>();

        _tidalDevicesSource = new CollectionViewSource { Source = WindowsDevices };
        _tidalDevicesSource.Filter += FilterTidalDevice;
        TidalDeviceChoices = _tidalDevicesSource.View;

        _silentDevicesSource = new CollectionViewSource { Source = WindowsDevices };
        _silentDevicesSource.Filter += FilterSilentDevice;
        SilentDeviceChoices = _silentDevicesSource.View;

        MusicModeCommand = new AsyncRelayCommand(() => ApplyModeAsync(AudioMode.Music), () => CanApplyMode(AudioMode.Music));
        NormalModeCommand = new AsyncRelayCommand(() => ApplyModeAsync(AudioMode.Normal), () => CanApplyMode(AudioMode.Normal));
        ToggleModeCommand = new AsyncRelayCommand(
            () => ApplyModeAsync(IsMusicModeActive ? AudioMode.Normal : AudioMode.Music),
            CanToggleMode);
    }

    public ObservableCollection<AudioEndpoint> WindowsDevices { get; }

    public ICollectionView TidalDeviceChoices { get; }

    public ICollectionView SilentDeviceChoices { get; }

    public AudioEndpoint? SelectedTidalDevice
    {
        get => _selectedTidalDevice;
        set
        {
            if (SetField(ref _selectedTidalDevice, value))
            {
                RefreshSilentDeviceFilter();
                RaiseCanExecuteChanged();
                PersistDeviceSelection();
            }
        }
    }

    public AudioEndpoint? SelectedSilentDevice
    {
        get => _selectedSilentDevice;
        set
        {
            if (SetField(ref _selectedSilentDevice, value))
            {
                RefreshTidalDeviceFilter();
                RaiseCanExecuteChanged();
                PersistDeviceSelection();
            }
        }
    }

    public AudioMode ActiveMode
    {
        get => _activeMode;
        private set
        {
            if (SetField(ref _activeMode, value))
            {
                OnPropertyChanged(nameof(IsMusicModeActive));
                OnPropertyChanged(nameof(IsNormalModeActive));
            }
        }
    }

    public bool IsMusicModeActive => ActiveMode == AudioMode.Music;

    public bool IsNormalModeActive => ActiveMode == AudioMode.Normal;

    public bool IsCompactView
    {
        get => _isCompactView;
        set
        {
            if (_suppressSettingsSave)
            {
                SetField(ref _isCompactView, value);
                return;
            }

            if (SetField(ref _isCompactView, value))
            {
                _settings.Update(s => s.CompactMode = value);
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetField(ref _logText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand MusicModeCommand { get; }
    public ICommand NormalModeCommand { get; }
    public ICommand ToggleModeCommand { get; }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadDevicesAsync();
        await CheckTidalConnectionAsync();
    }

    private async Task CheckTidalConnectionAsync()
    {
        var health = await _tidalCdp.CheckHealthAsync();
        if (health.IsReady)
        {
            return;
        }

        StartupLogger.Log($"TIDAL CDP health: {health.Status} — {health.Detail ?? health.UserMessage}");

        if (ShouldOfferAutoStart(health))
        {
            await OfferTidalLaunchAsync();
            return;
        }

        MessageBox.Show(
            health.UserMessage,
            "TIDAL-Verbindung",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static bool ShouldOfferAutoStart(TidalCdpHealthResult health) =>
        health.Status is TidalCdpHealthStatus.TidalNotRunning
            or TidalCdpHealthStatus.TidalRunningWithoutCdp;

    private async Task<bool> OfferTidalLaunchAsync()
    {
        const string promptMessage = "TIDAL wurde nicht mit den benötigten Startparametern gestartet.";

        while (true)
        {
            var launcherPath = TidalLaunchService.FindLauncherExecutable();
            var dialog = new TidalLaunchDialog(promptMessage, launcherPath)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            if (dialog.Result == TidalLaunchDialogResult.LaunchLauncher && launcherPath is not null)
            {
                TidalLaunchService.StartLauncherExecutable(launcherPath);
                return false;
            }

            if (dialog.Result != TidalLaunchDialogResult.AutoStart)
            {
                return false;
            }

            try
            {
                IsBusy = true;
                var outcome = await _tidalLaunch.StartTidalAndWaitForCdpAsync();
                switch (outcome)
                {
                    case TidalLaunchOutcome.Success:
                        return true;
                    case TidalLaunchOutcome.TidalAlreadyRunningNeedsClose:
                        MessageBox.Show(
                            "TIDAL läuft bereits ohne Debug-Port.\n\n" +
                            "Bitte TIDAL vollständig schließen und erneut auf „Automatisch starten“ klicken.",
                            "TIDAL-Verbindung",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        break;
                    case TidalLaunchOutcome.TidalNotFound:
                        MessageBox.Show(
                            "TIDAL.exe wurde auf diesem PC nicht gefunden.\n\n" +
                            $"Erwartet unter:\n{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\TIDAL\\app-*\\TIDAL.exe",
                            "TIDAL-Verbindung",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    case TidalLaunchOutcome.Timeout:
                        MessageBox.Show(
                            "TIDAL antwortet nicht rechtzeitig auf dem Debug-Port.\n\n" +
                            "Bitte erneut versuchen, sobald TIDAL vollständig geladen ist.",
                            "TIDAL-Verbindung",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        break;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private Task LoadDevicesAsync()
    {
        return RunBusyAsync("Lade Windows-Audiogeräte …", () =>
        {
            var saved = _settings.Load();
            var devices = _switcher.ListWindowsDevices();
            WindowsDevices.Clear();
            foreach (var device in devices)
            {
                WindowsDevices.Add(device);
            }

            RefreshAllDeviceFilters();
            RestoreDeviceSelection(saved);

            ActiveMode = saved.ActiveMode;
            SetField(ref _isCompactView, saved.CompactMode);
            OnPropertyChanged(nameof(IsCompactView));

            StatusText = $"{WindowsDevices.Count} Windows-Audiogerät(e) geladen.";
            return Task.CompletedTask;
        });
    }

    private void RestoreDeviceSelection(AppSettings saved)
    {
        StartupLogger.Log($"Device restore: settings file = {_settings.SettingsPath}");
        StartupLogger.Log($"Device restore: saved tidalDeviceId = {saved.TidalDeviceId ?? "(null)"}");
        StartupLogger.Log($"Device restore: saved silentDeviceId = {saved.SilentDeviceId ?? "(null)"}");

        var fallbackTidal = WindowsDevices.FirstOrDefault(d => d.IsDefault)
            ?? WindowsDevices.FirstOrDefault();
        var fallbackSilent = PickDefaultSilentDevice();

        var tidal = ResolveDevice(saved.TidalDeviceId, fallbackTidal, "TIDAL");
        var silent = ResolveDevice(saved.SilentDeviceId, fallbackSilent, "Silent");

        _suppressSettingsSave = true;
        try
        {
            _selectedTidalDevice = tidal;
            _selectedSilentDevice = silent;
        }
        finally
        {
            _suppressSettingsSave = false;
        }

        RefreshAllDeviceFilters();
        NotifyDeviceSelectionChanged();
        PersistDeviceSelection(logRestore: true);
    }

    private void NotifyDeviceSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedTidalDevice));
        OnPropertyChanged(nameof(SelectedSilentDevice));
    }

    private AudioEndpoint? ResolveDevice(string? savedId, AudioEndpoint? fallback, string label)
    {
        if (string.IsNullOrWhiteSpace(savedId))
        {
            StartupLogger.Log($"Device restore: {label} — keine gespeicherte ID, Fallback = {fallback?.FriendlyName ?? "(keines)"}");
            AppendLog($"Device restore: {label} — keine gespeicherte ID → Fallback: {fallback?.FriendlyName ?? "(keines)"}");
            return fallback;
        }

        var found = FindDeviceById(savedId);
        if (found is not null)
        {
            StartupLogger.Log($"Device restore: {label} — OK [{savedId}] → {found.FriendlyName}");
            AppendLog($"Device restore: {label} — OK [{savedId}] → {found.FriendlyName}");
            return found;
        }

        StartupLogger.Log($"Device restore: {label} — FEHLGESCHLAGEN [{savedId}], Fallback = {fallback?.FriendlyName ?? "(keines)"}");
        AppendLog($"Device restore: {label} — nicht gefunden [{savedId}] → Fallback: {fallback?.FriendlyName ?? "(keines)"}");
        return fallback;
    }

    public void SaveSettingsOnExit()
    {
        StartupLogger.Log("Device save: app exit flush");
        PersistDeviceSelection(logRestore: false);
        _settings.Update(s =>
        {
            s.ActiveMode = ActiveMode;
            s.CompactMode = IsCompactView;
        });
    }

    private AudioEndpoint? FindDeviceById(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return WindowsDevices.FirstOrDefault(d =>
            string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    private AudioEndpoint? PickDefaultSilentDevice()
    {
        var cable = WindowsDevices.FirstOrDefault(d =>
            d.Id != SelectedTidalDevice?.Id
            && (d.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
                || d.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)));
        if (cable is not null)
        {
            return cable;
        }

        return WindowsDevices.FirstOrDefault(d => d.Id != SelectedTidalDevice?.Id)
            ?? WindowsDevices.FirstOrDefault();
    }

    private Task ApplyModeAsync(AudioMode mode)
    {
        if (SelectedTidalDevice is null)
        {
            StatusText = "Bitte ein TIDAL Device auswählen.";
            return Task.CompletedTask;
        }

        if (mode == AudioMode.Music && SelectedSilentDevice is null)
        {
            StatusText = "Bitte ein Silent Device auswählen.";
            return Task.CompletedTask;
        }

        if (mode == AudioMode.Music && DevicesConflict)
        {
            StatusText = "Silent Device und TIDAL Device müssen unterschiedlich sein.";
            MessageBox.Show(
                "Silent Device und TIDAL Device müssen unterschiedlich sein.",
                "Tidal Audio Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        var label = mode == AudioMode.Music ? "Music Mode" : "Normal Mode";
        return RunBusyAsync($"{label} wird angewendet …", async () =>
        {
            if (mode == AudioMode.Music)
            {
                var (windowsLog, tidalStep) = await _switcher.ApplyMusicModeAsync(
                    SelectedTidalDevice,
                    SelectedSilentDevice!);

                var sb = new StringBuilder();
                sb.AppendLine(windowsLog.ToDisplayText());
                sb.AppendLine(tidalStep);
                LogText = sb.ToString();
                StatusText = windowsLog.VerifiedMatch
                    ? $"Music Mode OK — Windows-Standard: {windowsLog.VerifiedDefault?.FriendlyName}"
                    : "Music Mode — Verifikation fehlgeschlagen (Log prüfen)";
                if (windowsLog.VerifiedMatch)
                {
                    SetActiveMode(AudioMode.Music);
                }
            }
            else
            {
                var (windowsLog, tidalStep) = await _switcher.ApplyNormalModeAsync(SelectedTidalDevice);

                var sb = new StringBuilder();
                sb.AppendLine(tidalStep);
                sb.AppendLine(windowsLog.ToDisplayText());
                LogText = sb.ToString();
                StatusText = windowsLog.VerifiedMatch
                    ? $"Normal Mode OK — Windows-Standard: {windowsLog.VerifiedDefault?.FriendlyName}"
                    : "Normal Mode — Verifikation fehlgeschlagen (Log prüfen)";
                if (windowsLog.VerifiedMatch)
                {
                    SetActiveMode(AudioMode.Normal);
                }
            }

            ReloadDevicesPreserveSelection();
        });
    }

    private void ReloadDevicesPreserveSelection()
    {
        var tidalId = SelectedTidalDevice?.Id;
        var silentId = SelectedSilentDevice?.Id;
        var devices = _switcher.ListWindowsDevices();

        WindowsDevices.Clear();
        foreach (var device in devices)
        {
            WindowsDevices.Add(device);
        }

        _suppressSettingsSave = true;
        try
        {
            _selectedTidalDevice = WindowsDevices.FirstOrDefault(d =>
                string.Equals(d.Id, tidalId, StringComparison.OrdinalIgnoreCase));
            _selectedSilentDevice = WindowsDevices.FirstOrDefault(d =>
                string.Equals(d.Id, silentId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _suppressSettingsSave = false;
        }

        RefreshAllDeviceFilters();
        NotifyDeviceSelectionChanged();
        PersistDeviceSelection(logRestore: false);
    }

    private void SetActiveMode(AudioMode mode)
    {
        ActiveMode = mode;
        _settings.Update(s => s.ActiveMode = mode);
    }

    private void PersistDeviceSelection(bool logRestore = false)
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        var tidalId = SelectedTidalDevice?.Id;
        var silentId = SelectedSilentDevice?.Id;

        _settings.Update(s =>
        {
            s.TidalDeviceId = tidalId;
            s.SilentDeviceId = silentId;
        });

        StartupLogger.Log($"Device save: tidalDeviceId = {tidalId ?? "(null)"}, silentDeviceId = {silentId ?? "(null)"} → {_settings.SettingsPath}");
        if (logRestore)
        {
            AppendLog($"Device save: TIDAL [{tidalId ?? "(null)"}], Silent [{silentId ?? "(null)"}]");
        }
    }

    private bool DevicesConflict =>
        SelectedTidalDevice is not null
        && SelectedSilentDevice is not null
        && string.Equals(SelectedTidalDevice.Id, SelectedSilentDevice.Id, StringComparison.OrdinalIgnoreCase);

    private bool CanToggleMode() =>
        !IsBusy
        && SelectedTidalDevice is not null
        && (IsMusicModeActive ? CanApplyMode(AudioMode.Normal) : CanApplyMode(AudioMode.Music));

    private bool CanApplyMode(AudioMode mode) =>
        !IsBusy
        && SelectedTidalDevice is not null
        && (mode != AudioMode.Music || (SelectedSilentDevice is not null && !DevicesConflict));

    private void FilterTidalDevice(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item is AudioEndpoint ep
            && (SelectedSilentDevice is null
                || !string.Equals(ep.Id, SelectedSilentDevice.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void FilterSilentDevice(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item is AudioEndpoint ep
            && (SelectedTidalDevice is null
                || !string.Equals(ep.Id, SelectedTidalDevice.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshTidalDeviceFilter() => TidalDeviceChoices.Refresh();

    private void RefreshSilentDeviceFilter() => SilentDeviceChoices.Refresh();

    private void RefreshAllDeviceFilters()
    {
        RefreshSilentDeviceFilter();
        RefreshTidalDeviceFilter();
    }

    private async Task RunBusyAsync(string busyText, Func<Task> action)
    {
        try
        {
            IsBusy = true;
            StatusText = busyText;
            await action();
        }
        catch (TidalCdpUserException)
        {
            if (await OfferTidalLaunchAsync())
            {
                IsBusy = true;
                StatusText = busyText;
                await action();
                return;
            }

            StatusText = "TIDAL-Verbindung nicht verfügbar.";
        }
        catch (Exception ex)
        {
            StatusText = "Ein unerwarteter Fehler ist aufgetreten.";
            AppendLog(ex.ToString());
            StartupLogger.LogException("MainViewModel.RunBusyAsync", ex);
            MessageBox.Show(
                "Ein unerwarteter Fehler ist aufgetreten.\n\nDetails wurden in der Protokolldatei gespeichert.",
                "Tidal Audio Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string line)
    {
        LogText = string.IsNullOrEmpty(LogText) ? line : $"{LogText}\n{line}";
    }

    private void RaiseCanExecuteChanged()
    {
        (MusicModeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NormalModeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ToggleModeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
