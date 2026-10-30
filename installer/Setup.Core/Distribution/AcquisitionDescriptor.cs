namespace Setup.Core.Distribution;

/// <summary>
/// Description de l'acquisition d'un composant (embarqué, actif de release, indisponible).
/// </summary>
public sealed record AcquisitionDescriptor(
    string Kind,           // payload | release-asset | unavailable
    string? Asset,
    string? Url,
    long? Size,
    string? Sha256,
    string? Format,
    int? StripComponents,
    string? Reason);