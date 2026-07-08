using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TinyIcon.Services;

/// <summary>Loads source images and scales them into square icon sub-images using in-box WPF imaging.</summary>
public static class ImageScaler
{
    /// <summary>Loads an image file into a frozen <see cref="BitmapSource"/> (.bmp/.png/.jpg/.gif/.tiff).</summary>
    public static BitmapSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }

    /// <summary>
    /// Scales <paramref name="source"/> to fit a <paramref name="size"/>×<paramref name="size"/> square,
    /// preserving aspect ratio and centering the result with transparent padding.
    /// </summary>
    public static BitmapSource ScaleTo(BitmapSource source, int size)
    {
        double scale = Math.Min((double)size / source.PixelWidth, (double)size / source.PixelHeight);
        double w = Math.Round(source.PixelWidth * scale);
        double h = Math.Round(source.PixelHeight * scale);
        double x = Math.Round((size - w) / 2);
        double y = Math.Round((size - h) / 2);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            // force a quadratic canvas to avoid any potential issues with non-square images
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, size, size)));
            // scale the source for smoother results instead of scaling the target rectangle (which can produce aliasing artifacts)
            dc.DrawImage(new TransformedBitmap(source, new ScaleTransform(scale, scale)), new Rect(x, y, w, h));
        }

        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }
}
