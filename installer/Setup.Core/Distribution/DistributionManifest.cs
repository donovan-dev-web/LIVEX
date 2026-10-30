namespace Setup.Core.Distribution;

/// <summary>
/// Manifeste de distribution. Le champ <c>signature</c> est optionnel en V1
/// (ADR-007 §3 bis) : la confiance repose sur les SHA-256 et les attestations.
/// </summary>
public sealed record DistributionManifest(
    int Schema,
    string Product,
    string Version,
    string Channel,
    string Profile,
    string Platform,
    DateTimeOffset ReleasedAt,
    IReadOnlyList<ComponentDescriptor> Components,
    IReadOnlyList<FileEntry> Files,
    SignatureDescriptor? Signature);