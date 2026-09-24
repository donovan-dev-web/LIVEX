using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Population;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Console.Observability;

/// <summary>
/// Boucle de simulation « observée » : avance d'un tick (même contrat que
/// <see cref="SimulationLoop.Run"/>), puis diffuse le snapshot + événements du
/// tick sur le serveur WebSocket. Ne tire aucun tirage PRNG supplémentaire
/// (déterminisme préservé, DETERMINISM.md §3).
/// </summary>
public sealed class ObservabilityTickEmitter
{
    private readonly SimulationLoop _loop;
    private readonly ulong _seed;
    private readonly IObservabilitySink _sink;

    public ObservabilityTickEmitter(SimulationLoop loop, ulong seed, IObservabilitySink sink)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(sink);
        _loop = loop;
        _seed = seed;
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

    /// <summary>Diffuse le snapshot puis les événements du tick courant (API_CONTRACTS.md §2).</summary>
    public async Task EmitCurrentTickAsync()
    {
        WorldSnapshot snapshot = WorldSnapshot.Capture(_loop, _seed);
        await _sink.BroadcastAsync(
            ObservabilitySerializer.ToJsonText(ObservabilitySerializer.SnapshotMessage(snapshot)));

        await _sink.BroadcastAsync(
            ObservabilitySerializer.ToJsonText(
                ObservabilitySerializer.EventMessage(EventSensor.TickSummary(_loop.CurrentTick, snapshot.AliveCount))));

        foreach (Simulation.Core.Entities.Entity entity in _loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            if (_loop.Cognition.HasMind(entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = _loop.Cognition.MindOf(entity.Id.Value);
                await _sink.BroadcastAsync(
                    ObservabilitySerializer.ToJsonText(
                        ObservabilitySerializer.EventMessage(EventSensor.DecisionMade(_loop.CurrentTick, entity.Id.Value, mind))));

                if (mind.LastActionResult is { } actionResult)
                {
                    await _sink.BroadcastAsync(
                        ObservabilitySerializer.ToJsonText(
                            ObservabilitySerializer.EventMessage(EventSensor.ActionCompleted(_loop.CurrentTick, entity.Id.Value, actionResult))));
                }
            }
        }

        foreach (Simulation.Core.Communication.MessageSent sent in _loop.Cognition.Communication.LastSent)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageSent(_loop.CurrentTick, sent))));
        }

        foreach (Simulation.Core.Communication.MessageReceived received in _loop.Cognition.Communication.LastReceived)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageReceived(_loop.CurrentTick, received))));
        }

        foreach (GroupFormation formed in _loop.Cognition.Groups.LastFormed)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupFormed(_loop.CurrentTick, GroupOf(formed.GroupId)))));
        }

        foreach (GroupDissolution dissolved in _loop.Cognition.Groups.LastDissolved)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupDissolved(_loop.CurrentTick, dissolved))));
        }

        foreach (GroupDecision decision in _loop.Cognition.Groups.LastDecisions)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.GroupDecision(_loop.CurrentTick, decision))));
        }

        foreach (BirthObservation birth in _loop.Cognition.Birth.LastBirths)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.AgentSpawned(_loop.CurrentTick, birth))));
        }

        foreach (DeathObservation death in _loop.Cognition.Death.LastDeaths)
        {
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.AgentDied(_loop.CurrentTick, death))));
        }

        foreach (EnvironmentChange change in _loop.World.LastEnvironmentChanges)
        {
            ExternalEvent environmentEvent = change.Kind == EnvironmentChangeKind.Added
                ? EventSensor.ConstructionPlaced(_loop.CurrentTick, change.Obstacle)
                : EventSensor.ConstructionRemoved(_loop.CurrentTick, change.Obstacle);
            await _sink.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(ObservabilitySerializer.EventMessage(environmentEvent)));
        }

        _loop.World.ClearEnvironmentChanges();

        TicksEmitted++;
    }

    /// <summary>Résout le groupe vivant d'un événement (encore actif à la diffusion).</summary>
    private Group GroupOf(ulong groupId) =>
        _loop.Cognition.Groups.Active.First(group => group.Id == groupId);
}