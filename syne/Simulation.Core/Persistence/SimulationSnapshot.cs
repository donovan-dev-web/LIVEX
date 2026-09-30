using System.Text.Json;
using System.Text.Json.Serialization;
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
    double MineralStock,
    IReadOnlyList<BookSnapshotDto>? Books = null,
    IReadOnlyList<TerritorySnapshotDto>? Territories = null);

/// <summary>
/// Zone de territoire et son appartenance effective au tick T (SYNE-073). Sans ce
/// DTO, une restauration perdait les zones <i>et</i> l'appartenance : le monde
/// restauré n'en avait aucune, et le premier tick émettait une rafale de faux
/// événements <c>world.territory_membership_changed</c>.
/// </summary>
public sealed record TerritorySnapshotDto(
    string Id,
    double CenterX,
    double CenterY,
    double Radius,
    IReadOnlyList<ulong> Members);

public sealed record EntitySnapshotDto(
    ulong Id,
    string Species,
    string? Name,
    double X,
    double Y,
    IReadOnlyList<KeyValuePair<string, double>> Traits,
    ulong BornAt);

public sealed record ObstacleSnapshotDto(string Id, double X, double Y, double Radius);
public sealed record BookSnapshotDto(string Id, ulong AuthorId, string Title, string Content, ulong WrittenTick, IReadOnlyList<ulong> Readers);

/// <summary>État cognitif complet d'une entité au tick T (PERSISTENCE.md §3 table 4).
/// Les champs inventory/commitments/salience sont additifs (SchemaVersion 4 conservé,
/// hash inchangé quand désactivés — conditions WhenWritingDefault, jalon ADR 0.14.0).</summary>
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
    int? LastDecisionKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InventorySnapshotDto? Inventory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CommitmentSnapshotDto>? Commitments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    ulong LastDeliberationTick = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<int>? TriggeredAtLastDeliberation = null);

/// <summary>Inventaire persisté (D8) — quantité par type (ordre stable du type).</summary>
public sealed record InventorySnapshotDto(IReadOnlyDictionary<int, double> Amounts);

/// <summary>Engagement persisté (D5) — statut résolu inclus.</summary>
public sealed record CommitmentSnapshotDto(
    ulong ToEntityId,
    string RequestType,
    ulong CreatedTick,
    ulong ExpiryTick,
    int Status,
    ulong ResolvedTick);

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