using System.Net.WebSockets;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Population;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Console.Observability;

/// <summary>
/// Implémentée par les sinks capables d'indiquer si quelqu'un écoute. Une cible
/// qui ne l'implémente pas est traitée comme ayant toujours des abonnés, pour
/// ne jamais perdre une trame d'un sink de test existant.
/// </summary>
public interface IObservabilityDemand
{
    /// <summary>Vrai s'il existe au moins un consommateur des trames.</summary>
    bool HasSubscribers { get; }
}

/// <summary>
/// Boucle de simulation « observée » : avance d'un tick (même contrat que
/// <see cref="SimulationLoop.Run"/>), puis diffuse le snapshot + événements du
/// tick sur le serveur WebSocket. Ne tire aucun tirage PRNG supplémentaire
/// (déterminisme préservé, DETERMINISM.md §3).
/// </summary>
public sealed class ObservabilityTickEmitter
{
    /// <summary>
    /// Plafond d'attente d'une diffusion. L'émetteur ne fait pas confiance au
    /// sink pour être borné : un client lent ou un sink bloqué ne doit jamais
    /// figer la boucle de simulation (DETERMINISM.md §3).
    /// </summary>
    private static readonly TimeSpan SinkTimeout = TimeSpan.FromSeconds(5);

    private readonly SimulationLoop _loop;
    private readonly ulong _seed;
    private readonly string _runId;
    private readonly IObservabilitySink _sink;

    public ObservabilityTickEmitter(
        SimulationLoop loop,
        ulong seed,
        IObservabilitySink sink,
        string? runId = null)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(sink);
        _loop = loop;
        _seed = seed;
        _runId = string.IsNullOrWhiteSpace(runId)
            ? ObservabilityContract.RunIdFor(seed)
            : runId;
        _sink = sink;
    }

    public ulong TicksEmitted { get; private set; }

    /// <summary>Exécute la boucle jusqu'au tick n° <paramref name="maxTicks"/> en diffusant chaque tick.</summary>
    public async Task RunAsync(int maxTicks)
    {
        if (maxTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks), "maxTicks doit être &gt; 0.");
        }

        while (_loop.CurrentTick < (ulong)maxTicks)
        {
            _loop.AdvanceOneTick();
            await EmitCurrentTickAsync();
        }
    }

    /// <summary>
    /// Diffuse une trame sans jamais bloquer ni lever : l'observabilité est
    /// best-effort, l'échec d'un client ne doit pas remonter au run.
    /// </summary>
    private async Task SafeBroadcastAsync(string frame)
    {
        try
        {
            await _sink.BroadcastAsync(frame).WaitAsync(SinkTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException
            or WebSocketException or InvalidOperationException or ObjectDisposedException)
        {
            // Client mort, lent ou fermé : la trame est perdue (V0.1),
            // la simulation continue.
        }
    }

    /// <summary>Vrai si le sink peut diffuser à quelqu'un (cf. <see cref="IObservabilityDemand"/>).</summary>
    private bool HasSubscribers => _sink is not IObservabilityDemand demand || demand.HasSubscribers;

    /// <summary>
    /// Vide les tampons d'événements du tick. La couche d'observabilité en est
    /// le seul consommateur (et le seul appelant de ces <c>Clear*</c>) : sans ce
    /// vidage, un tick non diffusé ferait croître les tampons sans borne et
    /// rejouerait des événements obsolètes à la diffusion suivante.
    /// </summary>
    private void DrainTickBuffers()
    {
        _loop.World.ClearEnvironmentChanges();
        _loop.ClearSeasonChanges();
        _loop.ClearTerritoryChanges();
        _loop.ClearBookChanges();
    }

    /// <summary>Diffuse le snapshot puis les événements du tick courant (API_CONTRACTS.md §2).</summary>
    public async Task EmitCurrentTickAsync()
    {
        if (!HasSubscribers)
        {
            // Aucun client connecté : la capture du snapshot coûte O(entités) et
            // part dans le vide. On saute ce travail mais on vide les tampons.
            DrainTickBuffers();
            return;
        }

        IReadOnlyList<EnvironmentChange> environmentChanges = _loop.World.LastEnvironmentChanges;
        IReadOnlyList<Simulation.Core.Configuration.SeasonChange> seasonChanges = _loop.LastSeasonChanges;
        IReadOnlyList<Simulation.Core.World.TerritoryMembershipChange> territoryChanges = _loop.LastTerritoryChanges;
        IReadOnlyList<Simulation.Core.World.BookChange> bookChanges = _loop.LastBookChanges;

        WorldSnapshot snapshot = WorldSnapshot.Capture(_loop, _seed, _runId);
        await SafeBroadcastAsync(
            ObservabilitySerializer.ToJsonText(ObservabilitySerializer.SnapshotMessage(snapshot)));

        await SafeBroadcastAsync(
            ObservabilitySerializer.ToJsonText(
                ObservabilitySerializer.EventMessage(EventSensor.TickSummary(_loop.CurrentTick, snapshot.AliveCount))));

        if (environmentChanges.Count > 0)
        {
            await SafeBroadcastAsync(ObservabilitySerializer.ToJsonText(
                ObservabilitySerializer.WorldDeltaMessage(
                    _loop.CurrentTick, _runId, environmentChanges)));
        }

        foreach (Simulation.Core.Entities.Entity entity in _loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            if (_loop.Cognition.HasMind(entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = _loop.Cognition.MindOf(entity.Id.Value);
                await SafeBroadcastAsync(
                    ObservabilitySerializer.ToJsonText(
                        ObservabilitySerializer.EventMessage(EventSensor.DecisionMade(_loop.CurrentTick, entity.Id.Value, mind))));

                if (mind.LastActionResult is { } actionResult)
                {
                    await SafeBroadcastAsync(
                        ObservabilitySerializer.ToJsonText(
                            ObservabilitySerializer.EventMessage(EventSensor.ActionCompleted(_loop.CurrentTick, entity.Id.Value, actionResult))));
                }
            }
        }

        foreach (Simulation.Core.Communication.MessageSent sent in _loop.Cognition.Communication.LastSent)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageSent(_loop.CurrentTick, sent))));
        }

        foreach (Simulation.Core.Communication.MessageReceived received in _loop.Cognition.Communication.LastReceived)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageReceived(_loop.CurrentTick, received))));
        }

        foreach (GroupFormation formed in _loop.Cognition.Groups.LastFormed)
        {
            // Un groupe peut se former puis se dissoudre dans le même tick : il
            // figure alors dans LastFormed mais plus dans Active. GroupOf renvoyait
            // une exception, ce qui interrompait la boucle de simulation.
            Group? group = GroupOf(formed.GroupId);
            if (group is null)
            {
                continue;
            }

            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupFormed(_loop.CurrentTick, group))));
        }

        foreach (GroupDissolution dissolved in _loop.Cognition.Groups.LastDissolved)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupDissolved(_loop.CurrentTick, dissolved))));
        }

        foreach (GroupDecision decision in _loop.Cognition.Groups.LastDecisions)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupDecision(_loop.CurrentTick, decision))));
        }

        foreach (BirthObservation birth in _loop.Cognition.Birth.LastBirths)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.AgentSpawned(_loop.CurrentTick, birth))));
        }

        foreach (DeathObservation death in _loop.Cognition.Death.LastDeaths)
        {
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.AgentDied(_loop.CurrentTick, death))));
        }

        foreach (EnvironmentChange change in _loop.World.LastEnvironmentChanges)
        {
            ExternalEvent environmentEvent = change.Kind == EnvironmentChangeKind.Added
                ? EventSensor.ConstructionPlaced(_loop.CurrentTick, change.Obstacle)
                : EventSensor.ConstructionRemoved(_loop.CurrentTick, change.Obstacle);
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(ObservabilitySerializer.EventMessage(environmentEvent)));
        }

        foreach (Simulation.Core.Configuration.SeasonChange change in seasonChanges)
        {
            ExternalEvent seasonEvent = EventSensor.SeasonChanged(_loop.CurrentTick, change);
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(ObservabilitySerializer.EventMessage(seasonEvent)));
        }

        foreach (Simulation.Core.World.TerritoryMembershipChange change in territoryChanges)
        {
            ExternalEvent membershipEvent = EventSensor.TerritoryMembershipChanged(_loop.CurrentTick, change);
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(ObservabilitySerializer.EventMessage(membershipEvent)));
        }

        foreach (Simulation.Core.World.BookChange change in bookChanges)
        {
            ExternalEvent bookEvent = change.Kind == Simulation.Core.World.BookChangeKind.Written
                ? EventSensor.BookWritten(_loop.CurrentTick, change)
                : EventSensor.BookRead(_loop.CurrentTick, change);
            await SafeBroadcastAsync(
                ObservabilitySerializer.ToJsonText(ObservabilitySerializer.EventMessage(bookEvent)));
        }

        DrainTickBuffers();
        TicksEmitted++;
    }

    /// <summary>
    /// Résout le groupe vivant d'un événement, ou <c>null</c> s'il n'est plus
    /// actif (dissolution dans le même tick).
    /// </summary>
    private Group? GroupOf(ulong groupId)
    {
        foreach (Group group in _loop.Cognition.Groups.Active)
        {
            if (group.Id == groupId)
            {
                return group;
            }
        }

        return null;
    }
}