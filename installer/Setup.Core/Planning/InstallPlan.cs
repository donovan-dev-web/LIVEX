namespace Setup.Core.Planning;

/// <summary>
/// Plan d'installation calculé et validé avant écriture.
/// </summary>
public sealed record InstallPlan(
    InstallOperation Operation,
    IReadOnlyList<PlanAction> Actions,
    IReadOnlyList<string> RemovedComponents,
    long BytesToWrite,
    long BytesToDownload,
    IReadOnlyList<string> Warnings,
    bool RequiresConfirmation);