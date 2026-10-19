// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Viability.

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

    /// <summary>Viabilité : issue, population, besoins, ressources, complétude (P1/P3).</summary>
    private async Task RefreshViabilityAsync(string runId)
    {
        var viability = await _source.ReadViabilityAsync(runId, Every, CancellationToken.None);

        ViabilityHeader = viability.RunId +
            $" · issue publiée : {DescribeOutcome(viability.Outcome)}" +
            (viability.ExtinctionTick is { } extinction ? $" · extinction au tick {extinction}" : string.Empty);

        ViabilityRows.Clear();
        ViabilityRows.Add(new ViabilityRow("Issue publiée", viability.Outcome, "statut publié par ECHOS"));
        ViabilityRows.Add(new ViabilityRow(
            "État du run",
            DescribeRunState(viability),
            "issue + complétude publiées ; ECHOS ne distingue pas « en cours » d'« interrompu »"));
        ViabilityRows.Add(new ViabilityRow(
            "Population initiale",
            FormatNumber(viability.PopulationInitial),
            "entités vivantes au premier tick"));
        ViabilityRows.Add(new ViabilityRow(
            "Population finale",
            FormatNumber(viability.PopulationFinal),
            "entités vivantes au dernier tick observé"));
        ViabilityRows.Add(new ViabilityRow(
            "Minimum observé",
            FormatNumber(viability.PopulationMinimum),
            "plus bas niveau de population observé"));
        ViabilityRows.Add(new ViabilityRow(
            "Premier tick à population nulle",
            FormatNumber(viability.ExtinctionTick),
            "absent : aucune extinction observée"));
        ViabilityRows.Add(new ViabilityRow(
            "Ticks manquants",
            FormatNumber(viability.MissingTickCount),
            "lacunes de l'archive entre le premier et le dernier tick"));
        ViabilityRows.Add(new ViabilityRow(
            "Niveau de conservation",
            string.IsNullOrWhiteSpace(viability.ConservationLevel) ? "—" : viability.ConservationLevel,
            "métadonnée persistée par le pipeline ECHOS"));

        if (viability.CalibrationAvailable)
        {
            ViabilityCalibration = "Rapport de calibration post-run disponible (mesures ci-dessous).";
            if (viability.EnergySlopePerTick is { } slope)
            {
                ViabilityRows.Add(new ViabilityRow(
                    "Pente d'énergie par tick",
                    FormatNumber(slope),
                    "rapport post-run, fenêtre de calibration publiée"));
            }

            if (viability.HungryDecisions is { } hungry)
            {
                ViabilityRows.Add(new ViabilityRow(
                    "Décisions sous faim > 70",
                    FormatNumber(hungry),
                    "rapport post-run, comptage publié par ECHOS"));
            }
        }
        else
        {
            ViabilityCalibration = "Rapport de calibration post-run indisponible pour ce run.";
        }

        ViabilityNote = "Profil multi-dimensionnel : aucune de ces valeurs n'est agrégée en un score " +
            "dans cette fenêtre. Absence d'une ligne = donnée non publiée, jamais valeur nulle.";

        ExtinctionRows.Clear();
        foreach (var row in viability.ExtinctionChronology)
        {
            ExtinctionRows.Add(new ChronologyRow(
                row.Tick,
                row.Type,
                row.AgentId ?? "—",
                row.Action ?? "—",
                row.Cause ?? "—"));
        }

        PopulationSeries = BuildPointSeries("Population", viability.PopulationSeries, Palette[2]);
        NeedsSeries =
        [
            .. BuildPointSeries("Énergie", viability.EnergySeries, Palette[0]),
            .. BuildPointSeries("Faim", viability.HungerSeries, Palette[4]),
            .. BuildPointSeries("Soif", viability.ThirstSeries, Palette[5]),
        ];
        ResourceSeries =
        [
            .. BuildPointSeries("Nourriture", viability.FoodSeries, Palette[1]),
            .. BuildPointSeries("Eau", viability.WaterSeries, Palette[3]),
        ];

        StatusText = $"ECHOS · {viability.RunId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = ViabilityRows.Count > 0;
        RaiseChartsChanged();
    }
}
