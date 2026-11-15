// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du write-back de production (SYNE-223) : ObtenirObjet / FabriquerMacro / Poser.

using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismProductionCommandMapperTests
{
    private static PrismItemCatalog ItemsWithPlaces()
    {
        string root = Path.Combine(Path.GetTempPath(), "syne-prism-items-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "items"));
        File.WriteAllText(
            Path.Combine(root, "items", "base.json"),
            """
            [
              { "id": "workbench", "tags": ["station"], "places": "workbench" },
              { "id": "plank", "tags": ["wood"] }
            ]
            """);
        try
        {
            return PrismItemCatalog.Load(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Map_Gather_TargetsAnItemByContentName()
    {
        CommandEnvelope command = PrismProductionCommandMapper.Map(
            7, 3, "id", new GatherProductionCommand("log", 2), null, PrismItemCatalog.Empty)!;

        Assert.Equal("ObtenirObjet", command.Kind);
        Assert.Equal(3UL, command.AgentId);
        Assert.Equal("log", command.Payload.GetProperty("item").GetString());
        Assert.Equal(2, command.Payload.GetProperty("quantity").GetInt32());
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
    }

    [Fact]
    public void IsCompletion_AMacroAcceptanceIsNotItsEnd()
    {
        // PRISM publie l'acceptation d'une macro-commande comme `action_completed`
        // avec `result.started` : la confondre avec la fin faisait avancer le plan
        // pendant que la récolte marchait encore (`input_missing`,
        // `action_already_active`).
        var gather = new GatherProductionCommand("log", 1);
        JsonElement started = JsonSerializer.SerializeToElement(
            new { commandId = "c", reason = (string?)null, result = new { started = true, result = "Move" } },
            Prism.Contracts.ProtocolJson.Options);
        JsonElement completed = JsonSerializer.SerializeToElement(
            new { commandId = "c", reason = (string?)null, result = new { completed = true, quantityAcquired = 1 } },
            Prism.Contracts.ProtocolJson.Options);

        Assert.False(PrismProductionCommandMapper.IsCompletion("action_completed", started, gather));
        Assert.True(PrismProductionCommandMapper.IsCompletion("action_completed", completed, gather));
        Assert.False(PrismProductionCommandMapper.IsCompletion("action_started", completed, gather));
        Assert.False(PrismProductionCommandMapper.IsCompletion("action_failed", completed, gather));
    }

    [Fact]
    public void IsCompletion_AnInstantActionEndsOnItsDispatchReply()
    {
        // `Poser` n'a pas de phase `started` : sa réponse de dispatch est sa fin.
        var place = new PlaceProductionCommand("workbench");
        JsonElement accepted = JsonSerializer.SerializeToElement(
            new { commandId = "c", reason = (string?)null, result = new { } },
            Prism.Contracts.ProtocolJson.Options);

        Assert.True(PrismProductionCommandMapper.IsCompletion("action_completed", accepted, place));
        Assert.False(PrismProductionCommandMapper.IsCompletion(
            "action_completed",
            JsonSerializer.SerializeToElement(
                new { commandId = "c", reason = (string?)null, result = new { started = true } },
                Prism.Contracts.ProtocolJson.Options),
            place));
    }

    [Fact]
    public void Map_CraftWithoutStation_SendsTheRecipeOnly()
    {
        CommandEnvelope command = PrismProductionCommandMapper.Map(
            7, 3, "id", new CraftProductionCommand("make_plank", null), null, PrismItemCatalog.Empty)!;

        Assert.Equal("FabriquerMacro", command.Kind);
        Assert.Equal("make_plank", command.Payload.GetProperty("recipe").GetString());
        Assert.False(command.Payload.TryGetProperty("station", out _));
    }

    [Fact]
    public void Map_CraftAtStation_SendsThePlacedCell()
    {
        CommandEnvelope command = PrismProductionCommandMapper.Map(
            7, 3, "id", new CraftProductionCommand("craft_stone_pickaxe", "workbench"),
            new PrismBlockPosition(11, 22, 56), PrismItemCatalog.Empty)!;

        Assert.Equal("FabriquerMacro", command.Kind);
        JsonElement station = command.Payload.GetProperty("station");
        Assert.Equal(11, station.GetProperty("x").GetInt32());
        Assert.Equal(22, station.GetProperty("y").GetInt32());
        Assert.Equal(56, station.GetProperty("z").GetInt32());
    }

    [Fact]
    public void Map_CraftAtUnknownStation_IsNotIssued()
    {
        Assert.Null(PrismProductionCommandMapper.Map(
            7, 3, "id", new CraftProductionCommand("craft_stone_pickaxe", "workbench"), null, PrismItemCatalog.Empty));
    }

    [Fact]
    public void Map_Place_UsesTheItemPlacesMaterial()
    {
        PrismItemCatalog catalog = ItemsWithPlaces();

        CommandEnvelope command = PrismProductionCommandMapper.Map(
            7, 3, "id", new PlaceProductionCommand("workbench"),
            new PrismBlockPosition(4, 5, 40), catalog)!;

        Assert.Equal("Poser", command.Kind);
        JsonElement target = command.Payload.GetProperty("target");
        Assert.Equal(4, target.GetProperty("x").GetInt32());
        Assert.Equal(5, target.GetProperty("y").GetInt32());
        Assert.Equal(40, target.GetProperty("z").GetInt32());
        Assert.Equal("workbench", command.Payload.GetProperty("item").GetString());
        Assert.Equal("workbench", command.Payload.GetProperty("material").GetString());
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
    }

    [Fact]
    public void CommandId_IsDeterministicAndDistinctPerStep()
    {
        ProductionCommand gather = new GatherProductionCommand("log", 1);
        ProductionCommand craft = new CraftProductionCommand("make_plank", null);

        Assert.Equal(
            PrismProductionCommandMapper.CommandId(12, 4, 2, gather),
            PrismProductionCommandMapper.CommandId(12, 4, 2, gather));
        Assert.NotEqual(
            PrismProductionCommandMapper.CommandId(12, 4, 2, gather),
            PrismProductionCommandMapper.CommandId(12, 4, 3, craft));
        Assert.Equal("syne:12:4:prod:2:gather-log", PrismProductionCommandMapper.CommandId(12, 4, 2, gather));
    }
}
