namespace Setup.Core.Distribution;

/// <summary>
/// Composant déclaré par le manifeste de distribution.
/// </summary>
public sealed record ComponentDescriptor(
    string Id,
    string Version,
    bool Mandatory,
    string NameFr,
    string NameEn,
    string DescriptionFr,
    string DescriptionEn,
    string License,
    IReadOnlyList<string> Requires,
    IReadOnlyList<FileEntry> Files,
    AcquisitionDescriptor Acquisition);