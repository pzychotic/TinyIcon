using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TinyIcon.Models;

namespace TinyIcon.Services;

/// <summary>
/// Writes sub-images to a multi-resolution Windows <c>.ico</c> file. Each entry is encoded as a classic
/// DIB/BMP blob (BITMAPINFOHEADER + XOR colour data + 1-bit AND transparency mask), honouring its bpp:
/// 32-bit keeps the alpha channel, 24-bit relies on the AND mask for transparency.
/// </summary>
public static class IconFileWriter
{
    /// <summary>Writes the given images to <paramref name="path"/>.</summary>
    public static void Write(string path, IEnumerable<IconImage> images)
    {
        var entries = images
            .Select(i => BuildEntry(i.Bitmap, i.Bpp))
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

    private static Entry BuildEntry(BitmapSource source, int bpp)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;

        // Read straight (non-premultiplied) BGRA pixels, top-down.
        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int srcStride = width * 4;
        var pixels = new byte[srcStride * height];
        bgraSource.CopyPixels(pixels, srcStride, 0);

        byte[] data = bpp == 32
            ? BuildDib32(pixels, width, height)
            : BuildDib24(pixels, width, height);

        return new Entry(width, height, bpp, data);
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
                row[dst++] = bgra[src];     // B
                row[dst++] = bgra[src + 1]; // G
                row[dst++] = bgra[src + 2]; // R
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

    // 1-bpp transparency mask, rows bottom-up, MSB first. Bit set = transparent (source alpha < 128).
    private static void WriteAndMask(BinaryWriter w, byte[] bgra, int width, int height, int maskStride)
    {
        var row = new byte[maskStride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row);
            int src = y * width * 4 + 3; // alpha byte of the first pixel in this row
            for (int x = 0; x < width; x++, src += 4)
            {
                if (bgra[src] < 128)
                    row[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
            w.Write(row);
        }
    }

    private static int AndMaskStride(int width) => ((width + 31) / 32) * 4;
}
