namespace Prism.Host.Ticking;

public interface IWorldTickPhaseHandler
{
    WorldTickPhase Phase { get; }
    void Execute(WorldTickContext context);
}
