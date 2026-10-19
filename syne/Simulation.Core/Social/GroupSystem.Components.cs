// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : GroupSystem - partie Components.

namespace Simulation.Core.Social;

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

public sealed partial class GroupSystem
{

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
}
