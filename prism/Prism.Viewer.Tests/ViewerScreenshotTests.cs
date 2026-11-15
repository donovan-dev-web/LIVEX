using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerScreenshotTests
{
    [Fact]
    public void EncodePngWritesSignatureAndHeader()
    {
        var png = ViewerScreenshot.EncodePng(1, 2, new byte[8]);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8, 4)));
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
        Assert.Equal(0, png[26]);
        Assert.Equal(0, png[27]);
        Assert.Equal(0, png[28]);
    }

    [Fact]
    public void EncodePngStoresPixelsTopRowFirstWithFilterBytes()
    {
        // Tampon OpenGL : ligne du bas (rouge) puis ligne du haut (vert).
        var pixels = new byte[]
        {
            255, 0, 0, 255,
            0, 255, 0, 255,
        };

        var pixelsTopDown = DecodeIdat(ViewerScreenshot.EncodePng(1, 2, pixels));

        Assert.Equal(new byte[] { 0, 0, 255, 0, 255, 0, 255, 0, 0, 255 }, pixelsTopDown);
    }

    [Fact]
    public void EncodePngWritesValidChunkChecksums()
    {
        var png = ViewerScreenshot.EncodePng(3, 2, new byte[24]);

        var offset = 8;
        var chunks = new List<string>();
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var stored = BinaryPrimitives.ReadUInt32BigEndian(
                png.AsSpan(offset + 8 + length, 4));
            var computed = Crc32(png.AsSpan(offset + 4, 4 + length));
            Assert.Equal(computed, stored);
            chunks.Add(type);
            offset += 12 + length;
        }

        Assert.Equal(["IHDR", "IDAT", "IEND"], chunks);
        Assert.Equal(png.Length, offset);
    }

    [Fact]
    public void EncodePngRejectsPixelBuffersThatDoNotMatchDimensions()
    {
        Assert.Throws<ArgumentException>(() => ViewerScreenshot.EncodePng(2, 2, new byte[8]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScreenshot.EncodePng(0, 2, []));
    }

    private static byte[] DecodeIdat(byte[] png)
    {
        var offset = 8;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT")
            {
                using var compressed = new MemoryStream(png, offset + 8, length);
                using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                zlib.CopyTo(raw);
                return raw.ToArray();
            }

            offset += 12 + length;
        }

        throw new InvalidOperationException("PNG sans chunk IDAT.");
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
