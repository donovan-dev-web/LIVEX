// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Execution;

/// <summary>
/// Résultat d'une exécution. <see cref="ExitCodeHint"/> permet au moteur de
/// préciser le code de sortie attendu hors interface (par exemple
/// <c>6</c> lorsqu'un actif facultatif est indisponible) ; nul signifie que
/// l'appelant applique la règle générale.
/// </summary>
public sealed record ExecutionResult(
    bool Success,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Residuals,
    string LogPath,
    bool Simulated = false,
    IReadOnlyList<string>? AffectedPaths = null,
    int? ExitCodeHint = null);
