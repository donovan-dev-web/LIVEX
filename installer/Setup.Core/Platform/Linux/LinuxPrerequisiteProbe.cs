namespace Setup.Core.Platform.Linux;

public sealed class LinuxPrerequisiteProbe : IPrerequisiteProbe
{
    public IReadOnlyList<string> Check()
    {
        var issues = new List<string>();
        // Vérifications minimales : libicu, libssl via ldconfig non implémenté ici.
        return issues;
    }
}