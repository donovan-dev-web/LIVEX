// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Système de groupes émergents (SYNE-060/061).

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Social;

/// <summary>Événement discret de formation d'un groupe (SYNE-060).</summary>
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
/// Système de groupes émergents (SYNE-060/061, décisions
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
/// « proximité sociale » ;</item>
/// <item><b>Composantes connexes</b> du graphe = candidats groupes (union-find
///  déterministe, racine = plus petit id) ; taille ≥ <c>minGroupSize</c> ;</item>
/// <item><b>Cycle de vie</b> : continuité par ensemble de membres identique
///  (même id, durée cumulée) sinon formation / dissolution ;</item>
/// <item><b>Leader émergent</b> (SYNE-061) : membre de plus forte confiance
///  entrante intra-groupe (départage par id minimal) ;</item>
/// <item><b>Décisions collectives</b> (SYNE-061) : adoption par quorum
///  <c>consensusThreshold</c> sur l'intention dominante des membres —
///  <b>exposées seulement</b> (état du groupe, `LastDecisions`, snapshot et
///  événement <c>group_decision</c>), jamais injectées dans l'utilité
///  individuelle (arbitrage A0-3 du 17/10/2026, qui remplace la propagation
///  SYNE-076) ;</item>
/// <item><b>Distances des paires liées</b> (A0-3) : la liaison n'a aucune clause
///  de proximité explicite — la décroissance de confiance (0,9/tick sans
///  interaction) éteint de lui-même un lien distants — et la distance des paires
///  liées est <b>instrumentée</b> (max/moyenne par groupe, <c>null</c> = non
///  mesuré).</item>
/// </list>
///
/// Déterminisme : aucun tirage du PRNG global ; toutes les itérations suivent
/// des ordres triés par identifiant.
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

    /// <summary>Prochain identifiant de groupe (persistance bit-à-bit).</summary>
    internal ulong NextGroupId => _nextGroupId;

    /// <summary>Restauration des groupes actifs et du compteur d'identifiants (persistance bit-à-bit).</summary>
    internal void RestoreState(IEnumerable<Group> groups, ulong nextGroupId)
    {
        ArgumentNullException.ThrowIfNull(groups);
        _groups.Clear();
        foreach (Group group in groups)
        {
            _groups[group.Id] = group;
        }

        _nextGroupId = Math.Max(nextGroupId, _groups.Keys.Count == 0 ? 1 : _groups.Keys.Max() + 1);
    }

    /// <summary>Groupes actifs (ordre d'identifiant).</summary>
    public IReadOnlyList<Group> Active =>
        _groups.Values.OrderBy(group => group.Id).ToList();

    /// <summary>Formations du tick courant (ordre de formation).</summary>
    public IReadOnlyList<GroupFormation> LastFormed => _lastFormed;

    /// <summary>Dissolutions du tick courant (ordre de dissolution).</summary>
    public IReadOnlyList<GroupDissolution> LastDissolved => _lastDissolved;

    /// <summary>Décisions collectives adoptées au tick courant.</summary>
    public IReadOnlyList<GroupDecision> LastDecisions => _lastDecisions;

    /// <summary>
    /// Révision des groupes au tick courant (LOD <c>reviewIntervalTicks</c>),
    /// sans positions : la contrainte de proximité spatiale n'est alors
    /// <b>pas évaluable</b> et est réputée satisfaite (comportement historique des
    /// appels hors moteur). Le pipeline, lui, fournit toujours les positions
    /// finales du tick.
    /// </summary>
    public void Step(ulong tick, IReadOnlyDictionary<ulong, MindState> minds) => Step(tick, minds, positions: null);

    /// <summary>
    /// Révision des groupes au tick courant (LOD <c>reviewIntervalTicks</c>).
    /// <paramref name="positions"/> porte les positions <b>finales</b> du tick et
    /// sert uniquement à <b>instrumenter</b> la distance des paires liées
    /// (A0-3) : la liaison n'a plus aucune clause de proximité explicite.
    /// </summary>
    public void Step(
        ulong tick,
        IReadOnlyDictionary<ulong, MindState> minds,
        IReadOnlyDictionary<ulong, Position>? positions)
    {
        ArgumentNullException.ThrowIfNull(minds);
        _lastFormed.Clear();
        _lastDissolved.Clear();
        _lastDecisions.Clear();

        if (!_settings.Enabled || tick == 0 || tick % (ulong)_settings.ReviewIntervalTicks != 0)
        {
            return;
        }

        Dictionary<ulong, Belief[]> sortedBeliefs = SortedBeliefsOf(minds);
        IReadOnlyList<(List<ulong> Members, double? MaxBondDistance, double? MeanBondDistance)> components =
            ComputeComponents(minds, sortedBeliefs, positions);
        var current = new List<(List<ulong> Members, double MeanCohesion, double? MaxBondDistance, double? MeanBondDistance)>(components.Count);
        foreach ((List<ulong> members, double? maxBondDistance, double? meanBondDistance) in components)
        {
            if (members.Count < _settings.MinGroupSize)
            {
                continue;
            }

            current.Add((members, MeanCohesionOf(members, minds, sortedBeliefs), maxBondDistance, meanBondDistance));
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
                // La cohésion doit suivre les membres *et* leur évolution : la
                // laisser figée à la formation exposait un `meanCohesion` périmé
                // dans le snapshot et les formations/dissolutions rapportées.
                group.MeanCohesion = current[match].MeanCohesion;
                group.MaxBondDistance = current[match].MaxBondDistance;
                group.MeanBondDistance = current[match].MeanBondDistance;
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
                MaxBondDistance = current[index].MaxBondDistance,
                MeanBondDistance = current[index].MeanBondDistance,
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
    /// Croyances partagées, indexées par esprit et **pré-triées une seule fois par
    /// revue**. <see cref="SharedBeliefCount"/> parcourait auparavant
    /// <c>Beliefs.OrderedByFact()</c> — un tri + une allocation — pour *chaque paire*
    /// d'entités : le coût de la revue de groupes était O(N² × B log B) au lieu de
    /// O(N² × B), avec une allocation par paire.
    /// </summary>
    private static Dictionary<ulong, Belief[]> SortedBeliefsOf(IReadOnlyDictionary<ulong, MindState> minds)
    {
        var sorted = new Dictionary<ulong, Belief[]>(minds.Count);
        foreach ((ulong id, MindState mind) in minds)
        {
            sorted[id] = mind.Beliefs.OrderedByFact().ToArray();
        }

        return sorted;
    }

    /// <summary>
    /// Composantes connexes du graphe de cohésion, ordre déterministe (racine
    /// = plus petit identifiant, puis tri par racine). Union-find sans PRNG.
    /// Chaque composante porte les <b>distances des paires liées</b> qui la
    /// composent (instrumentation A0-3) : max et moyenne, <c>null</c> quand la
    /// table de positions n'est pas fournie — une distance non fournie est
    /// <i>non mesurée</i>, jamais zéro.
    /// </summary>
    private IReadOnlyList<(List<ulong> Members, double? MaxBondDistance, double? MeanBondDistance)> ComputeComponents(
        IReadOnlyDictionary<ulong, MindState> minds,
        Dictionary<ulong, Belief[]> sortedBeliefs,
        IReadOnlyDictionary<ulong, Position>? positions)
    {
        List<ulong> ordered = minds.Keys.OrderBy(id => id).ToList();
        var parent = new Dictionary<ulong, ulong>(ordered.Count);
        foreach (ulong id in ordered)
        {
            parent[id] = id;
        }

        var edges = new List<(ulong A, ulong B, double? Distance)>();
        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                if (AreBonded(minds[ordered[i]], ordered[i], minds[ordered[j]], ordered[j], sortedBeliefs))
                {
                    edges.Add((ordered[i], ordered[j], DistanceBetween(ordered[i], ordered[j], positions)));
                }
            }
        }

        edges.Sort(static (a, b) => a.A != b.A ? a.A.CompareTo(b.A) : a.B.CompareTo(b.B));
        foreach ((ulong a, ulong b, _) in edges)
        {
            Union(parent, a, b);
        }

        // Distances des paires liées par composante (A0-3) : agrégées par racine.
        var distancesByRoot = new Dictionary<ulong, List<double>>();
        foreach ((ulong a, _, double? distance) in edges)
        {
            if (distance is not { } value)
            {
                continue;
            }

            ulong root = Find(parent, a);
            if (!distancesByRoot.TryGetValue(root, out List<double>? values))
            {
                values = new List<double>();
                distancesByRoot[root] = values;
            }

            values.Add(value);
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

        var components = new List<(List<ulong>, double?, double?)>(buckets.Count);
        foreach ((ulong root, List<ulong> members) in buckets.OrderBy(pair => pair.Key))
        {
            List<double>? values = distancesByRoot.TryGetValue(root, out List<double>? found) ? found : null;
            components.Add((
                members,
                values is { Count: > 0 } ? values.Max() : null,
                values is { Count: > 0 } ? values.Average() : null));
        }

        return components;
    }

    /// <summary>
    /// Purge des membres décédés (SYNE-074, mortalité) : les survivants recalculent
    /// leurs caractères émergents ; un groupe tombé sous <c>minGroupSize</c> est
    /// dissous (dissolution rapportée, MembersOut = nombre de morts).
    /// </summary>
    public void PurgeDeceased(
        IReadOnlyCollection<ulong> deceased,
        ulong tick,
        IReadOnlyDictionary<ulong, MindState> minds,
        IReadOnlyDictionary<ulong, Position>? positions = null)
    {
        ArgumentNullException.ThrowIfNull(deceased);
        ArgumentNullException.ThrowIfNull(minds);
        if (deceased.Count == 0)
        {
            return;
        }

        var dead = deceased.ToHashSet();
        foreach (ulong groupId in _groups.Keys.OrderBy(id => id).ToList())
        {
            Group group = _groups[groupId];
            List<ulong> survivors = group.Members
                .Where(id => !dead.Contains(id))
                .OrderBy(id => id)
                .ToList();

            if (survivors.Count < _settings.MinGroupSize)
            {
                _groups.Remove(groupId);
                _lastDissolved.Add(new GroupDissolution(
                    group.Id,
                    tick,
                    group.BornTick,
                    survivors,
                    group.HadDecision,
                    MembersIn: 0,
                    MembersOut: group.Members.Count - survivors.Count));
            }
            else if (survivors.Count != group.Members.Count)
            {
                group.UpdateMembers(survivors);
                Dictionary<ulong, Belief[]> survivorBeliefs = SortedBeliefsOf(minds);
                group.MeanCohesion = MeanCohesionOf(survivors, minds, survivorBeliefs);
                (double? maxDistance, double? meanDistance) = BondDistancesOf(survivors, minds, survivorBeliefs, positions);
                group.MaxBondDistance = maxDistance;
                group.MeanBondDistance = meanDistance;
                RefreshEmergent(group, minds, tick);
            }
        }
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
    /// Lien social : confiance réciproque ≥ <c>trustThreshold</c>
    /// ET au moins une part commune (croyance partagée ou but partagé).
    /// <para>
    /// Aucune clause de proximité explicite (arbitrage A0-3 du 17/10/2026) : la
    /// cohésion est une relation <b>entretenue</b> — la confiance décroît de
    /// 0,9 par tick sans interaction, un lien distants s'éteint donc de lui-même.
    /// La distance des paires liées est <b>mesurée et exposée</b> (max/moyenne
    /// par groupe), jamais filtrante (l'ancienne clause D3 `proximityRequired` a
    /// été retirée avec la propagation SYNE-076).
    /// </para>
    /// </summary>
    private bool AreBonded(
        MindState self,
        ulong selfId,
        MindState other,
        ulong otherId,
        Dictionary<ulong, Belief[]> sortedBeliefs)
    {
        double minTrust = Math.Min(self.Trust.TrustWith(otherId), other.Trust.TrustWith(selfId));
        if (minTrust < _settings.TrustThreshold)
        {
            return false;
        }

        return Affinity(self, other, selfId, sortedBeliefs) > 1.0;
    }

    /// <summary>Distance entre deux entités positionnées — <c>null</c> = non mesurée.</summary>
    private static double? DistanceBetween(ulong a, ulong b, IReadOnlyDictionary<ulong, Position>? positions)
    {
        if (positions is null ||
            !positions.TryGetValue(a, out Position positionA) ||
            !positions.TryGetValue(b, out Position positionB))
        {
            return null;
        }

        return positionA.DistanceTo(positionB);
    }

    /// <summary>
    /// Distances des paires liées d'un ensemble de membres (instrumentation
    /// A0-3, chemin de purge) : max et moyenne, <c>null</c> si aucune paire liée
    /// n'est mesurable.
    /// </summary>
    private (double? Max, double? Mean) BondDistancesOf(
        IReadOnlyList<ulong> members,
        IReadOnlyDictionary<ulong, MindState> minds,
        Dictionary<ulong, Belief[]> sortedBeliefs,
        IReadOnlyDictionary<ulong, Position>? positions)
    {
        var distances = new List<double>();
        for (int i = 0; i < members.Count; i++)
        {
            for (int j = i + 1; j < members.Count; j++)
            {
                ulong a = members[i];
                ulong b = members[j];
                if (!AreBonded(minds[a], a, minds[b], b, sortedBeliefs))
                {
                    continue;
                }

                if (DistanceBetween(a, b, positions) is { } distance)
                {
                    distances.Add(distance);
                }
            }
        }

        return distances.Count == 0 ? (null, null) : (distances.Max(), distances.Average());
    }

    /// <summary>Affinité = 1 + bonus par but partagé + bonus par croyance partagée.</summary>
    private double Affinity(
        MindState self,
        MindState other,
        ulong selfId,
        Dictionary<ulong, Belief[]> sortedBeliefs)
    {
        double affinity = 1.0;

        if (self.Intention is { } selfIntention &&
            other.Intention is { } otherIntention &&
            selfIntention.Kind != DesireKind.Idle &&
            selfIntention.Kind == otherIntention.Kind)
        {
            affinity += _settings.GoalAlignmentBonus;
        }

        affinity += SharedBeliefCount(selfId, other, sortedBeliefs) * _settings.SharedBeliefBonus;
        return affinity;
    }

    private static int SharedBeliefCount(
        ulong selfId,
        MindState other,
        Dictionary<ulong, Belief[]> sortedBeliefs)
    {
        if (!sortedBeliefs.TryGetValue(selfId, out Belief[]? mine))
        {
            return 0;
        }

        int shared = 0;
        foreach (Belief belief in mine)
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

    private double MeanCohesionOf(
        IReadOnlyList<ulong> members,
        IReadOnlyDictionary<ulong, MindState> minds,
        Dictionary<ulong, Belief[]> sortedBeliefs)
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
                sum += CohesionOf(minds[members[i]], members[i], minds[members[j]], members[j], sortedBeliefs);
                count++;
            }
        }

        return count == 0 ? 0.0 : sum / count;
    }

    private double CohesionOf(
        MindState self,
        ulong selfId,
        MindState other,
        ulong otherId,
        Dictionary<ulong, Belief[]> sortedBeliefs)
    {
        double minTrust = Math.Min(self.Trust.TrustWith(otherId), other.Trust.TrustWith(selfId));
        return minTrust * Affinity(self, other, selfId, sortedBeliefs);
    }

    private int FindMemberSet(
        IReadOnlyList<(List<ulong> Members, double MeanCohesion, double? MaxBondDistance, double? MeanBondDistance)> current,
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
    ///
    /// L'absence de leader est modélisée par <c>null</c> et non par la sentinelle
    /// <c>0</c> : avec une sentinelle, l'entité d'identifiant 0 ne pouvait jamais
    /// être élue (le groupe resterait sans décision ni propagation).
    /// </summary>
    private ulong? EmergentLeader(IReadOnlyList<ulong> members, IReadOnlyDictionary<ulong, MindState> minds)
    {
        ulong? best = null;
        double bestScore = double.NegativeInfinity;
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

            if (best is null || score > bestScore ||
                (Math.Abs(score - bestScore) < 1e-12 && candidate < best.Value))
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Décision collective (SYNE-061) : intention dominante des membres travaux
    /// adoptée si sa part ≤…&gt; quorum <c>consensusThreshold</c>. Le vote est
    /// pondéré par la confiance du membre envers le leader émergent (la confiance
    /// « compte ») ; consensus nul en l'absence de leader.
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