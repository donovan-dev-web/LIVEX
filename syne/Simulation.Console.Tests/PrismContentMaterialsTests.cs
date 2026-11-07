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

            (ushort materialId, ResourceKind kind) = Assert.Single(table);
            Assert.Equal(2, materialId);
            Assert.Equal(ResourceKind.Water, kind);
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

            // Tri ordinal : iron_ore(0), leaves(1), log(2), stone(3).
            Assert.Equal(ResourceKind.Mineral, table[0]);
            Assert.Equal(ResourceKind.Wood, table[1]);
            Assert.Equal(ResourceKind.Wood, table[2]);
            Assert.False(table.ContainsKey(3));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
