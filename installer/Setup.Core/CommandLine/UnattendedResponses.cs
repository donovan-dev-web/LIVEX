// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Setup.Core.Distribution;
using Setup.Core.Execution;

namespace Setup.Core.CommandLine;

/// <summary>
/// Réponses fournies à <c>--unattended --config &lt;fichier&gt;</c>. Elles
/// reprennent les champs de l'assistant (ARCHITECTURE §18) : aucun chemin n'est
/// deviné, aucune valeur par défaut implicite n'est appliquée aux répertoires.
/// </summary>
public sealed record UnattendedResponses(
    string InstallDir,
    string WorkspaceDir,
    string DataRoot,
    IReadOnlyList<string> Components,
    bool AcceptLicense,
    bool CreateDesktopShortcut,
    bool LaunchAfterInstall,
    string? OfflineAssetsDir,
    bool NoNetwork)
{
    /// <summary>Construit la requête d'installation correspondante.</summary>
    public InstallRequest ToRequest(DistributionManifest manifest, string? payloadRoot = null) =>
        new(
            manifest,
            InstallDir,
            DataRoot,
            WorkspaceDir,
            Components,
            AcceptLicense,
            CreateDesktopShortcut,
            LaunchAfterInstall,
            DryRun: false,
            OfflineAssetsDir: OfflineAssetsDir,
            NoNetwork: NoNetwork,
            PayloadRoot: payloadRoot);
}

/// <summary>
/// Lecture et validation du fichier de réponses. Toute valeur manquante ou
/// invalide est une erreur explicite : le mode non interactif ne complète
/// jamais un champ à la place de l'opérateur.
/// </summary>
public static class UnattendedResponsesFile
{
    /// <summary>Charge le fichier ; en cas d'échec, <paramref name="errors"/> en donne la raison.</summary>
    public static bool TryLoad(
        string path,
        DistributionManifest manifest,
        string defaultDataRoot,
        bool noNetwork,
        out UnattendedResponses? responses,
        out IReadOnlyList<string> errors)
    {
        responses = null;
        var problems = new List<string>();

        if (!File.Exists(path))
        {
            errors = new[] { $"Fichier de réponses introuvable : {path}" };
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            errors = new[] { $"Fichier de réponses illisible : {ex.Message}" };
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors = new[] { "Fichier de réponses : un objet JSON est attendu à la racine." };
                return false;
            }

            var installDir = RequiredString(root, "installDir", problems);
            var workspaceDir = RequiredString(root, "workspaceDir", problems);
            var dataRoot = OptionalString(root, "dataRoot") ?? defaultDataRoot;
            var offlineAssets = OptionalString(root, "offlineAssetsDir");

            if (installDir is not null && !Path.IsPathFullyQualified(installDir))
            {
                problems.Add("installDir doit être un chemin absolu.");
            }

            if (workspaceDir is not null && !Path.IsPathFullyQualified(workspaceDir))
            {
                problems.Add("workspaceDir doit être un chemin absolu.");
            }

            var acceptLicense = OptionalBool(root, "acceptLicense") ?? false;
            if (!acceptLicense)
            {
                problems.Add("acceptLicense doit valoir true : la licence ne peut pas être acceptée à la place de l'utilisateur.");
            }

            var components = ReadComponents(root, manifest, problems);

            if (problems.Count > 0)
            {
                errors = problems;
                return false;
            }

            responses = new UnattendedResponses(
                installDir!,
                workspaceDir!,
                dataRoot,
                components,
                acceptLicense,
                OptionalBool(root, "desktopShortcut") ?? false,
                OptionalBool(root, "launchAfterInstall") ?? false,
                offlineAssets,
                noNetwork);
            errors = Array.Empty<string>();
            return true;
        }
    }

    /// <summary>Composants demandés, complétés par les composants obligatoires du manifeste.</summary>
    private static IReadOnlyList<string> ReadComponents(
        JsonElement root,
        DistributionManifest manifest,
        ICollection<string> problems)
    {
        var known = manifest.Components.Select(component => component.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mandatory = manifest.Components
            .Where(component => component.Mandatory)
            .Select(component => component.Id)
            .ToList();

        var requested = new List<string>();
        if (root.TryGetProperty("components", out var element) && element.ValueKind != JsonValueKind.Null)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                problems.Add("components doit être un tableau d'identifiants.");
            }
            else
            {
                foreach (var entry in element.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.String)
                    {
                        problems.Add("components ne doit contenir que des chaînes.");
                        continue;
                    }

                    var id = entry.GetString()!;
                    if (!known.Contains(id))
                    {
                        problems.Add($"Composant inconnu dans le manifeste : {id}");
                        continue;
                    }

                    if (!requested.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        requested.Add(id);
                    }
                }
            }
        }

        foreach (var id in mandatory)
        {
            if (!requested.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                requested.Add(id);
            }
        }

        return requested;
    }

    private static string? RequiredString(JsonElement root, string name, ICollection<string> problems)
    {
        var value = OptionalString(root, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{name} est obligatoire et ne peut pas être vide.");
            return null;
        }

        return value;
    }

    private static string? OptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool? OptionalBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;
}
