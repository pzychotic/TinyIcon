using System.Windows.Media.Imaging;

namespace TinyIcon.Models;

/// <summary>One image to be written into an icon file: a bitmap at a given colour depth and encoding.</summary>
public readonly record struct IconImage(BitmapSource Bitmap, int Bpp, IconImageFormat Format);
