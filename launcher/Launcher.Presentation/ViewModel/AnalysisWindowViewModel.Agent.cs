// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Agent.

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
    /// Pose l'entité observée pendant un relevé sans demander de relevé : ce relevé
    /// lit la fiche juste après la reconstruction de la liste.
    /// </summary>
    private void SelectAgentSilently(AgentOption? value)
    {
        if (!SetProperty(ref _selectedAgent, value, nameof(SelectedAgent)))
        {
            return;
        }

        OnPropertyChanged(nameof(AgentHeaderText));
    }

    /// <summary>En-tête de la fiche : entité, tick d'observation, groupe, état.</summary>
    public string AgentHeaderText
    {
        get => _agentHeaderText;
        private set => SetProperty(ref _agentHeaderText, value);
    }

    /// <summary>Croyances publiées pour l'entité sélectionnée.</summary>
    public ObservableCollection<BeliefRow> AgentBeliefs { get; }

    /// <summary>Relations de confiance déclarées par l'entité sélectionnée.</summary>
    public ObservableCollection<TrustRelationRow> AgentTrust { get; }

    /// <summary>Dernières décisions publiées pour l'entité sélectionnée.</summary>
    public ObservableCollection<DecisionRow> AgentDecisions { get; }

    /// <summary>Compte-rendu factuel du dernier relevé : source, run, état.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Détail du run : ticks, dernier tick, lacunes, pas de lecture.</summary>
    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    /// <summary>Disclaimer méthodologique publié par ECHOS, affiché tel quel.</summary>
    public string DisclaimerText
    {
        get => _disclaimerText;
        private set => SetProperty(ref _disclaimerText, value);
    }

    /// <summary>État du graphe de communautés : tick de l'observation et nombre de groupes.</summary>
    public string NetworkText
    {
        get => _networkText;
        private set => SetProperty(ref _networkText, value);
    }

    /// <summary>
    /// Groupes **natifs déclarés par SYNE** publiés tels qu'observés
    /// (`/api/runs/{id}/native-groups`) : chaque champ est transporté, jamais
    /// recalculé ni arrondi localement. Distincts des <see cref="Groups"/> (communautés
    /// inférées) : identités différentes, jamais fusionnées.
    /// </summary>
    public IReadOnlyList<NativeGroupRow> NativeGroups
    {
        get => _nativeGroups;
        private set => SetProperty(ref _nativeGroups, value);
    }

    /// <summary>Compte-rendu des groupes natifs : tick d'observation et nombre publié.</summary>
    public string NativeGroupsText
    {
        get => _nativeGroupsText;
        private set => SetProperty(ref _nativeGroupsText, value);
    }

    /// <summary>Vrai si un relevé a produit des données à afficher.</summary>
    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    /// <summary>Vrai pendant un relevé : les suivants sont ignorés, jamais empilés.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Vrai pendant l'ouverture du sélecteur de run : l'horloge de direct diffère le
    /// relevé pour que la liste ne se reconstruisse pas sous le doigt de l'utilisateur
    /// (reconstruire la liste referme la liste déroulante).
    /// </summary>
    public bool IsRunSelectorOpen { get; set; }

    /// <summary>
    /// Relevé complet : liste des runs, séries du run sélectionné, réseau, phénomènes.
    /// Un échec est **affiché**, jamais comblé (pas d'approximation locale).
    /// </summary>
    public async Task RefreshAsync()
    {
        lock (_gate)
        {
            if (IsBusy)
            {
                // Un seul relevé à la fois : l'horloge de direct est sans effet pendant
                // un relevé en cours (jamais empilé). Les demandes explicites de
                // l'utilisateur passent par RequestRefresh, qui les rejoue à la fin.
                return;
            }

            IsBusy = true;
        }

        try
        {
            var runs = await _source.ReadRunsAsync(CancellationToken.None);
            SyncRuns(runs);
            await EnsureCatalogAsync();

            if (SelectedRun is null)
            {
                ApplyEmpty("aucun run enregistré dans la base analytique");
                return;
            }

            var runId = SelectedRun.RunId;

            // Les séries sont relues à chaque relevé : le curseur de relecture et le
            // compte-rendu de ticks restent disponibles sur tous les sous-écrans.
            var series = await _source.ReadSeriesAsync(runId, Every, CancellationToken.None);
            ApplySeries(series);

            switch (SelectedSectionId)
            {
                case SectionViability:
                    await RefreshViabilityAsync(runId);
                    break;
                case SectionStatistics:
                    await RefreshStatisticsAsync(runId);
                    break;
                case SectionCompare:
                    await RefreshCompareAsync();
                    break;
                case SectionRelations:
                    await RefreshRelationsAsync(runId);
                    break;
                case SectionWorld:
                    await RefreshWorldAsync(runId);
                    break;
                case SectionAgents:
                    await RefreshWorldAsync(runId);
                    await RefreshAgentAsync(runId);
                    break;
                case SectionCausality:
                    // La liste des entités vient de l'observation de monde : elle
                    // est lue avant la chaîne, comme pour la fiche d'entité.
                    await RefreshWorldAsync(runId);
                    await RefreshCausalityAsync(runId);
                    break;
                default:
                    await RefreshOverviewAsync(runId);
                    break;
            }
        }
        catch (Exception exception)
        {
            StatusText = $"ECHOS indisponible — {exception.Message}";
            HasData = false;
        }
        finally
        {
            bool restart;
            lock (_gate)
            {
                IsBusy = false;
                restart = _pendingRefresh;
                _pendingRefresh = false;
            }

            if (restart)
            {
                _ = RefreshAsync();
            }
        }
    }


    /// <summary>Fiche d'entité : croyances, relations et dernières décisions publiées.</summary>
    private async Task RefreshAgentAsync(string runId)
    {
        if (SelectedAgent is not { } agent)
        {
            AgentHeaderText = "aucune entité sélectionnée";
            AgentBeliefs.Clear();
            AgentTrust.Clear();
            AgentDecisions.Clear();
            return;
        }

        try
        {
            var profile = await _source.ReadAgentProfileAsync(runId, agent.Id, CancellationToken.None);
            AgentBeliefs.Clear();
            foreach (var belief in profile.Beliefs)
            {
                AgentBeliefs.Add(new BeliefRow(
                    belief.Subject, belief.Predicate, belief.Value, FormatNumber(belief.Confidence)));
            }

            AgentTrust.Clear();
            foreach (var relation in profile.Trust)
            {
                AgentTrust.Add(new TrustRelationRow(relation.PeerId, FormatNumber(relation.Trust)));
            }

            var decisions = await _source.ReadDecisionsAsync(runId, CancellationToken.None);
            AgentDecisions.Clear();
            foreach (var decision in decisions
                         .Where(candidate => string.Equals(candidate.AgentId, agent.Id, StringComparison.Ordinal))
                         .TakeLast(12)
                         .Reverse())
            {
                AgentDecisions.Add(BuildDecisionRow(decision));
            }

            AgentHeaderText = $"Entité {agent.Id}" +
                (profile.Tick >= 0 ? $" · contexte lu au tick {profile.Tick}" : string.Empty) +
                DescribeContextAge(runId, profile.Tick) +
                (string.IsNullOrWhiteSpace(agent.Group) ? string.Empty : $" · communauté {agent.Group}") +
                $" · énergie {FormatNumber(agent.Energy)}" +
                (string.IsNullOrWhiteSpace(agent.Action) ? string.Empty : $" · {agent.Action}");
        }
        catch (Exception exception)
        {
            // Une fiche indisponible n'invalide pas le reste du relevé : l'absence est affichée.
            AgentHeaderText = $"ECHOS indisponible — {exception.Message}";
            AgentBeliefs.Clear();
            AgentTrust.Clear();
            AgentDecisions.Clear();
        }
    }


    /// <summary>Reconstruit la liste des fiches depuis l'observation de monde.</summary>
    private void RebuildAgentRows(IReadOnlyList<EchosAgentSnapshot> agents)
    {
        var previous = SelectedAgent?.Id;
        Agents.Clear();
        foreach (var agent in agents)
        {
            Agents.Add(new AgentOption(agent.Id, agent.X, agent.Y, agent.Energy, agent.Action, agent.Group));
        }

        // Sélection silencieuse (voir SelectAgentSilently) : la liste est reconstruite
        // à chaque relevé, une sélection qui relancerait un relevé en chaînerait.
        SelectAgentSilently(
            Agents.FirstOrDefault(option => option.Id == previous) ?? Agents.FirstOrDefault());
    }


    /// <summary>Met en forme les relations publiées pour le tableau (rendu seul).</summary>
    private void RebuildTrustEdgeRows(IReadOnlyList<EchosTrustEdge> edges)
    {
        TrustEdgeRows.Clear();
        foreach (var edge in edges.Take(EdgeRowLimit))
        {
            TrustEdgeRows.Add(new TrustEdgeRow(
                edge.Source,
                edge.Target,
                edge.Weight is { } weight ? FormatNumber(weight) : "—"));
        }
    }

    private void SyncRuns(IReadOnlyList<EchosRunSummary> runs)
    {
        var known = Runs.Select(option => option.RunId).ToHashSet(StringComparer.Ordinal);
        if (runs.Count == Runs.Count && runs.All(run => known.Contains(run.RunId)))
        {
            foreach (var option in Runs)
            {
                option.Update(runs.First(run => run.RunId == option.RunId));
            }

            SyncCompareRuns();
            return;
        }

        var previous = SelectedRun?.RunId;
        Runs.Clear();
        foreach (var run in runs.OrderByDescending(run => run.LastTick))
        {
            Runs.Add(new EchosRunOption(run));
        }

        RebuildCompareRuns();
        // Sélection silencieuse : le relevé en cours (celui qui reconstruit la liste)
        // lit cette sélection juste après — redemander un relevé enchaînerait les tours.
        SelectRunSilently(Runs.FirstOrDefault(option => option.RunId == previous) ?? Runs.FirstOrDefault());
    }
}
