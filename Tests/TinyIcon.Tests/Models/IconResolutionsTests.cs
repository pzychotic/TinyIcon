using TinyIcon.Models;

namespace TinyIcon.Tests.Models;

[TestFixture]
public class IconResolutionsTests
{
    [Test]
    public void Typical_ListsTheOfferedSizesInAscendingOrder()
    {
        Assert.That(IconResolutions.Typical, Is.EqualTo([8, 16, 24, 32, 48, 64, 96, 128, 256]));
    }

    [Test]
    public void DefaultChecked_AreTheCommonShellSizes()
    {
        Assert.That(IconResolutions.DefaultChecked, Is.EqualTo([16, 32, 48, 256]));
    }

    [Test]
    public void DefaultChecked_IsASubsetOfTypical()
    {
        Assert.That(IconResolutions.DefaultChecked, Is.SubsetOf(IconResolutions.Typical));
    }
}
