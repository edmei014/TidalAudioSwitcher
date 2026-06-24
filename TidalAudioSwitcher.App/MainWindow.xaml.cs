using System.Windows;
using System.Windows.Input;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services;
using TidalAudioSwitcher.App.ViewModels;

namespace TidalAudioSwitcher.App;

public partial class MainWindow : Window
{
    private const string PathStartupCompact = "A-startup";
    private const string PathRuntimeCompact = "B-runtime";
    private const double CompactWidth = 144;
    private const double CompactHeight = 54;
    private const double DefaultExpandedWidth = 720;
    private const double DefaultExpandedHeight = 210;

    private WindowLayoutSnapshot? _expandedSnapshot;
    private bool _startCompactRequested;
    private bool _initialForegroundApplied;

    public MainWindow(bool startCompact = false)
    {
        _startCompactRequested = startCompact;
        StartupLogger.Log("MainWindow ctor Begin");
        try
        {
            InitializeComponent();
            StartupLogger.Log("MainWindow InitializeComponent OK");

            DataContext = ((App)Application.Current).Services.ViewModel;
            Loaded += OnLoaded;
            Closing += (_, _) => PersistWindowState();
            StartupLogger.Log("MainWindow ctor Complete");
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("MainWindow ctor Failed", ex);
            throw;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ((App)Application.Current).Services.InitializeAsync();

            var settings = ((App)Application.Current).Services.Settings.Load();
            RestoreWindowPlacement(settings);
            WindowStateDiagnostics.Log(this, PathStartupCompact, "OnLoaded:before-EnterCompactView", settings.CompactMode);

            if (DataContext is MainViewModel vm
                && (_startCompactRequested || vm.IsCompactView))
            {
                EnterCompactView(PathStartupCompact);
            }
            else
            {
                WindowStateDiagnostics.Log(
                    this,
                    PathStartupCompact,
                    $"OnLoaded:EnterCompactView-skipped(startCompact={_startCompactRequested},vmCompact={(DataContext is MainViewModel m && m.IsCompactView)})",
                    settings.CompactMode);
                ApplyInitialForegroundDeferred();
            }
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("MainWindow OnLoaded Failed", ex);
            MessageBox.Show(
                "Die Anwendung konnte nicht initialisiert werden.\n\nDetails wurden in der Protokolldatei gespeichert.",
                "Initialisierung fehlgeschlagen",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyInitialForegroundDeferred()
    {
        Dispatcher.BeginInvoke(ApplyInitialForeground, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ApplyInitialForeground()
    {
        if (_initialForegroundApplied)
        {
            return;
        }

        if (DataContext is MainViewModel { IsCompactView: true })
        {
            return;
        }

        if (WindowForegroundHelper.TryBringToForeground(this, persistentTopmost: false))
        {
            _initialForegroundApplied = true;
            StartupLogger.Log("Window foreground applied (compact=false)");
            return;
        }

        Dispatcher.BeginInvoke(ApplyInitialForeground, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BringCompactToForeground(string path)
    {
        WindowStateDiagnostics.Log(this, path, "BringCompactToForeground:scheduled");

        void Apply()
        {
            WindowStateDiagnostics.Log(this, path, "BringCompactToForeground:before");
            WindowForegroundHelper.BringToForeground(this, persistentTopmost: true);
            WindowStateDiagnostics.Log(this, path, "BringCompactToForeground:after");

            if (new System.Windows.Interop.WindowInteropHelper(this).Handle == nint.Zero)
            {
                WindowStateDiagnostics.Log(this, path, "BringCompactToForeground:retry-no-hwnd");
                Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            var settledTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            settledTimer.Tick += (_, _) =>
            {
                settledTimer.Stop();
                WindowStateDiagnostics.Log(this, path, "BringCompactToForeground:settled-500ms");
            };
            settledTimer.Start();
        }

        Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void EnterCompactView_Click(object sender, RoutedEventArgs e) => EnterCompactView(PathRuntimeCompact);

    private void ExitCompactView_Click(object sender, RoutedEventArgs e) => ExitCompactView();

    private void EnterCompactView(string path)
    {
        WindowStateDiagnostics.Log(this, path, "EnterCompactView:begin");

        if (DataContext is MainViewModel vm && vm.IsCompactView && ResizeMode == ResizeMode.NoResize)
        {
            WindowStateDiagnostics.Log(this, path, "EnterCompactView:branch=early-return");
            ApplyCompactChrome(path);
            BringCompactToForeground(path);
            WindowStateDiagnostics.Log(this, path, "EnterCompactView:end");
            return;
        }

        WindowStateDiagnostics.Log(this, path, "EnterCompactView:branch=full");
        _expandedSnapshot = WindowLayoutSnapshot.Capture(this);
        ApplyCompactChrome(path);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsCompactView = true;
        }

        PersistWindowState();
        BringCompactToForeground(path);
        WindowStateDiagnostics.Log(this, path, "EnterCompactView:end");
    }

    private void ExitCompactView()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsCompactView = false;
        }

        if (_expandedSnapshot is not null)
        {
            _expandedSnapshot.Restore(this);
            _expandedSnapshot = null;
            ApplyExpandedChrome(restoreLayout: false);
        }
        else
        {
            ApplyExpandedChrome(restoreLayout: true);
        }

        PersistWindowState();
    }

    private void ApplyExpandedChrome(bool restoreLayout)
    {
        Background = System.Windows.Media.Brushes.Transparent;

        if (!restoreLayout)
        {
            return;
        }

        ResizeMode = ResizeMode.CanMinimize;
        Topmost = false;
        MinWidth = 600;
        MinHeight = 180;
        Width = DefaultExpandedWidth;
        Height = DefaultExpandedHeight;
    }

    private void ApplyCompactChrome(string path)
    {
        WindowStateDiagnostics.Log(this, path, "ApplyCompactChrome:before");
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = CompactWidth;
        MinHeight = CompactHeight;
        Width = CompactWidth;
        Height = CompactHeight;
        SizeToContent = SizeToContent.Manual;
        WindowStateDiagnostics.Log(this, path, "ApplyCompactChrome:after");
    }

    private void CompactDragHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            PersistWindowState();
        }
    }

    private void ExpandedDragHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void RestoreWindowPlacement(AppSettings settings)
    {
        var isCompact = settings.CompactMode || _startCompactRequested;
        var width = isCompact
            ? CompactWidth
            : settings.WindowWidth ?? DefaultExpandedWidth;
        var height = isCompact
            ? CompactHeight
            : settings.WindowHeight ?? DefaultExpandedHeight;

        if (!isCompact)
        {
            Width = width;
            Height = height;
        }

        if (settings.WindowLeft.HasValue
            && settings.WindowTop.HasValue
            && WindowPlacementHelper.IsPositionVisible(
                settings.WindowLeft.Value,
                settings.WindowTop.Value,
                width,
                height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
            StartupLogger.Log(
                $"Window restore: position ({Left}, {Top}), compact={isCompact}, size {width}x{height}");
        }
        else
        {
            WindowPlacementHelper.CenterOnScreen(this);
            StartupLogger.Log(
                $"Window restore: fallback CenterScreen (saved=({settings.WindowLeft}, {settings.WindowTop}))");
        }
    }

    internal void PersistWindowState()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var settings = ((App)Application.Current).Services.Settings;
        var left = Left;
        var top = Top;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        settings.Update(s =>
        {
            s.CompactMode = vm.IsCompactView;
            s.WindowLeft = left;
            s.WindowTop = top;
            if (!vm.IsCompactView)
            {
                s.WindowWidth = width;
                s.WindowHeight = height;
            }
        });

        StartupLogger.Log(
            $"Window save: compactMode={vm.IsCompactView}, left={left}, top={top}, size={width}x{height}");
    }

    private sealed class WindowLayoutSnapshot
    {
        public required double Left { get; init; }
        public required double Top { get; init; }
        public required double Width { get; init; }
        public required double Height { get; init; }
        public required ResizeMode ResizeMode { get; init; }
        public required bool Topmost { get; init; }
        public required double MinWidth { get; init; }
        public required double MinHeight { get; init; }

        public static WindowLayoutSnapshot Capture(Window window) =>
            new()
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height,
                ResizeMode = window.ResizeMode,
                Topmost = window.Topmost,
                MinWidth = window.MinWidth,
                MinHeight = window.MinHeight
            };

        public void Restore(Window window)
        {
            window.ResizeMode = ResizeMode;
            window.Topmost = Topmost;
            window.MinWidth = MinWidth;
            window.MinHeight = MinHeight;
            window.Width = Width;
            window.Height = Height;
            window.Left = Left;
            window.Top = Top;
        }
    }
}
