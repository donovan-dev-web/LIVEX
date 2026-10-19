// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Relations.

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

    /// <summary>Confiance et communautés : graphe publié, communautés observées.</summary>
    private async Task RefreshRelationsAsync(string runId)
    {
        // Le curseur de relecture pilote le graphe : au direct, le tick courant du
        // flux ; en relecture, le tick affiché — l'évolution des relations sociales
        // se visualise ainsi tick par tick, sans attente la fin du run.
        var graph = await _source.ReadTrustGraphAsync(runId, ViewTick, CancellationToken.None);
        TrustNodes = graph.Nodes;
        TrustEdges = graph.Edges;
        RelationsText = graph.Tick >= 0
            ? $"{graph.Nodes.Count} entité(s), {graph.Edges.Count} relation(s) publiée(s) au tick {graph.Tick}"
            : "aucune observation d'entités pour ce run";

        var network = await _source.ReadNetworkAsync(runId, CancellationToken.None);
        Groups = network.Groups;
        NetworkText = network.Tick >= 0
            ? $"{network.Groups.Count} communauté(s) observée(s) au tick {network.Tick}"
            : "aucune observation de groupes pour ce run";

        // Groupes natifs **déclarés par SYNE** : lus à part (route dédiée), au tick
        // du curseur comme le graphe de confiance, et présentés **tels quels** —
        // aucun recalcul, aucune moyenne, aucun arrondi local. ``tick: -1`` signale
        // l'absence d'observation (base antérieure) ; un ``[]`` publié est une
        // observation vraie « aucun groupe à ce tick ».
        // Groupes natifs **déclarés par SYNE** : lus à part (route dédiée), au tick
        // du curseur comme le graphe de confiance, et présentés **tels quels** —
        // aucun recalcul, aucune moyenne, aucun arrondi local. `tick: -1` signale
        // l'absence d'observation (base antérieure) ; un `[]` publié est une
        // observation vraie « aucun groupe à ce tick ». Une version d'ECHOS
        // antérieure à la route ne doit pas faire disparaître le graphe de
        // confiance : l'absence est annoncée dans le panneau, jamais comblée par
        // les communautés inférées.
        try
        {
            var nativeGroups = await _source.ReadNativeGroupsAsync(runId, ViewTick, CancellationToken.None);
            NativeGroups = [.. nativeGroups.Groups.Select(BuildNativeGroupRow)];
            NativeGroupsText = nativeGroups.Tick >= 0
                ? $"{nativeGroups.Groups.Count} groupe(s) natif(s) SYNE publié(s) au tick {nativeGroups.Tick}"
                : "aucune observation de groupes natifs pour ce run";
        }
        catch (Exception exception)
        {
            NativeGroups = [];
            NativeGroupsText = $"groupes natifs indisponibles — {exception.Message}";
        }

        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = graph.Nodes.Count > 0;
    }
}
