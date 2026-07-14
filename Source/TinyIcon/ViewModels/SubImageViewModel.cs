using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using TinyIcon.Models;

namespace TinyIcon.ViewModels;

/// <summary>One sub-image inside the icon: a single resolution at a single colour depth.</summary>
public partial class SubImageViewModel(int width, int height, int bpp) : ObservableObject
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Bpp { get; } = bpp;

    /// <summary>The scaled bitmap for this slot, or <see langword="null"/> until an image is imported.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial BitmapSource? Bitmap { get; set; }

    public bool HasImage => Bitmap is not null;

    /// <summary>How this sub-image is encoded when saved (PNG for 256×256 32-bit, BMP otherwise).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial IconImageFormat Format { get; set; } = IconImageFormats.DefaultFor(width, height, bpp);

    /// <summary>Caption shown under the preview, e.g. "32×32 · 32-bit BMP".</summary>
    public string Label => $"{Width}×{Height} · {Bpp}-bit {(Format == IconImageFormat.Png ? "PNG" : "BMP")}";
}
