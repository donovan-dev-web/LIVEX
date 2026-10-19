// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : GroupSystem - partie Emergent.

namespace Simulation.Core.Social;

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

public sealed partial class GroupSystem
{

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
