using TinyIcon.Models;
using TinyIcon.ViewModels;

namespace TinyIcon.Tests.ViewModels;

[TestFixture]
public class NewIconViewModelTests
{
    [Test]
    public void Constructor_CreatesAnOptionPerTypicalSizeForEachDepth()
    {
        var viewModel = new NewIconViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Bpp24.Select(o => o.Size), Is.EqualTo(IconResolutions.Typical));
            Assert.That(viewModel.Bpp32.Select(o => o.Size), Is.EqualTo(IconResolutions.Typical));
        });
    }

    [Test]
    public void Constructor_ChecksTheDefaultSizesForEachDepth()
    {
        var viewModel = new NewIconViewModel();

        var checked24 = viewModel.Bpp24.Where(o => o.IsSelected).Select(o => o.Size);
        var checked32 = viewModel.Bpp32.Where(o => o.IsSelected).Select(o => o.Size);

        Assert.Multiple(() =>
        {
            Assert.That(checked24, Is.EqualTo(IconResolutions.DefaultChecked));
            Assert.That(checked32, Is.EqualTo(IconResolutions.DefaultChecked));
        });
    }

    [Test]
    public void Constructor_ChecksTheGivenSizesPerDepth()
    {
        var viewModel = new NewIconViewModel([8, 64], [256]);

        var checked24 = viewModel.Bpp24.Where(o => o.IsSelected).Select(o => o.Size);
        var checked32 = viewModel.Bpp32.Where(o => o.IsSelected).Select(o => o.Size);

        Assert.Multiple(() =>
        {
            Assert.That(checked24, Is.EqualTo([8, 64]));
            Assert.That(checked32, Is.EqualTo([256]));
        });
    }

    [Test]
    public void Constructor_IgnoresSizesOutsideTheTypicalCatalog()
    {
        var viewModel = new NewIconViewModel([12, 16], []);

        var checked24 = viewModel.Bpp24.Where(o => o.IsSelected).Select(o => o.Size);

        Assert.That(checked24, Is.EqualTo([16]));
    }

    [Test]
    public void Constructor_Enables32BitButNot24BitByDefault()
    {
        var viewModel = new NewIconViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Bpp24Enabled, Is.False);
            Assert.That(viewModel.Bpp32Enabled, Is.True);
        });
    }

    [Test]
    public void Constructor_AppliesTheGivenEnabledFlags()
    {
        var viewModel = new NewIconViewModel([16], [16], enabled24: true, enabled32: false);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Bpp24Enabled, Is.True);
            Assert.That(viewModel.Bpp32Enabled, Is.False);
        });
    }

    [Test]
    public void HasSelection_IsTrueByDefault()
    {
        var viewModel = new NewIconViewModel();

        Assert.That(viewModel.HasSelection, Is.True);
    }

    [Test]
    public void HasSelection_IsFalseWhenEverythingIsUnchecked()
    {
        var viewModel = new NewIconViewModel();
        foreach (var option in viewModel.Bpp24.Concat(viewModel.Bpp32))
            option.IsSelected = false;

        Assert.That(viewModel.HasSelection, Is.False);
    }

    [Test]
    public void HasSelection_RaisesPropertyChangedWhenAnOptionToggles()
    {
        var viewModel = new NewIconViewModel();
        foreach (var option in viewModel.Bpp24.Concat(viewModel.Bpp32))
            option.IsSelected = false;

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Bpp32[0].IsSelected = true;

        Assert.That(raised, Does.Contain(nameof(NewIconViewModel.HasSelection)));
    }

    [Test]
    public void HasSelection_IgnoresCheckedSizesInDisabledDepths()
    {
        var viewModel = new NewIconViewModel([16, 32], [], enabled24: false, enabled32: true);

        Assert.That(viewModel.HasSelection, Is.False);
    }

    [Test]
    public void HasSelection_RaisesPropertyChangedWhenADepthIsToggled()
    {
        var viewModel = new NewIconViewModel();

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Bpp24Enabled = true;

        Assert.That(raised, Does.Contain(nameof(NewIconViewModel.HasSelection)));
    }

    [Test]
    public void BuildSpecs_ReturnsSelectedSpecsOrderedBy24BitThen32Bit()
    {
        var viewModel = new NewIconViewModel(
            IconResolutions.DefaultChecked, IconResolutions.DefaultChecked, enabled24: true, enabled32: true);

        var specs = viewModel.BuildSpecs();

        var expected = IconResolutions.DefaultChecked.Select(s => (s, 24))
            .Concat(IconResolutions.DefaultChecked.Select(s => (s, 32)))
            .ToList();
        Assert.That(specs, Is.EqualTo(expected));
    }

    [Test]
    public void BuildSpecs_IsEmptyWhenNothingIsSelected()
    {
        var viewModel = new NewIconViewModel();
        foreach (var option in viewModel.Bpp24.Concat(viewModel.Bpp32))
            option.IsSelected = false;

        Assert.That(viewModel.BuildSpecs(), Is.Empty);
    }

    [Test]
    public void BuildSpecs_OmitsDisabledDepthsButKeepsTheirCheckedState()
    {
        var viewModel = new NewIconViewModel([16, 48], [32], enabled24: false, enabled32: true);

        var specs = viewModel.BuildSpecs();

        Assert.Multiple(() =>
        {
            Assert.That(specs, Is.EqualTo([(32, 32)]));
            Assert.That(viewModel.Bpp24.Where(o => o.IsSelected).Select(o => o.Size), Is.EqualTo([16, 48]));
        });
    }
}
