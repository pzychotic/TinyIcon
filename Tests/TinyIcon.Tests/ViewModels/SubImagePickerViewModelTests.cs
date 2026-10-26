using TinyIcon.Models;
using TinyIcon.ViewModels;

namespace TinyIcon.Tests.ViewModels;

[TestFixture]
public class SubImagePickerViewModelTests
{
    private static IEnumerable<int> Checked(ColorDepthColumnViewModel column) =>
        column.Options.Where(o => o.IsSelected).Select(o => o.Size);

    private static void UncheckAll(SubImagePickerViewModel viewModel)
    {
        foreach (var option in viewModel.Columns.SelectMany(c => c.Options))
            option.IsSelected = false;
    }

    // --- New Icon -----------------------------------------------------------

    [Test]
    public void ForNewIcon_CreatesAColumnPerDepthWithAnOptionPerTypicalSize()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Columns.Select(c => c.Bpp), Is.EqualTo(IconColorDepths.All));
            foreach (var column in viewModel.Columns)
                Assert.That(column.Options.Select(o => o.Size), Is.EqualTo(IconResolutions.Typical));
        });
    }

    [Test]
    public void ForNewIcon_ChecksTheDefaultSizesInEveryColumn()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.Multiple(() =>
        {
            foreach (var column in viewModel.Columns)
                Assert.That(Checked(column), Is.EqualTo(IconResolutions.DefaultChecked));
        });
    }

    [Test]
    public void ForNewIcon_EnablesOnly32BitByDefault()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.That(viewModel.Columns.Where(c => c.IsEnabled).Select(c => c.Bpp), Is.EqualTo([32]));
    }

    [Test]
    public void ForNewIcon_ChecksTheGivenSizesPerDepth()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(
            bpp => bpp switch { 8 => [16, 32], 24 => [8, 64], 32 => [256], _ => [] }, [32]);

        Assert.Multiple(() =>
        {
            Assert.That(Checked(viewModel.Column(1)), Is.Empty);
            Assert.That(Checked(viewModel.Column(8)), Is.EqualTo([16, 32]));
            Assert.That(Checked(viewModel.Column(24)), Is.EqualTo([8, 64]));
            Assert.That(Checked(viewModel.Column(32)), Is.EqualTo([256]));
        });
    }

    [Test]
    public void ForNewIcon_IgnoresSizesOutsideTheTypicalCatalog()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(_ => [12, 16], [32]);

        Assert.That(Checked(viewModel.Column(24)), Is.EqualTo([16]));
    }

    [Test]
    public void ForNewIcon_EnablesTheGivenDepths()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(_ => [16], [4, 24]);

        Assert.That(viewModel.Columns.Where(c => c.IsEnabled).Select(c => c.Bpp), Is.EqualTo([4, 24]));
    }

    [Test]
    public void ForNewIcon_HasNoExistingEntries()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.That(viewModel.Columns.SelectMany(c => c.Options).Any(o => o.IsExisting), Is.False);
    }

    // --- HasSelection -------------------------------------------------------

    [Test]
    public void HasSelection_IsTrueByDefault()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.That(viewModel.HasSelection, Is.True);
    }

    [Test]
    public void HasSelection_IsFalseWhenEverythingIsUnchecked()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();
        UncheckAll(viewModel);

        Assert.That(viewModel.HasSelection, Is.False);
    }

    [Test]
    public void HasSelection_RaisesPropertyChangedWhenAnOptionToggles()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();
        UncheckAll(viewModel);

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Column(32).Options[0].IsSelected = true;

        Assert.That(raised, Does.Contain(nameof(SubImagePickerViewModel.HasSelection)));
    }

    [Test]
    public void HasSelection_IgnoresCheckedSizesInDisabledDepths()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(bpp => bpp == 24 ? [16, 32] : [], [32]);

        Assert.That(viewModel.HasSelection, Is.False);
    }

    [Test]
    public void HasSelection_RaisesPropertyChangedWhenADepthIsToggled()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Column(24).IsEnabled = true;

        Assert.That(raised, Does.Contain(nameof(SubImagePickerViewModel.HasSelection)));
    }

    // --- BuildSpecs ---------------------------------------------------------

    [Test]
    public void BuildSpecs_ReturnsSelectedSpecsOrderedByDepthThenSize()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(
            bpp => bpp switch { 4 => [32, 16], 24 => [48], 32 => [256, 16], _ => [] }, [4, 24, 32]);

        Assert.That(viewModel.BuildSpecs(), Is.EqualTo([(16, 4), (32, 4), (48, 24), (16, 32), (256, 32)]));
    }

    [Test]
    public void BuildSpecs_IsEmptyWhenNothingIsSelected()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();
        UncheckAll(viewModel);

        Assert.That(viewModel.BuildSpecs(), Is.Empty);
    }

    [Test]
    public void BuildSpecs_OmitsDisabledDepthsButKeepsTheirCheckedState()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(
            bpp => bpp switch { 24 => [16, 48], 32 => [32], _ => [] }, [32]);

        var specs = viewModel.BuildSpecs();

        Assert.Multiple(() =>
        {
            Assert.That(specs, Is.EqualTo([(32, 32)]));
            Assert.That(viewModel.Column(24).CheckedSizes, Is.EqualTo([16, 48]));
        });
    }

    // --- Add Sub-Images -----------------------------------------------------

    [Test]
    public void ForAddSubImages_ChecksAndLocksOnlyTheExistingEntries()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(16, 32), (48, 32), (32, 8)]);

        Assert.Multiple(() =>
        {
            Assert.That(Checked(viewModel.Column(32)), Is.EqualTo([16, 48]));
            Assert.That(Checked(viewModel.Column(8)), Is.EqualTo([32]));
            Assert.That(Checked(viewModel.Column(24)), Is.Empty);
            Assert.That(
                viewModel.Columns.SelectMany(c => c.Options).All(o => o.IsExisting == o.IsSelected), Is.True);
        });
    }

    [Test]
    public void ForAddSubImages_Enables32BitAndEveryDepthAlreadyInTheIcon()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(16, 4), (32, 24)]);

        Assert.That(viewModel.Columns.Where(c => c.IsEnabled).Select(c => c.Bpp), Is.EqualTo([4, 24, 32]));
    }

    [Test]
    public void ForAddSubImages_IgnoresExistingSizesOutsideTheTypicalCatalog()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(20, 32)]);

        Assert.That(Checked(viewModel.Column(32)), Is.Empty);
    }

    [Test]
    public void ForAddSubImages_HasNoSelectionUntilANewEntryIsChecked()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(16, 32), (32, 32)]);

        Assert.That(viewModel.HasSelection, Is.False);

        viewModel.Column(32).Options.Single(o => o.Size == 64).IsSelected = true;

        Assert.That(viewModel.HasSelection, Is.True);
    }

    [Test]
    public void ForAddSubImages_BuildSpecsReturnsOnlyTheNewEntries()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(16, 32), (32, 32)], showLegacy: true);
        viewModel.Column(32).Options.Single(o => o.Size == 64).IsSelected = true;
        viewModel.Column(8).IsEnabled = true;
        viewModel.Column(8).Options.Single(o => o.Size == 16).IsSelected = true;

        Assert.That(viewModel.BuildSpecs(), Is.EqualTo([(16, 8), (64, 32)]));
    }

    // --- Legacy depths -----------------------------------------------------

    private static IEnumerable<int> Visible(SubImagePickerViewModel viewModel) =>
        viewModel.Columns.Where(c => c.IsVisible).Select(c => c.Bpp);

    [Test]
    public void ForNewIcon_HidesLegacyDepthsByDefault()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowLegacy, Is.False);
            Assert.That(Visible(viewModel), Is.EqualTo([24, 32]));
        });
    }

    [Test]
    public void ForNewIcon_ShowsLegacyDepthsWhenPreferred()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(_ => [16], [32], showLegacy: true);

        Assert.That(Visible(viewModel), Is.EqualTo(IconColorDepths.All));
    }

    [Test]
    public void ForNewIcon_ShowsLegacyDepthsWhenOneIsEnabled()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(_ => [16], [8, 32], showLegacy: false);

        Assert.That(viewModel.ShowLegacy, Is.True);
    }

    [Test]
    public void ForAddSubImages_ShowsLegacyDepthsWhenTheIconUsesOne()
    {
        var viewModel = SubImagePickerViewModel.ForAddSubImages([(16, 4), (32, 32)], showLegacy: false);

        Assert.That(viewModel.ShowLegacy, Is.True);
    }

    [Test]
    public void ForAddSubImages_HidesLegacyDepthsForAModernIconUnlessPreferred()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SubImagePickerViewModel.ForAddSubImages([(16, 32)]).ShowLegacy, Is.False);
            Assert.That(SubImagePickerViewModel.ForAddSubImages([(16, 32)], showLegacy: true).ShowLegacy, Is.True);
        });
    }

    [Test]
    public void ToggleLegacy_ShowsAndHidesTheLegacyColumns()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        viewModel.ToggleLegacyCommand.Execute(null);
        var expanded = Visible(viewModel).ToList();
        viewModel.ToggleLegacyCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(expanded, Is.EqualTo(IconColorDepths.All));
            Assert.That(Visible(viewModel), Is.EqualTo([24, 32]));
        });
    }

    [Test]
    public void HiddenLegacyDepths_AreLeftOutOfTheResultUntilShownAgain()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon(bpp => bpp == 8 ? [16] : [], [8]);

        viewModel.ShowLegacy = false;
        var hiddenSelection = viewModel.HasSelection;
        var hiddenSpecs = viewModel.BuildSpecs();
        viewModel.ShowLegacy = true;

        Assert.Multiple(() =>
        {
            Assert.That(hiddenSelection, Is.False);
            Assert.That(hiddenSpecs, Is.Empty);
            Assert.That(viewModel.HasSelection, Is.True);
            Assert.That(viewModel.BuildSpecs(), Is.EqualTo([(16, 8)]));
        });
    }

    [Test]
    public void ShowLegacy_RaisesHasSelectionChanged()
    {
        var viewModel = SubImagePickerViewModel.ForNewIcon();

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.ShowLegacy = true;

        Assert.That(raised, Does.Contain(nameof(SubImagePickerViewModel.HasSelection)));
    }

    [Test]
    public void Title_DependsOnTheMode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SubImagePickerViewModel.ForNewIcon().Title, Is.EqualTo("New Icon"));
            Assert.That(SubImagePickerViewModel.ForAddSubImages([]).Title, Is.EqualTo("Add Sub-Images"));
        });
    }
}
