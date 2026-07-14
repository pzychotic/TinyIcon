using TinyIcon.Models;
using TinyIcon.Tests.TestSupport;
using TinyIcon.ViewModels;

namespace TinyIcon.Tests.ViewModels;

[TestFixture]
public class SubImageViewModelTests
{
    [Test]
    public void Constructor_ExposesDimensionsAndBpp()
    {
        var subImage = new SubImageViewModel(32, 32, 24);

        Assert.Multiple(() =>
        {
            Assert.That(subImage.Width, Is.EqualTo(32));
            Assert.That(subImage.Height, Is.EqualTo(32));
            Assert.That(subImage.Bpp, Is.EqualTo(24));
        });
    }

    [TestCase(32, 32, "32×32 · 32-bit BMP")]
    [TestCase(256, 32, "256×256 · 32-bit PNG")]
    public void Label_CombinesSizeColourDepthAndFormat(int size, int bpp, string expected)
    {
        var subImage = new SubImageViewModel(size, size, bpp);

        Assert.That(subImage.Label, Is.EqualTo(expected));
    }

    [TestCase(256, 32, IconImageFormat.Png)]
    [TestCase(256, 24, IconImageFormat.Bmp)]
    [TestCase(128, 32, IconImageFormat.Bmp)]
    public void Format_DefaultsPerTheAppRule(int size, int bpp, IconImageFormat expected)
    {
        var subImage = new SubImageViewModel(size, size, bpp);

        Assert.That(subImage.Format, Is.EqualTo(expected));
    }

    [Test]
    public void Format_Setting_UpdatesLabelAndNotifiesBoth()
    {
        var subImage = new SubImageViewModel(256, 256, 32);
        var raised = new List<string?>();
        subImage.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        subImage.Format = IconImageFormat.Bmp;

        Assert.Multiple(() =>
        {
            Assert.That(subImage.Label, Is.EqualTo("256×256 · 32-bit BMP"));
            Assert.That(raised, Does.Contain(nameof(SubImageViewModel.Format)));
            Assert.That(raised, Does.Contain(nameof(SubImageViewModel.Label)));
        });
    }

    [Test]
    public void HasImage_IsFalseUntilABitmapIsAssigned()
    {
        var subImage = new SubImageViewModel(16, 16, 32);

        Assert.That(subImage.HasImage, Is.False);
    }

    [Test]
    public void Bitmap_Setting_UpdatesHasImageAndNotifiesBoth()
    {
        var subImage = new SubImageViewModel(16, 16, 32);
        var raised = new List<string?>();
        subImage.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        subImage.Bitmap = BitmapTestHelpers.SolidColor(16, 16, 0, 0, 0, 255);

        Assert.Multiple(() =>
        {
            Assert.That(subImage.HasImage, Is.True);
            Assert.That(raised, Does.Contain(nameof(SubImageViewModel.Bitmap)));
            Assert.That(raised, Does.Contain(nameof(SubImageViewModel.HasImage)));
        });
    }
}
