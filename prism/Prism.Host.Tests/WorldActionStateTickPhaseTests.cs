using System.Text.Json;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

public sealed class WorldActionStateTickPhaseTests
{
    [Fact]
    public void CheckpointRestoresMasteryLevels()
    {
        var mastery = new MasterySystem(new MasteryRules(6, 40));
        mastery.Award(1, MasteryCategories.Mining);
        mastery.Award(1, MasteryCategories.Mining);
        mastery.Award(2, MasteryCategories.Farming);
        var phase = new WorldActionStateTickPhase(
            new MatterLedger(),
            new CraftingStationRegistry(),
            new WorldContainerStore(),
            mastery);

        var checkpoint = phase.CaptureState();

        var restoredMastery = new MasterySystem();
        var restored = new WorldActionStateTickPhase(
            new MatterLedger(),
            new CraftingStationRegistry(),
            new WorldContainerStore(),
            restoredMastery);
        restored.RestoreState(checkpoint);

        Assert.Equal(12, restoredMastery.GetPermille(1, MasteryCategories.Mining));
        Assert.Equal(6, restoredMastery.GetPermille(2, MasteryCategories.Farming));
        Assert.Equal(mastery.Snapshot(), restoredMastery.Snapshot());
    }

    [Fact]
    public void CheckpointWithMasteryRequiresAMasterySystem()
    {
        var mastery = new MasterySystem(new MasteryRules(5, 500));
        mastery.Award(1, MasteryCategories.Mining);
        var checkpoint = new WorldActionStateTickPhase(
            new MatterLedger(),
            new CraftingStationRegistry(),
            new WorldContainerStore(),
            mastery).CaptureState();

        var phase = new WorldActionStateTickPhase(
            new MatterLedger(),
            new CraftingStationRegistry(),
            new WorldContainerStore());

        Assert.Throws<JsonException>(() => phase.RestoreState(checkpoint));
    }
}
