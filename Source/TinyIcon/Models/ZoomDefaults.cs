namespace TinyIcon.Models;

/// <summary>Single source for the zoom range and step shared by the view model and the zoom behavior.</summary>
public static class ZoomDefaults
{
    public const double Min = 1.0;
    public const double Max = 16.0;

    /// <summary>Additive step per zoom command or mouse-wheel notch.</summary>
    public const double Step = 1.0;
}
