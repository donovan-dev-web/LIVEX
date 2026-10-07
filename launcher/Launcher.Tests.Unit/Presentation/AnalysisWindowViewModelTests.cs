using Launcher.Application;
using Launcher.Presentation.ViewModel;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Xunit;

namespace Launcher.Tests.Unit.Presentation;

/// <summary>
/// Fenêtre d'analyse native (ADR-007, ADR-003). Les règles qui comptent : tout vient de
/// l'API d'ECHOS, une métrique non mesurée ne se fait jamais passer pour une observation,
/// un tick sans observation reste un trou visible, l'indisponibilité est affichée au lieu
/// d'être approximée, et la relecture ne fait que tronquer des séries déjà publiées.
/// </summary>
public sealed class AnalysisWindowViewModelTests
{
    [Fact]
    public async Task Un_premier_releve_affiche_ce_que_ECHOS_publie()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasData);
        Assert.Equal("EXP-A-RUN-0001", viewModel.SelectedRun?.RunId);
        Assert.NotEmpty(viewModel.Metrics);
        Assert.NotEmpty(viewModel.TrendSeries);
        Assert.Equal(2, viewModel.Groups.Count);
        Assert.Equal(2, viewModel.Phenomena.Count);
        Assert.Equal("Consommation de ressources observée sur ce run.", viewModel.DisclaimerText);
        Assert.Equal("EXP-A-RUN-0001", source.LastSeriesRunId);
        Assert.Equal(1, source.LastEvery);
    }

    [Fact]
    public async Task Le_premier_ecran_est_le_profil_de_viabilite()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source);

        Assert.Equal(AnalysisWindowViewModel.SectionViability, viewModel.SelectedSectionId);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasData);
        Assert.Contains("extinction au tick 10", viewModel.ViabilityHeader, StringComparison.Ordinal);
        var population = Assert.Single(viewModel.ViabilityRows, row => row.Label == "Population initiale");
        Assert.Equal("8", population.Value);
        var gaps = Assert.Single(viewModel.ViabilityRows, row => row.Label == "Ticks manquants");
        Assert.Equal("3", gaps.Value);
        // Le rapport post-run est distingué des données temps réel (P3).
        var slope = Assert.Single(viewModel.ViabilityRows, row => row.Label == "Pente d'énergie par tick");
        Assert.Equal("-0.5", slope.Value);
        Assert.Contains("post-run", slope.Note, StringComparison.Ordinal);
        Assert.Contains("disponible", viewModel.ViabilityCalibration, StringComparison.Ordinal);
        Assert.Single(viewModel.PopulationSeries);
        Assert.Equal(3, Assert.IsType<LineSeries<ObservablePoint?>>(viewModel.PopulationSeries[0]).Values!.Count());
        var chronology = Assert.Single(viewModel.ExtinctionRows);
        Assert.Equal("death", chronology.Type);
        Assert.Equal(9, chronology.Tick);
    }

    [Fact]
    public async Task Une_metrique_non_mesuree_ne_peut_passer_pour_une_observation()
    {
        var viewModel = new AnalysisWindowViewModel(FakeTelemetry.WithSampleData())
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        var unmeasured = Assert.Single(viewModel.Metrics, metric => metric.Metric == "Modularity");
        Assert.Equal("non mesurée", unmeasured.LatestLabel);

        // Repli neutre : la série existe, mais aucun point n'est rendu comme observation.
        var line = Assert.IsType<LineSeries<ObservablePoint?>>(Assert.Single(
            viewModel.TrendSeries,
            series => series is LineSeries<ObservablePoint?> candidate && candidate.Name == "Modularity"));
        Assert.All<ObservablePoint?>(line.Values!, point => Assert.Null(point));
    }

    [Fact]
    public async Task Les_trous_de_donnees_rendent_des_coordonnees_de_tick_reelles()
    {
        var source = FakeTelemetry.WithSampleData();
        // Ticks non contigus (lacunes) + masque de mesure par tick publié par ECHOS.
        source.Series = new EchosSeries
        {
            RunId = "EXP-A-RUN-0001",
            Ticks = [1, 2, 5, 6],
            Values = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["ClusteringCoefficient"] = [0.1, 0.2, null, 0.4],
                },
            },
            MeasuredByTick = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<bool?>>
                {
                    ["ClusteringCoefficient"] = [true, false, null, true],
                },
            },
            Measured = new Dictionary<string, IReadOnlyDictionary<string, bool>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, bool>
                {
                    ["ClusteringCoefficient"] = true,
                },
            },
            Latest = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, double>
                {
                    ["ClusteringCoefficient"] = 0.4,
                },
            },
            LatestTick = 6,
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        var line = Assert.IsType<LineSeries<ObservablePoint?>>(Assert.Single(viewModel.TrendSeries));
        var points = line.Values!.ToArray();
        Assert.Equal(4, points.Length);
        // Tick réel en abscisse (5 et 6, pas les index 2 et 3) : aucune coordonnée décalée.
        Assert.Equal(1d, points[0]!.X);
        Assert.Equal(6d, points[3]!.X);
        // Mesuré → point rendu ; repli neutre, donnée absente → trou, jamais une valeur.
        Assert.NotNull(points[0]);
        Assert.Null(points[1]);
        Assert.Null(points[2]);
        Assert.NotNull(points[3]);
    }

    [Fact]
    public async Task La_selection_de_metrique_reconstruit_la_courbe()
    {
        var viewModel = new AnalysisWindowViewModel(FakeTelemetry.WithSampleData())
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };
        await viewModel.RefreshAsync();
        var before = viewModel.TrendSeries.Length;

        var option = viewModel.Metrics.First(metric => metric.Metric == "GroupFormationRate");
        option.IsSelected = false;
        Assert.Equal(before - 1, viewModel.TrendSeries.Length);

        option.IsSelected = true;
        Assert.Equal(before, viewModel.TrendSeries.Length);
    }

    [Fact]
    public async Task Une_metrique_deselectionnee_disparait_de_la_courbe()
    {
        var viewModel = new AnalysisWindowViewModel(FakeTelemetry.WithSampleData())
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };
        await viewModel.RefreshAsync();

        var selected = viewModel.Metrics.First(metric => metric.IsSelected);
        selected.IsSelected = false;

        Assert.DoesNotContain(viewModel.TrendSeries,
            series => series is LineSeries<ObservablePoint?> line && line.Name == selected.Label);
    }

    [Fact]
    public async Task La_relecture_tronque_la_courbe_sans_nouvelle_lecture()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };
        await viewModel.RefreshAsync();
        var observedReads = source.SeriesCalls;

        viewModel.ReplayPosition = 5;

        // Aucune relecture côté serveur : seule la fenêtre d'affichage bouge.
        Assert.Equal(observedReads, source.SeriesCalls);
        Assert.False(viewModel.IsAtLive);
        Assert.False(viewModel.IsLive); // « Relire ce run » se distingue de « Suivre le direct »
        Assert.Contains("relecture — tick 5 / 10", viewModel.ReplayText, StringComparison.Ordinal);
        var line = Assert.IsType<LineSeries<ObservablePoint?>>(Assert.Single(
            viewModel.TrendSeries,
            series => series is LineSeries<ObservablePoint?> candidate && candidate.Name == "ClusteringCoefficient"));
        Assert.Equal(5, line.Values!.Last()!.X);

        viewModel.ReplayCommand.Execute("play");
        Assert.True(viewModel.IsPlaying);
        viewModel.AdvanceReplay();
        Assert.Equal(6d, viewModel.ReplayPosition);

        viewModel.ReplayCommand.Execute("live");
        Assert.True(viewModel.IsAtLive);
        Assert.True(viewModel.IsLive);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(10d, viewModel.ReplayPosition);
        Assert.Equal(observedReads, source.SeriesCalls);
    }

    [Fact]
    public async Task La_relecture_synchronise_la_vue_2D_sur_le_tick_du_curseur()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionWorld,
        };
        await viewModel.RefreshAsync();

        viewModel.ReplayPosition = 7;

        Assert.Equal("7", viewModel.WorldTickInput);
        Assert.Equal(7, source.LastWorldTick);
        Assert.Equal("EXP-A-RUN-0001", source.LastWorldRunId);
    }

    [Fact]
    public async Task Un_ECHOS_indoignable_est_affiche_jamais_approxime()
    {
        var source = FakeTelemetry.WithSampleData();
        source.Unreachable = true;
        var viewModel = new AnalysisWindowViewModel(source);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasData);
        Assert.Contains("ECHOS indisponible", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Empty(viewModel.TrendSeries);
        Assert.Empty(viewModel.Metrics);
        Assert.Empty(viewModel.ViabilityRows);
    }

    [Fact]
    public async Task Le_pas_de_lecture_est_transmis_au_port_sans_interpretation()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            Every = 5,
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        Assert.Equal(5, source.LastEvery);
        Assert.Contains(5, viewModel.EveryOptions);
    }

    [Fact]
    public async Task Un_releve_en_cours_ne_se_superpose_jamais_au_suivant()
    {
        var source = FakeTelemetry.WithSampleData();
        source.Hold = true;
        var viewModel = new AnalysisWindowViewModel(source);

        var first = viewModel.RefreshAsync();
        var second = viewModel.RefreshAsync();
        Assert.True(viewModel.IsBusy);

        source.Release();
        await first;
        await second;

        // Le relevé concurrent a été ignoré : une seule série de lectures, jamais deux.
        Assert.Equal(1, source.SeriesCalls);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// Sélection pendant un relevé (changement de run, d'écran, d'entité…) : la
    /// demande n'est pas perdue, elle est rejouée à la fin du relevé en cours.
    /// C'est ce qui rend la sélection d'un run fiable dans la fenêtre d'analyse.
    /// </summary>
    [Fact]
    public async Task Une_selection_effectuee_pendant_un_releve_est_rejouee_apres_lui()
    {
        var source = FakeTelemetry.WithSampleData();
        source.Hold = true;
        var viewModel = new AnalysisWindowViewModel(source);

        var first = viewModel.RefreshAsync();
        // Changement de sous-écran pendant le relevé en cours : non ignoré.
        viewModel.SelectedSectionId = AnalysisWindowViewModel.SectionStatistics;
        Assert.True(viewModel.IsBusy);

        source.Release();
        await first;

        // Le relevé rejoué part tout seul (un seul relevé à la fois) :
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (source.SeriesCalls < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(2, source.SeriesCalls);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(AnalysisWindowViewModel.SectionStatistics, viewModel.SelectedSectionId);
    }

    [Fact]
    public async Task Le_sous_ecran_statistiques_affiche_les_valeurs_publiees_telles_quelles()
    {
        var source = FakeTelemetry.WithSampleData();
        source.RunDetail = new EchosRunDetail
        {
            RunId = "EXP-A-RUN-0001",
            Seed = 42,
            TicksCount = 10,
            FirstTick = 1,
            LastTick = 10,
            Outcome = "extinction",
            ExtinctionTick = 10,
            Metrics = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, double>
                {
                    ["ClusteringCoefficient"] = 0.875,
                    ["Modularity"] = 0.4,
                },
            },
            Measured = new Dictionary<string, IReadOnlyDictionary<string, bool>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, bool>
                {
                    ["ClusteringCoefficient"] = true,
                    ["Modularity"] = false,
                },
            },
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionStatistics,
        };

        await viewModel.RefreshAsync();

        Assert.Contains("EXP-A-RUN-0001", viewModel.StatsHeader, StringComparison.Ordinal);
        Assert.Contains("graine 42", viewModel.StatsHeader, StringComparison.Ordinal);
        Assert.Contains("extinction au tick 10", viewModel.StatsHeader, StringComparison.Ordinal);
        Assert.Contains("2.0.0", viewModel.CatalogText, StringComparison.Ordinal);

        var measured = Assert.Single(viewModel.Statistics, row => row.Metric == "ClusteringCoefficient");
        Assert.Equal("0.875", measured.Display); // valeur publiée, mise en forme sans arrondi local
        Assert.Equal("mesurée", measured.Provenance);
        Assert.Equal("mesuré", measured.Status);
        // La fiche du catalogue « que regarder ? » accompagne la valeur.
        Assert.Contains("Calcul", measured.Help, StringComparison.Ordinal);
        Assert.Equal(measured.Help, measured.Tip);

        var unmeasured = Assert.Single(viewModel.Statistics, row => row.Metric == "Modularity");
        Assert.Equal("repli neutre", unmeasured.Provenance);
        Assert.Equal("—", unmeasured.Status);
        Assert.Null(unmeasured.Tip); // aucune fiche : absence explicite, pas de texte inventé
    }

    [Fact]
    public async Task Le_sous_ecran_comparaison_affiche_le_contexte_et_la_dispersion_publies()
    {
        var source = FakeTelemetry.WithSampleData();
        source.Runs =
        [
            new EchosRunSummary("EXP-A-RUN-0001", 42, 10, 1, 10, "extinct", 10),
            new EchosRunSummary("EXP-A-RUN-0002", 43, 10, 1, 10, "surviving", null),
        ];
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionCompare,
        };

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasData);
        Assert.Equal(2, viewModel.CompareRuns.Count);
        Assert.All(viewModel.CompareRuns, option => Assert.True(option.IsSelected));
        Assert.Contains("2 run(s) comparé(s)", viewModel.CompareHeader, StringComparison.Ordinal);

        var context = Assert.Single(viewModel.CompareRunRows, row => row.RunId == "EXP-A-RUN-0002");
        Assert.Equal("0.1.0", context.Version);
        Assert.Equal("43", context.Seed);
        Assert.Equal("surviving", context.Outcome);
        Assert.Equal("1 → 10 (10)", context.Ticks);
        Assert.Equal("—", context.Extinction);

        var metric = Assert.Single(viewModel.CompareMetricRows);
        Assert.Equal("ClusteringCoefficient", metric.Metric);
        Assert.Equal(2, metric.RunsObserved);
        Assert.Equal("0.7", metric.Min);
        Assert.Equal("0.9", metric.Max);
        Assert.Equal("0.8", metric.Mean);
        Assert.Equal("0.2", metric.Spread);
        Assert.Contains("EXP-A-RUN-0001 = 0.9", metric.Values, StringComparison.Ordinal);
        Assert.Equal("Dispersion publiée par ECHOS.", viewModel.CompareNote);
    }

    [Fact]
    public async Task Une_comparaison_impossible_est_explicite_sans_valeur_inventee()
    {
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionCompare,
        };

        await viewModel.RefreshAsync();

        // Un seul run sélectionné : ECHOS n'est pas interrogé, l'absence est affichée.
        Assert.False(viewModel.HasData);
        Assert.Empty(viewModel.CompareRunRows);
        Assert.Empty(viewModel.CompareMetricRows);
        Assert.Equal(0, source.ExperimentSummaryCalls);
        Assert.Contains("sélectionnez au moins deux runs", viewModel.CompareHeader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Les_phenomenes_affichent_leurs_signaux_publies()
    {
        var viewModel = new AnalysisWindowViewModel(FakeTelemetry.WithSampleData())
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        var row = Assert.Single(viewModel.Phenomena, item => item.Identifier == "ResourceDepletion");
        Assert.Equal("Épuisement de réserve observé", row.DisplayName);
        Assert.Contains("seuil 0.8", row.Triggers, StringComparison.Ordinal);
        Assert.Contains("ResourceFillRatio", row.Triggers, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_sous_ecran_confiance_affiche_les_relations_publiees()
    {
        var source = FakeTelemetry.WithSampleData();
        source.TrustGraph = new EchosTrustGraph
        {
            RunId = "EXP-A-RUN-0001",
            Tick = 10,
            Nodes =
            [
                new EchosTrustNode("a", 1, 2, 40, 10, 10, "SeekFood", "G-1"),
                new EchosTrustNode("b", 3, 4, 30, 20, 15, null, "G-1"),
            ],
            Edges = [new EchosTrustEdge("a", "b", 0.75)],
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionRelations,
        };

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.TrustNodes.Count);
        var edge = Assert.Single(viewModel.TrustEdgeRows);
        Assert.Equal("a", edge.Source);
        Assert.Equal("b", edge.Target);
        Assert.Equal("0.75", edge.Display);
        Assert.Contains("1 relation", viewModel.RelationsText, StringComparison.Ordinal);
        Assert.Equal(2, viewModel.Groups.Count); // communautés affichées sur le même sous-écran
        Assert.True(viewModel.HasData);
    }

    [Fact]
    public async Task Le_graphe_de_confiance_suivre_le_curseur_de_relecture()
    {
        // L'évolution des relations sociales se visualise tick par tick : le
        // graphe doit lire le tick du curseur, pas toujours le dernier publié.
        var source = FakeTelemetry.WithSampleData();
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionRelations,
        };
        await viewModel.RefreshAsync();
        Assert.Equal(10, source.LastTrustTick); // au direct : dernier tick publié

        viewModel.ReplayCommand.Execute("prev");
        for (var attempt = 0; attempt < 200 && viewModel.IsBusy; attempt++)
        {
            await Task.Delay(10);
        }

        await viewModel.RefreshAsync();
        Assert.Equal(9, source.LastTrustTick); // relu au tick du curseur
    }

    [Fact]
    public async Task La_vue_2D_lit_le_monde_au_tick_demande()
    {
        var source = FakeTelemetry.WithSampleData();
        source.World = new EchosWorld
        {
            RunId = "EXP-A-RUN-0001",
            Tick = 7,
            WorldTick = 0,
            Description = new EchosWorldDescription(
                500, 500, 10,
                [new EchosWorldCell(0, 0, "grass", true)],
                [new EchosObstacle("o-1", 12, 24, 4)],
                [new EchosWorldResource("r-1", "food", 3, 4, 10)],
                [new EchosRegion("spawn", 0, 0, 4, 4)]),
            Agents = [new EchosAgentSnapshot("a", 5, 6, 42, 10, 12, "SeekFood", "G-1")],
            Resources = [new EchosTickResource("food", 5, 6, 8, 10)],
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionWorld,
            WorldTickInput = "7",
        };

        await viewModel.RefreshAsync();

        Assert.Equal(7, source.LastWorldTick);
        Assert.Equal("EXP-A-RUN-0001", source.LastWorldRunId);
        Assert.NotNull(viewModel.WorldDescription);
        Assert.Equal(500, viewModel.WorldDescription!.Width);
        Assert.Single(viewModel.WorldAgents);
        Assert.Single(viewModel.WorldResources);
        Assert.Contains("au tick 7", viewModel.WorldText, StringComparison.Ordinal);
        Assert.Single(viewModel.Agents);
    }

    [Fact]
    public async Task La_fiche_entite_affiche_croyances_relations_et_decisions()
    {
        var source = FakeTelemetry.WithSampleData();
        source.World = new EchosWorld
        {
            RunId = "EXP-A-RUN-0001",
            Tick = 10,
            Agents =
            [
                new EchosAgentSnapshot("a", 1, 2, 40, 10, 10, "SeekFood", "G-1"),
                new EchosAgentSnapshot("b", 3, 4, 30, 20, 15, null, "G-1"),
            ],
        };
        source.AgentProfile = new EchosAgentProfile
        {
            AgentId = "a",
            Tick = 10,
            Beliefs = [new EchosBelief("food", "near", "true", 0.9)],
            Trust = [new EchosTrustRelation("b", 0.8)],
        };
        source.Decisions =
        [
            new EchosDecision(8, "a", "SeekFood", 0.5, "hunger=30"),
            new EchosDecision(9, "b", "Rest", 0.1, ""),
            new EchosDecision(10, "a", "Drink", 0.9, "thirst=40"),
        ];
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionAgents,
        };

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.Agents.Count);
        Assert.NotNull(viewModel.SelectedAgent);
        Assert.Contains("Entité", viewModel.AgentHeaderText, StringComparison.Ordinal);
        var belief = Assert.Single(viewModel.AgentBeliefs);
        Assert.Equal("0.9", belief.Confidence);
        var relation = Assert.Single(viewModel.AgentTrust);
        Assert.Equal("b", relation.PeerId);
        // Seules les décisions de l'entité sélectionnée, les plus récentes d'abord.
        Assert.Equal(2, viewModel.AgentDecisions.Count);
        Assert.Equal("Drink", viewModel.AgentDecisions[0].Action);
        Assert.Equal("SeekFood", viewModel.AgentDecisions[1].Action);
    }

    [Fact]
    public async Task Un_ECHOS_indoignable_sur_la_fiche_entite_affiche_l_absence_sans_vider_l_ecran()
    {
        var source = FakeTelemetry.WithSampleData();
        source.World = new EchosWorld
        {
            RunId = "EXP-A-RUN-0001",
            Tick = 10,
            Agents = [new EchosAgentSnapshot("a", 1, 2, 40, 10, 10, null, null)],
        };
        source.ProfileFails = true;
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionAgents,
        };

        await viewModel.RefreshAsync();

        Assert.Contains("ECHOS indisponible", viewModel.AgentHeaderText, StringComparison.Ordinal);
        Assert.Empty(viewModel.AgentBeliefs);
        Assert.Single(viewModel.Agents); // l'observation de monde reste affichée
        Assert.True(viewModel.HasData);
    }

    [Fact]
    public async Task Un_run_sans_donnee_laisse_une_vue_vide_et_explicite()
    {
        var source = new FakeTelemetry();
        var viewModel = new AnalysisWindowViewModel(source);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasData);
        Assert.Equal("aucun run enregistré dans la base analytique", viewModel.StatusText);
        Assert.Empty(viewModel.Runs);
        Assert.Empty(viewModel.Phenomena);
        Assert.Empty(viewModel.ViabilityRows);
        Assert.Empty(viewModel.CompareMetricRows);
    }

    [Fact]
    public async Task L_etat_du_run_est_construit_a_partir_des_faits_publies()
    {
        // P3 : « actif / terminé / interrompu / incomplet » — ECHOS ne publie pas
        // l'état d'activité du flux, la fenêtre affiche les faits et la lacune.
        var source = FakeTelemetry.WithSampleData(); // outcome extinct, tick 10, 3 lacunes
        var viewModel = new AnalysisWindowViewModel(source);

        await viewModel.RefreshAsync();

        var ended = Assert.Single(viewModel.ViabilityRows, row => row.Label == "État du run");
        Assert.Equal(
            "terminé — extinction observée au tick 10 · incomplet : 3 tick(s) manquant(s)",
            ended.Value);
        Assert.Contains("ne distingue pas", ended.Note, StringComparison.Ordinal);

        source.Viability = new EchosViability
        {
            RunId = "EXP-A-RUN-0001",
            Outcome = "surviving",
            LastTick = 7,
        };
        await viewModel.RefreshAsync();

        var ongoing = Assert.Single(viewModel.ViabilityRows, row => row.Label == "État du run");
        // Survivant sans extinction : « possiblement en cours », jamais « terminé »
        // ni « interrompu » — ces deux états ne sont pas publiés par ECHOS.
        Assert.Equal(
            "aucune extinction observée jusqu'au tick 7 — run possiblement en cours",
            ongoing.Value);
    }

    [Fact]
    public async Task La_fiche_entite_publie_l_age_du_contexte_echantillonne()
    {
        // P3 : âge exact du contexte lu — cadence publiée + écart au dernier tick.
        var source = FakeTelemetry.WithSampleData();
        source.Runs =
        [
            new EchosRunSummary(
                "EXP-A-RUN-0001", 42, 10, 1, 10, "extinct", 10,
                "sampled_details", "0.1.0", 20),
        ];
        source.World = new EchosWorld
        {
            RunId = "EXP-A-RUN-0001",
            Tick = 10,
            Agents = [new EchosAgentSnapshot("a", 1, 2, 40, 10, 10, null, null)],
        };
        source.AgentProfile = new EchosAgentProfile { AgentId = "a", Tick = 5 };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionAgents,
        };

        await viewModel.RefreshAsync();

        Assert.Contains("contexte lu au tick 5", viewModel.AgentHeaderText, StringComparison.Ordinal);
        Assert.Contains("cadence 20 tick(s)", viewModel.AgentHeaderText, StringComparison.Ordinal);
        Assert.Contains("(âge ≤ 19)", viewModel.AgentHeaderText, StringComparison.Ordinal);
        Assert.Contains("5 tick(s) derrière le dernier (10)", viewModel.AgentHeaderText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task L_outil_de_distribution_publie_n_intervalles_et_exclut_les_non_mesures()
    {
        var source = FakeTelemetry.WithSampleData();
        source.Series = new EchosSeries
        {
            RunId = "EXP-A-RUN-0001",
            Ticks = [1, 2, 3, 4, 5, 6],
            Values = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["ClusteringCoefficient"] = [0.1, 0.2, null, 0.4, 0.5, 0.6],
                },
            },
            MeasuredByTick = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<bool?>>
                {
                    ["ClusteringCoefficient"] = [true, true, null, false, true, true],
                },
            },
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionStatistics,
        };

        await viewModel.RefreshAsync();

        // Le trou et le repli neutre sont exclus et comptés, jamais lus comme 0.
        Assert.Contains("4 observation(s) publiée(s)", viewModel.DistributionText, StringComparison.Ordinal);
        Assert.Contains("2 valeur(s) exclue(s)", viewModel.DistributionText, StringComparison.Ordinal);
        var column = Assert.IsType<ColumnSeries<double>>(Assert.Single(viewModel.DistributionSeries));
        Assert.Equal(4, column.Values!.Sum());
        // Intervalles explicites et bornés (12 classes maximum).
        Assert.Equal(3, viewModel.DistributionXAxes[0].Labels!.Count);
        Assert.Contains("intervalle(s)", viewModel.DistributionText, StringComparison.Ordinal);
        Assert.Contains("aucune extrapolation", viewModel.DistributionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Les_marqueurs_devenement_sont_publiesbornes_et_desactivables()
    {
        var source = FakeTelemetry.WithSampleData(); // ticks 1..10
        source.Events = new EchosEventFeed
        {
            Total = 3,
            Limit = 500,
            Types = [new EchosEventKind("agent_died", 3)],
            Events =
            [
                new EchosEventRow(3, "agent_died", "a", null, "energy=0"),
                new EchosEventRow(7, "agent_died", "b", null, "energy=0"),
                new EchosEventRow(99, "agent_died", "c", null, "energy=0"), // hors fenêtre
            ],
        };
        var viewModel = new AnalysisWindowViewModel(source)
        {
            SelectedSectionId = AnalysisWindowViewModel.SectionOverview,
        };

        await viewModel.RefreshAsync();

        Assert.Contains("2 marqueur(s) affiché(s) sur 3 événement(s) publié(s)", viewModel.EventsText, StringComparison.Ordinal);
        var markers = viewModel.TrendSeries
            .OfType<LineSeries<ObservablePoint?>>()
            .Where(series => series.Name?.StartsWith("t=", StringComparison.Ordinal) == true)
            .ToList();
        Assert.Equal(2, markers.Count);
        Assert.All(markers, marker => Assert.Equal(2, marker.Values!.Count()));

        // Borne de rendu : jamais plus de 16 marqueurs, même sur un journal dense.
        var denseTicks = Enumerable.Range(1, 40).ToArray();
        source.Series = new EchosSeries
        {
            RunId = "EXP-A-RUN-0001",
            Ticks = denseTicks,
            Values = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>
            {
                ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                {
                    ["ClusteringCoefficient"] = denseTicks.Select(tick => (double?)(tick / 40d)).ToArray(),
                },
            },
        };
        source.Events = new EchosEventFeed
        {
            Total = 40,
            Limit = 500,
            Events = denseTicks
                .Select(tick => new EchosEventRow(tick, "agent_died", "a", null, "energy=0"))
                .ToList(),
        };
        await viewModel.RefreshAsync();

        var bounded = viewModel.TrendSeries
            .OfType<LineSeries<ObservablePoint?>>()
            .Count(series => series.Name?.StartsWith("t=", StringComparison.Ordinal) == true);
        Assert.Equal(16, bounded);

        // La bascule retire les marqueurs sans toucher aux séries publiées.
        viewModel.ShowEventMarkers = false;
        Assert.DoesNotContain(
            viewModel.TrendSeries,
            series => series is LineSeries<ObservablePoint?> line
                && line.Name?.StartsWith("t=", StringComparison.Ordinal) == true);
        Assert.NotEmpty(viewModel.TrendSeries);
    }

    /// <summary>
    /// Source mémoire : reproduit les formes de l'API REST d'ECHOS (`/api/runs`,
    /// `/api/runs/{id}/metrics`, `/api/metrics/catalog`, `/api/runs/{id}/viability`,
    /// `/api/experiments/summary`, `/api/groups`, `/api/emergent-phenomena`).
    /// </summary>
    private sealed class FakeTelemetry : IEchosTelemetrySource
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _hold;

        public IReadOnlyList<EchosRunSummary> Runs { get; set; } = [];

        public EchosSeries Series { get; set; } = new();

        public EchosMetricCatalog Catalog { get; set; } = new();

        public EchosViability Viability { get; set; } = new();

        public EchosExperimentSummary ExperimentSummary { get; set; } = new();

        public EchosNetwork Network { get; set; } = new();

        public EchosPhenomena Phenomena { get; set; } = new();

        public EchosWorld World { get; set; } = new();

        public EchosTrustGraph TrustGraph { get; set; } = new();

        public EchosRunDetail RunDetail { get; set; } = new();

        public EchosAgentProfile AgentProfile { get; set; } = new();

        public IReadOnlyList<EchosDecision> Decisions { get; set; } = [];

        public EchosEventFeed Events { get; set; } = new();

        public bool ProfileFails { get; set; }

        public string? LastWorldRunId { get; private set; }

        public int? LastWorldTick { get; private set; }

        public bool Unreachable { get; set; }

        public bool Hold
        {
            get => _hold;
            set
            {
                _hold = value;
                if (!value)
                {
                    _gate.TrySetResult();
                }
            }
        }

        public int SeriesCalls { get; private set; }

        public int ExperimentSummaryCalls { get; private set; }

        public string? LastSeriesRunId { get; private set; }

        public int LastEvery { get; private set; }

        public void Release() => Hold = false;

        public static FakeTelemetry WithSampleData()
        {
            var ticks = Enumerable.Range(1, 10).ToArray();
            double?[] clustering = ticks.Select(tick => (double?)(tick / 10d)).ToArray();
            double?[] formation = ticks.Select(tick => (double?)(100d - tick)).ToArray();
            return new FakeTelemetry
            {
                Runs = [new EchosRunSummary("EXP-A-RUN-0001", 42, 10, 1, 10, "extinction", null)],
                Catalog = new EchosMetricCatalog
                {
                    Version = "2.0.0",
                    Metrics =
                    [
                        new EchosMetricDoc(
                            "ClusteringCoefficient",
                            "SocialComplexityMetrics",
                            "Coefficient de clustering",
                            "ratio",
                            "réseau",
                            "Part des liens observés qui referment un triplet.",
                            "triangles observés / triplets possibles",
                            "entités vivantes liées",
                            "fenêtre publiée",
                            "plus haut = plus dense",
                            "measured",
                            [],
                            "fenêtre absente = repli neutre",
                            "courbe",
                            []),
                    ],
                },
                Viability = new EchosViability
                {
                    RunId = "EXP-A-RUN-0001",
                    Outcome = "extinct",
                    ExtinctionTick = 10,
                    PopulationInitial = 8,
                    PopulationFinal = 0,
                    PopulationMinimum = 0,
                    PopulationSeries = [new EchosPoint(1, 8), new EchosPoint(5, 4), new EchosPoint(10, 0)],
                    EnergySeries = [new EchosPoint(1, 60), new EchosPoint(10, 20)],
                    MissingTickCount = 3,
                    ConservationLevel = "high_fidelity",
                    CalibrationAvailable = true,
                    EnergySlopePerTick = -0.5,
                    HungryDecisions = 12,
                    ExtinctionChronology = [new EchosEventRow(9, "death", "a", "collapse", "hunger=95")],
                },
                ExperimentSummary = new EchosExperimentSummary
                {
                    Runs =
                    [
                        new EchosCompareRun("EXP-A-RUN-0001", "0.1.0", "42", "extinct", 10, 1, 10, 10, "high_fidelity"),
                        new EchosCompareRun("EXP-A-RUN-0002", "0.1.0", "43", "surviving", 10, 1, 10, null, "base"),
                    ],
                    Metrics =
                    [
                        new EchosCompareMetric(
                            "SocialComplexityMetrics",
                            "ClusteringCoefficient",
                            new Dictionary<string, double?>
                            {
                                ["EXP-A-RUN-0001"] = 0.9,
                                ["EXP-A-RUN-0002"] = 0.7,
                            },
                            2,
                            0.7,
                            0.9,
                            0.8,
                            0.2),
                    ],
                    Note = "Dispersion publiée par ECHOS.",
                },
                Series = new EchosSeries
                {
                    RunId = "EXP-A-RUN-0001",
                    Ticks = ticks,
                    Values = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>
                    {
                        ["SocialComplexityMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                        {
                            ["ClusteringCoefficient"] = clustering,
                            ["Modularity"] = clustering.Select(value => value / 2).ToArray(),
                        },
                        ["GroupDynamicsMetrics"] = new Dictionary<string, IReadOnlyList<double?>>
                        {
                            ["GroupFormationRate"] = formation,
                        },
                    },
                    Latest = new Dictionary<string, IReadOnlyDictionary<string, double>>
                    {
                        ["SocialComplexityMetrics"] = new Dictionary<string, double>
                        {
                            ["ClusteringCoefficient"] = 0.9,
                            ["Modularity"] = 0.45,
                        },
                        ["GroupDynamicsMetrics"] = new Dictionary<string, double>
                        {
                            ["GroupFormationRate"] = 91,
                        },
                    },
                    Measured = new Dictionary<string, IReadOnlyDictionary<string, bool>>
                    {
                        ["SocialComplexityMetrics"] = new Dictionary<string, bool>
                        {
                            ["ClusteringCoefficient"] = true,
                            ["Modularity"] = false,
                        },
                        ["GroupDynamicsMetrics"] = new Dictionary<string, bool>
                        {
                            ["GroupFormationRate"] = true,
                        },
                    },
                    LatestTick = 10,
                },
                Network = new EchosNetwork
                {
                    RunId = "EXP-A-RUN-0001",
                    Tick = 10,
                    Groups =
                    [
                        new EchosGroup("G-1", ["a", "b"], 2),
                        new EchosGroup("G-2", ["c", "d", "e"], 3),
                    ],
                },
                Phenomena = new EchosPhenomena
                {
                    RunId = "EXP-A-RUN-0001",
                    Tick = 10,
                    Detected =
                    [
                        new EchosPhenomenon(
                            "ResourceDepletion",
                            3,
                            4,
                            9,
                            "Épuisement de réserve observé",
                            "La réserve publiée baisse jusqu'à zéro sur la fenêtre observée.",
                            [new EchosSignal("ResourceFillRatio", 0.1, 0.8)]),
                        new EchosPhenomenon("CommunityFormation", 1, 7, 7),
                    ],
                    Disclaimer = "Consommation de ressources observée sur ce run.",
                },
            };
        }

        public Task<IReadOnlyList<EchosRunSummary>> ReadRunsAsync(CancellationToken cancellationToken)
        {
            if (Unreachable)
            {
                throw new InvalidOperationException("ECHOS injoignable sur http://127.0.0.1:5000/");
            }

            return Task.FromResult(Runs);
        }

        public async Task<EchosSeries> ReadSeriesAsync(string? runId, int every, CancellationToken cancellationToken)
        {
            SeriesCalls++;
            if (Hold)
            {
                await _gate.Task;
            }

            if (Unreachable)
            {
                throw new InvalidOperationException("ECHOS injoignable sur http://127.0.0.1:5000/");
            }

            LastSeriesRunId = runId;
            LastEvery = every;
            return Series;
        }

        public Task<EchosMetricCatalog> ReadCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Catalog);

        public Task<EchosViability> ReadViabilityAsync(
            string runId, int every, CancellationToken cancellationToken) => Task.FromResult(Viability);

        public Task<EchosExperimentSummary> ReadExperimentSummaryAsync(
            IReadOnlyList<string> runIds, CancellationToken cancellationToken)
        {
            ExperimentSummaryCalls++;
            return Task.FromResult(ExperimentSummary);
        }

        public Task<EchosNetwork> ReadNetworkAsync(string? runId, CancellationToken cancellationToken) =>
            Task.FromResult(Network);

        public Task<EchosPhenomena> ReadPhenomenaAsync(string? runId, CancellationToken cancellationToken) =>
            Task.FromResult(Phenomena);

        public Task<EchosWorld> ReadWorldAsync(string? runId, int? tick, CancellationToken cancellationToken)
        {
            if (Unreachable)
            {
                throw new InvalidOperationException("ECHOS injoignable sur http://127.0.0.1:5000/");
            }

            LastWorldRunId = runId;
            LastWorldTick = tick;
            return Task.FromResult(World);
        }

        public int? LastTrustTick { get; private set; }

        public Task<EchosTrustGraph> ReadTrustGraphAsync(
            string? runId, int? tick, CancellationToken cancellationToken)
        {
            LastTrustTick = tick;
            return Task.FromResult(TrustGraph);
        }

        public Task<EchosRunDetail> ReadRunDetailAsync(string runId, CancellationToken cancellationToken) =>
            Task.FromResult(RunDetail);

        public Task<EchosAgentProfile> ReadAgentProfileAsync(
            string runId, string agentId, CancellationToken cancellationToken)
        {
            if (ProfileFails)
            {
                throw new InvalidOperationException("ECHOS injoignable sur http://127.0.0.1:5000/");
            }

            return Task.FromResult(AgentProfile);
        }

        public Task<IReadOnlyList<EchosDecision>> ReadDecisionsAsync(
            string runId, CancellationToken cancellationToken) => Task.FromResult(Decisions);

        public Task<EchosEventFeed> ReadEventsAsync(
            string runId, CancellationToken cancellationToken) =>
            Task.FromResult(Events with { RunId = runId });
    }
}
