namespace Prism.Host.Ticking;

public sealed class ApplyWorldWritesTickPhase : IWorldTickPhaseHandler
{
    public WorldTickPhase Phase => WorldTickPhase.ApplyWrites;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ApplyWorldWrites();
    }
}
