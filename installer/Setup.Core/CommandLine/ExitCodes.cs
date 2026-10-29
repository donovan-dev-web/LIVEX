namespace Setup.Core.CommandLine;

public static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int PreflightFailed = 2;
    public const int InstallFailed = 3;
    public const int UninstallPartial = 4;
    public const int AlreadyInstalled = 5;
    public const int OptionalAssetUnavailable = 6;
}