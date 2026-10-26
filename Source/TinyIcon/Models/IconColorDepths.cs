namespace TinyIcon.Models;

/// <summary>Catalog of the color depths (bits per pixel) the app can read and write.</summary>
public static class IconColorDepths
{
    /// <summary>Every supported depth, from monochrome to 32-bit with alpha.</summary>
    public static readonly int[] All = [1, 4, 8, 16, 24, 32];

    /// <summary>Palette and high-color depths, hidden by default in the sub-image picker.</summary>
    public static readonly int[] Legacy = [1, 4, 8, 16];

    /// <summary>Depths enabled by default in the New Icon dialog.</summary>
    public static readonly int[] DefaultEnabled = [32];

    /// <summary>Whether <paramref name="bpp"/> is one of the <see cref="Legacy"/> depths.</summary>
    public static bool IsLegacy(int bpp) => Legacy.Contains(bpp);
}
