using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TinyIcon.Services;

/// <summary>
/// The result of reducing a BGRA buffer to the colours a given icon depth can represent.
/// <see cref="Palette"/> holds four bytes (B, G, R, 0) per entry and <see cref="Indices"/> one byte per
/// pixel; both are empty/null above 8 bpp. <see cref="Bgra"/> is what the reduced image actually looks
/// like, so a preview built from it matches the saved entry pixel for pixel.
/// </summary>
public readonly record struct ReducedImage(byte[] Bgra, byte[] Palette, byte[]? Indices);

/// <summary>
/// Reduces straight (non-premultiplied) BGRA pixels to the colours representable at a given icon depth.
/// Both <see cref="ImageScaler"/> (for previews) and <see cref="IconFileWriter"/> (for the saved bytes)
/// go through here, so what the user sees is what lands in the file.
/// </summary>
public static class ColorReducer
{
    /// <summary>Opaque black, reserved as index 0 of every generated palette.</summary>
    private const uint Black = 0;

    /// <summary>
    /// Reduces <paramref name="bgra"/> in place for <paramref name="bpp"/> and returns the palette and
    /// indices needed to write it. 24 and 32 bpp pass through untouched — they can carry every colour.
    /// </summary>
    /// <remarks>
    /// Idempotent: reducing a buffer this method produced yields the same pixels and the same palette.
    /// That is what lets <see cref="IconFileWriter"/> re-derive a palette from the previewed bitmap
    /// instead of the model having to carry one around.
    /// </remarks>
    public static ReducedImage Reduce(byte[] bgra, int width, int height, int bpp)
    {
        switch (bpp)
        {
            case 16:
                SnapTo555(bgra);
                return new ReducedImage(bgra, [], null);

            case 1 or 4 or 8:
                return ReduceIndexed(bgra, width, height, bpp);

            default:
                return new ReducedImage(bgra, [], null);
        }
    }

    /// <summary>
    /// Snaps each channel to its top 5 bits and expands it back the way <c>IconFileReader.Expand5</c>
    /// does, so a 16 bpp entry survives a write/read round trip bit for bit.
    /// </summary>
    private static void SnapTo555(byte[] bgra)
    {
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = Expand5(bgra[i] >> 3);
            bgra[i + 1] = Expand5(bgra[i + 1] >> 3);
            bgra[i + 2] = Expand5(bgra[i + 2] >> 3);
        }
    }

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    private static ReducedImage ReduceIndexed(byte[] bgra, int width, int height, int bpp)
    {
        int maxColors = 1 << bpp;

        // Transparency lives solely in the 1-bit AND mask, and masked pixels must be black because legacy
        // renderers XOR the colour data over the destination. Reserving index 0 for black covers both.
        bool reserveBlack = false;
        for (int i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] < IconFileWriter.OpaqueAlphaThreshold)
            {
                reserveBlack = true;
                break;
            }
        }

        var palette = BuildPalette(bgra, width, height, maxColors, reserveBlack);

        var indices = new byte[width * height];
        var cache = new Dictionary<uint, byte>();
        for (int p = 0, i = 0; p < indices.Length; p++, i += 4)
        {
            if (bgra[i + 3] < IconFileWriter.OpaqueAlphaThreshold)
            {
                // Fully transparent: index 0 (black) and a zeroed pixel, matching what the reader gives back.
                indices[p] = 0;
                bgra[i] = 0;
                bgra[i + 1] = 0;
                bgra[i + 2] = 0;
                bgra[i + 3] = 0;
                continue;
            }

            uint color = Pack(bgra[i], bgra[i + 1], bgra[i + 2]);
            if (!cache.TryGetValue(color, out byte index))
            {
                index = (byte)NearestIndex(palette, color);
                cache[color] = index;
            }

            indices[p] = index;

            // Write the chosen colour back so Bgra is exactly what the entry will contain.
            uint chosen = palette[index];
            bgra[i] = (byte)chosen;
            bgra[i + 1] = (byte)(chosen >> 8);
            bgra[i + 2] = (byte)(chosen >> 16);
            bgra[i + 3] = 255;
        }

        var table = new byte[palette.Count * 4];
        for (int i = 0; i < palette.Count; i++)
        {
            table[i * 4] = (byte)palette[i];
            table[i * 4 + 1] = (byte)(palette[i] >> 8);
            table[i * 4 + 2] = (byte)(palette[i] >> 16);
            table[i * 4 + 3] = 0;
        }

        return new ReducedImage(bgra, table, indices);
    }

    /// <summary>
    /// Picks at most <paramref name="maxColors"/> colours for the opaque pixels of <paramref name="bgra"/>.
    /// When they all fit, the distinct colours are used verbatim — that is the path an icon opened from a
    /// palettized file takes, and it is what makes open/save lossless.
    /// </summary>
    private static List<uint> BuildPalette(byte[] bgra, int width, int height, int maxColors, bool reserveBlack)
    {
        // Colours are collected in order of first appearance, so the same pixels always yield the same
        // palette and the file a given icon saves to is reproducible.
        var palette = new List<uint>(maxColors);
        var seen = new HashSet<uint>();
        if (reserveBlack)
        {
            palette.Add(Black);
            seen.Add(Black);
        }

        bool fits = true;
        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < IconFileWriter.OpaqueAlphaThreshold)
                continue;

            uint color = Pack(bgra[i], bgra[i + 1], bgra[i + 2]);
            if (!seen.Add(color))
                continue;

            if (seen.Count > maxColors)
            {
                fits = false;
                break;
            }
            palette.Add(color);
        }

        if (!fits)
        {
            palette.Clear();
            if (reserveBlack)
                palette.Add(Black);

            // Median cut, courtesy of WPF. Ask for one fewer colour when black is already spoken for; the
            // constructor rejects counts below two, so clamp and drop any surplus while copying.
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
            var generated = new BitmapPalette(source, Math.Clamp(reserveBlack ? maxColors - 1 : maxColors, 2, 256));

            foreach (var color in generated.Colors)
            {
                if (palette.Count == maxColors)
                    break;

                uint packed = Pack(color.B, color.G, color.R);
                if (!palette.Contains(packed))
                    palette.Add(packed);
            }
        }

        // A colour table needs at least one entry, and a two-entry one keeps 1 bpp entries unremarkable.
        while (palette.Count < Math.Min(2, maxColors))
            palette.Add(Black);

        return palette;
    }

    private static int NearestIndex(List<uint> palette, uint color)
    {
        int b = (int)(color & 0xFF);
        int g = (int)((color >> 8) & 0xFF);
        int r = (int)((color >> 16) & 0xFF);

        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            uint candidate = palette[i];
            int db = b - (int)(candidate & 0xFF);
            int dg = g - (int)((candidate >> 8) & 0xFF);
            int dr = r - (int)((candidate >> 16) & 0xFF);

            int distance = db * db + dg * dg + dr * dr;
            if (distance < bestDistance)
            {
                if (distance == 0)
                    return i;

                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static uint Pack(byte b, byte g, byte r) => (uint)(b | (g << 8) | (r << 16));
}
