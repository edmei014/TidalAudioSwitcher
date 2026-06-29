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
using TidalAudioSwitcher.Shared;

namespace TidalAudioSwitcher.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAudioSwitcherService _switcher;
    private readonly SettingsService _settings;
    private readonly ITidalCdpService _tidalCdp;
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
        ITidalCdpService tidalCdp)
    {
        _switcher = switcher;
        _settings = settings;
        _tidalCdp = tidalCdp;
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
        RestartTidalCommand = new AsyncRelayCommand(RestartTidalAsync, () => !IsBusy);
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
    public ICommand RestartTidalCommand { get; }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadDevicesAsync();
    }

    private Task RestartTidalAsync()
    {
        return RunBusyAsync("TIDAL wird neu gestartet …", async () =>
        {
            var progress = new Progress<string>(message => StatusText = message);
            var result = await TidalBootstrap.RestartTidalAsync(progress, TimeSpan.FromSeconds(45));

            switch (result)
            {
                case TidalBootstrapResult.Success:
                    break;
                case TidalBootstrapResult.TidalNotFound:
                    throw new TidalCdpUserException(
                        "TIDAL.exe wurde auf diesem PC nicht gefunden.\n\n" +
                        $"Erwartet unter:\n{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\TIDAL\\app-*\\TIDAL.exe");
                case TidalBootstrapResult.CloseFailed:
                    throw new TidalCdpUserException(
                        "TIDAL konnte nicht vollständig beendet werden.\n\n" +
                        "Bitte TIDAL manuell schließen und erneut auf „Restart TIDAL“ klicken.");
                case TidalBootstrapResult.CdpTimeout:
                    throw new TidalCdpUserException(
                        "TIDAL antwortet nicht rechtzeitig auf dem Debug-Port.\n\n" +
                        "Bitte erneut versuchen, sobald TIDAL vollständig geladen ist.");
                default:
                    throw new TidalCdpUserException("TIDAL konnte nicht neu gestartet werden.");
            }

            var health = await _tidalCdp.CheckHealthAsync();
            if (!health.IsReady)
            {
                throw new TidalCdpUserException(health.UserMessage);
            }

            ReloadDevicesPreserveSelection();
            StatusText = "TIDAL neu gestartet — bereit.";
            StartupLogger.Log("TIDAL restart completed from UI");
        });
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
            // Compact UI is activated only via MainWindow.EnterCompactView so startup
            // matches the runtime Expanded→Compact sequence (ApplyCompactChrome first).
            StartupLogger.Log($"Device restore: saved CompactMode={saved.CompactMode} (UI stays expanded until EnterCompactView)");

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
        catch (TidalCdpUserException ex)
        {
            StartupLogger.Log($"TIDAL CDP runtime failure: {ex.Message}");
            StatusText = "TIDAL-Verbindung unterbrochen.";
            AppendLog(ex.Message);
            MessageBox.Show(
                ex.Message,
                "TIDAL-Verbindung",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        (RestartTidalCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
