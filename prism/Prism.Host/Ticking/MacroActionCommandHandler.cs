using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.Actions;
using Prism.Core.Content;
using Prism.Core.Perception;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class MacroActionCommandHandler : IWorldCommandHandler
{
    private readonly AgentLifecycleSystem _lifecycle;
    private readonly CompiledContentPack _content;
    private readonly WorldGrid _world;
    private readonly CropActionRules _cropRules;
    private readonly MacroActionSystem _macros;
    private readonly Dictionary<ulong, string> _commandIds;

    public MacroActionCommandHandler(
        AgentLifecycleSystem lifecycle,
        CompiledContentPack content,
        WorldGrid world,
        CropActionRules cropRules,
        MacroActionSystem macros,
        Dictionary<ulong, string> commandIds,
        string kind)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cropRules);
        ArgumentNullException.ThrowIfNull(macros);
        ArgumentNullException.ThrowIfNull(commandIds);
        if (kind is not ("ObtenirObjet" or "FabriquerMacro" or "Cultiver" or "Annuler"))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported macro-action command kind.");
        }

        _lifecycle = lifecycle;
        _content = content;
        _world = world;
        _cropRules = cropRules;
        _macros = macros;
        _commandIds = commandIds;
        Kind = kind;
    }

    public string Kind { get; }

    public CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        if (command.Payload.ValueKind != JsonValueKind.Object)
        {
            return CommandExecutionResult.Failure("invalid_payload");
        }

        if (Kind == "Annuler")
        {
            if (!_macros.Cancel(command.AgentId))
            {
                return CommandExecutionResult.Failure("nothing_to_cancel");
            }

            _commandIds.Remove(command.AgentId);
            return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
                new { cancelled = true },
                ProtocolJson.Options));
        }

        if (!_lifecycle.TryGetAgent(command.AgentId, out var agent))
        {
            return CommandExecutionResult.Failure("agent_not_found");
        }

        var sources = MacroPerceptionCompiler.Compile(
            command.AgentId,
            _lifecycle,
            _content,
            _world,
            _cropRules,
            null);
        MacroActionResult result;
        switch (Kind)
        {
            case "ObtenirObjet":
                if (!TryReadItemRequest(command.Payload, out var request))
                {
                    return CommandExecutionResult.Failure("invalid_payload");
                }

                result = _macros.StartObtainObject(agent!, request, sources);
                break;
            case "FabriquerMacro":
                if (!TryReadId(command.Payload, ["recipe", "recipeId"], _content.Registries.Recipes, out var recipeId) ||
                    !TryReadOptionalCoordinate(command.Payload, ["station", "stationPosition"], out var station))
                {
                    return CommandExecutionResult.Failure("invalid_payload");
                }

                result = _macros.StartCraft(
                    agent!,
                    recipeId,
                    station,
                    station is null || _lifecycle.HasPerceivedBlock(command.AgentId, station.Value));
                break;
            case "Cultiver":
                if (!TryReadCoordinate(command.Payload, ["parcel", "target"], out var parcel) ||
                    !TryResolveSeed(command.Payload, agent!, out var seedItemId) ||
                    !_world.TryGetSurfaceColumn(parcel.X, parcel.Y, out var surface) ||
                    parcel.Z != surface.SurfaceZ + 1)
                {
                    return CommandExecutionResult.Failure("invalid_payload");
                }

                var support = parcel with { Z = parcel.Z - 1 };
                var supportMaterial = _world.TryGetBlockOverride(support, out var block) && !block.IsAir
                    ? block.MaterialId
                    : surface.MaterialId;
                var needsTilling = _cropRules.TillableMaterialIds.Contains(supportMaterial);
                var parcelPerceived = _lifecycle.HasPerceivedBlock(command.AgentId, parcel) ||
                                      _lifecycle.HasPerceivedBlock(command.AgentId, support);
                result = _macros.StartCultivate(
                    agent!,
                    parcel,
                    seedItemId,
                    needsTilling,
                    parcelPerceived);
                break;
            default:
                return CommandExecutionResult.Failure("unknown_command");
        }

        if (!result.Succeeded)
        {
            return CommandExecutionResult.Failure(result.ErrorCode!);
        }

        _commandIds.Add(command.AgentId, command.CommandId);
        return CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
            new { started = true, result.Step },
            ProtocolJson.Options));
    }

    private bool TryReadItemRequest(JsonElement payload, out ObtainObjectRequest request)
    {
        request = null!;
        ushort? itemTypeId = null;
        string? tagId = null;
        var hasItem = TryReadId(payload, ["item", "itemId", "type"], _content.Registries.Items, out var resolvedItem);
        if (hasItem)
        {
            itemTypeId = resolvedItem;
        }
        else if (payload.TryGetProperty("item", out _) ||
                 payload.TryGetProperty("itemId", out _) ||
                 payload.TryGetProperty("type", out _))
        {
            return false;
        }

        if (payload.TryGetProperty("tag", out var tag))
        {
            if (tag.ValueKind != JsonValueKind.String ||
                !_content.Registries.Tags.TryGetId(tag.GetString()!, out _))
            {
                return false;
            }

            tagId = tag.GetString();
        }

        if (itemTypeId is null && tagId is null ||
            itemTypeId is not null && tagId is not null ||
            !TryReadInt(payload, ["quantity", "qty"], out var quantity) ||
            quantity <= 0 ||
            !TryReadOptionalCoordinate(payload, ["hint"], out var hint) ||
            !TryReadOptionalInt(payload, "hintRadius", out var hintRadius) ||
            hintRadius < 0 ||
            hint is null && hintRadius != 0)
        {
            return false;
        }

        request = new ObtainObjectRequest(itemTypeId, tagId, quantity, hint, hintRadius);
        return true;
    }

    private bool TryResolveSeed(JsonElement payload, AgentBody agent, out ushort seedItemId)
    {
        seedItemId = 0;
        if (payload.TryGetProperty("seed", out _) || payload.TryGetProperty("seedItemId", out _))
        {
            return TryReadId(payload, ["seed", "seedItemId"], _content.Registries.Items, out seedItemId) &&
                   _cropRules.SeedItemTypeIds.Contains(seedItemId);
        }

        foreach (var candidate in _cropRules.SeedItemTypeIds.Order())
        {
            if (agent.Inventory.Count(candidate) > 0)
            {
                seedItemId = candidate;
                return true;
            }
        }

        return false;
    }

    internal static bool TryReadCoordinate(
        JsonElement payload,
        IReadOnlyList<string> names,
        out BlockCoordinate coordinate)
    {
        coordinate = default;
        if (!TryReadOptionalCoordinate(payload, names, out var value) || value is null)
        {
            return false;
        }

        coordinate = value.Value;
        return true;
    }

    internal static bool TryReadOptionalCoordinate(
        JsonElement payload,
        IReadOnlyList<string> names,
        out BlockCoordinate? coordinate)
    {
        coordinate = null;
        foreach (var name in names)
        {
            if (!payload.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.Object ||
                !TryReadInt(value, ["x"], out var x) ||
                !TryReadInt(value, ["y"], out var y) ||
                !TryReadInt(value, ["z"], out var z))
            {
                return false;
            }

            coordinate = new BlockCoordinate(x, y, z);
            return true;
        }

        return true;
    }

    internal static bool TryReadId(
        JsonElement payload,
        IReadOnlyList<string> names,
        StableIdRegistry registry,
        out ushort id)
    {
        id = 0;
        foreach (var name in names)
        {
            if (!payload.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetUInt16(out id);
            }

            return value.ValueKind == JsonValueKind.String &&
                   registry.TryGetId(value.GetString()!, out id);
        }

        return false;
    }

    internal static bool TryReadInt(JsonElement payload, IReadOnlyList<string> names, out int result)
    {
        result = 0;
        foreach (var name in names)
        {
            if (payload.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
            }
        }

        return false;
    }

    private static bool TryReadOptionalInt(JsonElement payload, string name, out int result)
    {
        result = 0;
        return !payload.TryGetProperty(name, out var value) ||
               value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }
}

internal static class MacroPerceptionCompiler
{
    public static PerceivedMacroSource[] Compile(
        ulong agentId,
        AgentLifecycleSystem lifecycle,
        CompiledContentPack content,
        WorldGrid world,
        CropActionRules cropRules,
        WorldContainerStore? containers)
    {
        var tagsByItem = BuildItemTags(content);
        var materialById = content.Materials.ToDictionary(
            material => content.Registries.Materials.GetId(material.Id));
        var cropByMaterial = cropRules.CropRules.ToDictionary(rule => rule.Growth.CropMaterialId);
        var sources = new List<PerceivedMacroSource>();
        foreach (var entry in lifecycle.GetPerceivedEntries(agentId))
        {
            if (entry.Kind == PerceptionEntityKind.GroundItem)
            {
                sources.Add(new PerceivedMacroSource(
                    entry.Id,
                    MacroSourceKind.GroundItem,
                    new BlockCoordinate(entry.X, entry.Y, entry.Z),
                    entry.ItemTypeId,
                    entry.Quantity,
                    tagsByItem.GetValueOrDefault(entry.ItemTypeId) ?? new HashSet<string>()));
                continue;
            }

            if (entry.Kind != PerceptionEntityKind.Block)
            {
                continue;
            }

            var position = new BlockCoordinate(entry.X, entry.Y, entry.Z);
            if (cropByMaterial.TryGetValue(entry.MaterialId, out var crop))
            {
                AddDropSources(
                    sources,
                    MacroSourceKind.Crop,
                    GetSourceId(world, content, position, crop.Growth.CropMaterialId, crop: true),
                    position,
                    crop.HarvestDrops,
                    tagsByItem);
            }
            else if (materialById.TryGetValue(entry.MaterialId, out var material))
            {
                var drops = material.Drops.Select(drop => new ActionDropRule(
                    content.Registries.Items.GetId(drop.Item),
                    drop.Quantity.Min,
                    drop.Quantity.Max,
                    drop.ChancePermille,
                    drop.WhenState)).ToArray();
                AddDropSources(
                    sources,
                    MacroSourceKind.Block,
                    GetSourceId(world, content, position, entry.MaterialId, crop: false),
                    position,
                    drops,
                    tagsByItem);
            }

            if (containers is not null && containers.TryGet(position, out var container))
            {
                foreach (var stack in container!.Snapshot())
                {
                    sources.Add(new PerceivedMacroSource(
                        GetContainerSourceId(world, content, position, stack.ItemTypeId),
                        MacroSourceKind.Container,
                        position,
                        stack.ItemTypeId,
                        stack.Quantity,
                        tagsByItem.GetValueOrDefault(stack.ItemTypeId) ?? new HashSet<string>()));
                }
            }
        }

        return sources
            .OrderBy(static source => source.Kind)
            .ThenBy(static source => source.Position.Z)
            .ThenBy(static source => source.Position.Y)
            .ThenBy(static source => source.Position.X)
            .ThenBy(static source => source.SourceId)
            .ToArray();
    }

    private static void AddDropSources(
        ICollection<PerceivedMacroSource> sources,
        MacroSourceKind kind,
        ulong sourceId,
        BlockCoordinate position,
        IEnumerable<ActionDropRule> drops,
        IReadOnlyDictionary<ushort, IReadOnlySet<string>> tagsByItem)
    {
        foreach (var drop in drops.Where(static drop => drop.ChancePermille > 0))
        {
            sources.Add(new PerceivedMacroSource(
                sourceId,
                kind,
                position,
                drop.ItemTypeId,
                drop.MaximumQuantity,
                tagsByItem.GetValueOrDefault(drop.ItemTypeId) ?? new HashSet<string>()));
        }
    }

    private static IReadOnlyDictionary<ushort, IReadOnlySet<string>> BuildItemTags(CompiledContentPack content)
    {
        var parentByTag = content.Tags.ToDictionary(tag => tag.Id, tag => tag.Parent, StringComparer.Ordinal);
        return content.Items.ToDictionary(
            item => content.Registries.Items.GetId(item.Id),
            item =>
            {
                var tags = new HashSet<string>(item.Tags, StringComparer.Ordinal);
                foreach (var itemTag in item.Tags)
                {
                    var current = parentByTag.GetValueOrDefault(itemTag);
                    while (current is not null && tags.Add(current))
                    {
                        current = parentByTag.GetValueOrDefault(current);
                    }
                }

                return (IReadOnlySet<string>)tags;
            });
    }

    private static ulong GetSourceId(
        WorldGrid world,
        CompiledContentPack content,
        BlockCoordinate position,
        ushort identity,
        bool crop)
    {
        var linearCell = checked(
            ((ulong)(uint)position.Z * (uint)world.Height + (uint)position.Y) *
            (uint)world.Width + (uint)position.X);
        var contentKey = checked(linearCell * ((ulong)content.Registries.Materials.Count + 1) + identity + 1);
        if (contentKey >= (1UL << 62))
        {
            throw new InvalidOperationException("The world is too large to encode stable macro source identifiers.");
        }

        return (crop ? 1UL << 62 : 0) | contentKey;
    }

    private static ulong GetContainerSourceId(
        WorldGrid world,
        CompiledContentPack content,
        BlockCoordinate position,
        ushort itemId)
    {
        var linearCell = checked(
            ((ulong)(uint)position.Z * (uint)world.Height + (uint)position.Y) *
            (uint)world.Width + (uint)position.X);
        var contentKey = checked(linearCell * ((ulong)content.Registries.Items.Count + 1) + itemId + 1);
        if (contentKey >= (1UL << 62))
        {
            throw new InvalidOperationException("The world is too large to encode stable macro source identifiers.");
        }

        return (2UL << 62) | contentKey;
    }
}
