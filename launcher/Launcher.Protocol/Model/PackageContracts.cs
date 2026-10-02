using System.Text.Json.Serialization;

namespace Launcher.Protocol.Model;

/// <summary>État d'un paquet .livexp (PACKAGE_FORMAT.md §4.1).</summary>
public static class PackageStates
{
    /// <summary>Campagne en cours. Écriture attendue.</summary>
    public const string Live = "live";
    /// <summary>Campagne terminée. Paquet immuable.</summary>
    public const string Sealed = "sealed";
    /// <summary>Écriture interrompue. Reprise possible, contenu incomplet.</summary>
    public const string Recoverable = "recoverable";
}

/// <summary>
/// Manifeste du paquet .livexp (PACKAGE_FORMAT.md §4). Point de commit du paquet :
/// écrit en dernier, son absence à jour signifie que le paquet est récupérable plutôt que valide.
/// </summary>
public sealed class PackageManifest : ISchemaVersioned
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = PackageConstants.SchemaVersion;

    /// <summary>Identifiant unique, trié par temps (ULID).</summary>
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>Nature du paquet : « campaign » en V0.1.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "campaign";

    /// <summary>live, sealed ou recoverable.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = PackageStates.Live;

    /// <summary>Date de création, ISO 8601 en UTC.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Date de scellement, nulle tant que le paquet est vivant.</summary>
    [JsonPropertyName("sealedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SealedAt { get; set; }

    [JsonPropertyName("generator")]
    public JsonGenerator Generator { get; set; } = new();

    [JsonPropertyName("experiment")]
    public JsonExperimentRef Experiment { get; set; } = new();

    [JsonPropertyName("counts")]
    public JsonCounts Counts { get; set; } = new();

    /// <summary>Versions des composants engagés.</summary>
    [JsonPropertyName("components")]
    public List<JsonComponentRef> Components { get; set; } = new();

    /// <summary>Empreinte du contenu utile au contrôle d'intégrité.</summary>
    [JsonPropertyName("integrity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonIntegrity? Integrity { get; set; }
}

public sealed class JsonGenerator
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "livex-launcher";

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public sealed class JsonExperimentRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Profile { get; set; }
}

public sealed class JsonCounts
{
    [JsonPropertyName("runs")]
    public int Runs { get; set; }

    [JsonPropertyName("runsDone")]
    public int RunsDone { get; set; }

    [JsonPropertyName("runsFailed")]
    public int RunsFailed { get; set; }
}

public sealed class JsonComponentRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public sealed class JsonIntegrity
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "sha256";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Entrée de runs/index.json : état de chaque run, source de reprise (PACKAGE_FORMAT.md §3).</summary>
public sealed class RunIndexEntry
{
    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    /// <summary>Planifié, EnCours, Termine, Echoue, Annule (EXPERIMENTS.md §6).</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Planifie";

    /// <summary>Graine du run, dérivée ou explicite.</summary>
    [JsonPropertyName("seed")]
    public long Seed { get; set; }

    [JsonPropertyName("attempt")]
    public int Attempt { get; set; } = 1;

    [JsonPropertyName("startedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartedAt { get; set; }

    [JsonPropertyName("endedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndedAt { get; set; }

    /// <summary>Cause principale en cas d'échec ou d'annulation.</summary>
    [JsonPropertyName("cause")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cause { get; set; }

    /// <summary>Code de sortie du moteur (INTEGRATION_CONTRACT.md §4), si terminé.</summary>
    [JsonPropertyName("exitCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; set; }
}

/// <summary>Contenu de runs/index.json.</summary>
public sealed class RunIndex : ISchemaVersioned
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = PackageConstants.SchemaVersion;

    [JsonPropertyName("runs")]
    public List<RunIndexEntry> Runs { get; set; } = new();
}

/// <summary>Entrée de integrity.json : tailles et empreintes SHA-256 (PACKAGE_FORMAT.md §3).</summary>
public sealed class IntegrityEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>Contenu de runs/RUN-nnnn/integrity.json.</summary>
public sealed class RunIntegrity : ISchemaVersioned
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = PackageConstants.SchemaVersion;

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "sha256";

    [JsonPropertyName("files")]
    public List<IntegrityEntry> Files { get; set; } = new();
}

/// <summary>
/// Contenu de provenance.json (PACKAGE_FORMAT.md §3) : qui, quand, quelles versions,
/// quelle plateforme. Porte la traçabilité de la production, sans donnée scientifique.
/// </summary>
public sealed class PackageProvenance
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = PackageConstants.SchemaVersion;

    /// <summary>Launcher ayant produit le paquet.</summary>
    [JsonPropertyName("generator")]
    public JsonGenerator Generator { get; set; } = new();

    /// <summary>Date de création du paquet, UTC.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Versions des composants engagés dans la campagne.</summary>
    [JsonPropertyName("components")]
    public List<JsonComponentRef> Components { get; set; } = new();

    /// <summary>Plateforme de production (DATA_FLOW.md §4.2 : os, arch, runtime).</summary>
    [JsonPropertyName("platform")]
    public JsonPlatform Platform { get; set; } = new();
}

/// <summary>Description de la plateforme de production.</summary>
public sealed class JsonPlatform
{
    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    [JsonPropertyName("arch")]
    public string Arch { get; set; } = string.Empty;

    [JsonPropertyName("dotnet")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DotNet { get; set; }
}
