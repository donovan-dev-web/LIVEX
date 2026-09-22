using Simulation.Core.Cognition;
using Simulation.Core.Configuration;

namespace Simulation.Core.Social;

/// <summary>Événement discret de formation d'un groupe (SYNE-060, API_CONTRACTS.md §2.2).</summary>
public sealed record GroupFormation(ulong GroupId, ulong Tick, IReadOnlyList<ulong> Members, double MeanCohesion);

/// <summary>Événement discret de dissolution d'un groupe (SYNE-060).</summary>
public sealed record GroupDissolution(
    ulong GroupId,
    ulong Tick,
    ulong FormedTick,
    IReadOnlyList<ulong> Members,
    bool Success,
    int MembersIn,
    int MembersOut);

/// <summary>Événement discret de décision collective (SYNE-061, consensus pondéré par la confiance).</summary>
public sealed record GroupDecision(ulong GroupId, ulong Tick, ulong LeaderId, DesireKind Decision, double Consensus);

/// <summary>
/// Système de groupes émergents (SYNE-060/061, SYSTEM_SPEC.md §5, décisions
/// n°23/24). Il n'invente aucune coalition scriptée : les groupes naissent de la
/// **cohésion** constatée entre les entités —
/// <c>confiance réciproque × affinité (croyances partagées + buts partagés)</c>.
///
/// Exécuté à fréquence <c>reviewIntervalTicks</c> (LOD déterministe), après le
/// pipeline cognitif et la communication de masse (ordre causal strict) :
///
/// <list type="number">
/// <item><b>Liens sociaux</b> : paires (entité, entité) en confiance réciproque
///  ≥ <c>trustThreshold</c> partageant au moins une croyance ou un but — la
///  « proximité sociale » (décision n°24) ;</item>
/// <item><b>Composantes connexes</b> du graphe = candidats groupes (union-find
///  déterministe, racine = plus petit id) ; taille ≥ <c>minGroupSize</c> ;</item>
/// <item><b>Cycle de vie</b> : continuité par ensemble de membres identique
///  (même id, durée cumulée) sinon formation / dissolution ;</item>
/// <item><b>Leader émergent</b> (SYNE-061) : membre de plus forte confiance
///  entrante intra-groupe (départage par id minimal) ;</item>
/// <item><b>Décisions collectives</b> (SYNE-061) : adoption par quorum
///  <c>consensusThreshold</c> sur l'intention dominante des membres.</item>
/// </list>
///
/// Déterminisme : aucun tirage du PRNG global ; toutes les itérations suivent
/// des ordres triés par identifiant (DETERMINISM.md §3).
/// </summary>
public sealed class GroupSystem
{
    private readonly GroupSettings _settings;
    private readonly Dictionary<ulong, Group> _groups = new();
    private readonly List<GroupFormation> _lastFormed = new();
    private readonly List<GroupDissolution> _lastDissolved = new();
    private readonly List<GroupDecision> _lastDecisions = new();
    private ulong _nextGroupId = 1;

    public GroupSystem(GroupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public GroupSettings Settings => _settings;

    /// <summary>Groupes actifs (ordre d'identifiant).</summary>
    public IReadOnlyList<Group> Active =>
        _groups.Values.OrderBy(group => group.Id).ToList();

    /// <summary>Formations du tick courant (ordre de formation).</summary>
    public IReadOnlyList<GroupFormation> LastFormed => _lastFormed;

    /// <summary>Dissolutions du tick courant (ordre de dissolution).</summary>
    public IReadOnlyList<GroupDissolution> LastDissolved => _lastDissolved;

    /// <summary>Décisions collectives adoptées au tick courant.</summary>
    public IReadOnlyList<GroupDecision> LastDecisions => _lastDecisions;

    /// <summary>Révision des groupes au tick courant (LOD <c>reviewIntervalTicks</c>).</summary>
    public void Step(ulong tick, IReadOnlyDictionary<ulong, MindState> minds)
    {
        ArgumentNullException.ThrowIfNull(minds);
        _lastFormed.Clear();
        _lastDissolved.Clear();
        _lastDecisions.Clear();

        if (!_settings.Enabled || tick == 0 || tick % (ulong)_settings.ReviewIntervalTicks != 0)
        {
            return;
        }

        IReadOnlyList<List<ulong>> components = ComputeComponents(minds);
        var current = new List<(List<ulong> Members, double MeanCohesion)>(components.Count);
        foreach (List<ulong> members in components)
        {
            if (members.Count < _settings.MinGroupSize)
            {
                continue;
            }

            current.Add((members, MeanCohesionOf(members, minds)));
        }

        var unmatched = new HashSet<int>(Enumerable.Range(0, current.Count));
        var dissolved = new List<ulong>();

        foreach (Group group in _groups.Values.OrderBy(group => group.Id).ToList())
        {
            int match = FindMemberSet(current, unmatched, group.Members);
            if (match >= 0)
            {
                unmatched.Remove(match);
                group.UpdateMembers(current[match].Members);
                RefreshEmergent(group, minds, tick);
            }
            else
            {
                dissolved.Add(group.Id);
            }
        }

        foreach (ulong groupId in dissolved)
        {
            if (_groups.Remove(groupId, out Group? old))
            {
                _lastDissolved.Add(new GroupDissolution(
                    old.Id,
                    tick,
                    old.BornTick,
                    old.Members,
                    old.HadDecision,
                    old.Members.Count,
                    old.Members.Count));
            }
        }

        foreach (int index in unmatched.OrderBy(i => i))
        {
            var group = new Group(_nextGroupId++, tick, current[index].Members)
            {
                MeanCohesion = current[index].MeanCohesion,
            };
            RefreshEmergent(group, minds, tick);
            _groups[group.Id] = group;
            _lastFormed.Add(new GroupFormation(
                group.Id,
                tick,
                group.Members,
                group.MeanCohesion));
        }
    }

    /// <summary>
    /// Composantes connexes du graphe de cohésion, ordre déterministe (racine
    /// = plus petit identifiant, puis tri par racine). Union-find sans PRNG.
    /// </summary>
    private IReadOnlyList<List<ulong>> ComputeComponents(IReadOnlyDictionary<ulong, MindState> minds)
    {
        List<ulong> ordered = minds.Keys.OrderBy(id => id).ToList();
        var parent = new Dictionary<ulong, ulong>(ordered.Count);
        foreach (ulong id in ordered)
        {
            parent[id] = id;
        }

        var edges = new List<(ulong A, ulong B)>();
        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                if (AreBonded(minds[ordered[i]], ordered[i], minds[ordered[j]], ordered[j]))
                {
                    edges.Add((ordered[i], ordered[j]));
                }
            }
        }

        edges.Sort(static (a, b) => a.A != b.A ? a.A.CompareTo(b.A) : a.B.CompareTo(b.B));
        foreach ((ulong a, ulong b) in edges)
        {
            Union(parent, a, b);
        }

        var buckets = new Dictionary<ulong, List<ulong>>();
        foreach (ulong id in ordered)
        {
            ulong root = Find(parent, id);
            if (!buckets.TryGetValue(root, out List<ulong>? members))
            {
                members = new List<ulong>();
                buckets[root] = members;
            }

            members.Add(id);
        }

        return buckets.Values
            .OrderBy(members => members[0])
            .Select(members => members)
            .ToList();
    }

    private static ulong Find(Dictionary<ulong, ulong> parent, ulong x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }

        return x;
    }

    private static void Union(Dictionary<ulong, ulong> parent, ulong a, ulong b)
    {
        ulong rootA = Find(parent, a);
        ulong rootB = Find(parent, b);
        if (rootA == rootB)
        {
            return;
        }

        parent[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
    }

    /// <summary>
    /// Lien social (décision n°24) : confiance réciproque ≥ <c>trustThreshold</c>
    /// ET au moins une part commune (croyance partagée ou but partagé).
    /// </summary>
    private bool AreBonded(MindState self, ulong selfId, MindState other, ulong otherId)
    {
        double minTrust = Math.Min(self.Trust.TrustWith(otherId), other.Trust.TrustWith(selfId));
        if (minTrust < _settings.TrustThreshold)
        {
            return false;
        }

        return Affinity(self, other) > 1.0;
    }

    /// <summary>Affinité = 1 + bonus par but partagé + bonus par croyance partagée (décision n°24).</summary>
    private double Affinity(MindState self, MindState other)
    {
        double affinity = 1.0;

        if (self.Intention is { } selfIntention &&
            other.Intention is { } otherIntention &&
            selfIntention.Kind != DesireKind.Idle &&
            selfIntention.Kind == otherIntention.Kind)
        {
            affinity += _settings.GoalAlignmentBonus;
        }

        affinity += SharedBeliefCount(self, other) * _settings.SharedBeliefBonus;
        return affinity;
    }

    private static int SharedBeliefCount(MindState self, MindState other)
    {
        int shared = 0;
        foreach (Belief belief in self.Beliefs.OrderedByFact())
        {
            if (belief.Confidence < 0.5)
            {
                continue;
            }

            if (other.Beliefs.TryGet(belief.Fact, out Belief? counterpart) && counterpart.Confidence >= 0.5)
            {
                shared++;
            }
        }

        return shared;
    }

    private double MeanCohesionOf(IReadOnlyList<ulong> members, IReadOnlyDictionary<ulong, MindState> minds)
    {
        if (members.Count < 2)
        {
            return 0.0;
        }

        double sum = 0.0;
        int count = 0;
        for (int i = 0; i < members.Count; i++)
        {
            for (int j = i + 1; j < members.Count; j++)
            {
                sum += CohesionOf(minds[members[i]], members[i], minds[members[j]], members[j]);
                count++;
            }
        }

        return count == 0 ? 0.0 : sum / count;
    }

    private double CohesionOf(MindState self, ulong selfId, MindState other, ulong otherId)
    {
        double minTrust = Math.Min(self.Trust.TrustWith(otherId), other.Trust.TrustWith(selfId));
        return minTrust * Affinity(self, other);
    }

    private int FindMemberSet(
        IReadOnlyList<(List<ulong> Members, double MeanCohesion)> current,
        HashSet<int> unmatched,
        IReadOnlyList<ulong> expected)
    {
        foreach (int index in unmatched.OrderBy(i => i))
        {
            List<ulong> members = current[index].Members;
            if (members.Count != expected.Count)
            {
                continue;
            }

            bool equal = true;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != expected[i])
                {
                    equal = false;
                    break;
                }
            }

            if (equal)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Recalcule les caractères émergents (SYNE-061) : leader par confiance
    /// intra-groupe entrante, puis décision collective par consensus.
    /// </summary>
    private void RefreshEmergent(Group group, IReadOnlyDictionary<ulong, MindState> minds, ulong tick)
    {
        ulong? leader = EmergentLeader(group.Members, minds);
        group.LeaderId = leader;

        (DesireKind? decision, double consensus) = CollectiveDecision(group.Members, minds, leader);
        group.Decision = decision;
        group.Consensus = consensus;
        if (decision is not null)
        {
            group.HadDecision = true;
            if (leader is { } leaderId)
            {
                _lastDecisions.Add(new GroupDecision(group.Id, tick, leaderId, decision.Value, consensus));
            }
        }
    }

    /// <summary>
    /// Leader émergent (SYNE-061) : membre dont la somme des confiances internes
    /// entrantes est maximale (départage déterministe : identifiant minimal).
    /// </summary>
    private ulong? EmergentLeader(IReadOnlyList<ulong> members, IReadOnlyDictionary<ulong, MindState> minds)
    {
        ulong best = 0;
        double bestScore = double.MinValue;
        foreach (ulong candidate in members)
        {
            double score = 0.0;
            foreach (ulong peer in members)
            {
                if (peer != candidate)
                {
                    score += minds[candidate].Trust.TrustWith(peer);
                }
            }

            if (score > bestScore ||
                (Math.Abs(score - bestScore) < 1e-12 && (best == 0 || candidate < best)))
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best == 0 ? null : best;
    }

    /// <summary>
    /// Décision collective (SYNE-061) : intention dominante des membres travaux
    /// adoptée si sa part ≤…&gt; quorum <c>consensusThreshold</c>. Le vote est
    /// pondéré par la confiance du membre envers le leader émergent (la confiance
    /// « compte », décision n°24) ; consensus nul en l'absence de leader.
    /// </summary>
    private (DesireKind? Decision, double Consensus) CollectiveDecision(
        IReadOnlyList<ulong> members,
        IReadOnlyDictionary<ulong, MindState> minds,
        ulong? leader)
    {
        if (members.Count == 0 || leader is not { } leaderId)
        {
            return (null, 0.0);
        }

        var scores = new Dictionary<DesireKind, double>();
        foreach (ulong member in members)
        {
            DesireKind kind = minds[member].Intention?.Kind ?? DesireKind.Idle;
            if (kind == DesireKind.Idle)
            {
                continue;
            }

            double weight = minds[member].Trust.TrustWith(leaderId);
            scores[kind] = scores.TryGetValue(kind, out double current) ? current + weight : weight;
        }

        if (scores.Count == 0)
        {
            return (null, 0.0);
        }

        double total = 0.0;
        foreach (double value in scores.Values)
        {
            total += value;
        }

        DesireKind bestKind = scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => (int)pair.Key)
            .First().Key;
        double consensus = scores[bestKind] / total;

        return consensus >= _settings.ConsensusThreshold ? (bestKind, consensus) : (null, 0.0);
    }
}