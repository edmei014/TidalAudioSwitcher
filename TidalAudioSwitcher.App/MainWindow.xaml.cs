using System.Windows;
using System.Windows.Input;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services;
using TidalAudioSwitcher.App.ViewModels;

namespace TidalAudioSwitcher.App;

public partial class MainWindow : Window
{
    private const string PathExpandedStartup = "A-expanded";
    private const string PathCompactStartup = "B-startup";
    private const string PathRuntimeCompact = "A-runtime-compact";
    private const double CompactWidth = 144;
    private const double CompactHeight = 54;
    private const double DefaultExpandedWidth = 820;
    private const double DefaultExpandedHeight = 230;

    private WindowLayoutSnapshot? _expandedSnapshot;
    private bool _startCompactRequested;
    private bool _initialForegroundApplied;

    public MainWindow(bool startCompact = false)
    {
        _startCompactRequested = startCompact;
        WindowLifecycleTrace.Log(this, "startup", "MainWindow.ctor:begin");
        try
        {
            InitializeComponent();
            WindowLifecycleTrace.Log(this, "startup", "MainWindow.ctor:after-InitializeComponent");

            DataContext = ((App)Application.Current).Services.ViewModel;
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += (_, _) => PersistWindowState();
            WindowLifecycleTrace.Log(this, "startup", "MainWindow.ctor:complete");
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("MainWindow ctor Failed", ex);
            throw;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowLifecycleTrace.Log(this, "startup", "SourceInitialized");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowLifecycleTrace.Log(this, "startup", "Loaded:begin");
        try
        {
            WindowLifecycleTrace.Log(this, "startup", "Loaded:before-InitializeAsync");
            await ((App)Application.Current).Services.InitializeAsync();
            WindowLifecycleTrace.Log(this, "startup", "Loaded:after-InitializeAsync");

            var settings = ((App)Application.Current).Services.Settings.Load();
            var lifecyclePath = _startCompactRequested || settings.CompactMode
                ? PathCompactStartup
                : PathExpandedStartup;

            RestoreWindowPlacement(settings, lifecyclePath);
            WindowLifecycleTrace.Log(this, lifecyclePath, "Loaded:after-RestoreWindowPlacement");

            var startupCompact = _startCompactRequested || settings.CompactMode;
            if (startupCompact)
            {
                ScheduleStartupCompactTransition();
            }
            else
            {
                WindowLifecycleTrace.Log(this, lifecyclePath, "Loaded:expanded-startup");
                ApplyInitialForegroundDeferred(lifecyclePath);
            }

            WindowLifecycleTrace.Log(this, lifecyclePath, "Loaded:end");
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

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        WindowLifecycleTrace.Log(this, "startup", "ContentRendered");
    }

    private void ApplyInitialForegroundDeferred(string path)
    {
        Dispatcher.BeginInvoke(() => ApplyInitialForeground(path), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ApplyInitialForeground(string path)
    {
        WindowLifecycleTrace.Log(this, path, "ApplyInitialForeground:begin");
        if (_initialForegroundApplied)
        {
            return;
        }

        if (WindowForegroundHelper.TryBringToForeground(this, path, "ApplyInitialForeground", persistentTopmost: false))
        {
            _initialForegroundApplied = true;
            WindowLifecycleTrace.Log(this, path, "ApplyInitialForeground:applied");
        }
        else
        {
            Dispatcher.BeginInvoke(() => ApplyInitialForeground(path), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void BringCompactToForeground(string path)
    {
        WindowLifecycleTrace.Log(this, path, "BringCompactToForeground:scheduled");

        void Apply()
        {
            WindowLifecycleTrace.Log(this, path, "BringCompactToForeground:before");
            WindowForegroundHelper.BringToForeground(this, path, "BringCompactToForeground", persistentTopmost: true);
            TaskbarIconHelper.ApplyExeIcon(this, path, "BringCompactToForeground");
            WindowLifecycleTrace.Log(this, path, "BringCompactToForeground:after");

            if (new System.Windows.Interop.WindowInteropHelper(this).Handle == nint.Zero)
            {
                WindowLifecycleTrace.Log(this, path, "BringCompactToForeground:retry-no-hwnd");
                Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ScheduleStartupCompactTransition()
    {
        // Defer until after OnLoaded returns so ContentRendered completes with expanded UI,
        // matching path A before the user manually enters compact mode.
        WindowLifecycleTrace.Log(this, PathCompactStartup, "ScheduleStartupCompactTransition");
        Dispatcher.BeginInvoke(
            () => EnterCompactView(PathCompactStartup),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void EnterCompactView_Click(object sender, RoutedEventArgs e) => EnterCompactView(PathRuntimeCompact);

    private void ExitCompactView_Click(object sender, RoutedEventArgs e) => ExitCompactView();

    private void EnterCompactView(string path)
    {
        WindowLifecycleTrace.Log(this, path, "EnterCompactView:begin");

        if (DataContext is MainViewModel vm && vm.IsCompactView && ResizeMode == ResizeMode.NoResize)
        {
            WindowLifecycleTrace.Log(this, path, "EnterCompactView:branch=early-return");
            ApplyCompactChrome(path);
            BringCompactToForeground(path);
            WindowLifecycleTrace.Log(this, path, "EnterCompactView:end");
            return;
        }

        WindowLifecycleTrace.Log(this, path, "EnterCompactView:branch=full");
        _expandedSnapshot = WindowLayoutSnapshot.Capture(this);
        ApplyCompactChrome(path);

        if (DataContext is MainViewModel viewModel)
        {
            WindowLifecycleTrace.Log(this, path, "EnterCompactView:set-IsCompactView=true");
            viewModel.IsCompactView = true;
        }

        PersistWindowState();
        BringCompactToForeground(path);
        WindowLifecycleTrace.Log(this, path, "EnterCompactView:end");
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
        WindowLifecycleTrace.Log(this, path, "ApplyCompactChrome:before");
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = CompactWidth;
        MinHeight = CompactHeight;
        Width = CompactWidth;
        Height = CompactHeight;
        SizeToContent = SizeToContent.Manual;
        TaskbarIconHelper.ApplyExeIcon(this, path, "ApplyCompactChrome");
        WindowLifecycleTrace.Log(this, path, "ApplyCompactChrome:after");
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

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void RestoreWindowPlacement(AppSettings settings, string path)
    {
        WindowLifecycleTrace.Log(this, path, "RestoreWindowPlacement:begin");
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

        WindowLifecycleTrace.Log(this, path, "RestoreWindowPlacement:end");
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
