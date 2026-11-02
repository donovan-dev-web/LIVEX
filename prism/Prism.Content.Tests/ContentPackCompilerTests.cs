using System.Text.Json;
using Prism.Content;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Content.Tests;

public sealed class ContentPackCompilerTests
{
    private readonly ContentPackCompiler _compiler = new();

    [Fact]
    public void CompilesValidPackAndCreatesStableRegistries()
    {
        var result = _compiler.Compile(CreateValidPack());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Pack);
        Assert.Equal((ushort)0, result.Pack.Registries.Materials.GetId("stone"));
        Assert.Equal((ushort)1, result.Pack.Registries.Items.GetId("ore_chunk"));
        Assert.Equal(64, result.Pack.ContentHash.Length);
    }

    [Fact]
    public void ContentHashDoesNotDependOnDefinitionInputOrder()
    {
        var first = CreateValidPack();
        var second = first with
        {
            Tags = first.Tags.Reverse().ToArray(),
            Materials = first.Materials.Reverse().ToArray(),
            Items = first.Items.Reverse().ToArray(),
            Recipes = first.Recipes.Reverse().ToArray(),
        };

        Assert.Equal(_compiler.Compile(first).Pack!.ContentHash, _compiler.Compile(second).Pack!.ContentHash);
    }

    [Fact]
    public void ReportsMultipleInvalidReferencesAndRejectsCompilation()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Materials =
            [
                new MaterialDefinition(
                    "stone",
                    Solid: true,
                    Hardness: -1,
                    MinToolTier: 0,
                    Tags: ["unknown_tag"],
                    Drops: [new ItemDrop("missing_item", new QuantityRange(0, 0))],
                    Perceivable: true),
            ],
        };

        var result = _compiler.Compile(invalid);

        Assert.Null(result.Pack);
        Assert.Contains(result.Errors, error => error.Code == "material.value.invalid");
        Assert.Contains(result.Errors, error => error.Code == "tag.reference.missing");
        Assert.Contains(result.Errors, error => error.Code == "material.drop.item.missing");
        Assert.Contains(result.Errors, error => error.Code == "material.drop.quantity.invalid");
    }

    [Fact]
    public void DurableItemsMustBeUnstackableAndHavePositiveWearPerAction()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Items =
            [
                valid.Items[0] with { Durability = 5, MaxStack = 2, UsePerAction = 0 },
                valid.Items[1],
            ],
        };

        var result = _compiler.Compile(invalid);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "item.durability.stack.invalid");
        Assert.Contains(result.Errors, error => error.Code == "item.value.invalid");
    }

    [Fact]
    public void ReportsUndeclaredRecipeCyclesButAllowsDeclaredRegeneration()
    {
        var valid = CreateValidPack();
        var cycle = valid with
        {
            Items =
            [
                new ItemDefinition("a", [], 10),
                new ItemDefinition("b", [], 10),
            ],
            Recipes =
            [
                new RecipeDefinition("a_to_b", [new RecipeIngredient("a", null, 1)],
                    [new RecipeProduct("b", 1)], null, null, 1, 0, "craft"),
                new RecipeDefinition("b_to_a", [new RecipeIngredient("b", null, 1)],
                    [new RecipeProduct("a", 1)], null, null, 1, 0, "craft"),
            ],
        };

        var rejected = _compiler.Compile(cycle);
        Assert.Contains(rejected.Errors, error => error.Code == "recipe.graph.cycle");

        var regeneration = cycle with
        {
            Recipes = [cycle.Recipes[0] with { IsRegeneration = true }, cycle.Recipes[1]],
        };
        Assert.DoesNotContain(_compiler.Compile(regeneration).Errors,
            error => error.Code == "recipe.graph.cycle");
    }

    [Fact]
    public void RejectsUnsupportedSchemaAndDuplicateIds()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Manifest = valid.Manifest with { SchemaVersion = "2.0.0" },
            Items = [.. valid.Items, valid.Items[0]],
        };

        var result = _compiler.Compile(invalid);

        Assert.Contains(result.Errors, error => error.Code == "manifest.schema.unsupported");
        Assert.Contains(result.Errors, error => error.Code == "id.duplicate");
    }

    [Fact]
    public void RecipeMustReferenceMaterialDeclaredAsAStation()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Recipes = [valid.Recipes[0] with { Station = "missing_station" }],
        };

        Assert.Contains(_compiler.Compile(invalid).Errors,
            error => error.Code == "recipe.station.missing");
    }

    [Fact]
    public void ValidatesTemplateGrowthEffectAndParameterReferences()
    {
        var valid = CreateValidPack();
        using var parameterDocument = JsonDocument.Parse("""{"world":{"size":[256,256,64]}}""");
        var complete = valid with
        {
            Materials =
            [
                valid.Materials[0] with
                {
                    Growth = "wheat_growth",
                    Requires = new MaterialRequirements(Below: "stone", WaterWithin: 4),
                },
            ],
            Items = [valid.Items[0] with { Effects = "food_effect" }, valid.Items[1]],
            Templates =
            [
                new StructureTemplateDefinition(
                    "shelter",
                    [1, 1, 1],
                    [new TemplateBlock([0, 0, 0], "stone")],
                    [new TemplateMaterial("ore_chunk", 2)],
                    10,
                    new TemplatePlacement(FlatGround: true, ClearVolume: true),
                    new TemplateEffect("shelter", [[0, 0, 0], [0, 0, 0]], 500),
                    800),
            ],
            GrowthRules =
            [
                new GrowthRuleDefinition(
                    "wheat_growth",
                    4,
                    150,
                    25,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["spring"] = 1200,
                        ["summer"] = 1000,
                        ["autumn"] = 800,
                        ["winter"] = 0,
                    },
                    600),
            ],
            Effects =
            [
                new EffectDefinition(
                    "food_effect",
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["hunger"] = -300,
                        ["energyRecovery"] = 20,
                    }),
            ],
            Parameters = parameterDocument.RootElement.Clone(),
        };

        var result = _compiler.Compile(complete);

        Assert.True(result.Succeeded);
        Assert.Single(result.Pack!.Templates);
        Assert.Single(result.Pack.GrowthRules);
        Assert.Single(result.Pack.Effects);
    }

    [Fact]
    public void LoadsPackFromDocumentedDirectoryLayoutAndHashesFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"prism-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "materials"));
        Directory.CreateDirectory(Path.Combine(root, "items"));
        Directory.CreateDirectory(Path.Combine(root, "recipes"));
        Directory.CreateDirectory(Path.Combine(root, "templates"));
        Directory.CreateDirectory(Path.Combine(root, "growth"));
        Directory.CreateDirectory(Path.Combine(root, "effects"));

        try
        {
            WriteJson(Path.Combine(root, "pack.json"), new PackManifest("test_pack", "1.0.0", "1.0.0"));
            WriteJson(Path.Combine(root, "tags.json"), new[] { new ContentTag("ore") });
            WriteJson(Path.Combine(root, "materials", "stone.json"),
                new MaterialDefinition(
                    "stone", true, 1, 0, ["ore"], [new ItemDrop("ore_chunk", new QuantityRange(1, 1))], true, Station: "stone"));
            WriteJson(Path.Combine(root, "items", "ore_chunk.json"),
                new ItemDefinition("ore_chunk", ["ore"], 64));
            WriteJson(Path.Combine(root, "recipes", "smelt.json"),
                new RecipeDefinition(
                    "smelt",
                    [new RecipeIngredient("ore_chunk", null, 1)],
                    [new RecipeProduct("ore_chunk", 1)],
                    "stone",
                    null,
                    1,
                    0,
                    "craft",
                    IsRegeneration: true));
            File.WriteAllText(Path.Combine(root, "params.json"), "{}");

            var result = _compiler.CompileDirectory(root);

            Assert.True(result.Succeeded);
            Assert.Equal(64, result.Pack!.ContentHash.Length);
            Assert.Equal((ushort)0, result.Pack.Registries.Materials.GetId("stone"));
            Assert.Equal((ushort)0, result.Pack.Registries.Items.GetId("ore_chunk"));

            File.AppendAllText(Path.Combine(root, "items", "ore_chunk.json"), Environment.NewLine);
            var changed = _compiler.CompileDirectory(root);
            Assert.NotEqual(result.Pack.ContentHash, changed.Pack!.ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MinimalPackCompilesAndProductionGraphCanStartFromHandMinedMaterials()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var result = _compiler.CompileDirectory(root);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(17, result.Pack!.Materials.Count);
        Assert.Equal(19, result.Pack.Items.Count);
        Assert.Equal(8, result.Pack.Templates.Count);
        Assert.Single(result.Pack.GrowthRules);
        Assert.Equal(3, result.Pack.Effects.Count);
        Assert.Equal(250, result.Pack.Materials.Single(material => material.Id == "grass")
            .Drops.Single().ChancePermille);
        Assert.Equal(new QuantityRange(2, 3), result.Pack.Materials
            .Single(material => material.Id == "wheat_crop").Drops[0].Quantity);
        Assert.Equal(3, result.Pack.Materials
            .Single(material => material.Id == "wheat_crop").Drops[0].WhenState);
        var cropRule = GrowthRuleRuntimeCompiler.Compile(result.Pack)
            [result.Pack.Registries.Materials.GetId("wheat_crop")];
        Assert.Equal((ushort)result.Pack.Registries.Materials.GetId("farmland"), cropRule.FarmlandMaterialId);
        Assert.Equal((ushort)result.Pack.Registries.Materials.GetId("water"), cropRule.WaterMaterialId);
        Assert.Equal(150, cropRule.StageTicks);
        Assert.Equal(25, cropRule.JitterTicks);
        Assert.Equal(4, cropRule.WaterRadius);
        Assert.Equal(1200, cropRule.SpringFactorPermille);
        Assert.Equal(0, cropRule.WinterFactorPermille);
        var grassRule = GrassSpreadRuleRuntimeCompiler.Compile(result.Pack);
        Assert.Equal((ushort)result.Pack.Registries.Materials.GetId("grass"), grassRule.GrassMaterialId);
        Assert.Equal((ushort)result.Pack.Registries.Materials.GetId("dirt"), grassRule.SoilMaterialId);
        _ = PerceptionRuleRuntimeCompiler.Compile(result.Pack);
        var movementRules = MovementRuleRuntimeCompiler.Compile(result.Pack);
        Assert.Equal(2000, movementRules.PathSpeedupPermille);
        Assert.Contains(
            result.Pack.Registries.Materials.GetId("planks"),
            movementRules.PathMaterials.ToArray());

        Assert.Equal(100, grassRule.ChancePermille);

        var availableItems = new HashSet<string>(StringComparer.Ordinal);
        var availableToolTier = 0;
        var unlockedMaterialDrops = new HashSet<string>(StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var material in result.Pack.Materials)
            {
                if (material.MinToolTier <= availableToolTier && unlockedMaterialDrops.Add(material.Id))
                {
                    foreach (var drop in material.Drops)
                    {
                        changed |= availableItems.Add(drop.Item);
                    }
                }
            }

            foreach (var recipe in result.Pack.Recipes)
            {
                if (recipe.Station is not null && !availableItems.Contains(recipe.Station))
                {
                    continue;
                }

                var inputsAvailable = recipe.Inputs.All(input =>
                    input.Item is not null
                        ? availableItems.Contains(input.Item)
                        : result.Pack.Items.Any(item =>
                            availableItems.Contains(item.Id) && item.Tags.Contains(input.Tag, StringComparer.Ordinal)));
                if (!inputsAvailable)
                {
                    continue;
                }

                foreach (var output in recipe.Outputs.Concat(recipe.ByProducts ?? []))
                {
                    changed |= availableItems.Add(output.Item);
                }
            }

            var bestToolTier = result.Pack.Items
                .Where(item => availableItems.Contains(item.Id))
                .Select(item => item.ToolTier)
                .DefaultIfEmpty(0)
                .Max();
            if (bestToolTier > availableToolTier)
            {
                availableToolTier = bestToolTier;
                changed = true;
            }
        }

        Assert.Contains("stone_pickaxe", availableItems);
        Assert.Contains("iron_pickaxe", availableItems);
    }

    [Fact]
    public void PerceptionCompilerRejectsTagSetsThatCannotFitRegionalMask()
    {
        var definition = CreateValidPack() with
        {
            Tags = Enumerable.Range(0, 33)
                .Select(index => new ContentTag(index == 0 ? "ore" : $"tag_{index:D2}"))
                .ToArray(),
        };
        var pack = _compiler.Compile(definition).Pack!;

        var exception = Assert.Throws<InvalidOperationException>(
            () => PerceptionRuleRuntimeCompiler.Compile(pack));

        Assert.Contains("32", exception.Message);
    }

    [Fact]
    public void PerceptionCompilerIncludesAncestorTagsInRegionalSummary()
    {
        var definition = CreateValidPack() with
        {
            Tags = [new ContentTag("ore", Parent: "mineral"), new ContentTag("mineral")],
            Materials = CreateValidPack().Materials.Concat(
            [
                new MaterialDefinition("air", false, 0, 0, [], [], false),
                new MaterialDefinition("water", false, 0, 0, [], [], true),
            ]).ToArray(),
        };
        var pack = _compiler.Compile(definition).Pack!;
        var rules = PerceptionRuleRuntimeCompiler.Compile(pack);
        var stoneId = pack.Registries.Materials.GetId("stone");
        var world = new WorldGrid(1, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, stoneId, 0, 0));
        var movement = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { stoneId }, new HashSet<ushort>()));
        var registry = new AgentRegistry();
        var observer = registry.Incarnate(
            1,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(),
            movement);

        var frame = new PerceptionSystem().Observe(
            observer,
            world,
            registry,
            new GroundItemStore(),
            rules,
            new PerceptionSettings(NearRadius: 0, MidRadiusRegions: 0, MaximumObservedPerAgent: 8));
        var summary = Assert.Single(frame.Changes, change =>
            change.Entry.Kind == PerceptionEntityKind.RegionSummary);

        var expectedMask = (1u << pack.Registries.Tags.GetId("ore")) |
                           (1u << pack.Registries.Tags.GetId("mineral"));
        Assert.Equal(expectedMask, summary.Entry.TagMask);
    }

    [Fact]
    public void ValidatesDropQuantityRangesChanceAndState()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Materials =
            [
                valid.Materials[0] with
                {
                    Drops =
                    [
                        new ItemDrop("ore_chunk", new QuantityRange(3, 2), 1, 1001),
                        new ItemDrop("ore_chunk", new QuantityRange(1, 1), -1),
                    ],
                },
            ],
        };

        var result = _compiler.Compile(invalid);

        Assert.Contains(result.Errors, error => error.Code == "material.drop.quantity.invalid");
        Assert.Contains(result.Errors, error => error.Code == "material.drop.chance.invalid");
        Assert.Contains(result.Errors, error => error.Code == "material.drop.state.invalid");
    }

    [Fact]
    public void ValidatesTagParentsAndRejectsHierarchyCycles()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Tags =
            [
                new ContentTag("ore", "Mined resources", "mineral"),
                new ContentTag("mineral", Parent: "ore"),
            ],
        };

        var result = _compiler.Compile(invalid);

        Assert.Contains(result.Errors, error => error.Code == "tag.hierarchy.cycle");
    }

    [Theory]
    [InlineData("""{"world":{"size":[256,256,64],"regionSize":[16,16]}}""", null)]
    [InlineData("""{"world":{"size":[256,256]}}""", "params.dimension.invalid")]
    [InlineData("""{"world":{"size":[256,256,64],"regionSize":[16,0]}}""", "params.dimension.invalid")]
    [InlineData("""{"agent":{"inventorySlots":1.5}}""", "params.value.integer.required")]
    [InlineData("""{"movement":{"pathSpeedupPermille":999}}""", "params.value.out_of_range")]
    [InlineData("""{"unexpected":{"value":1}}""", "params.key.unknown")]
    [InlineData("""{"granary":{"decaySlowdownPermille":1001}}""", "params.value.out_of_range")]
    [InlineData("""{"growth":{"wheat":{"stageTicks":100,"jitterTicks":100}}}""", "params.growth.jitter.invalid")]
    public void ValidatesParameterSchema(string json, string? expectedError)
    {
        var valid = CreateValidPack();
        using var document = JsonDocument.Parse(json);
        var result = _compiler.Compile(valid with { Parameters = document.RootElement.Clone() });

        if (expectedError is null)
        {
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        }
        else
        {
            Assert.Contains(result.Errors, error => error.Code == expectedError);
        }
    }

    [Fact]
    public void RequiresSemverPackVersionAndValidContentIds()
    {
        var valid = CreateValidPack();
        var invalid = valid with
        {
            Manifest = valid.Manifest with { Version = "1.2" },
            Items = [new ItemDefinition("Invalid-Id", [], 1)],
        };

        var result = _compiler.Compile(invalid);

        Assert.Contains(result.Errors, error => error.Code == "manifest.version.invalid");
        Assert.Contains(result.Errors, error => error.Code == "id.format.invalid");
    }

    [Fact]
    public void ReportsMissingDirectoriesAndMalformedJsonWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"prism-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "pack.json"), "{");
            var result = _compiler.CompileDirectory(root);

            Assert.Null(result.Pack);
            Assert.Contains(result.Errors, error => error.Code == "pack.json.invalid");
            Assert.Contains(result.Errors, error => error.Code == "pack.file.required");
            Assert.Contains(result.Errors, error => error.Code == "pack.directory.required");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ContentPackDefinition CreateValidPack() =>
        new(
            new PackManifest("test_pack", "1.0.0", ContentPackCompiler.SupportedSchemaVersion),
            [new ContentTag("ore")],
            [new MaterialDefinition(
                "stone", true, 1, 0, ["ore"], [new ItemDrop("ore_chunk", new QuantityRange(1, 1))], true, Station: "stone")],
            [new ItemDefinition("ore_chunk", ["ore"], 64), new ItemDefinition("iron_ingot", [], 64)],
            [
                new RecipeDefinition(
                    "smelt",
                    [new RecipeIngredient("ore_chunk", null, 1)],
                    [new RecipeProduct("iron_ingot", 1)],
                    "stone",
                    null,
                    1,
                    0,
                    "craft"),
            ]);

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the content test output directory.");
    }
}
