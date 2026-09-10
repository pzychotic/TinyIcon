using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TinyIcon.Models;
using TinyIcon.Services;
using TinyIcon.Tests.TestSupport;

namespace TinyIcon.Tests.Services;

[TestFixture]
public class IconFileReaderTests
{
    private static IconImage Slot(int size, int bpp, IconImageFormat format = IconImageFormat.Bmp, byte a = 255) =>
        new(BitmapTestHelpers.SolidColor(size, size, 10, 20, 30, a), bpp, format);

    private static string TempIcoPath() =>
        Path.Combine(Path.GetTempPath(), $"tinyicon-test-{Guid.NewGuid():N}.ico");

    /// <summary>Writes the given images with <see cref="IconFileWriter"/> and reads them straight back.</summary>
    private static IReadOnlyList<IconImage> RoundTrip(params IconImage[] images)
    {
        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, images);
            return IconFileReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] GetPixels(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    // --- Round-tripping what the writer produces ---

    [Test]
    public void Read_RoundTripsA32BppEntry()
    {
        var images = RoundTrip(Slot(16, 32));

        Assert.That(images, Has.Count.EqualTo(1));
        var image = images[0];
        Assert.Multiple(() =>
        {
            Assert.That(image.Bpp, Is.EqualTo(32));
            Assert.That(image.Format, Is.EqualTo(IconImageFormat.Bmp));
            Assert.That(image.Bitmap.PixelWidth, Is.EqualTo(16));
            Assert.That(image.Bitmap.PixelHeight, Is.EqualTo(16));
            Assert.That(image.Bitmap.Format, Is.EqualTo(PixelFormats.Bgra32));
        });

        var pixels = GetPixels(image.Bitmap);
        Assert.That(pixels[0..4], Is.EqualTo(new byte[] { 10, 20, 30, 255 }));
    }

    [TestCase(1, 2)]
    [TestCase(4, 16)]
    [TestCase(8, 256)]
    public void Read_RoundTripsAPalettizedEntryWithoutLosingAColour(int bpp, int maxColors)
    {
        // An image already within the depth's colour budget must survive write-then-read untouched: this
        // is what makes opening a legacy icon and saving it again lossless. One colour is reserved for
        // black (index 0), so fill the budget minus that.
        var source = BitmapTestHelpers.DistinctColors(16, 16, maxColors - 1);
        var expected = GetPixels(source);

        var images = RoundTrip(new IconImage(source, bpp, IconImageFormat.Bmp));

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bpp, Is.EqualTo(bpp));
            Assert.That(GetPixels(images[0].Bitmap), Is.EqualTo(expected));
        });
    }

    [Test]
    public void Read_RoundTripsA16BppEntryBitForBit()
    {
        // Colours already snapped to 5-5-5 come back exactly, because Expand5 inverts the writer's shift.
        var source = BitmapTestHelpers.SolidColor(16, 16, b: 0x08, g: 0x52, r: 0xFF, a: 255);
        var images = RoundTrip(new IconImage(source, 16, IconImageFormat.Bmp));

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bpp, Is.EqualTo(16));
            Assert.That(GetPixels(images[0].Bitmap)[0..4], Is.EqualTo(new byte[] { 0x08, 0x52, 0xFF, 255 }));
        });
    }

    [Test]
    public void Read_RestoresTransparencyFromTheAndMaskOfAPalettizedEntry()
    {
        var source = BitmapTestHelpers.HalfTransparent(8, 8, b: 10, g: 20, r: 30);
        var images = RoundTrip(new IconImage(source, 8, IconImageFormat.Bmp));
        var pixels = GetPixels(images[0].Bitmap);

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bpp, Is.EqualTo(8));
            Assert.That(pixels[0..4], Is.EqualTo(new byte[] { 10, 20, 30, 255 }), "opaque half");
            Assert.That(pixels[16..20], Is.EqualTo(new byte[] { 0, 0, 0, 0 }), "transparent half");
        });
    }

    [Test]
    public void Read_RestoresTransparencyFromTheAndMaskOfA24BppEntry()
    {
        // The writer stores fully transparent pixels as black with their mask bit set.
        var images = RoundTrip(Slot(16, 24, a: 0));
        var pixels = GetPixels(images[0].Bitmap);

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bpp, Is.EqualTo(24));
            Assert.That(pixels, Is.All.EqualTo(0));
        });
    }

    [Test]
    public void Read_DecodesAPngEntryAndItsZeroDirectoryDimension()
    {
        // 256 is stored as 0 in the directory, so decoding it proves the special case works.
        var images = RoundTrip(Slot(256, 32, IconImageFormat.Png));

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Format, Is.EqualTo(IconImageFormat.Png));
            Assert.That(images[0].Bpp, Is.EqualTo(32));
            Assert.That(images[0].Bitmap.PixelWidth, Is.EqualTo(256));
            Assert.That(images[0].Bitmap.PixelHeight, Is.EqualTo(256));
        });
    }

    [Test]
    public void Read_WithMixedFormats_ReturnsEntriesInFileOrder()
    {
        var images = RoundTrip(Slot(16, 24), Slot(256, 32, IconImageFormat.Png), Slot(32, 32));

        Assert.Multiple(() =>
        {
            Assert.That(images.Select(i => i.Bitmap.PixelWidth), Is.EqualTo([16, 256, 32]));
            Assert.That(images.Select(i => i.Bpp), Is.EqualTo([24, 32, 32]));
            Assert.That(images.Select(i => i.Format), Is.EqualTo(
                [IconImageFormat.Bmp, IconImageFormat.Png, IconImageFormat.Bmp]));
        });
    }

    // --- Entries the app itself never writes ---

    [Test]
    public void Read_DecodesAPalettizedEntryAndKeepsItsColourDepth()
    {
        // 4×2, 8 bpp, two-colour table: index 0 = blue, index 1 = red.
        byte[] palette = [255, 0, 0, 0, /**/ 0, 0, 255, 0];
        byte[] rows =
        [
            1, 0, 1, 0, // bottom row
            0, 1, 0, 1, // top row
        ];
        byte[] dib = BuildDib(4, 2, bpp: 8, palette, rows, mask: new byte[4 * 2]);
        var images = IconFileReader.Read(BuildIco((4, 2, 8, dib)));

        var pixels = GetPixels(images[0].Bitmap);
        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bpp, Is.EqualTo(8));
            Assert.That(pixels[0..4], Is.EqualTo(new byte[] { 255, 0, 0, 255 }), "top-left is blue");
            Assert.That(pixels[4..8], Is.EqualTo(new byte[] { 0, 0, 255, 255 }), "next pixel is red");
        });
    }

    [Test]
    public void Read_ThenWrite_PreservesAForeignPalettizedEntry()
    {
        // End to end on bytes this project did not author: a hand-built 8 bpp entry must come back out at
        // 8 bpp, with its colours intact and in a file the platform codec still accepts.
        byte[] palette = [255, 0, 0, 0, /**/ 0, 0, 255, 0]; // index 0 = blue, index 1 = red
        byte[] rows = new byte[8 * 4];
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
                rows[y * 8 + x] = (byte)((x + y) & 1);
        }

        byte[] dib = BuildDib(4, 4, bpp: 8, palette, rows, mask: new byte[4 * 4]);
        var original = IconFileReader.Read(BuildIco((4, 4, 8, dib)));
        var expected = GetPixels(original[0].Bitmap);

        string path = TempIcoPath();
        try
        {
            IconFileWriter.Write(path, original);
            var reopened = IconFileReader.Read(path);

            using var stream = File.OpenRead(path);
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            Assert.Multiple(() =>
            {
                Assert.That(reopened[0].Bpp, Is.EqualTo(8), "depth survives");
                Assert.That(GetPixels(reopened[0].Bitmap), Is.EqualTo(expected), "colours survive");
                Assert.That(decoder.Frames, Has.Count.EqualTo(1), "platform codec accepts it");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Read_KeepsNonSquareDimensions()
    {
        int colorStride = 32 * 3;
        byte[] dib = BuildDib(32, 16, bpp: 24, palette: [], new byte[colorStride * 16], new byte[4 * 16]);
        var images = IconFileReader.Read(BuildIco((32, 16, 24, dib)));

        Assert.Multiple(() =>
        {
            Assert.That(images[0].Bitmap.PixelWidth, Is.EqualTo(32));
            Assert.That(images[0].Bitmap.PixelHeight, Is.EqualTo(16));
        });
    }

    [Test]
    public void Read_With32BppEntryWithoutAlpha_FallsBackToTheAndMask()
    {
        // Every alpha byte is zero, so the AND mask is the only transparency information available.
        var rows = new byte[4 * 4 * 2];
        for (int i = 0; i < rows.Length; i += 4)
            rows[i + 2] = 200; // opaque-looking red, alpha left at 0

        var mask = new byte[4 * 2];
        mask[0] = 0x80; // bottom row, first pixel transparent
        mask[4] = 0x80; // top row, first pixel transparent

        byte[] dib = BuildDib(4, 2, bpp: 32, palette: [], rows, mask);
        var images = IconFileReader.Read(BuildIco((4, 2, 32, dib)));

        var pixels = GetPixels(images[0].Bitmap);
        Assert.Multiple(() =>
        {
            Assert.That(pixels[3], Is.EqualTo(0), "masked pixel stays transparent");
            Assert.That(pixels[7], Is.EqualTo(255), "unmasked pixel becomes opaque");
            Assert.That(pixels[6], Is.EqualTo(200), "colour survives");
        });
    }

    [Test]
    public void Read_SkipsUndecodableEntriesAndKeepsTheRest()
    {
        byte[] good = BuildDib(4, 2, bpp: 24, palette: [], new byte[4 * 3 * 2], new byte[4 * 2]);
        byte[] compressed = BuildDib(4, 2, bpp: 24, palette: [], new byte[4 * 3 * 2], new byte[4 * 2]);
        WriteInt32(compressed, 16, 2); // biCompression = BI_RLE4

        var images = IconFileReader.Read(BuildIco((4, 2, 24, compressed), (4, 2, 24, good)));

        Assert.That(images, Has.Count.EqualTo(1));
    }

    // --- Invalid input ---

    [Test]
    public void Read_WithNonIconBytes_Throws()
    {
        Assert.That(
            () => IconFileReader.Read("not an icon at all"u8.ToArray()),
            Throws.InstanceOf<InvalidDataException>());
    }

    [Test]
    public void Read_WithACursorFile_Throws()
    {
        var bytes = BuildIco((4, 2, 24, new byte[64]));
        bytes[2] = 2; // type = cursor

        Assert.That(() => IconFileReader.Read(bytes), Throws.InstanceOf<InvalidDataException>());
    }

    [Test]
    public void Read_WithATruncatedDirectory_Throws()
    {
        byte[] bytes = [0, 0, 1, 0, 3, 0, 0, 0]; // announces three entries, holds none

        Assert.That(() => IconFileReader.Read(bytes), Throws.InstanceOf<InvalidDataException>());
    }

    [Test]
    public void Read_WhenNoEntryIsDecodable_Throws()
    {
        byte[] tooSmall = new byte[8];
        Assert.That(
            () => IconFileReader.Read(BuildIco((4, 2, 24, tooSmall))),
            Throws.InstanceOf<InvalidDataException>());
    }

    // --- Hand-built icon bytes ---

    /// <summary>Builds a DIB entry blob: BITMAPINFOHEADER + colour table + XOR data + AND mask.</summary>
    private static byte[] BuildDib(int width, int height, int bpp, byte[] palette, byte[] rows, byte[] mask)
    {
        var dib = new byte[40 + palette.Length + rows.Length + mask.Length];
        WriteInt32(dib, 0, 40);          // biSize
        WriteInt32(dib, 4, width);       // biWidth
        WriteInt32(dib, 8, height * 2);  // biHeight (XOR + AND)
        dib[12] = 1;                     // biPlanes
        dib[14] = (byte)bpp;             // biBitCount
        WriteInt32(dib, 32, bpp <= 8 ? palette.Length / 4 : 0); // biClrUsed

        palette.CopyTo(dib, 40);
        rows.CopyTo(dib, 40 + palette.Length);
        mask.CopyTo(dib, 40 + palette.Length + rows.Length);
        return dib;
    }

    /// <summary>Wraps ready-made entry blobs in an ICONDIR + ICONDIRENTRY records.</summary>
    private static byte[] BuildIco(params (int Width, int Height, int Bpp, byte[] Data)[] entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)entries.Length);

        int offset = 6 + 16 * entries.Length;
        foreach (var e in entries)
        {
            writer.Write((byte)(e.Width >= 256 ? 0 : e.Width));
            writer.Write((byte)(e.Height >= 256 ? 0 : e.Height));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)e.Bpp);
            writer.Write(e.Data.Length);
            writer.Write(offset);
            offset += e.Data.Length;
        }

        foreach (var e in entries)
            writer.Write(e.Data);

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
