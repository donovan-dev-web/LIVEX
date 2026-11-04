using Prism.Core.Actions;

namespace Prism.Core.World;

public sealed record StructureUsage(string SiteId, string EffectType, int FatigueRecoveryBonusPermille);

/// <summary>
/// Consumes active structure effects. An agent standing inside an active effect volume only
/// benefits from it when the structure access rules grant the <see cref="ContainerAccessRights.Use"/>
/// right, or when it owns the structure.
/// </summary>
public sealed class StructureEffectConsumer
{
    public const string ShelterEffectType = "shelter";
    public const string WaterAccessEffectType = "water_access";
    public const string StorageEffectType = "storage";
    private readonly StructureConstructionSystem _construction;

    public StructureEffectConsumer(StructureConstructionSystem construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        _construction = construction;
    }

    public StructureUsage? QueryShelter(
        ulong agentId,
        BlockCoordinate feetPosition,
        IEnumerable<AccessPrincipal>? groupPrincipals = null) =>
        QueryEffect(agentId, feetPosition, ShelterEffectType, groupPrincipals);

    public StructureUsage? QueryWaterAccess(
        ulong agentId,
        BlockCoordinate feetPosition,
        IEnumerable<AccessPrincipal>? groupPrincipals = null) =>
        QueryEffect(agentId, feetPosition, WaterAccessEffectType, groupPrincipals);

    public StructureUsage? QueryStorage(
        ulong agentId,
        BlockCoordinate feetPosition,
        IEnumerable<AccessPrincipal>? groupPrincipals = null) =>
        QueryEffect(agentId, feetPosition, StorageEffectType, groupPrincipals);

    public StructureUsage? QueryShelterWithGroups(
        ulong agentId,
        BlockCoordinate feetPosition,
        GroupMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return QueryShelter(agentId, feetPosition, membership.PrincipalsOf(agentId));
    }

    public StructureUsage? QueryWaterAccessWithGroups(
        ulong agentId,
        BlockCoordinate feetPosition,
        GroupMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return QueryWaterAccess(agentId, feetPosition, membership.PrincipalsOf(agentId));
    }

    public StructureUsage? QueryStorageWithGroups(
        ulong agentId,
        BlockCoordinate feetPosition,
        GroupMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return QueryStorage(agentId, feetPosition, membership.PrincipalsOf(agentId));
    }

    /// <summary>
    /// Returns the canonical active structure of <paramref name="effectType"/> whose volume contains
    /// <paramref name="position"/>, ignoring access rules. Used by world-side effects such as the
    /// storage slowdown, which belongs to the container and not to a specific agent.
    /// </summary>
    public StructureUsage? QueryVolume(BlockCoordinate position, string effectType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectType);
        StructureUsage? selected = null;
        foreach (var effect in _construction.GetActiveEffects())
        {
            if (!string.Equals(effect.Type, effectType, StringComparison.Ordinal) ||
                !Contains(effect, position))
            {
                continue;
            }

            var usage = new StructureUsage(effect.SiteId, effect.Type, effect.FatigueRecoveryBonusPermille);
            if (selected is null || string.CompareOrdinal(usage.SiteId, selected.SiteId) < 0)
            {
                selected = usage;
            }
        }

        return selected;
    }

    /// <summary>
    /// Returns the canonical (lowest <c>siteId</c>) active structure of <paramref name="effectType"/>
    /// whose volume contains <paramref name="feetPosition"/> and whose access rules admit the agent.
    /// </summary>
    public StructureUsage? QueryEffect(
        ulong agentId,
        BlockCoordinate feetPosition,
        string effectType,
        IEnumerable<AccessPrincipal>? groupPrincipals = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectType);
        var principals = new List<AccessPrincipal> { AccessPrincipal.ForAgent(agentId) };
        if (groupPrincipals is not null)
        {
            principals.AddRange(groupPrincipals);
            principals = principals
                .Distinct()
                .OrderBy(static principal => principal.Kind)
                .ThenBy(static principal => principal.Id)
                .ToList();
        }

        StructureUsage? selected = null;
        foreach (var effect in _construction.GetActiveEffects())
        {
            if (!string.Equals(effect.Type, effectType, StringComparison.Ordinal) ||
                !Contains(effect, feetPosition) ||
                !_construction.CheckAccess(effect.SiteId, principals, ContainerAccessRights.Use).Allowed)
            {
                continue;
            }

            var usage = new StructureUsage(effect.SiteId, effect.Type, effect.FatigueRecoveryBonusPermille);
            if (selected is null || string.CompareOrdinal(usage.SiteId, selected.SiteId) < 0)
            {
                selected = usage;
            }
        }

        return selected;
    }

    private static bool Contains(ActiveStructureEffect effect, BlockCoordinate position) =>
        position.X >= effect.Minimum.X && position.X <= effect.Maximum.X &&
        position.Y >= effect.Minimum.Y && position.Y <= effect.Maximum.Y &&
        position.Z >= effect.Minimum.Z && position.Z <= effect.Maximum.Z;
}
