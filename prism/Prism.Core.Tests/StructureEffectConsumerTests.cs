using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class StructureEffectConsumerTests
{
    [Fact]
    public void ShelterEffectIsConsumedInsideItsVolumeOnly()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        Assert.True(construction.CreateSite(
            "shelter-1",
            CreateShelterTemplate(),
            new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("shelter-1", "builder", 0).Succeeded);
        var consumer = new StructureEffectConsumer(construction);

        var inside = consumer.QueryShelter(7, new BlockCoordinate(1, 1, 5));
        var outside = consumer.QueryShelter(7, new BlockCoordinate(3, 3, 5));

        Assert.NotNull(inside);
        Assert.Equal("shelter-1", inside!.SiteId);
        Assert.Equal(StructureEffectConsumer.ShelterEffectType, inside.EffectType);
        Assert.Equal(500, inside.FatigueRecoveryBonusPermille);
        Assert.Null(outside);
    }

    [Fact]
    public void ShelterUsageAppliesAccessRulesAndStopsWhenTheStructureIsDamaged()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var owner = AccessPrincipal.ForAgent(10);
        var builderGroup = AccessPrincipal.ForGroup(7);
        Assert.True(construction.CreateSite(
            "shelter-1",
            CreateShelterTemplate(),
            new BlockCoordinate(1, 1, 5),
            owner).Succeeded);
        Assert.True(construction.ContributeWork("shelter-1", "builder", 0, owner).Succeeded);
        Assert.True(construction.SetAccessList(
            "shelter-1",
            owner,
            [new ContainerAccessRule(builderGroup, ContainerAccessRights.Use)]).Allowed);
        var consumer = new StructureEffectConsumer(construction);
        var inside = new BlockCoordinate(1, 1, 5);

        Assert.Null(consumer.QueryShelter(7, inside));
        Assert.NotNull(consumer.QueryShelter(7, inside, [builderGroup]));
        Assert.NotNull(consumer.QueryShelter(10, inside));

        world.SetBlockOverride(inside, new BlockOverride(1, 0, IsAir: true));

        Assert.Null(consumer.QueryShelter(10, inside));
        Assert.Null(consumer.QueryShelter(7, inside, [builderGroup]));
    }

    [Fact]
    public void ShelterUsageSurvivesCheckpointThroughStructureAccessRules()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var owner = AccessPrincipal.ForAgent(10);
        var template = CreateShelterTemplate();
        Assert.True(construction.CreateSite("shelter-1", template, new BlockCoordinate(1, 1, 5), owner).Succeeded);
        Assert.True(construction.ContributeWork("shelter-1", "builder", 0, owner).Succeeded);
        Assert.True(construction.SetAccessList(
            "shelter-1",
            owner,
            [new ContainerAccessRule(AccessPrincipal.ForAgent(7), ContainerAccessRights.Use)]).Allowed);
        var checkpoint = construction.CaptureState();

        var restoredConstruction = new StructureConstructionSystem(world);
        restoredConstruction.RestoreState(
            checkpoint,
            new Dictionary<string, StructureTemplateRule>(StringComparer.Ordinal)
            {
                [template.Id] = template,
            });
        var consumer = new StructureEffectConsumer(restoredConstruction);

        Assert.NotNull(consumer.QueryShelter(7, new BlockCoordinate(1, 1, 5)));
        Assert.Null(consumer.QueryShelter(8, new BlockCoordinate(1, 1, 5)));
    }

    [Fact]
    public void ShelterUsageResolvesGroupMembershipIntoAccessPrincipals()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        var owner = AccessPrincipal.ForAgent(10);
        const ulong buildersGroup = 7;
        Assert.True(construction.CreateSite(
            "shelter-1",
            CreateShelterTemplate(),
            new BlockCoordinate(1, 1, 5),
            owner).Succeeded);
        Assert.True(construction.ContributeWork("shelter-1", "builder", 0, owner).Succeeded);
        Assert.True(construction.SetAccessList(
            "shelter-1",
            owner,
            [new ContainerAccessRule(AccessPrincipal.ForGroup(buildersGroup), ContainerAccessRights.Use)]).Allowed);
        var membership = new GroupMembership();
        Assert.True(membership.Join(buildersGroup, 7));
        Assert.False(membership.Join(buildersGroup, 7));
        Assert.Equal([buildersGroup], membership.GroupsOf(7));
        Assert.Equal([7UL], membership.MembersOf(buildersGroup));
        var consumer = new StructureEffectConsumer(construction);
        var inside = new BlockCoordinate(1, 1, 5);

        Assert.NotNull(consumer.QueryShelterWithGroups(7, inside, membership));
        Assert.Null(consumer.QueryShelterWithGroups(8, inside, membership));
        Assert.True(membership.Leave(buildersGroup, 7));
        Assert.Null(consumer.QueryShelterWithGroups(7, inside, membership));
        Assert.Equal(0, membership.GroupCount);
    }

    [Fact]
    public void WaterAccessAndStorageEffectsAreConsumedByTypeInsideTheirVolumeOnly()
    {
        var world = CreateWorld(6, 6);
        var construction = new StructureConstructionSystem(world);
        Assert.True(construction.CreateSite(
            "well-1",
            CreateEffectTemplate(StructureEffectConsumer.WaterAccessEffectType),
            new BlockCoordinate(1, 1, 5)).Succeeded);
        Assert.True(construction.ContributeWork("well-1", "builder", 0).Succeeded);
        Assert.True(construction.CreateSite(
            "granary-1",
            CreateEffectTemplate(StructureEffectConsumer.StorageEffectType),
            new BlockCoordinate(3, 3, 5)).Succeeded);
        Assert.True(construction.ContributeWork("granary-1", "builder", 0).Succeeded);
        var consumer = new StructureEffectConsumer(construction);

        Assert.Equal("well-1", consumer.QueryWaterAccess(7, new BlockCoordinate(1, 1, 5))?.SiteId);
        Assert.Equal("granary-1", consumer.QueryStorage(7, new BlockCoordinate(3, 3, 5))?.SiteId);
        Assert.Null(consumer.QueryWaterAccess(7, new BlockCoordinate(3, 3, 5)));
        Assert.Null(consumer.QueryStorage(7, new BlockCoordinate(1, 1, 5)));
        Assert.Null(consumer.QueryShelter(7, new BlockCoordinate(1, 1, 5)));
        Assert.Equal("well-1", consumer.QueryEffect(
            7,
            new BlockCoordinate(1, 1, 5),
            StructureEffectConsumer.WaterAccessEffectType)?.SiteId);
    }

    private static StructureTemplateRule CreateEffectTemplate(string effectType) =>
        new(
            $"effect-{effectType}",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 1)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 500,
            Effect: new StructureEffectRule(
                effectType,
                new BlockCoordinate(0, 0, 0),
                new BlockCoordinate(0, 0, 0),
                FatigueRecoveryBonusPermille: 0));

    private static StructureTemplateRule CreateShelterTemplate() =>        new(
            "lean-to",
            1,
            1,
            1,
            [new StructureBlockRule(0, 0, 0, 1)],
            RequiresFlatGround: true,
            RequiresClearVolume: true,
            WorkTicks: 1,
            IntegrityThresholdPermille: 500,
            Effect: new StructureEffectRule(
                StructureEffectConsumer.ShelterEffectType,
                new BlockCoordinate(0, 0, 0),
                new BlockCoordinate(0, 0, 0),
                FatigueRecoveryBonusPermille: 500));

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
