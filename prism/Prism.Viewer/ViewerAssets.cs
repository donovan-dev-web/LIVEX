using System.Text.Json;

namespace Prism.Viewer;

/// <summary>Couleurs des silhouettes d'agents (RENDERING_SPEC.md §2).</summary>
public sealed record ViewerAgentPalette(
    (float R, float G, float B) Idle,
    (float R, float G, float B) Carrying,
    (float R, float G, float B) Acting,
    float HeadBoost,
    float LegDarken)
{
    public static ViewerAgentPalette Default { get; } = new(
        (0.20f, 0.57f, 0.68f),
        (0.82f, 0.49f, 0.20f),
        (0.58f, 0.40f, 0.78f),
        HeadBoost: 0.12f,
        LegDarken: 0.72f);

    public static ViewerAgentPalette Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("agent-palette.json doit être un objet.");
        }

        var palette = Default;
        return palette with
        {
            Idle = ReadColor(root, "idle", palette.Idle),
            Carrying = ReadColor(root, "carrying", palette.Carrying),
            Acting = ReadColor(root, "acting", palette.Acting),
            HeadBoost = ReadUnit(root, "headBoost", palette.HeadBoost, 0f, 0.3f),
            LegDarken = ReadUnit(root, "legDarken", palette.LegDarken, 0.5f, 1f),
        };
    }

    private static (float R, float G, float B) ReadColor(
        JsonElement root,
        string name,
        (float R, float G, float B) fallback)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        var channels = new float[3];
        var index = 0;
        foreach (var channel in value.EnumerateArray())
        {
            if (index >= 3 || !channel.TryGetInt32(out var parsed) || parsed is < 0 or > 255)
            {
                throw new InvalidDataException($"Couleur invalide pour '{name}' dans agent-palette.json.");
            }

            channels[index++] = parsed / 255f;
        }

        if (index != 3)
        {
            throw new InvalidDataException($"Couleur incomplète pour '{name}' dans agent-palette.json.");
        }

        return (channels[0], channels[1], channels[2]);
    }

    private static float ReadUnit(
        JsonElement root,
        string name,
        float fallback,
        float minimum,
        float maximum)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        if (!value.TryGetSingle(out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidDataException(
                $"'{name}' doit être compris entre {minimum} et {maximum} dans agent-palette.json.");
        }

        return parsed;
    }
}

/// <summary>Police bitmap du HUD : glyphes 5 × 7, avance de 6 px à l'échelle 1.</summary>
public sealed record ViewerHudFont(
    IReadOnlyDictionary<char, byte[]> Glyphs,
    int GlyphWidth,
    int GlyphHeight,
    int Advance)
{
    public const int DefaultGlyphWidth = 5;
    public const int DefaultGlyphHeight = 7;
    public const int DefaultAdvance = 6;

    public static ViewerHudFont Default { get; } = new(CreateDefaultGlyphs(), 5, 7, 6);

    public bool TryGetGlyph(char character, out byte[] glyph) =>
        Glyphs.TryGetValue(char.ToUpperInvariant(character), out glyph!);

    public static ViewerHudFont Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("glyphs", out var glyphsElement) ||
            glyphsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("hud-5x7.json doit déclarer un objet 'glyphs'.");
        }

        var width = ReadInt(root, "width", 3, 16, DefaultGlyphWidth);
        var height = ReadInt(root, "height", 3, 16, DefaultGlyphHeight);
        var advance = ReadInt(root, "advance", width, 32, Math.Max(DefaultAdvance, width));
        var glyphs = new Dictionary<char, byte[]>();
        foreach (var property in glyphsElement.EnumerateObject())
        {
            if (property.Name.Length != 1 || property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    $"Glyphe invalide '{property.Name}' dans hud-5x7.json.");
            }

            var rows = new byte[height];
            var index = 0;
            foreach (var row in property.Value.EnumerateArray())
            {
                if (index >= height || !row.TryGetInt32(out var bits) ||
                    bits < 0 || bits >= 1 << width)
                {
                    throw new InvalidDataException(
                        $"Ligne invalide pour le glyphe '{property.Name}' dans hud-5x7.json.");
                }

                rows[index++] = (byte)bits;
            }

            if (index != height)
            {
                throw new InvalidDataException(
                    $"Le glyphe '{property.Name}' doit avoir {height} lignes.");
            }

            glyphs[char.ToUpperInvariant(property.Name[0])] = rows;
        }

        if (!glyphs.ContainsKey('?'))
        {
            glyphs['?'] = CreateDefaultGlyphs()['?'];
        }

        return new ViewerHudFont(glyphs, width, height, advance);
    }

    private static int ReadInt(JsonElement root, string name, int minimum, int maximum, int fallback)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        if (!value.TryGetInt32(out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidDataException(
                $"'{name}' doit être compris entre {minimum} et {maximum} dans hud-5x7.json.");
        }

        return parsed;
    }

    internal static IReadOnlyDictionary<char, byte[]> CreateDefaultGlyphs() =>
        new Dictionary<char, byte[]>
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
            [','] = [0, 0, 0, 0, 12, 12, 4],
            ['='] = [0, 31, 0, 31, 0, 0, 0],
            ['+'] = [0, 4, 4, 31, 4, 4, 0],
            ['%'] = [17, 2, 4, 4, 8, 16, 17],
            ['('] = [2, 4, 8, 8, 8, 4, 2],
            [')'] = [8, 4, 2, 2, 2, 4, 8],
            ['?'] = [14, 17, 1, 2, 4, 0, 4],
            ['!'] = [4, 4, 4, 4, 4, 0, 4],
        };
}

/// <summary>
/// Assets du viewer chargés depuis `assets/` (ou `--assets`). Chaque fichier est optionnel :
/// un fichier absent ou invalide produit un avertissement et laisse le générateur procédural
/// en place, de sorte qu'un pack d'assets partiel ne casse jamais le rendu.
/// </summary>
public sealed record ViewerAssets(
    string? Root,
    ViewerTextureAtlas Atlas,
    TerrainMaterialPalette Palette,
    ViewerAgentPalette Agents,
    ViewerHudFont Font,
    IReadOnlyList<string> Warnings)
{
    public static ViewerAssets Load(
        string? explicitRoot,
        string baseDirectory,
        TerrainMaterialPalette fallbackPalette)
    {
        ArgumentNullException.ThrowIfNull(fallbackPalette);
        var warnings = new List<string>();
        var root = ResolveRoot(explicitRoot, baseDirectory, warnings);
        var atlas = ViewerTextureAtlas.Create();
        var palette = fallbackPalette;
        var agents = ViewerAgentPalette.Default;
        var font = ViewerHudFont.Default;
        if (root is null)
        {
            return new ViewerAssets(null, atlas, palette, agents, font, warnings);
        }

        LoadFile(
            Path.Combine(root, "textures", "viewer-atlas.png"),
            "atlas",
            warnings,
            path => atlas = ViewerTextureAtlas.Load(path));
        LoadFile(
            Path.Combine(root, "config", "terrain-palette.json"),
            "palette des matériaux",
            warnings,
            path => palette = LoadTerrainPalette(path, fallbackPalette));
        LoadFile(
            Path.Combine(root, "config", "agent-palette.json"),
            "palette des agents",
            warnings,
            path => agents = ViewerAgentPalette.Load(path));
        LoadFile(
            Path.Combine(root, "fonts", "hud-5x7.json"),
            "police du HUD",
            warnings,
            path => font = ViewerHudFont.Load(path));
        return new ViewerAssets(root, atlas, palette, agents, font, warnings);
    }

    /// <summary>
    /// `--assets` l'emporte ; sinon `assets/` à côté du binaire, sinon `prism/assets/` du
    /// dépôt (exécution depuis `bin/Debug/net10.0`).
    /// </summary>
    public static string? ResolveRoot(
        string? explicitRoot,
        string baseDirectory,
        ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var fullPath = Path.GetFullPath(explicitRoot);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }

            warnings.Add($"dossier d'assets introuvable : {fullPath} (génération procédurale)");
            return null;
        }

        var local = Path.Combine(baseDirectory, "assets");
        if (Directory.Exists(local))
        {
            return local;
        }

        var development = Path.GetFullPath(
            Path.Combine(baseDirectory, "..", "..", "..", "..", "assets"));
        return Directory.Exists(development) ? development : null;
    }

    public static TerrainMaterialPalette LoadTerrainPalette(
        string path,
        TerrainMaterialPalette fallback)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("terrain-palette.json doit être un objet.");
        }

        return fallback with
        {
            Grass = ReadMaterial(root, "grass", fallback.Grass),
            Sand = ReadMaterial(root, "sand", fallback.Sand),
            Stone = ReadMaterial(root, "stone", fallback.Stone),
            Soil = ReadMaterial(root, "soil", fallback.Soil),
            Bedrock = ReadMaterial(root, "bedrock", fallback.Bedrock),
            Water = ReadMaterial(root, "water", fallback.Water),
            Ore = ReadMaterial(root, "ore", fallback.Ore),
            Tree = ReadMaterial(root, "tree", fallback.Tree),
            Leaves = ReadMaterial(root, "leaves", fallback.Leaves),
        };
    }

    private static ushort ReadMaterial(JsonElement root, string name, ushort fallback)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        if (!value.TryGetUInt16(out var parsed) || parsed == 0)
        {
            throw new InvalidDataException(
                $"'{name}' doit être un identifiant de matériau non nul dans terrain-palette.json.");
        }

        return parsed;
    }

    private static void LoadFile(
        string path,
        string label,
        ICollection<string> warnings,
        Action<string> load)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            load(path);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or JsonException or
                ArgumentException or UnauthorizedAccessException)
        {
            warnings.Add($"{label} ignoré ({path}) : {exception.Message}");
        }
    }
}
