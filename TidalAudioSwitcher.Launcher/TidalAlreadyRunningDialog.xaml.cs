using System.Windows;

namespace TidalAudioSwitcher.Launcher;

public partial class TidalAlreadyRunningDialog : Window
{
    public TidalAlreadyRunningDialog()
    {
        InitializeComponent();
    }

    public bool RestartRequested { get; private set; }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        RestartRequested = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        RestartRequested = false;
        DialogResult = false;
        Close();
    }
}
