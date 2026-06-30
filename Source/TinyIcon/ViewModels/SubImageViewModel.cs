using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

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

    /// <summary>Caption shown under the preview, e.g. "32×32 · 32-bit".</summary>
    public string Label => $"{Width}×{Height} · {Bpp}-bit";
}
