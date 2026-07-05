using System.IO;
using TinyIcon.Models;
using TinyIcon.Services;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class SettingsServiceTests
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
    public void SaveTo_ThenLoadFrom_RoundTripsAllProperties()
    {
        var settings = new AppSettings
        {
            WindowLeft = 10.5,
            WindowTop = -20,
            WindowWidth = 1024,
            WindowHeight = 768,
            WindowMaximized = true,
            Bpp24Sizes = [16, 48],
            Bpp32Sizes = [32, 256],
        };

        SettingsService.SaveTo(_path, settings);
        var loaded = SettingsService.LoadFrom(_path);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.WindowLeft, Is.EqualTo(10.5));
            Assert.That(loaded.WindowTop, Is.EqualTo(-20));
            Assert.That(loaded.WindowWidth, Is.EqualTo(1024));
            Assert.That(loaded.WindowHeight, Is.EqualTo(768));
            Assert.That(loaded.WindowMaximized, Is.True);
            Assert.That(loaded.Bpp24Sizes, Is.EqualTo([16, 48]));
            Assert.That(loaded.Bpp32Sizes, Is.EqualTo([32, 256]));
        });
    }

    [Test]
    public void SaveTo_CreatesTheMissingDirectory()
    {
        SettingsService.SaveTo(_path, new AppSettings());

        Assert.That(File.Exists(_path), Is.True);
    }

    [Test]
    public void LoadFrom_ReturnsDefaultsWhenTheFileIsMissing()
    {
        var loaded = SettingsService.LoadFrom(_path);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.WindowLeft, Is.Null);
            Assert.That(loaded.WindowMaximized, Is.False);
            Assert.That(loaded.Bpp24Sizes, Is.Null);
            Assert.That(loaded.Bpp32Sizes, Is.Null);
        });
    }

    [Test]
    public void LoadFrom_ReturnsDefaultsWhenTheFileIsMalformed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not valid json !");

        var loaded = SettingsService.LoadFrom(_path);

        Assert.That(loaded.WindowWidth, Is.Null);
    }

    [Test]
    public void LoadFrom_ReturnsDefaultsWhenTheFileContainsJsonNull()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "null");

        var loaded = SettingsService.LoadFrom(_path);

        Assert.That(loaded.WindowHeight, Is.Null);
    }
}
