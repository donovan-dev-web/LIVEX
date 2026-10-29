namespace Setup.Core.Distribution;

/// <summary>
/// Manifeste de distribution signé.
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
    SignatureDescriptor Signature);