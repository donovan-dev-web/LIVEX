// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Catalogue d'objets PRISM (étape 3b de SYNE-220) : quels identifiants d'objets
//        sont comestibles ou buvables, lu dans le pack de contenu de l'hôte.

using System.Text.Json;

namespace Simulation.Console.Integration;

/// <summary>
/// Table <c>itemTypeId PRISM → nature</c>, lue dans le pack de contenu
/// (<c>items/*.json</c>) : de quoi traduire un désir de nourriture en commande
/// réellement exécutable par PRISM.
/// </summary>
/// <remarks>
/// <para>
/// Pourquoi lire le contenu plutôt que deviner : une observation PRISM publie
/// l'<b>identifiant</b> des objets portés (<c>self.inventory[].itemTypeId</c>), pas
/// leurs tags. Sans cette table, SYNE saurait qu'il porte l'objet 7 sans jamais
/// pouvoir dire s'il se mange — donc sans jamais pouvoir émettre <c>Consommer</c>.
/// </para>
/// <para>
/// L'identifiant est <b>l'index du nom après tri ordinal</b> : c'est la règle de
/// <c>StableIdRegistry</c> côté PRISM (contrat de contenu, pas un détail
/// d'implémentation), la même que celle appliquée aux matériaux par
/// <see cref="PrismContentMaterials"/>. La table est donc reproductible à partir
/// des seuls fichiers.
/// </para>
/// <para>
/// Un répertoire absent ou illisible renvoie <see cref="Empty"/> plutôt que de
/// lever : le couplage reste possible sans contenu, SYNE retombe simplement sur le
/// vocabulaire minimal (<c>Aller</c>/<c>Consommer</c> à la source).
/// </para>
/// </remarks>
public sealed class PrismItemCatalog
{
    /// <summary>Tag de gameplay d'un objet comestible (pack minimal : <c>wheat</c>, <c>flatbread</c>).</summary>
    public const string FoodTag = "edible";

    /// <summary>Tag de gameplay d'un objet buvable (pack minimal : <c>water_bucket</c>).</summary>
    public const string DrinkTag = "drinkable";

    private readonly HashSet<ushort> _food;
    private readonly HashSet<ushort> _drink;
    private readonly Dictionary<ushort, string> _keyById;
    private readonly Dictionary<string, ushort> _idByKey;
    private readonly Dictionary<string, string> _placesByKey;

    private PrismItemCatalog(
        HashSet<ushort> food,
        HashSet<ushort> drink,
        Dictionary<ushort, string>? keyById = null,
        Dictionary<string, ushort>? idByKey = null,
        Dictionary<string, string>? placesByKey = null)
    {
        _food = food;
        _drink = drink;
        _keyById = keyById ?? [];
        _idByKey = idByKey ?? new Dictionary<string, ushort>(StringComparer.Ordinal);
        _placesByKey = placesByKey ?? new Dictionary<string, string>(StringComparer.Ordinal);
        FoodItemIds = food.Order().ToArray();
        DrinkItemIds = drink.Order().ToArray();
    }

    /// <summary>Catalogue vide : aucune acquisition possible, aucun objet identifié comme mangeable.</summary>
    public static PrismItemCatalog Empty { get; } = FromIds([], []);

    /// <summary>
    /// Catalogue <b>explicite</b> : les identifiants sont déjà connus de l'appelant
    /// (hôte qui connaît son contenu sans pack lisible, tests). Aucune déduction
    /// depuis des tags — c'est la même table, à sa source près.
    /// </summary>
    public static PrismItemCatalog FromIds(
        IEnumerable<ushort> foodItemIds,
        IEnumerable<ushort>? drinkItemIds = null)
    {
        ArgumentNullException.ThrowIfNull(foodItemIds);
        return new PrismItemCatalog(
            foodItemIds.ToHashSet(),
            drinkItemIds?.ToHashSet() ?? []);
    }

    /// <summary>Identifiants comestibles, en ordre croissant (déterministe).</summary>
    public IReadOnlyList<ushort> FoodItemIds { get; }

    /// <summary>Identifiants buvables, en ordre croissant (déterministe).</summary>
    public IReadOnlyList<ushort> DrinkItemIds { get; }

    /// <summary>
    /// Vrai si le pack déclare au moins un objet comestible : c'est la condition pour
    /// émettre une acquisition par tag (<c>ObtenirObjet</c>) au lieu d'un simple
    /// déplacement.
    /// </summary>
    public bool HasFood => _food.Count > 0;

    public bool IsFood(ushort itemTypeId) => _food.Contains(itemTypeId);

    public bool IsDrink(ushort itemTypeId) => _drink.Contains(itemTypeId);

    /// <summary>Nom de contenu d'un objet (SYNE-223 : inventaire porté lisible par la cognition).</summary>
    public bool TryGetKey(ushort itemTypeId, out string key) => _keyById.TryGetValue(itemTypeId, out key!);

    /// <summary>Identifiant PRISM d'un nom de contenu (ordre stable du registre).</summary>
    public bool TryGetId(string key, out ushort itemTypeId) => _idByKey.TryGetValue(key, out itemTypeId);

    /// <summary>
    /// Matière posée par un objet (champ <c>places</c> du pack), pour traduire
    /// « poser un établi » en commande <c>Poser</c> sans deviner l'identifiant.
    /// </summary>
    public bool TryGetPlaces(string itemKey, out string materialKey) =>
        _placesByKey.TryGetValue(itemKey, out materialKey!);

    /// <summary>
    /// Charge le catalogue d'un pack. Les fichiers sont lus récursivement sous
    /// <c>items/</c> ; l'ordre des fichiers n'a pas d'importance puisque les
    /// identifiants sont triés avant indexation.
    /// </summary>
    public static PrismItemCatalog Load(string? contentDirectory)
    {
        if (string.IsNullOrWhiteSpace(contentDirectory))
        {
            return Empty;
        }

        string itemsDirectory = Path.Combine(contentDirectory, "items");
        if (!Directory.Exists(itemsDirectory))
        {
            return Empty;
        }

        var itemIds = new List<string>();
        var tagsByItem = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var placesByItem = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory
                     .EnumerateFiles(itemsDirectory, "*.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
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

                foreach (JsonElement item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("id", out JsonElement idElement) ||
                        idElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string id = idElement.GetString()!;
                    itemIds.Add(id);
                    tagsByItem[id] = ReadTags(item);
                    if (item.TryGetProperty("places", out JsonElement places) &&
                        places.ValueKind == JsonValueKind.String)
                    {
                        placesByItem[id] = places.GetString()!;
                    }
                }
            }
        }

        if (itemIds.Count == 0 || itemIds.Count > ushort.MaxValue + 1)
        {
            return Empty;
        }

        // `StableIdRegistry` trie les clés par ordinal puis les indexe : c'est la
        // définition de l'identifiant côté PRISM, pas une convention locale.
        itemIds.Sort(StringComparer.Ordinal);
        var food = new HashSet<ushort>();
        var drink = new HashSet<ushort>();
        var keyById = new Dictionary<ushort, string>(itemIds.Count);
        var idByKey = new Dictionary<string, ushort>(itemIds.Count, StringComparer.Ordinal);
        var placesByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < itemIds.Count; index++)
        {
            string key = itemIds[index];
            keyById[(ushort)index] = key;
            idByKey[key] = (ushort)index;
            if (placesByItem.TryGetValue(key, out string? material))
            {
                placesByKey[key] = material;
            }

            List<string> tags = tagsByItem[key];
            if (tags.Contains(FoodTag, StringComparer.Ordinal))
            {
                food.Add((ushort)index);
            }

            if (tags.Contains(DrinkTag, StringComparer.Ordinal))
            {
                drink.Add((ushort)index);
            }
        }

        return new PrismItemCatalog(food, drink, keyById, idByKey, placesByKey);
    }

    private static List<string> ReadTags(JsonElement item)
    {
        if (!item.TryGetProperty("tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>(tags.GetArrayLength());
        foreach (JsonElement tag in tags.EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String)
            {
                result.Add(tag.GetString()!);
            }
        }

        return result;
    }
}
