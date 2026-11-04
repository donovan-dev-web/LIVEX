using Prism.Core.World;

namespace Prism.Viewer;

public sealed record AgentVisualMesh(ulong AgentId, float[] Vertices);

public static class AgentVisualMeshBuilder
{
    private const float WorldUnitsPerCell = 1000f;

    public static IReadOnlyList<AgentVisualMesh> Build(
        IEnumerable<AgentEntitySnapshot> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var meshes = new List<AgentVisualMesh>();
        foreach (var agent in agents)
        {
            ArgumentNullException.ThrowIfNull(agent);
            ArgumentNullException.ThrowIfNull(agent.Inventory);
            var vertices = new List<float>();
            var x = agent.Position.X / WorldUnitsPerCell;
            var y = agent.Position.Y / WorldUnitsPerCell;
            var feetZ = agent.Position.Z / WorldUnitsPerCell;
            var carrying = agent.Inventory.Length > 0;
            var body = carrying
                ? (R: 0.82f, G: 0.49f, B: 0.20f)
                : (R: 0.20f, G: 0.57f, B: 0.68f);
            var head = (R: Math.Min(body.R + 0.12f, 1f),
                G: Math.Min(body.G + 0.12f, 1f),
                B: Math.Min(body.B + 0.12f, 1f));
            var legs = (R: body.R * 0.72f, G: body.G * 0.72f, B: body.B * 0.72f);

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
    }
}
