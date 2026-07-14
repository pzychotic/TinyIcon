using System.IO;
using TinyIcon.Services;
using TinyIcon.Tests.TestSupport;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class ImageScalerTests
{
    [Test]
    public void Load_ReadsAnImageFileAtItsNativeSize()
    {
        string path = BitmapTestHelpers.WriteTempPng(40, 24);
        try
        {
            var loaded = ImageScaler.Load(path);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.PixelWidth, Is.EqualTo(40));
                Assert.That(loaded.PixelHeight, Is.EqualTo(24));
                Assert.That(loaded.IsFrozen, Is.True);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Load_WhenFileMissing_Throws()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");

        Assert.That(() => ImageScaler.Load(missing), Throws.InstanceOf<FileNotFoundException>());
    }

    [Test]
    public void ScaleTo_ProducesASquareOfTheRequestedSize()
    {
        var source = BitmapTestHelpers.SolidColor(40, 20, 10, 20, 30, 255);

        var scaled = ImageScaler.ScaleTo(source, 32);

        Assert.Multiple(() =>
        {
            Assert.That(scaled.PixelWidth, Is.EqualTo(32));
            Assert.That(scaled.PixelHeight, Is.EqualTo(32));
            Assert.That(scaled.IsFrozen, Is.True);
        });
    }

    [Test]
    public void ScaleTo_PadsNonSquareSourceWithTransparency()
    {
        // A 40×20 source fitted into 32×32 becomes 32×16, centred, leaving transparent
        // padding at the top-left corner.
        var source = BitmapTestHelpers.SolidColor(40, 20, 10, 20, 30, 255);

        var scaled = ImageScaler.ScaleTo(source, 32);

        var corner = new byte[4];
        scaled.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), corner, 4, 0);
        Assert.That(corner[3], Is.Zero, "top-left corner should be transparent padding");
    }

    [Test]
    public void ApplyBinaryTransparency_ZeroesPixelsBelowThreshold()
    {
        var source = BitmapTestHelpers.SolidColor(2, 2, 10, 20, 30, 127);

        var result = ImageScaler.ApplyBinaryTransparency(source);

        var pixel = new byte[4];
        result.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        Assert.That(pixel, Is.All.Zero, "an alpha-127 pixel should be fully zeroed, colour included");
    }

    [Test]
    public void ApplyBinaryTransparency_MakesPixelsAtThresholdFullyOpaque()
    {
        var source = BitmapTestHelpers.SolidColor(2, 2, 10, 20, 30, 128);

        var result = ImageScaler.ApplyBinaryTransparency(source);

        var pixel = new byte[4];
        result.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        Assert.That(pixel, Is.EqualTo(new byte[] { 10, 20, 30, 255 }),
            "an alpha-128 pixel should keep its colour and become fully opaque");
    }

    [Test]
    public void ScaleTo_With24Bpp_AppliesBinaryTransparency()
    {
        // Alpha 100 is below the mask threshold, so a 24 bpp slot must render it fully transparent
        // while a 32 bpp slot keeps the partial alpha.
        var source = BitmapTestHelpers.SolidColor(32, 32, 10, 20, 30, 100);

        var masked = ImageScaler.ScaleTo(source, 32, bpp: 24);
        var alpha = ImageScaler.ScaleTo(source, 32, bpp: 32);

        var maskedPixel = new byte[4];
        var alphaPixel = new byte[4];
        masked.CopyPixels(new System.Windows.Int32Rect(16, 16, 1, 1), maskedPixel, 4, 0);
        alpha.CopyPixels(new System.Windows.Int32Rect(16, 16, 1, 1), alphaPixel, 4, 0);

        Assert.Multiple(() =>
        {
            Assert.That(maskedPixel, Is.All.Zero, "24 bpp: centre pixel should be binary-transparent");
            Assert.That(alphaPixel[3], Is.Not.Zero, "32 bpp: centre pixel should keep partial alpha");
        });
    }
}
