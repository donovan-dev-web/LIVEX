namespace Prism.Contracts;

public interface IWorldHost
{
    TickResult Step(TickRequest request);
}

public interface ICheckpointableWorldHost : IWorldHost
{
    long LastCompletedTick { get; }
    byte[] CaptureCheckpoint();
    void RestoreCheckpoint(ReadOnlySpan<byte> checkpoint);
}
