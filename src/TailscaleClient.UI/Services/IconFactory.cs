using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TailscaleClient.UI.Services;

/// <summary>
/// Renders state-coloured circle icons for the system tray. Avalonia ships
/// SkiaSharp internally, so <see cref="RenderTargetBitmap"/> + PNG round-trip
/// gives us a cross-platform <see cref="WindowIcon"/> with no asset files.
/// </summary>
public static class IconFactory
{
    public static WindowIcon CreateDotIcon(Color color, int size = 32)
    {
        var dpi = new Vector(96, 96);
        using var rtb = new RenderTargetBitmap(new PixelSize(size, size), dpi);
        using (var ctx = rtb.CreateDrawingContext())
        {
            var pad = size / 16.0;
            var rect = new Rect(pad, pad, size - 2 * pad, size - 2 * pad);
            ctx.DrawEllipse(new SolidColorBrush(color), null, rect.Center, rect.Width / 2, rect.Height / 2);
            ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)), 1),
                rect.Center, rect.Width / 2, rect.Height / 2);
        }
        using var ms = new MemoryStream();
        rtb.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }
}
