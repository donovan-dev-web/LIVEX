using System.Text;
using System.Text.Json.Nodes;
using Prism.Core.World;
using Prism.Core.Actions;

namespace Prism.Core.Tests;

public sealed class StructurePlacementTests
{
    [Fact]
    public void FlatClearTemplateAcceptsLevelEmptyFootprint()
    {
        var world = CreateWorld(4, 4);
        var result = StructurePlacementValidator.Validate(
            world,
            new StructureTemplateRule(
                "hut",
                2,
                2,
                2,
                [new StructureBlockRule(0, 0, 0, 2)],
                RequiresFlatGround: true,
                RequiresClearVolume: true,
                WorkTicks: 10,
                IntegrityThresholdPermille: 800),
            new BlockCoordinate(1, 1, 5));

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void PlacementFailuresExplainOutOfWorldUnevenAndOccupiedVolumes()
    {
        var world = CreateWorld(4, 4);
        var rule = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 10,
            IntegrityThresholdPermille: 800);

        Assert.Equal(
            "target_out_of_world",
            StructurePlacementValidator.Validate(world, rule, new BlockCoordinate(3, 3, 5)).ErrorCode);

        world.SetSurfaceColumn(2, 1, new SurfaceColumn(5, 1, 0, 0));
        Assert.Equal(
            "terrain_unsuitable",
            StructurePlacementValidator.Validate(world, rule, new BlockCoordinate(1, 1, 5)).ErrorCode);

        world.SetSurfaceColumn(2, 1, new SurfaceColumn(4, 1, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(2, 2, 5), new BlockOverride(3, 0, IsAir: false));
        Assert.Equal(
            "volume_not_clear",
            StructurePlacementValidator.Validate(world, rule, new BlockCoordinate(1, 1, 5)).ErrorCode);
    }

    [Fact]
    public void ConstructionSitesAcceptMultipleWorkersAndPlaceBlocksProgressively()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [
                new StructureBlockRule(1, 0, 0, 3),
                new StructureBlockRule(0, 0, 0, 2),
            ],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 4,
            IntegrityThresholdPermille: 800);

        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);

        var firstWorker = construction.ContributeWork("site-1", "agent-1", 0);
        var duplicateWork = construction.ContributeWork("site-1", "agent-1", 0);
        var secondWorker = construction.ContributeWork("site-1", "agent-2", 0);

        Assert.True(firstWorker.Succeeded);
        Assert.Equal("duplicate_contribution", duplicateWork.ErrorCode);
        Assert.Equal(1, secondWorker.Site!.BlocksPlaced);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out var firstBlock));
        Assert.Equal((ushort)2, firstBlock.MaterialId);
        Assert.False(world.TryGetBlockOverride(new BlockCoordinate(2, 1, 5), out _));

        var thirdWorker = construction.ContributeWork("site-1", "agent-3", 1);
        var fourthWorker = construction.ContributeWork("site-1", "agent-4", 1);
        Assert.Equal(StructureConstructionState.Completed, fourthWorker.Site!.State);
        Assert.Equal(2, fourthWorker.Site.BlocksPlaced);
        Assert.Equal(4, fourthWorker.Site.WorkCompleted);
        Assert.Equal(1, thirdWorker.Site!.BlocksPlaced);
    }

    [Fact]
    public void ContributionsDebitRequiredMaterialsAtomicallyAndCheckpointTheirBalance()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "material-wall",
            1,
            1,
            2,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(0, 0, 1, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 2,
            IntegrityThresholdPermille: 800,
            Materials: [new StructureMaterialRule(10, 3)]);
        Assert.True(construction.CreateSite(
            "wall",
            template,
            new BlockCoordinate(1, 1, 5)).Succeeded);

        var inventory = new AgentInventory(2);
        Assert.True(inventory.TryAdd(10, 1, 10, out var remaining));
        Assert.Equal(0, remaining);
        Assert.Equal(
            "materials_missing",
            construction.ContributeWork("wall", "agent-1", 0, inventory: inventory).ErrorCode);
        Assert.Equal(0, construction.Sites.Single().WorkCompleted);
        Assert.Equal(1, inventory.Count(10));

        Assert.True(inventory.TryAdd(10, 1, 10, out remaining));
        Assert.Equal(0, remaining);
        var first = construction.ContributeWork("wall", "agent-1", 0, inventory: inventory);
        Assert.True(first.Succeeded);
        Assert.Equal(0, inventory.Count(10));
        Assert.Equal(1, Assert.Single(first.Site!.MaterialsMissing!).Quantity);

        var checkpoint = construction.CaptureState();
        var restoredWorld = CreateWorld(4, 4);
        restoredWorld.SetBlockOverride(
            new BlockCoordinate(1, 1, 5),
            new BlockOverride(2, 0, IsAir: false));
        var restored = new StructureConstructionSystem(restoredWorld);
        restored.RestoreState(checkpoint);

        var finalInventory = new AgentInventory(1);
        Assert.True(finalInventory.TryAdd(10, 1, 10, out remaining));
        Assert.Equal(0, remaining);
        var completed = restored.ContributeWork(
            "wall",
            "agent-2",
            1,
            inventory: finalInventory);
        Assert.Equal(StructureConstructionState.Completed, completed.Site!.State);
        Assert.Empty(completed.Site.MaterialsMissing!);
        Assert.Equal(0, finalInventory.Count(10));
    }

    [Fact]
    public void ConstructionSitesRejectOverlapsAndAbandonRemovesPartialWork()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(1, 0, 0, 3)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 2,
            IntegrityThresholdPermille: 800);

        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.Equal(
            "site_overlaps",
            construction.CreateSite("site-2", template, new BlockCoordinate(2, 1, 5)).ErrorCode);
        Assert.True(construction.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));

        var abandoned = construction.AbandonSite("site-1");

        Assert.Equal(StructureConstructionState.Abandoned, abandoned.Site!.State);
        Assert.Equal(0, abandoned.Site.BlocksPlaced);
        Assert.False(world.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));
        Assert.Equal(
            "site_not_active",
            construction.ContributeWork("site-1", "agent-2", 1).ErrorCode);

        var restoredConstruction = new StructureConstructionSystem(CreateWorld(6, 6));
        restoredConstruction.RestoreState(
            construction.CaptureState(),
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
            {
                [template.Id] = template,
            });
        Assert.Equal(StructureConstructionState.Abandoned, Assert.Single(restoredConstruction.Sites).State);
    }

    [Fact]
    public void ZeroWorkConstructionPlacesTemplateImmediately()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "marker",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 5)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 0,
            IntegrityThresholdPermille: 1000);

        var result = construction.CreateSite("instant", template, new BlockCoordinate(1, 1, 5));

        Assert.Equal(StructureConstructionState.Completed, result.Site!.State);
        Assert.Equal(1, result.Site.BlocksPlaced);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));
    }

    [Fact]
    public void PlacementRejectsTemplatesWithDuplicateBlockCoordinates()
    {
        var world = CreateWorld(4, 4);
        var template = new StructureTemplateRule(
            "invalid",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(0, 0, 0, 3)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);

        Assert.Throws<ArgumentException>(() =>
            StructurePlacementValidator.Validate(world, template, new BlockCoordinate(1, 1, 5)));
    }

    [Fact]
    public void ConstructionSiteDoesNotOverwriteBlocksAddedAfterPlanning()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "marker",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);
        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        var target = new BlockCoordinate(1, 1, 5);
        world.SetBlockOverride(target, new BlockOverride(9, 0, IsAir: false));

        var obstructed = construction.ContributeWork("site-1", "agent-1", 0);

        Assert.Equal("construction_block_obstructed", obstructed.ErrorCode);
        Assert.Equal(0, construction.Sites[0].WorkCompleted);
        Assert.True(world.TryGetBlockOverride(target, out var obstacle));
        Assert.Equal((ushort)9, obstacle.MaterialId);

        var obstructedRestoreWorld = CreateWorld(4, 4);
        obstructedRestoreWorld.SetBlockOverride(target, obstacle);
        var restoredConstruction = new StructureConstructionSystem(obstructedRestoreWorld);
        restoredConstruction.RestoreState(
            construction.CaptureState(),
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
            {
                [template.Id] = template,
            });
        Assert.Equal(construction.CaptureState(), restoredConstruction.CaptureState());

        world.RemoveBlockOverride(target);
        Assert.True(construction.ContributeWork("site-1", "agent-1", 0).Succeeded);
    }

    [Fact]
    public void ConstructionCheckpointRestoresContributionWindowAndContinuesDeterministically()
    {
        var sourceWorld = CreateWorld(6, 6);
        var template = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(1, 0, 0, 3)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 4,
            IntegrityThresholdPermille: 800);
        var templates = new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
        {
            [template.Id] = template,
        };
        var original = new StructureConstructionSystem(sourceWorld);
        Assert.True(original.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(original.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.True(original.ContributeWork("site-1", "agent-2", 0).Succeeded);
        var checkpoint = original.CaptureState();

        var restoredWorld = CreateWorld(6, 6);
        foreach (var (coordinate, block) in sourceWorld.EnumerateOverrides())
        {
            restoredWorld.SetBlockOverride(coordinate, block);
        }

        var restored = new StructureConstructionSystem(restoredWorld);
        restored.RestoreState(checkpoint, templates);

        Assert.Equal(checkpoint, restored.CaptureState());
        var legacyCheckpoint = JsonNode.Parse(Encoding.UTF8.GetString(checkpoint))!.AsObject();
        legacyCheckpoint["version"] = 2;
        legacyCheckpoint.Remove("templates");
        var legacyRestored = new StructureConstructionSystem(restoredWorld);
        legacyRestored.RestoreState(Encoding.UTF8.GetBytes(legacyCheckpoint.ToJsonString()), templates);
        Assert.Equal(restored.Sites, legacyRestored.Sites);

        Assert.Equal(
            "duplicate_contribution",
            restored.ContributeWork("site-1", "agent-1", 0).ErrorCode);
        Assert.True(original.ContributeWork("site-1", "agent-3", 0).Succeeded);
        Assert.True(restored.ContributeWork("site-1", "agent-3", 0).Succeeded);
        Assert.True(original.ContributeWork("site-1", "agent-4", 1).Succeeded);
        Assert.True(restored.ContributeWork("site-1", "agent-4", 1).Succeeded);
        Assert.Equal(original.Sites, restored.Sites);
        Assert.Equal(
            sourceWorld.EnumerateOverrides().ToArray(),
            restoredWorld.EnumerateOverrides().ToArray());
    }

    [Fact]
    public void ConstructionCheckpointPreservesDamageAndRejectsUnknownTemplate()
    {
        var sourceWorld = CreateWorld(4, 4);
        var template = new StructureTemplateRule(
            "marker",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);
        var source = new StructureConstructionSystem(sourceWorld);
        Assert.True(source.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(source.ContributeWork("site-1", "agent-1", 0).Succeeded);
        var checkpoint = source.CaptureState();

        var damagedWorld = CreateWorld(4, 4);
        damagedWorld.SetBlockOverride(
            new BlockCoordinate(1, 1, 5),
            new BlockOverride(9, 0, IsAir: false));
        var target = new StructureConstructionSystem(damagedWorld);
        var templates = new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
        {
            [template.Id] = template,
        };

        Assert.Throws<InvalidDataException>(() => target.RestoreState(
            checkpoint,
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)));
        Assert.Empty(target.Sites);
        target.RestoreState(checkpoint, templates);
        Assert.Equal(StructureConstructionState.Completed, Assert.Single(target.Sites).State);
        Assert.Equal(0, target.InspectIntegrity("site-1").IntegrityPermille);
        Assert.False(target.InspectIntegrity("site-1").EffectsActive);
    }

    [Fact]
    public void StructureIntegrityIsEvaluatedLazilyAgainstTheConfiguredThreshold()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "granary",
            3,
            1,
            1,
            [
                new StructureBlockRule(0, 0, 0, 2),
                new StructureBlockRule(1, 0, 0, 2),
                new StructureBlockRule(2, 0, 0, 2),
            ],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 667,
            Effect: new StructureEffectRule(
                "storage",
                new BlockCoordinate(0, 0, 0),
                new BlockCoordinate(2, 0, 0),
                FatigueRecoveryBonusPermille: 0));
        Assert.True(construction.CreateSite("granary-1", template, new BlockCoordinate(0, 0, 5)).Succeeded);
        Assert.True(construction.ContributeWork("granary-1", "builder", 0).Succeeded);

        var intact = construction.InspectIntegrity("granary-1");
        var activeEffect = Assert.Single(construction.GetActiveEffects());
        world.SetBlockOverride(
            new BlockCoordinate(2, 0, 5),
            new BlockOverride(2, 0, IsAir: true));
        var damaged = construction.InspectIntegrity("granary-1");

        Assert.Equal(1000, intact.IntegrityPermille);
        Assert.True(intact.EffectsActive);
        Assert.Equal("storage", activeEffect.Type);
        Assert.Equal(new BlockCoordinate(0, 0, 5), activeEffect.Minimum);
        Assert.Equal(new BlockCoordinate(2, 0, 5), activeEffect.Maximum);
        Assert.Equal(666, damaged.IntegrityPermille);
        Assert.Equal(2, damaged.IntactBlocks);
        Assert.False(damaged.EffectsActive);
        Assert.Empty(construction.GetActiveEffects());
        Assert.Throws<KeyNotFoundException>(() => construction.InspectIntegrity("missing"));
    }

    [Fact]
    public void PlacementRejectsStructureEffectsOutsideTheTemplateVolume()
    {
        var world = CreateWorld(4, 4);
        var template = new StructureTemplateRule(
            "invalid-effect",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800,
            Effect: new StructureEffectRule(
                "shelter",
                new BlockCoordinate(0, 0, 0),
                new BlockCoordinate(1, 0, 0),
                FatigueRecoveryBonusPermille: 500));

        Assert.Throws<ArgumentException>(() =>
            StructurePlacementValidator.Validate(world, template, new BlockCoordinate(1, 1, 5)));
    }

    [Fact]
    public void StructureAccessIsOwnerManagedAndBuildRightsGateContributions()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var owner = AccessPrincipal.ForAgent(10);
        var builderGroup = AccessPrincipal.ForGroup(7);
        var template = new StructureTemplateRule(
            "hut",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);
        Assert.True(construction.CreateSite(
            "hut-1",
            template,
            new BlockCoordinate(1, 1, 5),
            owner).Succeeded);

        Assert.Equal(
            "not_owner",
            construction.SetAccessList(
                "hut-1",
                AccessPrincipal.ForAgent(11),
                []).ErrorCode);
        Assert.True(construction.SetAccessList(
            "hut-1",
            owner,
            [new ContainerAccessRule(builderGroup, ContainerAccessRights.Build | ContainerAccessRights.Use)]).Allowed);
        Assert.True(construction.CheckAccess(
            "hut-1",
            [owner],
            ContainerAccessRights.Take).Allowed);
        Assert.True(construction.CheckAccess(
            "hut-1",
            [builderGroup],
            ContainerAccessRights.Use).Allowed);
        Assert.Equal(
            "access_denied",
            construction.CheckAccess(
                "hut-1",
                [AccessPrincipal.ForAgent(11)],
                ContainerAccessRights.Use).ErrorCode);
        Assert.Equal(
            "access_denied",
            construction.ContributeWork("hut-1", "unauthorized-worker", 0).ErrorCode);
        Assert.Equal(0, construction.Sites[0].WorkCompleted);
        Assert.True(construction.ContributeWork(
            "hut-1",
            "authorized-worker",
            0,
            builderGroup).Succeeded);

        var checkpoint = construction.CaptureState();
        var restored = new StructureConstructionSystem(world);
        restored.RestoreState(
            checkpoint,
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
            {
                [template.Id] = template,
            });
        Assert.Equal(
            "access_denied",
            restored.CheckAccess(
                "hut-1",
                [AccessPrincipal.ForAgent(11)],
                ContainerAccessRights.Build).ErrorCode);
        Assert.True(restored.CheckAccess(
            "hut-1",
            [builderGroup],
            ContainerAccessRights.Build).Allowed);
        Assert.True(restored.SetAccessList("hut-1", owner, []).Allowed);
        Assert.True(restored.CheckAccess(
            "hut-1",
            [AccessPrincipal.ForAgent(11)],
            ContainerAccessRights.Use).Allowed);
    }

    [Fact]
    public void AbandonedSitesCanBePurgedIndividuallyAndInBulk()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(1, 0, 0, 3)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 2,
            IntegrityThresholdPermille: 800);

        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.CreateSite("site-2", template, new BlockCoordinate(3, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.True(construction.AbandonSite("site-1").Succeeded);

        // Active site is not purged.
        Assert.False(construction.RemoveAbandonedSite("site-2"));
        Assert.Equal(2, construction.Sites.Count);

        // Abandoned site is purged individually.
        Assert.True(construction.RemoveAbandonedSite("site-1"));
        Assert.Single(construction.Sites);
        Assert.False(construction.RemoveAbandonedSite("site-1"));

        // Bulk purge removes any remaining abandoned sites and reports the count.
        Assert.True(construction.AbandonSite("site-2").Succeeded);
        Assert.Equal(1, construction.PurgeAbandonedSites());
        Assert.Empty(construction.Sites);
        Assert.Equal(0, construction.PurgeAbandonedSites());
    }

    [Fact]
    public void PurgedAbandonedSitesAreNotRestoredFromCheckpoint()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            2,
            2,
            2,
            [new StructureBlockRule(0, 0, 0, 2), new StructureBlockRule(1, 0, 0, 3)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 2,
            IntegrityThresholdPermille: 800);

        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.AbandonSite("site-1").Succeeded);
        Assert.True(construction.RemoveAbandonedSite("site-1"));
        var checkpoint = construction.CaptureState();

        var restoredWorld = CreateWorld(6, 6);
        var restored = new StructureConstructionSystem(restoredWorld);
        restored.RestoreState(
            checkpoint,
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
            {
                [template.Id] = template,
            });

        Assert.Empty(restored.Sites);
    }

    [Fact]
    public void OptionalStructureWearIsDisabledByDefaultAndDamagesCompletedSitesWhenEnabled()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);
        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.False(construction.WearEnabled);

        // Disabled by default: no-op even on a multiple tick.
        Assert.Equal(0, construction.ApplyWear(2));
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));

        var wearingWorld = CreateWorld(4, 4);
        var wearing = new StructureConstructionSystem(wearingWorld, wearIntervalTicks: 2);
        Assert.True(wearing.WearEnabled);
        Assert.True(wearing.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(wearing.ContributeWork("site-1", "agent-1", 0).Succeeded);

        Assert.Equal(0, wearing.ApplyWear(1));
        Assert.True(wearingWorld.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));
        Assert.Equal(1, wearing.ApplyWear(2));
        Assert.False(wearingWorld.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));
        Assert.Equal(0, wearing.InspectIntegrity("site-1").IntegrityPermille);
    }

    [Fact]
    public void GetDamagedSitesListsCompletedStructuresBelowIntegrityThreshold()
    {
        var world = CreateWorld(4, 4);
        var construction = new StructureConstructionSystem(world);
        var template = new StructureTemplateRule(
            "hut",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800);
        Assert.True(construction.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.Empty(construction.GetDamagedSites());

        world.SetBlockOverride(new BlockCoordinate(1, 1, 5), new BlockOverride(2, 0, IsAir: true));
        var damaged = construction.GetDamagedSites();
        Assert.Single(damaged);
        Assert.Equal("site-1", damaged[0].Id);
    }

    [Fact]
    public void AblationMechanismsDisableIndividualEffectsInIsolation()
    {
        var world = CreateWorld(4, 4);
        var template = new StructureTemplateRule(
            "granary",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 2)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 800,
            Effect: new StructureEffectRule("storage", new BlockCoordinate(0, 0, 0), new BlockCoordinate(0, 0, 0), 0));

        // All enabled: effect active.
        var allOn = new StructureConstructionSystem(world, mechanisms: AblationMechanisms.AllEnabled);
        Assert.True(allOn.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(allOn.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.Single(allOn.GetActiveEffects());

        // Storage lever off alone: effect filtered out.
        var storageOff = new AblationMechanisms(Storage: false);
        var world2 = CreateWorld(4, 4);
        var ablated = new StructureConstructionSystem(world2, mechanisms: storageOff);
        Assert.True(ablated.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(ablated.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.Empty(ablated.GetActiveEffects());

        // Wear lever off alone: ApplyWear is a no-op even when enabled by interval.
        var wearOff = new AblationMechanisms(Wear: false);
        var world3 = CreateWorld(4, 4);
        var wearing = new StructureConstructionSystem(world3, wearIntervalTicks: 1, mechanisms: wearOff);
        Assert.True(wearing.CreateSite("site-1", template, new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(wearing.ContributeWork("site-1", "agent-1", 0).Succeeded);
        Assert.Equal(0, wearing.ApplyWear(1));
        Assert.True(world3.TryGetBlockOverride(new BlockCoordinate(1, 1, 5), out _));
    }

    private static WorldGrid CreateWorld(int width, int height)
    {
        var world = new WorldGrid(width, height, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        return world;
    }
}
