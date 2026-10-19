// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie World.

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

    /// <summary>Monde 2D : description, entités et réserves au tick demandé.</summary>
    private async Task RefreshWorldAsync(string runId)
    {
        int? requested = int.TryParse(
                WorldTickInput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) && tick >= 0
            ? tick
            // Sinon le curseur de relecture : la vue 2D montre le tick affiché,
            // pas seulement le dernier publié (positions visibles à chaque tick).
            : ViewTick;
        var world = await _source.ReadWorldAsync(runId, requested, CancellationToken.None);
        WorldDescription = world.Description;
        WorldAgents = world.Agents;
        WorldResources = world.Resources;
        WorldText = world.Tick < 0
            ? "aucune observation d'entités pour ce run"
            : $"{world.Agents.Count} entité(s), {world.Resources.Count} réserve(s) au tick {world.Tick}" +
                (world.Description is { } description
                    ? $" · monde {description.Width}×{description.Height}"
                    : " · description de monde indisponible");
        RebuildAgentRows(world.Agents);
        RebuildWorldResourceRows(world.Resources);
        StatusText = $"ECHOS · {runId} · relevé à " +
            DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        HasData = world.Tick >= 0;
    }


    /// <summary>
    /// Met en forme les réserves du tick (rendu seul). Une réserve sans position
    /// publiée reste comptée et listée : elle n'est simplement pas projetée sur
    /// la carte, faute d'un point où la placer sans l'inventer.
    /// </summary>
    private void RebuildWorldResourceRows(IReadOnlyList<EchosTickResource> resources)
    {
        WorldResourceRows.Clear();
        foreach (var resource in resources)
        {
            WorldResourceRows.Add(new TickResourceRow(
                string.IsNullOrWhiteSpace(resource.Type) ? "—" : resource.Type,
                FormatNumber(resource.Quantity),
                FormatNumber(resource.Capacity),
                resource.Located));
        }

        var located = resources.Count(resource => resource.Located);
        WorldResourcesText = resources.Count == 0
            ? "aucune réserve publiée au tick affiché"
            : $"{resources.Count} réserve(s) publiée(s) · {located} localisée(s)" +
                (resources.Count > located
                    ? " · les autres sont agrégées par type par le contrat SYNE (pas de position)"
                    : string.Empty);
    }
}
