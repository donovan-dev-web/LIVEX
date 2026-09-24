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
    int SeasonIndex)
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
            (int)loop.CurrentSeason);
    }
}