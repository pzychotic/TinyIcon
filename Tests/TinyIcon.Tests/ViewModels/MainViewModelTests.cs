using System.IO;
using TinyIcon.Tests.TestSupport;
using TinyIcon.ViewModels;

namespace TinyIcon.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private static MainViewModel Create(FakeDialogService dialogs) => new(dialogs);

    [Test]
    public void Constructor_StartsAtOneHundredPercentZoomWithNoSlots()
    {
        var vm = Create(new FakeDialogService());

        Assert.Multiple(() =>
        {
            Assert.That(vm.ZoomLevel, Is.EqualTo(1.0));
            Assert.That(vm.SubImages, Is.Empty);
        });
    }

    // --- New Icon -----------------------------------------------------------

    [Test]
    public void NewIcon_PopulatesSlotsAndSelectsTheFirst()
    {
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 24), (32, 32)],
        };
        var vm = Create(dialogs);

        vm.NewIconCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SubImages.Select(s => (s.Width, s.Bpp)), Is.EqualTo([(16, 24), (32, 32)]));
            Assert.That(vm.SelectedSubImage, Is.SameAs(vm.SubImages[0]));
        });
    }

    [Test]
    public void NewIcon_ReplacesAnyExistingSlots()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)] };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        dialogs.NewIconResult = [(48, 24), (64, 24)];
        vm.NewIconCommand.Execute(null);

        Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([48, 64]));
    }

    [Test]
    public void NewIcon_WhenCancelled_LeavesSlotsUntouched()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)] };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        dialogs.NewIconResult = null; // cancelled
        vm.NewIconCommand.Execute(null);

        Assert.That(vm.SubImages, Has.Count.EqualTo(1));
    }

    [Test]
    public void NewIcon_EnablesImportCommand()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(32, 32)] };
        var vm = Create(dialogs);

        Assert.That(vm.ImportImageCommand.CanExecute(null), Is.False);

        vm.NewIconCommand.Execute(null);

        Assert.That(vm.ImportImageCommand.CanExecute(null), Is.True);
    }

    // --- Import Image -------------------------------------------------------

    [Test]
    public void ImportImage_ScalesSourceIntoEverySlot()
    {
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32), (32, 32)],
            OpenImageResult = BitmapTestHelpers.WriteTempPng(40, 40),
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        try
        {
            vm.ImportImageCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.SubImages.All(s => s.HasImage), Is.True);
                Assert.That(vm.SubImages[0].Bitmap!.PixelWidth, Is.EqualTo(16));
                Assert.That(vm.SubImages[1].Bitmap!.PixelWidth, Is.EqualTo(32));
                Assert.That(vm.SaveIconCommand.CanExecute(null), Is.True);
            });
        }
        finally
        {
            File.Delete(dialogs.OpenImageResult!);
        }
    }

    [Test]
    public void ImportImage_WhenCancelled_DoesNothing()
    {
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32)],
            OpenImageResult = null,
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        vm.ImportImageCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SubImages[0].HasImage, Is.False);
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void ImportImage_WhenLoadFails_ReportsAnError()
    {
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32)],
            OpenImageResult = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png"),
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        vm.ImportImageCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.Errors, Has.Count.EqualTo(1));
            Assert.That(vm.SubImages[0].HasImage, Is.False);
            Assert.That(vm.SaveIconCommand.CanExecute(null), Is.False);
        });
    }

    // --- Save Icon ----------------------------------------------------------

    [Test]
    public void SaveIcon_CanExecute_OnlyAfterAnImageIsImported()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)] };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        Assert.That(vm.SaveIconCommand.CanExecute(null), Is.False);

        vm.SubImages[0].Bitmap = BitmapTestHelpers.SolidColor(16, 16, 0, 0, 0, 255);
        vm.SaveIconCommand.NotifyCanExecuteChanged();

        Assert.That(vm.SaveIconCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void SaveIcon_WritesTheIconToTheChosenPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32)],
            SaveIconResult = path,
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);
        vm.SubImages[0].Bitmap = BitmapTestHelpers.SolidColor(16, 16, 1, 2, 3, 255);

        try
        {
            vm.SaveIconCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(dialogs.Errors, Is.Empty);
            });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void SaveIcon_SkipsSlotsWithoutABitmap()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32), (32, 32)],
            SaveIconResult = path,
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);
        vm.SubImages[0].Bitmap = BitmapTestHelpers.SolidColor(16, 16, 1, 2, 3, 255);

        try
        {
            vm.SaveIconCommand.Execute(null);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 4; // skip reserved + type, land on the image count
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadUInt16(), Is.EqualTo(1), "only the filled slot is written");
                Assert.That(dialogs.Errors, Is.Empty);
            });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void SaveIcon_WhenCancelled_WritesNothing()
    {
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(16, 32)],
            SaveIconResult = null,
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);
        vm.SubImages[0].Bitmap = BitmapTestHelpers.SolidColor(16, 16, 1, 2, 3, 255);

        vm.SaveIconCommand.Execute(null);

        Assert.That(dialogs.Errors, Is.Empty);
    }

    // --- Zoom ---------------------------------------------------------------

    [Test]
    public void ZoomIn_MultipliesTheZoomLevel()
    {
        var vm = Create(new FakeDialogService());

        vm.ZoomInCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(1.25).Within(1e-9));
    }

    [Test]
    public void ZoomOut_DividesTheZoomLevel()
    {
        var vm = Create(new FakeDialogService());

        vm.ZoomOutCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(1.0 / 1.25).Within(1e-9));
    }

    [Test]
    public void ZoomIn_IsClampedToTheMaximum()
    {
        var vm = Create(new FakeDialogService());

        for (int i = 0; i < 100; i++)
            vm.ZoomInCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(16.0));
    }

    [Test]
    public void ZoomOut_IsClampedToTheMinimum()
    {
        var vm = Create(new FakeDialogService());

        for (int i = 0; i < 100; i++)
            vm.ZoomOutCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(0.1));
    }

    [Test]
    public void ZoomReset_ReturnsToOneHundredPercent()
    {
        var vm = Create(new FakeDialogService());
        vm.ZoomInCommand.Execute(null);

        vm.ZoomResetCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(1.0));
    }
}
