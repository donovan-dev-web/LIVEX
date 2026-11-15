using Prism.Core.World;

namespace Prism.Viewer;

public sealed record AgentVisualMesh(ulong AgentId, float[] Vertices);

public static class AgentVisualMeshBuilder
{
    public const int FloatsPerVertex = TerrainMeshBuilder.FloatsPerVertex;

    private const float WorldUnitsPerCell = 1000f;

    public static IReadOnlyList<AgentVisualMesh> Build(
        IEnumerable<AgentEntitySnapshot> agents,
        ViewerAgentPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        return Build(agents.Select(static agent =>
        {
            ArgumentNullException.ThrowIfNull(agent);
            return new ViewerAgentSample(
                agent.AgentId,
                agent.Position.X / WorldUnitsPerCell,
                agent.Position.Y / WorldUnitsPerCell,
                agent.Position.Z / WorldUnitsPerCell,
                agent.Inventory,
                null);
        }), palette);
    }

    /// <summary>
    /// Silhouettes voxel des agents live (RENDERING_SPEC.md §2) : la couleur distingue un agent
    /// inactif, chargé ou en action ; un marqueur au-dessus de la tête porte l'action en cours.
    /// </summary>
    public static IReadOnlyList<AgentVisualMesh> Build(
        IEnumerable<ViewerAgentSample> agents,
        ViewerAgentPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var colors = palette ?? ViewerAgentPalette.Default;
        var meshes = new List<AgentVisualMesh>();
        foreach (var agent in agents)
        {
            ArgumentNullException.ThrowIfNull(agent);
            ArgumentNullException.ThrowIfNull(agent.Inventory);
            var vertices = new List<float>();
            var x = agent.X;
            var y = agent.Y;
            var feetZ = agent.Z;
            var carrying = agent.Inventory.Length > 0;
            var acting = !string.IsNullOrEmpty(agent.ActiveAction);
            var body = acting
                ? colors.Acting
                : carrying
                    ? colors.Carrying
                    : colors.Idle;
            var head = (R: Math.Min(body.R + colors.HeadBoost, 1f),
                G: Math.Min(body.G + colors.HeadBoost, 1f),
                B: Math.Min(body.B + colors.HeadBoost, 1f));
            var legs = (R: body.R * colors.LegDarken,
                G: body.G * colors.LegDarken,
                B: body.B * colors.LegDarken);

            AddBox(vertices, x - 0.30f, y - 0.22f, feetZ,
                x - 0.04f, y + 0.22f, feetZ + 0.48f, legs);
            AddBox(vertices, x + 0.04f, y - 0.22f, feetZ,
                x + 0.30f, y + 0.22f, feetZ + 0.48f, legs);
            AddBox(vertices, x - 0.48f, y - 0.21f, feetZ + 0.58f,
                x - 0.29f, y + 0.21f, feetZ + 1.30f, body);
            AddBox(vertices, x + 0.29f, y - 0.21f, feetZ + 0.58f,
                x + 0.48f, y + 0.21f, feetZ + 1.30f, body);
            AddBox(vertices, x - 0.27f, y - 0.20f, feetZ + 0.48f,
                x + 0.27f, y + 0.20f, feetZ + 1.42f, body);
            AddBox(vertices, x - 0.23f, y - 0.23f, feetZ + 1.43f,
                x + 0.23f, y + 0.23f, feetZ + 1.88f, head);

            if (carrying)
            {
                AddBox(vertices, x + 0.28f, y - 0.30f, feetZ + 0.78f,
                    x + 0.48f, y - 0.08f, feetZ + 1.02f,
                    (R: 0.94f, G: 0.77f, B: 0.31f));
            }

            if (acting)
            {
                // Marqueur d'action : petite barre au-dessus de la tête, lisible de loin.
                AddBox(vertices, x - 0.20f, y - 0.20f, feetZ + 2.02f,
                    x + 0.20f, y + 0.20f, feetZ + 2.16f,
                    (R: 0.90f, G: 0.94f, B: 0.52f));
            }

            meshes.Add(new AgentVisualMesh(agent.AgentId, vertices.ToArray()));
        }

        return meshes;
    }

    private static void AddBox(
        List<float> vertices,
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ,
        (float R, float G, float B) color)
    {
        AddFace(
            vertices,
            (minX, minY, maxZ), (maxX, minY, maxZ),
            (maxX, maxY, maxZ), (minX, maxY, maxZ), color, 1f);
        AddFace(
            vertices,
            (minX, maxY, minZ), (maxX, maxY, minZ),
            (maxX, maxY, maxZ), (minX, maxY, maxZ), color, 0.72f);
        AddFace(
            vertices,
            (maxX, minY, minZ), (minX, minY, minZ),
            (minX, minY, maxZ), (maxX, minY, maxZ), color, 0.64f);
        AddFace(
            vertices,
            (minX, minY, minZ), (minX, maxY, minZ),
            (minX, maxY, maxZ), (minX, minY, maxZ), color, 0.78f);
        AddFace(
            vertices,
            (maxX, maxY, minZ), (maxX, minY, minZ),
            (maxX, minY, maxZ), (maxX, maxY, maxZ), color, 0.68f);
        AddFace(
            vertices,
            (minX, maxY, minZ), (minX, minY, minZ),
            (maxX, minY, minZ), (maxX, maxY, minZ), color, 0.52f);
    }

    private static void AddFace(
        List<float> vertices,
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        (float X, float Y, float Z) d,
        (float R, float G, float B) color,
        float shade)
    {
        AddVertex(vertices, a, color, shade);
        AddVertex(vertices, b, color, shade);
        AddVertex(vertices, c, color, shade);
        AddVertex(vertices, a, color, shade);
        AddVertex(vertices, c, color, shade);
        AddVertex(vertices, d, color, shade);
    }

    private static void AddVertex(
        List<float> vertices,
        (float X, float Y, float Z) point,
        (float R, float G, float B) color,
        float shade)
    {
        vertices.Add(point.X);
        vertices.Add(point.Y);
        vertices.Add(point.Z);
        vertices.Add(color.R * shade);
        vertices.Add(color.G * shade);
        vertices.Add(color.B * shade);
        vertices.Add(0.5f);
        vertices.Add(0.5f);
    }
}
