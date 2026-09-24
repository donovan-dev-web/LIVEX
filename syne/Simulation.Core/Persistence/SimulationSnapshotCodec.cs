using System.Text.Json;
using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Core.Persistence;

/// <summary>
/// Codec bit-à-bit des snapshots de simulation (SYNE-111) : capture l'état
/// mutable durable d'un <see cref="SimulationLoop"/> à l'instant T et le
/// restaure sans tirage du PRNG. La sérialisation JSON est déterministe
/// (System.Text.Json en indentation simple, ordre de propriétés stable).
/// </summary>
public static class SimulationSnapshotCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public const int SchemaVersion = 2;

    /// <summary>Capte l'état complet du monde + cognition à l'instant T.</summary>
    public static SimulationSnapshot Capture(SimulationLoop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);

        (ulong s0, ulong s1, ulong s2, ulong s3) = loop.Rng.State;

        var entities = new List<EntitySnapshotDto>();
        foreach (Entity entity in loop.World.Entities.OrderBy(static e => e.Id.Value))
        {
            entities.Add(new EntitySnapshotDto(
                entity.Id.Value,
                entity.Species,
                entity.Name,
                entity.Position.X,
                entity.Position.Y,
                entity.Traits.Values.ToList(),
                entity.BornAt));
        }

        var obstacles = new List<ObstacleSnapshotDto>();
        foreach (Obstacle obstacle in loop.World.Obstacles)
        {
            obstacles.Add(new ObstacleSnapshotDto(obstacle.Id, obstacle.Position.X, obstacle.Position.Y, obstacle.Radius));
        }

        var minds = new List<MindSnapshotDto>();
        foreach ((ulong entityId, MindState mind) in loop.Cognition.MindPairsSorted())
        {
            minds.Add(CaptureMind(entityId, mind));
        }

        var groups = new List<GroupSnapshotDto>();
        foreach (Group group in loop.Cognition.Groups.Active)
        {
            groups.Add(new GroupSnapshotDto(
                group.Id,
                group.BornTick,
                group.Members.ToList(),
                group.MeanCohesion,
                group.LeaderId,
                group.Decision is { } decision ? (int)decision : null,
                group.Consensus,
                group.HadDecision));
        }

        IReadOnlyDictionary<World.ResourceKind, double> stocks = loop.Resources.Snapshot();
        var world = new WorldSnapshotDto(
            loop.World.Size.Width,
            loop.World.Size.Height,
            loop.World.Grid.CellSize,
            entities,
            obstacles,
            stocks[World.ResourceKind.Food],
            stocks[World.ResourceKind.Water],
            stocks[World.ResourceKind.Wood],
            stocks[World.ResourceKind.Mineral]);

        return new SimulationSnapshot(
            SchemaVersion,
            ObservabilityContract.EngineVersion,
            loop.CurrentTick,
            new RngStateDto(s0, s1, s2, s3),
            world,
            minds,
            groups,
            loop.Cognition.Groups.NextGroupId);
    }

    private static MindSnapshotDto CaptureMind(ulong entityId, MindState mind)
    {
        var beliefs = new List<BeliefSnapshotDto>();
        foreach (Belief belief in mind.Beliefs.OrderedByFact())
        {
            beliefs.Add(new BeliefSnapshotDto(
                belief.Fact.Subject,
                belief.Fact.Predicate,
                belief.Fact.Value,
                belief.Confidence,
                belief.Source,
                belief.BornTick,
                belief.UpdatedTick,
                belief.ExpiryTick));
        }

        var trust = new List<TrustSnapshotDto>();
        foreach ((ulong peerId, double value) in mind.Trust.Snapshot())
        {
            trust.Add(new TrustSnapshotDto(peerId, value));
        }

        var outgoing = new List<MessageSnapshotDto>();
        foreach (Message message in mind.Communication.OutgoingSnapshot)
        {
            outgoing.Add(new MessageSnapshotDto(
                message.MessageId,
                message.SenderId,
                message.TargetId,
                (int)message.Type,
                message.Payload,
                message.Confidence,
                message.Hops,
                message.Tick));
        }

        var memory = new List<MemoryEntrySnapshotDto>();
        foreach (MemoryEntry entry in mind.Memory.AllEntries)
        {
            memory.Add(new MemoryEntrySnapshotDto(
                entry.Sequence,
                (int)entry.Category,
                entry.Source,
                entry.Content,
                entry.Confidence,
                entry.StoredAt));
        }

        var successRates = new Dictionary<int, double>();
        foreach ((DesireKind kind, double rate) in mind.SuccessRatesSnapshot())
        {
            successRates[(int)kind] = rate;
        }

        return new MindSnapshotDto(
            entityId,
            new NeedsSnapshotDto(
                mind.Needs.Hunger,
                mind.Needs.Thirst,
                mind.Needs.Fatigue,
                mind.Needs.Safety,
                mind.Needs.Social,
                mind.Needs.Curiosity,
                mind.Needs.Energy),
            new MemorySnapshotDto(mind.Memory.NextSequence, memory),
            beliefs,
            trust,
            outgoing,
            mind.Communication.RelayedIdsSnapshot.ToList(),
            successRates,
            mind.Intention is { } intention
                ? new GoalSnapshotDto((int)intention.Kind, intention.BornTick)
                : null,
            mind.CollectiveObjective is { } objective
                ? new GroupObjectiveSnapshotDto(
                    objective.GroupId,
                    (int)objective.Kind,
                    objective.Consensus,
                    objective.LeaderTrust,
                    objective.AdoptedTick,
                    objective.ExpiresTick)
                : null,
            mind.LastDecision is { } last ? (int)last.Kind : null);
    }

    /// <summary>Sérialise le snapshot en JSON déterministe (PERSISTENCE.md §3 : <c>tick_states.state</c>).</summary>
    public static string ToJson(SimulationSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static SimulationSnapshot FromJson(string json) =>
        JsonSerializer.Deserialize<SimulationSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Snapshot JSON invalide (null).");

    /// <summary>Code de hachage stable (baseline bit-à-bit, DETERMINISM.md §4).</summary>
    public static ulong Hash(SimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ulong hash = 14695981039346656037UL;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ToJson(snapshot));
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}