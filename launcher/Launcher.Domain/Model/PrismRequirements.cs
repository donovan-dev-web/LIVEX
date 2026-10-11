// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Exigences publiées de PRISM vérifiables sans le démarrer.

using Launcher.Protocol.Model;

namespace Launcher.Domain.Model;

/// <summary>
/// Exigences publiées de PRISM vérifiables sans le démarrer.
/// Le verrou du mode Immersion est une condition évaluée au manifeste, jamais une
/// constante de code. Toute non-conformité produit la
/// cause exacte affichée à l'utilisateur.
/// </summary>
public static class PrismRequirements
{
    /// <summary>
    /// Vérifie le manifeste de PRISM. Renvoie la liste des causes d'échec, vide si le
    /// manifeste satisfait le contrat (mode déclaré, snapshotStream, renderCadence, contrôle).
    /// </summary>
    public static IReadOnlyList<string> Validate(ComponentManifest manifest)
    {
        var problems = new List<string>();

        var carriesMode = string.Equals(manifest.Type, "immersion", StringComparison.Ordinal)
            || manifest.ContributesTo?.Any(value => string.Equals(value, "immersion", StringComparison.Ordinal)) == true;
        if (!carriesMode)
        {
            problems.Add("PRISM ne se déclare pas porteur du mode Immersion");
        }

        if (!manifest.Capabilities.Contains("snapshotStream", StringComparer.Ordinal))
        {
            problems.Add("capacité « snapshotStream » absente du manifeste");
        }

        if (!manifest.Capabilities.Contains("renderCadence", StringComparer.Ordinal))
        {
            problems.Add("capacité « renderCadence » absente du manifeste");
        }

        var control = manifest.Endpoints?.GetValueOrDefault("control");
        if (control is null || control.Enabled == false)
        {
            problems.Add("point d'accès de contrôle non déclaré");
        }

        return problems;
    }
}
