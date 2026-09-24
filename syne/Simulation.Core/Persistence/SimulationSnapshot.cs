using System.Text.Json;
using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Core.Persistence;

/// <summary>
/// Snapshot bit-à-bit de l'état de simulation à un instant T (SYNE-111,
/// PERSISTENCE.md §4) : ne capture que les états **mutables durables** — le reste
/// (grille spatiale, cache de chemins, buffers par-tick) est reconstruit
/// déterministiquement à la restauration.
/// </summary>
public sealed record SimulationSnapshot(
    int SchemaVersion,
    string EngineVersion,
    ulong Tick,
    RngStateDto Rng,
    WorldSnapshotDto World,
    IReadOnlyList<MindSnapshotDto> Minds,
    IReadOnlyList<GroupSnapshotDto> Groups,
    ulong NextGroupId);

/// <summary>État d'un PRNG xoshiro256** (4 × ulong, PERSISTENCE.md §4).</summary>
public sealed record RngStateDto(ulong S0, ulong S1, ulong S2, ulong S3);

/// <summary>État du monde au tick T : dimensions, obstacles, entités, réserves.</summary>
public sealed record WorldSnapshotDto(
    double Width,
    double Height,
    double CellSize,
    IReadOnlyList<EntitySnapshotDto> Entities,
    IReadOnlyList<ObstacleSnapshotDto> Obstacles,
    double FoodStock,
    double WaterStock,
    double WoodStock,
    double MineralStock);

public sealed record EntitySnapshotDto(
    ulong Id,
    string Species,
    string? Name,
    double X,
    double Y,
    IReadOnlyList<KeyValuePair<string, double>> Traits,
    ulong BornAt);

public sealed record ObstacleSnapshotDto(string Id, double X, double Y, double Radius);

/// <summary>État cognitif complet d'une entité au tick T (PERSISTENCE.md §3 table 4).</summary>
public sealed record MindSnapshotDto(
    ulong EntityId,
    NeedsSnapshotDto Needs,
    MemorySnapshotDto Memory,
    IReadOnlyList<BeliefSnapshotDto> Beliefs,
    IReadOnlyList<TrustSnapshotDto> Trust,
    IReadOnlyList<MessageSnapshotDto> OutgoingMessages,
    IReadOnlyList<ulong> RelayedMessageIds,
    IReadOnlyDictionary<int, double> SuccessRates,
    GoalSnapshotDto? Intention,
    GroupObjectiveSnapshotDto? CollectiveObjective,
    int? LastDecisionKind);

public sealed record NeedsSnapshotDto(
    double Hunger,
    double Thirst,
    double Fatigue,
    double Safety,
    double Social,
    double Curiosity,
    double Energy);

public sealed record MemorySnapshotDto(ulong NextSequence, IReadOnlyList<MemoryEntrySnapshotDto> Entries);

public sealed record MemoryEntrySnapshotDto(
    ulong Sequence,
    int Category,
    string Source,
    string Content,
    double Confidence,
    ulong StoredAt);

public sealed record BeliefSnapshotDto(
    string Subject,
    string Predicate,
    string Value,
    double Confidence,
    string Source,
    ulong BornTick,
    ulong UpdatedTick,
    ulong ExpiryTick);

public sealed record TrustSnapshotDto(ulong PeerId, double Trust);

public sealed record MessageSnapshotDto(
    ulong MessageId,
    ulong SenderId,
    ulong? TargetId,
    int Type,
    string Payload,
    double Confidence,
    int Hops,
    ulong Tick);

public sealed record GoalSnapshotDto(int Kind, ulong BornTick);
public sealed record GroupObjectiveSnapshotDto(ulong GroupId, int Kind, double Consensus, double LeaderTrust, ulong AdoptedTick, ulong ExpiresTick);

/// <summary>Groupe émergent actif au tick T (PERSISTENCE.md §3 table 7-8).</summary>
public sealed record GroupSnapshotDto(
    ulong Id,
    ulong BornTick,
    IReadOnlyList<ulong> Members,
    double MeanCohesion,
    ulong? LeaderId,
    int? Decision,
    double Consensus,
    bool HadDecision);