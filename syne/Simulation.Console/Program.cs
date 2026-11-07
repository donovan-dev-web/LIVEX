// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Point d'entrée SYNE — mode CLI/batch.

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Simulation.Console.Batch;
using Simulation.Console.Control;
using Simulation.Console.Integration;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Performance;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Prism.Contracts;

namespace Simulation.Console;

/// <summary>
/// Point d'entrée SYNE — mode CLI/batch.
/// Priorité de configuration : défauts → --config → flags CLI.
/// Boucle minimale : monde + entités + grille spatiale + tick (SYNE-002/003/004).
/// Mode --observe : diffusion WebSocket des snapshots/événements par tick (SYNE-080).
/// Mode --benchmark : mesure débit/budgets/checksum aux échelles 50/500/1000 (SYNE-090…093).
/// </summary>
public static class Program
{
    /// <summary>
    /// Intervalle de ré-émission d'une commande couplée identique (write-back P9) :
    /// une intention stable n'est pas renvoyée à chaque tick, mais elle n'est pas
    /// perdue non plus si PRISM a abandonné l'ordre (obstacle, reprise).
    /// </summary>
    private const int PrismCommandRefreshTicks = 30;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            CliOptions cli = CliOptions.Parse(args);

            if (cli.Help)
            {
                System.Console.Out.WriteLine(CliOptions.Usage);
                return 0;
            }

            SimulationOptions options = ResolveOptions(cli);

            IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);
            if (errors.Count > 0)
            {
                System.Console.Error.WriteLine("Configuration invalide :");
                foreach (string error in errors)
                {
                    System.Console.Error.WriteLine($"  - {error}");
                }

                return 2;
            }

            if (cli.ControlPort is not null)
            {
                return await RunSupervisedAsync(options, cli);
            }

            if (cli.PrismUrl is not null)
            {
                return await RunPrismCoupledAsync(cli);
            }

            if (cli.Observe == true)
            {
                await RunObservedAsync(options, cli);
            }
            else if (cli.Benchmark == true)
            {
                RunBenchmark(options, cli);
            }
            else if (cli.Serve == true)
            {
                await RunServeAsync(options, cli);
            }
            else
            {
                await RunBatchAsync(options, cli);
            }

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException
            or DirectoryNotFoundException or InvalidDataException or FormatException or OverflowException
            or IOException or UnauthorizedAccessException or InvalidOperationException or HttpListenerException)
        {
            // FormatException et OverflowException proviennent de l'analyse des
            // arguments : sans ce filtre, « --seed -1 » ou « --max-ticks abc »
            // sortaient en trace d'appels au lieu d'un message exploitable.
            System.Console.Error.WriteLine($"Erreur : {ex.Message}");
            System.Console.Error.WriteLine("Lancez --help pour la liste des options.");
            return 2;
        }
    }

    /// <summary>
    /// Résolution effectives des options : défauts intégrés → profil de scénario
    /// (<c>--simulation</c>) → surcouche <c>--config</c> → flags CLI
    /// <c>reference</c> n'injecte rien : le profil de
    /// référence EST le défaut intégré calibré — tout chemin existant
    /// reste bit-à-bit identique. <c>prism</c> s'applique comme
    /// surcouche complète <b>sous</b> <c>--config</c>, qui garde donc la priorité
    /// (paradigme du Launcher : surcouche `launcher-config.json`).
    /// </summary>
    public static SimulationOptions ResolveOptions(CliOptions cli)
    {
        ArgumentNullException.ThrowIfNull(cli);
        SimulationOptions options = ConfigLoader.LoadDefaults();

        if (cli.ConfigPath is not null)
        {
            options = ConfigLoader.MergeJson(options, File.ReadAllText(cli.ConfigPath));
        }

        return ApplyCliOverrides(options, cli);
    }

    private static SimulationOptions ApplyCliOverrides(SimulationOptions options, CliOptions cli)
    {
        if (cli.WorldSize is { } size)
        {
            options.Simulation.WorldWidth = size.Width;
            options.Simulation.WorldHeight = size.Height;
        }

        if (cli.MaxTicks is { } maxTicks)
        {
            options.Simulation.MaxTicks = maxTicks;
        }

        if (cli.Ticks is { } ticks)
        {
            options.Simulation.MaxTicks = ticks;
        }

        if (cli.ExternalWorld == true)
        {
            options.World.ExternalWorld = true;
        }

        if (cli.Seed is { } seed)
        {
            options.Random.Seed = seed;
        }

        return options;
    }

    /// <summary>
    /// Lit la grille du monde PRISM (dimensions en blocs, cases d'incarnation)
    /// dans la première enveloppe du flux de lecture <c>/data/read</c>. PRISM est
    /// autoritaire sur la géométrie (ADR-013) : SYNE adopte ses dimensions au lieu
    /// de les déclarer.
    /// </summary>
    private static async Task<PrismWorldGrid> ReadPrismWorldGridAsync(Uri baseUri)
    {
        var endpoint = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = "/data/read",
        };
        await using var stream = new PrismReadStreamClient(endpoint.Uri);
        await stream.ConnectAsync().ConfigureAwait(false);
        await foreach (PrismReadStreamEnvelope envelope in stream.ReadAllAsync().ConfigureAwait(false))
        {
            if (!PrismWorldGrid.TryParse(envelope.Payload, out PrismWorldGrid? grid, out string? error))
            {
                throw new InvalidOperationException(error);
            }

            return grid!;
        }

        throw new InvalidOperationException(
            "Le flux de lecture PRISM s'est fermé sans instantané de monde.");
    }

    /// <summary>
    /// Gisements connus de SYNE : blocs ressource de l'instantané de monde PRISM,
    /// typés par le pack de contenu (seule table qui dise ce qu'un matériau est).
    /// </summary>
    private static PrismResourceSites BuildResourceSites(
        PrismWorldGrid grid,
        string? contentDirectory,
        out string summary)
    {
        IReadOnlyDictionary<ushort, ResourceKind> materials = PrismContentMaterials.Load(contentDirectory);
        PrismResourceSites sites = PrismResourceSites.Build(grid, materials);
        summary = string.IsNullOrWhiteSpace(contentDirectory)
            ? $"SYNE — contenu PRISM : aucun répertoire de contenu, {sites.Count} gisement(s) exposé(s)."
            : $"SYNE — contenu PRISM : {materials.Count} matériau(x) ressource, {sites.Count} gisement(s) " +
              $"exposé(s) dont {sites.DrinkableCount} buvable(s).";
        return sites;
    }

    private static async Task<int> RunPrismCoupledAsync(CliOptions cli)
    {
        if (string.IsNullOrWhiteSpace(cli.PrismUrl))
        {
            throw new ArgumentException("--prism-url est obligatoire.");
        }

        if (!Uri.TryCreate(cli.PrismUrl, UriKind.Absolute, out Uri? baseUri))
        {
            throw new ArgumentException($"URL PRISM invalide : {cli.PrismUrl}");
        }

        int ticks = cli.PrismTicks ?? 100;
        if (ticks <= 0)
        {
            throw new ArgumentException("--prism-ticks doit être strictement positif.");
        }

        using var httpClient = new HttpClient { BaseAddress = baseUri };
        string sessionToken = Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN") ?? "dev-token";
        var client = new PrismClient(httpClient, sessionToken);

        if (cli.PrismSeed is { } seed && cli.PrismContentHash is { } contentHash)
        {
            var prepared = await client.PrepareAsync(seed, contentHash, cli.PrismContentDir);
            System.Console.WriteLine($"PRISM prepare : {prepared.State} (tick {prepared.LastCompletedTick})");
        }

        long startTick = 0;
        // Tick du snapshot SYNE restauré : le checkpoint contient le dernier tick
        // ACHEVÉ (2499), alors que le premier tick à demander est le suivant (2500,
        // sinon PRISM répond « unexpected_tick »). Les deux valeurs sont distinctes
        // et l'alignement du snapshot SYNE se vérifie sur celle du checkpoint.
        long restoredCheckpointTick = 0;
        ulong? restoredSeed = null;
        bool restoredFromCheckpoint = false;
        if (!string.IsNullOrWhiteSpace(cli.PrismRestoreCheckpoint))
        {
            var restored = await client.RestoreCheckpointAsync(
                cli.PrismRestoreCheckpoint!,
                cli.PrismContentHash ?? string.Empty);
            restoredCheckpointTick = restored.LastCompletedTick;
            // Reprise sans `--prism-seed` (le host est déjà préparé par le
            // checkpoint) : la graine vient du host restauré, sinon SYNE se
            // reconstruirait avec une autre graine et la reprise divergerait de
            // la trajectoire d'origine pour une raison étrangère au checkpoint.
            restoredSeed = restored.Seed;
            restoredFromCheckpoint = true;
            startTick = CoupledCheckpointStore.NextTickAfterRestore(restoredCheckpointTick);
            ticks = (int)Math.Max(0, ticks - startTick);
            System.Console.WriteLine(
                $"PRISM restauration : {restored.State} (dernier tick achevé {restoredCheckpointTick}, reprise à {startTick})");
        }

        if (cli.PrismTicks is not null && ticks > 0)
        {
            var started = await client.StartAsync(maxTicks: ticks + (int)startTick);
            System.Console.WriteLine($"PRISM start : {started.State}");
        }

        // Point de reprise couplé (SYNE-225) : répertoire des snapshots SYNE.
        string checkpointDirectory = cli.PrismCheckpointDir ?? CoupledCheckpointStore.DefaultDirectory;

        SimulationLoop? loop = null;
        Dictionary<ulong, Entity>? entitiesById = null;
        PrismWorldSynchronizer? synchronizer = null;
        IReadOnlyList<(ulong AgentId, PrismSpawnCell Cell)>? incarnation = null;
        PrismResourceSites resourceSites = PrismResourceSites.Empty;
        // Étape 3b : ce que l'hôte sait obtenir. Sans pack de contenu lisible, le
        // pont retombe sur le vocabulaire minimal (Aller / Consommer à la source).
        PrismItemCatalog itemCatalog = PrismItemCatalog.Empty;
        int agents = cli.PrismAgents ?? 0;
        if (agents > 0)
        {
            // ADR-013 : le monde SYNE *est* la grille PRISM. Dimensions et cases
            // d'incarnation se lisent dans l'instantané de monde publié par PRISM
            // — aucune constante de monde, aucune échelle. Sans cet instantané,
            // le run couplé serait aveugle (constat R2 du 6 novembre 2026).
            PrismWorldGrid worldGrid;
            try
            {
                worldGrid = await ReadPrismWorldGridAsync(baseUri);
            }
            catch (Exception exception)
            {
                System.Console.Error.WriteLine(
                    $"SYNE — instantané de monde PRISM illisible : {exception.Message}");
                return 3;
            }

            // SYNE-220 étape 2 (bascule par profil) : en mode couplé, SYNE ne
            // génère plus de monde local — il lit celui de PRISM par
            // PrismWorldAdapter. Le profil est celui de référence : un mode
            // couplé n'a plus de calibration propre (l'ancien profil `prism` ne
            // portait que la géométrie d'Unreal, désormais lue dans l'instantané
            // de PRISM, et une cadence qui ralentissait les désirs sans
            // contrepartie côté PRISM).
            SimulationOptions options = ResolveOptions(cli);
            options.World.ExternalWorld = true;
            options.Agents.InitialCount = agents;
            options.Random.Seed = cli.PrismSeed ?? restoredSeed ?? 1UL;
            options.Simulation.WorldWidth = worldGrid.Width;
            options.Simulation.WorldHeight = worldGrid.Height;
            (_, Simulation.Core.World.IWorld world, loop, _) =
                BuildSimulation(options, (ulong)agents, options.Random.Seed);
            if (world is not Simulation.Core.World.PrismWorldAdapter externalWorld)
            {
                throw new InvalidOperationException(
                    "Le run couplé exige le monde externe PRISM (world.externalWorld).");
            }

            synchronizer = new PrismWorldSynchronizer(externalWorld);
            entitiesById = loop!.World.Entities.ToDictionary(e => e.Id.Value);

            // Points d'eau (P9) : PRISM est la seule autorité sur ses sources, et
            // le pack de contenu est la seule table qui dise qu'un matériau se
            // boit. Sans contenu, le monde n'expose aucun gisement — dégradation
            // visible au diagnostic, jamais une source inventée.
            resourceSites = BuildResourceSites(worldGrid, cli.PrismContentDir, out string contentSummary);
            // SYNE-220 étape 3c : plus de gisement local (PRISM publie les ressources).
            System.Console.WriteLine(contentSummary);
            itemCatalog = PrismItemCatalog.Load(cli.PrismContentDir);
            System.Console.WriteLine(
                itemCatalog.HasFood
                    ? $"SYNE — objets PRISM : {itemCatalog.FoodItemIds.Count} comestible(s), " +
                      $"{itemCatalog.DrinkItemIds.Count} buvable(s) — acquisition par tag « {PrismItemCatalog.FoodTag} »."
                    : $"SYNE — objets PRISM : aucun comestible déclaré, la faim reste un simple déplacement.");

            // Affectation déterministe agent ↔ case, puis alignement du monde SYNE
            // sur les positions que PRISM attribuera : la cognition du tick 0 lit
            // déjà le bon endroit. Aucune ré-incarnation lors d'une reprise, les
            // agents existent dans le checkpoint.
            if (!restoredFromCheckpoint)
            {
                IReadOnlyList<PrismSpawnCell> cells = worldGrid.PlanSpawns(agents);
                var assignments = new List<(ulong AgentId, PrismSpawnCell Cell)>(agents);
                int index = 0;
                foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
                {
                    if (index >= cells.Count)
                    {
                        break;
                    }

                    assignments.Add((entity.Id.Value, cells[index++]));
                }

                incarnation = assignments;
                externalWorld.ApplyAgentStates(assignments.Select(static assignment =>
                    (assignment.AgentId,
                     WorldSpace.CellCenter(assignment.Cell.X, assignment.Cell.Y))));
                System.Console.WriteLine(
                    $"SYNE cognition : {assignments.Count} agent(s) — monde PRISM {worldGrid.Width}×{worldGrid.Height} blocs (ADR-013).");
            }
        }

        if (!string.IsNullOrWhiteSpace(cli.PrismRestoreCheckpoint) && loop is not null)
        {
            long expectedTick = restoredFromCheckpoint ? restoredCheckpointTick : startTick;
            CoupledCheckpointLoad load = CoupledCheckpointStore.Load(
                checkpointDirectory, cli.PrismRestoreCheckpoint!, expectedTick);
            if (!load.Succeeded)
            {
                System.Console.Error.WriteLine($"SYNE — reprise couplée refusée : {load.Error}");
                return 2;
            }

            Simulation.Core.Persistence.SimulationSnapshot restored =
                Simulation.Core.Persistence.SimulationSnapshotCodec.FromJson(load.Checkpoint!.SnapshotJson);
            Simulation.Core.Persistence.SimulationSnapshotRestorer.RestoreCognition(loop, restored);
            entitiesById = loop.World.Entities.ToDictionary(e => e.Id.Value);
            System.Console.WriteLine(
                $"SYNE — cognition restaurée : {restored.Minds.Count} esprit(s) depuis {load.Path} (tick {load.Checkpoint.Tick}, empreinte {load.Checkpoint.Fingerprint}).");
        }

        // Le checkpoint PRISM et le snapshot SYNE sont écrits au même identifiant
        // et au même tick ; le snapshot SYNE est validé avant toute reprise — un
        // désalignement est refusé plutôt que corrigé en silence.
        Func<string, long, CancellationToken, Task>? saveCheckpoint = null;
        if (!string.IsNullOrWhiteSpace(cli.PrismCheckpointId))
        {
            saveCheckpoint = async (id, tick, ct) =>
            {
                await client.SaveCheckpointAsync(id, ct).ConfigureAwait(false);
                if (loop is not null)
                {
                    string path = CoupledCheckpointStore.Save(checkpointDirectory, id, tick, loop);
                    System.Console.WriteLine($"SYNE — snapshot couplé : {path} (tick {tick})");
                }
            };
        }

        // Position SYNE → case PRISM d'approche. L'altitude n'entre pas dans le
        // cheminement PRISM (A* en 2D, `StartMoveOrder` ne contrôle que x/y), elle
        // n'est donc pas devinée ici ; seule une consommation porte un z exact.
        PrismBlockPosition MapApproachCell(Position position) => new(
            WorldSpace.BlockOf(position.X),
            WorldSpace.BlockOf(position.Y),
            0);

        // Boire : la cible cognitive est la case d'approche, le bloc à viser est le
        // bloc d'eau lui-même (adjacence à distance 1 côté PRISM).
        PrismBlockPosition? MapDrinkSource(GoalTarget target) =>
            resourceSites.TrySource(target.Label, out PrismBlockPosition source) ? source : null;

        // Write-back BDI → PRISM (P9) : chaque décision traduisible devient une
        // commande du tick. Une commande n'est re-émise que si sa cible change, ou
        // toutes les `PrismCommandRefreshTicks` — sinon PRISM repaie un A* par agent
        // et par tick pour réordonner un trajet identique.
        var lastCommands = new Dictionary<ulong, (string Kind, string Payload, long Tick)>();
        // Inventaire observé par agent (étape 3b) : la seule source honnête de « ce
        // que je porte » en mode couplé, puisque l'inventaire local n'est pas autoritaire.
        var carriedItems = new Dictionary<ulong, IReadOnlyList<ushort>>();
        var decisionsByKind = new Dictionary<DesireKind, int>();
        int decisionsSeen = 0;
        int decisionsWithTarget = 0;
        int commandsSent = 0;
        int commandsSuppressed = 0;
        var commandOutcomes = new Dictionary<string, int>(StringComparer.Ordinal);

        // Mémoire des refus durables : une cible que PRISM vient de déclarer
        // inatteignable n'est pas redemandée à chaque fenêtre de rafraîchissement.
        var failureMemory = new PrismCommandFailureMemory();

        var runner = new PrismCoupledRunner(
            (request, ct) => client.StepAsync(request, ct),
            decide: (tick, observed) =>
            {
                var commands = new List<CommandEnvelope>();
                if (incarnation is not null && tick == startTick)
                {
                    commands.AddRange(PrismIncarnation.Build(tick, incarnation));
                    return commands;
                }

                // Aucune observation reçue : l'agent n'a rien perçu, il n'y a rien à
                // décider (le premier tick d'un run sert à incarner).
                if (loop is null || observed.Count == 0)
                {
                    return commands;
                }

                IReadOnlyList<DecisionRecord> decisions = loop.Cognition.StepCoupled((ulong)tick);
                decisionsSeen += decisions.Count;
                foreach (DecisionRecord decision in decisions)
                {
                    decisionsByKind[decision.Chosen] = decisionsByKind.GetValueOrDefault(decision.Chosen) + 1;
                    if (decision.Target is not null)
                    {
                        decisionsWithTarget++;
                    }

                    PrismCommandVocabulary? decisionVocabulary = itemCatalog.HasFood
                        ? new PrismCommandVocabulary(itemCatalog, carriedItems.GetValueOrDefault(decision.EntityId))
                        : null;
                    CommandEnvelope? command = PrismDecisionCommandMapper.MapOne(
                        tick,
                        decision,
                        MapApproachCell,
                        MapDrinkSource,
                        decisionVocabulary);
                    if (command is null)
                    {
                        continue;
                    }

                    string payload = command.Payload.GetRawText();
                    if (lastCommands.TryGetValue(command.AgentId, out var previous) &&
                        previous.Kind == command.Kind &&
                        string.Equals(previous.Payload, payload, StringComparison.Ordinal) &&
                        tick - previous.Tick < PrismCommandRefreshTicks)
                    {
                        continue;
                    }

                    if (failureMemory.IsSuppressed(command.AgentId, command.Kind, payload, tick))
                    {
                        commandsSuppressed++;
                        continue;
                    }

                    lastCommands[command.AgentId] = (command.Kind, payload, tick);
                    commands.Add(command);
                    commandsSent++;
                }

                return commands;
            },
            onObservations: (agentId, obs, tick) =>
            {
                if (loop is not null && entitiesById is not null &&
                    entitiesById.TryGetValue(agentId, out Entity? entity))
                {
                    loop.Cognition.ApplyExternalObservations(entity, obs, (ulong)tick);
                }
            },
            saveCheckpointAsync: saveCheckpoint,
            onEnvelope: (envelope, tick) =>
            {
                // Monde externe puis mémoire spatiale (SYNE-220 étape 2, SYNE-221) :
                // les deux lisent l'état du tick qui vient d'être reçu.
                synchronizer?.Apply(envelope);
                carriedItems[envelope.AgentId] = PrismObservationMapper.ToCarriedItemIds(envelope);
                if (loop is not null &&
                    entitiesById is not null &&
                    entitiesById.TryGetValue(envelope.AgentId, out Entity? observed))
                {
                    IReadOnlyList<Simulation.Core.Perception.SpatialChunkObservation> chunks =
                        PrismObservationMapper.ToSpatialChunks(envelope);
                    if (chunks.Count > 0)
                    {
                        loop.Cognition.ApplyExternalSpatialObservations(observed, chunks, (ulong)tick);
                    }
                }
            },
            resourceSites: resourceSites,
            itemCatalog: itemCatalog,
            onTickResult: result =>
            {
                // Issues de commandes publiées par PRISM : une commande refusée
                // (`invalid_payload`, `path_not_found`, `not_adjacent`…) doit se
                // voir. Sans ce compteur, le write-back paraît fonctionner alors
                // que PRISM n'exécute rien.
                foreach (EventEnvelope envelope in result.Events)
                {
                    string reason = envelope.Payload.ValueKind == JsonValueKind.Object &&
                                    envelope.Payload.TryGetProperty("reason", out JsonElement reasonElement) &&
                                    reasonElement.ValueKind == JsonValueKind.String
                        ? reasonElement.GetString()!
                        : string.Empty;
                    string key = reason.Length == 0
                        ? envelope.Kind
                        : string.Concat(envelope.Kind, ":", reason);
                    commandOutcomes[key] = commandOutcomes.GetValueOrDefault(key) + 1;
                }
            },
            onCommandFailed: (agentId, kind, payload, reason, failureTick) =>
            {
                // Refus durable (`path_not_found`, `source_not_found`) : la cible
                // sort du write-back pour la fenêtre de réessai.
                failureMemory.Record(agentId, kind, payload, reason, failureTick);
            });

        System.Console.WriteLine($"SYNE — boucle couplée PRISM : {ticks} ticks (départ {startTick})...");
        var run = await runner.RunAsync(
            ticks,
            startTick: startTick,
            checkpointId: cli.PrismCheckpointId,
            checkpointEvery: cli.PrismCheckpointEvery ?? 0);
        if (run.LastCheckpointId is not null)
        {
            System.Console.WriteLine($"SYNE — dernier checkpoint couplé : {run.LastCheckpointId} (tick {run.LastCheckpointTick})");
        }
        if (run.RestoredCheckpointId is not null)
        {
            System.Console.WriteLine($"SYNE — checkpoint restauré : {run.RestoredCheckpointId} (tick {run.RestoredCheckpointTick})");
        }

        try
        {
            await client.StopAsync();
        }
        catch (PrismClientException exception) when (exception.ResponseBody.Contains("invalid_state", StringComparison.Ordinal))
        {
            // L'hôte s'est déjà arrêté à l'échéance de maxTicks : rien à faire.
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine($"PRISM stop : {exception.Message}");
        }

        if (run.Succeeded)
        {
            System.Console.WriteLine($"SYNE — boucle couplée terminée : {run.TicksCompleted}/{ticks} ticks OK.");
            if (loop is not null)
            {
                // PRISM-082 (run R2) : l'empreinte rend la reproductibilité
                // vérifiable — trois passes de même graine doivent la reproduire.
                // L'empreinte porte le tick absolu (départ + ticks exécutés) : un
                // run repris après un checkpoint doit produire celle du run
                // continu de même horizon, sinon la reprise n'est pas comparable.
                System.Console.WriteLine(
                    $"SYNE — empreinte couplée : {CoupledFingerprint.Compute(loop, (ulong)(startTick + run.TicksCompleted))}");
                if (synchronizer is not null)
                {
                    // Critère de succès du couplage : une position effectivement
                    // appliquée prouve que PRISM a incarné les agents et publie
                    // leurs observations (un run sans incarnation laisse ces
                    // compteurs à zéro — c'est le constat qui a invalidé R2).
                    int minds = loop.World.Entities.Count(entity => loop.Cognition.HasMind(entity.Id.Value));
                    System.Console.WriteLine(
                        $"SYNE — couplage effectif : {synchronizer.AppliedTotal} observation(s) positionnée(s), " +
                        $"{synchronizer.MovedTotal} déplacement(s), {minds}/{loop.World.Entities.Count} esprit(s).");

                    // Write-back : sans ces deux compteurs, un run où SYNE ne dit
                    // jamais rien à PRISM ressemblerait encore à un succès.
                    string kinds = string.Join(
                        ", ",
                        decisionsByKind
                            .OrderByDescending(pair => pair.Value)
                            .ThenBy(pair => pair.Key)
                            .Take(5)
                            .Select(pair => $"{pair.Key}={pair.Value}"));
                    string outcomes = commandOutcomes.Count == 0
                        ? "aucune issue publiée"
                        : string.Join(
                            ", ",
                            commandOutcomes
                                .OrderByDescending(pair => pair.Value)
                                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                                .Take(6)
                                .Select(pair => $"{pair.Key}={pair.Value}"));
                    System.Console.WriteLine(
                        $"SYNE — write-back : {decisionsSeen} décision(s) dont {decisionsWithTarget} ciblée(s) " +
                        $"[{kinds}], {commandsSent} commande(s) émise(s), {resourceSites.Count} gisement(s) " +
                        $"dont {resourceSites.DrinkableCount} buvable(s) — " +
                        $"issues PRISM : {outcomes} ; refus durables : {failureMemory.Describe()}, " +
                        $"{commandsSuppressed} commande(s) non réémise(s).");
                }
            }

            return 0;
        }

        System.Console.Error.WriteLine(
            $"SYNE — échec au tick {run.FailedAtTick} : {run.Failure?.Message}");
        return 1;
    }

    private static async Task RunBatchAsync(SimulationOptions options, CliOptions cli)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        (_, var world, SimulationLoop loop, EntityTemplate template) = BuildSimulation(options);
        int initialEntityCount = world.Entities.Count;

        // L'export du flux passe par l'émetteur d'observabilité : les trames
        // écrites sont exactement celles du WebSocket, et l'export n'ajoute
        // aucun tirage PRNG.
        string? runId = null;
        if (cli.ExportStream)
        {
            string streamDirectory = cli.ExportDirectory
                ?? throw new ArgumentException("--export-stream exige --export-dir.");
            runId = string.IsNullOrWhiteSpace(cli.RunId)
                ? ObservabilityContract.RunIdFor(options.Random.Seed)
                : cli.RunId;
            await using var stream = new BatchStreamFile(
                Path.Combine(streamDirectory, BatchStreamFile.DefaultFileName));
            var emitter = new Observability.ObservabilityTickEmitter(loop, options.Random.Seed, stream, runId);
            await emitter.RunAsync(options.Simulation.MaxTicks);
            System.Console.WriteLine($"  flux d'observabilité : {stream.Path} ({stream.FramesWritten} trame(s))");
        }
        else
        {
            loop.Run(options.Simulation.MaxTicks);
        }

        sw.Stop();

        if (cli.ExportDirectory is not null)
        {
            string resultPath = await BatchRunExporter.WriteResultAsync(
                loop,
                cli.Simulation ?? "reference",
                options.Random.Seed,
                (ulong)options.Agents.InitialCount,
                cli.ExportDirectory,
                cli.RunId ?? runId,
                cli.ExportStream ? BatchStreamFile.DefaultFileName : null);
            System.Console.WriteLine($"  résultat batch : {resultPath}");
        }

        if (cli.Headless != true)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds, initialEntityCount);
        }
    }

    /// <summary>
    /// Mode --benchmark (SYNE-090…093) : pour chaque population
    /// ∈ {50, 500, 1000} × chaque seed ∈ {12345, 999, 7} (config) exécute
    /// <c>ticks</c> (défaut 300) et mesure le débit (t/s), les budgets par
    /// sous-système et le checksum d'état bit-à-bit. Rien de parallèle — la
    /// trajectoire reste déterministe.
    /// </summary>
    private static void RunBenchmark(SimulationOptions options, CliOptions cli)
    {
        ulong[] populations = cli.BenchmarkPopulations is { Length: > 0 } raw
            ? raw.Split(',').Select(ulong.Parse).ToArray()
            : [50, 500, 1000];
        int ticks = cli.BenchmarkTicks ?? 300;
        ulong[] seeds = cli.Seed is { } one
            ? [one]
            : [12_345, 999, 7];

        System.Console.WriteLine($"SYNE — benchmark PERFORMANCE.md §7 ({populations.Length} populations × {seeds.Length} seeds × {ticks} ticks, monde {options.Simulation.WorldWidth}×{options.Simulation.WorldHeight})");
        System.Console.WriteLine("Population | seed  | ticks | t/s    | tick moyen | part computation | perc. | mémo. | bes.  | déc.  | comm. | act.  | évén.");

        for (int p = 0; p < populations.Length; p++)
        {
            ulong population = populations[p];
            for (int s = 0; s < seeds.Length; s++)
            {
                ulong seed = seeds[s];
                var budgets = TickBudgetCollector.CreateEnabled();
                (_, Simulation.Core.World.IWorld world, SimulationLoop loop, _) = BuildSimulation(options, population, seed, budgets);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                loop.Run(ticks);
                sw.Stop();

                TickBudgetSnapshot snap = budgets.Snapshot();
                double ticksPerSecond = ticks / (sw.Elapsed.TotalMilliseconds / 1000.0);
                System.Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{population,-10} | {seed,-5} | {ticks,-5} | {ticksPerSecond,6:F1} | {snap.MeanPipelineMs,10:F2} ms | {snap.ComputationShare() * 100,10:F1} % | {snap.MeanMs(TickPhase.Perception),6:F2} | {snap.MeanMs(TickPhase.MemoryBeliefs),5:F2} | {snap.MeanMs(TickPhase.NeedsGoals),5:F2} | {snap.MeanMs(TickPhase.UtilityDecision),5:F2} | {snap.MeanMs(TickPhase.Communication),5:F2} | {snap.MeanMs(TickPhase.ActionsMovement),5:F2} | {snap.MeanMs(TickPhase.EventsGroupsPopulation),5:F2}"));
            }
        }

        System.Console.WriteLine("Checksum d'état (FNV-1a, id;x;y;énergie — reproductible à seed égale) :");
        foreach (ulong population in populations)
        {
            foreach (ulong seed in seeds)
            {
                (_, Simulation.Core.World.IWorld world, SimulationLoop loop, _) = BuildSimulation(options, population, seed);
                loop.Run(ticks);
                System.Console.WriteLine($"  N={population,-5} seed={seed,-5} → 0x{ChecksumState(loop, population, ticks):x16}");
            }
        }
    }

    /// <summary>
    /// FNV-1a canonique de l'état du monde : préfixe
    /// « elément=population;ticks » puis une ligne par entité vivante
    /// (id;x;y;énergie trié par id). Reprodéterminé bit-à-bit à seed égale.
    /// </summary>
    private static ulong ChecksumState(SimulationLoop loop, ulong population, int ticks)
    {
        var text = new StringBuilder();
        text.Append("population=").Append(population.ToString(CultureInfo.InvariantCulture));
        text.Append(";ticks=").Append(ticks.ToString(CultureInfo.InvariantCulture));
        text.AppendLine();
        foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            text.Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture));
            text.Append(';');
            text.Append(entity.Position.X.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(';');
            text.Append(entity.Position.Y.ToString("0.00", CultureInfo.InvariantCulture));
            text.Append(';');
            double energy = loop.Cognition.HasMind(entity.Id.Value)
                ? loop.Cognition.MindOf(entity.Id.Value).Needs.Energy
                : 0.0;
            text.Append(energy.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine();
        }

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte value in Encoding.UTF8.GetBytes(text.ToString()))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    /// <summary>
    /// Mode --observe (SYNE-080) : boucle pilotée par l'émetteur, chaque tick
    /// diffusé en WebSocket (snapshot + événements) puis résumé final.
    /// </summary>
    private static async Task RunObservedAsync(SimulationOptions options, CliOptions cli)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        (_, var world, SimulationLoop loop, EntityTemplate template) = BuildSimulation(options);
        int initialEntityCount = world.Entities.Count;

        int port = cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort;
        await using var server = new Observability.ObservabilityServer(port);
        server.Start();
        var emitter = new Observability.ObservabilityTickEmitter(loop, options.Random.Seed, server);
        await emitter.RunAsync(options.Simulation.MaxTicks);

        sw.Stop();

        if (cli.Headless != true)
        {
            PrintSummary(options, loop, template, sw.ElapsedMilliseconds, initialEntityCount);
            System.Console.WriteLine($"  observabilité : ws://127.0.0.1:{server.Port}/ | {emitter.TicksEmitted} ticks diffusés | {server.ClientCount} client(s)");
        }
    }

    /// <summary>
    /// Mode --serve (SYNE-113) : expose l'API HTTP REST de contrôle sur
    /// 127.0.0.1:[port] (défaut 5181). Le process
    /// reste en vie jusqu'à Ctrl+C ; aucun run n'est démarré automatiquement —
    /// il est lancé via POST /api/control/start.
    /// </summary>
    private static async Task RunServeAsync(SimulationOptions options, CliOptions cli)
    {
        int port = cli.ServePort ?? Control.ControlServer.DefaultPort;
        var observability = new Observability.ObservabilityServer(
            cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort);
        await using var server = new Control.ControlServer(port, observability: observability);
        server.Start();

        System.Console.WriteLine($"SYNE — serveur de contrôle HTTP :{server.Port}/ (API_CONTRACTS.md §3)");
        System.Console.WriteLine($"  observabilité WebSocket : ws://127.0.0.1:{observability.Port}/");
        System.Console.WriteLine($"  POST /api/control/start|pause|resume|stop|reset | GET /api/control/status");
        System.Console.WriteLine($"  seed par défaut : {options.Random.Seed} | population : {options.Agents.InitialCount}");
        System.Console.WriteLine("  Ctrl+C pour arrêter.");

        var exit = new TaskCompletionSource();
        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            exit.TrySetResult();
        };

        await exit.Task;
    }

    private static async Task<int> RunSupervisedAsync(SimulationOptions options, CliOptions cli)
    {
        if (!cli.AutoStart
            && (cli.Simulation is not null || cli.Ticks is not null || cli.ExportDirectory is not null
                || cli.Seed is not null || cli.MaxTicks is not null || cli.ConfigPath is not null
                || cli.RunId is not null || cli.ExportStream))
        {
            throw new ArgumentException("Les options de batch (--simulation, --seed, --ticks, --config, --export-dir, --run-id, --export-stream) exigent --autostart en mode supervisé.");
        }

        string sessionToken = Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN")
            ?? throw new ArgumentException("LIVEX_SESSION_TOKEN est obligatoire avec --control-port.");
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("LIVEX_SESSION_TOKEN ne peut pas être vide.");
        }

        string correlationId = cli.CorrelationId!;
        if (!string.Equals(Environment.GetEnvironmentVariable("LIVEX_CORRELATION_ID"), correlationId, StringComparison.Ordinal))
        {
            throw new ArgumentException("--correlation-id doit correspondre à LIVEX_CORRELATION_ID.");
        }

        string workDirectory = Path.GetFullPath(cli.WorkDirectory!);
        string logDirectory = Path.GetFullPath(cli.LogDirectory!);
        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(logDirectory);

        string? exportDirectory = null;
        if (cli.AutoStart)
        {
            exportDirectory = ResolveExportDirectory(cli.ExportDirectory, workDirectory);
            Directory.CreateDirectory(exportDirectory);
        }
        else if (cli.ExportDirectory is not null)
        {
            throw new ArgumentException("--export-dir exige --autostart en mode service.");
        }

        // Configuration **effectivement appliquée** (profil de scénario +
        // surcouche + flags CLI) : le contrôleur reçoit le résolu complet au
        // prepare — sinon le prepare du contrôleur mergerait la seule surcouche
        // sur le profil de référence par défaut, et perdrait le scénario
        // demandé.
        string configJson = ConfigLoader.ToJson(options);
        Observability.ObservabilityServer? observability = cli.AutoStart && cli.ObservePort is null
            ? null
            : new Observability.ObservabilityServer(cli.ObservePort ?? Observability.ObservabilityServer.DefaultPort);
        await using var streamFile = cli.ExportStream
            ? new BatchStreamFile(Path.Combine(exportDirectory!, BatchStreamFile.DefaultFileName))
            : null;
        Observability.IObservabilitySink? sink = (Observability.IObservabilitySink?)observability ?? streamFile;
        if (observability is not null && streamFile is not null)
        {
            sink = new Observability.CompositeObservabilitySink(observability, streamFile);
        }

        await using var server = new Control.ControlServer(
            cli.ControlPort!.Value,
            controller: sink is null ? null : new Control.SimulationController(sink),
            observability: observability,
            sessionToken: sessionToken,
            instanceId: cli.InstanceId!,
            version: ObservabilityContract.EngineVersion,
            initiallyReady: false);

        if (cli.AutoStart)
        {
            // Listener (contrôle + observabilité) ouvert AVANT la préparation :
            // la trame `world_initialized` n'est diffusée qu'une fois, et un
            // consommateur live (analyse temps réel) doit pouvoir se connecter
            // avant elle — sinon le mode supervisé ne livre jamais la description
            // du monde à personne. Le contrôle reste fermé tant que MarkReady
            // n'a pas été appelé (initiallyReady = false).
            server.Start();
            var world = await server.Controller.PrepareAsync(options.Random.Seed, configJson);
            server.MarkReady();
            server.Controller.AcknowledgeReady(world.Version);
            await server.Controller.StartAsync(
                options.Random.Seed,
                configJson: null,
                cli.Ticks ?? cli.MaxTicks ?? options.Simulation.MaxTicks,
                requestedRunId: cli.RunId);
            await WriteComponentLogAsync(logDirectory, correlationId, "batch_started");
            System.Console.WriteLine($"SYNE — instance {cli.InstanceId} prête sur 127.0.0.1:{server.Port}; batch lancé.");

            Task finished = WaitUntilRunFinishedAsync(server.Controller);
            Task ended = await WaitForTerminationAsync(server.ShutdownRequested, finished);
            if (ended != finished)
            {
                await server.Controller.StopAsync();
                await WriteComponentLogAsync(logDirectory, correlationId, "batch_cancelled");
                return 130;
            }

            SimulationLoop loop = server.Controller.CurrentLoop
                ?? throw new InvalidOperationException("La boucle batch terminée est indisponible.");
            string resultPath = await BatchRunExporter.WriteResultAsync(
                loop,
                cli.Simulation ?? "reference",
                options.Random.Seed,
                (ulong)options.Agents.InitialCount,
                exportDirectory!,
                server.Controller.RunId,
                streamFile is null ? null : BatchStreamFile.DefaultFileName);
            await WriteComponentLogAsync(logDirectory, correlationId, "batch_completed");
            System.Console.WriteLine($"SYNE — batch terminé; résultat : {resultPath}");
            return 0;
        }

        server.Start();
        server.MarkReady();
        await WriteComponentLogAsync(logDirectory, correlationId, "service_ready");
        System.Console.WriteLine($"SYNE — instance {cli.InstanceId} prête sur 127.0.0.1:{server.Port}.");
        // Affichage **opt-in** du jeton de session, pour le développement : un
        // consommateur local (éditeur Unreal en PIE, script) a besoin de le
        // présenter sur les `POST /api/control/*`. Jamais affiché par défaut —
        // cette ligne finit dans stdout.log, archivé : en production le secret
        // ne doit pas y figurer (le Launcher le transmet par l'environnement).
        if (string.Equals(Environment.GetEnvironmentVariable("LIVEX_SESSION_TOKEN_DISPLAY"), "1", StringComparison.Ordinal))
        {
            System.Console.WriteLine($"  jeton de session (dev) : {sessionToken}");
        }

        await WaitForTerminationAsync(server.ShutdownRequested);
        await WriteComponentLogAsync(logDirectory, correlationId, "service_stopping");
        return 0;
    }

    private static string ResolveExportDirectory(string? configuredDirectory, string workDirectory)
    {
        string configured = configuredDirectory ?? Path.Combine(workDirectory, "data");
        string exportDirectory = Path.GetFullPath(configured);
        string relative = Path.GetRelativePath(workDirectory, exportDirectory);
        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("--export-dir doit être situé dans --work-dir.");
        }

        return exportDirectory;
    }

    private static async Task WriteComponentLogAsync(string logDirectory, string correlationId, string eventName)
    {
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O} correlationId={correlationId} event={eventName}{Environment.NewLine}");
        await File.AppendAllTextAsync(Path.Combine(logDirectory, "syne.log"), line);
    }

    private static async Task WaitUntilRunFinishedAsync(SimulationController controller)
    {
        while (controller.State != SimulationControlState.Finished)
        {
            await Task.Delay(20);
        }
    }

    private static async Task<Task> WaitForTerminationAsync(params Task[] tasks)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            signal.TrySetResult();
        };
        System.Console.CancelKeyPress += handler;
        try
        {
            return await Task.WhenAny(tasks.Append(signal.Task));
        }
        finally
        {
            System.Console.CancelKeyPress -= handler;
        }
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.IWorld World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(SimulationOptions options)
    {
        return BuildSimulation(options, (ulong)options.Agents.InitialCount, options.Random.Seed, budgets: null);
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.IWorld World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(
        SimulationOptions options,
        ulong population,
        ulong seed)
    {
        return BuildSimulation(options, population, seed, budgets: null);
    }

    private static (Xoshiro256StarStar Rng, Simulation.Core.World.IWorld World, SimulationLoop Loop, EntityTemplate Template) BuildSimulation(
        SimulationOptions options,
        ulong population,
        ulong seed,
        TickBudgetCollector? budgets)
    {
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        Simulation.Core.World.IWorld world;

        EntityTemplate template = EntityTemplate.DefaultA;
        if (options.World.ExternalWorld)
        {
            // SYNE-220 étape 2 : bascule par profil — SYNE ne génère plus de
            // monde local, il s'appuie sur l'adaptateur externe (PRISM).
            world = new Simulation.Core.World.PrismWorldAdapter(
                new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight),
                options.Agents.Perception.SpatialCellSize);

            for (ulong i = 0; i < population; i++)
            {
                (Simulation.Core.Entities.Entity entity, Xoshiro256StarStar next) =
                    Simulation.Core.Entities.EntityFactory.CreateNext(template, world, rng, i + 1, bornAt: 0);
                rng = next;
                world.AddEntity(entity);
            }
        }
        else
        {
            var localWorld = new Simulation.Core.World.World(
                new Simulation.Core.World.WorldSize(options.Simulation.WorldWidth, options.Simulation.WorldHeight));

            // Même peuplement que le serveur de contrôle (layout d'obstacles posé
            // avant les entités, aucune entité dans une case bloquée) : une seule
            // implémentation, pour que le run CLI et le run supervisé ne puissent pas
            // diverger sur le monde initial.
            rng = Simulation.Console.Control.SimulationFactory.Populate(localWorld, options, population, rng);
            world = localWorld;
        }

        var loop = new SimulationLoop(world, rng, options, budgets);
        return (rng, world, loop, template);
    }

    private static void PrintSummary(
        SimulationOptions options,
        SimulationLoop loop,
        EntityTemplate template,
        long elapsedMs,
        int initialEntityCount)
    {
        var summary = new System.Text.StringBuilder();
        var clock = Simulation.Core.Configuration.SimulationClock.From(options.Simulation);
        summary.AppendLine($"SYNE — boucle minimale ({template.Species}) :");
        summary.AppendLine($"  monde : {options.Simulation.WorldWidth} x {options.Simulation.WorldHeight}");
        summary.AppendLine($"  PRNG : {options.Random.Engine}, seed : {options.Random.Seed}");

        if (loop.CurrentTick > 0)
        {
            // La ligne « (tête) » décrit le tick 1 : les entités ne meurent qu'en
            // cours de run, son compte est donc le compte initial capturé avant la
            // boucle — pas le compteur final, qui faisait lire « tick 1 … 42 entités »
            // après une extinction.
            var head = BuildTickLine(1UL, initialEntityCount, clock);
            var tail = BuildTickLine(loop.CurrentTick, loop.World.Entities.Count, clock);
            summary.AppendLine("(tête) " + head);
            summary.AppendLine("  …    …");
            summary.AppendLine("(queue) " + tail);
        }

        summary.AppendLine($"  exécuté en {elapsedMs} ms | 1 tick = {clock.SimulatedSecondsPerTick} s simulées");
        System.Console.WriteLine(summary.ToString());
    }

    private static string BuildTickLine(ulong tick, int entityCount, Simulation.Core.Configuration.SimulationClock clock)
    {
        return $"tick {tick:D8}  {clock.FormatClock(tick)}  entités : {entityCount}";
    }
}
