namespace TinyIcon.Models;

/// <summary>
/// State remembered between application runs (window placement, last icon size selection).
/// Null properties mean "never saved" and leave the built-in defaults in effect.
/// </summary>
public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>Sizes last checked in the 24-bpp column of the New Icon dialog.</summary>
    public int[]? Bpp24Sizes { get; set; }

    /// <summary>Sizes last checked in the 32-bpp column of the New Icon dialog.</summary>
    public int[]? Bpp32Sizes { get; set; }

    /// <summary>Whether the 24-bpp column of the New Icon dialog was last enabled (default: off).</summary>
    public bool? Bpp24Enabled { get; set; }

    /// <summary>Whether the 32-bpp column of the New Icon dialog was last enabled (default: on).</summary>
    public bool? Bpp32Enabled { get; set; }
}
