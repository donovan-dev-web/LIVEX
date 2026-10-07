using System.Globalization;
using System.Text.Json;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;

namespace Launcher.App.Composition;

/// <summary>
/// Réalise <see cref="IEchosTelemetrySource"/> par HTTP contre l'API REST d'ECHOS
/// (ADR-007 : ECHOS produit, le Launcher présente).
///
/// Joignabilité : instance en cours si elle existe, sinon port déclaré au manifeste,
/// sinon 5000 (NETWORK.md §6.2) — la même règle que <see cref="EchosAnalysisService"/>.
/// Aucune valeur n'est calculée ici : les champs de la réponse sont transportés tels
/// quels, y compris le drapeau de provenance <c>measured</c> et le disclaimer.
/// </summary>
public sealed class EchosTelemetryService : IEchosTelemetrySource, IDisposable
{
    /// <summary>Port de contrôle ECHOS par défaut (NETWORK.md §6.2).</summary>
    public const int DefaultControlPort = 5000;

    private readonly OrchestrationService _orchestration;
    private readonly HttpClient _client;

    /// <summary>Initialise le service sur la composition.</summary>
    public EchosTelemetryService(OrchestrationService orchestration)
    {
        _orchestration = orchestration;
        _client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EchosRunSummary>> ReadRunsAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync("api/runs", cancellationToken).ConfigureAwait(false);
        var runs = new List<EchosRunSummary>();
        if (!document.RootElement.TryGetProperty("runs", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return runs;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var conservation = ObjectOrUndefined(item, "conservation");
            runs.Add(new EchosRunSummary(
                ReadString(item, "run_id"),
                ReadInt64(item, "seed"),
                ReadInt32(item, "ticks_count"),
                ReadInt32(item, "first_tick"),
                ReadInt32(item, "last_tick"),
                ReadString(item, "outcome"),
                ReadNullableInt32(item, "extinction_tick"),
                conservation.ValueKind == JsonValueKind.Object
                    ? ReadString(conservation, "level")
                    : null,
                ReadString(item, "version"),
                ReadNullableInt32(
                    ObjectOrUndefined(conservation, "sampledDetails"), "agentContextEvery")));
        }

        return runs;
    }

    /// <inheritdoc />
    public async Task<EchosSeries> ReadSeriesAsync(string? runId, int every, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new InvalidOperationException("aucun run sélectionné pour la lecture des séries");
        }

        var step = Math.Max(1, every);
        using var document = await GetJsonAsync(
            $"api/runs/{Uri.EscapeDataString(runId)}/metrics?every={step}", cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        return new EchosSeries
        {
            RunId = ReadString(root, "run_id"),
            Ticks = ReadIntArray(root, "ticks"),
            Values = ReadNullableSeriesMap(root, "values"),
            Latest = ReadLatestMap(root, "latest"),
            Measured = ReadMeasuredMap(root, "measured"),
            MeasuredByTick = ReadNullableMeasuredMap(root, "measured_by_tick"),
            MissingTicks = ReadIntArray(root, "missing_ticks"),
            MissingTicksCount = ReadInt32(root, "missing_ticks_count"),
            LatestTick = ReadNullableInt32(root, "latest_tick"),
        };
    }

    /// <inheritdoc />
    public async Task<EchosMetricCatalog> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync("api/metrics/catalog", cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var metrics = new List<EchosMetricDoc>();
        if (root.TryGetProperty("metrics", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var identifier = ReadString(item, "id");
                if (identifier.Length == 0)
                {
                    continue;
                }

                metrics.Add(new EchosMetricDoc(
                    identifier,
                    ReadString(item, "engine"),
                    ReadString(item, "label"),
                    ReadString(item, "unit"),
                    ReadString(item, "domain"),
                    ReadString(item, "definition"),
                    ReadString(item, "calculation"),
                    ReadString(item, "population"),
                    ReadString(item, "window"),
                    ReadString(item, "direction"),
                    ReadString(item, "status"),
                    ReadStringArray(item, "states"),
                    ReadString(item, "warning"),
                    ReadString(item, "visual"),
                    ReadStringArray(item, "renamedFrom")));
            }
        }

        return new EchosMetricCatalog
        {
            Version = ReadString(root, "version"),
            Metrics = metrics,
        };
    }

    /// <inheritdoc />
    public async Task<EchosViability> ReadViabilityAsync(
        string runId, int every, CancellationToken cancellationToken)
    {
        var step = Math.Max(1, every);
        using var document = await GetJsonAsync(
            $"api/runs/{Uri.EscapeDataString(runId)}/viability?every={step}", cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var population = ObjectOrUndefined(root, "population");
        var needs = ObjectOrUndefined(root, "needs");
        var resources = ObjectOrUndefined(root, "resources");
        var completeness = ObjectOrUndefined(root, "completeness");
        var conservation = ObjectOrUndefined(completeness, "conservation");
        var viability = ObjectOrUndefined(root, "viability");
        var calibration = ObjectOrUndefined(root, "calibration");

        return new EchosViability
        {
            RunId = ReadString(root, "run_id"),
            Outcome = ReadString(root, "outcome"),
            ExtinctionTick = ReadNullableInt32(root, "extinction_tick"),
            PopulationInitial = ReadNullableInt32(population, "initial"),
            PopulationFinal = ReadNullableInt32(population, "final"),
            PopulationMinimum = ReadNullableInt32(population, "minimumAlive"),
            PopulationSeries = ReadPoints(population, "series"),
            EnergySeries = ReadPoints(needs, "energy"),
            HungerSeries = ReadPoints(needs, "hunger"),
            ThirstSeries = ReadPoints(needs, "thirst"),
            FoodSeries = ReadPoints(resources, "food"),
            WaterSeries = ReadPoints(resources, "water"),
            MissingTickCount = ReadInt32(completeness, "missingTickCount"),
            ConservationLevel = ReadString(conservation, "level"),
            AgentContextEvery = ReadNullableInt32(
                ObjectOrUndefined(conservation, "sampledDetails"), "agentContextEvery"),
            LastTick = ReadInt32(root, "last_tick"),
            CalibrationAvailable = calibration.ValueKind == JsonValueKind.Object,
            EnergySlopePerTick = ReadNullableDouble(viability, "energySlopePerTick"),
            HungryDecisions = ReadNullableInt32(viability, "hungryDecisions"),
            ExtinctionChronology = ReadEventRows(root),
        };
    }

    /// <inheritdoc />
    public async Task<EchosExperimentSummary> ReadExperimentSummaryAsync(
        IReadOnlyList<string> runIds, CancellationToken cancellationToken)
    {
        var joined = string.Join(",", JoinIds(runIds));
        using var document = await GetJsonAsync(
            $"api/experiments/summary?runs={joined}", cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var runs = new List<EchosCompareRun>();
        if (root.TryGetProperty("runs", out var runItems) && runItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in runItems.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var conservation = ObjectOrUndefined(item, "conservation");
                runs.Add(new EchosCompareRun(
                    ReadString(item, "run_id"),
                    ReadString(item, "version"),
                    ReadString(item, "seed"),
                    ReadString(item, "outcome"),
                    ReadInt32(item, "ticks_count"),
                    ReadInt32(item, "first_tick"),
                    ReadInt32(item, "last_tick"),
                    ReadNullableInt32(item, "extinction_tick"),
                    ReadString(conservation, "level")));
            }
        }

        var metrics = new List<EchosCompareMetric>();
        if (root.TryGetProperty("metrics", out var engines) && engines.ValueKind == JsonValueKind.Object)
        {
            foreach (var engine in engines.EnumerateObject())
            {
                if (engine.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var metric in engine.Value.EnumerateObject())
                {
                    if (metric.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    metrics.Add(new EchosCompareMetric(
                        engine.Name,
                        metric.Name,
                        ReadNullableValueMap(metric.Value, "values"),
                        ReadInt32(metric.Value, "runsObserved"),
                        ReadNullableDouble(metric.Value, "min"),
                        ReadNullableDouble(metric.Value, "max"),
                        ReadNullableDouble(metric.Value, "mean"),
                        ReadNullableDouble(metric.Value, "spread")));
                }
            }
        }

        return new EchosExperimentSummary
        {
            Runs = runs,
            Metrics = metrics,
            Note = ReadString(root, "note"),
        };
    }

    /// <inheritdoc />
    public async Task<EchosNetwork> ReadNetworkAsync(string? runId, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(runId)
            ? "api/groups"
            : $"api/groups?run_id={Uri.EscapeDataString(runId)}";
        using var document = await GetJsonAsync(path, cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var groups = new List<EchosGroup>();
        if (root.TryGetProperty("groups", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var members = new List<string>();
                if (item.TryGetProperty("members", out var raw) && raw.ValueKind == JsonValueKind.Array)
                {
                    foreach (var member in raw.EnumerateArray())
                    {
                        members.Add(member.ToString());
                    }
                }

                var label = ReadString(item, "label");
                if (members.Count == 0 || label.Length == 0)
                {
                    continue;
                }

                var size = ReadInt32(item, "size");
                groups.Add(new EchosGroup(label, members, size > 0 ? size : members.Count));
            }
        }

        return new EchosNetwork
        {
            RunId = ReadString(root, "run_id"),
            Tick = ReadInt32(root, "tick"),
            Groups = groups,
        };
    }

    /// <inheritdoc />
    public async Task<EchosPhenomena> ReadPhenomenaAsync(string? runId, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(runId)
            ? "api/emergent-phenomena"
            : $"api/emergent-phenomena?run_id={Uri.EscapeDataString(runId)}";
        using var document = await GetJsonAsync(path, cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var detected = new List<EchosPhenomenon>();
        if (root.TryGetProperty("phenomena", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var identifier = ReadString(item, "identifier");
                if (identifier.Length == 0)
                {
                    continue;
                }

                detected.Add(ReadPhenomenon(item, identifier));
            }
        }

        return new EchosPhenomena
        {
            RunId = ReadString(root, "run_id"),
            Tick = ReadInt32(root, "tick"),
            Detected = detected,
            Disclaimer = ReadString(root, "disclaimer"),
        };
    }

    /// <summary>Libère le client HTTP.</summary>
    public void Dispose() => _client.Dispose();

    /// <inheritdoc />
    public async Task<EchosWorld> ReadWorldAsync(string? runId, int? tick, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(Query("api/world", runId, tick), cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var description = ReadWorldDescription(root);
        var agents = ReadAgents(root, out var groups);
        return new EchosWorld
        {
            RunId = ReadString(root, "run_id"),
            Tick = ReadInt32(root, "tick"),
            WorldTick = ReadInt32(root, "world_tick"),
            Description = description,
            Agents = agents,
            Groups = groups,
            Resources = ReadTickResources(root),
        };
    }

    /// <inheritdoc />
    public async Task<EchosTrustGraph> ReadTrustGraphAsync(
        string? runId, int? tick, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(Query("api/trust-graph", runId, tick), cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var nodes = new List<EchosTrustNode>();
        if (root.TryGetProperty("nodes", out var nodeItems) && nodeItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodeItems.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var position = node.TryGetProperty("position", out var nested) ? nested : node;
                nodes.Add(new EchosTrustNode(
                    ReadString(node, "id"),
                    ReadNullableDouble(node, "x") ?? ReadNullableDouble(position, "x"),
                    ReadNullableDouble(node, "y") ?? ReadNullableDouble(position, "y"),
                    ReadNullableDouble(node, "energy"),
                    ReadNullableDouble(node, "hunger"),
                    ReadNullableDouble(node, "thirst"),
                    ReadOptionalString(node, "action"),
                    ReadOptionalString(node, "group")));
            }
        }

        var edges = new List<EchosTrustEdge>();
        if (root.TryGetProperty("edges", out var edgeItems) && edgeItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in edgeItems.EnumerateArray())
            {
                if (edge.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                edges.Add(new EchosTrustEdge(
                    ReadString(edge, "source"),
                    ReadString(edge, "target"),
                    ReadNullableDouble(edge, "weight")));
            }
        }

        return new EchosTrustGraph
        {
            RunId = ReadString(root, "run_id"),
            Tick = ReadInt32(root, "tick"),
            Nodes = nodes,
            Edges = edges,
        };
    }

    /// <inheritdoc />
    public async Task<EchosRunDetail> ReadRunDetailAsync(string runId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"api/runs/{Uri.EscapeDataString(runId)}", cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var phenomena = root.TryGetProperty("phenomena", out var detectedRoot)
            ? detectedRoot
            : default;
        return new EchosRunDetail
        {
            RunId = ReadString(root, "run_id"),
            Seed = ReadInt64(root, "seed"),
            TicksCount = ReadInt32(root, "ticks_count"),
            FirstTick = ReadInt32(root, "first_tick"),
            LastTick = ReadInt32(root, "last_tick"),
            Outcome = ReadString(root, "outcome"),
            ExtinctionTick = ReadNullableInt32(root, "extinction_tick"),
            Metrics = ReadLatestMap(root, "metrics"),
            Measured = ReadMeasuredMap(root, "measured"),
            Detected = phenomena.ValueKind == JsonValueKind.Object
                ? ReadDetected(phenomena)
                : Array.Empty<EchosPhenomenon>(),
            Disclaimer = phenomena.ValueKind == JsonValueKind.Object
                ? ReadString(phenomena, "disclaimer")
                : string.Empty,
        };
    }

    /// <inheritdoc />
    public async Task<EchosAgentProfile> ReadAgentProfileAsync(
        string runId, string agentId, CancellationToken cancellationToken)
    {
        var run = Uri.EscapeDataString(runId);
        var agent = Uri.EscapeDataString(agentId);
        using var beliefsDocument = await GetJsonAsync(
            $"api/beliefs/{agent}?run_id={run}", cancellationToken).ConfigureAwait(false);
        using var trustDocument = await GetJsonAsync(
            $"api/relationships/{agent}?run_id={run}", cancellationToken).ConfigureAwait(false);

        var beliefsRoot = beliefsDocument.RootElement;
        var beliefs = new List<EchosBelief>();
        if (beliefsRoot.TryGetProperty("beliefs", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                beliefs.Add(new EchosBelief(
                    ReadString(item, "subject"),
                    ReadString(item, "predicate"),
                    ReadString(item, "value"),
                    ReadDouble(item, "confidence")));
            }
        }

        var trustRoot = trustDocument.RootElement;
        var trust = new List<EchosTrustRelation>();
        if (trustRoot.TryGetProperty("trust", out var relations) && relations.ValueKind == JsonValueKind.Array)
        {
            foreach (var relation in relations.EnumerateArray())
            {
                if (relation.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                trust.Add(new EchosTrustRelation(
                    ReadString(relation, "peerId"),
                    ReadDouble(relation, "trust")));
            }
        }

        return new EchosAgentProfile
        {
            AgentId = ReadString(beliefsRoot, "agent_id"),
            Tick = ReadInt32(beliefsRoot, "tick"),
            Beliefs = beliefs,
            Trust = trust,
        };
    }

    /// <inheritdoc />
    public async Task<EchosEventFeed> ReadEventsAsync(
        string runId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"api/runs/{Uri.EscapeDataString(runId)}/events", cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var events = new List<EchosEventRow>();
        if (root.TryGetProperty("events", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                events.Add(new EchosEventRow(
                    ReadInt32(item, "tick"),
                    ReadString(item, "type"),
                    ReadOptionalString(item, "agent_id"),
                    ReadOptionalString(item, "action"),
                    ReadOptionalString(item, "cause")));
            }
        }

        var kinds = new List<EchosEventKind>();
        if (root.TryGetProperty("types", out var typeItems) && typeItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeItems.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                kinds.Add(new EchosEventKind(ReadString(item, "type"), ReadInt32(item, "count")));
            }
        }

        return new EchosEventFeed
        {
            RunId = ReadString(root, "run_id"),
            Total = ReadInt32(root, "total"),
            Limit = ReadInt32(root, "limit"),
            Types = kinds,
            Events = events,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EchosDecision>> ReadDecisionsAsync(
        string runId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"api/runs/{Uri.EscapeDataString(runId)}/decisions", cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var decisions = new List<EchosDecision>();
        if (!root.TryGetProperty("decisions", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return decisions;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            decisions.Add(new EchosDecision(
                ReadInt32(item, "tick"),
                ReadString(item, "agent_id"),
                ReadString(item, "chosen_action"),
                ReadDouble(item, "utility"),
                ReadString(item, "cause")));
        }

        return decisions;
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        var baseUri = ResolveControlBase();
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(new Uri(baseUri, path), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"ECHOS injoignable sur {baseUri} — {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"ECHOS n'a pas répondu dans le délai accordé ({path})");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Le détail FastAPI (« run inconnu », « entité introuvable… ») est
                // la seule explication utile : l'afficher tel quel évite un
                // « 404 » muet qui se lit comme une panne générale d'ECHOS.
                throw new InvalidOperationException(
                    $"ECHOS a refusé la demande {path} ({(int)response.StatusCode}){DescribeDetail(body)}");
            }

            try
            {
                return JsonDocument.Parse(body);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"réponse malformée d'ECHOS ({path}) — {exception.Message}");
            }
        }
    }

    /// <summary>Extrait le champ <c>detail</c> d'une réponse d'erreur FastAPI, vide sinon.</summary>
    private static string DescribeDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("detail", out var detail))
            {
                var text = detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()
                    : detail.GetRawText();
                return string.IsNullOrWhiteSpace(text) ? string.Empty : $" — {text}";
            }
        }
        catch (JsonException)
        {
            // Corps non JSON : le code de statut reste seul, jamais d'exception.
        }

        return string.Empty;
    }

    /// <summary>Base de contrôle d'ECHOS : instance en cours, sinon manifeste, sinon 5000.</summary>
    private Uri ResolveControlBase()
    {
        var instance = _orchestration.Registry.FindByComponent("echos");
        if (instance is not null && instance.Endpoints.TryGetValue("control", out var resolved))
        {
            return new Uri(resolved.Url, UriKind.Absolute);
        }

        var installation = _orchestration.Registry.GetActiveInstallation("echos");
        var declaredPort = installation?.ManifestValid == true
            ? installation.Manifest?.Endpoints?.GetValueOrDefault("control")?.Port
            : null;
        return new Uri($"http://127.0.0.1:{declaredPort ?? DefaultControlPort}/", UriKind.Absolute);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double>>> ReadSeriesMap(
        JsonElement root, string property)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double>>>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var engines) || engines.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var engine in engines.EnumerateObject())
        {
            var metrics = new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);
            if (engine.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var metric in engine.Value.EnumerateObject())
                {
                    metrics[metric.Name] = ReadDoubleArray(metric.Value);
                }
            }

            result[engine.Name] = metrics;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> ReadLatestMap(
        JsonElement root, string property)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var engines) || engines.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var engine in engines.EnumerateObject())
        {
            var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
            if (engine.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var metric in engine.Value.EnumerateObject())
                {
                    metrics[metric.Name] = metric.Value.ValueKind == JsonValueKind.Number
                        ? metric.Value.GetDouble()
                        : 0;
                }
            }

            result[engine.Name] = metrics;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> ReadMeasuredMap(
        JsonElement root, string property)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var engines) || engines.ValueKind == JsonValueKind.Undefined)
        {
            return result;
        }

        if (engines.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var engine in engines.EnumerateObject())
        {
            var metrics = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (engine.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var metric in engine.Value.EnumerateObject())
                {
                    // Absence du drapeau = série publiée avant son introduction : on
                    // considère qu'elle est mesurée, faute de preuve du contraire.
                    metrics[metric.Name] = metric.Value.ValueKind != JsonValueKind.False;
                }
            }

            result[engine.Name] = metrics;
        }

        return result;
    }

    private static IReadOnlyList<double> ReadDoubleArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<double>();
        }

        var values = new List<double>();
        foreach (var item in element.EnumerateArray())
        {
            values.Add(item.ValueKind == JsonValueKind.Number ? item.GetDouble() : double.NaN);
        }

        return values;
    }

    /// <summary>Série alignée sur les ticks : ``null`` = tick sans observation.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>> ReadNullableSeriesMap(
        JsonElement root, string property)
    {
        var result =
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<double?>>>(
                StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var engines)
            || engines.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var engine in engines.EnumerateObject())
        {
            var metrics = new Dictionary<string, IReadOnlyList<double?>>(StringComparer.Ordinal);
            if (engine.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var metric in engine.Value.EnumerateObject())
                {
                    metrics[metric.Name] = ReadNullableDoubleArray(metric.Value);
                }
            }

            result[engine.Name] = metrics;
        }

        return result;
    }

    private static IReadOnlyList<double?> ReadNullableDoubleArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<double?>();
        }

        var values = new List<double?>();
        foreach (var item in element.EnumerateArray())
        {
            values.Add(item.ValueKind == JsonValueKind.Number ? item.GetDouble() : null);
        }

        return values;
    }

    /// <summary>Masque de provenance aligné par tick (`null` = tick sans observation).</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>> ReadNullableMeasuredMap(
        JsonElement root, string property)
    {
        var result =
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<bool?>>>(
                StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var engines)
            || engines.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var engine in engines.EnumerateObject())
        {
            var metrics = new Dictionary<string, IReadOnlyList<bool?>>(StringComparer.Ordinal);
            if (engine.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var metric in engine.Value.EnumerateObject())
                {
                    var flags = new List<bool?>();
                    if (metric.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in metric.Value.EnumerateArray())
                        {
                            flags.Add(item.ValueKind switch
                            {
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => null,
                            });
                        }
                    }

                    metrics[metric.Name] = flags;
                }
            }

            result[engine.Name] = metrics;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, double?> ReadNullableValueMap(
        JsonElement element, string property)
    {
        var result = new Dictionary<string, double?>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var values)
            || values.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var pair in values.EnumerateObject())
        {
            result[pair.Name] = pair.Value.ValueKind == JsonValueKind.Number
                ? pair.Value.GetDouble()
                : null;
        }

        return result;
    }

    /// <summary>Objet imbriqué, ou valeur vide si absent — jamais une exception.</summary>
    private static JsonElement ObjectOrUndefined(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return nested;
        }

        return default;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                items.Add(item.GetString() ?? string.Empty);
            }
        }

        return items;
    }

    /// <summary>Série ``(tick, valeur)`` publiée par ECHOS — coordonnées tick réelles.</summary>
    private static IReadOnlyList<EchosPoint> ReadPoints(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var series)
            || series.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EchosPoint>();
        }

        var points = new List<EchosPoint>();
        foreach (var item in series.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array
                || item.GetArrayLength() != 2
                || item[0].ValueKind != JsonValueKind.Number
                || item[1].ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            points.Add(new EchosPoint(item[0].GetDouble(), item[1].GetDouble()));
        }

        return points;
    }

    private static IReadOnlyList<EchosEventRow> ReadEventRows(JsonElement root)
    {
        if (!root.TryGetProperty("extinctionChronology", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EchosEventRow>();
        }

        var rows = new List<EchosEventRow>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            rows.Add(new EchosEventRow(
                ReadInt32(item, "tick"),
                ReadString(item, "type"),
                ReadOptionalString(item, "agent_id"),
                ReadOptionalString(item, "action"),
                ReadOptionalString(item, "cause")));
        }

        return rows;
    }

    /// <summary>Encodage des identifiants de runs pour la comparaison multi-runs.</summary>
    private static IReadOnlyList<string> JoinIds(IReadOnlyList<string> runIds)
    {
        var encoded = new List<string>(runIds.Count);
        foreach (var runId in runIds)
        {
            encoded.Add(Uri.EscapeDataString(runId));
        }

        return encoded;
    }

    private static IReadOnlyList<int> ReadIntArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var values = new List<int>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt32(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static long ReadInt64(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt64();
        }

        return value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), out var parsed)
                ? parsed
                : 0;
    }

    private static int? ReadNullableInt32(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static double ReadDouble(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    private static double? ReadNullableDouble(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.GetDouble();
    }

    private static string? ReadOptionalString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    /// <summary>Chemine un paramètre optionnel sans jamais l'inventer.</summary>
    private static string Query(string path, string? runId, int? tick)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(runId))
        {
            parameters.Add($"run_id={Uri.EscapeDataString(runId)}");
        }

        if (tick is { } value)
        {
            parameters.Add($"tick={value.ToString(CultureInfo.InvariantCulture)}");
        }

        return parameters.Count == 0 ? path : $"{path}?{string.Join("&", parameters)}";
    }

    /// <summary>Description de monde publiée une fois (`world_initialized`).</summary>
    private static EchosWorldDescription? ReadWorldDescription(JsonElement root)
    {
        if (!root.TryGetProperty("world", out var world) || world.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var cells = new List<EchosWorldCell>();
        if (world.TryGetProperty("cells", out var cellItems) && cellItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var cell in cellItems.EnumerateArray())
            {
                if (cell.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                cells.Add(new EchosWorldCell(
                    ReadInt32(cell, "x"),
                    ReadInt32(cell, "y"),
                    ReadString(cell, "terrainType"),
                    cell.TryGetProperty("walkable", out var walkable)
                        && walkable.ValueKind == JsonValueKind.True));
            }
        }

        var obstacles = new List<EchosObstacle>();
        if (world.TryGetProperty("obstacles", out var obstacleItems) && obstacleItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var obstacle in obstacleItems.EnumerateArray())
            {
                if (obstacle.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                obstacles.Add(new EchosObstacle(
                    ReadString(obstacle, "id"),
                    ReadDouble(obstacle, "x"),
                    ReadDouble(obstacle, "y"),
                    ReadDouble(obstacle, "radius")));
            }
        }

        var resources = new List<EchosWorldResource>();
        if (world.TryGetProperty("resources", out var resourceItems) && resourceItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var resource in resourceItems.EnumerateArray())
            {
                if (resource.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                resources.Add(new EchosWorldResource(
                    ReadString(resource, "id"),
                    ReadString(resource, "kind"),
                    ReadDouble(resource, "x"),
                    ReadDouble(resource, "y"),
                    ReadDouble(resource, "quantity")));
            }
        }

        var regions = new List<EchosRegion>();
        if (world.TryGetProperty("regions", out var regionItems) && regionItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var region in regionItems.EnumerateArray())
            {
                if (region.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                regions.Add(new EchosRegion(
                    ReadString(region, "id"),
                    ReadInt32(region, "x"),
                    ReadInt32(region, "y"),
                    ReadInt32(region, "width"),
                    ReadInt32(region, "height")));
            }
        }

        return new EchosWorldDescription(
            ReadInt32(world, "width"),
            ReadInt32(world, "height"),
            ReadDouble(world, "cellSize"),
            cells,
            obstacles,
            resources,
            regions);
    }

    /// <summary>Entités observées, rattachées à leur communauté par appartenance publiée.</summary>
    private static IReadOnlyList<EchosAgentSnapshot> ReadAgents(
        JsonElement root, out IReadOnlyList<EchosGroup> groups)
    {
        groups = ReadGroupArray(root);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var member in group.Members)
            {
                labels[member] = group.Label;
            }
        }

        var agents = new List<EchosAgentSnapshot>();
        if (!root.TryGetProperty("agents", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return agents;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            double? x = null;
            double? y = null;
            if (item.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.Object)
            {
                x = ReadNullableDouble(position, "x");
                y = ReadNullableDouble(position, "y");
            }

            var id = ReadString(item, "id");
            agents.Add(new EchosAgentSnapshot(
                id,
                x ?? 0,
                y ?? 0,
                ReadDouble(item, "energy"),
                ReadDouble(item, "hunger"),
                ReadDouble(item, "thirst"),
                ReadOptionalString(item, "currentAction"),
                labels.GetValueOrDefault(id)));
        }

        return agents;
    }

    private static IReadOnlyList<EchosGroup> ReadGroupArray(JsonElement root)
    {
        var groups = new List<EchosGroup>();
        if (!root.TryGetProperty("groups", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return groups;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var members = new List<string>();
            if (item.TryGetProperty("members", out var raw) && raw.ValueKind == JsonValueKind.Array)
            {
                foreach (var member in raw.EnumerateArray())
                {
                    members.Add(member.ToString());
                }
            }

            var label = ReadString(item, "label");
            if (members.Count == 0 || label.Length == 0)
            {
                continue;
            }

            var size = ReadInt32(item, "size");
            groups.Add(new EchosGroup(label, members, size > 0 ? size : members.Count));
        }

        return groups;
    }

    /// <summary>Réserves du tick, avec leur position observée.</summary>
    private static IReadOnlyList<EchosTickResource> ReadTickResources(JsonElement root)
    {
        var resources = new List<EchosTickResource>();
        if (!root.TryGetProperty("resources", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return resources;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            double x = 0;
            double y = 0;
            if (item.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.Object)
            {
                x = ReadNullableDouble(position, "x") ?? 0;
                y = ReadNullableDouble(position, "y") ?? 0;
            }

            resources.Add(new EchosTickResource(
                ReadString(item, "type"),
                x,
                y,
                ReadDouble(item, "quantity"),
                ReadNullableDouble(item, "capacity")));
        }

        return resources;
    }

    /// <summary>Phénomènes détectés d'un bloc `phenomena` (détail du run).</summary>
    private static IReadOnlyList<EchosPhenomenon> ReadDetected(JsonElement phenomenaRoot)
    {
        var detected = new List<EchosPhenomenon>();
        if (!phenomenaRoot.TryGetProperty("detected", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return detected;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var identifier = ReadString(item, "identifier");
            if (identifier.Length == 0)
            {
                continue;
            }

            detected.Add(ReadPhenomenon(item, identifier));
        }

        return detected;
    }

    /// <summary>
    /// Un phénomène détecté, **avec** son libellé requalifié, sa description et la
    /// trace des signaux déclencheurs (métrique, valeur, seuil) — c'est cette trace
    /// qui permet d'expliquer un déclenchement au lieu d'affirmer un phénomène.
    /// </summary>
    private static EchosPhenomenon ReadPhenomenon(JsonElement item, string identifier)
    {
        var signals = new List<EchosSignal>();
        if (item.TryGetProperty("signals", out var rawSignals)
            && rawSignals.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in rawSignals.EnumerateArray())
            {
                if (signal.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var metric = ReadString(signal, "metric");
                if (metric.Length == 0)
                {
                    continue;
                }

                signals.Add(new EchosSignal(
                    metric,
                    ReadDouble(signal, "value"),
                    ReadDouble(signal, "threshold")));
            }
        }

        return new EchosPhenomenon(
            identifier,
            ReadInt32(item, "occurrences"),
            ReadInt32(item, "firstTick"),
            ReadInt32(item, "lastTick"),
            ReadString(item, "label"),
            ReadString(item, "description"),
            signals);
    }
}
