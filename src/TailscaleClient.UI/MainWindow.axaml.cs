using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TailscaleClient.UI;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Close-to-tray: the X button hides the window. The tray menu has an
    /// explicit Exit item that calls <see cref="ForceClose"/>.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    public void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
}
