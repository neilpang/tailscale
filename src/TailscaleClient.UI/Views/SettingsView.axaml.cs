using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TailscaleClient.UI.Services;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI.Views;

public partial class SettingsView : UserControl
{
    private static readonly char[] CidrSeparators = { ',', ' ', '\n', '\r', '\t' };

    public SettingsView()
    {
        InitializeComponent();
        LocalCidrsList.ItemsSource = LocalNetworkInfo.GetLocalCidrs();
    }

    private TailscaleService? Svc => (DataContext as MainViewModel)?.Service;

    private async void AddLocalCidr_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LocalCidr lc }) return;
        await AddCidrsAsync(new[] { lc.Cidr });
    }

    private async void AddRoutes_Click(object? sender, RoutedEventArgs e)
    {
        var raw = RoutesBox.Text ?? "";
        var typed = raw.Split(CidrSeparators, StringSplitOptions.RemoveEmptyEntries)
                       .Select(r => r.Trim())
                       .Where(r => r.Length > 0);
        if (await AddCidrsAsync(typed))
            RoutesBox.Text = "";
    }

    private void RoutesBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            AddRoutes_Click(sender, e);
            e.Handled = true;
        }
    }

    private async Task<bool> AddCidrsAsync(IEnumerable<string> cidrs)
    {
        if (Svc is null) return false;
        var current = Svc.AdvertisedRoutes.ToList();
        var seen = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var cidr in cidrs)
            if (seen.Add(cidr)) { current.Add(cidr); added = true; }
        if (!added) return false;
        await Svc.SetAdvertiseRoutesAsync(current);
        return true;
    }

    private async void RemoveRoute_Click(object? sender, RoutedEventArgs e)
    {
        if (Svc is null) return;
        if (sender is not Button { DataContext: string cidr }) return;

        var remaining = Svc.AdvertisedRoutes
            .Where(r => !string.Equals(r, cidr, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await Svc.SetAdvertiseRoutesAsync(remaining);
    }

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

}
