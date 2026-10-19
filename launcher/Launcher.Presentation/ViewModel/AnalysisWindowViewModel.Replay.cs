// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : AnalysisWindowViewModel - partie Replay.

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

    /// <summary>Avance d'un pas de relecture ; appelée par l'horloge d'interface (1 s).</summary>
    public void AdvanceReplay()
    {
        if (!IsPlaying || _ticks.Length == 0)
        {
            return;
        }

        var index = ReplayIndex + ReplaySpeed;
        if (index >= _ticks.Length - 1)
        {
            ReplayPosition = _ticks[^1];
            IsPlaying = false;
        }
        else
        {
            ReplayPosition = _ticks[index];
        }
    }

    /// <summary>
    /// Catalogue des métriques : lu une fois, relu tant qu'il reste vide. Une
    /// indisponibilité est **affichée** (les noms techniques restent lisibles),
    /// jamais simulée.
    /// </summary>
    private async Task EnsureCatalogAsync()
    {
        if (Catalog.Metrics.Count > 0)
        {
            return;
        }

        try
        {
            var catalog = await _source.ReadCatalogAsync(CancellationToken.None);
            Catalog = catalog;
            CatalogText = catalog.Metrics.Count == 0
                ? "catalogue des métriques vide — seuls les noms techniques sont affichés"
                : $"catalogue {catalog.Version} · {catalog.Metrics.Count} fiche(s)";
        }
        catch (Exception exception)
        {
            Catalog = EchosMetricCatalog.Empty;
            CatalogText = $"catalogue des métriques indisponible — {exception.Message}";
        }
    }


    private void StartRefresh() => RequestRefresh();


    private void OnReplayCommand(string? action)
    {
        switch (action)
        {
            case "play":
                if (_ticks.Length > 0 && !IsAtLive)
                {
                    IsPlaying = true;
                }

                break;
            case "pause":
                IsPlaying = false;
                break;
            case "prev":
                if (ReplayIndex > 0)
                {
                    ReplayPosition = _ticks[ReplayIndex - 1];
                }

                break;
            case "next":
                if (ReplayIndex < _ticks.Length - 1)
                {
                    ReplayPosition = _ticks[ReplayIndex + 1];
                }

                break;
            case "live":
                if (_ticks.Length > 0)
                {
                    IsPlaying = false;
                    ReplayPosition = _ticks[^1];
                }

                break;
        }
    }

    /// <summary>Tick affiché par les vues liées au temps (monde, graphe de confiance) ;
    /// null tant qu'aucune série n'est publiée (lecture du dernier tick par ECHOS).</summary>
    private int? ViewTick => _ticks.Length == 0 ? null : _replayTick;

    /// <summary>Index de la position de relecture dans les ticks publiés.</summary>
    private int ReplayIndex
    {
        get
        {
            if (_ticks.Length == 0)
            {
                return 0;
            }

            var index = Array.FindIndex(_ticks, candidate => candidate >= _replayTick);
            return index < 0 ? _ticks.Length - 1 : index;
        }
    }


    /// <summary>
    /// Déplace le curseur de relecture. La fenêtre de lecture ne fait que tronquer
    /// les séries déjà publiées : aucune donnée n'est lue, écrite ni interpolée.
    /// </summary>
    private void SetReplayTick(int tick)
    {
        if (_ticks.Length == 0)
        {
            return;
        }

        var clamped = Math.Clamp(tick, _ticks[0], _ticks[^1]);
        if (_replayTick == clamped)
        {
            return;
        }

        _replayTick = clamped;
        var atLive = IsAtLive;
        if (atLive)
        {
            // « Suivre le direct » et « Relire ce run » sont deux états distincts (P3).
            IsPlaying = false;
            IsLive = true;
        }
        else
        {
            IsLive = false;
        }

        RaiseReplayChanged();
        RebuildTrend();

        if (string.Equals(SelectedSectionId, SectionWorld, StringComparison.Ordinal)
            || string.Equals(SelectedSectionId, SectionAgents, StringComparison.Ordinal)
            || string.Equals(SelectedSectionId, SectionRelations, StringComparison.Ordinal))
        {
            // Synchronisation des panneaux : la vue 2D et le graphe de confiance
            // lisent le tick du curseur.
            WorldTickInput = clamped.ToString(CultureInfo.InvariantCulture);
            RequestRefresh();
        }
    }


    private void RaiseReplayChanged()
    {
        // Ordre important : la valeur d'abord, puis les bornes (maximum avant minimum).
        // Sinon le curseur voit une fenêtre [min > max] invalide, se cale sur le plancher
        // et renvoie cette valeur à la vue modèle, déplaçant le curseur au premier tick.
        OnPropertyChanged(nameof(ReplayPosition));
        OnPropertyChanged(nameof(ReplayMaximum));
        OnPropertyChanged(nameof(ReplayMinimum));
        OnPropertyChanged(nameof(IsAtLive));
        OnPropertyChanged(nameof(ReplayText));
    }

    private void OnMetricSelectionChanged()
    {
        // La sélection ne modifie que la vue : les séries restent celles d'ECHOS.
        if (!HasData)
        {
            return;
        }

        RebuildTrend();
    }

    /// <summary>
    /// Reconstruit la courbe depuis le dernier relevé déjà lu : la sélection de
    /// l'utilisateur et la fenêtre de relecture sont des décisions d'affichage,
    /// aucune nouvelle lecture, aucun calcul local.
    /// </summary>
    private void RebuildTrend()
    {
        if (_lastSeries is null)
        {
            TrendSeries = Array.Empty<ISeries>();
            OnPropertyChanged(nameof(TrendSeries));
            RebuildDistribution();
            return;
        }

        var selected = Metrics.Where(option => option.IsSelected).Take(MaxSeries).ToList();
        var markers = BuildEventMarkers(selected);
        TrendSeries = BuildTrend(selected, _lastSeries, _ticks, ReplayIndex)
            .Concat(markers)
            .ToArray();
        OnPropertyChanged(nameof(TrendSeries));
        UpdateEventsText(markers.Length);
        RebuildDistribution();
    }

    /// <summary>
    /// Marqueurs d'événements (P3 — annotations de la vue temporelle) : une
    /// ligne verticale par tick publié portant au moins un événement.
    ///
    /// Le marqueur ne dit **pas** ce qui s'est passé : il signale qu'un
    /// événement existe à ce tick, et son type — la cause reste dans le journal
    /// publié. Borné à <see cref="MaxEventMarkers"/> marqueurs, limité à la
    /// fenêtre affichée, tracé sur l'étendue déjà dessinée : aucune valeur n'est
    /// produite (transformation de rendu).
    /// </summary>
    private ISeries[] BuildEventMarkers(IReadOnlyList<MetricOption> selected)
    {
        if (!ShowEventMarkers || _runEvents.Count == 0 || _ticks.Length == 0 || _lastSeries is null)
        {
            return [];
        }

        double? lowest = null;
        double? highest = null;
        var window = Math.Min(ReplayIndex, _ticks.Length - 1);
        foreach (var option in selected)
        {
            if (!_lastSeries.Values.TryGetValue(option.Engine, out var metrics)
                || !metrics.TryGetValue(option.Metric, out var values))
            {
                continue;
            }

            for (var index = 0; index <= window && index < values.Count; index++)
            {
                if (values[index] is not { } value)
                {
                    continue;
                }

                lowest = lowest is null ? value : Math.Min(lowest.Value, value);
                highest = highest is null ? value : Math.Max(highest.Value, value);
            }
        }

        if (lowest is null || highest is null)
        {
            return [];
        }

        // Série plate : une marge minimale évite des marqueurs invisibles.
        var padding = highest.Value > lowest.Value
            ? 0d
            : Math.Max(Math.Abs(lowest.Value) * 0.05, 0.5);
        var bottom = lowest.Value - padding;
        var top = highest.Value + padding;

        var from = _ticks[0];
        var to = _ticks[window];
        return _runEvents
            .Where(row => row.Tick >= from && row.Tick <= to)
            .GroupBy(row => row.Tick)
            .OrderBy(group => group.Key)
            .Take(MaxEventMarkers)
            .Select(group => (ISeries)new LineSeries<ObservablePoint?>
            {
                Name = $"t={group.Key} · {group.First().Type}",
                Values =
                [
                    new ObservablePoint(group.Key, bottom),
                    new ObservablePoint(group.Key, top),
                ],
                Stroke = new SolidColorPaint(EventColor(group.First().Type))
                {
                    StrokeThickness = 1.5f,
                },
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0,
            })
            .ToArray();
    }

    /// <summary>Couleur d'un marqueur par type d'événement (rendu seulement).</summary>
    private static SKColor EventColor(string type) => type switch
    {
        "agent_died" => new SKColor(0xf8, 0x51, 0x49),
        "group_formed" or "group_dissolved" => new SKColor(0xd2, 0xa8, 0xff),
        _ => new SKColor(0x8b, 0x94, 0x9e),
    };

    /// <summary>Journal d'événements du run : publié, borné, jamais interprété.</summary>
    private async Task RefreshEventsAsync(string runId)
    {
        try
        {
            var feed = await _source.ReadEventsAsync(runId, CancellationToken.None);
            _eventsFeed = feed;
            _runEvents = feed.Events;
        }
        catch (Exception exception)
        {
            // L'absence du journal est affichée : la courbe reste lisible sans lui.
            _eventsFeed = null;
            _runEvents = [];
            EventsText = $"journal d'événements indisponible — {exception.Message}";
        }

        RebuildTrend();
    }

    /// <summary>
    /// Compte-rendu des marqueurs : le texte annonce **ce qui est dessiné**
    /// (marqueurs dans la fenêtre affichée) sur ce qui est publié (total du
    /// journal) — jamais l'inverse, pour qu'une borne de rendu ne passe pas pour
    /// une intégralité.
    /// </summary>
    private void UpdateEventsText(int shown)
    {
        if (_eventsFeed is null)
        {
            return; // le relevé d'événements a déjà affiché son indisponibilité
        }

        if (_eventsFeed.Total == 0)
        {
            EventsText = "aucun événement publié pour ce run";
            return;
        }

        EventsText = $"{shown} marqueur(s) affiché(s) sur {_eventsFeed.Total} événement(s) publié(s)" +
            $" · {_eventsFeed.Types.Count} type(s) : " +
            string.Join(", ", _eventsFeed.Types.Select(kind => $"{kind.Type} ({kind.Count})")) +
            " · marqueur = tick publié, sans interprétation" +
            (shown < _eventsFeed.Events.Count ? $" · borné à {MaxEventMarkers} marqueurs" : string.Empty);
    }

    /// <summary>
    /// Outil de distribution (P3 — l'histogramme de la vue d'ensemble a été
    /// retiré, il est réintroduit ici comme **outil** explicite) : comptage des
    /// valeurs **publiées** de la métrique choisie, sur la fenêtre affichée.
    ///
    /// Le comptage est une transformation de **rendu** bornée : aucune
    /// valeur n'est produite, seule une fréquence de classe est dessinée. Les
    /// valeurs non mesurées et les ticks sans observation sont **exclus** et
    /// comptés séparément — jamais convertis en zéro observé.
    /// </summary>
    private void RebuildDistribution()
    {
        if (_lastSeries is null || SelectedDistributionOption is not { } option)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = "aucune métrique sélectionnée pour l'instant";
            RaiseDistributionChanged();
            return;
        }

        if (!_lastSeries.Values.TryGetValue(option.Engine, out var metrics)
            || !metrics.TryGetValue(option.Metric, out var values)
            || values.Count == 0)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = $"{option.Label} — aucune série publiée pour ce run";
            RaiseDistributionChanged();
            return;
        }

        var mask = _lastSeries.MeasuredByTick.TryGetValue(option.Engine, out var engineMask)
            && engineMask.TryGetValue(option.Metric, out var metricMask)
            ? metricMask
            : null;
        var window = Math.Min(ReplayIndex, values.Count - 1);
        var samples = new List<double>(window + 1);
        var excluded = 0;
        for (var index = 0; index <= window; index++)
        {
            var flag = mask is null || index >= mask.Count ? null : mask[index];
            if (values[index] is { } value && flag != false)
            {
                samples.Add(value);
            }
            else
            {
                // Repli neutre ou tick sans observation : exclu et compté, jamais compté comme 0.
                excluded++;
            }
        }

        if (samples.Count == 0)
        {
            DistributionSeries = Array.Empty<ISeries>();
            DistributionXAxes = BuildLabelAxes([]);
            DistributionText = $"{option.Label} — aucune observation publiée dans la fenêtre affichée " +
                $"({excluded} valeur(s) exclue(s) : non mesurée ou tick sans observation)";
            RaiseDistributionChanged();
            return;
        }

        var minimum = samples.Min();
        var maximum = samples.Max();
        var counts = new List<double>();
        var labels = new List<string>();
        var width = 0d;
        if (samples.Count >= 2 && maximum > minimum)
        {
            var bins = Math.Clamp((int)Math.Ceiling(Math.Log2(samples.Count)) + 1, 2, MaxDistributionBins);
            width = (maximum - minimum) / bins;
            var counters = new double[bins];
            foreach (var sample in samples)
            {
                var slot = Math.Min(bins - 1, (int)((sample - minimum) / width));
                counters[slot]++;
            }

            counts.AddRange(counters);
            for (var bin = 0; bin < bins; bin++)
            {
                labels.Add(FormatNumber(minimum + (bin * width)) + "–" + FormatNumber(minimum + ((bin + 1) * width)));
            }
        }
        else
        {
            // Toutes les observations publiées sont identiques : un seul intervalle.
            counts.Add(samples.Count);
            labels.Add(FormatNumber(minimum));
        }

        DistributionSeries =
        [
            new ColumnSeries<double>
            {
                Name = option.Metric,
                Values = counts,
                Fill = new SolidColorPaint(new SKColor(0x00, 0x78, 0xd7)),
                Stroke = null,
            },
        ];
        DistributionXAxes = BuildLabelAxes(labels);
        DistributionText = $"{option.Label} · {samples.Count} observation(s) publiée(s) · " +
            $"{counts.Count} intervalle(s)" +
            (width > 0 ? $" de largeur {FormatNumber(width)}" : string.Empty) +
            $" · {excluded} valeur(s) exclue(s) (non mesurée ou tick sans observation) · " +
            "comptage de classe sur la fenêtre affichée, aucune extrapolation";
        RaiseDistributionChanged();
    }

    /// <summary>Reconstruit la liste déroulante de l'outil de distribution (séries publiées).</summary>
    private void RebuildDistributionOptions()
    {
        var previous = SelectedDistributionOption?.Key;
        DistributionOptions.Clear();
        if (_lastSeries is null)
        {
            SelectedDistributionOption = null;
            return;
        }

        foreach (var (engine, metrics) in _lastSeries.Values)
        {
            foreach (var metric in metrics.Keys)
            {
                DistributionOptions.Add(new DistributionOption(engine, metric, $"{metric} ({engine})"));
            }
        }

        SelectedDistributionOption = DistributionOptions.FirstOrDefault(
                option => string.Equals(option.Key, previous, StringComparison.Ordinal))
            ?? DistributionOptions.FirstOrDefault();
    }

    private void RaiseDistributionChanged()
    {
        OnPropertyChanged(nameof(DistributionSeries));
        OnPropertyChanged(nameof(DistributionXAxes));
        OnPropertyChanged(nameof(DistributionYAxes));
        OnPropertyChanged(nameof(DistributionText));
    }

    /// <summary>Axe catégoriel : un libellé par classe de l'outil de distribution.</summary>
    private static Axis[] BuildLabelAxes(IReadOnlyList<string> labels) =>
    [
        new Axis
        {
            Name = "intervalle",
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            Labels = labels.ToList(),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            LabelsRotation = 20,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    /// <summary>
    /// Courbe : un point par tick publié, en coordonnée de tick réelle. Un tick
    /// sans observation (valeur nulle ou masque de mesure à `false`) devient un
    /// **trou** : la ligne se interrompt, jamais interpolée (P0).
    /// </summary>
    private static ISeries[] BuildTrend(
        IReadOnlyList<MetricOption> selected, EchosSeries series, IReadOnlyList<int> ticks, int lastIndex)
    {
        var list = new List<ISeries>();
        var color = 0;
        foreach (var option in selected)
        {
            if (!series.Values.TryGetValue(option.Engine, out var engine)
                || !engine.TryGetValue(option.Metric, out var values))
            {
                continue;
            }

            IReadOnlyList<bool?>? mask = null;
            if (series.MeasuredByTick.TryGetValue(option.Engine, out var engineMask)
                && engineMask.TryGetValue(option.Metric, out var publishedMask)
                && publishedMask is not null)
            {
                mask = publishedMask;
            }

            var limit = Math.Min(Math.Min(values.Count, ticks.Count), lastIndex + 1);
            var points = new ObservablePoint?[limit];
            for (var index = 0; index < limit; index++)
            {
                if (values[index] is { } value
                    && (mask is null
                        ? option.Measured
                        : index < mask.Count && mask[index] == true))
                {
                    points[index] = new ObservablePoint(ticks[index], value);
                }
                else
                {
                    points[index] = null;
                }
            }

            list.Add(new LineSeries<ObservablePoint?>
            {
                Name = option.Label,
                Values = points,
                Stroke = new SolidColorPaint(Palette[color % Palette.Length], 2f),
                Fill = null,
                GeometryStroke = new SolidColorPaint(Palette[color % Palette.Length], 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 5,
                LineSmoothness = 0,
            });
            color++;
        }

        return list.ToArray();
    }

    /// <summary>Panneau d'une série publiée (tick réel en abscisse, jamais interpolé).</summary>
    private static ISeries[] BuildPointSeries(string name, IReadOnlyList<EchosPoint> points, SKColor color)
    {
        if (points.Count == 0)
        {
            return Array.Empty<ISeries>();
        }

        var values = new ObservablePoint?[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            values[index] = new ObservablePoint(points[index].Tick, points[index].Value);
        }

        return
        [
            new LineSeries<ObservablePoint?>
            {
                Name = name,
                Values = values,
                Stroke = new SolidColorPaint(color, 2f),
                Fill = null,
                GeometryStroke = new SolidColorPaint(color, 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 4,
                LineSmoothness = 0,
            },
        ];
    }

    /// <summary>Texte des signaux déclencheurs publié derrière chaque phénomène.</summary>
    private string DescribeTriggers(EchosPhenomenon phenomenon)
    {
        if (phenomenon.Triggers.Count == 0)
        {
            return "aucun signal publié avec cette détection";
        }

        return string.Join(" · ", phenomenon.Triggers.Select(signal =>
            $"{signal.Metric} = {FormatNumber(signal.Value)} (seuil {FormatNumber(signal.Threshold)})"));
    }

    /// <summary>Fiche du catalogue « que regarder ? » pour une métrique, ou absence vide.</summary>
    private string LookupHelp(string engine, string metric) => LookupDoc(engine, metric)?.Help ?? string.Empty;

    /// <summary>Statut publié par le catalogue (« exploratoire » n'est jamais masqué).</summary>
    private string LookupStatus(string engine, string metric) => LookupDoc(engine, metric)?.StatusLabel ?? "—";

    /// <summary>Fiche du catalogue pour une métrique, par moteur puis par identifiant.</summary>
    private EchosMetricDoc? LookupDoc(string engine, string metric) =>
        Catalog.Metrics.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, metric, StringComparison.Ordinal)
                && string.Equals(candidate.Engine, engine, StringComparison.Ordinal))
            ?? Catalog.Metrics.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, metric, StringComparison.Ordinal));

    /// <summary>Issue de population en clair, sans réinterpréter le statut publié.</summary>
    private static string DescribeOutcome(string outcome) => outcome switch
    {
        "extinct" => "extinction observée",
        "surviving" => "survie observée",
        _ => string.IsNullOrWhiteSpace(outcome) ? "inconnue" : outcome,
    };

    /// <summary>
    /// État du run construit **uniquement** à partir des faits publiés (issue,
    /// dernier tick, lacunes). ECHOS ne publie pas l'état d'activité du flux :
    /// la distinction « en cours » / « interrompu » est une lacune explicite du
    /// contrat, affichée comme telle — jamais devinée (P3).
    /// </summary>
    private static string DescribeRunState(EchosViability viability)
    {
        var state = viability.Outcome switch
        {
            "extinct" => viability.ExtinctionTick is { } extinction
                ? $"terminé — extinction observée au tick {extinction}"
                : "terminé — extinction observée",
            "surviving" => viability.LastTick > 0
                ? $"aucune extinction observée jusqu'au tick {viability.LastTick} — run possiblement en cours"
                : "aucune extinction observée",
            _ => "inconnu — aucune issue publiée",
        };

        return viability.MissingTickCount > 0
            ? $"{state} · incomplet : {viability.MissingTickCount} tick(s) manquant(s)"
            : state;
    }

    /// <summary>
    /// Âge affiché d'un contexte échantillonné : cadence publiée
    /// (``conservation.sampledDetails.agentContextEvery``) et écart au dernier
    /// tick observé — deux métadonnées affichées côte à côte, jamais
    /// interpolées ni converties en valeur mesurée (P3).
    /// </summary>
    private string DescribeContextAge(string runId, int contextTick)
    {
        var age = string.Empty;
        var cadence = Runs.FirstOrDefault(option => option.RunId == runId)?.AgentContextEvery;
        if (cadence is { } every)
        {
            age = $" · cadence {every} tick(s)";
            if (every > 1)
            {
                age += $" (âge ≤ {every - 1})";
            }
        }

        if (contextTick >= 0 && _lastSeries?.LatestTick is { } latest && latest > contextTick)
        {
            age += $" · {latest - contextTick} tick(s) derrière le dernier ({latest})";
        }

        return age;
    }

    private static Axis[] BuildTickAxes() =>
    [
        new Axis
        {
            Name = "tick",
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            Labeler = value => value.ToString("0", CultureInfo.InvariantCulture),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            LabelsRotation = 0,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    private static Axis[] BuildValueAxes(string name) =>
    [
        new Axis
        {
            Name = name,
            NamePaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            LabelsPaint = new SolidColorPaint(new SKColor(0x9a, 0xa0, 0xaa)),
            TextSize = 12,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x2a, 0x2e, 0x37)),
            MinLimit = null,
        },
    ];

    /// <summary>
    /// Met une décision publiée en ligne de fiche : utilité publiée **ou**
    /// « non mesurée », état servi par ECHOS, score reporté cité séparément.
    ///
    /// Aucune substitution par zéro : la valeur affichée vient du champ publié
    /// (BL-09). L'état vient de `decision_state` tel que servi ; inconnu, il est
    /// annoncé comme tel plutôt que deviné.
    /// </summary>
    private static DecisionRow BuildDecisionRow(EchosDecision decision)
    {
        var measured = decision.Utility is { };
        var state = decision.DecisionState switch
        {
            "fresh" => "délibérée",
            "holdover" => "reportée",
            "interrupted" => "interrompue",
            "none" => "absente",
            null => "état non renseigné",
            var published => published,
        };
        if (decision.HeldUtility is { } held)
        {
            state += $" · {FormatNumber(held)}" +
                (decision.DecisionTick is { } origin ? $" (décision du tick {origin})" : string.Empty);
        }

        return new DecisionRow(
            decision.Tick,
            decision.ChosenAction,
            measured ? FormatNumber(decision.Utility) : "non mesurée",
            state,
            decision.Cause,
            measured);
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatNumber(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string FormatNumber(double? value) => value is { } published
        ? FormatNumber(published)
        : "—";

    private static string FormatNumber(int? value) => value is { } published
        ? FormatNumber(published)
        : "—";

    /// <summary>
    /// Rend une valeur **publiée par le moteur sans arrondi local** : sa
    /// représentation aller-retour exacte. Un champ non publié affiche son motif
    /// explicite (« non mesuré » ≠ 0) — jamais une valeur inventée.
    /// </summary>
    private static string FormatPublished(double? value, string missing) => value is { } published
        ? published.ToString("R", CultureInfo.InvariantCulture)
        : missing;

    /// <summary>
    /// Une ligne du panneau « groupes natifs » : composition, chef, naissance et
    /// mesures transportées depuis la réponse d'ECHOS, telles quelles.
    /// </summary>
    private static NativeGroupRow BuildNativeGroupRow(EchosNativeGroup group) => new(
        group.GroupId,
        group.Members.Count == 0 ? "—" : string.Join(", ", group.Members),
        FormatNumber(group.Size),
        group.LeaderId ?? "non publié",
        FormatNumber(group.BornTick),
        FormatPublished(group.Cohesion, "non publiée"),
        group.Decision ?? "non publiée",
        FormatPublished(group.Consensus, "non publiée"),
        FormatPublished(group.MaxBondDistance, "non mesurée"),
        FormatPublished(group.MeanBondDistance, "non mesurée"));

    private void RaiseChartsChanged()
    {
        OnPropertyChanged(nameof(TrendSeries));
        OnPropertyChanged(nameof(TrendXAxes));
        OnPropertyChanged(nameof(TrendYAxes));
        OnPropertyChanged(nameof(PopulationSeries));
        OnPropertyChanged(nameof(NeedsSeries));
        OnPropertyChanged(nameof(ResourceSeries));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(NetworkText));
        OnPropertyChanged(nameof(NativeGroups));
        OnPropertyChanged(nameof(NativeGroupsText));
    }
}

/// <summary>Run proposé dans le sélecteur, avec ses métadonnées d'issue de population.</summary>
public sealed class EchosRunOption : ObservableObject
{
    private EchosRunSummary _summary;

    /// <summary>Construit l'option depuis la métadonnée ECHOS.</summary>
    public EchosRunOption(EchosRunSummary summary) => _summary = summary;

    /// <summary>Identifiant du run.</summary>
    public string RunId => _summary.RunId;

    /// <summary>Libellé affiché : identifiant, ticks et issue.</summary>
    public string Label => $"{_summary.RunId} · {_summary.LastTick} tick(s) · {_summary.Outcome}";

    /// <summary>Dernier tick observé.</summary>
    public int LastTick => _summary.LastTick;

    /// <summary>Cadence publiée du contexte ``agents`` (ticks), si le run la publie.</summary>
    public int? AgentContextEvery => _summary.AgentContextEvery;

    /// <summary>
    /// Met à jour la métadonnée sans changer d'identité de sélection — le libellé
    /// notifié se rafraîchit dans le sélecteur sans que l'utilisateur perde le run
    /// choisi (le compte de ticks avance en direct).
    /// </summary>
    public void Update(EchosRunSummary summary)
    {
        _summary = summary;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(LastTick));
        OnPropertyChanged(nameof(AgentContextEvery));
    }
}

/// <summary>Run cochable pour la comparaison multi-run (aucune valeur n'est calculée ici).</summary>
public sealed class CompareRunOption(string runId, string label) : ObservableObject
{
    private bool _isSelected;
    private string _label = label;

    /// <summary>Identifiant du run.</summary>
    public string RunId { get; } = runId;

    /// <summary>Libellé affiché (identifiant, ticks, issue).</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>Vrai si le run est inclus dans la comparaison publiée.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>Une métrique découverte dans les séries, sélectionnable pour les vues.</summary>
public sealed class MetricOption : ObservableObject
{
    private bool _isSelected;
    private double _latest;
    private EchosSeries? _series;

    /// <summary>Construit l'option à partir du moteur et de la métrique.</summary>
    public MetricOption(string engine, string metric, bool measured, double latest, string help = "")
    {
        Engine = engine;
        Metric = metric;
        Measured = measured;
        _latest = latest;
        Help = help;
    }

    /// <summary>Moteur producteur (ex. <c>SocialComplexityMetrics</c>).</summary>
    public string Engine { get; }

    /// <summary>Nom de la métrique (ex. <c>ClusteringCoefficient</c>).</summary>
    public string Metric { get; }

    /// <summary>Étiquette affichée : métrique, abrégée du moteur si utile.</summary>
    public string Label => Metric;

    /// <summary>Vrai si ECHOS signale une mesure réelle (sinon repli neutre).</summary>
    public bool Measured { get; }

    /// <summary>Fiche du catalogue « que regarder ? » (vide si le catalogue est absent).</summary>
    public string Help { get; }

    /// <summary>Info-bulle « que regarder ? » ; null = aucune fiche au catalogue.</summary>
    public string? Tip => string.IsNullOrEmpty(Help) ? null : Help;

    /// <summary>Clef stable pour la sélection.</summary>
    public string Key => $"{Engine}/{Metric}";

    /// <summary>Séries du dernier relevé, pour reconstruire la courbe sans relecture.</summary>
    public EchosSeries? Series => _series;

    /// <summary>Vrai si la métrique est affichée sur la courbe.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Dernière valeur publiée par ECHOS.</summary>
    public double Latest
    {
        get => _latest;
        private set => SetProperty(ref _latest, value);
    }

    /// <summary>Dernière valeur sous forme de libellé.</summary>
    public string LatestLabel => Measured
        ? Latest.ToString("0.###", CultureInfo.InvariantCulture)
        : "non mesurée";

    /// <summary>Rattache les séries du relevé (reconstruction sans nouvel appel).</summary>
    public void NotifyLatest(EchosSeries series)
    {
        _series = series;
        if (series.Latest.TryGetValue(Engine, out var engine)
            && engine.TryGetValue(Metric, out var value))
        {
            Latest = value;
        }

        OnPropertyChanged(nameof(LatestLabel));
    }
}

/// <summary>Une ligne du tableau des phénomènes émergents, avec ses signaux publiés.</summary>
public sealed record PhenomenonRow(
    string Identifier,
    string DisplayName,
    string Description,
    int Occurrences,
    int FirstTick,
    int LastTick,
    string Triggers);

/// <summary>Un sous-écran de la fenêtre d'analyse (menu de navigation).</summary>
public sealed record AnalysisSectionOption(string Id, string Title, string Description);

/// <summary>Une ligne du tableau des statistiques exactes : valeur publiée et provenance.</summary>
public sealed record StatRow(string Engine, string Metric, string Display, bool Measured, string Status, string Help)
{
    /// <summary>Provenance affichée : mesurée, ou repli neutre signalé comme tel.</summary>
    public string Provenance => Measured ? "mesurée" : "repli neutre";

    /// <summary>Info-bulle « que regarder ? » ; null = aucune fiche au catalogue.</summary>
    public string? Tip => string.IsNullOrEmpty(Help) ? null : Help;
}

/// <summary>Une ligne du profil de viabilité : fait publié, valeur et note de lecture.</summary>
public sealed record ViabilityRow(string Label, string Value, string Note);

/// <summary>Une entrée de la chronologie publiée avant l'extinction.</summary>
public sealed record ChronologyRow(int Tick, string Type, string AgentId, string Action, string Cause);

/// <summary>Métrique proposée à l'outil de distribution (clé de série publiée).</summary>
public sealed record DistributionOption(string Engine, string Metric, string Label)
{
    /// <summary>Clé stable de sélection : moteur puis identifiant.</summary>
    public string Key => $"{Engine}/{Metric}";
}

/// <summary>Contexte de contrôle d'un run comparé, publié par ECHOS.</summary>
public sealed record CompareRunRow(
    string RunId,
    string Version,
    string Seed,
    string Outcome,
    string Ticks,
    string Extinction,
    string Conservation);

/// <summary>Valeurs publiées d'une métrique sur les runs comparés et sa dispersion publiée.</summary>
public sealed record CompareMetricRow(
    string Engine,
    string Metric,
    string Values,
    int RunsObserved,
    string Min,
    string Max,
    string Mean,
    string Spread);

/// <summary>Une relation mise en forme pour le tableau du sous-écran confiance.</summary>
public sealed record TrustEdgeRow(string Source, string Target, string Display);

/// <summary>
/// Une ligne du panneau « groupes natifs SYNE » : identité du groupe, composition,
/// chef, naissance et mesures publiées par le moteur, mises en forme telles quelles
/// (aucun arrondi local ; un champ non publié porte son motif explicite).
/// </summary>
public sealed record NativeGroupRow(
    int GroupId,
    string Members,
    string Size,
    string LeaderId,
    string BornTick,
    string Cohesion,
    string Decision,
    string Consensus,
    string MaxBondDistance,
    string MeanBondDistance);

/// <summary>Une entité sélectionnable dans la liste des fiches.</summary>
public sealed class AgentOption(string Id, double X, double Y, double Energy, string? Action, string? Group)
{
    /// <summary>Identifiant de l'entité.</summary>
    public string Id { get; } = Id;

    /// <summary>Position publiée au tick observé.</summary>
    public double X { get; } = X;

    /// <summary>Position publiée au tick observé.</summary>
    public double Y { get; } = Y;

    /// <summary>Énergie publiée.</summary>
    public double Energy { get; } = Energy;

    /// <summary>Action en cours publiée, si renseignée.</summary>
    public string? Action { get; } = Action;

    /// <summary>
    /// Communauté à laquelle l'entité appartient, si observée. C'est le libellé
    /// **communauté** publié par ECHOS (plus petit identifiant du groupe, stable) —
    /// une communauté **inférée** sur le graphe de confiance, distincte des groupes
    /// **natifs** du moteur (`groupId`, cohésion, consensus) que publie
    /// `/api/runs/{id}/native-groups` et qu'affiche le panneau « groupes natifs ».
    /// D'où le mot « communauté » ici.
    /// </summary>
    public string? Group { get; } = Group;

    /// <summary>Libellé affiché : identifiant, communauté et action.</summary>
    public string Label => Id + (string.IsNullOrWhiteSpace(Group) ? string.Empty : $" · communauté {Group}") +
        (string.IsNullOrWhiteSpace(Action) ? string.Empty : $" · {Action}");
}

/// <summary>Une croyance publiée, avec sa confiance brute mise en forme.</summary>
public sealed record BeliefRow(string Subject, string Predicate, string Value, string Confidence);

/// <summary>Une relation de confiance publiée pour l'entité sélectionnée.</summary>
public sealed record TrustRelationRow(string PeerId, string Trust);

/// <summary>
/// Une décision publiée pour l'entité sélectionnée.
///
/// L'utilité n'est affichée que si elle est publiée : une **absence** ou un
/// **report** affiche « non mesurée », jamais « 0 » (BL-09 / S-28 / B-10 — un
/// zéro observé et une absence de mesure ne se confondent pas). Le score d'un
/// report est cité dans la colonne d'état, sous son propre nom et avec le tick
/// de sa décision d'origine.
/// </summary>
public sealed record DecisionRow(
    int Tick, string Action, string Utility, string State, string Cause, bool Measured)
{
    private static readonly IBrush MeasuredBrush = new SolidColorBrush(Color.Parse("#e0a25a"));
    private static readonly IBrush AbsentBrush = new SolidColorBrush(Color.Parse("#9aa0aa"));

    /// <summary>Couleur de la colonne utilité : une absence n'est pas une mesure.</summary>
    public IBrush UtilityBrush => Measured ? MeasuredBrush : AbsentBrush;
}

/// <summary>
/// Une réserve du tick mise en forme pour le panneau de la vue 2D : quantité et
/// capacité publiées, et provenance de la position (« position publiée » ou
/// « agrégée par type » — le contrat SYNE ne localise pas les réserves d'un tick).
/// </summary>
public sealed record TickResourceRow(string Type, string Quantity, string Capacity, bool Located)
{
    /// <summary>Provenance affichée de la position.</summary>
    public string Provenance => Located ? "position publiée" : "agrégée par type";

    /// <summary>Explication de l'absence de position, sinon null.</summary>
    public string? PositionTip => Located
        ? null
        : "Réserves du tick : le contrat d'observabilité publie un total par type, sans " +
            "position. La réserve est donc listée ici et n'est pas projetée sur la carte " +
            "(aucun point ne serait publié pour la placer).";
}

/// <summary>
/// Une couche de la chaîne causale telle que servie par ECHOS : numéro d'étape,
/// couche, tick, libellé et détail publiés. L'accent est un choix de rendu, il ne
/// porte aucune valeur.
/// </summary>
public sealed record CausalNodeRow(int Step, string Layer, int Tick, string Label, string Detail)
{
    private static readonly IReadOnlyDictionary<string, IBrush> LayerBrushes =
        new Dictionary<string, IBrush>(StringComparer.Ordinal)
        {
            ["Action"] = new SolidColorBrush(Color.Parse("#2d9bf0")),
            ["Intention"] = new SolidColorBrush(Color.Parse("#5cc4ff")),
            ["Objectif"] = new SolidColorBrush(Color.Parse("#e0a25a")),
            ["Besoin"] = new SolidColorBrush(Color.Parse("#e05252")),
            ["Croyance"] = new SolidColorBrush(Color.Parse("#a04cf0")),
            ["Mémoire"] = new SolidColorBrush(Color.Parse("#2ecc71")),
            ["Perception"] = new SolidColorBrush(Color.Parse("#7ad1a0")),
        };

    private static readonly IBrush FallbackBrush = new SolidColorBrush(Color.Parse("#9aa0aa"));

    /// <summary>Couleur d'affichage de la couche (rendu seul).</summary>
    public IBrush Accent => LayerBrushes.TryGetValue(Layer, out var brush) ? brush : FallbackBrush;

    /// <summary>Étape affichée (« 1 » → « 7 »), pour lire l'ordre de la chaîne.</summary>
    public string StepLabel => Step.ToString(CultureInfo.InvariantCulture);

    /// <summary>Tick affiché avec le libellé de la trace.</summary>
    public string TickLabel => $"tick {Tick.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Info-bulle du détail publié ; null quand ECHOS n'en publie aucun.</summary>
    public string? DetailTip => string.IsNullOrWhiteSpace(Detail) ? null : Detail;
}

