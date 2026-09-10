using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TinyIcon.Models;

namespace TinyIcon.Services;

/// <summary>
/// Reads the sub-images of a Windows <c>.ico</c> file — the exact inverse of <see cref="IconFileWriter"/>.
/// Each directory entry is either a complete PNG stream (Vista+) or a classic DIB blob
/// (BITMAPINFOHEADER + XOR colour data + 1-bit AND transparency mask); both are decoded into a
/// straight (non-premultiplied) BGRA bitmap, keeping the entry's real size and colour depth.
/// </summary>
public static class IconFileReader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Reads every decodable sub-image of the icon at <paramref name="path"/>, in file order.</summary>
    /// <exception cref="InvalidDataException">
    /// The file is not an icon, or none of its entries could be decoded.
    /// </exception>
    public static IReadOnlyList<IconImage> Read(string path) => Read(File.ReadAllBytes(path));

    /// <summary>Reads every decodable sub-image out of an in-memory <c>.ico</c> file.</summary>
    internal static IReadOnlyList<IconImage> Read(byte[] bytes)
    {
        // ICONDIR
        if (bytes.Length < 6)
            throw new InvalidDataException("This is not a valid icon file.");

        int reserved = ReadUInt16(bytes, 0);
        int type = ReadUInt16(bytes, 2);
        int count = ReadUInt16(bytes, 4);
        if (reserved != 0 || type != 1 || count == 0)
            throw new InvalidDataException("This is not a valid icon file.");

        if (bytes.Length < 6 + 16 * count)
            throw new InvalidDataException("The icon file is truncated.");

        var images = new List<IconImage>(count);
        for (int i = 0; i < count; i++)
        {
            int e = 6 + 16 * i;
            int entryBpp = ReadUInt16(bytes, e + 6);
            int size = ReadInt32(bytes, e + 8);
            int offset = ReadInt32(bytes, e + 12);

            // Skip entries whose data does not lie inside the file rather than failing the whole load.
            if (size <= 0 || offset < 0 || (long)offset + size > bytes.Length)
                continue;

            try
            {
                images.Add(ReadEntry(bytes, offset, size, entryBpp));
            }
            catch (Exception)
            {
                // Unsupported encoding (BI_BITFIELDS, RLE, corrupt PNG): drop this entry, keep the rest.
            }
        }

        if (images.Count == 0)
            throw new InvalidDataException("The icon file contains no readable images.");

        return images;
    }

    private static IconImage ReadEntry(byte[] bytes, int offset, int size, int entryBpp)
    {
        if (StartsWithPngSignature(bytes, offset, size))
        {
            using var stream = new MemoryStream(bytes, offset, size, writable: false);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var bitmap = ToFrozenBgra32(decoder.Frames[0]);
            // PNG entries are conventionally 32-bit; trust the directory only when it says something.
            return new IconImage(bitmap, entryBpp > 0 ? entryBpp : 32, IconImageFormat.Png);
        }

        return ReadDib(bytes, offset, size, entryBpp);
    }

    private static bool StartsWithPngSignature(byte[] bytes, int offset, int size)
    {
        if (size < PngSignature.Length)
            return false;

        for (int i = 0; i < PngSignature.Length; i++)
        {
            if (bytes[offset + i] != PngSignature[i])
                return false;
        }
        return true;
    }

    private static IconImage ReadDib(byte[] bytes, int offset, int size, int entryBpp)
    {
        if (size < 40)
            throw new InvalidDataException("The image entry is too small to hold a bitmap header.");

        int headerSize = ReadInt32(bytes, offset);
        int width = ReadInt32(bytes, offset + 4);
        int storedHeight = ReadInt32(bytes, offset + 8);
        int bpp = ReadUInt16(bytes, offset + 14);
        int compression = ReadInt32(bytes, offset + 16);
        int clrUsed = ReadInt32(bytes, offset + 32);

        if (headerSize < 40 || width <= 0 || storedHeight == 0)
            throw new InvalidDataException("The image entry has an unsupported bitmap header.");
        if (compression != 0) // BI_RGB only
            throw new InvalidDataException("Compressed icon entries are not supported.");

        if (bpp == 0)
            bpp = entryBpp;
        if (bpp is not (1 or 4 or 8 or 16 or 24 or 32))
            throw new InvalidDataException($"Unsupported colour depth: {bpp} bpp.");

        int colorStride = ((width * bpp) + 31) / 32 * 4;
        int maskStride = AndMaskStride(width);
        int paletteEntries = bpp <= 8 ? (clrUsed > 0 ? clrUsed : 1 << bpp) : 0;
        int paletteOffset = offset + headerSize;
        int dataSize = size - (headerSize + paletteEntries * 4);

        // The writer doubles biHeight to cover the XOR and AND masks; tolerate files that do not.
        int height = storedHeight / 2;
        bool hasMask = true;
        if (height <= 0 || (long)(colorStride + maskStride) * height > dataSize)
        {
            height = storedHeight;
            hasMask = false;
        }
        if (height <= 0 || (long)colorStride * height > dataSize)
            throw new InvalidDataException("The image entry is truncated.");

        int colorOffset = paletteOffset + paletteEntries * 4;
        int maskOffset = colorOffset + colorStride * height;

        int stride = width * 4;
        var pixels = new byte[stride * height];

        // Rows are stored bottom-up.
        for (int y = 0; y < height; y++)
        {
            int src = colorOffset + (height - 1 - y) * colorStride;
            int dst = y * stride;
            DecodeRow(bytes, src, pixels, dst, width, bpp, paletteOffset, paletteEntries);
        }

        // 32-bit entries carry their own alpha, but plenty of older icons leave it all zero and rely on
        // the AND mask instead; using the mask in that case is the only way to get a visible image.
        if (hasMask && (bpp != 32 || IsAlphaAllZero(pixels)))
            ApplyAndMask(bytes, maskOffset, pixels, width, height, stride, maskStride);

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return new IconImage(bitmap, bpp, IconImageFormat.Bmp);
    }

    private static void DecodeRow(
        byte[] bytes, int src, byte[] pixels, int dst, int width, int bpp, int paletteOffset, int paletteEntries)
    {
        switch (bpp)
        {
            case 32:
                for (int x = 0; x < width; x++, src += 4, dst += 4)
                {
                    pixels[dst] = bytes[src];
                    pixels[dst + 1] = bytes[src + 1];
                    pixels[dst + 2] = bytes[src + 2];
                    pixels[dst + 3] = bytes[src + 3];
                }
                break;

            case 24:
                for (int x = 0; x < width; x++, src += 3, dst += 4)
                {
                    pixels[dst] = bytes[src];
                    pixels[dst + 1] = bytes[src + 1];
                    pixels[dst + 2] = bytes[src + 2];
                    pixels[dst + 3] = 255;
                }
                break;

            case 16: // BI_RGB at 16 bpp is XRGB1555; expand each 5-bit channel to 8 bits.
                for (int x = 0; x < width; x++, src += 2, dst += 4)
                {
                    int value = bytes[src] | (bytes[src + 1] << 8);
                    pixels[dst] = Expand5(value & 0x1F);
                    pixels[dst + 1] = Expand5((value >> 5) & 0x1F);
                    pixels[dst + 2] = Expand5((value >> 10) & 0x1F);
                    pixels[dst + 3] = 255;
                }
                break;

            default: // 1, 4 or 8 bpp: indices into the BGRA colour table that follows the header.
                for (int x = 0; x < width; x++, dst += 4)
                {
                    int index = ReadIndex(bytes, src, x, bpp);
                    int p = paletteOffset + (index < paletteEntries ? index : 0) * 4;
                    pixels[dst] = bytes[p];
                    pixels[dst + 1] = bytes[p + 1];
                    pixels[dst + 2] = bytes[p + 2];
                    pixels[dst + 3] = 255;
                }
                break;
        }
    }

    // Indices are packed most-significant-bits first.
    private static int ReadIndex(byte[] bytes, int rowStart, int x, int bpp) => bpp switch
    {
        8 => bytes[rowStart + x],
        4 => (x & 1) == 0 ? bytes[rowStart + (x >> 1)] >> 4 : bytes[rowStart + (x >> 1)] & 0x0F,
        _ => (bytes[rowStart + (x >> 3)] >> (7 - (x & 7))) & 1,
    };

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    /// <summary>Applies the 1-bpp AND mask (rows bottom-up, MSB first, bit set = transparent).</summary>
    private static void ApplyAndMask(
        byte[] bytes, int maskOffset, byte[] pixels, int width, int height, int stride, int maskStride)
    {
        for (int y = 0; y < height; y++)
        {
            int src = maskOffset + (height - 1 - y) * maskStride;
            int dst = y * stride + 3; // alpha byte of the first pixel in this row
            for (int x = 0; x < width; x++, dst += 4)
            {
                if ((bytes[src + (x >> 3)] & (0x80 >> (x & 7))) != 0)
                {
                    // The writer stores black under masked pixels; clear the whole pixel to match.
                    pixels[dst - 3] = 0;
                    pixels[dst - 2] = 0;
                    pixels[dst - 1] = 0;
                    pixels[dst] = 0;
                }
                else
                {
                    pixels[dst] = 255;
                }
            }
        }
    }

    private static bool IsAlphaAllZero(byte[] pixels)
    {
        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
                return false;
        }
        return true;
    }

    private static BitmapSource ToFrozenBgra32(BitmapSource source)
    {
        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        if (bgra.IsFrozen)
            return bgra;

        var copy = new WriteableBitmap(bgra);
        copy.Freeze();
        return copy;
    }

    private static int AndMaskStride(int width) => (width + 31) / 32 * 4;

    private static int ReadUInt16(byte[] bytes, int offset) => bytes[offset] | (bytes[offset + 1] << 8);

    private static int ReadInt32(byte[] bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
}
