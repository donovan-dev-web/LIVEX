namespace Setup.Core.CommandLine;

public sealed class CommandLineOptions
{
    public bool Check { get; set; }
    public bool Unattended { get; set; }
    public string? ConfigPath { get; set; }
    public bool Uninstall { get; set; }
    public string Scope { get; set; } = "a";
    public bool PurgeResearch { get; set; }
    public bool Repair { get; set; }
    public bool Maintenance { get; set; }
    public bool Version { get; set; }
    public string? OfflineAssetsDir { get; set; }
    public bool NoNetwork { get; set; }
    public bool Test { get; set; }
    public string? LogPath { get; set; }
    public string? Language { get; set; }
    public bool Headless { get; set; }
    public bool Help { get; set; }
}