using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using WorldNamespace = Simulation.Core.World;

namespace Simulation.Core.Persistence;

/// <summary>
/// Restauration bit-à-bit (SYNE-111/112) : reconstruit un <see cref="SimulationLoop"/>
/// depuis un <see cref="SimulationSnapshot"/> sans retirer de tirages au PRNG.
/// Les états dérivés (grille spatiale, cache de chemins, buffers par-tick) sont
/// reconstruits déterministiquement. La boucle reprend exactement au tick T.
/// </summary>
public static class SimulationSnapshotRestorer
{
    /// <summary>
    /// Restaure une boucle exécutable depuis le snapshot. L'appelant doit fournir
    /// les options de config du run (persistées par ailleurs : <c>runs.config</c>).
    /// </summary>
    public static SimulationLoop Restore(SimulationSnapshot snapshot, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        WorldNamespace.World world = RestoreWorld(snapshot.World);

        var rng = new Xoshiro256StarStar(
            snapshot.Rng.S0,
            snapshot.Rng.S1,
            snapshot.Rng.S2,
            snapshot.Rng.S3);

        var loop = new SimulationLoop(world, rng, options);
        loop.Resources.RestoreState(snapshot.World.FoodStock, snapshot.World.WaterStock, snapshot.World.WoodStock, snapshot.World.MineralStock);
        loop.RestoreState(snapshot.Tick, rng);

        RestoreCognition(loop, snapshot, options);
        return loop;
    }

    private static WorldNamespace.World RestoreWorld(WorldSnapshotDto dto)
    {
        var world = new WorldNamespace.World(new WorldNamespace.WorldSize((int)dto.Width, (int)dto.Height), dto.CellSize);

        foreach (ObstacleSnapshotDto obstacle in dto.Obstacles)
        {
            world.AddObstacle(new WorldNamespace.Obstacle(obstacle.Id, new WorldNamespace.Position(obstacle.X, obstacle.Y), obstacle.Radius));
        }

        foreach (BookSnapshotDto bookDto in dto.Books ?? [])
        {
            var book = new WorldNamespace.Book(bookDto.Id, bookDto.AuthorId, bookDto.Title, bookDto.Content)
            {
                WrittenTick = bookDto.WrittenTick,
            };
            book.RestoreReaders(bookDto.Readers);
            world.AddBook(book);
        }

        foreach (EntitySnapshotDto entity in dto.Entities)
        {
            var traits = new TraitSet(entity.Traits.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            var rebuilt = new Entity(
                new EntityId(entity.Id),
                entity.Species,
                entity.Name,
                new WorldNamespace.Position(entity.X, entity.Y),
                traits,
                entity.BornAt);
            world.AddEntity(rebuilt);
        }

        return world;
    }

    private static void RestoreCognition(SimulationLoop loop, SimulationSnapshot snapshot, SimulationOptions options)
    {
        var minds = new Dictionary<ulong, MindState>();
        foreach (MindSnapshotDto mindDto in snapshot.Minds)
        {
            minds[mindDto.EntityId] = RestoreMind(mindDto, options);
        }

        var groups = new List<Group>();
        foreach (GroupSnapshotDto group in snapshot.Groups)
        {
            var rebuilt = new Group(group.Id, group.BornTick, group.Members.ToList())
            {
                MeanCohesion = group.MeanCohesion,
                LeaderId = group.LeaderId,
                Decision = group.Decision is { } decision ? (DesireKind)decision : null,
                Consensus = group.Consensus,
                HadDecision = group.HadDecision,
            };
            groups.Add(rebuilt);
        }

        loop.Cognition.RestoreState(minds, groups, snapshot.NextGroupId);
    }

    private static MindState RestoreMind(MindSnapshotDto dto, SimulationOptions options)
    {
        var mind = new MindState(options)
        {
            Intention = dto.Intention is { } intention ? new Goal((DesireKind)intention.Kind, intention.BornTick) : null,
            CollectiveObjective = dto.CollectiveObjective is { } objective
                ? new GroupObjective(
                    objective.GroupId,
                    (DesireKind)objective.Kind,
                    objective.Consensus,
                    objective.LeaderTrust,
                    objective.AdoptedTick,
                    objective.ExpiresTick)
                : null,
        };

        var memory = new List<MemoryEntry>(dto.Memory.Entries.Count);
        foreach (MemoryEntrySnapshotDto entry in dto.Memory.Entries)
        {
            memory.Add(new MemoryEntry(
                entry.Sequence,
                (MemoryCategory)entry.Category,
                entry.Source,
                entry.Content,
                entry.Confidence,
                entry.StoredAt));
        }

        var beliefs = new List<Belief>();
        foreach (BeliefSnapshotDto belief in dto.Beliefs)
        {
            beliefs.Add(new Belief(
                new Fact(belief.Subject, belief.Predicate, belief.Value),
                belief.Confidence,
                belief.Source,
                belief.BornTick,
                belief.UpdatedTick,
                belief.ExpiryTick));
        }

        var trust = new List<(ulong PeerId, double Trust)>();
        foreach (TrustSnapshotDto relation in dto.Trust)
        {
            trust.Add((relation.PeerId, relation.Trust));
        }

        var outgoing = new List<Message>();
        foreach (MessageSnapshotDto message in dto.OutgoingMessages)
        {
            outgoing.Add(new Message(
                message.MessageId,
                message.SenderId,
                message.TargetId,
                (MessageType)message.Type,
                message.Payload,
                message.Confidence,
                message.Hops,
                message.Tick));
        }

        var successRates = new Dictionary<DesireKind, double>();
        foreach ((int kind, double rate) in dto.SuccessRates)
        {
            successRates[(DesireKind)kind] = rate;
        }

        var needs = BodyNeeds.FromState(
            dto.Needs.Hunger,
            dto.Needs.Thirst,
            dto.Needs.Fatigue,
            dto.Needs.Safety,
            dto.Needs.Social,
            dto.Needs.Curiosity,
            dto.Needs.Energy);

        var memoryState = new Memory(options.Agents.Memory);
        memoryState.RestoreState(memory, dto.Memory.NextSequence);
        var beliefsState = new BeliefSet();
        beliefsState.RestoreState(beliefs);
        var trustState = new Relationships(options.Agents.Trust);
        trustState.RestoreState(trust);
        var communicationState = new CommunicationState();
        communicationState.RestoreState(outgoing, dto.RelayedMessageIds);

        mind.RestoreState(
            needs,
            memoryState,
            beliefsState,
            trustState,
            communicationState,
            successRates,
            factors: null,
            mind.Intention,
            mind.CollectiveObjective);
        mind.RestoreLastDecision(dto.LastDecisionKind is { } lastKind ? (DesireKind)lastKind : null);

        return mind;
    }
}