using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Core.World;

public enum StructureConstructionState : byte
{
    Planned,
    InProgress,
    Completed,
    Abandoned,
}

public sealed record StructureConstructionSite(
    string Id,
    string TemplateId,
    BlockCoordinate Origin,
    StructureConstructionState State,
    int WorkRequired,
    int WorkCompleted,
    int BlocksPlaced,
    StructureMaterialRule[]? MaterialsMissing = null);

public sealed record StructureMaterialBalance(ushort ItemTypeId, int Quantity);

public sealed record StructureConstructionResult(
    bool Succeeded,
    string? ErrorCode,
    StructureConstructionSite? Site)
{
    public static StructureConstructionResult Success(StructureConstructionSite site) =>
        new(true, null, site);

    public static StructureConstructionResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed record StructureIntegritySnapshot(
    string SiteId,
    int IntegrityPermille,
    int IntactBlocks,
    int TotalBlocks,
    bool EffectsActive);

public sealed record ActiveStructureEffect(
    string SiteId,
    string Type,
    BlockCoordinate Minimum,
    BlockCoordinate Maximum,
    int FatigueRecoveryBonusPermille);

public sealed record StructureAccessResult(bool Allowed, string? ErrorCode)
{
    public static StructureAccessResult Success() => new(true, null);
    public static StructureAccessResult Failure(string errorCode) => new(false, errorCode);
}

public sealed class StructureConstructionSystem
{
    private const int CheckpointVersion = 3;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IComparer<StructureBlockRule> BlockComparer =
        Comparer<StructureBlockRule>.Create(static (left, right) =>
        {
            var comparison = left.Z.CompareTo(right.Z);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Y.CompareTo(right.Y);
            return comparison != 0 ? comparison : left.X.CompareTo(right.X);
        });

    private readonly WorldGrid _world;
    private readonly SortedDictionary<string, SiteState> _sites = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, StructureTemplateRule> _templates = new(StringComparer.Ordinal);
    private readonly int _wearIntervalTicks;
    private readonly AblationMechanisms _mechanisms;

    public StructureConstructionSystem(
        WorldGrid world,
        IReadOnlyDictionary<string, StructureTemplateRule>? templates = null,
        int wearIntervalTicks = 0,
        AblationMechanisms? mechanisms = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (wearIntervalTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wearIntervalTicks));
        }

        _world = world;
        _wearIntervalTicks = wearIntervalTicks;
        _mechanisms = mechanisms ?? AblationMechanisms.AllEnabled;
        if (templates is not null)
        {
            RegisterTemplates(templates.Values);
        }
    }

    public AblationMechanisms Mechanisms => _mechanisms;

    public bool WearEnabled => _wearIntervalTicks > 0;
    public int WearIntervalTicks => _wearIntervalTicks;

    /// <summary>
    /// Returns the completed sites whose current integrity is strictly below their configured
    /// threshold (i.e. damaged structures whose effects are no longer active). Useful for
    /// emitting damage events or driving repairs.
    /// </summary>
    public IReadOnlyList<StructureConstructionSite> GetDamagedSites() =>
        _sites.Values
            .Where(static site => site.State == StructureConstructionState.Completed)
            .Select(site => (site, integrity: InspectIntegrity(site.Id)))
            .Where(pair => !pair.integrity.EffectsActive && pair.integrity.TotalBlocks > 0)
            .Select(pair => pair.site.ToSnapshot())
            .ToArray();

    /// <summary>
    /// Applies optional, deterministic structure wear. When <see cref="WearEnabled"/> is false
    /// (the default) this is a no-op. Otherwise, for every completed site whose
    /// <paramref name="tick"/> is a positive multiple of <see cref="WearIntervalTicks"/>,
    /// the last placed block of the site is removed from the world, lowering its integrity
    /// and possibly deactivating its effects. Construction progress and checkpoints are
    /// left untouched: wear is damage, not deconstruction.
    /// </summary>
    public int ApplyWear(long tick)
    {
        if (!WearEnabled || !_mechanisms.Wear || tick <= 0 || tick % _wearIntervalTicks != 0)
        {
            return 0;
        }

        var worn = 0;
        foreach (var site in _sites.Values.ToArray())
        {
            if (site.State != StructureConstructionState.Completed || site.BlocksPlaced == 0)
            {
                continue;
            }

            var block = site.Template.Blocks[site.BlocksPlaced - 1];
            var coordinate = new BlockCoordinate(
                site.Origin.X + block.X,
                site.Origin.Y + block.Y,
                site.Origin.Z + block.Z);
            if (_world.TryGetBlockOverride(coordinate, out var current) &&
                !current.IsAir &&
                current.MaterialId == block.MaterialId)
            {
                _world.RemoveBlockOverride(coordinate);
                worn++;
            }
        }

        return worn;
    }

    public IReadOnlyList<StructureConstructionSite> Sites =>
        _sites.Values.Select(static site => site.ToSnapshot()).ToArray();

    public IReadOnlyDictionary<string, StructureTemplateRule> Templates => _templates;

    public void RegisterTemplates(IEnumerable<StructureTemplateRule> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        foreach (var template in templates)
        {
            StructurePlacementValidator.ValidateTemplate(template);
            if (!_templates.TryAdd(template.Id, template))
            {
                throw new ArgumentException($"Structure template '{template.Id}' is already registered.", nameof(templates));
            }
        }
    }

    public StructureIntegritySnapshot InspectIntegrity(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (!_sites.TryGetValue(siteId, out var site))
        {
            throw new KeyNotFoundException($"Construction site '{siteId}' was not found.");
        }

        var intactBlocks = 0;
        foreach (var block in site.Template.Blocks)
        {
            var coordinate = new BlockCoordinate(
                site.Origin.X + block.X,
                site.Origin.Y + block.Y,
                site.Origin.Z + block.Z);
            if (_world.TryGetBlockOverride(coordinate, out var current) &&
                !current.IsAir &&
                current.MaterialId == block.MaterialId)
            {
                intactBlocks++;
            }
        }

        var totalBlocks = site.Template.Blocks.Count;
        var integrityPermille = totalBlocks == 0 ? 0 : (int)((long)intactBlocks * 1000 / totalBlocks);
        return new StructureIntegritySnapshot(
            site.Id,
            integrityPermille,
            intactBlocks,
            totalBlocks,
            site.State == StructureConstructionState.Completed &&
            totalBlocks > 0 &&
            integrityPermille >= site.Template.IntegrityThresholdPermille);
    }

    public IReadOnlyList<ActiveStructureEffect> GetActiveEffects()
    {
        var effects = new List<ActiveStructureEffect>();
        foreach (var site in _sites.Values)
        {
            if (site.Template.Effect is not { } effect ||
                !InspectIntegrity(site.Id).EffectsActive)
            {
                continue;
            }

            if (!_mechanisms.IsEnabled(effect.Type))
            {
                continue;
            }

            effects.Add(new ActiveStructureEffect(
                site.Id,
                effect.Type,
                new BlockCoordinate(
                    site.Origin.X + effect.MinimumOffset.X,
                    site.Origin.Y + effect.MinimumOffset.Y,
                    site.Origin.Z + effect.MinimumOffset.Z),
                new BlockCoordinate(
                    site.Origin.X + effect.MaximumOffset.X,
                    site.Origin.Y + effect.MaximumOffset.Y,
                    site.Origin.Z + effect.MaximumOffset.Z),
                effect.FatigueRecoveryBonusPermille));
        }

        return effects;
    }

    public byte[] CaptureState()
    {
        var checkpoint = new ConstructionCheckpoint(
            CheckpointVersion,
            _templates.Values.ToArray(),
            _sites.Values
                .Select(static site => new ConstructionSiteCheckpoint(
                    site.Id,
                    site.Template.Id,
                    site.Origin,
                    site.State,
                    site.WorkCompleted,
                    site.BlocksPlaced,
                    site.LastContributionTick,
                    site.ContributorsThisTick.Order(StringComparer.Ordinal).ToArray(),
                    site.Owner,
                    site.AccessRules,
                    site.MaterialsConsumed
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry => new StructureMaterialBalance(entry.Key, entry.Value))
                        .ToArray()))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreState(
        ReadOnlySpan<byte> checkpointBytes,
        IReadOnlyDictionary<string, StructureTemplateRule>? templates = null)
    {
        if (_sites.Count != 0)
        {
            throw new InvalidOperationException("Construction state can only be restored into a new system.");
        }

        var checkpoint = JsonSerializer.Deserialize<ConstructionCheckpoint>(
                            checkpointBytes,
                            CheckpointJsonOptions)
                        ?? throw new InvalidDataException("Construction checkpoint is empty.");
        if (checkpoint.Version is not (2 or CheckpointVersion) || checkpoint.Sites is null)
        {
            throw new InvalidDataException("Construction checkpoint version or contents are invalid.");
        }

        var restoredTemplates = new SortedDictionary<string, StructureTemplateRule>(StringComparer.Ordinal);
        var templateSource = checkpoint.Version == 2
            ? templates?.Values ?? _templates.Values
            : checkpoint.Templates
              ?? throw new InvalidDataException("Construction checkpoint templates are missing.");
        foreach (var template in templateSource)
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Id))
            {
                throw new InvalidDataException("Construction checkpoint contains an invalid template.");
            }

            try
            {
                StructurePlacementValidator.ValidateTemplate(template);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Construction checkpoint template '{template.Id}' is invalid.", exception);
            }

            if (!restoredTemplates.TryAdd(template.Id, template))
            {
                throw new InvalidDataException($"Construction checkpoint duplicates template '{template.Id}'.");
            }
        }

        var templatesToCompare = templates ?? _templates;
        if (checkpoint.Version == CheckpointVersion &&
            (templates is not null || _templates.Count > 0))
        {
            if (templatesToCompare.Count != restoredTemplates.Count)
            {
                throw new InvalidDataException("Construction checkpoint templates do not match the compiled content.");
            }

            foreach (var template in templatesToCompare)
            {
                if (!restoredTemplates.TryGetValue(template.Key, out var saved) ||
                    !AreTemplatesEquivalent(saved, template.Value))
                {
                    throw new InvalidDataException(
                        $"Construction checkpoint template '{template.Key}' does not match the compiled content.");
                }
            }
        }

        var restoredSites = new SortedDictionary<string, SiteState>(StringComparer.Ordinal);
        foreach (var entry in checkpoint.Sites)
        {
            if (entry is null ||
                string.IsNullOrWhiteSpace(entry.Id) ||
                string.IsNullOrWhiteSpace(entry.TemplateId) ||
                entry.Contributors is null ||
                entry.AccessRules is null ||
                !restoredTemplates.TryGetValue(entry.TemplateId, out var template) ||
                restoredSites.ContainsKey(entry.Id))
            {
                throw new InvalidDataException("Construction checkpoint contains an invalid or duplicate site.");
            }

            StructurePlacementValidator.ValidateTemplate(template);
            ValidatePrincipal(entry.Owner);
            if (!StructurePlacementValidator.IsOriginWithinBounds(_world, template, entry.Origin))
            {
                throw new InvalidDataException($"Construction site '{entry.Id}' is outside the restored world.");
            }

            var orderedBlocks = template.Blocks.Order(BlockComparer).ToArray();
            var state = new SiteState(
                _world,
                entry.Id,
                template with { Blocks = orderedBlocks },
                entry.Origin,
                entry.State,
                entry.Owner);
            state.RestoreProgress(
                entry.WorkCompleted,
                entry.BlocksPlaced,
                entry.LastContributionTick,
                entry.Contributors);
            state.RestoreAccessRules(entry.AccessRules);
            state.RestoreMaterialsConsumed(entry.MaterialsConsumed ?? []);
            if (!state.HasValidCheckpointProgress())
            {
                throw new InvalidDataException($"Construction site '{entry.Id}' has inconsistent progress.");
            }

            if (entry.BlocksPlaced > 0 && template.RequiresFlatGround)
            {
                for (var y = entry.Origin.Y; y < entry.Origin.Y + template.SizeY; y++)
                {
                    for (var x = entry.Origin.X; x < entry.Origin.X + template.SizeX; x++)
                    {
                        if (_world.GetSurfaceColumn(x, y).SurfaceZ != entry.Origin.Z - 1)
                        {
                            throw new InvalidDataException(
                                $"Construction site '{entry.Id}' has invalid ground in the restored world.");
                        }
                    }
                }
            }

            if (restoredSites.Values.Any(existing =>
                    (existing.State is StructureConstructionState.Planned or StructureConstructionState.InProgress) &&
                    Overlaps(existing.Template, existing.Origin, template, entry.Origin)))
            {
                throw new InvalidDataException($"Construction site '{entry.Id}' overlaps another active site.");
            }

            restoredSites.Add(entry.Id, state);
        }

        foreach (var entry in restoredSites)
        {
            _sites.Add(entry.Key, entry.Value);
        }

        _templates.Clear();
        foreach (var template in restoredTemplates)
        {
            _templates.Add(template.Key, template.Value);
        }
    }

    private static bool AreTemplatesEquivalent(
        StructureTemplateRule left,
        StructureTemplateRule right) =>
        left.Id == right.Id &&
        left.SizeX == right.SizeX &&
        left.SizeY == right.SizeY &&
        left.SizeZ == right.SizeZ &&
        left.RequiresFlatGround == right.RequiresFlatGround &&
        left.RequiresClearVolume == right.RequiresClearVolume &&
        left.WorkTicks == right.WorkTicks &&
        left.IntegrityThresholdPermille == right.IntegrityThresholdPermille &&
        left.Effect == right.Effect &&
        left.Blocks.SequenceEqual(right.Blocks) &&
        (left.Materials ?? []).SequenceEqual(right.Materials ?? []);

    public StructureConstructionResult CreateSite(
        string siteId,
        StructureTemplateRule template,
        BlockCoordinate origin,
        AccessPrincipal? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(template);
        ValidatePrincipal(owner);
        if (_sites.ContainsKey(siteId))
        {
            return StructureConstructionResult.Failure("site_exists");
        }

        if (_templates.TryGetValue(template.Id, out var registeredTemplate) &&
            !AreTemplatesEquivalent(registeredTemplate, template))
        {
            return StructureConstructionResult.Failure("template_mismatch");
        }

        if (_sites.Values.Any(site =>
                (site.State is StructureConstructionState.Planned or StructureConstructionState.InProgress) &&
                Overlaps(site.Template, site.Origin, template, origin)))
        {
            return StructureConstructionResult.Failure("site_overlaps");
        }

        var placement = StructurePlacementValidator.Validate(_world, template, origin);
        if (!placement.Succeeded)
        {
            return StructureConstructionResult.Failure(placement.ErrorCode!);
        }

        var blocks = template.Blocks.Order(BlockComparer).ToArray();
        var state = new SiteState(
            _world,
            siteId,
            template with { Blocks = blocks },
            origin,
            template.WorkTicks == 0 ? StructureConstructionState.Completed : StructureConstructionState.Planned,
            owner);
        _sites.Add(siteId, state);
        _templates.TryAdd(template.Id, template);
        if (template.WorkTicks == 0)
        {
            state.PlaceThrough(blocks.Length);
        }

        return StructureConstructionResult.Success(state.ToSnapshot());
    }

    public StructureAccessResult SetAccessList(
        string siteId,
        AccessPrincipal requester,
        IEnumerable<ContainerAccessRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(rules);
        if (!_sites.TryGetValue(siteId, out var site))
        {
            return StructureAccessResult.Failure("unknown_target");
        }

        if (site.Owner != requester)
        {
            return StructureAccessResult.Failure("not_owner");
        }

        site.SetAccessRules(rules);
        return StructureAccessResult.Success();
    }

    public StructureAccessResult CheckAccess(
        string siteId,
        IEnumerable<AccessPrincipal> principals,
        ContainerAccessRights requiredRight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(principals);
        ValidateRights(requiredRight, allowNone: false);
        var principalList = principals.ToArray();
        foreach (var principal in principalList)
        {
            ValidatePrincipal(principal);
        }

        if (!_sites.TryGetValue(siteId, out var site))
        {
            return StructureAccessResult.Failure("unknown_target");
        }

        return site.IsAuthorized(principalList, requiredRight)
            ? StructureAccessResult.Success()
            : StructureAccessResult.Failure("access_denied");
    }

    public StructureConstructionResult ContributeWork(
        string siteId,
        string workerId,
        long tick,
        AccessPrincipal? principal = null,
        AgentInventory? inventory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ValidatePrincipal(principal);
        if (!_sites.TryGetValue(siteId, out var site))
        {
            return StructureConstructionResult.Failure("site_not_found");
        }

        IReadOnlyList<AccessPrincipal> principals = principal is { } value ? [value] : [];
        if (site.HasAccessRules &&
            !site.IsAuthorized(principals, ContainerAccessRights.Build))
        {
            return StructureConstructionResult.Failure("access_denied");
        }

        if (site.State is not (StructureConstructionState.Planned or StructureConstructionState.InProgress))
        {
            return StructureConstructionResult.Failure("site_not_active");
        }

        if (tick < 0 || tick < site.LastContributionTick)
        {
            return StructureConstructionResult.Failure("tick_out_of_order");
        }

        var newContributionTick = tick > site.LastContributionTick;
        if (!newContributionTick && site.ContributorsThisTick.Contains(workerId))
        {
            return StructureConstructionResult.Failure("duplicate_contribution");
        }

        var nextWorkCompleted = checked(site.WorkCompleted + 1);
        var blocksToPlace = (int)((long)nextWorkCompleted * site.Template.Blocks.Count / site.Template.WorkTicks);
        if (!site.CanPlaceThrough(blocksToPlace))
        {
            return StructureConstructionResult.Failure("construction_block_obstructed");
        }

        var materialDelta = site.GetMaterialDelta(nextWorkCompleted);
        if (materialDelta.Count > 0 &&
            (inventory is null || !inventory.TryConsume(materialDelta)))
        {
            return StructureConstructionResult.Failure("materials_missing");
        }

        if (newContributionTick)
        {
            site.LastContributionTick = tick;
            site.ContributorsThisTick.Clear();
        }

        site.ContributorsThisTick.Add(workerId);
        site.WorkCompleted = nextWorkCompleted;
        site.RecordMaterialsConsumed(materialDelta);
        site.PlaceThrough(blocksToPlace);
        site.State = site.WorkCompleted == site.Template.WorkTicks
            ? StructureConstructionState.Completed
            : StructureConstructionState.InProgress;
        return StructureConstructionResult.Success(site.ToSnapshot());
    }

    public StructureConstructionResult AbandonSite(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (!_sites.TryGetValue(siteId, out var site))
        {
            return StructureConstructionResult.Failure("site_not_found");
        }

        if (site.State is not (StructureConstructionState.Planned or StructureConstructionState.InProgress))
        {
            return StructureConstructionResult.Failure("site_not_active");
        }

        site.RemovePlacedBlocks();
        site.State = StructureConstructionState.Abandoned;
        return StructureConstructionResult.Success(site.ToSnapshot());
    }

    /// <summary>
    /// Removes a single abandoned site from the system. Returns <c>true</c> when the site existed
    /// and was in the <see cref="StructureConstructionState.Abandoned"/> state and has been purged;
    /// <c>false</c> otherwise (unknown site or site still active/completed).
    /// </summary>
    public bool RemoveAbandonedSite(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (!_sites.TryGetValue(siteId, out var site) ||
            site.State != StructureConstructionState.Abandoned)
        {
            return false;
        }

        _sites.Remove(siteId);
        return true;
    }

    /// <summary>
    /// Purges every site currently in the <see cref="StructureConstructionState.Abandoned"/> state.
    /// Returns the number of sites removed. Abandoned sites are not restored on checkpoint load
    /// because they are no longer part of the persisted collection.
    /// </summary>
    public int PurgeAbandonedSites()
    {
        var abandonedIds = _sites
            .Where(static pair => pair.Value.State == StructureConstructionState.Abandoned)
            .Select(static pair => pair.Key)
            .ToArray();

        foreach (var id in abandonedIds)
        {
            _sites.Remove(id);
        }

        return abandonedIds.Length;
    }

    private static bool Overlaps(
        StructureTemplateRule leftTemplate,
        BlockCoordinate leftOrigin,
        StructureTemplateRule rightTemplate,
        BlockCoordinate rightOrigin) =>
    leftOrigin.X < (long)rightOrigin.X + rightTemplate.SizeX &&
    rightOrigin.X < (long)leftOrigin.X + leftTemplate.SizeX &&
    leftOrigin.Y < (long)rightOrigin.Y + rightTemplate.SizeY &&
    rightOrigin.Y < (long)leftOrigin.Y + leftTemplate.SizeY &&
    leftOrigin.Z < (long)rightOrigin.Z + rightTemplate.SizeZ &&
    rightOrigin.Z < (long)leftOrigin.Z + leftTemplate.SizeZ;

    private static void ValidatePrincipal(AccessPrincipal? principal)
    {
    if (principal is { } value && value.Kind is not (AccessPrincipalKind.Agent or AccessPrincipalKind.Group))
    {
        throw new ArgumentOutOfRangeException(nameof(principal));
    }
    }

    private static void ValidateRights(ContainerAccessRights rights, bool allowNone)
    {
    const ContainerAccessRights allRights =
        ContainerAccessRights.Use |
        ContainerAccessRights.Take |
        ContainerAccessRights.Put |
        ContainerAccessRights.Build;
    if ((!allowNone && rights == ContainerAccessRights.None) || (rights & ~allRights) != 0)
    {
        throw new ArgumentOutOfRangeException(nameof(rights));
    }
    }

    private sealed class SiteState(
    WorldGrid world,
    string id,
    StructureTemplateRule template,
    BlockCoordinate origin,
    StructureConstructionState state,
    AccessPrincipal? owner)
    {
    private readonly HashSet<string> _contributorsThisTick = new(StringComparer.Ordinal);
    private readonly Dictionary<AccessPrincipal, ContainerAccessRights> _accessRules = [];
    private readonly SortedDictionary<ushort, int> _materialsConsumed = [];
    private int _blocksPlaced;

        private readonly WorldGrid _world = world;
        public string Id { get; } = id;
        public AccessPrincipal? Owner { get; } = owner;
        public StructureTemplateRule Template { get; } = template;
        public BlockCoordinate Origin { get; } = origin;
        public StructureConstructionState State { get; set; } = state;
        public int WorkCompleted { get; set; }
        public long LastContributionTick { get; set; } = -1;
        public HashSet<string> ContributorsThisTick => _contributorsThisTick;
        public int BlocksPlaced => _blocksPlaced;
        public bool HasAccessRules => _accessRules.Count != 0;
        public IReadOnlyDictionary<ushort, int> MaterialsConsumed => _materialsConsumed;
        public ContainerAccessRule[] AccessRules =>
            _accessRules
                .OrderBy(static pair => pair.Key.Kind)
                .ThenBy(static pair => pair.Key.Id)
                .Select(static pair => new ContainerAccessRule(pair.Key, pair.Value))
                .ToArray();

        public bool IsAuthorized(IEnumerable<AccessPrincipal> principals, ContainerAccessRights requiredRight)
        {
            if (_accessRules.Count == 0)
            {
                return true;
            }

            foreach (var principal in principals)
            {
                if (principal == Owner ||
                    (_accessRules.TryGetValue(principal, out var allowed) &&
                     (allowed & requiredRight) == requiredRight))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetAccessRules(IEnumerable<ContainerAccessRule> rules)
        {
            var updated = new Dictionary<AccessPrincipal, ContainerAccessRights>();
            foreach (var rule in rules)
            {
                ValidatePrincipal(rule.Principal);
                ValidateRights(rule.Rights, allowNone: false);
                if (!updated.TryAdd(rule.Principal, rule.Rights))
                {
                    throw new ArgumentException("The structure access list contains duplicate principals.", nameof(rules));
                }
            }

            _accessRules.Clear();
            foreach (var rule in updated)
            {
                _accessRules.Add(rule.Key, rule.Value);
            }
        }

        public void RestoreAccessRules(IEnumerable<ContainerAccessRule> rules) => SetAccessRules(rules);

        public void PlaceThrough(int count)
        {
            while (_blocksPlaced < count)
            {
                var block = Template.Blocks[_blocksPlaced];
                _world.SetBlockOverride(
                    new BlockCoordinate(Origin.X + block.X, Origin.Y + block.Y, Origin.Z + block.Z),
                    new BlockOverride(block.MaterialId, 0, IsAir: false));
                _blocksPlaced++;
            }
        }

        public bool CanPlaceThrough(int count)
        {
            for (var index = _blocksPlaced; index < count; index++)
            {
                var block = Template.Blocks[index];
                var x = Origin.X + block.X;
                var y = Origin.Y + block.Y;
                var z = Origin.Z + block.Z;
                if (_world.GetSurfaceColumn(x, y).SurfaceZ >= z ||
                    _world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var current) && !current.IsAir)
                {
                    return false;
                }
            }

            return true;
        }

        public void RestoreProgress(
            int workCompleted,
            int blocksPlaced,
            long lastContributionTick,
            IEnumerable<string> contributors)
        {
            WorkCompleted = workCompleted;
            _blocksPlaced = blocksPlaced;
            LastContributionTick = lastContributionTick;
            foreach (var contributor in contributors)
            {
                if (string.IsNullOrWhiteSpace(contributor) || !_contributorsThisTick.Add(contributor))
                {
                    throw new InvalidDataException("Construction checkpoint contains invalid contributors.");
                }
            }
        }

        public bool HasValidCheckpointProgress()
        {
            if (WorkCompleted < 0 ||
                WorkCompleted > Template.WorkTicks ||
                _blocksPlaced < 0 ||
                _blocksPlaced > Template.Blocks.Count ||
                LastContributionTick < -1 ||
                (LastContributionTick == -1) != (_contributorsThisTick.Count == 0))
            {
                return false;
            }

            if (State == StructureConstructionState.Abandoned)
            {
                return _blocksPlaced == 0;
            }

            var expectedBlocks = Template.WorkTicks == 0
                ? Template.Blocks.Count
                : (int)((long)WorkCompleted * Template.Blocks.Count / Template.WorkTicks);
            if (_blocksPlaced != expectedBlocks)
            {
                return false;
            }

            var expectedMaterials = GetRequiredMaterials(WorkCompleted);
            if (expectedMaterials.Count != _materialsConsumed.Count ||
                expectedMaterials.Any(entry =>
                    !_materialsConsumed.TryGetValue(entry.Key, out var consumed) ||
                    consumed != entry.Value))
            {
                return false;
            }

            return State switch
            {
                StructureConstructionState.Planned => WorkCompleted == 0 && Template.WorkTicks > 0,
                StructureConstructionState.InProgress => WorkCompleted > 0 && WorkCompleted < Template.WorkTicks,
                StructureConstructionState.Completed => WorkCompleted == Template.WorkTicks,
                _ => false,
            };
        }

        public void RemovePlacedBlocks()
        {
            for (var index = 0; index < _blocksPlaced; index++)
            {
                var block = Template.Blocks[index];
                var coordinate = new BlockCoordinate(Origin.X + block.X, Origin.Y + block.Y, Origin.Z + block.Z);
                var expected = new BlockOverride(block.MaterialId, 0, IsAir: false);
                if (_world.TryGetBlockOverride(coordinate, out var current) && current == expected)
                {
                    _world.RemoveBlockOverride(coordinate);
                }
            }

            _blocksPlaced = 0;
        }

        public StructureConstructionSite ToSnapshot() =>
            new(
                Id,
                Template.Id,
                Origin,
                State,
                Template.WorkTicks,
                WorkCompleted,
                _blocksPlaced,
                (Template.Materials ?? [])
                    .Select(material => new StructureMaterialRule(
                        material.ItemTypeId,
                        material.Quantity - _materialsConsumed.GetValueOrDefault(material.ItemTypeId)))
                    .Where(static material => material.Quantity > 0)
                    .ToArray());

        public Dictionary<ushort, int> GetMaterialDelta(int nextWorkCompleted)
        {
            var required = GetRequiredMaterials(nextWorkCompleted);
            return required
                .Where(entry => entry.Value > _materialsConsumed.GetValueOrDefault(entry.Key))
                .ToDictionary(
                    static entry => entry.Key,
                    entry => entry.Value - _materialsConsumed.GetValueOrDefault(entry.Key));
        }

        public void RecordMaterialsConsumed(IReadOnlyDictionary<ushort, int> materials)
        {
            foreach (var (itemTypeId, quantity) in materials)
            {
                _materialsConsumed[itemTypeId] = checked(
                    _materialsConsumed.GetValueOrDefault(itemTypeId) + quantity);
            }
        }

        public void RestoreMaterialsConsumed(IEnumerable<StructureMaterialBalance> materials)
        {
            foreach (var material in materials)
            {
                if (material is null ||
                    material.Quantity <= 0 ||
                    !_materialsConsumed.TryAdd(material.ItemTypeId, material.Quantity))
                {
                    throw new InvalidDataException("Construction checkpoint contains invalid material progress.");
                }
            }
        }

        private Dictionary<ushort, int> GetRequiredMaterials(int workCompleted)
        {
            if (Template.WorkTicks == 0)
            {
                return [];
            }

            return (Template.Materials ?? [])
                .ToDictionary(
                    static material => material.ItemTypeId,
                    material => (int)(((long)material.Quantity * workCompleted + Template.WorkTicks - 1) /
                                      Template.WorkTicks));
        }
    }

    private sealed record ConstructionCheckpoint(
        int Version,
        StructureTemplateRule[]? Templates,
        ConstructionSiteCheckpoint[]? Sites);

    private sealed record ConstructionSiteCheckpoint(
        string Id,
        string TemplateId,
        BlockCoordinate Origin,
        StructureConstructionState State,
        int WorkCompleted,
        int BlocksPlaced,
        long LastContributionTick,
        string[] Contributors,
        AccessPrincipal? Owner,
        ContainerAccessRule[] AccessRules,
        StructureMaterialBalance[]? MaterialsConsumed = null);
}
