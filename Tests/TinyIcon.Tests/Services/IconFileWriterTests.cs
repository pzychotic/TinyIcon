using System.IO;
using System.Windows.Media.Imaging;
using TinyIcon.Models;
using TinyIcon.Services;
using TinyIcon.Tests.TestSupport;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class IconFileWriterTests
{
    private static IconImage Slot(int size, int bpp) =>
        new(BitmapTestHelpers.SolidColor(size, size, 10, 20, 30, 255), bpp);

    private static string TempIcoPath() =>
        Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");

    [Test]
    public void Write_WithNoImages_Throws()
    {
        Assert.That(
            () => IconFileWriter.Write(TempIcoPath(), []),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Write_ProducesAValidIconDirectoryHeader()
    {
        string path = TempIcoPath();
        var slots = new[] { Slot(16, 24), Slot(32, 32) };
        try
        {
            IconFileWriter.Write(path, slots);

            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadUInt16(), Is.EqualTo(0), "reserved");
                Assert.That(reader.ReadUInt16(), Is.EqualTo(1), "type = icon");
                Assert.That(reader.ReadUInt16(), Is.EqualTo(2), "image count");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_RecordsTheBppOfEachEntry()
    {
        string path = TempIcoPath();
        var slots = new[] { Slot(16, 24), Slot(32, 32) };
        try
        {
            IconFileWriter.Write(path, slots);

            var bpps = ReadEntryBpps(path);
            Assert.That(bpps, Is.EqualTo([24, 32]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_Stores256AsZeroInTheDirectoryDimensions()
    {
        string path = TempIcoPath();
        var slots = new[] { Slot(256, 32) };
        try
        {
            IconFileWriter.Write(path, slots);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6; // skip ICONDIR, land on first ICONDIRENTRY
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadByte(), Is.EqualTo(0), "width byte for 256");
                Assert.That(reader.ReadByte(), Is.EqualTo(0), "height byte for 256");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_RoundTrips_DecodableBackToTheSameSizes()
    {
        string path = TempIcoPath();
        var slots = new[] { Slot(16, 32), Slot(32, 24), Slot(48, 32) };
        try
        {
            IconFileWriter.Write(path, slots);

            using var stream = File.OpenRead(path);
            var decoder = new IconBitmapDecoder(
                stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            var sizes = decoder.Frames.Select(f => f.PixelWidth).OrderBy(w => w);
            Assert.That(sizes, Is.EqualTo([16, 32, 48]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<int> ReadEntryBpps(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 4;
        int count = reader.ReadUInt16();

        var bpps = new List<int>();
        for (int i = 0; i < count; i++)
        {
            long entry = 6 + 16 * i;
            reader.BaseStream.Position = entry + 6; // bBitCount is at offset 6 within the entry
            bpps.Add(reader.ReadUInt16());
        }
        return bpps;
    }
}
