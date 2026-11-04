namespace Prism.Host.Ticking;

public interface IWorldTickPhaseHandler
{
    WorldTickPhase Phase { get; }
    void Execute(WorldTickContext context);
}

public interface ICheckpointableWorldTickPhaseHandler : IWorldTickPhaseHandler
{
    System.Text.Json.JsonElement CaptureState();
    void RestoreState(System.Text.Json.JsonElement state);
}

public interface IOptionalCheckpointableWorldTickPhaseHandler : ICheckpointableWorldTickPhaseHandler
{
    void RestoreEmptyState();
}
