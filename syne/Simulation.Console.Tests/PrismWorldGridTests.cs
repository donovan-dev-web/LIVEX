using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismWorldGridTests
{
    private static JsonElement Snapshot(
        int width,
        int height,
        int verticalSize,
        int surfaceZ = 10,
        params (int X, int Y, int Z)[] blocked)
    {
        var columns = new object[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                columns[x + (width * y)] = new { surfaceZ, materialId = 1, state = 0, strataProfileId = 1 };
            }
        }

        var overrides = blocked
            .Select(cell => new
            {
                key = new { x = cell.X, y = cell.Y, z = cell.Z },
                value = new { materialId = 3, state = 0, isAir = false },
            })
            .ToArray();

        return JsonSerializer.SerializeToElement(
            new { width, height, verticalSize, surfaceColumns = columns, overrides },
            ProtocolJson.Options);
    }

    [Fact]
    public void TryParse_ReadsTheGridInBlocks()
    {
        Assert.True(PrismWorldGrid.TryParse(Snapshot(16, 16, verticalSize: 64), out PrismWorldGrid? grid, out string? error));

        Assert.Null(error);
        Assert.NotNull(grid);
        Assert.Equal(16, grid!.Width);
        Assert.Equal(16, grid.Height);
        Assert.Equal(64, grid.VerticalSize);
        Assert.Equal(new Simulation.Core.World.WorldSize(16, 16), grid.Size);
        Assert.Equal(256, grid.SpawnCells.Count);
        Assert.Equal(new PrismSpawnCell(0, 0, 11), grid.SpawnCells[0]);
    }

    [Fact]
    public void TryParse_UnwrapsTheWorldSnapshotEnvelope()
    {
        // Le flux /data/read publie { state, lastCompletedTick, seed,
        // contentHash, error, world } : la grille est sous « world ».
        JsonElement envelope = JsonSerializer.SerializeToElement(
            new
            {
                state = "running",
                lastCompletedTick = 12,
                seed = 7UL,
                contentHash = "abc",
                error = (string?)null,
                world = Snapshot(8, 8, verticalSize: 64),
            },
            ProtocolJson.Options);

        Assert.True(PrismWorldGrid.TryParse(envelope, out PrismWorldGrid? grid, out string? error));
        Assert.Null(error);
        Assert.Equal(8, grid!.Width);
        Assert.Equal(64, grid.SpawnCells.Count);
    }

    [Fact]
    public void TryParse_RejectsAnEnvelopeWithoutGeneratedWorld()
    {
        JsonElement envelope = JsonSerializer.SerializeToElement(
            new { state = "idle", lastCompletedTick = -1L, world = (object?)null },
            ProtocolJson.Options);

        Assert.False(PrismWorldGrid.TryParse(envelope, out _, out string? missingWorld));
        Assert.NotNull(missingWorld);
    }

    [Fact]
    public void TryParse_RejectsAMalformedSnapshot()
    {
        Assert.False(PrismWorldGrid.TryParse(default, out _, out string? notObject));
        Assert.NotNull(notObject);

        JsonElement noColumns = JsonSerializer.SerializeToElement(
            new { width = 16, height = 16, verticalSize = 64 },
            ProtocolJson.Options);
        Assert.False(PrismWorldGrid.TryParse(noColumns, out _, out string? missingColumns));
        Assert.NotNull(missingColumns);

        // Une colonne manquante rendrait l'indexation (x + width * y) fausse.
        JsonElement shortColumns = JsonSerializer.SerializeToElement(
            new
            {
                width = 16,
                height = 16,
                verticalSize = 64,
                surfaceColumns = new object[16],
            },
            ProtocolJson.Options);
        Assert.False(PrismWorldGrid.TryParse(shortColumns, out _, out string? truncated));
        Assert.NotNull(truncated);
    }

    [Fact]
    public void TryParse_SkipsCellsWhoseTwoBlocksAboveAreNotFree()
    {
        // L'eau est posée en surcharge au-dessus de la surface : elle bloque la
        // case, comme une construction.
        JsonElement payload = Snapshot(16, 16, verticalSize: 64, surfaceZ: 10, blocked: [(3, 4, 11), (5, 6, 12)]);

        Assert.True(PrismWorldGrid.TryParse(payload, out PrismWorldGrid? grid, out _));

        Assert.DoesNotContain(new PrismSpawnCell(3, 4, 11), grid!.SpawnCells);
        // La surcharge deux blocs au-dessus (12) occupe la tête de l'agent.
        Assert.DoesNotContain(new PrismSpawnCell(5, 6, 11), grid.SpawnCells);
        Assert.Equal(254, grid.SpawnCells.Count);
    }

    [Fact]
    public void TryParse_SkipsCellsWithoutHeadroomInsideTheWorld()
    {
        // Surface à 62 dans un monde de 64 : la tête (64) sort du monde, donc
        // plus aucune case — l'instantané est refusé plutôt que rendu vide.
        Assert.False(PrismWorldGrid.TryParse(
            Snapshot(4, 4, verticalSize: 64, surfaceZ: 62),
            out _,
            out string? noCell));
        Assert.NotNull(noCell);
    }

    [Fact]
    public void PlanSpawns_IsDeterministicSparseAndDistinct()
    {
        Assert.True(PrismWorldGrid.TryParse(Snapshot(16, 16, verticalSize: 64), out PrismWorldGrid? grid, out _));

        IReadOnlyList<PrismSpawnCell> plan = grid!.PlanSpawns(5);

        Assert.Equal(5, plan.Count);
        Assert.Equal(5, plan.Distinct().Count());
        Assert.Equal(plan, grid.PlanSpawns(5));
        // Ordre canonique croissant : pas de retour en arrière dans la grille.
        for (int i = 1; i < plan.Count; i++)
        {
            Assert.True(plan[i].Y > plan[i - 1].Y || (plan[i].Y == plan[i - 1].Y && plan[i].X > plan[i - 1].X));
        }
    }

    [Fact]
    public void PlanSpawns_RefusesMoreAgentsThanCells()
    {
        Assert.True(PrismWorldGrid.TryParse(Snapshot(2, 2, verticalSize: 64), out PrismWorldGrid? grid, out _));

        Assert.Throws<InvalidOperationException>(() => grid!.PlanSpawns(5));
        Assert.Empty(grid!.PlanSpawns(0));
        Assert.Equal(4, grid.PlanSpawns(4).Count);
    }

    [Fact]
    public void Incarnation_BuildsOneIncarnerCommandPerAgentInIdOrder()
    {
        IReadOnlyList<CommandEnvelope> commands = PrismIncarnation.Build(
            7,
            [
                (42UL, new PrismSpawnCell(10, 20, 11)),
                (7UL, new PrismSpawnCell(3, 4, 5)),
            ]);

        Assert.Equal(2, commands.Count);
        Assert.Equal(7UL, commands[0].AgentId);
        Assert.Equal(42UL, commands[1].AgentId);
        Assert.All(commands, command => Assert.Equal("Incarner", command.Kind));
        Assert.All(commands, command => Assert.Equal(7L, command.Tick));
        Assert.Equal("syne:7:7:Incarner", commands[0].CommandId);

        JsonElement position = commands[1].Payload.GetProperty("position");
        Assert.Equal(10, position.GetProperty("x").GetInt32());
        Assert.Equal(20, position.GetProperty("y").GetInt32());
        Assert.Equal(11, position.GetProperty("z").GetInt32());
    }
}
