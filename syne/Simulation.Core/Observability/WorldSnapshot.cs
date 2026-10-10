using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.World;

namespace Simulation.Core.Observability;

/// <summary>État d'une réserve globale dans le snapshot (SYNE-042, API_CONTRACTS.md §2.1).</summary>
public sealed record ResourceSnapshot(string Type, double Quantity);

/// <summary>
/// Groupe émergent dans le snapshot (SYNE-060/061, API_CONTRACTS.md §2.1) :
/// identifiant, membres triés, leader et dernière décision collective.
/// </summary>
public sealed record GroupSnapshot(
    ulong GroupId,
    IReadOnlyList<ulong> Members,
    int Size,
    ulong? LeaderId,
    ulong BornTick,
    double Cohesion,
    string? Decision,
    double Consensus);

/// <summary>Obstacle statique (construction, SYNE-071, API_CONTRACTS.md §2.1).</summary>
public sealed record ObstacleSnapshot(string Id, double X, double Y, double Radius);

/// <summary>
/// Zone de territoire au snapshot (SYNE-073, décision n°21, API_CONTRACTS.md §2.1) :
/// disque « point de survie » + members courants (identifiants croissants,
/// <c>memberCount</c> = taille, redondante mais explicite à l'émission JSON).
/// </summary>
public sealed record TerritorySnapshot(
    string Id,
    double X,
    double Y,
    double Radius,
    int MemberCount,
    IReadOnlyList<ulong> Members);

/// <summary>
/// Livre au snapshot (SYNE-121, décisions n°18/19, Monographie §3.18) :
/// identifiant, auteur, titre, tick d'écriture (poinçonné par la boucle,
/// <c>writtenTick</c>) et liste des lecteurs distincts (ordre de première
/// consultation, <c>readCount</c> = taille redondante mais explicite).
/// Émise quand <c>world.books.enabled</c> est actif (désactivé par défaut).
/// </summary>
public sealed record BookSnapshot(
    string Id,
    ulong AuthorId,
    string Title,
    string Content,
    ulong WrittenTick,
    IReadOnlyList<ulong> Readers,
    int ReadCount);

public sealed record WorldChangeSnapshot(
    string Kind,
    string Id,
    double X,
    double Y,
    double Radius);

public sealed record ActionSnapshot(
    string AgentId,
    string Action,
    string Outcome,
    string? Cause,
    double EnergyDelta,
    double HungerDelta,
    double ThirstDelta,
    double FatigueDelta,
    string? Reserve,
    double? ReserveConsumed);

/// <summary>
/// Photographie du monde à un tick (API_CONTRACTS.md §2.1 — WorldSnapshot).
/// Représentation pure, sérialisée en camelCase par <see cref="ObservabilitySerializer"/>.
/// </summary>
public sealed record WorldSnapshot(
    string Version,
    string RunId,
    // Seed effectif du run — champ additif V0.2.1 du contrat (API_CONTRACTS.md §2.1) : ECHOS n'a
    // plus à dériver le seed du run_id, ce qui invalidait same_seed sur les runs pilotés.
    ulong Seed,
    ulong Tick,
    // Temps simulé en minutes (plancher entier, conserve le contrat historique
    // validé/stocké en INTEGER par ECHOS) et en secondes (champ additif ADR-017 —
    // à `simulatedSecondsPerTick = 60`, minutes = tick, comme avant).
    long SimulatedTimeMinutes,
    long SimulatedTimeSeconds,
    int AliveCount,
    IReadOnlyList<AgentSnapshot> Agents,
    IReadOnlyList<ResourceSnapshot> Resources,
    IReadOnlyList<GroupSnapshot> Groups,
    IReadOnlyList<ObstacleSnapshot> Obstacles,
    string Season,
    int SeasonIndex,
    IReadOnlyList<TerritorySnapshot> Territories,
    IReadOnlyList<BookSnapshot> Books,
    IReadOnlyList<WorldChangeSnapshot> WorldChanges,
    IReadOnlyList<ActionSnapshot> Actions)
{
    /// <summary>Capte l'état du monde + cognition + réserves après un tick (pipeline BDI exécuté).</summary>
    public static WorldSnapshot Capture(SimulationLoop loop, ulong seed, string? runId = null)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var entities = loop.World.Entities.OrderBy(entity => entity.Id.Value).ToList();
        var agents = new List<AgentSnapshot>(entities.Count);
        var actions = new List<ActionSnapshot>(entities.Count);
        foreach (Entity entity in entities)
        {
            if (loop.Cognition.HasMind(entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                agents.Add(AgentSnapshot.From(entity, mind, loop.CurrentTick));
                if (mind.LastActionResult is { } result)
                {
                    actions.Add(new ActionSnapshot(
                        entity.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        result.Kind.ToString(),
                        result.Outcome.ToString().ToLowerInvariant(),
                        result.Reason,
                        Math.Round(result.EnergyDelta, 4),
                        Math.Round(result.HungerDelta, 4),
                        Math.Round(result.ThirstDelta, 4),
                        Math.Round(result.FatigueDelta, 4),
                        result.ReserveConsumed?.ToString().ToLowerInvariant(),
                        result.ReserveConsumed is null ? null : Math.Round(result.ReserveConsumedAmount, 4)));
                }
            }
        }

        var resources = new List<ResourceSnapshot>(Enum.GetValues<World.ResourceKind>().Length);
        foreach (World.ResourceKind kind in Enum.GetValues<World.ResourceKind>())
        {
            resources.Add(new ResourceSnapshot(kind.ToString().ToLowerInvariant(), loop.Resources.Stock(kind)));
        }

        var groups = new List<GroupSnapshot>(loop.Cognition.Groups.Active.Count);
        foreach (Social.Group group in loop.Cognition.Groups.Active)
        {
            groups.Add(new GroupSnapshot(
                group.Id,
                group.Members,
                group.Members.Count,
                group.LeaderId,
                group.BornTick,
                Math.Round(group.MeanCohesion, 4),
                group.Decision?.ToString(),
                Math.Round(group.Consensus, 4)));
        }

        var obstacles = new List<ObstacleSnapshot>(loop.World.Obstacles.Count);
        foreach (World.Obstacle obstacle in loop.World.Obstacles)
        {
            obstacles.Add(new ObstacleSnapshot(
                obstacle.Id,
                Math.Round(obstacle.Position.X, 4),
                Math.Round(obstacle.Position.Y, 4),
                Math.Round(obstacle.Radius, 4)));
        }

        var territories = new List<TerritorySnapshot>();
        if (loop.TerritoriesEnabled)
        {
            foreach (World.Territory zone in loop.World.Territories)
            {
                IReadOnlyList<ulong> members = loop.MembersOfTerritory(zone.Id);
                territories.Add(new TerritorySnapshot(
                    zone.Id,
                    Math.Round(zone.Center.X, 4),
                    Math.Round(zone.Center.Y, 4),
                    Math.Round(zone.Radius, 4),
                    members.Count,
                    members));
            }
        }

        return new WorldSnapshot(
            ObservabilityContract.Version,
            runId ?? ObservabilityContract.RunIdFor(seed),
            seed,
            loop.CurrentTick,
            loop.Clock.ToSimulatedMinutes(loop.CurrentTick),
            loop.Clock.ToSimulatedSeconds(loop.CurrentTick),
            agents.Count,
            agents,
            resources,
            groups,
            obstacles,
            World.Seasons.Name(loop.CurrentSeason),
            (int)loop.CurrentSeason,
            territories,
            loop.BooksEnabled
                ? loop.World.Books.Select(book => new BookSnapshot(
                    book.Id, book.AuthorId, book.Title, book.Content, book.WrittenTick,
                    book.Readers.ToArray(), book.ReadCount)).ToArray()
                : [],
            loop.World.LastEnvironmentChanges.Select(change => new WorldChangeSnapshot(
                change.Kind == EnvironmentChangeKind.Added ? "added" : "removed",
                change.Obstacle.Id,
                Math.Round(change.Obstacle.Position.X, 4),
                Math.Round(change.Obstacle.Position.Y, 4),
                Math.Round(change.Obstacle.Radius, 4))).ToArray(),
            actions);
    }
}