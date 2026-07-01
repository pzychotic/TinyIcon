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
}
