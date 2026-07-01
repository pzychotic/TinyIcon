using TinyIcon.ViewModels;

namespace TinyIcon.Tests.ViewModels;

[TestFixture]
public class ResolutionOptionViewModelTests
{
    [Test]
    public void Constructor_ExposesSizeAndInitialSelection()
    {
        var option = new ResolutionOptionViewModel(48, isSelected: true);

        Assert.Multiple(() =>
        {
            Assert.That(option.Size, Is.EqualTo(48));
            Assert.That(option.IsSelected, Is.True);
        });
    }

    [Test]
    public void Label_IsFormattedAsSizeBySize()
    {
        var option = new ResolutionOptionViewModel(256, isSelected: false);

        Assert.That(option.Label, Is.EqualTo("256×256"));
    }

    [Test]
    public void IsSelected_RaisesPropertyChanged()
    {
        var option = new ResolutionOptionViewModel(32, isSelected: false);
        var raised = new List<string?>();
        option.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        option.IsSelected = true;

        Assert.That(raised, Does.Contain(nameof(ResolutionOptionViewModel.IsSelected)));
    }

    [Test]
    public void IsSelected_DoesNotRaiseWhenUnchanged()
    {
        var option = new ResolutionOptionViewModel(32, isSelected: true);
        var raised = 0;
        option.PropertyChanged += (_, _) => raised++;

        option.IsSelected = true;

        Assert.That(raised, Is.Zero);
    }
}
