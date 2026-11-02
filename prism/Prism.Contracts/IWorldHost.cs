namespace Prism.Contracts;

public interface IWorldHost
{
    TickResult Step(TickRequest request);
}
