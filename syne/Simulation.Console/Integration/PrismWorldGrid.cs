// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>Case d'incarnation proposée par PRISM : coordonnées de bloc (ADR-013).</summary>
public readonly record struct PrismSpawnCell(int X, int Y, int FeetZ);

/// <summary>Bloc non aérien déclaré en surcharge dans l'instantané de monde.</summary>
public readonly record struct PrismBlockCell(int X, int Y, int Z, ushort MaterialId);

/// <summary>
/// Grille du monde PRISM lue dans l'enveloppe <c>world_snapshot</c> du flux de
/// lecture (<c>/data/read</c>) : dimensions en <b>blocs</b> et cases où un agent
/// peut être incarné.
/// </summary>
/// <remarks>
/// PRISM est autoritaire sur la géométrie (ADR-013) : SYNE ne devine ni la
/// taille du monde ni une case de départ, il les lit. Les cellules retenues
/// reproduisent la règle de <c>MovementGraph.CanStandAt</c> pour ce qui est
/// observable dans l'instantané : une colonne de surface existe, les deux blocs
/// au-dessus d'elle sont libres (l'eau est posée en surcharge au-dessus de la
/// surface, donc elle est filtrée ici) et l'altitude reste dans le monde.
/// </remarks>
public sealed class PrismWorldGrid
{
    private readonly IReadOnlyList<PrismSpawnCell> _spawnCells;
    private readonly Dictionary<(int X, int Y), int> _feetZByColumn;

    private PrismWorldGrid(
        int width,
        int height,
        int verticalSize,
        IReadOnlyList<PrismSpawnCell> spawnCells,
        IReadOnlyList<PrismBlockCell> blocks)
    {
        Width = width;
        Height = height;
        VerticalSize = verticalSize;
        _spawnCells = spawnCells;
        Blocks = blocks;
        _feetZByColumn = new Dictionary<(int X, int Y), int>(spawnCells.Count);
        foreach (PrismSpawnCell cell in spawnCells)
        {
            _feetZByColumn[(cell.X, cell.Y)] = cell.FeetZ;
        }
    }

    public int Width { get; }

    public int Height { get; }

    public int VerticalSize { get; }

    /// <summary>Dimensions du monde SYNE, en unités de monde = blocs.</summary>
    public WorldSize Size => new(Width, Height);

    /// <summary>Cases d'incarnation possibles, en ordre canonique (y puis x).</summary>
    public IReadOnlyList<PrismSpawnCell> SpawnCells => _spawnCells;

    /// <summary>
    /// Blocs non aériens posés en surcharge (eau, arbres, minerais, constructions)
    /// dans l'ordre de l'instantané : c'est là que PRISM déclare ses sources — la
    /// surface seule ne suffit pas à connaître un point d'eau.
    /// </summary>
    public IReadOnlyList<PrismBlockCell> Blocks { get; }

    /// <summary>Altitude des pieds d'un agent sur cette colonne, si elle est praticable.</summary>
    public bool TryFeetZ(int x, int y, out int feetZ) =>
        _feetZByColumn.TryGetValue((x, y), out feetZ);

    /// <summary>
    /// Lit une enveloppe <c>world_snapshot</c>. Le flux publie
    /// <c>{ state, lastCompletedTick, seed, contentHash, error, world }</c> : la
    /// grille est sous <c>world</c> (un objet nu est aussi accepté). Échoue avec
    /// un motif explicite plutôt que de retomber sur des dimensions supposées.
    /// </summary>
    public static bool TryParse(JsonElement payload, out PrismWorldGrid? grid, out string? error)
    {
        grid = null;
        error = null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            error = "L'instantané de monde PRISM n'est pas un objet JSON.";
            return false;
        }

        if (payload.TryGetProperty("world", out JsonElement nested))
        {
            if (nested.ValueKind != JsonValueKind.Object)
            {
                error = "L'instantané de monde PRISM ne contient pas encore de monde généré.";
                return false;
            }

            payload = nested;
        }

        if (!TryReadInt(payload, "width", out int width) ||
            !TryReadInt(payload, "height", out int height) ||
            !TryReadInt(payload, "verticalSize", out int verticalSize) ||
            width <= 0 || height <= 0 || verticalSize <= 0)
        {
            error = "L'instantané de monde PRISM n'annonce pas de dimensions exploitables.";
            return false;
        }

        if (!payload.TryGetProperty("surfaceColumns", out JsonElement columns) ||
            columns.ValueKind != JsonValueKind.Array ||
            columns.GetArrayLength() != width * height)
        {
            error = "L'instantané de monde PRISM n'a pas une colonne de surface par case.";
            return false;
        }

        // Surcharges non aériennes indexées par (x, y, z) : elles bloquent la
        // case quand elles occupent les deux blocs au-dessus de la surface, et
        // elles portent le matériau — seule source des blocs d'eau, de minerai
        // et de bois du monde (la colonne de surface ne dit que le sol).
        var blocked = new HashSet<(int X, int Y, int Z)>();
        var blocks = new List<PrismBlockCell>();
        if (payload.TryGetProperty("overrides", out JsonElement overrides) &&
            overrides.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in overrides.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("key", out JsonElement key) ||
                    key.ValueKind != JsonValueKind.Object ||
                    !TryReadInt(key, "x", out int x) ||
                    !TryReadInt(key, "y", out int y) ||
                    !TryReadInt(key, "z", out int z))
                {
                    continue;
                }

                bool isAir = entry.TryGetProperty("value", out JsonElement value) &&
                             value.ValueKind == JsonValueKind.Object &&
                             value.TryGetProperty("isAir", out JsonElement air) &&
                             air.ValueKind == JsonValueKind.True;
                if (isAir)
                {
                    continue;
                }

                blocked.Add((x, y, z));
                if (value.ValueKind == JsonValueKind.Object &&
                    value.TryGetProperty("materialId", out JsonElement material) &&
                    material.ValueKind == JsonValueKind.Number &&
                    material.TryGetUInt16(out ushort materialId))
                {
                    blocks.Add(new PrismBlockCell(x, y, z, materialId));
                }
            }
        }

        var cells = new List<PrismSpawnCell>();
        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++, index++)
            {
                if (!TryReadInt(columns[index], "surfaceZ", out int surfaceZ))
                {
                    continue;
                }

                int feetZ = surfaceZ + 1;
                if (feetZ + 1 >= verticalSize)
                {
                    continue;
                }

                if (blocked.Contains((x, y, feetZ)) || blocked.Contains((x, y, feetZ + 1)))
                {
                    continue;
                }

                cells.Add(new PrismSpawnCell(x, y, feetZ));
            }
        }

        if (cells.Count == 0)
        {
            error = "Aucune case d'incarnation valide dans le monde PRISM.";
            return false;
        }

        grid = new PrismWorldGrid(width, height, verticalSize, cells, blocks);
        return true;
    }

    /// <summary>
    /// Choisit <paramref name="count"/> cases réparties sur le monde, en ordre
    /// canonique et sans hasard : deux runs sur le même instantané produisent la
    /// même affectation agent ↔ case.
    /// </summary>
    public IReadOnlyList<PrismSpawnCell> PlanSpawns(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return [];
        }

        if (count > _spawnCells.Count)
        {
            throw new InvalidOperationException(
                $"{count} agents demandés pour {_spawnCells.Count} cases d'incarnation valides.");
        }

        // Sélection à pas régulier dans l'ordre canonique : une case tous les
        // `candidates / count`, sans doublon et dispersée sur tout le monde.
        var plan = new PrismSpawnCell[count];
        int stride = _spawnCells.Count / count;
        for (int i = 0; i < count; i++)
        {
            plan[i] = _spawnCells[Math.Min(i * stride, _spawnCells.Count - 1)];
        }

        return plan;
    }

    private static bool TryReadInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out JsonElement found) &&
               found.ValueKind == JsonValueKind.Number &&
               found.TryGetInt32(out value);
    }
}
