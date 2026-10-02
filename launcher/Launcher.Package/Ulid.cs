using System.Security.Cryptography;

namespace Launcher.Package;

/// <summary>
/// Générateur d'ULID : identifiant de 26 caractères Crockford Base32, trié par temps,
/// déterministe dans son format. Utilisé pour « packageId » (PACKAGE_FORMAT.md §4).
/// </summary>
public static class Ulid
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Produit un nouvel ULID (temps courant + 80 bits aléatoires).</summary>
    public static string NewUlid()
    {
        Span<byte> bytes = stackalloc byte[16];
        var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(milliseconds >> 40);
        bytes[1] = (byte)(milliseconds >> 32);
        bytes[2] = (byte)(milliseconds >> 24);
        bytes[3] = (byte)(milliseconds >> 16);
        bytes[4] = (byte)(milliseconds >> 8);
        bytes[5] = (byte)milliseconds;
        RandomNumberGenerator.Fill(bytes[6..]);
        return Encode(bytes);
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        // Encodage Crockford Base32 des 128 bits en 26 caractères de 5 bits,
        // du groupe le plus significatif au moins significatif.
        Span<char> chars = stackalloc char[26];
        for (var group = 0; group < 26; group++)
        {
            var index = 0;
            for (var bit = 0; bit < 5; bit++)
            {
                var position = 127 - (5 * group) - bit;
                if (position < 0)
                {
                    continue;
                }

                var bitValue = (bytes[position >> 3] >> (position & 7)) & 1;
                index = (index << 1) | bitValue;
            }

            chars[group] = Alphabet[index];
        }

        return new string(chars);
    }
}
