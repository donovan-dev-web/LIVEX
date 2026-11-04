using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using Prism.Core.Content;

namespace Prism.Content;

public sealed class ContentPackCompiler
{
    public const string SupportedSchemaVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly IReadOnlyDictionary<string, (int Min, int Max)> ParameterRanges =
        new Dictionary<string, (int Min, int Max)>(StringComparer.Ordinal)
        {
            ["perception.nearRadius"] = (0, int.MaxValue),
            ["perception.midRadiusChunks"] = (0, int.MaxValue),
            ["perception.maxObservedPerAgent"] = (1, int.MaxValue),
            ["communication.radius"] = (0, int.MaxValue),
            ["agent.inventorySlots"] = (1, int.MaxValue),
            ["agent.moveBudgetPerTick"] = (0, int.MaxValue),
            ["movement.pathSpeedupPermille"] = (1000, 10000),
            ["time.dayLengthTicks"] = (1, int.MaxValue),
            ["time.seasonLengthDays"] = (1, int.MaxValue),
            ["growth.wheat.stageTicks"] = (1, int.MaxValue),
            ["growth.wheat.jitterTicks"] = (0, int.MaxValue),
            ["growth.waterRadius"] = (0, int.MaxValue),
            ["tools.stonePickaxe.durability"] = (1, int.MaxValue),
            ["tools.ironPickaxe.durability"] = (1, int.MaxValue),
            ["mining.baseTicksPerHardness"] = (1, int.MaxValue),
            ["food.rawWheat.nutrition"] = (0, int.MaxValue),
            ["food.rawWheat.shelfLifeTicks"] = (1, int.MaxValue),
            ["food.flatbread.nutrition"] = (0, int.MaxValue),
            ["food.flatbread.shelfLifeTicks"] = (1, int.MaxValue),
            ["granary.decaySlowdownPermille"] = (0, 1000),
            ["mastery.gainPerActionPermille"] = (0, 1000),
            ["mastery.maxSpeedupPermille"] = (0, 1000),
            ["updates.randomTicksPerChunkPerTick"] = (0, int.MaxValue),
            ["updates.grassSpreadChancePermille"] = (0, 1000),
            ["wear.structure.decayPerTickPermille"] = (0, 1000),
        };

    private static readonly HashSet<string> ParameterObjects = new(StringComparer.Ordinal)
    {
        "world", "perception", "communication", "agent", "time", "growth", "growth.wheat",
        "movement", "tools", "tools.stonePickaxe", "tools.ironPickaxe", "mining", "food",
        "food.rawWheat", "food.flatbread", "granary", "mastery", "updates", "wear",
        "wear.structure",
    };

    private static readonly Regex SemanticVersionPattern = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ContentCompilationResult Compile(ContentPackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            return new ContentCompilationResult(null, errors);
        }

        var registries = new ContentRegistries(
            definition.Materials.Select(material => material.Id),
            definition.Items.Select(item => item.Id),
            definition.Tags.Select(tag => tag.Id),
            definition.Recipes.Select(recipe => recipe.Id));
        return new ContentCompilationResult(
            new CompiledContentPack(
                definition.Manifest,
                registries,
                SortById(definition.Tags, tag => tag.Id),
                SortById(definition.Materials, material => material.Id),
                SortById(definition.Items, item => item.Id),
                SortById(definition.Recipes, recipe => recipe.Id),
                SortById(definition.Templates ?? [], template => template.Id),
                SortById(definition.GrowthRules ?? [], rule => rule.Id),
                SortById(definition.Effects ?? [], effect => effect.Id),
                definition.Parameters,
                ComputeContentHash(definition)),
            []);
    }

    public ContentCompilationResult CompileDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var root = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(root))
        {
            return Failure("pack.directory.missing", root, "The content pack directory does not exist.");
        }

        var errors = new List<ContentValidationError>();
        var manifest = ReadRequiredJson<PackManifest>(Path.Combine(root, "pack.json"), errors);
        var tags = ReadRequiredJson<ContentTag[]>(Path.Combine(root, "tags.json"), errors);
        var materials = ReadJsonDirectory<MaterialDefinition>(Path.Combine(root, "materials"), errors);
        var items = ReadJsonDirectory<ItemDefinition>(Path.Combine(root, "items"), errors);
        var recipes = ReadJsonDirectory<RecipeDefinition>(Path.Combine(root, "recipes"), errors);
        var templates = ReadJsonDirectory<StructureTemplateDefinition>(Path.Combine(root, "templates"), errors);
        var growthRules = ReadJsonDirectory<GrowthRuleDefinition>(Path.Combine(root, "growth"), errors);
        var effects = ReadJsonDirectory<EffectDefinition>(Path.Combine(root, "effects"), errors);
        var parameters = ReadJsonElement(Path.Combine(root, "params.json"), errors);

        if (manifest is null || tags is null || parameters is null)
        {
            return new ContentCompilationResult(null, errors);
        }

        var definition = new ContentPackDefinition(
            manifest, tags, materials, items, recipes, templates, growthRules, effects, parameters);
        errors.AddRange(Validate(definition));
        if (errors.Count > 0)
        {
            return new ContentCompilationResult(null, errors);
        }

        var hash = ComputeDirectoryHash(root, errors);
        if (errors.Count > 0)
        {
            return new ContentCompilationResult(null, errors);
        }

        var registries = new ContentRegistries(
            materials.Select(material => material.Id),
            items.Select(item => item.Id),
            tags.Select(tag => tag.Id),
            recipes.Select(recipe => recipe.Id));
        return new ContentCompilationResult(
            new CompiledContentPack(
                manifest,
                registries,
                SortById(tags, tag => tag.Id),
                SortById(materials, material => material.Id),
                SortById(items, item => item.Id),
                SortById(recipes, recipe => recipe.Id),
                SortById(templates, template => template.Id),
                SortById(growthRules, growthRule => growthRule.Id),
                SortById(effects, effect => effect.Id),
                parameters,
                hash),
            []);
    }

    private static List<ContentValidationError> Validate(ContentPackDefinition definition)
    {
        var errors = new List<ContentValidationError>();
        if (definition.Manifest is null)
        {
            AddError(errors, "manifest.required", "pack.json", "Pack manifest is required.");
        }
        else
        {
            ValidateManifest(definition.Manifest, errors);
        }

        if (definition.Tags is null || definition.Materials is null ||
            definition.Items is null || definition.Recipes is null)
        {
            AddError(errors, "pack.collections.required", "pack",
                "Tags, materials, items, and recipes collections are required.");
            return errors;
        }

        if (definition.Tags.Any(value => value is null) ||
            definition.Materials.Any(value => value is null) ||
            definition.Items.Any(value => value is null) ||
            definition.Recipes.Any(value => value is null) ||
            (definition.Templates?.Any(value => value is null) ?? false) ||
            (definition.GrowthRules?.Any(value => value is null) ?? false) ||
            (definition.Effects?.Any(value => value is null) ?? false))
        {
            AddError(errors, "pack.entry.null", "pack", "Content collections cannot contain null entries.");
            return errors;
        }

        if (definition.Tags.Count == 0 || definition.Materials.Count == 0 || definition.Items.Count == 0)
        {
            AddError(errors, "pack.collection.empty", "pack",
                "A pack must define at least one tag, material, and item.");
        }

        ValidateUniqueKeys(definition.Tags.Select(tag => tag.Id), "tags", errors);
        ValidateUniqueKeys(definition.Materials.Select(material => material.Id), "materials", errors);
        ValidateUniqueKeys(definition.Items.Select(item => item.Id), "items", errors);
        ValidateUniqueKeys(definition.Recipes.Select(recipe => recipe.Id), "recipes", errors);
        ValidateKeys(definition.Tags.Select(tag => tag.Id), "tags", errors);
        ValidateKeys(definition.Materials.Select(material => material.Id), "materials", errors);
        ValidateKeys(definition.Items.Select(item => item.Id), "items", errors);
        ValidateKeys(definition.Recipes.Select(recipe => recipe.Id), "recipes", errors);
        ValidateKeys((definition.Templates ?? []).Select(template => template.Id), "templates", errors);
        ValidateKeys((definition.GrowthRules ?? []).Select(rule => rule.Id), "growth", errors);
        ValidateKeys((definition.Effects ?? []).Select(effect => effect.Id), "effects", errors);

        var tagKeys = BuildKeySet(definition.Tags.Select(tag => tag.Id));
        var materialKeys = BuildKeySet(definition.Materials.Select(material => material.Id));
        var stationKeys = BuildKeySet(definition.Materials
            .Where(material => !string.IsNullOrWhiteSpace(material.Station))
            .Select(material => material.Station!));
        var itemKeys = BuildKeySet(definition.Items.Select(item => item.Id));
        var growthKeys = BuildKeySet((definition.GrowthRules ?? []).Select(growth => growth.Id));
        var effectKeys = BuildKeySet((definition.Effects ?? []).Select(effect => effect.Id));
        var parentByTag = definition.Tags.ToDictionary(tag => tag.Id, tag => tag.Parent, StringComparer.Ordinal);
        ValidateTagHierarchy(definition.Tags, tagKeys, errors);
        ValidateMaterials(definition.Materials, tagKeys, itemKeys, errors);
        ValidateItems(definition.Items, tagKeys, materialKeys, errors);
        ValidateRecipes(definition.Recipes, tagKeys, materialKeys, stationKeys, itemKeys, errors);
        ValidateMaterialRequirements(definition.Materials, materialKeys, growthKeys, errors);
        ValidateTemplateReferences(
            definition.Templates ?? [], materialKeys, itemKeys, errors);
        ValidateGrowthRules(definition.GrowthRules ?? [], errors);
        ValidateEffects(definition.Effects ?? [], errors);
        ValidateItemEffects(definition.Items, effectKeys, errors);
        ValidateMaterialEffects(definition.Materials, effectKeys, parentByTag, errors);
        ValidateParameters(definition.Parameters, errors);
        ValidateRecipeGraph(definition.Recipes, definition.Items, errors);
        return errors;
    }

    private static void ValidateManifest(PackManifest manifest, List<ContentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            AddError(errors, "manifest.id.required", "pack.json", "Pack id is required.");
        }
        else
        {
            ValidateKeys([manifest.Id], "pack.json", errors);
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            AddError(errors, "manifest.version.required", "pack.json", "Pack version is required.");
        }
        else if (!SemanticVersionPattern.IsMatch(manifest.Version))
        {
            AddError(errors, "manifest.version.invalid", "pack.json",
                "Pack version must follow semantic versioning.");
        }

        if (!string.Equals(manifest.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            AddError(errors, "manifest.schema.unsupported", "pack.json",
                $"Schema version '{manifest.SchemaVersion}' is not supported; expected '{SupportedSchemaVersion}'.");
        }
    }

    private static void ValidateTagHierarchy(
        IReadOnlyList<ContentTag> tags,
        HashSet<string> tagKeys,
        List<ContentValidationError> errors)
    {
        foreach (var tag in tags.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(tag.Parent))
            {
                continue;
            }

            if (!tagKeys.Contains(tag.Parent))
            {
                AddError(errors, "tag.parent.missing", $"tags/{tag.Id}",
                    $"Tag references unknown parent '{tag.Parent}'.");
                continue;
            }

            var ancestors = new HashSet<string>(StringComparer.Ordinal) { tag.Id };
            var parent = tag.Parent;
            while (!string.IsNullOrWhiteSpace(parent))
            {
                if (!ancestors.Add(parent))
                {
                    AddError(errors, "tag.hierarchy.cycle", $"tags/{tag.Id}",
                        "Tag parent hierarchy must not contain cycles.");
                    break;
                }

                parent = tags.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, parent, StringComparison.Ordinal))?.Parent;
            }
        }
    }

    private static void ValidateMaterials(
        IReadOnlyList<MaterialDefinition> materials,
        HashSet<string> tagKeys,
        HashSet<string> itemKeys,
        List<ContentValidationError> errors)
    {
        foreach (var material in materials.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var path = $"materials/{material.Id}";
            ValidateTags(material.Tags, tagKeys, path, errors);
            if (material.Hardness < 0 || material.MinToolTier < 0)
            {
                AddError(errors, "material.value.invalid", path,
                    "Hardness and minimum tool tier must be non-negative.");
            }

            if (material.SeedItem is { Length: > 0 } seedItem &&
                (!itemKeys.Contains(seedItem) || string.IsNullOrWhiteSpace(material.Growth)))
            {
                AddError(errors, "material.seed.invalid", path,
                    $"Seed item '{seedItem}' must exist and may only be set on a growing material.");
            }

            if (material.Tillable && !material.Solid)
            {
                AddError(errors, "material.tillable.invalid", path,
                    "Only solid materials can be tilled.");
            }

            foreach (var drop in material.Drops ?? [])
            {
                if (drop is null)
                {
                    AddError(errors, "material.drop.invalid", path, "Drop entries cannot be null.");
                    continue;
                }

                if (!itemKeys.Contains(drop.Item))
                {
                    AddError(errors, "material.drop.item.missing", path,
                        $"Drop references unknown item '{drop.Item}'.");
                }

                if (drop.Quantity.Min <= 0 || drop.Quantity.Max < drop.Quantity.Min)
                {
                    AddError(errors, "material.drop.quantity.invalid", path,
                        $"Drop quantity for '{drop.Item}' must be a positive value or a valid positive range.");
                }

                if (drop.ChancePermille is < 0 or > 1000)
                {
                    AddError(errors, "material.drop.chance.invalid", path,
                        $"Drop chance for '{drop.Item}' must be between 0 and 1000 permille.");
                }

                if (drop.WhenState is < 0)
                {
                    AddError(errors, "material.drop.state.invalid", path,
                        $"Drop state for '{drop.Item}' must be non-negative.");
                }
            }
        }
    }

    private static void ValidateItems(
        IReadOnlyList<ItemDefinition> items,
        HashSet<string> tagKeys,
        HashSet<string> materialKeys,
        List<ContentValidationError> errors)
    {
        foreach (var item in items.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var path = $"items/{item.Id}";
            ValidateTags(item.Tags, tagKeys, path, errors);
            if (item.MaxStack < 1 || item.ToolTier < 0 || item.Durability < 0 ||
                item.Weight < 0 || item.ShelfLifeTicks < 0 || item.UsePerAction <= 0)
            {
                AddError(errors, "item.value.invalid", path,
                    "Stack size must be positive, wear per action must be positive, and tool tier, durability, weight, and shelf life must be non-negative.");
            }

            if (item.Durability > 0 && item.MaxStack != 1)
            {
                AddError(errors, "item.durability.stack.invalid", path,
                    "Items with durability must have a maximum stack size of one.");
            }

            if (!string.IsNullOrWhiteSpace(item.Places) && !materialKeys.Contains(item.Places))
            {
                AddError(errors, "item.place.material.missing", path,
                    $"Placeable item references unknown material '{item.Places}'.");
            }
        }
    }

    private static void ValidateMaterialRequirements(
        IReadOnlyList<MaterialDefinition> materials,
        HashSet<string> materialKeys,
        HashSet<string> growthKeys,
        List<ContentValidationError> errors)
    {
        foreach (var material in materials.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(material.Growth) && !growthKeys.Contains(material.Growth))
            {
                AddError(errors, "material.growth.missing", $"materials/{material.Id}",
                    $"Material references unknown growth rule '{material.Growth}'.");
            }

            if (material.Requires is { } requirements)
            {
                if (!string.IsNullOrWhiteSpace(requirements.Below) &&
                    !materialKeys.Contains(requirements.Below))
                {
                    AddError(errors, "material.requirement.material.missing", $"materials/{material.Id}",
                        $"Material requires unknown supporting material '{requirements.Below}'.");
                }

                if (requirements.WaterWithin is < 0)
                {
                    AddError(errors, "material.requirement.distance.invalid", $"materials/{material.Id}",
                        "Water distance must be non-negative.");
                }
            }
        }
    }

    private static void ValidateTemplateReferences(
        IReadOnlyList<StructureTemplateDefinition> templates,
        HashSet<string> materialKeys,
        HashSet<string> itemKeys,
        List<ContentValidationError> errors)
    {
        ValidateUniqueKeys(templates.Select(template => template.Id), "templates", errors);
        foreach (var template in templates.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var path = $"templates/{template.Id}";
            if (template.Size is not { Length: 3 } ||
                template.Size.Any(size => size <= 0))
            {
                AddError(errors, "template.size.invalid", path,
                    "Template size must contain three positive dimensions.");
            }

            if (template.WorkTicks < 0 || template.IntegrityThresholdPermille is < 0 or > 1000)
            {
                AddError(errors, "template.value.invalid", path,
                    "Work ticks must be non-negative and integrity threshold must be between 0 and 1000 permille.");
            }

            if (!template.IsTree && template.Effect is null)
            {
                AddError(errors, "template.effect.required", path,
                    "A structure template must declare a measurable effect.");
            }

            if (template.IsTree && ((template.Materials?.Length ?? 0) != 0 || template.Effect is not null))
            {
                AddError(errors, "template.tree.invalid", path,
                    "Tree templates must not declare construction materials or structure effects.");
            }

            if (template.Placement is null)
            {
                AddError(errors, "template.placement.required", path,
                    "Template placement rules are required.");
            }

            foreach (var block in template.Blocks ?? [])
            {
                if (block is null)
                {
                    AddError(errors, "template.block.invalid", path, "Template block entries cannot be null.");
                    continue;
                }

                if (!materialKeys.Contains(block.Block))
                {
                    AddError(errors, "template.block.missing", path,
                        $"Template references unknown block material '{block.Block}'.");
                }

                if (block.At is not { Length: 3 } ||
                    block.At.Where((coordinate, index) =>
                            coordinate < 0 || template.Size is not { Length: 3 } ||
                            coordinate >= template.Size[index])
                        .Any())
                {
                    AddError(errors, "template.block.position.invalid", path,
                        "Template block coordinates must be inside the declared size.");
                }
            }

            foreach (var material in template.Materials ?? [])
            {
                if (material is null)
                {
                    AddError(errors, "template.material.invalid", path,
                        "Template construction material entries cannot be null.");
                    continue;
                }

                if (!itemKeys.Contains(material.Item))
                {
                    AddError(errors, "template.material.item.missing", path,
                        $"Template references unknown construction item '{material.Item}'.");
                }

                if (material.Quantity <= 0)
                {
                    AddError(errors, "template.material.quantity.invalid", path,
                        "Template construction quantities must be positive.");
                }
            }

            if (template.Effect is { } effect &&
                !IsTemplateEffectValid(effect, template.Size))
            {
                AddError(errors, "template.effect.invalid", path,
                    "Template effect must have a type, two in-bounds 3D volume corners, and a non-negative bonus.");
            }
        }
    }

    private static bool IsTemplateEffectValid(TemplateEffect effect, int[]? templateSize)
    {
        if (string.IsNullOrWhiteSpace(effect.Type) ||
            effect.Volume is not { Length: 2 } ||
            effect.Volume.Any(point => point is not { Length: 3 }) ||
            effect.FatigueRecoveryBonusPermille < 0 ||
            templateSize is not { Length: 3 } ||
            templateSize.Any(size => size <= 0))
        {
            return false;
        }

        for (var axis = 0; axis < 3; axis++)
        {
            var minimum = Math.Min(effect.Volume[0][axis], effect.Volume[1][axis]);
            var maximum = Math.Max(effect.Volume[0][axis], effect.Volume[1][axis]);
            if (minimum < 0 || maximum >= templateSize[axis])
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateGrowthRules(
        IReadOnlyList<GrowthRuleDefinition> growthRules,
        List<ContentValidationError> errors)
    {
        ValidateUniqueKeys(growthRules.Select(rule => rule.Id), "growth", errors);
        foreach (var rule in growthRules.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var path = $"growth/{rule.Id}";
            if (rule.Stages < 2 || rule.StageTicks <= 0 || rule.JitterTicks < 0 ||
                rule.WitherAfterTicks <= 0)
            {
                AddError(errors, "growth.value.invalid", path,
                    "Growth requires at least two stages, positive stage/wither durations, and non-negative jitter.");
            }

            if (rule.JitterTicks >= rule.StageTicks)
            {
                AddError(errors, "growth.jitter.invalid", path,
                    "Growth jitter must be smaller than the stage duration.");
            }

            foreach (var season in new[] { "spring", "summer", "autumn", "winter" })
            {
                if (rule.SeasonFactorsPermille is null ||
                    !rule.SeasonFactorsPermille.TryGetValue(season, out var factor) || factor < 0)
                {
                    AddError(errors, "growth.season.factor.invalid", path,
                        $"A non-negative permille factor for '{season}' is required.");
                }
            }
        }
    }

    private static void ValidateEffects(
        IReadOnlyList<EffectDefinition> effects,
        List<ContentValidationError> errors)
    {
        ValidateUniqueKeys(effects.Select(effect => effect.Id), "effects", errors);
        foreach (var effect in effects.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            if (effect.Values is null || effect.Values.Count == 0)
            {
                AddError(errors, "effect.values.required", $"effects/{effect.Id}",
                    "An effect must define at least one integer value.");
            }
        }
    }

    private static void ValidateItemEffects(
        IReadOnlyList<ItemDefinition> items,
        HashSet<string> effectKeys,
        List<ContentValidationError> errors)
    {
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Effects) && !effectKeys.Contains(item.Effects))
            {
                AddError(errors, "item.effect.missing", $"items/{item.Id}",
                    $"Item references unknown effect '{item.Effects}'.");
            }
        }
    }

    private static void ValidateMaterialEffects(
        IReadOnlyList<MaterialDefinition> materials,
        HashSet<string> effectKeys,
        IReadOnlyDictionary<string, string?> parentByTag,
        List<ContentValidationError> errors)
    {
        foreach (var material in materials)
        {
            if (string.IsNullOrWhiteSpace(material.Effects))
            {
                continue;
            }

            var drinkable = HasTagOrAncestor(material.Tags, "drinkable", parentByTag);
            if (!effectKeys.Contains(material.Effects))
            {
                AddError(errors, "material.effect.missing", $"materials/{material.Id}",
                    $"Material references unknown effect '{material.Effects}'.");
            }
            else if (!drinkable)
            {
                AddError(errors, "material.effect.invalid", $"materials/{material.Id}",
                    "Only drinkable materials may define consumption effects.");
            }
        }
    }

    private static bool HasTagOrAncestor(
        IEnumerable<string> tags,
        string requiredTag,
        IReadOnlyDictionary<string, string?> parentByTag)
    {
        foreach (var tag in tags)
        {
            var current = tag;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current is not null && visited.Add(current))
            {
                if (StringComparer.Ordinal.Equals(current, requiredTag))
                {
                    return true;
                }

                current = parentByTag.GetValueOrDefault(current);
            }
        }

        return false;
    }

    private static void ValidateParameters(JsonElement? parameters, List<ContentValidationError> errors)
    {
        if (parameters is { ValueKind: not JsonValueKind.Object })
        {
            AddError(errors, "params.object.required", "params.json",
                "Pack parameters must be a JSON object.");
            return;
        }

        if (parameters is not { } root)
        {
            return;
        }

        ValidateParameterObject(root, string.Empty, errors);

        ValidateDimensionArray(root, "world.size", 3, errors);
        ValidateDimensionArray(root, "world.regionSize", 2, errors);

        if (TryGetParameter(root, "growth.wheat.stageTicks", out var stageTicks) &&
            TryGetParameter(root, "growth.wheat.jitterTicks", out var jitterTicks) &&
            stageTicks.TryGetInt32(out var stageDuration) &&
            jitterTicks.TryGetInt32(out var jitter) &&
            jitter >= stageDuration)
        {
            AddError(errors, "params.growth.jitter.invalid", "params.json:growth.wheat",
                "Growth jitter must be smaller than the stage duration.");
        }
    }

    private static void ValidateParameterObject(
        JsonElement element,
        string prefix,
        List<ContentValidationError> errors)
    {
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            if (!seenProperties.Add(property.Name))
            {
                AddError(errors, "params.key.duplicate", $"params.json:{path}",
                    $"Parameter '{path}' is defined more than once.");
                continue;
            }

            if (path is "world.size" or "world.regionSize")
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                if (!ParameterObjects.Contains(path))
                {
                    AddError(errors, "params.key.unknown", $"params.json:{path}",
                        $"Parameter group '{path}' is not defined by the content specification.");
                    continue;
                }

                ValidateParameterObject(property.Value, path, errors);
                continue;
            }

            if (!ParameterRanges.TryGetValue(path, out var range))
            {
                AddError(errors, "params.key.unknown", $"params.json:{path}",
                    $"Parameter '{path}' is not defined by the content specification.");
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out var value))
            {
                AddError(errors, "params.value.integer.required", $"params.json:{path}",
                    $"Parameter '{path}' must be an integer.");
                continue;
            }

            if (value < range.Min || value > range.Max)
            {
                AddError(errors, "params.value.out_of_range", $"params.json:{path}",
                    string.Format(CultureInfo.InvariantCulture,
                        "Parameter '{0}' must be between {1} and {2}.", path, range.Min, range.Max));
            }
        }
    }

    private static bool TryGetParameter(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var child))
            {
                return false;
            }

            value = child;
        }

        return true;
    }

    private static void ValidateDimensionArray(
        JsonElement root,
        string path,
        int expectedLength,
        List<ContentValidationError> errors)
    {
        if (!TryGetParameter(root, path, out var dimensions))
        {
            return;
        }

        if (dimensions.ValueKind != JsonValueKind.Array ||
            dimensions.GetArrayLength() != expectedLength ||
            dimensions.EnumerateArray().Any(value =>
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var dimension) || dimension <= 0))
        {
            AddError(errors, "params.dimension.invalid", $"params.json:{path}",
                $"Parameter '{path}' must contain {expectedLength} positive integer dimensions.");
        }
    }

    private static void ValidateKeys(
        IEnumerable<string> keys,
        string path,
        List<ContentValidationError> errors)
    {
        foreach (var key in keys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            if (key[0] is < 'a' or > 'z' ||
                key.Any(character => character is not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and not '_'))
            {
                AddError(errors, "id.format.invalid", path,
                    $"Content id '{key}' must use lowercase ASCII letters, digits, and underscores, beginning with a letter.");
            }
        }
    }

    private static void ValidateRecipes(
        IReadOnlyList<RecipeDefinition> recipes,
        HashSet<string> tagKeys,
        HashSet<string> materialKeys,
        HashSet<string> stationKeys,
        HashSet<string> itemKeys,
        List<ContentValidationError> errors)
    {
        foreach (var recipe in recipes.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            var path = $"recipes/{recipe.Id}";
            if (string.IsNullOrWhiteSpace(recipe.MasteryCategory))
            {
                AddError(errors, "recipe.mastery.category.required", path,
                    "A mastery category is required for every recipe.");
            }

            if (recipe.Inputs is not { Length: > 0 })
            {
                AddError(errors, "recipe.inputs.required", path,
                    "A recipe must declare at least one input.");
            }

            if (recipe.Outputs is not { Length: > 0 })
            {
                AddError(errors, "recipe.outputs.required", path,
                    "A recipe must declare at least one output.");
            }

            if (recipe.DurationTicks < 0 || recipe.MinMastery < 0)
            {
                AddError(errors, "recipe.value.invalid", path,
                    "Duration and minimum mastery must be non-negative.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.Station) &&
                (!materialKeys.Contains(recipe.Station) || !stationKeys.Contains(recipe.Station)))
            {
                AddError(errors, "recipe.station.missing", path,
                    $"Recipe references unknown station '{recipe.Station}'.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.Tool) && !itemKeys.Contains(recipe.Tool))
            {
                AddError(errors, "recipe.tool.missing", path,
                    $"Recipe references unknown tool item '{recipe.Tool}'.");
            }

            foreach (var input in recipe.Inputs ?? [])
            {
                if (input is null)
                {
                    AddError(errors, "recipe.input.invalid", path, "Recipe input entries cannot be null.");
                    continue;
                }

                var hasItem = !string.IsNullOrWhiteSpace(input.Item);
                var hasTag = !string.IsNullOrWhiteSpace(input.Tag);
                if (hasItem == hasTag)
                {
                    AddError(errors, "recipe.input.target.invalid", path,
                        "Each input must specify exactly one item or tag.");
                }
                else if (hasItem && !itemKeys.Contains(input.Item!))
                {
                    AddError(errors, "recipe.input.item.missing", path,
                        $"Recipe references unknown input item '{input.Item}'.");
                }
                else if (hasTag && !tagKeys.Contains(input.Tag!))
                {
                    AddError(errors, "recipe.input.tag.missing", path,
                        $"Recipe references unknown input tag '{input.Tag}'.");
                }

                if (input.Quantity <= 0)
                {
                    AddError(errors, "recipe.input.quantity.invalid", path,
                        "Recipe input quantities must be positive.");
                }
            }

            ValidateProducts(recipe.Outputs, itemKeys, path, "output", errors);
            ValidateProducts(recipe.ByProducts ?? [], itemKeys, path, "byproduct", errors);
        }
    }

    private static void ValidateProducts(
        IEnumerable<RecipeProduct> products,
        HashSet<string> itemKeys,
        string path,
        string role,
        List<ContentValidationError> errors)
    {
        foreach (var product in products)
        {
            if (product is null)
            {
                AddError(errors, $"recipe.{role}.invalid", path, $"{role} entries cannot be null.");
                continue;
            }

            if (!itemKeys.Contains(product.Item))
            {
                AddError(errors, $"recipe.{role}.item.missing", path,
                    $"Recipe references unknown {role} item '{product.Item}'.");
            }

            if (product.Quantity <= 0)
            {
                AddError(errors, $"recipe.{role}.quantity.invalid", path,
                    $"{role} quantities must be positive.");
            }
        }
    }

    private static void ValidateRecipeGraph(
        IReadOnlyList<RecipeDefinition> recipes,
        IReadOnlyList<ItemDefinition> items,
        List<ContentValidationError> errors)
    {
        var itemsByTag = items
            .SelectMany(item => (item.Tags ?? []).Select(tag => (tag, item.Id)))
            .GroupBy(entry => entry.tag, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Id).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var graph = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var recipe in recipes.Where(recipe => !recipe.IsRegeneration))
        {
            var outputs = (recipe.Outputs ?? [])
                .Concat(recipe.ByProducts ?? [])
                .Select(product => product.Item)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var inputs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var input in recipe.Inputs ?? [])
            {
                if (!string.IsNullOrWhiteSpace(input.Item))
                {
                    inputs.Add(input.Item);
                }
                else if (!string.IsNullOrWhiteSpace(input.Tag) &&
                         itemsByTag.TryGetValue(input.Tag, out var matchingItems))
                {
                    inputs.UnionWith(matchingItems);
                }
            }

            foreach (var input in inputs)
            {
                if (!graph.TryGetValue(input, out var adjacent))
                {
                    adjacent = new SortedSet<string>(StringComparer.Ordinal);
                    graph.Add(input, adjacent);
                }

                adjacent.UnionWith(outputs);
            }
        }

        var states = new Dictionary<string, byte>(StringComparer.Ordinal);
        var stack = new List<string>();
        var reportedCycles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Keys.ToArray())
        {
            Visit(node);
        }

        void Visit(string node)
        {
            if (states.TryGetValue(node, out var state))
            {
                return;
            }

            states[node] = 1;
            stack.Add(node);
            if (graph.TryGetValue(node, out var adjacent))
            {
                foreach (var target in adjacent)
                {
                    if (states.TryGetValue(target, out var targetState) && targetState == 1)
                    {
                        var start = stack.FindIndex(value => StringComparer.Ordinal.Equals(value, target));
                        var cycle = stack.Skip(start).Append(target).ToArray();
                        var cycleKey = string.Join(" -> ", cycle);
                        if (reportedCycles.Add(cycleKey))
                        {
                            AddError(errors, "recipe.graph.cycle", "recipes",
                                $"Recipe production contains an undeclared cycle: {cycleKey}.");
                        }
                    }
                    else if (!states.ContainsKey(target))
                    {
                        Visit(target);
                    }
                }
            }

            stack.RemoveAt(stack.Count - 1);
            states[node] = 2;
        }
    }

    private static void ValidateTags(
        IEnumerable<string> tags, HashSet<string> tagKeys, string path, List<ContentValidationError> errors)
    {
        foreach (var tag in tags ?? [])
        {
            if (!tagKeys.Contains(tag))
            {
                AddError(errors, "tag.reference.missing", path, $"Unknown tag '{tag}'.");
            }
        }
    }

    private static void ValidateUniqueKeys(
        IEnumerable<string> keys, string path, List<ContentValidationError> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                AddError(errors, "id.required", path, "Content ids must not be empty.");
            }
            else if (!seen.Add(key))
            {
                AddError(errors, "id.duplicate", path, $"Duplicate content id '{key}'.");
            }
        }
    }

    private static HashSet<string> BuildKeySet(IEnumerable<string> keys) =>
        new(keys.Where(key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);

    private static List<T> ReadJsonDirectory<T>(string directoryPath, List<ContentValidationError> errors)
    {
        if (!Directory.Exists(directoryPath))
        {
            AddError(errors, "pack.directory.required", directoryPath, "Required content directory is missing.");
            return [];
        }

        var result = new List<T>();
        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var values = JsonSerializer.Deserialize<T[]>(json, JsonOptions)
                        ?? throw new JsonException("The JSON array is null.");
                    result.AddRange(values);
                }
                else
                {
                    var value = JsonSerializer.Deserialize<T>(json, JsonOptions)
                        ?? throw new JsonException("The JSON document contains null instead of the expected object.");
                    result.Add(value);
                }
            }
            catch (JsonException exception)
            {
                AddError(errors, "pack.json.invalid", filePath, exception.Message);
            }
            catch (IOException exception)
            {
                AddError(errors, "pack.file.read", filePath, exception.Message);
            }
        }

        return result;
    }

    private static T? ReadRequiredJson<T>(string filePath, List<ContentValidationError> errors)
    {
        if (!File.Exists(filePath))
        {
            AddError(errors, "pack.file.required", filePath, "Required JSON file is missing.");
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(filePath), JsonOptions)
                ?? throw new JsonException("The JSON document contains null instead of the expected object.");
        }
        catch (JsonException exception)
        {
            AddError(errors, "pack.json.invalid", filePath, exception.Message);
            return default;
        }
        catch (IOException exception)
        {
            AddError(errors, "pack.file.read", filePath, exception.Message);
            return default;
        }
    }

    private static JsonElement? ReadJsonElement(string filePath, List<ContentValidationError> errors)
    {
        if (!File.Exists(filePath))
        {
            AddError(errors, "pack.file.required", filePath, "Required JSON file is missing.");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            AddError(errors, "pack.json.invalid", filePath, exception.Message);
            return null;
        }
        catch (IOException exception)
        {
            AddError(errors, "pack.file.read", filePath, exception.Message);
            return null;
        }
    }

    private static string ComputeDirectoryHash(string root, List<ContentValidationError> errors)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var filePath in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                         .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
            {
                var relativePath = Path.GetRelativePath(root, filePath).Replace('\\', '/');
                var pathBytes = Encoding.UTF8.GetBytes(relativePath);
                var contentBytes = File.ReadAllBytes(filePath);
                AppendInt32(hash, pathBytes.Length);
                hash.AppendData(pathBytes);
                AppendInt32(hash, contentBytes.Length);
                hash.AppendData(contentBytes);
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }
        catch (IOException exception)
        {
            AddError(errors, "pack.hash.failed", root, exception.Message);
            return string.Empty;
        }
    }

    private static string ComputeContentHash(ContentPackDefinition definition)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendCanonical("pack.json", definition.Manifest);
        AppendCanonical("tags.json", SortById(definition.Tags, tag => tag.Id));
        AppendCanonical("materials", SortById(definition.Materials, material => material.Id));
        AppendCanonical("items", SortById(definition.Items, item => item.Id));
        AppendCanonical("recipes", SortById(definition.Recipes, recipe => recipe.Id));
        AppendCanonical("templates", SortById(definition.Templates ?? [], template => template.Id));
        AppendCanonical("growth", SortById(definition.GrowthRules ?? [], rule => rule.Id));
        AppendCanonical("effects", SortById(definition.Effects ?? [], effect => effect.Id));
        if (definition.Parameters is { } parameters)
        {
            AppendCanonical("params.json", parameters);
        }

        return Convert.ToHexString(hash.GetHashAndReset());

        void AppendCanonical<T>(string path, T value)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var contentBytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            AppendInt32(hash, pathBytes.Length);
            hash.AppendData(pathBytes);
            AppendInt32(hash, contentBytes.Length);
            hash.AppendData(contentBytes);
        }
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static IReadOnlyList<T> SortById<T>(IEnumerable<T> values, Func<T, string> getId) =>
        values.OrderBy(getId, StringComparer.Ordinal).ToArray();

    private static ContentCompilationResult Failure(string code, string path, string message) =>
        new(null, [new ContentValidationError(code, path, message)]);

    private static void AddError(
        List<ContentValidationError> errors, string code, string path, string message) =>
        errors.Add(new ContentValidationError(code, path, message));
}
