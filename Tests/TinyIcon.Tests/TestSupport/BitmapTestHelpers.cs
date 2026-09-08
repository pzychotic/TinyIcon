using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TinyIcon.Tests.TestSupport;

/// <summary>Small helpers for producing in-memory and on-disk bitmaps for the imaging tests.</summary>
internal static class BitmapTestHelpers
{
    /// <summary>Creates a frozen solid-colour Bgra32 bitmap of the given size.</summary>
    public static BitmapSource SolidColor(int width, int height, byte b, byte g, byte r, byte a)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Creates a frozen Bgra32 bitmap whose opaque pixels cycle through <paramref name="colorCount"/>
    /// distinct colours, so colour-reduction tests can start from a known number of them.
    /// </summary>
    public static BitmapSource DistinctColors(int width, int height, int colorCount)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        for (int p = 0, i = 0; i < pixels.Length; p++, i += 4)
        {
            // Spread the colours far apart so nearest-colour mapping has unambiguous answers.
            int n = p % colorCount;
            pixels[i] = (byte)(n * 7 % 251);
            pixels[i + 1] = (byte)(n * 29 % 241);
            pixels[i + 2] = (byte)(n * 61 % 239);
            pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Creates a frozen Bgra32 bitmap whose left half is opaque and right half fully transparent.</summary>
    public static BitmapSource HalfTransparent(int width, int height, byte b = 10, byte g = 20, byte r = 30)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width / 2; x++)
            {
                int i = y * stride + x * 4;
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Writes a solid-colour PNG to a temp file and returns its path.</summary>
    public static string WriteTempPng(int width, int height, byte b = 10, byte g = 20, byte r = 30, byte a = 255)
    {
        var bitmap = SolidColor(width, height, b, g, r, a);
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
