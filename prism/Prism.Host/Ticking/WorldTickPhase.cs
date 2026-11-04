namespace Prism.Host.Ticking;

public enum WorldTickPhase : byte
{
    Receive = 1,
    Lifecycle = 2,
    Arbitration = 3,
    StartOrCancelActions = 4,
    AdvanceMacroActions = 5,
    ApplyWrites = 6,
    ScheduledUpdates = 7,
    RandomUpdates = 8,
    WearExpiryAndConstruction = 9,
    Calendar = 10,
    Perception = 11,
    Output = 12,
}
