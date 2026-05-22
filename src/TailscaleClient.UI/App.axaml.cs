using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TailscaleClient.UI.Services;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = BuildServices();

        var service = Services.GetRequiredService<TailscaleService>();
        await service.StartAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Don't quit when the main window closes — the tray keeps the app alive.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var window = Services.GetRequiredService<MainWindow>();
            window.DataContext = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = window;

            var tray = Services.GetRequiredService<TrayService>();
            tray.Initialize(window);

            window.Show();
            Toast.Initialize(window);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<UserSettings>();
        sc.AddSingleton<TailscaleService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
        sc.AddSingleton<TrayService>();
        return sc.BuildServiceProvider();
    }
}
