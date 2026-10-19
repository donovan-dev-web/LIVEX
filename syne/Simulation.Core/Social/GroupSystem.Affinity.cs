// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : GroupSystem - partie Affinity.

namespace Simulation.Core.Social;

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

public sealed partial class GroupSystem
{

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
}
