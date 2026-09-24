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

/// <summary>
/// Photographie du monde à un tick (API_CONTRACTS.md §2.1 — WorldSnapshot).
/// Représentation pure, sérialisée en camelCase par <see cref="ObservabilitySerializer"/>.
/// </summary>
public sealed record WorldSnapshot(
    string Version,
    string RunId,
    ulong Tick,
    long SimulatedTimeMinutes,
    int AliveCount,
    IReadOnlyList<AgentSnapshot> Agents,
    IReadOnlyList<ResourceSnapshot> Resources,
    IReadOnlyList<GroupSnapshot> Groups,
    IReadOnlyList<ObstacleSnapshot> Obstacles,
    string Season,
    int SeasonIndex,
    IReadOnlyList<TerritorySnapshot> Territories,
    IReadOnlyList<BookSnapshot> Books)
{
    /// <summary>Capte l'état du monde + cognition + réserves après un tick (pipeline BDI exécuté).</summary>
    public static WorldSnapshot Capture(SimulationLoop loop, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var entities = loop.World.Entities.OrderBy(entity => entity.Id.Value).ToList();
        var agents = new List<AgentSnapshot>(entities.Count);
        foreach (Entity entity in entities)
        {
            if (loop.Cognition.HasMind(entity.Id.Value))
            {
                agents.Add(AgentSnapshot.From(entity, loop.Cognition.MindOf(entity.Id.Value), loop.CurrentTick));
            }
        }

        var resources = new List<ResourceSnapshot>(3);
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
            ObservabilityContract.RunIdFor(seed),
            loop.CurrentTick,
            SimulationTime.ToSimulatedMinutes(loop.CurrentTick),
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
                : []);
    }
}