namespace TinyIcon.Models;

/// <summary>Catalog of the square icon sub-image sizes the app offers (in pixels).</summary>
public static class IconResolutions
{
    /// <summary>Typical sizes offered in the New Icon dialog, from 8×8 up to 256×256.</summary>
    public static readonly int[] Typical = [8, 16, 24, 32, 48, 64, 96, 128, 256];

    /// <summary>Sizes checked by default (the most common Windows shell icon sizes).</summary>
    public static readonly int[] DefaultChecked = [16, 32, 48, 256];
}
