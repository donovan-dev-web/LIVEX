using Prism.Core.World;

namespace Prism.Viewer;

public sealed record ViewerHudState(
    ulong Seed,
    int WorldWidth,
    int WorldHeight,
    int AgentCount,
    ViewerCameraMode CameraMode,
    bool ZoneOverlay,
    AgentEntitySnapshot? SelectedAgent,
    ulong? FollowedAgentId,
    long? Tick = null,
    ViewerDayNightState? Time = null,
    bool Live = false);

public static class ViewerHudMeshBuilder
{
    private const int Margin = 16;
    private const int PanelWidth = 430;
    private const int PanelHeight = 300;
    private const int GlyphScale = 2;
    private const int LineHeight = 17;
    private static readonly IReadOnlyDictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
    {
        [' '] = [0, 0, 0, 0, 0, 0, 0],
        ['-'] = [0, 0, 0, 31, 0, 0, 0],
        [':'] = [0, 12, 12, 0, 12, 12, 0],
        ['/'] = [1, 2, 2, 4, 8, 8, 16],
        ['.'] = [0, 0, 0, 0, 0, 12, 12],
        ['0'] = [14, 17, 19, 21, 25, 17, 14],
        ['1'] = [4, 12, 4, 4, 4, 4, 14],
        ['2'] = [14, 17, 1, 2, 4, 8, 31],
        ['3'] = [30, 1, 1, 14, 1, 1, 30],
        ['4'] = [2, 6, 10, 18, 31, 2, 2],
        ['5'] = [31, 16, 16, 30, 1, 1, 30],
        ['6'] = [14, 16, 16, 30, 17, 17, 14],
        ['7'] = [31, 1, 2, 4, 8, 8, 8],
        ['8'] = [14, 17, 17, 14, 17, 17, 14],
        ['9'] = [14, 17, 17, 15, 1, 1, 14],
        ['A'] = [14, 17, 17, 31, 17, 17, 17],
        ['B'] = [30, 17, 17, 30, 17, 17, 30],
        ['C'] = [14, 17, 16, 16, 16, 17, 14],
        ['D'] = [30, 17, 17, 17, 17, 17, 30],
        ['E'] = [31, 16, 16, 30, 16, 16, 31],
        ['F'] = [31, 16, 16, 30, 16, 16, 16],
        ['G'] = [14, 17, 16, 23, 17, 17, 15],
        ['H'] = [17, 17, 17, 31, 17, 17, 17],
        ['I'] = [14, 4, 4, 4, 4, 4, 14],
        ['J'] = [7, 2, 2, 2, 2, 18, 12],
        ['K'] = [17, 18, 20, 24, 20, 18, 17],
        ['L'] = [16, 16, 16, 16, 16, 16, 31],
        ['M'] = [17, 27, 21, 21, 17, 17, 17],
        ['N'] = [17, 25, 21, 19, 17, 17, 17],
        ['O'] = [14, 17, 17, 17, 17, 17, 14],
        ['P'] = [30, 17, 17, 30, 16, 16, 16],
        ['Q'] = [14, 17, 17, 17, 21, 18, 13],
        ['R'] = [30, 17, 17, 30, 20, 18, 17],
        ['S'] = [15, 16, 16, 14, 1, 1, 30],
        ['T'] = [31, 4, 4, 4, 4, 4, 4],
        ['U'] = [17, 17, 17, 17, 17, 17, 14],
        ['V'] = [17, 17, 17, 17, 17, 10, 4],
        ['W'] = [17, 17, 17, 21, 21, 21, 10],
        ['X'] = [17, 17, 10, 4, 10, 17, 17],
        ['Y'] = [17, 17, 10, 4, 4, 4, 4],
        ['Z'] = [31, 1, 2, 4, 8, 16, 31],
    };

    public static float[] Build(ViewerHudState state, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(state.WorldWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(state.WorldHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(state.AgentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (width <= Margin * 2 || height <= Margin * 2)
        {
            return [];
        }

        var scale = Math.Min(
            1f,
            Math.Min(
                (width - Margin * 2f) / PanelWidth,
                (height - Margin * 2f) / PanelHeight));
        var vertices = new List<float>();
        AddRectangle(vertices, width, height, Margin, Margin,
            PanelWidth, PanelHeight, scale, (0.055f, 0.085f, 0.11f));

        var lines = new[]
        {
            $"PRISM DEBUG VIEWER",
            state.Live ? "LIVE HOST STREAM" : "STATIC PREVIEW / NO LIVE HOST",
            $"SEED: {state.Seed}   SIZE: {state.WorldWidth} X {state.WorldHeight}",
            $"TICK: {GetTick(state)}   AGENTS: {state.AgentCount}",
            GetCalendarLine(state.Time),
            GetLightLine(state.Time),
            $"CAMERA: {state.CameraMode}   ZONE OVERLAY: {(state.ZoneOverlay ? "ON" : "OFF")}",
            "O ORBIT  F FREE  WASD MOVE",
            "ARROWS LOOK  Q/E ZOOM  ESC EXIT",
            "V ZONE OVERLAY  TAB CYCLE AGENT",
            "C FOLLOW/UNFOLLOW SELECTED AGENT",
            $"AGENT: {GetAgentId(state.SelectedAgent)}",
            GetAgentPosition(state.SelectedAgent),
            GetInventorySummary(state.SelectedAgent),
            $"CAMERA FOLLOW: {state.FollowedAgentId?.ToString() ?? "OFF"}",
        };
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0
                ? (R: 0.36f, G: 0.83f, B: 0.78f)
                : (R: 0.86f, G: 0.90f, B: 0.91f);
            AddText(vertices, lines[index], Margin + 13, Margin + 10 + index * LineHeight,
                width, height, scale, color);
        }

        return vertices.ToArray();
    }

    private static string GetTick(ViewerHudState state) =>
        state.Tick?.ToString() ?? "PREVIEW";

    private static string GetCalendarLine(ViewerDayNightState? time) =>
        time is null
            ? "CALENDAR: UNAVAILABLE"
            : $"DAY {time.Value.DayIndex}  {time.Value.Season} D{time.Value.DayOfSeason}  " +
              $"{time.Value.Hour:00}:{time.Value.Minute:00}";

    private static string GetLightLine(ViewerDayNightState? time) =>
        time is null
            ? "SUN: UNAVAILABLE"
            : $"SUNLIGHT: {(int)MathF.Round(time.Value.Daylight * 100f)}%  " +
              $"ELEVATION: {(int)MathF.Round(time.Value.SunElevation * 100f)}%";

    private static string GetAgentId(AgentEntitySnapshot? agent) =>
        agent is null ? "NONE SELECTED" : agent.AgentId.ToString();

    private static string GetAgentPosition(AgentEntitySnapshot? agent) =>
        agent is null
            ? "POSITION: UNAVAILABLE"
            : $"POS X:{agent.Position.CellX} Y:{agent.Position.CellY} Z:{agent.Position.FeetZ}";

    private static string GetInventorySummary(AgentEntitySnapshot? agent)
    {
        if (agent is null)
        {
            return "INVENTORY: UNAVAILABLE";
        }

        var itemCount = agent.Inventory.Sum(stack => (long)stack.Quantity);
        return $"INVENTORY: {itemCount} ITEMS {agent.Inventory.Length} STACKS";
    }

    private static void AddText(
        List<float> vertices,
        string text,
        int x,
        int y,
        int width,
        int height,
        float scale,
        (float R, float G, float B) color)
    {
        foreach (var character in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(character, out var glyph))
            {
                for (var row = 0; row < glyph.Length; row++)
                {
                    for (var column = 0; column < 5; column++)
                    {
                        if ((glyph[row] & (1 << (4 - column))) == 0)
                        {
                            continue;
                        }

                        AddRectangle(
                            vertices,
                            width,
                            height,
                            x + column * GlyphScale,
                            y + row * GlyphScale,
                            GlyphScale,
                            GlyphScale,
                            scale,
                            color);
                    }
                }
            }

            x += 6 * GlyphScale;
        }
    }

    private static void AddRectangle(
        List<float> vertices,
        int viewportWidth,
        int viewportHeight,
        float x,
        float y,
        float rectangleWidth,
        float rectangleHeight,
        float scale,
        (float R, float G, float B) color)
    {
        var left = x * scale * 2f / viewportWidth - 1f;
        var right = (x + rectangleWidth) * scale * 2f / viewportWidth - 1f;
        var top = 1f - y * scale * 2f / viewportHeight;
        var bottom = 1f - (y + rectangleHeight) * scale * 2f / viewportHeight;
        AddVertex(vertices, left, bottom, color);
        AddVertex(vertices, right, bottom, color);
        AddVertex(vertices, right, top, color);
        AddVertex(vertices, left, bottom, color);
        AddVertex(vertices, right, top, color);
        AddVertex(vertices, left, top, color);
    }

    private static void AddVertex(
        List<float> vertices,
        float x,
        float y,
        (float R, float G, float B) color)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(0);
        vertices.Add(color.R);
        vertices.Add(color.G);
        vertices.Add(color.B);
    }
}
