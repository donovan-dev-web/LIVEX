// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Distribution;

/// <summary>
/// Règles de cohérence du manifeste (ARCHITECTURE §4.2). Elles sont vérifiées
/// à la lecture, après la vérification de signature, et avant tout plan.
/// </summary>
public static class ManifestValidator
{
    private static readonly string[] AcquisitionKinds = { "payload", "release-asset", "unavailable" };

    public static IReadOnlyList<string> Validate(DistributionManifest manifest)
    {
        var issues = new List<string>();

        if (manifest.Schema <= 0) issues.Add("schéma de manifeste absent");
        if (string.IsNullOrWhiteSpace(manifest.Product)) issues.Add("produit absent");
        if (string.IsNullOrWhiteSpace(manifest.Version)) issues.Add("version absente");
        if (manifest.Components.Count == 0) issues.Add("aucun composant déclaré");

        var componentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in manifest.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Id))
            {
                issues.Add("composant sans identifiant");
                continue;
            }

            if (!componentIds.Add(component.Id)) issues.Add($"identifiant de composant dupliqué : {component.Id}");
            if (string.IsNullOrWhiteSpace(component.NameFr) || string.IsNullOrWhiteSpace(component.NameEn))
                issues.Add($"libellé manquant : {component.Id}");
            if (!AcquisitionKinds.Contains(component.Acquisition.Kind, StringComparer.Ordinal))
                issues.Add($"mode d'acquisition inconnu pour {component.Id} : {component.Acquisition.Kind}");

            switch (component.Acquisition.Kind)
            {
                case "release-asset":
                    if (string.IsNullOrWhiteSpace(component.Acquisition.Url))
                        issues.Add($"URL absente pour l'actif {component.Id}");
                    if (component.Acquisition.Size is null or <= 0)
                        issues.Add($"taille absente pour l'actif {component.Id}");
                    if (string.IsNullOrWhiteSpace(component.Acquisition.Sha256))
                        issues.Add($"empreinte absente pour l'actif {component.Id}");
                    break;
                case "unavailable":
                    if (string.IsNullOrWhiteSpace(component.Acquisition.Reason))
                        issues.Add($"motif absent pour le composant indisponible {component.Id}");
                    if (component.Files.Count > 0)
                        issues.Add($"composant indisponible avec des fichiers : {component.Id}");
                    break;
                case "payload":
                    foreach (var file in component.Files.Where(f => !PathIssues(f.Path).Any()))
                    {
                        if (!manifest.Files.Any(m => string.Equals(m.Path, file.Path, StringComparison.Ordinal)))
                            issues.Add($"fichier non déclaré dans files[] : {file.Path}");
                    }
                    break;
            }
        }

        foreach (var component in manifest.Components)
        {
            foreach (var required in component.Requires)
            {
                if (!componentIds.Contains(required))
                {
                    issues.Add($"{component.Id} requiert un composant absent du manifeste : {required}");
                    continue;
                }

                var target = manifest.Components.First(c => c.Id == required);
                if (component.Mandatory && !target.Mandatory)
                    issues.Add($"un composant obligatoire dépend d'un composant facultatif : {component.Id} → {required}");
            }
        }

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            foreach (var issue in PathIssues(file.Path)) issues.Add(issue);
            if (!seenPaths.Add(file.Path)) issues.Add($"chemin dupliqué : {file.Path}");
            if (file.Size < 0) issues.Add($"taille négative : {file.Path}");
            if (!string.IsNullOrEmpty(file.Sha256) && file.Sha256.Length != 64)
                issues.Add($"empreinte non conforme : {file.Path}");
        }

        if (manifest.Components.Any(c => c.Acquisition.Kind == "payload") && manifest.Files.Count == 0)
            issues.Add("charge utile déclarée mais files[] vide");

        if (string.IsNullOrWhiteSpace(manifest.Signature.Algorithm) ||
            string.IsNullOrWhiteSpace(manifest.Signature.KeyId) ||
            string.IsNullOrWhiteSpace(manifest.Signature.Value))
        {
            issues.Add("signature absente ou incomplète");
        }

        return issues;
    }

    /// <summary>Refuse un chemin vide, absolu, remontant, ou porteur d'un lecteur Windows.</summary>
    public static IReadOnlyList<string> PathIssues(string? path)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            issues.Add("chemin vide");
            return issues;
        }

        if (path.StartsWith('/') || path.StartsWith('\\')) issues.Add($"chemin absolu refusé : {path}");
        if (path.Length >= 2 && path[1] == ':') issues.Add($"lecteur Windows refusé : {path}");
        if (path.Contains("..", StringComparison.Ordinal)) issues.Add($"remontée de répertoire refusée : {path}");
        if (path.Contains('\0')) issues.Add($"caractère nul dans le chemin : {path}");
        return issues;
    }
}
