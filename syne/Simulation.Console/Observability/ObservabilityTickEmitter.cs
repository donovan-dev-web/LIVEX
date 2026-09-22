using Simulation.Core.Loop;
using Simulation.Core.Observability;

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
    private readonly ObservabilityServer _server;

    public ObservabilityTickEmitter(SimulationLoop loop, ulong seed, ObservabilityServer server)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(server);
        _loop = loop;
        _seed = seed;
        _server = server;
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
        await _server.BroadcastAsync(
            ObservabilitySerializer.ToJsonText(ObservabilitySerializer.SnapshotMessage(snapshot)));

        await _server.BroadcastAsync(
            ObservabilitySerializer.ToJsonText(
                ObservabilitySerializer.EventMessage(EventSensor.TickSummary(_loop.CurrentTick, snapshot.AliveCount))));

        foreach (Simulation.Core.Entities.Entity entity in _loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            if (_loop.Cognition.HasMind(entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = _loop.Cognition.MindOf(entity.Id.Value);
                await _server.BroadcastAsync(
                    ObservabilitySerializer.ToJsonText(
                        ObservabilitySerializer.EventMessage(EventSensor.DecisionMade(_loop.CurrentTick, entity.Id.Value, mind))));

                if (mind.LastActionResult is { } actionResult)
                {
                    await _server.BroadcastAsync(
                        ObservabilitySerializer.ToJsonText(
                            ObservabilitySerializer.EventMessage(EventSensor.ActionCompleted(_loop.CurrentTick, entity.Id.Value, actionResult))));
                }
            }
        }

        foreach (Simulation.Core.Communication.MessageSent sent in _loop.Cognition.Communication.LastSent)
        {
            await _server.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageSent(_loop.CurrentTick, sent))));
        }

        foreach (Simulation.Core.Communication.MessageReceived received in _loop.Cognition.Communication.LastReceived)
        {
            await _server.BroadcastAsync(
                ObservabilitySerializer.ToJsonText(
                    ObservabilitySerializer.EventMessage(EventSensor.MessageReceived(_loop.CurrentTick, received))));
        }

        TicksEmitted++;
    }
}