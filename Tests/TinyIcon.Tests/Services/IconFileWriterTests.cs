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

    [Test]
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    [TestCase(24)]
    [TestCase(32)]
    public void Write_StoresEachSupportedDepthAtThatDepth(int bpp)
    {
        // Palettized and 16-bit entries arrive via IconFileReader; they must save back at their own depth
        // rather than being promoted, and the directory and the bitmap header have to agree on it.
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, [Slot(16, bpp)]);

            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Multiple(() =>
            {
                Assert.That(ReadEntryBpps(path), Is.EqualTo([bpp]), "directory entry");
                // biBitCount sits 14 bytes into the BITMAPINFOHEADER.
                Assert.That(BitConverter.ToUInt16(ReadBlobStart(reader, entryIndex: 0, count: 16), 14),
                    Is.EqualTo(bpp), "bitmap header");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_StoresAnUnrecognisedDepthAsA24BitEntry()
    {
        // 2 bpp is a legal DIB depth but not one IconFileReader decodes, so writing it would produce a
        // file we could not open again.
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, [Slot(16, 2)]);
            Assert.That(ReadEntryBpps(path), Is.EqualTo([24]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_RecordsTheColourTableOfAPalettizedEntry()
    {
        string path = TempIcoPath();
        var slot = new IconImage(BitmapTestHelpers.DistinctColors(8, 8, 5), 8, IconImageFormat.Bmp);
        try
        {
            IconFileWriter.Write(path, [slot]);

            using var reader = new BinaryReader(File.OpenRead(path));
            var header = ReadBlobStart(reader, entryIndex: 0, count: 40);
            int clrUsed = BitConverter.ToInt32(header, 32); // biClrUsed

            Assert.Multiple(() =>
            {
                Assert.That(clrUsed, Is.EqualTo(5), "biClrUsed");
                Assert.That(ReadEntryColorCounts(path), Is.EqualTo([5]), "bColorCount");
                // 40-byte header + colour table + 8 rows of indices + 8 rows of AND mask, each 4-byte aligned.
                Assert.That(ReadEntrySizes(path), Is.EqualTo([40 + 5 * 4 + 8 * 8 + 8 * 4]), "bytes in resource");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_ReservesPaletteIndexZeroForTransparentPixels()
    {
        // Masked-out pixels must be black, so index 0 is always opaque black and every transparent pixel
        // points at it. The left half of the source is opaque, the right half transparent.
        string path = TempIcoPath();
        var slot = new IconImage(BitmapTestHelpers.HalfTransparent(8, 8), 8, IconImageFormat.Bmp);
        try
        {
            IconFileWriter.Write(path, [slot]);

            using var reader = new BinaryReader(File.OpenRead(path));
            var blob = ReadBlobStart(reader, entryIndex: 0, count: 40 + 2 * 4 + 8);
            var firstEntry = blob[40..44];
            var lastRow = blob[(40 + 2 * 4)..]; // rows are bottom-up, so this is the image's last row

            Assert.Multiple(() =>
            {
                Assert.That(firstEntry[..3], Is.EqualTo(new byte[] { 0, 0, 0 }), "palette index 0 is black");
                Assert.That(lastRow[4..], Is.EqualTo(new byte[] { 0, 0, 0, 0 }), "transparent pixels use index 0");
                Assert.That(lastRow[..4], Is.All.Not.Zero, "opaque pixels do not");
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

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    public void Write_AtALegacyDepth_ProducesAnIconWindowsCanDecode(int bpp)
    {
        // Our own reader is not the audience for these files; Explorer's is. WPF's IconBitmapDecoder wraps
        // the same platform codec, so it is the closest check available from a test.
        string path = TempIcoPath();
        var slot = new IconImage(BitmapTestHelpers.HalfTransparent(32, 32), bpp, IconImageFormat.Bmp);
        try
        {
            IconFileWriter.Write(path, [slot]);

            using var stream = File.OpenRead(path);
            var decoder = new IconBitmapDecoder(
                stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            Assert.That(decoder.Frames.Select(f => f.PixelWidth), Is.EqualTo([32]));
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

    /// <summary>Reads bColorCount (offset 2) from every ICONDIRENTRY.</summary>
    private static List<int> ReadEntryColorCounts(string path) => ReadEntryFields(path, 2, r => r.ReadByte());

    /// <summary>Reads dwBytesInRes (offset 8) from every ICONDIRENTRY.</summary>
    private static List<int> ReadEntrySizes(string path) => ReadEntryFields(path, 8, r => r.ReadInt32());

    private static List<int> ReadEntryFields(string path, int fieldOffset, Func<BinaryReader, int> read)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 4;
        int count = reader.ReadUInt16();

        var values = new List<int>();
        for (int i = 0; i < count; i++)
        {
            reader.BaseStream.Position = 6 + 16 * i + fieldOffset;
            values.Add(read(reader));
        }
        return values;
    }
}
