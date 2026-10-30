namespace Setup.Core.Platform;

public interface IPrerequisiteProbe
{
    IReadOnlyList<string> Check();
}