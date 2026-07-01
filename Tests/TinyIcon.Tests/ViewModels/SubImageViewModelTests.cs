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

    [Test]
    public void Label_CombinesSizeAndColourDepth()
    {
        var subImage = new SubImageViewModel(32, 32, 32);

        Assert.That(subImage.Label, Is.EqualTo("32×32 · 32-bit"));
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
