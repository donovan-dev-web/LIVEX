namespace Prism.Bots;

/// <summary>
/// Incarnates an agent and never commands anything. Used to measure the cost of a tick that has
/// perception but no commands, without a second variable in the measurement.
/// </summary>
public sealed class NoOpBot : IPrismBot
{
    public string Kind => "idle";

    public IReadOnlyList<BotCommand> Decide(BotSenses senses) => [];
}
