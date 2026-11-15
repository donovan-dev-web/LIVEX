using System.Reflection;
using Prism.Core.Diagnostics;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// Vérification opt-in du cache de chemins par agent (PRISM-108 sur le cache de PRISM-104/109).
/// Le mode rejoue une entrée mémorisée depuis sa propre case de départ et lève à la première
/// différence avec ce qu'une recherche fraîche rend.
/// </summary>
/// <remarks>
/// La faute injectée est une écriture de <b>matière de surface</b> dont la révision de région est
/// rembobinée : elle ne change pas <see cref="MovementGraph.CanStandAt"/> (les deux matières sont
/// solides), donc le mémo de station debout ne peut pas la masquer — seul le chemin, dont le coût
/// dépend des matières de chemin, la voit. C'est exactement le genre d'écart qu'une invalidation
/// trop étroite laisserait passer sans rien casser de visible.
/// </remarks>
public sealed class PathMemoVerificationTests
{
    private const ushort SlowMaterial = 1;
    private const ushort PathMaterial = 3;

    [Fact]
    public void VerificationStaysQuietWhileTheAgentWalksACachedPath()
    {
        var world = CreateLaneWorld();
        var lifecycle = CreateLifecycle(world, verifyCaches: true);
        lifecycle.Incarnate(1, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile());
        var target = new BlockCoordinate(11, 0, 5);

        // Chaque appel après le premier réutilise le chemin mémorisé : le mode rejoue donc une
        // recherche complète à chaque pas, et doit rester muet tant que le monde est intact.
        for (var step = 0; step < 11; step++)
        {
            Assert.True(lifecycle.TryMoveOneStep(1, target, out var error), error);
        }

        var agent = Assert.Single(lifecycle.SnapshotAgents());
        Assert.Equal(11, agent.Position.CellX);
        Assert.Equal(0, agent.Position.CellY);
    }

    [Fact]
    public void VerificationCatchesACachedPathTheWorldHasOutgrown()
    {
        var world = CreateLaneWorld();
        var lifecycle = CreateLifecycle(world, verifyCaches: true);
        lifecycle.Incarnate(1, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile());
        var target = new BlockCoordinate(11, 0, 5);

        // Deux pas : le chemin mémorisé couvre encore les cases de la voie rapide.
        Assert.True(lifecycle.TryMoveOneStep(1, target, out _));
        Assert.True(lifecycle.TryMoveOneStep(1, target, out _));

        // La voie rapide devient lente sur trois cases, et la voie lente devient rapide en face :
        // le meilleur itinéraire change de rangée. Une écriture ordinaire périmerait le chemin ;
        // ici les révisions sont rembobinées, comme si un effet avait changé le monde sans passer
        // par la grille. Le chemin mémorisé reste « valide » et serait suivi.
        for (var x = 5; x <= 7; x++)
        {
            SetSurfaceColumnWithoutRevisionBump(
                world, x, 0, new SurfaceColumn(4, SlowMaterial, 0, 0));
            SetSurfaceColumnWithoutRevisionBump(
                world, x, 1, new SurfaceColumn(4, PathMaterial, 0, 0));
        }

        var exception = Assert.Throws<CacheVerificationException>(
            () => lifecycle.TryMoveOneStep(1, target, out _));

        Assert.Equal("lifecycle.pathCache", exception.Cache);
        Assert.Equal(1UL, exception.AgentId);
        Assert.Contains("(0,0) -> (11,0)", exception.Location, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deux rangées : la voie du haut (y = 0) est en matière de chemin, celle du bas (y = 1) est
    /// lente. Le meilleur itinéraire longe la voie rapide ; assez de cases lentes sur celle-ci
    /// rendent le détour par la rangée du bas moins cher, donc le chemin change réellement.
    /// </summary>
    private static WorldGrid CreateLaneWorld()
    {
        var world = new WorldGrid(12, 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(
                    x, y, new SurfaceColumn(4, y == 0 ? PathMaterial : SlowMaterial, 0, 0));
            }
        }

        return world;
    }

    private static AgentLifecycleSystem CreateLifecycle(WorldGrid world, bool verifyCaches) =>
        new(
            world,
            new MovementRules(
                new HashSet<ushort> { SlowMaterial, PathMaterial },
                new HashSet<ushort> { 2 },
                pathMaterials: new HashSet<ushort> { PathMaterial }),
            verifyCaches: verifyCaches);

    /// <summary>
    /// Écrit une colonne de surface puis rembobine la révision globale et celle de la région :
    /// la faute que le mode existe pour attraper (PRISM-108).
    /// </summary>
    private static void SetSurfaceColumnWithoutRevisionBump(
        WorldGrid world,
        int x,
        int y,
        SurfaceColumn column)
    {
        Assert.True(world.TryGetRegionId(x, y, out var regionId));
        var revisionBefore = world.Revision;
        var regionRevisionBefore = world.GetRegionRevision(regionId);
        world.SetSurfaceColumn(x, y, column);
        Assert.NotEqual(revisionBefore, world.Revision);
        Assert.NotEqual(regionRevisionBefore, world.GetRegionRevision(regionId));

        var revisionField = typeof(WorldGrid).GetField(
                                "<Revision>k__BackingField",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException(
                                "WorldGrid.Revision must stay an auto-property for the fault injection to work.");
        revisionField.SetValue(world, revisionBefore);

        var regionRevisions = typeof(WorldGrid)
                                  .GetField("_regionRevisions", BindingFlags.Instance | BindingFlags.NonPublic)
                                  ?.GetValue(world) as long[]
                              ?? throw new InvalidOperationException(
                                  "WorldGrid must keep its per-region revisions in a long[] array.");
        regionRevisions[regionId] = regionRevisionBefore;
    }
}
