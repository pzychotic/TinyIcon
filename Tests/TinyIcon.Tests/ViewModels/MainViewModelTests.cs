using System.IO;
using TinyIcon.Models;
using TinyIcon.Services;
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
    public void SaveIcon_Writes256x256x32SlotAsPng()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        var dialogs = new FakeDialogService
        {
            NewIconResult = [(256, 32)],
            SaveIconResult = path,
        };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);
        vm.SubImages[0].Bitmap = BitmapTestHelpers.SolidColor(256, 256, 1, 2, 3, 255);

        try
        {
            vm.SaveIconCommand.Execute(null);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6 + 12; // dwImageOffset within the single ICONDIRENTRY
            reader.BaseStream.Position = reader.ReadInt32();
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadBytes(8), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
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

    // --- Open Icon ----------------------------------------------------------

    /// <summary>Writes a two-entry icon to a temp file and returns its path.</summary>
    private static string WriteTempIcon()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        IconFileWriter.Write(path,
        [
            new IconImage(BitmapTestHelpers.SolidColor(16, 16, 1, 2, 3, 255), 24, IconImageFormat.Bmp),
            new IconImage(BitmapTestHelpers.SolidColor(32, 32, 4, 5, 6, 255), 32, IconImageFormat.Bmp),
        ]);
        return path;
    }

    [Test]
    public void OpenIcon_ReplacesSlotsWithTheEntriesOfTheFile()
    {
        string path = WriteTempIcon();
        var dialogs = new FakeDialogService { NewIconResult = [(48, 32)], OpenIconResult = path };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        try
        {
            vm.OpenIconCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(dialogs.OpenIconCalls, Is.EqualTo(1));
                Assert.That(dialogs.Errors, Is.Empty);
                Assert.That(vm.SubImages.Select(s => (s.Width, s.Height, s.Bpp)),
                    Is.EqualTo([(16, 16, 24), (32, 32, 32)]));
                Assert.That(vm.SubImages.All(s => s.HasImage), Is.True);
                Assert.That(vm.SelectedSubImage, Is.SameAs(vm.SubImages[0]));
                Assert.That(vm.SaveIconCommand.CanExecute(null), Is.True);
                Assert.That(vm.ImportImageCommand.CanExecute(null), Is.True);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void OpenIcon_ThenSaveIcon_PreservesTheDepthOfEveryEntry()
    {
        // The point of the feature: a legacy icon must not be promoted to 24/32 bpp by a round trip
        // through the app.
        string source = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        string saved = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        IconFileWriter.Write(source,
        [
            new IconImage(BitmapTestHelpers.DistinctColors(16, 16, 12), 4, IconImageFormat.Bmp),
            new IconImage(BitmapTestHelpers.DistinctColors(32, 32, 200), 8, IconImageFormat.Bmp),
            new IconImage(BitmapTestHelpers.SolidColor(48, 48, 8, 82, 255, 255), 16, IconImageFormat.Bmp),
        ]);

        var dialogs = new FakeDialogService { OpenIconResult = source, SaveIconResult = saved };
        var vm = Create(dialogs);

        try
        {
            vm.OpenIconCommand.Execute(null);
            vm.SaveIconCommand.Execute(null);

            var reopened = IconFileReader.Read(saved);
            Assert.Multiple(() =>
            {
                Assert.That(dialogs.Errors, Is.Empty);
                Assert.That(vm.SubImages.Select(s => s.Bpp), Is.EqualTo([4, 8, 16]), "slots");
                Assert.That(reopened.Select(i => i.Bpp), Is.EqualTo([4, 8, 16]), "saved file");
            });
        }
        finally
        {
            File.Delete(source);
            File.Delete(saved);
        }
    }

    [Test]
    public void OpenIcon_WhenCancelled_LeavesSlotsUntouched()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)], OpenIconResult = null };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        vm.OpenIconCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([16]));
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void OpenIcon_WhenTheFileIsNotAnIcon_ReportsAnErrorAndKeepsTheSlots()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");
        File.WriteAllText(path, "definitely not an icon");
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)], OpenIconResult = path };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        try
        {
            vm.OpenIconCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(dialogs.Errors, Has.Count.EqualTo(1));
                Assert.That(dialogs.Errors[0], Does.StartWith("Could not open icon:"));
                Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([16]));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- Drop ---------------------------------------------------------------

    [Test]
    public void Drop_WithAnIconFile_OpensIt()
    {
        string path = WriteTempIcon();
        var vm = Create(new FakeDialogService());

        try
        {
            vm.DropCommand.Execute([path]);

            Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([16, 32]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Drop_WithAnImageFile_ImportsItIntoTheSlots()
    {
        string path = BitmapTestHelpers.WriteTempPng(64, 64);
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32), (32, 32)] };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        try
        {
            vm.DropCommand.Execute([path]);

            Assert.Multiple(() =>
            {
                Assert.That(vm.SubImages.All(s => s.HasImage), Is.True);
                Assert.That(dialogs.OpenImageCalls, Is.EqualTo(0), "no dialog for a dropped file");
                Assert.That(dialogs.Errors, Is.Empty);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Drop_WithAnImageFileAndNoSlots_ReportsAnError()
    {
        string path = BitmapTestHelpers.WriteTempPng(64, 64);
        var dialogs = new FakeDialogService();
        var vm = Create(dialogs);

        try
        {
            vm.DropCommand.Execute([path]);

            Assert.Multiple(() =>
            {
                Assert.That(vm.SubImages, Is.Empty);
                Assert.That(dialogs.Errors, Has.Count.EqualTo(1));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Drop_WithNoFiles_DoesNothing()
    {
        var dialogs = new FakeDialogService { NewIconResult = [(16, 32)] };
        var vm = Create(dialogs);
        vm.NewIconCommand.Execute(null);

        vm.DropCommand.Execute(null);
        vm.DropCommand.Execute(Array.Empty<string>());

        Assert.Multiple(() =>
        {
            Assert.That(vm.SubImages[0].HasImage, Is.False);
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    // --- Delete Sub-Image --------------------------------------------------

    /// <summary>A view model with one empty 32-bit slot per size, the first one selected.</summary>
    private static MainViewModel CreateWithSlots(params int[] sizes)
    {
        var vm = Create(new FakeDialogService { NewIconResult = sizes.Select(s => (s, 32)).ToList() });
        vm.NewIconCommand.Execute(null);
        return vm;
    }

    [Test]
    public void DeleteSubImage_WithItem_RemovesThatItem()
    {
        var vm = CreateWithSlots(16, 32, 48);

        vm.DeleteSubImageCommand.Execute(vm.SubImages[1]);

        Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([16, 48]));
    }

    [Test]
    public void DeleteSubImage_SelectedMiddleItem_SelectsTheNextOne()
    {
        var vm = CreateWithSlots(16, 32, 48);
        vm.SelectedSubImage = vm.SubImages[1];

        vm.DeleteSubImageCommand.Execute(vm.SubImages[1]);

        Assert.That(vm.SelectedSubImage?.Width, Is.EqualTo(48));
    }

    [Test]
    public void DeleteSubImage_SelectedLastItem_SelectsThePreviousOne()
    {
        var vm = CreateWithSlots(16, 32, 48);
        vm.SelectedSubImage = vm.SubImages[2];

        vm.DeleteSubImageCommand.Execute(vm.SubImages[2]);

        Assert.That(vm.SelectedSubImage?.Width, Is.EqualTo(32));
    }

    [Test]
    public void DeleteSubImage_UnselectedItem_KeepsTheSelection()
    {
        var vm = CreateWithSlots(16, 32, 48);
        var selected = vm.SubImages[0];

        vm.DeleteSubImageCommand.Execute(vm.SubImages[2]);

        Assert.That(vm.SelectedSubImage, Is.SameAs(selected));
    }

    [Test]
    public void DeleteSubImage_WithoutItem_RemovesTheSelectedOne()
    {
        var vm = CreateWithSlots(16, 32, 48);
        vm.SelectedSubImage = vm.SubImages[1];

        vm.DeleteSubImageCommand.Execute(null);

        Assert.That(vm.SubImages.Select(s => s.Width), Is.EqualTo([16, 48]));
    }

    [Test]
    public void DeleteSubImage_OnlyItem_LeavesAnEmptyIcon()
    {
        var vm = CreateWithSlots(16);

        vm.DeleteSubImageCommand.Execute(vm.SubImages[0]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SubImages, Is.Empty);
            Assert.That(vm.SelectedSubImage, Is.Null);
            Assert.That(vm.ImportImageCommand.CanExecute(null), Is.False);
            Assert.That(vm.SaveIconCommand.CanExecute(null), Is.False);
            Assert.That(vm.DeleteSubImageCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void DeleteSubImage_CanExecuteWithoutItem_FollowsTheSelection()
    {
        var vm = CreateWithSlots(16, 32);
        var canExecuteChanged = 0;
        vm.DeleteSubImageCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.SelectedSubImage = null;

        Assert.Multiple(() =>
        {
            Assert.That(vm.DeleteSubImageCommand.CanExecute(null), Is.False);
            Assert.That(vm.DeleteSubImageCommand.CanExecute(vm.SubImages[0]), Is.True);
            Assert.That(canExecuteChanged, Is.GreaterThan(0));
        });
    }

    // --- Zoom ---------------------------------------------------------------

    [Test]
    public void ZoomIn_IncreasesTheZoomLevel()
    {
        var vm = Create(new FakeDialogService());

        vm.ZoomInCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(2.0).Within(1e-9));
    }

    [Test]
    public void ZoomOut_DecreasesTheZoomLevel()
    {
        var vm = Create(new FakeDialogService());

        vm.ZoomLevel = 4.0;
        vm.ZoomOutCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(3.0).Within(1e-9));
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

        vm.ZoomLevel = 16.0;
        for (int i = 0; i < 100; i++)
            vm.ZoomOutCommand.Execute(null);

        Assert.That(vm.ZoomLevel, Is.EqualTo(1.0));
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
