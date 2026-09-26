namespace TinyIcon.Models;

/// <summary>Catalog of the colour depths (bits per pixel) the app can read and write.</summary>
public static class IconColorDepths
{
    /// <summary>Every supported depth, from monochrome to 32-bit with alpha.</summary>
    public static readonly int[] All = [1, 4, 8, 16, 24, 32];

    /// <summary>Depths enabled by default in the New Icon dialog.</summary>
    public static readonly int[] DefaultEnabled = [32];
}
