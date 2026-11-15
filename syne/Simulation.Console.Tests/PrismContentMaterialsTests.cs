// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using Simulation.Console.Integration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismContentMaterialsTests
{
    private static string WritePack(params string[] materialFiles)
    {
        string root = Path.Combine(Path.GetTempPath(), "syne-prism-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "materials"));
        for (int index = 0; index < materialFiles.Length; index++)
        {
            File.WriteAllText(
                Path.Combine(root, "materials", $"part-{index}.json"),
                materialFiles[index]);
        }

        return root;
    }

    [Fact]
    public void Load_IndexesMaterialsByOrdinalSortedId()
    {
        // `StableIdRegistry` trie les clés par ordinal puis les indexe : « water »
        // arrive après « air » et « stone », donc l'identifiant 2.
        string root = WritePack(
            """
            [
              { "id": "water", "solid": false, "tags": ["drinkable"] },
              { "id": "air", "solid": false, "tags": [] },
              { "id": "stone", "solid": true, "tags": ["stone"] }
            ]
            """);
        try
        {
            IReadOnlyDictionary<ushort, ResourceKind> table = PrismContentMaterials.Load(root);

            // L'air n'a aucune nature ; la pierre est un gisement minéral (une
            // chaîne d'outils commence par elle) et l'eau la seule ressource
            // buvable.
            Assert.Equal(ResourceKind.Mineral, table[1]);
            Assert.Equal(ResourceKind.Water, table[2]);
            Assert.Equal(2, table.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReadsEveryFileOfTheMaterialsDirectory()
    {
        string root = WritePack(
            """[ { "id": "air", "tags": [] } ]""",
            """[ { "id": "water", "tags": ["drinkable"] } ]""");
        try
        {
            IReadOnlyDictionary<ushort, ResourceKind> table = PrismContentMaterials.Load(root);

            Assert.Equal(ResourceKind.Water, Assert.Single(table).Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_MapsWoodAndOreTagsToTheirResourceKind()
    {
        // Bois et minerai ne nourrissent aucun désir : ils donnent des repères de
        // déplacement, sans quoi un monde sans eau atteignable laisserait
        // l'exploration sans cible.
        string root = WritePack(
            """
            [
              { "id": "log", "tags": ["wood"] },
              { "id": "leaves", "tags": ["wood"] },
              { "id": "iron_ore", "tags": ["ore", "stone"] },
              { "id": "stone", "tags": ["stone"] }
            ]
            """);
        try
        {
            IReadOnlyDictionary<ushort, ResourceKind> table = PrismContentMaterials.Load(root);

            // Tri ordinal : iron_ore(0), leaves(1), log(2), stone(3). La pierre
            // porte le tag `stone` : c'est un gisement minéral, pas un décor.
            Assert.Equal(ResourceKind.Mineral, table[0]);
            Assert.Equal(ResourceKind.Wood, table[1]);
            Assert.Equal(ResourceKind.Wood, table[2]);
            Assert.Equal(ResourceKind.Mineral, table[3]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadDropItemKinds_LinksAGatheredItemToItsDepositFamily()
    {
        // Prospection : une récolte de « stone » refusée faute de source doit
        // pouvoir se traduire en « va vers un gisement minéral ». La table vient
        // du pack (bloc qui produit l'objet), jamais d'un nom deviné.
        string root = WritePack(
            """
            [
              { "id": "log", "tags": ["wood"], "drops": [ { "item": "log", "qty": 1 } ] },
              { "id": "stone", "tags": ["stone"], "drops": [ { "item": "stone", "qty": 1 } ] },
              { "id": "grass", "tags": [], "drops": [ { "item": "wheat_seed", "qty": 1 } ] }
            ]
            """);
        try
        {
            IReadOnlyDictionary<string, ResourceKind> table = PrismContentMaterials.LoadDropItemKinds(root);

            Assert.Equal(ResourceKind.Wood, table["log"]);
            Assert.Equal(ResourceKind.Mineral, table["stone"]);
            // Un bloc sans nature ne donne aucune famille : on ne devine rien.
            Assert.DoesNotContain("wheat_seed", table.Keys);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadDropItemKinds_WithoutDirectoryIsEmpty()
    {
        Assert.Empty(PrismContentMaterials.LoadDropItemKinds(null));
        Assert.Empty(PrismContentMaterials.LoadDropItemKinds("   "));
    }

    [Fact]
    public void Load_WithoutDirectoryIsEmpty()
    {
        Assert.Empty(PrismContentMaterials.Load(null));
        Assert.Empty(PrismContentMaterials.Load("   "));
        Assert.Empty(PrismContentMaterials.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void Load_WithUnreadableContentIsEmpty()
    {
        string root = WritePack("[ { \"id\": ");
        try
        {
            // Un pack illisible ne doit pas interrompre le couplage : les agents
            // n'ont simplement aucune cible de ressource.
            Assert.Empty(PrismContentMaterials.Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
