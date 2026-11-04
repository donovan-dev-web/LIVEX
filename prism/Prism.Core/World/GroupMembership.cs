using Prism.Core.Actions;

namespace Prism.Core.World;

/// <summary>
/// Opaque group membership used by access rules: an agent inherits the principals of the groups
/// it belongs to. Membership is session configuration supplied by the control plane.
/// </summary>
public sealed class GroupMembership
{
    private readonly SortedDictionary<ulong, SortedSet<ulong>> _groups = [];

    public int GroupCount => _groups.Count;

    public int MembershipCount => _groups.Values.Sum(static members => members.Count);

    public bool Join(ulong groupId, ulong agentId)
    {
        if (!_groups.TryGetValue(groupId, out var members))
        {
            members = [];
            _groups.Add(groupId, members);
        }

        return members.Add(agentId);
    }

    public bool Leave(ulong groupId, ulong agentId)
    {
        if (!_groups.TryGetValue(groupId, out var members) || !members.Remove(agentId))
        {
            return false;
        }

        if (members.Count == 0)
        {
            _groups.Remove(groupId);
        }

        return true;
    }

    public IReadOnlyList<ulong> GroupsOf(ulong agentId) =>
        _groups
            .Where(pair => pair.Value.Contains(agentId))
            .Select(static pair => pair.Key)
            .ToArray();

    public IReadOnlyList<ulong> MembersOf(ulong groupId) =>
        _groups.TryGetValue(groupId, out var members) ? members.ToArray() : [];

    public IReadOnlyList<AccessPrincipal> PrincipalsOf(ulong agentId) =>
        GroupsOf(agentId).Select(AccessPrincipal.ForGroup).ToArray();
}
