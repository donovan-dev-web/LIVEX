// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Mesure des boucles de besoin (Phase 2 du plan de réalignement — BL-17 /
//        S-13 / D-14 / B-08 / B-12) :
//        (1) signe de la dérive Safety → vers l'insécurité ;
//        (2) exutoires Socialize / Explore à l'arrivée ;
//        (3) franchissement mesuré du seuil de fuite menace + dérive, et
//            rétablissement par Flee ;
//        (4) un cycle homéostatique réel sur les taux nominaux (le besoin se
//            déclenche, se satisfait, redescend — il ne sature jamais à 1).
//        Ces mesures ferment le reste ouvert de la phase 2 (« la boucle n'est
//        pas mesurée ») ; la contre-preuve du défaut historique est la sonde
//        AC4-5 : docs/realignment-evidence/probes/Phase1.Probes/Probe5_NeedsWithoutOutlet.cs
//        (échec 3/3 capturé sur 707f86c dans
//        docs/realignment-evidence/probes/evidence/probe5-results-707f86c.txt).

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

/// <summary>
/// Boucles de besoin : dérive, déclenchement, exutoire à l'arrivée,
/// rétablissement — mesurées sur le moteur aligné (engineVersion 0.16.0).
/// </summary>
public class NeedsLoopTests
{
    private static readonly SimulationOptions Options = ConfigLoader.LoadDefaults();

    private static (WorldType world, Entity entity, MindState mind, ActionExecutor executor) NewAgent()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 250), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        var mind = new MindState(Options);
        var executor = new ActionExecutor(
            world,
            new ActionCatalog(Options.Agents.Actions),
            new ResourceStocks(Options.Resources),
            Options);
        return (world, entity, mind, executor);
    }

    [Fact]
    public void Advance_DriftsSafetyTowardInsecurity_NotTowardSafety()
    {
        var needs = BodyNeeds.FromState(safety: 1.0, social: 0.0, curiosity: 0.0);
        double safetyBefore = needs.Safety;

        needs.Advance(Options.Agents.Needs);

        // Signe corrigé (S-13) : Safety DIMINUE du taux configuré, elle n'augmente
        // plus et ne sature plus à 1 — condition pour que Flee devienne atteignable.
        Assert.True(needs.Safety < safetyBefore,
            $"La dérive doit aller vers l'insécurité : Safety {safetyBefore} → {needs.Safety}");
        Assert.Equal(Options.Agents.Needs.SafetyDriftRate, safetyBefore - needs.Safety, 10);
        Assert.True(needs.Social > 0.0, "La dérive sociale doit augmenter Social.");
        Assert.True(needs.Curiosity > 0.0, "La dérive de curiosité doit augmenter Curiosity.");
    }

    [Fact]
    public void Socialize_Executed_SatisfiesTheSocialNeed()
    {
        var (_, entity, mind, executor) = NewAgent();

        // Porter Social au-dessus de son seuil (0,7) par dérive isolée.
        var drift = new NeedsSettings { SocialDriftRate = 0.1 };
        for (int i = 0; i < 8; i++)
        {
            mind.Needs.Advance(drift);
        }

        Assert.True(mind.Needs.IsTriggered(DesireKind.Socialize));
        double before = mind.Needs.Social;

        ActionResult result = executor.Execute(entity, mind, DesireKind.Socialize, currentTick: 1);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        // Exutoire (socialRecovery = 0,35) appliqué à l'arrivée / sans cible :
        // le besoin est réduit et repasse SOUS le seuil → la boucle peut se fermer.
        Assert.Equal(before - 0.35, mind.Needs.Social, 10);
        Assert.False(mind.Needs.IsTriggered(DesireKind.Socialize),
            "Après exutoire, Social doit repasser sous son seuil de déclenchement.");
    }

    [Fact]
    public void Explore_Executed_SatisfiesTheCuriosityNeed()
    {
        var (_, entity, mind, executor) = NewAgent();

        var drift = new NeedsSettings { CuriosityDriftRate = 0.1 };
        for (int i = 0; i < 4; i++)
        {
            mind.Needs.Advance(drift);
        }

        Assert.True(mind.Needs.IsTriggered(DesireKind.Explore));
        double before = mind.Needs.Curiosity;

        ActionResult result = executor.Execute(entity, mind, DesireKind.Explore, currentTick: 1);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        // curiosityRecovery = 0,50 ≥ 0,40 → besoin entièrement apaisé.
        Assert.True(mind.Needs.Curiosity < before, "Explore doit réduire Curiosity à l'arrivée.");
        Assert.False(mind.Needs.IsTriggered(DesireKind.Explore));
    }

    [Fact]
    public void SafetyDrift_FromFullSafety_ReachesTheFleeThreshold_WithinBoundedTicks()
    {
        var needs = BodyNeeds.FromState(safety: 1.0);

        const int maxTicks = 3000;
        int ticks = 0;
        while (!needs.IsTriggered(DesireKind.Flee) && ticks < maxTicks)
        {
            needs.Advance(Options.Agents.Needs);
            ticks++;
        }

        // Mesure : à 0,0002/tick, le seuil 0,5 est franchi vers le tick ~2500
        // (fin d'horizon de calibration), pas en saturation ni jamais.
        // Mesuré le 17/10 : franchi au tick 2501 (Safety = 0,4998).
        Assert.True(needs.IsTriggered(DesireKind.Flee),
            $"Seuil de fuite jamais atteint en {maxTicks} ticks (Safety = {needs.Safety}).");
        Assert.InRange(ticks, 2000, maxTicks);
    }

    [Fact]
    public void SafetyThreats_CumulateWithDrift_CrossTheFleeThreshold_AndFleeRecovers()
    {
        var (_, entity, mind, executor) = NewAgent();
        var needs = mind.Needs;

        // Une menace abaisse la sûreté…
        needs.ThreatenSafety(0.3);
        Assert.Equal(0.7, needs.Safety, 10);

        // …et la dérive ne la remonte JAMAIS : elle continue vers l'insécurité
        // (c'était l'inverse sur la base 707f86c : Safety repartait vers 1).
        double previous = needs.Safety;
        for (int i = 0; i < 100; i++)
        {
            needs.Advance(Options.Agents.Needs);
            Assert.True(needs.Safety <= previous, "La dérive ne doit pas rétablir la sûreté.");
            previous = needs.Safety;
        }

        // Menaces cumulées → franchissement du seuil de fuite.
        needs.ThreatenSafety(0.2);
        Assert.True(needs.IsTriggered(DesireKind.Flee),
            $"Flee doit être atteignable après menaces cumulées (Safety = {needs.Safety}).");

        // L'exutoire Flee (safetyRecovery = 0,30) referme la boucle.
        ActionResult result = executor.Execute(entity, mind, DesireKind.Flee, currentTick: 100);
        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.False(needs.IsTriggered(DesireKind.Flee),
            $"Flee doit rétablir la sûreté au-dessus du seuil (Safety = {needs.Safety}).");
    }

    [Fact]
    public void SocialLoop_OnNominalDriftRates_CyclesInsteadOfSaturating()
    {
        // Mesure du cycle homéostatique aux taux nominaux (socialRecovery = 0,35,
        // drift 0,003/tick) : le besoin se déclenche, l'action Socialize le
        // satisfait à l'arrivée, il repasse sous le seuil, puis redéclenche.
        // Sur la base 707f86c, AUCUN exutoire n'existait : le besoin saturait
        // au voisinage de 1 et Socialize pilotait jusqu'à 96 % des décisions.
        var (_, entity, mind, executor) = NewAgent();
        var needs = mind.Needs;

        const int horizon = 2000;
        int firstTriggerTick = 0;
        int triggerCount = 0;
        double maxSocial = 0.0;
        bool droppedBelowThresholdAfterFirstTrigger = false;

        for (int tick = 1; tick <= horizon; tick++)
        {
            needs.Advance(Options.Agents.Needs);
            maxSocial = Math.Max(maxSocial, needs.Social);

            if (needs.IsTriggered(DesireKind.Socialize))
            {
                if (triggerCount == 0)
                {
                    firstTriggerTick = tick;
                }

                triggerCount++;
                executor.Execute(entity, mind, DesireKind.Socialize, currentTick: (ulong)tick);

                if (triggerCount == 1)
                {
                    Assert.False(needs.IsTriggered(DesireKind.Socialize),
                        "L'exutoire doit ramener Social sous son seuil immédiatement après l'action.");
                    droppedBelowThresholdAfterFirstTrigger = true;
                }
            }
        }

        // Mesuré le 17/10 aux taux nominaux : 1er déclenchement au tick 234,
        // 16 déclenchements sur 2000 ticks, Social plafonne à 0,702 (jamais à 1).
        Assert.True(firstTriggerTick > 0, "Le besoin social n'a jamais été déclenché sur l'horizon mesuré.");
        Assert.True(droppedBelowThresholdAfterFirstTrigger);
        Assert.True(triggerCount >= 3,
            $"Le cycle doit se répéter : {triggerCount} déclenchements sur {horizon} ticks.");
        Assert.True(maxSocial < 0.99,
            $"Social ne doit jamais saturer (mesuré : max = {maxSocial}).");
    }
}
