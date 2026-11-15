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
    bool Live = false,
    string Mode = "preview",
    string? SelectedAction = null,
    ViewerLayer[]? ActiveLayers = null,
    float CutZ = 0f,
    bool CutEnabled = false,
    ulong? WorldHash = null,
    int Resynchronisations = 0,
    string? Status = null);

public static class ViewerHudMeshBuilder
{
    /// <summary>Position (3), couleur (3) et coordonnées de texture (2), comme le monde.</summary>
    public const int FloatsPerVertex = TerrainMeshBuilder.FloatsPerVertex;

    private const int Margin = 16;
    private const int PanelWidth = 470;
    private const int PanelHeight = 380;
    private const int GlyphScale = 2;
    private const int LineHeight = 17;

    public static float[] Build(
        ViewerHudState state,
        int width,
        int height,
        ViewerHudFont? font = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var glyphs = font ?? ViewerHudFont.Default;
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

        var lines = new List<string>
        {
            "PRISM DEBUG VIEWER",
            GetModeLine(state),
            $"SEED: {state.Seed}   SIZE: {state.WorldWidth} X {state.WorldHeight}",
            $"TICK: {GetTick(state)}   AGENTS: {state.AgentCount}",
            GetCalendarLine(state.Time),
            GetLightLine(state.Time),
            $"CAMERA: {state.CameraMode}   CUT: {GetCutLine(state)}",
            $"LAYERS: {GetLayersLine(state)}",
            GetWorldLine(state),
            "O ORBIT  F FREE  WASD MOVE",
            "ARROWS LOOK  Q/E ZOOM  ESC EXIT",
            "TAB AGENT  C FOLLOW  V ZONES",
            "1-7 LAYERS  X CUT  [/] SLICE",
            "P PLACE  R REMOVE (JOURNALED)",
            $"AGENT: {GetAgentId(state.SelectedAgent)}",
            GetAgentPosition(state.SelectedAgent),
            GetInventorySummary(state.SelectedAgent),
            $"ACTION: {state.SelectedAction ?? "IDLE"}",
            $"CAMERA FOLLOW: {state.FollowedAgentId?.ToString() ?? "OFF"}",
            GetStatusLine(state),
        };
        for (var index = 0; index < lines.Count; index++)
        {
            var color = index == 0
                ? (R: 0.36f, G: 0.83f, B: 0.78f)
                : (R: 0.86f, G: 0.90f, B: 0.91f);
            AddText(vertices, glyphs, lines[index], Margin + 13, Margin + 10 + index * LineHeight,
                width, height, scale, color);
        }

        return vertices.ToArray();
    }

    private static string GetModeLine(ViewerHudState state) => state.Mode switch
    {
        "live" => state.Live ? "MODE: LIVE CLIENT" : "MODE: LIVE CLIENT / CONNECTING",
        "embedded" => "MODE: EMBEDDED HOST",
        "replay" => "MODE: REPLAY",
        _ => "MODE: STATIC PREVIEW / NO LIVE HOST",
    };

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

    private static string GetCutLine(ViewerHudState state) =>
        state.CutEnabled ? $"ON Z={(int)MathF.Round(state.CutZ)}" : "OFF";

    private static string GetLayersLine(ViewerHudState state)
    {
        var layers = state.ActiveLayers;
        if (layers is null || layers.Length == 0)
        {
            return "NONE";
        }

        var names = layers
            .OrderBy(layer => layer)
            .Select(layer => layer.ToString().ToUpperInvariant());
        return string.Join(" ", names);
    }

    private static string GetWorldLine(ViewerHudState state)
    {
        var hash = state.WorldHash is { } value ? $"HASH: {value:X16}" : "HASH: UNAVAILABLE";
        return state.Resynchronisations > 0
            ? $"{hash}  RESYNC: {state.Resynchronisations}"
            : hash;
    }

    private static string GetStatusLine(ViewerHudState state) =>
        string.IsNullOrWhiteSpace(state.Status) ? "STATUS: READY" : $"STATUS: {state.Status}";

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
        ViewerHudFont font,
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
            if (font.TryGetGlyph(character, out var glyph))
            {
                for (var row = 0; row < font.GlyphHeight && row < glyph.Length; row++)
                {
                    for (var column = 0; column < font.GlyphWidth; column++)
                    {
                        if ((glyph[row] & (1 << (font.GlyphWidth - 1 - column))) == 0)
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

            x += font.Advance * GlyphScale;
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
        vertices.Add(0.5f);
        vertices.Add(0.5f);
    }
}
