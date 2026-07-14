using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TinyIcon.Models;

namespace TinyIcon.Services;

/// <summary>
/// Writes sub-images to a multi-resolution Windows <c>.ico</c> file. Each entry is encoded per its
/// <see cref="IconImage.Format"/>: either a classic DIB/BMP blob (BITMAPINFOHEADER + XOR colour data +
/// 1-bit AND transparency mask), honouring its bpp — 32-bit keeps the alpha channel, 24-bit relies on the
/// AND mask for transparency — or a complete PNG stream (Vista+, typically the 256×256 32-bit entry).
/// </summary>
public static class IconFileWriter
{
    /// <summary>
    /// Minimum source alpha for a pixel to count as opaque in the 1-bit AND mask.
    /// <see cref="ImageScaler.ApplyBinaryTransparency"/> must use the same threshold so previews match saved files.
    /// </summary>
    internal const byte OpaqueAlphaThreshold = 128;

    /// <summary>Writes the given images to <paramref name="path"/>.</summary>
    public static void Write(string path, IEnumerable<IconImage> images)
    {
        var entries = images
            .Select(BuildEntry)
            .ToList();

        if (entries.Count == 0)
            throw new InvalidOperationException("There are no imported images to save.");

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // ICONDIR
        writer.Write((ushort)0);             // reserved
        writer.Write((ushort)1);             // type = icon
        writer.Write((ushort)entries.Count); // image count

        // ICONDIRENTRY records; image data follows all of them.
        int offset = 6 + 16 * entries.Count;
        foreach (var e in entries)
        {
            writer.Write((byte)(e.Width >= 256 ? 0 : e.Width));
            writer.Write((byte)(e.Height >= 256 ? 0 : e.Height));
            writer.Write((byte)0);           // colour count (0 for >= 8bpp)
            writer.Write((byte)0);           // reserved
            writer.Write((ushort)1);         // colour planes
            writer.Write((ushort)e.Bpp);     // bits per pixel
            writer.Write(e.Data.Length);     // bytes in resource
            writer.Write(offset);            // image offset
            offset += e.Data.Length;
        }

        foreach (var e in entries)
            writer.Write(e.Data);
    }

    private readonly record struct Entry(int Width, int Height, int Bpp, byte[] Data);

    private static Entry BuildEntry(IconImage image)
    {
        BitmapSource source = image.Bitmap;
        int width = source.PixelWidth;
        int height = source.PixelHeight;

        // Straight (non-premultiplied) BGRA, top-down; PNG icon entries are conventionally 32-bit BGRA too.
        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        if (image.Format == IconImageFormat.Png)
            return new Entry(width, height, image.Bpp, BuildPng(bgraSource));

        int srcStride = width * 4;
        var pixels = new byte[srcStride * height];
        bgraSource.CopyPixels(pixels, srcStride, 0);

        byte[] data = image.Bpp == 32
            ? BuildDib32(pixels, width, height)
            : BuildDib24(pixels, width, height);

        return new Entry(width, height, image.Bpp, data);
    }

    // Vista+ icons embed the complete PNG file as the entry data; readers detect it by its signature.
    private static byte[] BuildPng(BitmapSource bgraSource)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bgraSource));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static byte[] BuildDib32(byte[] bgra, int width, int height)
    {
        int colorStride = width * 4; // already 4-byte aligned
        int maskStride = AndMaskStride(width);

        using var ms = new MemoryStream(40 + (colorStride + maskStride) * height);
        using var w = new BinaryWriter(ms);
        WriteHeader(w, width, height, 32);

        // XOR mask: BGRA colour data, rows bottom-up.
        for (int y = height - 1; y >= 0; y--)
            w.Write(bgra, y * colorStride, colorStride);

        WriteAndMask(w, bgra, width, height, maskStride);
        return ms.ToArray();
    }

    private static byte[] BuildDib24(byte[] bgra, int width, int height)
    {
        int colorStride = ((width * 3) + 3) & ~3; // pad each row to 4 bytes
        int maskStride = AndMaskStride(width);

        using var ms = new MemoryStream(40 + (colorStride + maskStride) * height);
        using var w = new BinaryWriter(ms);
        WriteHeader(w, width, height, 24);

        // XOR mask: BGR colour data, rows bottom-up and padded.
        var row = new byte[colorStride];
        for (int y = height - 1; y >= 0; y--)
        {
            int src = y * width * 4;
            int dst = 0;
            for (int x = 0; x < width; x++, src += 4)
            {
                // Masked-out pixels must be black: legacy renderers XOR the colour data over the
                // destination, so stray colour under a transparent mask bit shows as artifacts.
                bool opaque = bgra[src + 3] >= OpaqueAlphaThreshold;
                row[dst++] = opaque ? bgra[src] : (byte)0;     // B
                row[dst++] = opaque ? bgra[src + 1] : (byte)0; // G
                row[dst++] = opaque ? bgra[src + 2] : (byte)0; // R
            }
            for (; dst < colorStride; dst++)
                row[dst] = 0;
            w.Write(row);
        }

        WriteAndMask(w, bgra, width, height, maskStride);
        return ms.ToArray();
    }

    private static void WriteHeader(BinaryWriter w, int width, int height, int bpp)
    {
        w.Write(40);          // biSize
        w.Write(width);       // biWidth
        w.Write(height * 2);  // biHeight (XOR + AND)
        w.Write((ushort)1);   // biPlanes
        w.Write((ushort)bpp); // biBitCount
        w.Write(0);           // biCompression = BI_RGB
        w.Write(0);           // biSizeImage
        w.Write(0);           // biXPelsPerMeter
        w.Write(0);           // biYPelsPerMeter
        w.Write(0);           // biClrUsed
        w.Write(0);           // biClrImportant
    }

    // 1-bpp transparency mask, rows bottom-up, MSB first. Bit set = transparent.
    private static void WriteAndMask(BinaryWriter w, byte[] bgra, int width, int height, int maskStride)
    {
        var row = new byte[maskStride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row);
            int src = y * width * 4 + 3; // alpha byte of the first pixel in this row
            for (int x = 0; x < width; x++, src += 4)
            {
                if (bgra[src] < OpaqueAlphaThreshold)
                    row[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
            w.Write(row);
        }
    }

    private static int AndMaskStride(int width) => ((width + 31) / 32) * 4;
}
