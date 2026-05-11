using Avalonia.Controls;
using Avalonia.Interactivity;
using TailscaleClient.UI.Services;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private TailscaleService? Svc => (DataContext as MainViewModel)?.Service;

    private async void AcceptDns_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is not null && sender is CheckBox cb) await Svc.SetAcceptDnsAsync(cb.IsChecked == true);
    }

    private async void AcceptRoutes_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is not null && sender is CheckBox cb) await Svc.SetAcceptRoutesAsync(cb.IsChecked == true);
    }

    private async void ShieldsUp_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is not null && sender is CheckBox cb) await Svc.SetShieldsUpAsync(cb.IsChecked == true);
    }

    private async void RunSsh_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is not null && sender is CheckBox cb) await Svc.SetRunSshAsync(cb.IsChecked == true);
    }

    private async void ApplyRoutes_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is null) return;
        var raw = RoutesBox.Text ?? "";
        var routes = raw.Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Trim())
                        .Where(r => r.Length > 0)
                        .ToList();
        await Svc.SetAdvertiseRoutesAsync(routes);
    }
}
