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
    private readonly StructureConstructionSystem _construction;

    public StructureEffectConsumer(StructureConstructionSystem construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        _construction = construction;
    }

    public StructureUsage? QueryShelter(
        ulong agentId,
        BlockCoordinate feetPosition,
        IEnumerable<AccessPrincipal>? groupPrincipals = null)
    {        var principals = new List<AccessPrincipal> { AccessPrincipal.ForAgent(agentId) };
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
            if (!string.Equals(effect.Type, ShelterEffectType, StringComparison.Ordinal) ||
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

    public StructureUsage? QueryShelterWithGroups(
        ulong agentId,
        BlockCoordinate feetPosition,
        GroupMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return QueryShelter(agentId, feetPosition, membership.PrincipalsOf(agentId));
    }

    private static bool Contains(ActiveStructureEffect effect, BlockCoordinate position) =>
        position.X >= effect.Minimum.X && position.X <= effect.Maximum.X &&
        position.Y >= effect.Minimum.Y && position.Y <= effect.Maximum.Y &&
        position.Z >= effect.Minimum.Z && position.Z <= effect.Maximum.Z;
}
