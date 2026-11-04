using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class AgentPerceptionTickPhase(
    AgentLifecycleSystem? lifecycle,
    StructureConstructionSystem? construction = null) : IWorldTickPhaseHandler
{
    private readonly StructureEffectConsumer? _structureEffects =
        construction is null ? null : new StructureEffectConsumer(construction);

    public WorldTickPhase Phase => WorldTickPhase.Perception;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (lifecycle is null)
        {
            return;
        }

        foreach (var observation in lifecycle.ObserveAgents(construction?.Sites, _structureEffects))
        {
            context.AddObservation(new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                context.Tick,
                observation.AgentId,
                JsonSerializer.SerializeToElement(
                    new { changes = observation.Frame.Changes },
                    ProtocolJson.Options),
                JsonSerializer.SerializeToElement(observation.Frame.Self, ProtocolJson.Options)));
        }
    }
}
