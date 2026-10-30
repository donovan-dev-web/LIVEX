// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Execution;

/// <summary>
/// Progression d'une étape. <see cref="Step"/> est le nom du type d'action
/// (rétro-compatible avec l'assistant) ; <see cref="Kind"/> est la clé stable
/// et neutre (« write-file », « extract »…) recommandée pour les traductions.
/// </summary>
public sealed record ProgressReport(
    string Step,
    string? ComponentId,
    int Current,
    int Total,
    long BytesProcessed,
    long BytesTotal,
    string? TargetPath = null,
    string? Kind = null);
