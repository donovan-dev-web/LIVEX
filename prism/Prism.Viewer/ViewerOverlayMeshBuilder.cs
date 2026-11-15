using Prism.Core.World;

namespace Prism.Viewer;

/// <summary>
/// Maillages d'inspection et de fantômes superposés au terrain (RENDERING_SPEC.md §2 et §5).
/// Aucun de ces maillages ne modifie le monde : ils traduisent uniquement l'état reçu.
/// </summary>
public static class ViewerOverlayMeshBuilder
{
    public const int FloatsPerVertex = TerrainMeshBuilder.FloatsPerVertex;

    /// <summary>Chantiers rendus en fantômes : un volume par site, rempli au prorata du travail.</summary>
    public static float[] BuildConstructionGhosts(
        IReadOnlyList<ViewerConstructionSiteSample> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        var vertices = new List<float>();
        foreach (var site in sites)
        {
            var progress = site.WorkRequired <= 0
                ? 0f
                : Math.Clamp(site.WorkCompleted / (float)site.WorkRequired, 0f, 1f);
            var color = GetSiteColor(site.State);
            var height = site.State == nameof(StructureConstructionState.Completed)
                ? 1.6f
                : 0.35f + progress * 1.25f;
            AddGlyph(vertices, site.X, site.Y, site.Z, height, color);
        }

        return vertices.ToArray();
    }

    /// <summary>Objets au sol : petits cubes colorés par fraîcheur (calque « fraîcheur des stocks »).</summary>
    public static float[] BuildGroundItems(IReadOnlyList<ViewerGroundItemSample> items, bool freshness)
    {
        ArgumentNullException.ThrowIfNull(items);
        var vertices = new List<float>();
        var reference = 0;
        foreach (var item in items)
        {
            reference = Math.Max(reference, item.FreshnessPermilleTicks ?? 0);
        }

        foreach (var item in items)
        {
            var color = freshness
                ? Blend(
                    (0.92f, 0.42f, 0.30f),
                    (0.36f, 0.74f, 0.52f),
                    GetFreshnessFraction(item, reference))
                : (0.86f, 0.83f, 0.72f);
            AddGlyph(vertices, item.X, item.Y, item.Z, 0.22f, color, halfWidth: 0.22f);
        }

        return vertices.ToArray();
    }

    /// <summary>Carte de chaleur : une dalle au-dessus de la surface pour chaque cellule visitée.</summary>
    public static float[] BuildHeatmap(
        IReadOnlyList<ViewerHeatCell> cells,
        WorldGrid world,
        int peak)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(world);
        if (cells.Count == 0)
        {
            return [];
        }

        TerrainMeshBuilder.BuildTopSurfaces(world, out var topHeights, out _);
        var vertices = new List<float>();
        var reference = Math.Max(peak, 1);
        foreach (var cell in cells)
        {
            if ((uint)cell.X >= (uint)world.Width || (uint)cell.Y >= (uint)world.Height)
            {
                continue;
            }

            var intensity = Math.Clamp(cell.Count / (float)reference, 0.05f, 1f);
            var color = Blend((0.24f, 0.44f, 0.82f), (0.92f, 0.32f, 0.22f), intensity);
            var top = Math.Max(topHeights[cell.X + world.Width * cell.Y], cell.Z) + 1.06f;
            AddQuad(
                vertices,
                (cell.X, cell.Y, top),
                (cell.X + 1, cell.Y, top),
                (cell.X + 1, cell.Y + 1, top),
                (cell.X, cell.Y + 1, top),
                color);
        }

        return vertices.ToArray();
    }

    public static (float R, float G, float B) GetSiteColor(string state) => state switch
    {
        nameof(StructureConstructionState.Completed) => (0.36f, 0.76f, 0.48f),
        nameof(StructureConstructionState.InProgress) => (0.92f, 0.72f, 0.28f),
        nameof(StructureConstructionState.Abandoned) => (0.58f, 0.58f, 0.60f),
        _ => (0.42f, 0.62f, 0.88f),
    };

    private static float GetFreshnessFraction(ViewerGroundItemSample item, int reference)
    {
        if (item.FreshnessPermilleTicks is not { } remaining || reference <= 0)
        {
            return 1f;
        }

        // La pile porte une durée de vie en milli-ticks : la fraction restante est relative
        // au maximum observé, suffisante pour un calque de lecture (aucune valeur métier).
        return Math.Clamp(remaining / (float)reference, 0f, 1f);
    }

    private static void AddGlyph(
        List<float> vertices,
        int cellX,
        int cellY,
        int cellZ,
        float height,
        (float R, float G, float B) color,
        float halfWidth = 0.45f)
    {
        var minX = cellX + 0.5f - halfWidth;
        var maxX = cellX + 0.5f + halfWidth;
        var minY = cellY + 0.5f - halfWidth;
        var maxY = cellY + 0.5f + halfWidth;
        var minZ = cellZ + 1.02f;
        var maxZ = minZ + Math.Max(height, 0.1f);
        AddQuad(vertices, (minX, minY, maxZ), (maxX, minY, maxZ), (maxX, maxY, maxZ), (minX, maxY, maxZ), color);
        AddQuad(vertices, (minX, maxY, minZ), (maxX, maxY, minZ), (maxX, maxY, maxZ), (minX, maxY, maxZ), Shade(color, 0.72f));
        AddQuad(vertices, (maxX, minY, minZ), (minX, minY, minZ), (minX, minY, maxZ), (maxX, minY, maxZ), Shade(color, 0.64f));
        AddQuad(vertices, (minX, minY, minZ), (minX, maxY, minZ), (minX, maxY, maxZ), (minX, minY, maxZ), Shade(color, 0.78f));
        AddQuad(vertices, (maxX, maxY, minZ), (maxX, minY, minZ), (maxX, minY, maxZ), (maxX, maxY, maxZ), Shade(color, 0.68f));
    }

    private static (float R, float G, float B) Shade((float R, float G, float B) color, float factor) =>
        (color.R * factor, color.G * factor, color.B * factor);

    private static (float R, float G, float B) Blend(
        (float R, float G, float B) from,
        (float R, float G, float B) to,
        float amount) =>
        (from.R + (to.R - from.R) * amount,
            from.G + (to.G - from.G) * amount,
            from.B + (to.B - from.B) * amount);

    private static void AddQuad(
        List<float> vertices,
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        (float X, float Y, float Z) d,
        (float R, float G, float B) color)
    {
        AddVertex(vertices, a, color);
        AddVertex(vertices, b, color);
        AddVertex(vertices, c, color);
        AddVertex(vertices, a, color);
        AddVertex(vertices, c, color);
        AddVertex(vertices, d, color);
    }

    private static void AddVertex(
        List<float> vertices,
        (float X, float Y, float Z) point,
        (float R, float G, float B) color)
    {
        vertices.Add(point.X);
        vertices.Add(point.Y);
        vertices.Add(point.Z);
        vertices.Add(color.R);
        vertices.Add(color.G);
        vertices.Add(color.B);
        vertices.Add(0.5f);
        vertices.Add(0.5f);
    }
}
