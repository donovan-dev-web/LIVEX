using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Bots;

/// <summary>
/// A bot that plays a fixed, hand-authored sequence of commands. It is the "scripted bot"
/// demonstration for PRISM-094: it drives the bare-hands to iron-pickaxe tool chain through the
/// contract vocabulary, one command per tick, so the chain can be replayed and fingerprinted by
/// the bot runner. Unlike the planner, it does not react to perception; it exists to exercise
/// and document the chain end to end.
/// </summary>
public sealed class ScriptedToolChainBot : IPrismBot
{
    private readonly IReadOnlyList<BotCommand> _script;
    private int _index;

    public ScriptedToolChainBot(IReadOnlyList<BotCommand> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script;
    }

    public string Kind => "scripted_tool_chain";

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

    /// <summary>Resets the script so the bot can be replayed deterministically.</summary>
    public void Reset() => _index = 0;

    /// <summary>Builds the canonical bare-hands to iron-pickaxe script for the minimal pack.</summary>
    public static ScriptedToolChainBot CreateMinimalToolChain(
        ushort logMaterialId,
        ushort stoneMaterialId,
        ushort ironOreMaterialId,
        ushort workbenchItemId,
        ushort workbenchMaterialId,
        ushort furnaceItemId,
        ushort furnaceMaterialId,
        ushort stonePickaxeRecipeId,
        ushort ironPickaxeRecipeId,
        ushort plankRecipeId,
        ushort handleRecipeId,
        ushort stoneHeadRecipeId,
        ushort workbenchRecipeId,
        ushort furnaceRecipeId,
        ushort smeltIronRecipeId)
    {
        var commands = new List<BotCommand>();

        // Mine three logs and three stone blocks (bare hands).
        for (var x = 1; x <= 3; x++)
        {
            commands.Add(BotCommand.Mine(new BotCoordinate(x, 0, 5)));
        }

        for (var x = 4; x <= 6; x++)
        {
            commands.Add(BotCommand.Mine(new BotCoordinate(x, 0, 5)));
        }

        // Craft the stone pickaxe on a workbench.
        commands.Add(BotCommand.Craft(plankRecipeId));
        commands.Add(BotCommand.Craft(plankRecipeId));
        commands.Add(BotCommand.Craft(plankRecipeId));
        commands.Add(BotCommand.Craft(handleRecipeId));
        commands.Add(BotCommand.Craft(stoneHeadRecipeId));
        commands.Add(BotCommand.Craft(workbenchRecipeId));
        commands.Add(BotCommand.Place(new BotCoordinate(6, 0, 5), workbenchItemId, workbenchMaterialId));
        commands.Add(BotCommand.Craft(stonePickaxeRecipeId, new BotCoordinate(6, 0, 5)));

        // Mine iron ore with the stone pickaxe.
        for (var x = 7; x <= 9; x++)
        {
            commands.Add(BotCommand.Mine(new BotCoordinate(x, 0, 5)));
        }

        // Smelt iron and craft the iron pickaxe.
        commands.Add(BotCommand.Craft(furnaceRecipeId));
        commands.Add(BotCommand.Place(new BotCoordinate(7, 0, 5), furnaceItemId, furnaceMaterialId));
        commands.Add(BotCommand.Craft(smeltIronRecipeId, new BotCoordinate(7, 0, 5)));
        commands.Add(BotCommand.Craft(smeltIronRecipeId, new BotCoordinate(7, 0, 5)));
        commands.Add(BotCommand.Craft(smeltIronRecipeId, new BotCoordinate(7, 0, 5)));
        commands.Add(BotCommand.Craft(handleRecipeId));
        commands.Add(BotCommand.Craft(ironPickaxeRecipeId, new BotCoordinate(6, 0, 5)));

        return new ScriptedToolChainBot(commands);
    }
}