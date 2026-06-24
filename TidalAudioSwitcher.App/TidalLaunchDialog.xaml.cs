using System.Windows;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services;

namespace TidalAudioSwitcher.App;

public partial class TidalLaunchDialog : Window
{
    public TidalLaunchDialog(string message, string? launcherPath)
    {
        InitializeComponent();
        MessageText.Text = message;
        Result = TidalLaunchDialogResult.Cancel;

        if (!string.IsNullOrWhiteSpace(launcherPath))
        {
            LauncherInfoText.Text = $"Launcher gefunden:\n{launcherPath}";
            LauncherButton.Visibility = Visibility.Visible;
            _launcherPath = launcherPath;
        }
        else
        {
            LauncherInfoText.Text =
                "Kein separater Launcher neben der Anwendung gefunden.\n" +
                "Verwenden Sie „Automatisch starten“, um TIDAL direkt zu starten.";
        }
    }

    public TidalLaunchDialogResult Result { get; private set; }

    private readonly string? _launcherPath;

    private void AutoStartButton_Click(object sender, RoutedEventArgs e)
    {
        Result = TidalLaunchDialogResult.AutoStart;
        DialogResult = true;
        Close();
    }

    private void LauncherButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_launcherPath))
        {
            return;
        }

        Result = TidalLaunchDialogResult.LaunchLauncher;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = TidalLaunchDialogResult.Cancel;
        DialogResult = false;
        Close();
    }
}
