using Avalonia.Controls;
using Avalonia.Interactivity;
using TailscaleClient.UI.ViewModels;

namespace TailscaleClient.UI.Views;

public partial class ExitNodeView : UserControl
{
    public ExitNodeView() => InitializeComponent();

    private async void ExitNodeAllowLan_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is CheckBox cb)
            await vm.Service.SetExitNodeAllowLanAsync(cb.IsChecked == true);
    }
}
