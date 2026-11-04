using Prism.Core.World;

namespace Prism.Core.Time;

public readonly record struct ScheduledWorldEvent(
    long DueTick,
    ulong Sequence,
    string Kind,
    BlockCoordinate Target,
    int State,
    long ContextTick = 0);
