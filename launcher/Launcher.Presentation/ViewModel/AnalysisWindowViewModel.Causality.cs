// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Causality.

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
    /// Chaîne causale de l'entité sélectionnée, reconstruite **par ECHOS**. Aucun
    /// nœud n'est déduit ici : les couches, leurs libellés, les boucles et la
    /// troncature sont ceux de la réponse.
    /// </summary>
    private async Task RefreshCausalityAsync(string runId)
    {
        if (SelectedAgent is not { } agent)
        {
            CausalHeader = "aucune entité sélectionnée";
            CausalText = string.Empty;
            CausalNote = string.Empty;
            CausalNodes.Clear();
            CausalCycles.Clear();
            return;
        }

        var requestedTick = int.TryParse(
                CausalTickInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) && tick >= 0
            ? tick
            : (int?)null;

        try
        {
            var chain = await _source.ReadCausalChainAsync(
                runId, agent.Id, requestedTick, CausalDepth, CancellationToken.None);

            CausalHeader = $"Entité {chain.AgentId} · trace de décision du tick {chain.Tick}" +
                $" · profondeur {chain.DepthServed} / {chain.DepthRequested} couche(s)";

            CausalNodes.Clear();
            var step = 1;
            foreach (var node in chain.Nodes)
            {
                CausalNodes.Add(new CausalNodeRow(
                    step++, node.Layer, node.Tick, node.Label, node.Detail));
            }

            CausalCycles.Clear();
            foreach (var cycle in chain.Cycles)
            {
                CausalCycles.Add(new CausalCycleRow(
                    cycle.Layer,
                    cycle.Label,
                    cycle.Ticks.Count == 0
                        ? "boucle signalée sans tick de récurrence publié"
                        : $"récurrence aux ticks {string.Join(", ", cycle.Ticks)}"));
            }

            CausalText = $"{chain.Nodes.Count} couche(s) servie(s)" +
                (chain.Truncated
                    ? " · tronquée : des couches existent au-delà de la profondeur demandée"
                    : " · chaîne complète pour la profondeur demandée") +
                (chain.Cycle ? " · boucle signalée" : " · aucune boucle signalée");

            CausalNote = "Reconstruction hors ligne par ECHOS (traces de décision, événements, " +
                "contexte d'agents) : Action → Intention → Objectif → Besoin → Croyance → " +
                "Mémoire → Perception. Une couche absente est affichée « imperception » ; " +
                "la chaîne explique la trace publiée, elle ne prouve pas une causalité établie.";
        }
        catch (Exception exception)
        {
            // Absence de trace (404) ou ECHOS indisponible : l'absence est affichée.
            CausalHeader = $"ECHOS indisponible — {exception.Message}";
            CausalText = string.Empty;
            CausalNote = string.Empty;
            CausalNodes.Clear();
            CausalCycles.Clear();
        }
    }
}
