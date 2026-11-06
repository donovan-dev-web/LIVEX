using Prism.Content;

namespace Prism.Bots;

/// <summary>
/// Scripted bot for the storage chain (PRISM-098): mines logs, crafts planks and a chest,
/// builds a granary, places the chest inside its volume and stores a plank in it.
/// </summary>
public sealed class ScriptedStorageChainBot : IPrismBot
{
    private readonly IReadOnlyList<BotCommand> _script;
    private int _index;

    public ScriptedStorageChainBot(IReadOnlyList<BotCommand> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script;
    }

    public string Kind => "scripted_storage_chain";

    public IReadOnlyList<BotCommand> Decide(BotSenses senses)
    {
        ArgumentNullException.ThrowIfNull(senses);
        if (_index >= _script.Count)
        {
            return [];
        }

        var command = _script[_index];
        _index++;
        return [command];
    }

    public void Reset() => _index = 0;

    public static ScriptedStorageChainBot CreateMinimalStorageChain(BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var commands = new List<BotCommand>();
        commands.Add(BotCommand.Incarnate(new BotCoordinate(30, 0, 5)));

        // Mine eight logs.
        for (var x = 31; x <= 38; x++)
        {
            commands.Add(BotCommand.Mine(new BotCoordinate(x, 0, 5)));
        }

        var plankRecipe = content.RecipeId("make_plank");
        var chestRecipe = content.RecipeId("craft_chest");
        var plankItem = content.ItemId("plank");
        var chestItem = content.ItemId("chest");
        ushort chestMaterial = 0;
        content.TryPlacedMaterialId(chestItem, out chestMaterial);

        // Craft sixteen planks and one chest.
        for (var i = 0; i < 4; i++)
        {
            commands.Add(BotCommand.Craft(plankRecipe));
        }
        commands.Add(BotCommand.Craft(chestRecipe));

        // Build the granary.
        commands.Add(BotCommand.StartConstruction("granary-1", "granary", new BotCoordinate(30, 0, 5)));
        for (var i = 0; i < 150; i++)
        {
            commands.Add(BotCommand.Contribute("granary-1"));
        }

        // Place the chest inside the granary volume and store a plank in it.
        commands.Add(BotCommand.Place(new BotCoordinate(31, 0, 5), chestItem, chestMaterial));
        commands.Add(BotCommand.Deposit(new BotCoordinate(31, 0, 5), plankItem, 1));

        return new ScriptedStorageChainBot(commands);
    }
}