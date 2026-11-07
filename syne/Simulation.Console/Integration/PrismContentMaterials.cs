// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>
/// Table <c>materialId PRISM → ResourceKind SYNE</c>, lue dans le pack de contenu
/// PRISM (<c>materials/*.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Pourquoi lire le contenu plutôt que deviner : PRISM publie ses blocs par
/// <b>identifiant de matériau</b> (un nombre) et SYNE ne peut pas en déduire la
/// nature. Le pack, lui, porte les tags de gameplay (<c>drinkable</c>,
/// <c>wood</c>, <c>ore</c>…) : c'est la seule table honnête.
/// </para>
/// <para>
/// L'identifiant est <b>l'index du nom après tri ordinal</b> : c'est la règle de
/// <c>StableIdRegistry</c> côté PRISM (contrat de contenu, pas un détail
/// d'implémentation — les identifiants doivent être stables entre versions de
/// pack). La table est donc reproductible à partir des seuls fichiers.
/// </para>
/// <para>
/// Trois tags sont exploités : <c>drinkable</c> → <see cref="ResourceKind.Water"/>
/// (seule ressource à porter un désir moteur, boire), <c>wood</c> →
/// <see cref="ResourceKind.Wood"/> et <c>ore</c> → <see cref="ResourceKind.Mineral"/>.
/// Le bois et le minerai ne nourrissent aucun désir : ils donnent des
/// <b>repères</b> — sans eux, un monde PRISM sans eau atteignable laisse
/// l'exploration sans cible et le write-back sans commande.
/// </para>
/// </remarks>
public static class PrismContentMaterials
{
    /// <summary>Table vide : aucun gisement ne sera exposé (dégradation explicite).</summary>
    public static IReadOnlyDictionary<ushort, ResourceKind> Empty { get; } =
        new Dictionary<ushort, ResourceKind>();

    /// <summary>
    /// Charge la table des matériaux d'un pack. Un répertoire absent ou illisible
    /// renvoie une table vide plutôt que de lever : le couplage reste possible
    /// sans contenu (les agents n'ont simplement aucune cible de ressource).
    /// </summary>
    public static IReadOnlyDictionary<ushort, ResourceKind> Load(string? contentDirectory)
    {
        if (string.IsNullOrWhiteSpace(contentDirectory))
        {
            return Empty;
        }

        string materialsDirectory = Path.Combine(contentDirectory, "materials");
        if (!Directory.Exists(materialsDirectory))
        {
            return Empty;
        }

        var materialIds = new List<string>();
        var kindByMaterial = new Dictionary<string, ResourceKind?>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(materialsDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                return Empty;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement material in document.RootElement.EnumerateArray())
                {
                    if (material.ValueKind != JsonValueKind.Object ||
                        !material.TryGetProperty("id", out JsonElement idElement) ||
                        idElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string id = idElement.GetString()!;
                    materialIds.Add(id);
                    kindByMaterial[id] = KindFromTags(material);
                }
            }
        }

        if (materialIds.Count == 0 || materialIds.Count > ushort.MaxValue + 1)
        {
            return Empty;
        }

        // `StableIdRegistry` trie les clés par ordinal puis les indexe : c'est la
        // définition de l'identifiant, pas une convention locale à ce fichier.
        materialIds.Sort(StringComparer.Ordinal);
        var table = new Dictionary<ushort, ResourceKind>();
        for (int index = 0; index < materialIds.Count; index++)
        {
            if (kindByMaterial[materialIds[index]] is { } kind)
            {
                table[(ushort)index] = kind;
            }
        }

        return table;
    }

    /// <summary>Nature SYNE d'un matériau, déduite de ses tags de gameplay.</summary>
    private static ResourceKind? KindFromTags(JsonElement material)
    {
        if (!material.TryGetProperty("tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            switch (tag.GetString())
            {
                case "drinkable":
                    return ResourceKind.Water;
                case "wood":
                    return ResourceKind.Wood;
                case "ore":
                    return ResourceKind.Mineral;
                default:
                    break;
            }
        }

        return null;
    }
}
