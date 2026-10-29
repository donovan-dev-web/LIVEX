namespace Setup.Core.Platform.Windows;

public sealed class WindowsPrerequisiteProbe : IPrerequisiteProbe
{
    public IReadOnlyList<string> Check() => Array.Empty<string>();
}