// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Statistics.

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Launcher.Application;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Launcher.Presentation.ViewModel;

public sealed partial class AnalysisWindowViewModel : ObservableObject
{

    /// <summary>Statistiques exactes : métadonnées, métriques et provenance publiées.</summary>
    private async Task RefreshStatisticsAsync(string runId)
    {
        var detail = await _source.ReadRunDetailAsync(runId, CancellationToken.None);
        Statistics.Clear();
        foreach (var (engine, metrics) in detail.Metrics)
        {
            foreach (var (metric, value) in metrics)
            {
                var measured = !detail.Measured.TryGetValue(engine, out var flags)
                    || !flags.TryGetValue(metric, out var flag)
                    || flag;
                Statistics.Add(new StatRow(
                    engine,
                    metric,
                    FormatNumber(value),
                    measured,
                    LookupStatus(engine, metric),
                    LookupHelp(engine, metric)));
            }
        }

        StatsHeader = detail.RunId + $" · graine {detail.Seed} · {detail.TicksCount} tick(s)" +
            $" · {detail.Outcome}" +
            (detail.ExtinctionTick is { } extinction ? $" · extinction au tick {extinction}" : string.Empty);
        DisclaimerText = detail.Disclaimer;
        StatusText = $"ECHOS · {detail.RunId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = Statistics.Count > 0;
    }
}
