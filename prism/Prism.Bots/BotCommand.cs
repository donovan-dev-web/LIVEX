using System.Text.Json;
using Prism.Contracts;

namespace Prism.Bots;

/// <summary>
/// One intent a bot wants the world to execute. The payload is already the contract payload:
/// bots speak the same command vocabulary as any other client, they get no private shortcut.
/// </summary>
public sealed record BotCommand(string Kind, JsonElement Payload)
{
    public static BotCommand Incarnate(BotCoordinate position, int? carrySlots = null)
    {
        var payload = carrySlots is null
            ? JsonSerializer.SerializeToElement(
                new { position = Coordinates(position) },
                ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(
                new { position = Coordinates(position), profile = new { carrySlots } },
                ProtocolJson.Options);
        return new BotCommand("Incarner", payload);
    }

    public static BotCommand Retire() =>
        new("Retirer", JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

    public static BotCommand MoveTo(BotCoordinate target) =>
        new("Aller", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target) },
            ProtocolJson.Options));

    public static BotCommand Mine(BotCoordinate target) =>
        new("Miner", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target) },
            ProtocolJson.Options));

    public static BotCommand Till(BotCoordinate target) =>
        new("Labourer", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target) },
            ProtocolJson.Options));

    public static BotCommand Harvest(BotCoordinate target) =>
        new("Recolter", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target) },
            ProtocolJson.Options));

    public static BotCommand Plant(BotCoordinate target, ushort seedItemTypeId) =>
        new("Planter", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target), seed = seedItemTypeId },
            ProtocolJson.Options));

    public static BotCommand Deposit(BotCoordinate target, ushort itemTypeId, int quantity) =>
        new("Deposer", JsonSerializer.SerializeToElement(
            new { container = Coordinates(target), item = itemTypeId, quantity },
            ProtocolJson.Options));

    public static BotCommand Consume(ushort itemTypeId) =>
        new("Consommer", JsonSerializer.SerializeToElement(
            new { item = itemTypeId },
            ProtocolJson.Options));

    public static BotCommand Drink(BotCoordinate target) =>
        new("Consommer", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target) },
            ProtocolJson.Options));

    public static BotCommand Place(BotCoordinate target, ushort itemTypeId, ushort materialId) =>
        new("Poser", JsonSerializer.SerializeToElement(
            new { target = Coordinates(target), item = itemTypeId, material = materialId },
            ProtocolJson.Options));

    public static BotCommand Craft(ushort recipeId, BotCoordinate? station = null)
    {
        var payload = station is null
            ? JsonSerializer.SerializeToElement(new { recipe = recipeId }, ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(
                new { recipe = recipeId, station = Coordinates(station.Value) },
                ProtocolJson.Options);
        return new BotCommand("Fabriquer", payload);
    }

    public static BotCommand ObtainObject(ushort itemTypeId, int quantity, BotCoordinate hint, int hintRadius) =>
        new("ObtenirObjet", JsonSerializer.SerializeToElement(
            new { item = itemTypeId, quantity, hint = Coordinates(hint), hintRadius },
            ProtocolJson.Options));

    public static BotCommand ObtainTag(string tagId, int quantity, BotCoordinate hint, int hintRadius) =>
        new("ObtenirObjet", JsonSerializer.SerializeToElement(
            new { tag = tagId, quantity, hint = Coordinates(hint), hintRadius },
            ProtocolJson.Options));

    public static BotCommand CraftMacro(ushort recipeId, BotCoordinate? station = null)
    {
        var payload = station is null
            ? JsonSerializer.SerializeToElement(new { recipe = recipeId }, ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(
                new { recipe = recipeId, station = Coordinates(station.Value) },
                ProtocolJson.Options);
        return new BotCommand("FabriquerMacro", payload);
    }

    public static BotCommand Cultivate(BotCoordinate parcel, ushort seedItemTypeId) =>
        new("Cultiver", JsonSerializer.SerializeToElement(
            new { parcel = Coordinates(parcel), seed = seedItemTypeId },
            ProtocolJson.Options));

    public static BotCommand Cancel() =>
        new("Annuler", JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options));

    public static BotCommand StartConstruction(string siteId, string templateId, BotCoordinate position) =>
        new("DemarrerChantier", JsonSerializer.SerializeToElement(
            new { siteId, templateId, position = Coordinates(position) },
            ProtocolJson.Options));

    public static BotCommand Contribute(string siteId) =>
        new("Contribuer", JsonSerializer.SerializeToElement(
            new { siteId },
            ProtocolJson.Options));

    private static object Coordinates(BotCoordinate coordinate) =>
        new { x = coordinate.X, y = coordinate.Y, z = coordinate.Z };
}

public interface IPrismBot
{
    /// <summary>Stable identifier used in command ids and golden runs.</summary>
    string Kind { get; }

    /// <summary>Decides the commands for the current tick. Must be deterministic.</summary>
    IReadOnlyList<BotCommand> Decide(BotSenses senses);
}
