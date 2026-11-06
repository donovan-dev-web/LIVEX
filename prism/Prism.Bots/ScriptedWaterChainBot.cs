using System.Text.Json;
using Prism.Contracts;

namespace Prism.Bots;

/// <summary>
/// Scripted bot for the water chain (PRISM-096): mines stone, builds a well, then drinks
/// from its <c>water_access</c> volume. Drives the chain through the contract vocabulary.
/// </summary>
public sealed class ScriptedWaterChainBot : IPrismBot
{
    private readonly IReadOnlyList<BotCommand> _script;
    private int _index;

    public ScriptedWaterChainBot(IReadOnlyList<BotCommand> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script;
    }

    public string Kind => "scripted_water_chain";

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

    public static ScriptedWaterChainBot CreateMinimalWaterChain()
    {
        var commands = new List<BotCommand>();
        commands.Add(BotCommand.Incarnate(new BotCoordinate(10, 0, 5)));

        // Mine twelve stones for the well.
        for (var x = 11; x <= 22; x++)
        {
            commands.Add(BotCommand.Mine(new BotCoordinate(x, 0, 5)));
        }

        // Build the well.
        commands.Add(BotCommand.StartConstruction("well-1", "well", new BotCoordinate(10, 0, 5)));
        for (var i = 0; i < 100; i++)
        {
            commands.Add(BotCommand.Contribute("well-1"));
        }

        // Drink from the well volume.
        commands.Add(BotCommand.Drink(new BotCoordinate(11, 0, 5)));

        return new ScriptedWaterChainBot(commands);
    }
}