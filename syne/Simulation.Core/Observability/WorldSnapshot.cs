using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.World;

namespace Simulation.Core.Observability;

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
    IReadOnlyList<AgentSnapshot> Agents)
{
    /// <summary>Capte l'état du monde + cognition après un tick (pipeline BDI exécuté).</summary>
    public static WorldSnapshot Capture(SimulationLoop loop, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var entities = loop.World.Entities.OrderBy(entity => entity.Id.Value).ToList();
        var agents = new List<AgentSnapshot>(entities.Count);
        foreach (Entity entity in entities)
        {
            if (loop.Cognition.HasMind(entity.Id.Value))
            {
                agents.Add(AgentSnapshot.From(entity, loop.Cognition.MindOf(entity.Id.Value)));
            }
        }

        return new WorldSnapshot(
            ObservabilityContract.Version,
            ObservabilityContract.RunIdFor(seed),
            loop.CurrentTick,
            SimulationTime.ToSimulatedMinutes(loop.CurrentTick),
            agents.Count,
            agents);
    }
}