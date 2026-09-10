using TinyIcon.Services;
using TinyIcon.Tests.TestSupport;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class ColorReducerTests
{
    private static byte[] Pixels(int width, int height, int colorCount)
    {
        var bitmap = BitmapTestHelpers.DistinctColors(width, height, colorCount);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static HashSet<uint> DistinctOpaqueColors(byte[] bgra)
    {
        var colors = new HashSet<uint>();
        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] != 0)
                colors.Add((uint)(bgra[i] | (bgra[i + 1] << 8) | (bgra[i + 2] << 16)));
        }
        return colors;
    }

    [TestCase(24)]
    [TestCase(32)]
    public void Reduce_AtFullColourDepths_LeavesThePixelsAlone(int bpp)
    {
        var pixels = Pixels(8, 8, 60);
        var expected = (byte[])pixels.Clone();

        var reduced = ColorReducer.Reduce(pixels, 8, 8, bpp);

        Assert.Multiple(() =>
        {
            Assert.That(reduced.Bgra, Is.EqualTo(expected));
            Assert.That(reduced.Palette, Is.Empty);
            Assert.That(reduced.Indices, Is.Null);
        });
    }

    [TestCase(1, 2)]
    [TestCase(4, 16)]
    [TestCase(8, 256)]
    public void Reduce_NeverExceedsTheColourBudgetOfTheDepth(int bpp, int maxColors)
    {
        var pixels = Pixels(32, 32, 400);

        var reduced = ColorReducer.Reduce(pixels, 32, 32, bpp);

        Assert.Multiple(() =>
        {
            Assert.That(reduced.Palette.Length / 4, Is.LessThanOrEqualTo(maxColors), "palette entries");
            Assert.That(DistinctOpaqueColors(reduced.Bgra), Has.Count.LessThanOrEqualTo(maxColors), "pixels");
            Assert.That(reduced.Indices, Has.Length.EqualTo(32 * 32));
        });
    }

    [Test]
    public void Reduce_WhenTheColoursAlreadyFit_UsesThemVerbatim()
    {
        // The path an icon opened from a palettized file takes; it is what makes open/save lossless.
        var pixels = Pixels(16, 16, 200);
        var expected = (byte[])pixels.Clone();

        var reduced = ColorReducer.Reduce(pixels, 16, 16, 8);

        Assert.Multiple(() =>
        {
            Assert.That(reduced.Bgra, Is.EqualTo(expected));
            Assert.That(reduced.Palette.Length / 4, Is.EqualTo(200));
        });
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    public void Reduce_IsIdempotent(int bpp)
    {
        // IconFileWriter re-reduces the bitmap ImageScaler already previewed, so the second pass must not
        // change a pixel — otherwise the saved file would drift from what the user was shown. The palette
        // may be reordered (the first pass quantizes, the second recognises the colours verbatim), which
        // is why the assertion is on the pixels and on the set of colours, not on index numbering.
        var once = ColorReducer.Reduce(Pixels(16, 16, 400), 16, 16, bpp);
        var expected = (byte[])once.Bgra.Clone();

        var twice = ColorReducer.Reduce(once.Bgra, 16, 16, bpp);

        Assert.Multiple(() =>
        {
            Assert.That(twice.Bgra, Is.EqualTo(expected));
            Assert.That(twice.Palette.Chunk(4), Is.EquivalentTo(once.Palette.Chunk(4)));
        });
    }

    [Test]
    public void Reduce_MapsTransparentPixelsToPaletteIndexZero()
    {
        var bitmap = BitmapTestHelpers.HalfTransparent(8, 8);
        var pixels = new byte[8 * 8 * 4];
        bitmap.CopyPixels(pixels, 8 * 4, 0);

        var reduced = ColorReducer.Reduce(pixels, 8, 8, 4);

        Assert.Multiple(() =>
        {
            Assert.That(reduced.Palette[..3], Is.EqualTo(new byte[] { 0, 0, 0 }), "index 0 is black");
            Assert.That(reduced.Indices![4], Is.Zero, "a transparent pixel");
            Assert.That(reduced.Indices![0], Is.Not.Zero, "an opaque one");
            Assert.That(reduced.Bgra[16..20], Is.EqualTo(new byte[] { 0, 0, 0, 0 }), "stays transparent");
        });
    }

    [Test]
    public void Reduce_At16Bpp_SnapsEachChannelToFiveBits()
    {
        var pixels = Pixels(8, 8, 60);

        var reduced = ColorReducer.Reduce(pixels, 8, 8, 16);

        // Expand5 replicates the top three bits into the low three, so every channel matches that pattern.
        Assert.That(reduced.Bgra.Where((_, i) => i % 4 != 3),
            Is.All.Matches<byte>(c => c == (byte)(((c >> 3) << 3) | ((c >> 3) >> 2))));
    }
}
