using System.IO;
using System.Windows.Media.Imaging;
using TinyIcon.Models;
using TinyIcon.Services;
using TinyIcon.Tests.TestSupport;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class IconFileWriterTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static IconImage Slot(int size, int bpp, IconImageFormat format = IconImageFormat.Bmp) =>
        new(BitmapTestHelpers.SolidColor(size, size, 10, 20, 30, 255), bpp, format);

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
    public void Write_Zeroes24BppColourDataUnderTheTransparencyMask()
    {
        string path = TempIcoPath();
        // Alpha 100 is below the mask threshold, so every pixel is masked out and its
        // colour must not leak into the XOR data (legacy renderers XOR it over the screen).
        var bitmap = BitmapTestHelpers.SolidColor(16, 16, 10, 20, 30, 100);
        try
        {
            IconFileWriter.Write(path, [new IconImage(bitmap, 24, IconImageFormat.Bmp)]);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6 + 12; // dwImageOffset within the single ICONDIRENTRY
            reader.BaseStream.Position = reader.ReadInt32() + 40; // skip BITMAPINFOHEADER
            var xorData = reader.ReadBytes(16 * 3 * 16); // 16 rows of 16 BGR pixels (stride already 4-byte aligned)
            Assert.That(xorData, Is.All.Zero);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_PngEntry_EmbedsAPngStreamAtTheRecordedOffset()
    {
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, [Slot(256, 32, IconImageFormat.Png)]);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6 + 8; // dwBytesInRes within the single ICONDIRENTRY
            int length = reader.ReadInt32();
            int offset = reader.ReadInt32();

            reader.BaseStream.Position = offset;
            Assert.Multiple(() =>
            {
                Assert.That(reader.ReadBytes(8), Is.EqualTo(PngSignature));
                Assert.That(length, Is.EqualTo(reader.BaseStream.Length - offset), "entry data runs to end of file");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_PngEntry_RoundTripsThroughPngBitmapDecoder()
    {
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, [Slot(256, 32, IconImageFormat.Png)]);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6 + 8; // dwBytesInRes within the single ICONDIRENTRY
            int length = reader.ReadInt32();
            reader.BaseStream.Position = reader.ReadInt32();

            using var png = new MemoryStream(reader.ReadBytes(length));
            var frame = new PngBitmapDecoder(
                png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Multiple(() =>
            {
                Assert.That(frame.PixelWidth, Is.EqualTo(256));
                Assert.That(frame.PixelHeight, Is.EqualTo(256));
                Assert.That(frame.Format.BitsPerPixel, Is.EqualTo(32));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_HonoursTheFormatIndependentlyOfSize()
    {
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, [Slot(16, 32, IconImageFormat.Png)]);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 6 + 12; // dwImageOffset within the single ICONDIRENTRY
            reader.BaseStream.Position = reader.ReadInt32();
            Assert.That(reader.ReadBytes(8), Is.EqualTo(PngSignature));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_MixedFormats_ChainsTheOffsetsCorrectly()
    {
        string path = TempIcoPath();
        var slots = new[] { Slot(16, 32), Slot(256, 32, IconImageFormat.Png), Slot(32, 24) };
        try
        {
            IconFileWriter.Write(path, slots);

            // Each entry's data must start with its format's magic bytes: biSize == 40 for
            // a DIB, the PNG signature for a PNG stream.
            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Multiple(() =>
            {
                Assert.That(ReadBlobStart(reader, entryIndex: 0, count: 4), Is.EqualTo(BitConverter.GetBytes(40)), "16px DIB");
                Assert.That(ReadBlobStart(reader, entryIndex: 1, count: 8), Is.EqualTo(PngSignature), "256px PNG");
                Assert.That(ReadBlobStart(reader, entryIndex: 2, count: 4), Is.EqualTo(BitConverter.GetBytes(40)), "32px DIB");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] ReadBlobStart(BinaryReader reader, int entryIndex, int count)
    {
        reader.BaseStream.Position = 6 + 16 * entryIndex + 12; // dwImageOffset within the entry
        reader.BaseStream.Position = reader.ReadInt32();
        return reader.ReadBytes(count);
    }

    [Test]
    public void Write_RoundTrips_DecodableBackToTheSameSizes()
    {
        string path = TempIcoPath();
        // DIB-only on purpose: WPF's IconBitmapDecoder cannot decode PNG frames.
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
