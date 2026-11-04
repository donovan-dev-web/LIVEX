using Prism.Core.Actions;

namespace Prism.Core.Tests;

public sealed class MasterySystemTests
{
    [Fact]
    public void AwardIncreasesPermilleAndClampsToMaximum()
    {
        var mastery = new MasterySystem(new MasteryRules(5, 12));

        Assert.Equal(0, mastery.GetPermille(1, MasteryCategories.Mining));
        Assert.Equal(5, mastery.Award(1, MasteryCategories.Mining));
        Assert.Equal(10, mastery.Award(1, MasteryCategories.Mining));
        Assert.Equal(12, mastery.Award(1, MasteryCategories.Mining));
        Assert.Equal(12, mastery.Award(1, MasteryCategories.Mining));
        Assert.Equal(12, mastery.GetPermille(1, MasteryCategories.Mining));
    }

    [Fact]
    public void TracksAgentsAndCategoriesIndependently()
    {
        var mastery = new MasterySystem(new MasteryRules(7, 500));

        mastery.Award(1, MasteryCategories.Mining);
        mastery.Award(1, MasteryCategories.Crafting);
        mastery.Award(2, MasteryCategories.Mining);

        Assert.Equal(7, mastery.GetPermille(1, MasteryCategories.Mining));
        Assert.Equal(7, mastery.GetPermille(1, MasteryCategories.Crafting));
        Assert.Equal(7, mastery.GetPermille(2, MasteryCategories.Mining));
        Assert.Equal(0, mastery.GetPermille(2, MasteryCategories.Crafting));
        Assert.Equal(3, mastery.Snapshot().Count);
    }

    [Fact]
    public void ZeroGainDoesNotCreateState()
    {
        var mastery = new MasterySystem(new MasteryRules(0, 500));

        Assert.Equal(0, mastery.Award(1, MasteryCategories.Mining));
        Assert.True(mastery.IsEmpty);
        Assert.Empty(mastery.Snapshot());
    }

    [Fact]
    public void RejectsInvalidRulesAndCategories()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MasteryRules(-1, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MasteryRules(5, 1001));

        var mastery = new MasterySystem();
        Assert.Throws<ArgumentException>(() => mastery.GetPermille(1, " "));
        Assert.Throws<ArgumentException>(() => mastery.Award(1, string.Empty));
    }

    [Fact]
    public void ForgetRemovesAnAgent()
    {
        var mastery = new MasterySystem(new MasteryRules(5, 500));
        mastery.Award(1, MasteryCategories.Mining);
        mastery.Award(2, MasteryCategories.Mining);

        Assert.True(mastery.Forget(1));
        Assert.False(mastery.Forget(1));
        Assert.Equal(0, mastery.GetPermille(1, MasteryCategories.Mining));
        Assert.Equal(5, mastery.GetPermille(2, MasteryCategories.Mining));
    }

    [Fact]
    public void CheckpointRoundTripsRulesAndLevels()
    {
        var mastery = new MasterySystem(new MasteryRules(6, 40));
        mastery.Award(9, MasteryCategories.Crafting);
        mastery.Award(9, MasteryCategories.Crafting);
        mastery.Award(3, MasteryCategories.Building);
        var checkpoint = mastery.CaptureState();

        var restored = new MasterySystem();
        restored.RestoreState(checkpoint);

        Assert.Equal(6, restored.Rules.GainPerActionPermille);
        Assert.Equal(40, restored.Rules.MaximumSpeedupPermille);
        Assert.Equal(12, restored.GetPermille(9, MasteryCategories.Crafting));
        Assert.Equal(6, restored.GetPermille(3, MasteryCategories.Building));
        Assert.Equal(
            mastery.Snapshot(),
            restored.Snapshot());
    }

    [Fact]
    public void RestoreRejectsANonEmptySystemAndInvalidCheckpoints()
    {
        var mastery = new MasterySystem(new MasteryRules(5, 500));
        mastery.Award(1, MasteryCategories.Mining);

        var nonEmpty = new MasterySystem();
        nonEmpty.Award(2, MasteryCategories.Mining);
        Assert.Throws<InvalidOperationException>(() => nonEmpty.RestoreState(mastery.CaptureState()));

        var empty = new MasterySystem();
        empty.RestoreState(new MasterySystem().CaptureState());
        Assert.True(empty.IsEmpty);

        var duplicate = new MasterySystem();
        Assert.Throws<InvalidDataException>(() => duplicate.RestoreState(
            System.Text.Encoding.UTF8.GetBytes(
                """
                {"version":1,"gainPerActionPermille":5,"maximumSpeedupPermille":500,"levels":[
                {"agentId":1,"category":"mine","permille":5},
                {"agentId":1,"category":"mine","permille":10}]}
                """)));

        var tooHigh = new MasterySystem();
        Assert.Throws<InvalidDataException>(() => tooHigh.RestoreState(
            System.Text.Encoding.UTF8.GetBytes(
                """
                {"version":1,"gainPerActionPermille":5,"maximumSpeedupPermille":500,"levels":[
                {"agentId":1,"category":"mine","permille":501}]}
                """)));
    }
}
