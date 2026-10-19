// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Snapshot bit-à-bit de l'état de simulation à un instant T (SYNE-111) : ne capture que les états mutables
//        durables — le reste (grille spatiale, cache de chemins, buffers par-tick) est reconstruit
//        déterministiquement à la restauration.

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
/// ne capture que les états **mutables durables** — le reste
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
    ulong NextGroupId,
    // Identité du run (réalignement Phase 4, D-17) : hash canonique de la
    // configuration effective au moment de la capture. Absent des snapshots
    // antérieurs — une absence déclarée, jamais un zéro de remplissage.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ulong? ConfigHash = null);

/// <summary>État d'un PRNG xoshiro256** (4 × ulong).</summary>
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
    IReadOnlyList<TerritorySnapshotDto>? Territories = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ResourceNodeSnapshotDto>? ResourceNodes = null);

/// <summary>
/// Gisement localisé persisté (Phase 0) : identité, type, position, quantité
/// courante, capacité et régénération. Sans ce bloc, une reprise perdait toutes
/// les sources spatiales — le monde restauré repartait des emplacements initiaux
/// alors que la trajectoire, elle, continuait sur les gisements entamés.
/// </summary>
public sealed record ResourceNodeSnapshotDto(
    string Id,
    int Kind,
    double X,
    double Y,
    double Amount,
    double Capacity,
    double RegenerationRate);

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

/// <summary>État cognitif complet d'une entité au tick T (table 4).
/// Les champs inventory/commitments/salience sont additifs (SchemaVersion 4 conservé,
/// hash inchangé quand désactivés — conditions WhenWritingDefault, jalon 0.14.0).</summary>
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
    int? LastDecisionKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InventorySnapshotDto? Inventory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CommitmentSnapshotDto>? Commitments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    ulong LastDeliberationTick = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<int>? TriggeredAtLastDeliberation = null,
    // Trace décisionnelle complète (D-18) : sans elle, la trajectoire restait
    // exacte après reprise mais l'explication repartait d'un score nul et d'une
    // cible absente. Additif : absent si aucune décision.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DecisionSnapshotDto? Decision = null);

/// <summary>
/// Trace décisionnelle persistée : état (nouvelle / maintenue / interruptive /
/// absente), action retenue, utilité, cible crue avec âge et confiance, candidats
/// rejetés, scores décomposés, faits justificatifs et résultat d'exécution.
/// </summary>
public sealed record DecisionSnapshotDto(
    ulong Tick,
    int State,
    bool Missing,
    int Chosen,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Utility,
    IReadOnlyList<UtilityScoreSnapshotDto> Scores,
    string? TargetType = null,
    string? TargetLabel = null,
    string? TargetPosition = null,
    ulong TargetObservedTick = 0,
    double TargetConfidence = 0.0,
    IReadOnlyList<int>? Rejected = null,
    IReadOnlyList<DecisionEvidenceSnapshotDto>? Evidence = null,
    string? ResultOutcome = null,
    string? ResultReason = null);

/// <summary>Score d'utilité persisté (composantes de la formule, réalignment S-04).</summary>
public sealed record UtilityScoreSnapshotDto(
    int Kind,
    double Benefit,
    double Cost,
    double Risk,
    double Confidence,
    double PersonalityModifier,
    double Urgency,
    double Utility,
    double SuccessRate);

/// <summary>Fait justificatif persisté (provenance, sujet, confiance, âge).</summary>
public sealed record DecisionEvidenceSnapshotDto(
    string Source,
    string Subject,
    double Confidence,
    ulong ObservedTick);

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

/// <summary>
/// Objectif persisté : type, tick de naissance <b>et cible connue</b>
/// (type, libellé, position observée, âge, confiance). Sans la cible, une
/// reprise repartait avec une intention <i>sans destination</i> : le déplacement
/// du tick suivant divergeait du run continu (le but n'est plus le même état
/// visé). C'est la moitié « objectif ← croyance » de la continuité de preuve.
/// </summary>
public sealed record GoalSnapshotDto(
    int Kind,
    ulong BornTick,
    string? TargetType = null,
    string? TargetLabel = null,
    string? TargetPosition = null,
    ulong TargetObservedTick = 0,
    double TargetConfidence = 0.0);

/// <summary>Groupe émergent actif au tick T (table 7-8).</summary>
public sealed record GroupSnapshotDto(
    ulong Id,
    ulong BornTick,
    IReadOnlyList<ulong> Members,
    double MeanCohesion,
    ulong? LeaderId,
    int? Decision,
    double Consensus,
    bool HadDecision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? MaxBondDistance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? MeanBondDistance = null);
