using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Prism.Viewer;

/// <summary>
/// Capture d'image du viewer : encodage PNG autonome (aucune dépendance graphique) des pixels
/// lus dans le tampon de la fenêtre. Les octets reçus sont ceux d'OpenGL, ligne du bas en
/// premier ; le PNG est écrit dans l'ordre d'affichage habituel (ligne du haut en premier).
/// </summary>
public static class ViewerScreenshot
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>Encode une image RGBA (4 octets par pixel, origine en bas) en PNG 8 bits.</summary>
    public static byte[] EncodePng(int width, int height, ReadOnlySpan<byte> rgbaBottomUp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var stride = checked(width * 4);
        if (rgbaBottomUp.Length != checked(stride * height))
        {
            throw new ArgumentException(
                "Pixel buffer length must equal width * height * 4.",
                nameof(rgbaBottomUp));
        }

        // Chaque ligne PNG porte un octet de filtre (0 = aucun) puis les pixels de gauche à droite.
        var raw = new byte[checked(height * (stride + 1))];
        for (var row = 0; row < height; row++)
        {
            var target = row * (stride + 1);
            raw[target] = 0;
            rgbaBottomUp.Slice((height - 1 - row) * stride, stride)
                .CopyTo(raw.AsSpan(target + 1, stride));
        }

        var compressed = Compress(raw);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;   // profondeur par canal
        header[9] = 6;   // vrai couleur + alpha
        header[10] = 0;  // compression deflate
        header[11] = 0;  // filtre standard
        header[12] = 0;  // pas d'entrelacement

        using var png = new MemoryStream(Signature.Length + 13 + compressed.Length + 64);
        png.Write(Signature);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Écrit la capture et renvoie le chemin absolu utilisé.</summary>
    public static string Save(string path, int width, int height, ReadOnlySpan<byte> rgbaBottomUp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(fullPath, EncodePng(width, height, rgbaBottomUp));
        return fullPath;
    }

    private static byte[] Compress(ReadOnlySpan<byte> raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return output.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = 0xFFFFFFFFu;
        foreach (var value in typeBytes)
        {
            crc = Append(crc, value);
        }

        foreach (var value in data)
        {
            crc = Append(crc, value);
        }

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc ^ 0xFFFFFFFFu);
        stream.Write(checksum);
    }

    private static uint Append(uint crc, byte value) =>
        CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0
                    ? 0xEDB88320u ^ (value >> 1)
                    : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
