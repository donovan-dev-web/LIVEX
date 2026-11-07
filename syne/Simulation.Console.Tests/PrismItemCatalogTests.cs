// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du catalogue d'objets PRISM (étape 3b de SYNE-220) : quels identifiants sont
//        comestibles/buvables, et dégradation explicite sans contenu.

using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismItemCatalogTests
{
    private static string WritePack(params string[] itemFiles)
    {
        string root = Path.Combine(Path.GetTempPath(), "syne-prism-items-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "items"));
        for (int index = 0; index < itemFiles.Length; index++)
        {
            File.WriteAllText(Path.Combine(root, "items", $"part-{index}.json"), itemFiles[index]);
        }

        return root;
    }

    [Fact]
    public void Load_IndexesItemsByOrdinalSortedId_AndKeepsOnlyGameplayTags()
    {
        // Même règle que les matériaux : `StableIdRegistry` trie les clés par
        // ordinal puis les indexe. « flatbread » (0) < « log » (1) < « wheat » (2).
        string root = WritePack(
            """
            [
              { "id": "wheat", "tags": ["crop", "edible"] },
              { "id": "log", "tags": ["wood"] },
              { "id": "flatbread", "tags": ["edible"] }
            ]
            """);
        try
        {
            PrismItemCatalog catalog = PrismItemCatalog.Load(root);

            Assert.True(catalog.HasFood);
            Assert.Equal([0, 2], catalog.FoodItemIds);
            Assert.True(catalog.IsFood(0));
            Assert.False(catalog.IsFood(1));
            Assert.False(catalog.IsDrink(0));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_RecognisesDrinkableItems()
    {
        string root = WritePack(
            """
            [
              { "id": "water_bucket", "tags": ["container", "drinkable"] },
              { "id": "wheat", "tags": ["edible"] }
            ]
            """);
        try
        {
            PrismItemCatalog catalog = PrismItemCatalog.Load(root);

            // « water_bucket » (0) précède « wheat » (1) à l'ordre ordinal.
            Assert.Equal([0], catalog.DrinkItemIds);
            Assert.Equal([1], catalog.FoodItemIds);
            Assert.True(catalog.IsDrink(0));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutContentDirectory_IsExplicitlyEmpty()
    {
        IReadOnlyList<ushort> food = PrismItemCatalog.Empty.FoodItemIds;

        Assert.False(PrismItemCatalog.Empty.HasFood);
        Assert.Empty(food);
        Assert.Same(PrismItemCatalog.Empty, PrismItemCatalog.Load(null));
        Assert.Same(PrismItemCatalog.Empty, PrismItemCatalog.Load(Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void Load_WithoutAnyEdibleItem_LeavesHasFoodFalse()
    {
        string root = WritePack("""[ { "id": "log", "tags": ["wood"] } ]""");
        try
        {
            PrismItemCatalog catalog = PrismItemCatalog.Load(root);

            Assert.False(catalog.HasFood);
            Assert.Empty(catalog.FoodItemIds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
