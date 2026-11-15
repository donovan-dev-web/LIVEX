using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Prism.Viewer;

/// <summary>Image décodée : pixels RGBA 8 bits, lignes du haut vers le bas.</summary>
public sealed record ViewerPngImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// Décodeur PNG autonome du viewer : 8 bits par canal, couleur RGB ou RGBA, sans
/// entrelacement. Il couvre exactement ce que produisent les assets du dépôt (atlas, police
/// rendue en PNG le cas échéant) et refuse explicitement le reste plutôt que de deviner.
/// </summary>
public static class ViewerPng
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static ViewerPngImage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path));
    }

    public static ViewerPngImage Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
        {
            throw new InvalidDataException("Ce fichier n'est pas un PNG (signature absente).");
        }

        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = -1;
        using var idat = new MemoryStream();
        var offset = Signature.Length;
        var ended = false;
        while (!ended && offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length)
            {
                throw new InvalidDataException("Chunk PNG tronqué.");
            }

            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            var data = png.Slice(offset + 8, length);
            var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset + 8 + length, 4));
            if (Crc32(png.Slice(offset + 4, 4), data) != storedCrc)
            {
                throw new InvalidDataException($"Chunk PNG corrompu ({type}).");
            }

            switch (type)
            {
                case "IHDR":
                    if (length != 13)
                    {
                        throw new InvalidDataException("En-tête PNG invalide.");
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    bitDepth = data[8];
                    colorType = data[9];
                    if (data[10] != 0 || data[11] != 0)
                    {
                        throw new InvalidDataException("Compression ou filtre PNG non standard.");
                    }

                    if (data[12] != 0)
                    {
                        throw new InvalidDataException("PNG entrelacé non supporté.");
                    }

                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    ended = true;
                    break;
            }

            offset += 12 + length;
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Dimensions PNG absentes ou invalides.");
        }

        if (bitDepth != 8 || colorType is not (2 or 6))
        {
            throw new InvalidDataException(
                "Seuls les PNG 8 bits RGB ou RGBA non entrelacés sont supportés.");
        }

        var channels = colorType == 6 ? 4 : 3;
        var stride = checked(width * channels);
        var raw = Inflate(idat.ToArray());
        if (raw.Length != checked(height * (stride + 1)))
        {
            throw new InvalidDataException("Flux PNG de taille inattendue.");
        }

        var rows = Unfilter(raw, width, height, channels);
        var rgba = new byte[checked(width * height * 4)];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var source = pixel * channels;
            var target = pixel * 4;
            rgba[target] = rows[source];
            rgba[target + 1] = rows[source + 1];
            rgba[target + 2] = rows[source + 2];
            rgba[target + 3] = channels == 4 ? rows[source + 3] : (byte)255;
        }

        return new ViewerPngImage(width, height, rgba);
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int channels)
    {
        var stride = width * channels;
        var pixels = new byte[checked(stride * height)];
        var previous = new byte[stride];
        var source = 0;
        for (var row = 0; row < height; row++)
        {
            var filter = raw[source++];
            var target = row * stride;
            for (var index = 0; index < stride; index++)
            {
                var value = raw[source + index];
                var left = index >= channels ? pixels[target + index - channels] : (byte)0;
                var up = previous[index];
                var upLeft = index >= channels ? previous[index - channels] : (byte)0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Filtre PNG inconnu : {filter}."),
                };
                pixels[target + index] = (byte)(value + predictor);
            }

            source += stride;
            Buffer.BlockCopy(pixels, target, previous, 0, stride);
        }

        return pixels;
    }

    private static int Paeth(byte left, byte up, byte upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        if (toLeft <= toUp && toLeft <= toUpLeft)
        {
            return left;
        }

        return toUp <= toUpLeft ? up : upLeft;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
