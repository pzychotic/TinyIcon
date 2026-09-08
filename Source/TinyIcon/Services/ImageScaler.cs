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
    public static BitmapSource ScaleTo(BitmapSource source, int size) => ScaleToBox(source, size, size);

    /// <summary>
    /// Scales <paramref name="source"/> to fit a <paramref name="width"/>×<paramref name="height"/> box,
    /// preserving aspect ratio and centering the result with transparent padding. Icons read from a file
    /// may have non-square sub-images, so the target box is not assumed to be square.
    /// </summary>
    public static BitmapSource ScaleToBox(BitmapSource source, int width, int height)
    {
        double scale = Math.Min((double)width / source.PixelWidth, (double)height / source.PixelHeight);
        double w = Math.Round(source.PixelWidth * scale);
        double h = Math.Round(source.PixelHeight * scale);
        double x = Math.Round((width - w) / 2);
        double y = Math.Round((height - h) / 2);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            // clip to the target box so nothing bleeds outside it
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            // scale the source for smoother results instead of scaling the target rectangle (which can produce aliasing artifacts)
            dc.DrawImage(new TransformedBitmap(source, new ScaleTransform(scale, scale)), new Rect(x, y, w, h));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>
    /// Scales like <see cref="ScaleTo(BitmapSource, int)"/> and, for every target below 32 bpp, additionally
    /// applies <see cref="ApplyBinaryTransparency"/> — those entries carry transparency in the 1-bit AND mask
    /// — and, at 16 bpp and below, the colour reduction that depth forces. The result therefore previews
    /// exactly as it will be saved.
    /// </summary>
    public static BitmapSource ScaleTo(BitmapSource source, int size, int bpp) =>
        ScaleToBox(source, size, size, bpp);

    /// <summary>Non-square counterpart of <see cref="ScaleTo(BitmapSource, int, int)"/>.</summary>
    public static BitmapSource ScaleToBox(BitmapSource source, int width, int height, int bpp)
    {
        var scaled = ScaleToBox(source, width, height);
        if (bpp == 32)
            return scaled;

        var pixels = CopyBgra(scaled);
        ApplyBinaryTransparency(pixels);

        // 24 bpp holds every colour; 16 bpp and the palettized depths do not, and the user should see that.
        if (bpp < 24)
            ColorReducer.Reduce(pixels, width, height, bpp);

        return FromBgra(pixels, width, height);
    }

    /// <summary>
    /// Simulates the visual effect of an icon entry below 32 bpp with a 1-bit AND mask:
    /// pixels with alpha below 128 become fully transparent, all others become fully opaque.
    /// This lets the preview match the final <c>.ico</c> output.
    /// </summary>
    public static BitmapSource ApplyBinaryTransparency(BitmapSource source)
    {
        var pixels = CopyBgra(source);
        ApplyBinaryTransparency(pixels);
        return FromBgra(pixels, source.PixelWidth, source.PixelHeight);
    }

    private static void ApplyBinaryTransparency(byte[] pixels)
    {
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
    }

    /// <summary>Copies <paramref name="source"/> into straight (non-premultiplied) BGRA bytes, top-down.</summary>
    private static byte[] CopyBgra(BitmapSource source)
    {
        // Work in non-premultiplied BGRA so alpha thresholding is straightforward.
        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static BitmapSource FromBgra(byte[] pixels, int width, int height)
    {
        var result = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Bgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }
}
