using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TailscaleClient.UI.Services;

namespace TailscaleClient.UI;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        // Reuse the same colored-dot the tray uses, just bigger so the OS has
        // enough resolution for taskbar + Alt-Tab.
        Icon = IconFactory.CreateDotIcon(Color.FromRgb(0x10, 0xB9, 0x81), size: 64);
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
