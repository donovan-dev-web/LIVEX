// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Overview.

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
    /// Vue d'ensemble : communautés et phénomènes, tels que publiés.
    /// Les séries ont déjà été lues et appliquées par le relevé courant.
    /// </summary>
    private async Task RefreshOverviewAsync(string runId)
    {
        var network = await _source.ReadNetworkAsync(runId, CancellationToken.None);
        Groups = network.Groups;
        NetworkText = network.Tick >= 0
            ? $"{network.Groups.Count} communauté(s) observée(s) au tick {network.Tick}"
            : "aucune observation de groupes pour ce run";

        var phenomena = await _source.ReadPhenomenaAsync(runId, CancellationToken.None);
        Phenomena.Clear();
        foreach (var phenomenon in phenomena.Detected)
        {
            Phenomena.Add(new PhenomenonRow(
                phenomenon.Identifier,
                phenomenon.DisplayName,
                phenomenon.Description,
                phenomenon.Occurrences,
                phenomenon.FirstTick,
                phenomenon.LastTick,
                DescribeTriggers(phenomenon)));
        }

        DisclaimerText = phenomena.Disclaimer;
        await RefreshEventsAsync(runId);
        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = true;
    }
}
