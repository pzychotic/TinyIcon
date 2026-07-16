using System.IO;
using TinyIcon.Models;
using TinyIcon.Services;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class JsonSettingsServiceTests
{
    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _path = Path.Combine(_directory, "Settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void Save_ThenLoad_RoundTripsAllProperties()
    {
        var service = new JsonSettingsService(_directory);
        var settings = new AppSettings
        {
            WindowLeft = 10.5,
            WindowTop = -20,
            WindowWidth = 1024,
            WindowHeight = 768,
            WindowMaximized = true,
            Bpp24Sizes = [16, 48],
            Bpp32Sizes = [32, 256],
            Bpp24Enabled = true,
            Bpp32Enabled = false,
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.WindowLeft, Is.EqualTo(10.5));
            Assert.That(loaded.WindowTop, Is.EqualTo(-20));
            Assert.That(loaded.WindowWidth, Is.EqualTo(1024));
            Assert.That(loaded.WindowHeight, Is.EqualTo(768));
            Assert.That(loaded.WindowMaximized, Is.True);
            Assert.That(loaded.Bpp24Sizes, Is.EqualTo([16, 48]));
            Assert.That(loaded.Bpp32Sizes, Is.EqualTo([32, 256]));
            Assert.That(loaded.Bpp24Enabled, Is.True);
            Assert.That(loaded.Bpp32Enabled, Is.False);
        });
    }

    [Test]
    public void Load_LeavesTheEnabledFlagsNullWhenTheFilePredatesThem()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "Bpp24Sizes": [16], "Bpp32Sizes": [32] }""");

        var service = new JsonSettingsService(_directory);
        var loaded = service.Load();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Bpp24Enabled, Is.Null);
            Assert.That(loaded.Bpp32Enabled, Is.Null);
        });
    }

    [Test]
    public void Save_CreatesTheMissingDirectory()
    {
        var service = new JsonSettingsService(_directory);

        service.Save(new AppSettings());

        Assert.That(File.Exists(_path), Is.True);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileIsMissing()
    {
        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileIsMalformed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not valid json !");

        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileContainsJsonNull()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "null");

        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }
}
