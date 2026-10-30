using Setup.Core.Lifecycle;

namespace Setup.Core.Execution;

public sealed record DetectionResult(
    bool IsInstalled,
    string? InstallDir,
    InstallationRecord? Record,
    bool IsValid,
    IReadOnlyList<string> Issues);