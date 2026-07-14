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

    /// <summary>
    /// Scales like <see cref="ScaleTo(BitmapSource, int)"/> and, for 24 bpp targets, additionally applies
    /// <see cref="ApplyBinaryTransparency"/> so the result previews exactly as it will be saved.
    /// </summary>
    public static BitmapSource ScaleTo(BitmapSource source, int size, int bpp)
    {
        var scaled = ScaleTo(source, size);
        return bpp == 24 ? ApplyBinaryTransparency(scaled) : scaled;
    }

    /// <summary>
    /// Simulates the visual effect of a 24 bpp icon entry with a 1-bit AND mask:
    /// pixels with alpha below 128 become fully transparent, all others become fully opaque.
    /// This lets the preview match the final <c>.ico</c> output.
    /// </summary>
    public static BitmapSource ApplyBinaryTransparency(BitmapSource source)
    {
        // Work in non-premultiplied BGRA so alpha thresholding is straightforward.
        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bgra.PixelWidth;
        int height = bgra.PixelHeight;
        int stride = width * 4;
        var pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);

        for (int i = 3; i < pixels.Length; i += 4) // walk alpha bytes
        {
            if (pixels[i] < IconFileWriter.OpaqueAlphaThreshold)
            {
                // Fully transparent – zero out the whole pixel
                pixels[i - 3] = 0; // B
                pixels[i - 2] = 0; // G
                pixels[i - 1] = 0; // R
                pixels[i] = 0; // A
            }
            else
            {
                pixels[i] = 255; // Fully opaque
            }
        }

        var result = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
