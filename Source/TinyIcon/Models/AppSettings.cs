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

    /// <summary>Sizes last checked per color depth (bpp) in the New Icon dialog.</summary>
    public Dictionary<int, int[]>? DepthSizes { get; set; }

    /// <summary>Color depths (bpp) last enabled in the New Icon dialog (default: 32-bit only).</summary>
    public int[]? EnabledDepths { get; set; }

    // Legacy New Icon selection from before the dialog offered every depth. Only read as a fallback for
    // DepthSizes/EnabledDepths, and cleared the next time the dialog is confirmed.

    /// <summary>Legacy: sizes last checked in the 24-bpp column.</summary>
    public int[]? Bpp24Sizes { get; set; }

    /// <summary>Legacy: sizes last checked in the 32-bpp column.</summary>
    public int[]? Bpp32Sizes { get; set; }

    /// <summary>Legacy: whether the 24-bpp column was last enabled.</summary>
    public bool? Bpp24Enabled { get; set; }

    /// <summary>Legacy: whether the 32-bpp column was last enabled.</summary>
    public bool? Bpp32Enabled { get; set; }
}
