namespace Setup.Core.Platform;

public interface IProcessProbe
{
    bool IsProcessRunningFrom(string directory);
    IReadOnlyList<int> GetProcessIdsFrom(string directory);
}