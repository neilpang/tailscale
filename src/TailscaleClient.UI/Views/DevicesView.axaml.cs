using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using TailscaleClient.UI.Services;

namespace TailscaleClient.UI.Views;

public partial class DevicesView : UserControl
{
    // Width (in DIPs) of the click target on either edge of a column header
    // that activates the resize handle. Avalonia DataGrid hard-codes its own
    // hit-test region at this same value internally.
    private const double ResizeHandleWidth = 8;

    public DevicesView() => InitializeComponent();

    /// <summary>Copy a TextBlock's text on double-click. Wired to the three
    /// info lines in the "This machine" card so the user can grab the short
    /// hostname, FQDN, or IP list without reaching for the keyboard.</summary>
    private async void OnCopyTextDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock { Text: { Length: > 0 } text })
        {
            await Dialogs.CopyAsync(TopLevel.GetTopLevel(this), text);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Double-click on the right edge of a column header autofits that column
    /// to its cell content — the standard Excel / file-explorer affordance.
    /// Avalonia's built-in DataGrid does not wire this up by default, and
    /// <c>DataGridColumnHeader.OwningColumn</c> is internal, so we identify
    /// the target column by scanning <c>DataGrid.Columns</c> by cumulative
    /// <c>ActualWidth</c> from the pointer position.
    /// </summary>
    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PeersGrid is null) return;

        // Only act on the header row — we don't want double-clicking a cell to
        // mysteriously resize a column.
        Visual? v = e.Source as Visual;
        bool inHeader = false;
        while (v is not null && v != PeersGrid)
        {
            if (v is DataGridColumnHeader) { inHeader = true; break; }
            v = v.GetVisualParent();
        }
        if (!inHeader) return;

        var pos = e.GetPosition(PeersGrid);

        // Walk visible columns left → right; the boundary nearest the pointer
        // (within ResizeHandleWidth) identifies the column whose right edge
        // got the gesture.
        double x = 0;
        foreach (var col in PeersGrid.Columns)
        {
            if (!col.IsVisible) continue;
            x += col.ActualWidth;
            if (Math.Abs(pos.X - x) <= ResizeHandleWidth)
            {
                col.Width = DataGridLength.Auto;
                e.Handled = true;
                return;
            }
        }
    }
}
