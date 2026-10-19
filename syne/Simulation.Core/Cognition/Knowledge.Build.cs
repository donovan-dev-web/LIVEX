// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// RÃ´le : Knowledge - partie Build.

using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

public static partial class KnowledgeBuilder
{

    public static CognitionKnowledge Build(
        MindState mind,
        Position self,
        ulong currentTick,
        BeliefSettings settings,
        double interactionRadius)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(settings);

        GoalTarget? food = null;
        double foodDistance = double.MaxValue;
        GoalTarget? water = null;
        double waterDistance = double.MaxValue;
        GoalTarget? landmark = null;
        double landmarkDistance = -1.0;
        GoalTarget? peer = null;
        double peerDistance = double.MaxValue;
        bool atFood = false;
        bool atWater = false;

        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (belief.IsExpired(currentTick) || belief.Confidence < MinConfidence)
            {
                continue;
            }

            Fact fact = belief.Fact;
            if (string.Equals(fact.Predicate, NodeStatePredicate, StringComparison.Ordinal) &&
                fact.Subject.StartsWith("node-", StringComparison.Ordinal))
            {
                if (!TryParseNodeState(fact.Value, out Position position, out double amount))
                {
                    continue;
                }

                string label = fact.Subject["node-".Length..];
                double distance = self.DistanceTo(position);
                bool atHand = distance <= interactionRadius && amount > 0.0;

                // Un gisement dont on croit la quantitÃ© Ã©puisÃ©e n'est pas une
                // destination : la cible est invalide et l'objectif doit Ãªtre rÃ©visÃ©.
                if (amount <= 0.0)
                {
                    continue;
                }

                var target = new GoalTarget("node", Fnv1a64.HashUtf8(label), label, position, belief.UpdatedTick, belief.Confidence);
                ResourceKind? kind = KindFromLabel(label);
                if (kind == ResourceKind.Food && distance < foodDistance)
                {
                    food = target;
                    foodDistance = distance;
                    atFood = atHand;
                }
                else if (kind == ResourceKind.Water && distance < waterDistance)
                {
                    water = target;
                    waterDistance = distance;
                    atWater = atHand;
                }

                if (distance > landmarkDistance)
                {
                    landmark = target;
                    landmarkDistance = distance;
                }
            }
            else if (string.Equals(fact.Predicate, EntityPositionPredicate, StringComparison.Ordinal) &&
                     fact.Subject.StartsWith("entity-", StringComparison.Ordinal))
            {
                if (!TryParsePosition(fact.Value, out Position position))
                {
                    continue;
                }

                double distance = self.DistanceTo(position);
                if (distance < peerDistance)
                {
                    peer = new GoalTarget(
                        "entity",
                        ParseEntityId(fact.Subject),
                        fact.Subject,
                        position,
                        belief.UpdatedTick,
                        belief.Confidence);
                    peerDistance = distance;
                }
            }
        }

        if (food is null || water is null || landmark is null)
        {
            FillFromMemory(mind, self, currentTick, interactionRadius, ref food, ref water, ref landmark, ref atFood, ref atWater);
        }

        return new CognitionKnowledge(food, water, peer, landmark, atFood, atWater);
    }


    /// <summary>
    /// Repli mÃ©moire : les observations de gisements encore assez saillantes
    /// (<c>Memory.Recall</c>) fournissent des cibles que la croyance n'a pas
    /// conservÃ©es (expiration, Ã©viction de capacitÃ©). La plus rÃ©cente par
    /// identifiant gagne â€” <c>Recall</c> est triÃ© par (StoredAt, Sequence)
    /// croissants, la derniÃ¨re vue Ã©crase donc les prÃ©cÃ©dentes.
    /// </summary>
    private static void FillFromMemory(
        MindState mind,
        Position self,
        ulong currentTick,
        double interactionRadius,
        ref GoalTarget? food,
        ref GoalTarget? water,
        ref GoalTarget? landmark,
        ref bool atFood,
        ref bool atWater)
    {
        if (food is not null && water is not null && landmark is not null)
        {
            return;
        }

        var remembered = new Dictionary<string, MemoryRecall>(StringComparer.Ordinal);
        foreach (MemoryRecall recall in mind.Memory.Recall(MemoryCategory.Observation, currentTick))
        {
            if (!TryParseNodeMemory(recall.Entry.Content, out string label, out Position _, out double amount, out double _) || amount <= 0.0)
            {
                continue;
            }

            remembered[label] = recall;
        }

        foreach ((string label, MemoryRecall recall) in remembered.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            MemoryEntry entry = recall.Entry;
            if (!TryParseNodeMemory(entry.Content, out string _, out Position position, out double _, out double confidence))
            {
                continue;
            }

            double distance = self.DistanceTo(position);
            var target = new GoalTarget(
                "node",
                Fnv1a64.HashUtf8(label),
                label,
                position,
                entry.StoredAt,
                // La confiance d'un souvenir rappelÃ© est bornÃ©e par sa salience :
                // une mÃ©moire qui s'efface ne peut pas produire une croyance ferme.
                Math.Min(confidence, recall.Salience));

            ResourceKind? kind = KindFromLabel(label);
            if (kind == ResourceKind.Food && food is null)
            {
                food = target;
                atFood = distance <= interactionRadius;
            }
            else if (kind == ResourceKind.Water && water is null)
            {
                water = target;
                atWater = distance <= interactionRadius;
            }
            else if (landmark is null)
            {
                landmark = target;
            }
        }
    }
}
