using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TinyIcon.Models;

namespace TinyIcon.Services;

/// <summary>
/// Writes sub-images to a multi-resolution Windows <c>.ico</c> file. Each entry is encoded per its
/// <see cref="IconImage.Format"/>: either a classic DIB/BMP blob (BITMAPINFOHEADER + optional colour table +
/// XOR colour data + 1-bit AND transparency mask), honouring its bpp — indexed depths always ship a full
/// 2^bpp colour table, because many readers locate the pixel data at that fixed offset rather than from
/// biClrUsed — 32-bit keeps the alpha channel, every
/// lesser depth relies on the AND mask for transparency — or a complete PNG stream (Vista+, typically the
/// 256×256 32-bit entry). 1, 4, 8, 16, 24 and 32 bpp DIBs are produced, matching what
/// <see cref="IconFileReader"/> can decode, so an icon opened from a file saves back at its original depth.
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
            // Colour count; the field is a byte, so a full 256-entry table is recorded as 0, as is "no table".
            writer.Write((byte)(e.PaletteCount < 256 ? e.PaletteCount : 0));
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

    private readonly record struct Entry(int Width, int Height, int Bpp, int PaletteCount, byte[] Data);

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
            return new Entry(width, height, image.Bpp, 0, BuildPng(bgraSource));

        // Only the depths IconFileReader can decode are written; anything else degrades to 24-bit rather
        // than producing a header we could not read back.
        int storedBpp = image.Bpp is 1 or 4 or 8 or 16 or 24 or 32 ? image.Bpp : 24;

        int srcStride = width * 4;
        var pixels = new byte[srcStride * height];
        bgraSource.CopyPixels(pixels, srcStride, 0);

        // ImageScaler already reduced imported images for the preview; doing it again here is a no-op for
        // those and quantizes anything that reached a slot by another route.
        var reduced = ColorReducer.Reduce(pixels, width, height, storedBpp);

        byte[] data = storedBpp switch
        {
            32 => BuildDib32(reduced.Bgra, width, height),
            24 => BuildDib24(reduced.Bgra, width, height),
            16 => BuildDib16(reduced.Bgra, width, height),
            _ => BuildDibIndexed(reduced, width, height, storedBpp),
        };

        // Indexed entries always ship a full 2^bpp colour table; see BuildDibIndexed.
        int paletteCount = storedBpp <= 8 ? 1 << storedBpp : 0;
        return new Entry(width, height, storedBpp, paletteCount, data);
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
        WriteHeader(w, width, height, 32, 0);

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
        WriteHeader(w, width, height, 24, 0);

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

    // BI_RGB at 16 bpp is XRGB1555; IconFileReader.Expand5 inverts the shift below exactly.
    private static byte[] BuildDib16(byte[] bgra, int width, int height)
    {
        int colorStride = ((width * 16) + 31) / 32 * 4;
        int maskStride = AndMaskStride(width);

        using var ms = new MemoryStream(40 + (colorStride + maskStride) * height);
        using var w = new BinaryWriter(ms);
        WriteHeader(w, width, height, 16, 0);

        var row = new byte[colorStride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row);
            int src = y * width * 4;
            int dst = 0;
            for (int x = 0; x < width; x++, src += 4, dst += 2)
            {
                // Masked-out pixels stay zero (black); see BuildDib24 for why that matters.
                if (bgra[src + 3] < OpaqueAlphaThreshold)
                    continue;

                int value = ((bgra[src + 2] >> 3) << 10) | ((bgra[src + 1] >> 3) << 5) | (bgra[src] >> 3);
                row[dst] = (byte)value;
                row[dst + 1] = (byte)(value >> 8);
            }
            w.Write(row);
        }

        WriteAndMask(w, bgra, width, height, maskStride);
        return ms.ToArray();
    }

    // 1, 4 and 8 bpp: a BGRA colour table follows the header, then packed indices into it.
    private static byte[] BuildDibIndexed(ReducedImage reduced, int width, int height, int bpp)
    {
        byte[] indices = reduced.Indices!;

        // The colour table is padded out to the full 2^bpp entries even when the reduction needed fewer.
        // biClrUsed says how many are meaningful, but plenty of readers (XnView, WinMerge, …) ignore it and
        // locate the pixel data at a fixed 40 + (1 << bpp) * 4 bytes, so a short table shifts the whole image.
        int paletteEntries = 1 << bpp;
        var palette = new byte[paletteEntries * 4];
        reduced.Palette.CopyTo(palette, 0);

        int colorStride = ((width * bpp) + 31) / 32 * 4;
        int maskStride = AndMaskStride(width);
        int perByte = 8 / bpp;

        using var ms = new MemoryStream(40 + palette.Length + (colorStride + maskStride) * height);
        using var w = new BinaryWriter(ms);
        WriteHeader(w, width, height, bpp, paletteEntries);
        w.Write(palette);

        var row = new byte[colorStride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row);
            int src = y * width;
            for (int x = 0; x < width; x++)
            {
                // Indices pack MSB-first within each byte; IconFileReader.ReadIndex unpacks them the same way.
                int shift = (perByte - 1 - (x % perByte)) * bpp;
                row[x / perByte] |= (byte)(indices[src + x] << shift);
            }
            w.Write(row);
        }

        WriteAndMask(w, reduced.Bgra, width, height, maskStride);
        return ms.ToArray();
    }

    private static void WriteHeader(BinaryWriter w, int width, int height, int bpp, int clrUsed)
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
        w.Write(clrUsed);     // biClrUsed
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
