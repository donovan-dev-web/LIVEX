using Setup.Core.Platform.Linux;
using Setup.Core.Platform.Windows;

namespace Setup.Core.Platform;

public static class PlatformServices
{
    public static IPathPolicy CreatePathPolicy()
        => OperatingSystem.IsWindows() ? new WindowsPathPolicy() : new LinuxPathPolicy();

    public static IProcessProbe CreateProcessProbe()
        => OperatingSystem.IsWindows() ? new WindowsProcessProbe() : new LinuxProcessProbe();

    public static IInstallationStore CreateInstallationStore(string dataRoot)
        => OperatingSystem.IsWindows()
            ? new WindowsInstallationStore()
            : new LinuxInstallationStore(dataRoot);

    public static IIntegration CreateIntegration()
        => OperatingSystem.IsWindows() ? new WindowsIntegration() : new LinuxIntegration();

    public static IPrerequisiteProbe CreatePrerequisiteProbe()
        => OperatingSystem.IsWindows() ? new WindowsPrerequisiteProbe() : new LinuxPrerequisiteProbe();

    public static IFileSync CreateFileSync()
        => OperatingSystem.IsWindows() ? new WindowsFileSync() : new WindowsFileSync(); // Flush(true) works on both

    public static IHttpDownloader CreateHttpDownloader()
        => new WindowsHttpDownloader();

    public static IArchiveExtractor CreateArchiveExtractor()
        => new WindowsArchiveExtractor();
}