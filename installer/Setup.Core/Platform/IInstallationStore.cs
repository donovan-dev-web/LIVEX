using Setup.Core.Lifecycle;

namespace Setup.Core.Platform;

public interface IInstallationStore
{
    InstallationRecord? Read();
    void Write(InstallationRecord record);
    void Delete();
    string StorePath { get; }
}