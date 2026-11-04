using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class MacroActionSystemTests
{
    [Fact]
    public void ObtainObjectSelectsOnlyPerceivedMatchingSourcesInStableOrder()
    {
        var (agent, _) = CreateAgents();
        var macroActions = new MacroActionSystem();
        var sources = new[]
        {
            new PerceivedMacroSource(
                9,
                MacroSourceKind.GroundItem,
                new BlockCoordinate(1, 0, 6),
                10,
                3,
                new HashSet<string>(StringComparer.Ordinal) { "wood" }),
            new PerceivedMacroSource(
                4,
                MacroSourceKind.GroundItem,
                new BlockCoordinate(1, 0, 6),
                10,
                1,
                new HashSet<string>(StringComparer.Ordinal) { "wood" }),
        };

        var started = macroActions.StartObtainObject(
            agent,
            new ObtainObjectRequest(ItemTypeId: 10, TagId: null, Quantity: 2),
            sources);

        Assert.Equal(MacroStepKind.Move, started.Step!.Kind);
        Assert.Equal((ulong)4, started.Step.SourceId);
        Assert.Equal(0, started.QuantityAcquired);
    }

    [Fact]
    public void ObtainObjectFailsWithoutPerceivedHintedSourceAndDoesNotSearchWorld()
    {
        var (agent, world) = CreateAgents();
        world.SetBlockOverride(
            new BlockCoordinate(1, 0, 6),
            new BlockOverride(99, 0, IsAir: false));
        var macroActions = new MacroActionSystem();
        var notPerceived = macroActions.StartObtainObject(
            agent,
            new ObtainObjectRequest(ItemTypeId: 10, TagId: null, Quantity: 1),
            []);

        Assert.Equal("source_not_found", notPerceived.ErrorCode);
        Assert.Equal(0, notPerceived.QuantityAcquired);
        Assert.Equal(
            "nothing_to_advance",
            macroActions.GetNextStep(agent.AgentId, agent.Position, []).ErrorCode);
    }

    [Fact]
    public void ObtainObjectPreservesProgressAndFailsWhenNextSourceIsNoLongerPerceived()
    {
        var (agent, _) = CreateAgents();
        var source = new PerceivedMacroSource(
            1,
            MacroSourceKind.GroundItem,
            new BlockCoordinate(1, 0, 6),
            10,
            2,
            new HashSet<string>(StringComparer.Ordinal) { "wood" });
        var macroActions = new MacroActionSystem();
        var started = macroActions.StartObtainObject(
            agent,
            new ObtainObjectRequest(ItemTypeId: 10, TagId: null, Quantity: 2),
            [source]);

        Assert.Equal(MacroStepKind.Move, started.Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        var acquire = macroActions.GetNextStep(
            agent.AgentId,
            AgentPosition.AtCell(1, 0, 5),
            [source]);
        Assert.Equal(MacroStepKind.Take, acquire.Step!.Kind);
        Assert.Equal(2, acquire.Step.Quantity);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true, quantityAcquired: 1).Succeeded);

        var noLongerPerceived = macroActions.GetNextStep(
            agent.AgentId,
            AgentPosition.AtCell(1, 0, 5),
            []);

        Assert.Equal("source_not_found", noLongerPerceived.ErrorCode);
        Assert.Equal(1, noLongerPerceived.QuantityAcquired);
    }

    [Fact]
    public void ObtainObjectRejectsNonmatchingAndOutOfHintSources()
    {
        var (agent, _) = CreateAgents();
        var macroActions = new MacroActionSystem();
        var source = new PerceivedMacroSource(
            1,
            MacroSourceKind.Block,
            new BlockCoordinate(1, 0, 6),
            10,
            2,
            new HashSet<string>(StringComparer.Ordinal) { "wood" });

        var result = macroActions.StartObtainObject(
            agent,
            new ObtainObjectRequest(
                ItemTypeId: null,
                TagId: "stone",
                Quantity: 1,
                Hint: new BlockCoordinate(5, 0, 5),
                HintRadius: 0),
            [source]);

        Assert.Equal("source_not_found", result.ErrorCode);
    }

    [Fact]
    public void CraftMacroMovesToPerceivedStationThenEmitsCraftCommand()
    {
        var (agent, _) = CreateAgents();
        var macroActions = new MacroActionSystem();
        var station = new BlockCoordinate(1, 0, 7);

        var started = macroActions.StartCraft(agent, recipeId: 5, station, stationPerceived: true);

        Assert.Equal(MacroStepKind.Move, started.Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        var craft = macroActions.GetNextStep(agent.AgentId, agent.Position, []);
        Assert.Equal(MacroStepKind.Craft, craft.Step!.Kind);
        Assert.Equal((ushort)5, craft.Step.RecipeId);
        Assert.Equal(station, craft.Step.Target);
    }

    [Fact]
    public void CultivateMacroWaitsForRipeSignalAndHarvestsOnlyOnRequest()
    {
        var (agent, _) = CreateAgents();
        var macroActions = new MacroActionSystem();
        var parcel = new BlockCoordinate(0, 0, 4);

        var started = macroActions.StartCultivate(
            agent,
            parcel,
            seedItemTypeId: 10,
            needsTilling: true,
            parcelPerceived: true);

        Assert.Equal(MacroStepKind.Till, started.Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        Assert.Equal(
            MacroStepKind.Plant,
            macroActions.GetNextStep(agent.AgentId, agent.Position, []).Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        Assert.Equal(
            MacroStepKind.WaitForRipe,
            macroActions.GetNextStep(agent.AgentId, agent.Position, []).Step!.Kind);
        Assert.Equal("not_ripe", macroActions.RequestCultivateHarvest(agent.AgentId).ErrorCode);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);

        var harvest = macroActions.RequestCultivateHarvest(agent.AgentId);
        Assert.Equal(MacroStepKind.Harvest, harvest.Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        Assert.True(macroActions.GetNextStep(agent.AgentId, agent.Position, []).Completed);
    }

    [Fact]
    public void MacroCheckpointPreservesPerceivedSourcesAndInProgressSequence()
    {
        var (agent, _) = CreateAgents();
        var source = new PerceivedMacroSource(
            55,
            MacroSourceKind.GroundItem,
            new BlockCoordinate(1, 0, 6),
            10,
            2,
            new HashSet<string>(StringComparer.Ordinal) { "wood", "material" });
        var original = new MacroActionSystem();
        var started = original.StartObtainObject(
            agent,
            new ObtainObjectRequest(ItemTypeId: 10, TagId: null, Quantity: 2),
            [source]);
        Assert.Equal(MacroStepKind.Move, started.Step!.Kind);

        var restored = new MacroActionSystem();
        restored.RestoreState(original.CaptureState());
        Assert.Equal([agent.AgentId], restored.ActiveAgentIds);
        Assert.True(restored.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        var acquiredStep = restored.GetNextStep(
            agent.AgentId,
            AgentPosition.AtCell(1, 0, 5),
            [source]);
        Assert.Equal(MacroStepKind.Take, acquiredStep.Step!.Kind);

        var resumed = new MacroActionSystem();
        resumed.RestoreState(restored.CaptureState());
        Assert.Equal(
            acquiredStep.Step,
            resumed.GetNextStep(agent.AgentId, AgentPosition.AtCell(1, 0, 5), [source]).Step);
        Assert.True(resumed.CompleteCurrentStep(agent.AgentId, true, quantityAcquired: 1).Succeeded);
        Assert.Equal(1, resumed.GetNextStep(
            agent.AgentId,
            AgentPosition.AtCell(1, 0, 5),
            [source]).QuantityAcquired);
    }

    [Fact]
    public void CultivateMacroUsesTheGroundCellForTillingAndSurvivesCheckpoint()
    {
        var (agent, _) = CreateAgents();
        var macroActions = new MacroActionSystem();
        var cropCell = new BlockCoordinate(0, 0, 5);
        var started = macroActions.StartCultivate(
            agent,
            cropCell,
            seedItemTypeId: 10,
            needsTilling: true);

        Assert.Equal(new BlockCoordinate(0, 0, 4), started.Step!.Target);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        Assert.Equal(cropCell, macroActions.GetNextStep(agent.AgentId, agent.Position, []).Step!.Target);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);
        Assert.Equal(
            MacroStepKind.WaitForRipe,
            macroActions.GetNextStep(agent.AgentId, agent.Position, []).Step!.Kind);
        Assert.True(macroActions.CompleteCurrentStep(agent.AgentId, succeeded: true).Succeeded);

        var restored = new MacroActionSystem();
        restored.RestoreState(macroActions.CaptureState());
        Assert.True(restored.IsWaitingForHarvest(agent.AgentId));
        Assert.True(restored.RequestCultivateHarvest(agent.AgentId).Succeeded);
    }

    private static (AgentBody Agent, WorldGrid World) CreateAgents()
    {
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        var movement = new MovementGraph(world, new MovementRules([1], []));
        var agent = new AgentRegistry().Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(),
            movement);
        return (agent, world);
    }
}
