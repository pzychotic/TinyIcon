using TinyIcon.Models;

namespace TinyIcon.Tests.Models;

[TestFixture]
public class IconImageFormatsTests
{
    [TestCase(256, 256, 32, IconImageFormat.Png)]
    [TestCase(512, 512, 32, IconImageFormat.Png)]
    [TestCase(256, 256, 24, IconImageFormat.Bmp)]
    [TestCase(128, 128, 32, IconImageFormat.Bmp)]
    [TestCase(16, 16, 24, IconImageFormat.Bmp)]
    public void DefaultFor_UsesPngOnlyFor32BitEntriesOf256AndUp(
        int width, int height, int bpp, IconImageFormat expected)
    {
        Assert.That(IconImageFormats.DefaultFor(width, height, bpp), Is.EqualTo(expected));
    }
}
