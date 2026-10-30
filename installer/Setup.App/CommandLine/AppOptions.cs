// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.CommandLine;
using Setup.Core.Simulation;

namespace Setup.App.CommandLine;

/// <summary>
/// Options de l'assistant. Les options communes restent dans
/// <see cref="CommandLineOptions"/> (Setup.Core) ; le mode fictif et la langue
/// sont propres à l'interface.
/// </summary>
public sealed class AppOptions
{
    public CommandLineOptions Core { get; init; } = new();

    public bool Demo { get; init; }

    public DemoScenario DemoScenario { get; init; } = DemoScenario.FreshInstall;

    public string Language { get; init; } = "fr";

    public bool Verbose { get; init; }

    /// <summary>Confirmation explicite de la suppression de l'espace de travail (portée C).</summary>
    public bool ConfirmWorkspace { get; init; }

    /// <summary>
    /// Options non reconnues, dans l'ordre de la ligne de commande. Une option
    /// inconnue est une erreur d'usage (code 1) : elle ne doit jamais être
    /// ignorée en silence ni ouvrir l'assistant à la place d'une commande.
    /// </summary>
    public IReadOnlyList<string> Unknown { get; init; } = Array.Empty<string>();

    public bool Check => Core.Check;

    public bool Version => Core.Version;

    public bool Help => Core.Help;

    public bool NoNetwork => Core.NoNetwork;

    public static AppOptions Parse(string[] args)
    {
        var core = new CommandLineOptions();
        var demo = false;
        var scenario = DemoScenario.FreshInstall;
        var language = "fr";
        var verbose = Environment.GetEnvironmentVariable("LIVEX_SETUP_VERBOSE") == "1";
        var confirmWorkspace = false;
        var unknown = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var (name, inlineValue) = Split(arg);

            switch (name)
            {
                case "--check": core.Check = true; break;
                case "--version": core.Version = true; break;
                case "--help" or "-h" or "/?": core.Help = true; break;
                case "--unattended": core.Unattended = true; break;
                case "--uninstall": core.Uninstall = true; break;
                case "--repair": core.Repair = true; break;
                case "--maintenance": core.Maintenance = true; break;
                case "--purge-research": core.PurgeResearch = true; break;
                case "--confirm-workspace": confirmWorkspace = true; break;
                case "--no-network": core.NoNetwork = true; break;
                case "--demo": demo = true; break;
                case "--verbose": verbose = true; break;
                case "--test": demo = true; core.Test = true; break;
                case "--demo-scenario":
                {
                    var value = Value(inlineValue, args, ref i);
                    var parsed = ParseScenario(value);
                    if (parsed is null)
                    {
                        unknown.Add($"--demo-scenario {value ?? "(vide)"}");
                    }
                    else
                    {
                        scenario = parsed.Value;
                    }

                    demo = true;
                    break;
                }
                case "--lang":
                    language = Value(inlineValue, args, ref i) ?? "fr";
                    break;
                case "--config":
                    core.ConfigPath = Value(inlineValue, args, ref i);
                    break;
                case "--scope":
                {
                    var value = Value(inlineValue, args, ref i)?.ToLowerInvariant();
                    if (value is "a" or "b" or "c")
                    {
                        core.Scope = value;
                    }
                    else
                    {
                        unknown.Add($"--scope {value ?? "(vide)"}");
                    }

                    break;
                }
                case "--offline-assets":
                    core.OfflineAssetsDir = Value(inlineValue, args, ref i);
                    break;
                default:
                    if (name.StartsWith('-'))
                    {
                        unknown.Add(name);
                    }

                    break;
            }
        }

        core.Language = language;
        core.Headless = core.Check || core.Unattended || core.Uninstall || core.Repair;

        return new AppOptions
        {
            Core = core,
            Demo = demo,
            DemoScenario = scenario,
            Language = language,
            Verbose = verbose,
            ConfirmWorkspace = confirmWorkspace,
            Unknown = unknown,
        };
    }

    /// <summary>Scénario nommé, ou <c>null</c> si le nom n'est pas reconnu (erreur d'usage).</summary>
    private static DemoScenario? ParseScenario(string? value) => value?.ToLowerInvariant() switch
    {
        null or "" => null,
        "fresh" or "neuve" or "nouvelle" => DemoScenario.FreshInstall,
        "existing" or "existante" or "maintenance" => DemoScenario.ExistingInstall,
        "failure" or "echec" or "échec" => DemoScenario.FailureDuringWrite,
        "prism" or "prism-absent" or "sans-prism" => DemoScenario.PrismAbsent,
        _ => null,
    };

    private static (string Name, string? Value) Split(string arg)
    {
        var index = arg.IndexOf('=');
        return index < 0 ? (arg, null) : (arg[..index], arg[(index + 1)..]);
    }

    private static string? Value(string? inline, string[] args, ref int index)
    {
        if (inline is not null)
        {
            return inline;
        }

        return index + 1 < args.Length ? args[++index] : null;
    }
}
