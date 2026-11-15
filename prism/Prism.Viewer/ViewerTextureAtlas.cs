namespace Prism.Viewer;

/// <summary>
/// Atlas de textures du viewer, généré par programme au démarrage (RENDERING_SPEC.md §2 :
/// « palette sobre, pas de ressources externes lourdes »). Une tuile par type de matériau,
/// une tuile de repli pour tout identifiant inconnu. La génération est entièrement
/// déterministe : même identifiant de matériau, même tuile, mêmes octets.
/// </summary>
public sealed record ViewerTextureAtlas(byte[] Rgba, int Width, int Height, int TileSize)
{
    public const int DefaultTileSize = 16;
    public const int Columns = 4;
    public const int Rows = 4;
    public const int FallbackTile = Columns * Rows - 1;

    /// <summary>Tuile du gazon.</summary>
    public const int GrassTile = 0;
    /// <summary>Tuile du sable.</summary>
    public const int SandTile = 1;
    /// <summary>Tuile de la pierre.</summary>
    public const int StoneTile = 2;
    /// <summary>Tuile de la terre labourée.</summary>
    public const int SoilTile = 3;
    /// <summary>Tuile du socle rocheux.</summary>
    public const int BedrockTile = 4;
    /// <summary>Tuile de l'eau statique.</summary>
    public const int WaterTile = 5;
    /// <summary>Tuile des matériaux de construction (bois, pierre taillée).</summary>
    public const int StructureTile = 6;
    /// <summary>Tuile du minerai (granulat clair, teinte portée par la palette).</summary>
    public const int OreTile = 7;
    /// <summary>Tuile du feuillage.</summary>
    public const int LeavesTile = 8;
    /// <summary>Tuile du tronc.</summary>
    public const int WoodTile = 9;

    public static ViewerTextureAtlas Create(int tileSize = DefaultTileSize)
    {
        if (tileSize is < 4 or > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileSize),
                "Atlas tile size must be between 4 and 128 pixels.");
        }

        var width = Columns * tileSize;
        var height = Rows * tileSize;
        var rgba = new byte[checked(width * height * 4)];
        for (var tile = 0; tile < Columns * Rows; tile++)
        {
            FillTile(rgba, width, tile, tileSize);
        }

        return new ViewerTextureAtlas(rgba, width, height, tileSize);
    }

    /// <summary>
    /// Charge un atlas externe (`prism/assets/textures/viewer-atlas.png`) : carré, quatre
    /// colonnes, tuiles de taille homogène. L'ordre des tuiles est celui de <see cref="Create"/>.
    /// </summary>
    public static ViewerTextureAtlas Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var image = ViewerPng.Load(path);
        if (image.Width != image.Height)
        {
            throw new InvalidDataException("L'atlas doit être carré.");
        }

        if (image.Width % Columns != 0 || image.Height % Rows != 0)
        {
            throw new InvalidDataException(
                $"L'atlas doit se découper en {Columns} × {Rows} tuiles.");
        }

        var tileSize = image.Width / Columns;
        if (tileSize != image.Height / Rows || tileSize is < 4 or > 128)
        {
            throw new InvalidDataException(
                "Tuiles d'atlas non carrées ou hors des bornes 4–128 pixels.");
        }

        return new ViewerTextureAtlas(image.Rgba, image.Width, image.Height, tileSize);
    }

    /// <summary>Tuile d'un identifiant de matériau, déduite de la palette du monde.</summary>
    public static int GetTileIndex(ushort materialId, TerrainMaterialPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (materialId == palette.Water)
        {
            return WaterTile;
        }

        if (materialId == palette.Sand)
        {
            return SandTile;
        }

        if (materialId == palette.Stone)
        {
            return StoneTile;
        }

        if (materialId == palette.Soil)
        {
            return SoilTile;
        }

        if (materialId == palette.Bedrock)
        {
            return BedrockTile;
        }

        if (materialId == palette.Grass)
        {
            return GrassTile;
        }

        if (palette.Tree != 0 && materialId == palette.Tree)
        {
            return WoodTile;
        }

        if (palette.Leaves != 0 && materialId == palette.Leaves)
        {
            return LeavesTile;
        }

        if (palette.Ore != 0 && materialId == palette.Ore)
        {
            return OreTile;
        }

        // Un matériau inconnu de la palette reste lisible : il reçoit la tuile de structure,
        // dont le grain neutre ne dénature pas la couleur de repli de la palette.
        return StructureTile;
    }

    /// <summary>Bornes UV d'une tuile dans l'atlas.</summary>
    public (float MinU, float MinV, float MaxU, float MaxV) GetTileBounds(int tile)
    {
        if (tile is < 0 || tile >= Columns * Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(tile));
        }

        var column = tile % Columns;
        var row = tile / Columns;
        var minU = column * (float)TileSize / Width;
        var minV = row * (float)TileSize / Height;
        return (minU, minV, minU + (float)TileSize / Width, minV + (float)TileSize / Height);
    }

    private static void FillTile(byte[] rgba, int width, int tile, int tileSize)
    {
        var (baseR, baseG, baseB) = GetTileTint(tile);
        var originX = tile % Columns * tileSize;
        var originY = tile / Columns * tileSize;
        for (var localY = 0; localY < tileSize; localY++)
        {
            for (var localX = 0; localX < tileSize; localX++)
            {
                var shade = GetTileShade(tile, localX, localY, tileSize);
                var offset = ((originY + localY) * width + originX + localX) * 4;
                rgba[offset] = ToByte(baseR * shade);
                rgba[offset + 1] = ToByte(baseG * shade);
                rgba[offset + 2] = ToByte(baseB * shade);
                rgba[offset + 3] = 255;
            }
        }
    }

    private static (float R, float G, float B) GetTileTint(int tile) => tile switch
    {
        GrassTile => (0.94f, 1.00f, 0.90f),
        SandTile => (1.00f, 0.96f, 0.88f),
        StoneTile => (0.94f, 0.96f, 1.00f),
        SoilTile => (0.92f, 0.88f, 0.84f),
        BedrockTile => (0.88f, 0.90f, 0.94f),
        WaterTile => (0.88f, 0.96f, 1.00f),
        StructureTile => (1.00f, 0.98f, 0.92f),
        OreTile => (1.00f, 0.95f, 0.88f),
        LeavesTile => (0.94f, 1.00f, 0.92f),
        WoodTile => (1.00f, 0.96f, 0.90f),
        _ => (1f, 1f, 1f),
    };

    private static float GetTileShade(int tile, int x, int y, int tileSize)
    {
        var noise = Hash((uint)(tile * 65_537 + x * 257 + y)) % 1000 / 1000f;
        switch (tile)
        {
            case GrassTile:
                // Brins verticaux courts : une variation par colonne, atténuée par ligne.
                return 0.94f + 0.10f * noise * (1f - y / (float)tileSize * 0.4f);
            case WaterTile:
                // Ondulations horizontales statiques : aucune animation décorative.
                return y % 4 == 0 ? 0.92f + 0.08f * noise : 1.02f - 0.04f * noise;
            case StoneTile:
                return 0.86f + 0.24f * noise;
            case SoilTile:
                return 0.88f + 0.14f * noise * (x % 3 == 0 ? 1.1f : 0.8f);
            case BedrockTile:
                return 0.82f + 0.30f * noise;
            case StructureTile:
                // Grain de bois : bandes verticales régulières.
                return x % 4 == 0 ? 0.90f : 0.98f + 0.04f * noise;
            case OreTile:
                // Granulat : quelques éclats francs sur une trame irrégulière.
                return noise > 0.78f ? 1.04f : 0.82f + 0.18f * noise;
            case LeavesTile:
                // Feuillage : touffes diagonales, contraste modéré.
                return (x + y) % 3 == 0 ? 0.80f + 0.12f * noise : 0.96f + 0.10f * noise;
            case WoodTile:
                // Écorce : fibres verticales marquées.
                return x % 3 == 0 ? 0.84f : 0.96f + 0.06f * noise;
            default:
                return 0.95f + 0.08f * noise;
        }
    }

    private static byte ToByte(float value) =>
        (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    private static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352d;
        value ^= value >> 15;
        value *= 0x846ca68b;
        return value ^ (value >> 16);
    }
}
