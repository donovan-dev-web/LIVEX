using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Accounting;
using Prism.Core.Actions;

namespace Prism.Host.Ticking;

public sealed class WorldActionStateTickPhase(
    MatterLedger ledger,
    CraftingStationRegistry stations,
    WorldContainerStore containers,
    MasterySystem? mastery = null) : IOptionalCheckpointableWorldTickPhaseHandler
{
    public WorldTickPhase Phase => WorldTickPhase.Arbitration;

    public void Execute(WorldTickContext context) => ArgumentNullException.ThrowIfNull(context);

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(
            new ActionStateCheckpoint(
                ledger.CaptureState(),
                stations.CaptureState(),
                containers.CaptureState(),
                mastery?.CaptureState()),
            ProtocolJson.Options);

    public void RestoreEmptyState()
    {
        if (!ledger.IsEmpty || !stations.IsEmpty || !containers.IsEmpty ||
            mastery is { IsEmpty: false })
        {
            throw new JsonException("Action state can only be initialized as empty in a new runtime.");
        }
    }

    public void RestoreState(JsonElement state)
    {
        var checkpoint = state.Deserialize<ActionStateCheckpoint>(ProtocolJson.Options)
                         ?? throw new JsonException("The action checkpoint is empty.");
        if (checkpoint.Ledger is null || checkpoint.Stations is null)
        {
            throw new JsonException("The action checkpoint is missing ledger or station state.");
        }

        ledger.RestoreState(checkpoint.Ledger);
        stations.RestoreState(checkpoint.Stations);
        if (checkpoint.Containers is not null)
        {
            containers.RestoreState(checkpoint.Containers);
        }

        if (checkpoint.Mastery is not null)
        {
            if (mastery is null)
            {
                throw new JsonException("The action checkpoint contains mastery state for a runtime without mastery.");
            }

            mastery.RestoreState(checkpoint.Mastery);
        }
    }

    private sealed record ActionStateCheckpoint(
        byte[] Ledger,
        byte[] Stations,
        byte[]? Containers = null,
        byte[]? Mastery = null);
}
