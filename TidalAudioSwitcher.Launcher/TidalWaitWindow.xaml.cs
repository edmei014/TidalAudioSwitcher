using System.Windows;

namespace TidalAudioSwitcher.Launcher;

public partial class TidalWaitWindow : Window
{
    public TidalWaitWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message) => StatusText.Text = message;
}
