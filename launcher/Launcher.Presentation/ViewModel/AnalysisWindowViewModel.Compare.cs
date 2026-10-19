// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Compare.

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

    /// <summary>
    /// Comparaison multi-run : contexte de contrôle et dispersion publiés par ECHOS.
    /// Aucun agrégat n'est calculé dans cette fenêtre.
    /// </summary>
    private async Task RefreshCompareAsync()
    {
        var runIds = CompareRuns
            .Where(option => option.IsSelected)
            .Select(option => option.RunId)
            .Take(CompareRunLimit)
            .ToList();

        CompareRunRows.Clear();
        CompareMetricRows.Clear();
        if (runIds.Count < 2)
        {
            CompareHeader = "sélectionnez au moins deux runs dans la liste";
            CompareNote = string.Empty;
            StatusText = "comparaison indisponible — moins de deux runs sélectionnés";
            HasData = false;
            return;
        }

        var summary = await _source.ReadExperimentSummaryAsync(runIds, CancellationToken.None);
        CompareHeader = $"{summary.Runs.Count} run(s) comparé(s) · dispersion publiée par ECHOS";
        CompareNote = summary.Note;

        foreach (var run in summary.Runs)
        {
            CompareRunRows.Add(new CompareRunRow(
                run.RunId,
                string.IsNullOrWhiteSpace(run.Version) ? "—" : run.Version,
                string.IsNullOrWhiteSpace(run.Seed) ? "—" : run.Seed,
                run.Outcome,
                $"{run.FirstTick} → {run.LastTick} ({run.TicksCount})",
                FormatNumber(run.ExtinctionTick),
                string.IsNullOrWhiteSpace(run.ConservationLevel) ? "—" : run.ConservationLevel));
        }

        foreach (var metric in summary.Metrics)
        {
            var values = string.Join(" · ", metric.Values.Select(pair =>
                $"{pair.Key} = {FormatNumber(pair.Value)}"));
            CompareMetricRows.Add(new CompareMetricRow(
                metric.Engine,
                metric.Metric,
                values,
                metric.RunsObserved,
                FormatNumber(metric.Min),
                FormatNumber(metric.Max),
                FormatNumber(metric.Mean),
                FormatNumber(metric.Spread)));
        }

        StatusText = $"ECHOS · comparaison de {summary.Runs.Count} run(s) · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = CompareRunRows.Count > 0;
    }


    /// <summary>Reconstruit la sélection de comparaison en conservant les coches existantes.</summary>
    private void RebuildCompareRuns()
    {
        var previous = CompareRuns
            .Where(option => option.IsSelected)
            .Select(option => option.RunId)
            .ToHashSet(StringComparer.Ordinal);
        CompareRuns.Clear();
        for (var index = 0; index < Runs.Count; index++)
        {
            var run = Runs[index];
            var option = new CompareRunOption(run.RunId, run.Label)
            {
                // Premier relevé : les trois runs les plus récents sont présélectionnés.
                IsSelected = previous.Count == 0
                    ? index < Math.Min(3, Runs.Count)
                    : previous.Contains(run.RunId),
            };
            option.PropertyChanged += (_, _) => OnCompareSelectionChanged();
            CompareRuns.Add(option);
        }
    }


    /// <summary>Met à jour les options de comparaison sans changer la coche de l'utilisateur.</summary>
    private void SyncCompareRuns()
    {
        if (CompareRuns.Count != Runs.Count
            || CompareRuns.Select(option => option.RunId)
                .Intersect(Runs.Select(run => run.RunId), StringComparer.Ordinal)
                .Count() != Runs.Count)
        {
            RebuildCompareRuns();
            return;
        }

        for (var index = 0; index < Runs.Count; index++)
        {
            CompareRuns[index].Label = Runs[index].Label;
        }
    }


    private void OnCompareSelectionChanged()
    {
        if (string.Equals(SelectedSectionId, SectionCompare, StringComparison.Ordinal))
        {
            RequestRefresh();
        }
    }

    private void ApplyEmpty(string reason)
    {
        TrendSeries = Array.Empty<ISeries>();
        _lastSeries = null;
        _ticks = [];
        _seriesRunId = null;
        _replayTick = 0;
        Metrics.Clear();
        DistributionOptions.Clear();
        SelectedDistributionOption = null;
        DistributionText = "aucune métrique sélectionnée pour l'instant";
        _runEvents = [];
        _eventsFeed = null;
        EventsText = string.Empty;
        Phenomena.Clear();
        Groups = [];
        NativeGroups = [];
        NativeGroupsText = string.Empty;
        DetailText = string.Empty;
        NetworkText = string.Empty;
        Statistics.Clear();
        StatsHeader = string.Empty;
        ViabilityRows.Clear();
        ExtinctionRows.Clear();
        ViabilityHeader = string.Empty;
        ViabilityCalibration = string.Empty;
        ViabilityNote = string.Empty;
        PopulationSeries = Array.Empty<ISeries>();
        NeedsSeries = Array.Empty<ISeries>();
        ResourceSeries = Array.Empty<ISeries>();
        CompareRunRows.Clear();
        CompareMetricRows.Clear();
        CompareHeader = string.Empty;
        CompareNote = string.Empty;
        TrustNodes = [];
        TrustEdges = [];
        RelationsText = string.Empty;
        WorldDescription = null;
        WorldAgents = [];
        WorldResources = [];
        WorldText = string.Empty;
        WorldResourceRows.Clear();
        WorldResourcesText = string.Empty;
        CausalNodes.Clear();
        CausalCycles.Clear();
        CausalHeader = string.Empty;
        CausalText = string.Empty;
        CausalNote = string.Empty;
        Agents.Clear();
        AgentBeliefs.Clear();
        AgentTrust.Clear();
        AgentDecisions.Clear();
        AgentHeaderText = string.Empty;
        StatusText = reason;
        HasData = false;
        RaiseReplayChanged();
        RaiseChartsChanged();
    }

    private void ApplySeries(EchosSeries series)
    {
        // Le direct est une décision d'affichage : si le curseur était au bord du
        // flux publié, il suit les nouveaux ticks — sinon la fenêtre reste figée
        // sur l'état précédent alors que des points sont dessinés au-delà.
        var wasLive = _ticks.Length == 0 || _replayTick >= _ticks[^1];
        _ticks = series.Ticks.ToArray();
        _lastSeries = series;

        if (!string.Equals(_seriesRunId, series.RunId, StringComparison.Ordinal))
        {
            // Changement de run : la relecture repart au direct (dernier tick publié)
            // et le journal d'événements du run précédent est abandonné — jamais
            // reporté sur les courbes d'un autre run.
            _seriesRunId = series.RunId;
            _replayTick = _ticks.Length == 0 ? 0 : _ticks[^1];
            IsPlaying = false;
            _runEvents = [];
            _eventsFeed = null;
            EventsText = string.Empty;
            RaiseReplayChanged();
        }
        else if (_ticks.Length > 0)
        {
            _replayTick = wasLive
                ? _ticks[^1]
                : Math.Clamp(_replayTick, _ticks[0], _ticks[^1]);
            RaiseReplayChanged();
        }

        // Découvertes des métriques : on conserve l'ordre stable renvoyé par ECHOS
        // et on écarte celles qu'il signale comme non mesurées (repli neutre).
        var discovered = new List<MetricOption>();
        foreach (var (engine, metrics) in series.Values)
        {
            foreach (var (metric, values) in metrics)
            {
                if (values.Count == 0)
                {
                    continue;
                }

                var measured = !series.Measured.TryGetValue(engine, out var engineMeasured)
                    || !engineMeasured.TryGetValue(metric, out var flag)
                    || flag;
                discovered.Add(new MetricOption(
                    engine, metric, measured, ResolveLatest(series, engine, metric, values, measured),
                    LookupHelp(engine, metric)));
            }
        }

        var previousSelection = Metrics.Where(option => option.IsSelected).Select(option => option.Key)
            .ToHashSet(StringComparer.Ordinal);
        Metrics.Clear();
        foreach (var option in discovered)
        {
            // Au premier relevé, on présélectionne les métriques mesurées ;
            // ensuite la sélection de l'utilisateur fait foi, même vide.
            if (previousSelection.Count == 0)
            {
                option.IsSelected = discovered.Count(item => item.Measured) <= MaxSeries
                    || discovered.Take(MaxSeries).Any(item => item.Key == option.Key);
            }
            else
            {
                option.IsSelected = previousSelection.Contains(option.Key);
            }

            option.PropertyChanged += (_, _) => OnMetricSelectionChanged();
            Metrics.Add(option);
        }

        foreach (var option in Metrics)
        {
            option.NotifyLatest(series);
        }

        RebuildDistributionOptions();
        RebuildTrend();

        DetailText =
            $"{series.RunId} · {series.Ticks.Count} point(s) affiché(s) · " +
            $"dernier tick {series.LatestTick?.ToString(CultureInfo.InvariantCulture) ?? "—"}" +
            (series.MissingTicksCount > 0
                ? $" · {series.MissingTicksCount} tick(s) manquant(s)"
                : " · aucune lacune de tick") +
            (Every > 1 ? $" · pas de lecture : {Every} tick(s)" : string.Empty);
        RaiseChartsChanged();
    }

    /// <summary>
    /// Dernière valeur publiée : la valeur d'ECHOS fait foi ; à défaut, la dernière
    /// observation non nulle de la série. Une valeur jamais observée vaut zéro et
    /// reste signalée « non mesurée ».
    /// </summary>
    private static double ResolveLatest(
        EchosSeries series, string engine, string metric, IReadOnlyList<double?> values, bool measured)
    {
        if (series.Latest.TryGetValue(engine, out var engineLatest)
            && engineLatest.TryGetValue(metric, out var published))
        {
            return published;
        }

        if (measured)
        {
            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is { } value)
                {
                    return value;
                }
            }
        }

        return 0d;
    }
}
