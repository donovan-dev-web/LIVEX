namespace Launcher.Protocol;

/// <summary>
/// Refus explicite levé quand une version de schéma dépasse la version connue du Launcher.
/// Politique : refuser, jamais interpréter approximativement (PACKAGE_FORMAT.md §7, ARCHITECTURE.md §9).
/// </summary>
public sealed class SchemaNotSupportedException : Exception
{
    public SchemaNotSupportedException(string kind, int supported, int received)
        : base($"{kind} : version de schéma non prise en charge (requise ≤ {supported}, lue {received})")
    {
        SupportedSchema = supported;
        ReceivedSchema = received;
    }

    /// <summary>Version maximale connue du Launcher.</summary>
    public int SupportedSchema { get; }

    /// <summary>Version lue dans le document.</summary>
    public int ReceivedSchema { get; }
}

/// <summary>Contenu altéré, entrée fautive nommée (PACKAGE_FORMAT.md §10).</summary>
public sealed class CorruptedPackageException : Exception
{
    public CorruptedPackageException(string entryName, string reason)
        : base($"Paquet corrompu — entrée « {entryName} » : {reason}")
    {
        EntryName = entryName;
    }

    /// <summary>Nom de l'entrée fautive.</summary>
    public string EntryName { get; }
}

/// <summary>Tentative d'écriture dans un paquet scellé. Cause : « paquet scellé » (PACKAGE_FORMAT.md §5.3).</summary>
public sealed class SealedPackageException : Exception
{
    public SealedPackageException()
        : base("paquet scellé : toute écriture est refusée")
    {
    }
}

/// <summary>Entrée refusée pour cause de chemin non conforme : traversal, absolu ou lecteur (PACKAGE_FORMAT.md §8).</summary>
public sealed class UnsafeEntryPathException : Exception
{
    public UnsafeEntryPathException(string entryName)
        : base($"Chemin d'entrée non conforme, refusé : « {entryName} »")
    {
        EntryName = entryName;
    }

    /// <summary>Nom de l'entrée refusée.</summary>
    public string EntryName { get; }
}

/// <summary>Manifeste de composant invalide ou incompatible (COMPONENTS.md §12, PACKAGING.md §4).</summary>
public sealed class InvalidManifestException : Exception
{
    public InvalidManifestException(string location, string reason)
        : base($"Manifeste invalide ({location}) : {reason}")
    {
        Location = location;
    }

    /// <summary>Emplacement du manifeste fautif.</summary>
    public string Location { get; }
}
