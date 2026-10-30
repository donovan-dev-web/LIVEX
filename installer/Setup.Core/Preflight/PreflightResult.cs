// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Preflight;

/// <summary>Statuts d'un contrôle de préflight. Chaînes stables, traduites par l'interface.</summary>
public static class PreflightStatus
{
    public const string Pass = "Pass";
    public const string Fail = "Fail";
    public const string Warn = "Warn";
    public const string Info = "Info";
    public const string Simulated = "Simulated";
}

public sealed record PreflightCheck(
    string Name,
    string Status,
    string? Message)
{
    public bool Passed => Status == PreflightStatus.Pass;
    public bool Failed => Status == PreflightStatus.Fail;
    public bool IsSimulated => Status == PreflightStatus.Simulated;
}

public sealed record PreflightResult(
    bool Success,
    IReadOnlyList<PreflightCheck> Checks,
    IReadOnlyList<string> Errors);
