namespace TinyIcon.Models;

/// <summary>How a sub-image is encoded inside the .ico file.</summary>
public enum IconImageFormat
{
    /// <summary>Classic DIB entry: BITMAPINFOHEADER + XOR colour data + 1-bit AND mask.</summary>
    Bmp,

    /// <summary>Complete PNG stream (Vista+), conventionally used for the 256×256 32-bit entry.</summary>
    Png,
}

/// <summary>The app's default encoding rule for icon sub-images.</summary>
public static class IconImageFormats
{
    /// <summary>
    /// PNG for 256×256-and-up 32-bit entries (Vista+ convention, dramatically smaller); everything else —
    /// including 24-bit 256×256, whose transparency lives in the AND mask PNG doesn't have — stays a classic DIB.
    /// </summary>
    public static IconImageFormat DefaultFor(int width, int height, int bpp) =>
        width >= 256 && height >= 256 && bpp == 32 ? IconImageFormat.Png : IconImageFormat.Bmp;
}
