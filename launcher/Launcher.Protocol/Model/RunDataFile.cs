namespace Launcher.Protocol.Model;

/// <summary>
/// Contenu d'une entrée de données de run, sans engagement sur la taille.
///
/// <para><b>Pourquoi ce type</b> : les fichiers de run étaient chargés en <c>byte[]</c>
/// (<c>File.ReadAllBytes</c>), plafonnés à <c>int.MaxValue</c> o (2 147 483 647).
/// Un <c>stream.jsonl</c> de campagne dépasse cette borne dès ~2500 ticks (mesuré :
/// 2 337 158 145 o) et l'écriture du paquet échouait avec « The file is too long ».
/// Ici la source fichier reste sur disque et n'est lue qu'en flux, à l'écriture
/// comme au hachage ; les octets en mémoire ne servent qu'au contenu fabriqué sur
/// place (métadonnées, analyses ECHOS, contenu de test).</para>
/// </summary>
public sealed class RunDataFile
{
    private readonly string? _sourcePath;
    private readonly byte[]? _content;

    private RunDataFile(string? sourcePath, byte[]? content)
    {
        _sourcePath = sourcePath;
        _content = content;
    }

    /// <summary>Source fichier : lu en flux à l'usage, jamais chargé en mémoire.</summary>
    public static RunDataFile FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new RunDataFile(path, null);
    }

    /// <summary>Octets déjà en mémoire (contenu court fabriqué ou collecté ailleurs).</summary>
    public static RunDataFile FromBytes(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new RunDataFile(null, content);
    }

    /// <summary>Taille courante du contenu, sans le charger en mémoire.</summary>
    public long Length => _content is not null ? _content.LongLength : new FileInfo(_sourcePath!).Length;

    /// <summary>Ouvre le contenu en lecture séquentielle (flux jetable).</summary>
    public Stream OpenRead() => _content is not null
        ? new MemoryStream(_content, writable: false)
        : File.OpenRead(_sourcePath!);

    /// <summary>Pratisme d'écriture : un octet déjà en mémoire devient une source mémoire.</summary>
    public static implicit operator RunDataFile(byte[] content) => FromBytes(content);
}
