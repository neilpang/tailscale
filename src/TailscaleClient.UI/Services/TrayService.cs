using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TailscaleClient.Core.Models;

namespace TailscaleClient.UI.Services;

/// <summary>
/// Manages the system-tray icon (Windows notification area, macOS menu bar,
/// Linux StatusNotifierItem). Built on Avalonia's cross-platform
/// <see cref="TrayIcon"/>.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly TailscaleService _svc;
    private readonly IAutostartService _autostart = AutostartService.Create();
    private TrayIcon? _tray;
    private MainWindow? _window;
    private WindowIcon? _runningIcon;
    private WindowIcon? _stoppedIcon;
    private WindowIcon? _attentionIcon;
    private NativeMenuItem? _connectItem;
    private NativeMenuItem? _disconnectItem;
    private NativeMenuItem? _loginItem;
    private NativeMenuItem? _logoutItem;
    private NativeMenuItem? _autostartItem;

    public TrayService(TailscaleService svc)
    {
        _svc = svc;
    }

    public void Initialize(MainWindow window)
    {
        _window = window;
        _runningIcon  = IconFactory.CreateDotIcon(Color.FromRgb(0x10, 0xB9, 0x81));
        _stoppedIcon  = IconFactory.CreateDotIcon(Color.FromRgb(0x6B, 0x72, 0x80));
        _attentionIcon = IconFactory.CreateDotIcon(Color.FromRgb(0xEF, 0x44, 0x44));

        _tray = new TrayIcon
        {
            ToolTipText = "Tailscale",
            Icon = _stoppedIcon,
            Menu = BuildMenu(),
        };
        _tray.Clicked += (_, _) => RestoreWindow();

        // Register the tray with the Application so it survives.
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });

        _svc.PropertyChanged += OnServiceChanged;
        UpdateIcon();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        var open = new NativeMenuItem("Open Tailscale");
        open.Click += (_, _) => RestoreWindow();
        menu.Add(open);
        menu.Add(new NativeMenuItemSeparator());

        _connectItem = new NativeMenuItem("Connect");
        _connectItem.Click += async (_, _) => await _svc.ConnectAsync();
        menu.Add(_connectItem);

        _disconnectItem = new NativeMenuItem("Disconnect");
        _disconnectItem.Click += async (_, _) => await _svc.DisconnectAsync();
        menu.Add(_disconnectItem);

        menu.Add(new NativeMenuItemSeparator());

        _loginItem = new NativeMenuItem("Sign in");
        _loginItem.Click += async (_, _) => await _svc.LoginAsync();
        menu.Add(_loginItem);

        _logoutItem = new NativeMenuItem("Log out");
        _logoutItem.Click += async (_, _) => await _svc.LogoutAsync();
        menu.Add(_logoutItem);

        menu.Add(new NativeMenuItemSeparator());

        _autostartItem = new NativeMenuItem("Launch at login")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _autostart.IsEnabled,
        };
        _autostartItem.Click += (_, _) =>
        {
            try
            {
                if (_autostart.IsEnabled) _autostart.Disable();
                else _autostart.Enable();
            }
            catch (Exception ex)
            {
                Toast.Show("Autostart", ex.Message, Avalonia.Controls.Notifications.NotificationType.Error);
            }
            // Re-read so the checkbox reflects what's actually on disk / in the
            // registry, not what we think we did.
            _autostartItem!.IsChecked = _autostart.IsEnabled;
            Toast.Show("Launch at login",
                _autostartItem.IsChecked ? "Enabled" : "Disabled",
                Avalonia.Controls.Notifications.NotificationType.Information);
        };
        menu.Add(_autostartItem);

        menu.Add(new NativeMenuItemSeparator());

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) =>
        {
            _window?.ForceClose();
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                ?.Shutdown();
        };
        menu.Add(exit);
        return menu;
    }

    private void OnServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TailscaleService.BackendState)
            or nameof(TailscaleService.AuthUrl)
            or nameof(TailscaleService.Status)
            or nameof(TailscaleService.IsLoggedIn)
            or nameof(TailscaleService.IsRunning))
        {
            Dispatcher.UIThread.Post(UpdateIcon);
        }
    }

    private void UpdateIcon()
    {
        if (_tray is null) return;
        var state = _svc.BackendState;
        _tray.ToolTipText = state switch
        {
            BackendState.Running          => $"Tailscale · Connected · {_svc.TailnetName}",
            BackendState.Starting         => "Tailscale · Connecting…",
            BackendState.NeedsLogin       => "Tailscale · Sign in required",
            BackendState.NeedsMachineAuth => "Tailscale · Machine auth required",
            BackendState.Stopped          => "Tailscale · Disconnected",
            _                             => "Tailscale",
        };
        _tray.Icon = state switch
        {
            BackendState.Running => _runningIcon,
            BackendState.NeedsLogin or BackendState.NeedsMachineAuth => _attentionIcon,
            _ => _stoppedIcon,
        };

        if (_connectItem is not null) _connectItem.IsEnabled = _svc.CanConnect;
        if (_disconnectItem is not null) _disconnectItem.IsEnabled = _svc.CanDisconnect;
        if (_loginItem is not null) _loginItem.IsEnabled = _svc.CanSignIn;
        if (_logoutItem is not null) _logoutItem.IsEnabled = _svc.IsLoggedIn;
    }

    private void RestoreWindow()
    {
        Dispatcher.UIThread.Post(() => _window?.RestoreFromTray());
    }

    public void Dispose()
    {
        _svc.PropertyChanged -= OnServiceChanged;
        _tray?.Dispose();
    }
}
