// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gate Phase 4 (AC8) — trace causale : les faits justificatifs d'une décision
//        (DecisionRecord.Evidence) remontent la chaîne action ← but ← croyance ← mémoire ← perception,
//        que la cible provienne d'une croyance vive ou d'un repli mémoire.

using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Observability;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Adr;

/// <summary>
/// Gate Phase 4 (AC8, D-06) : la trace d'une décision porte ses <b>preuves</b> —
/// provenance, sujet, confiance et âge de l'information qui a fondé le choix.
/// Deux voies sont couvertes : la cible fondée sur une <b>croyance</b> issue de la
/// perception, et le <b>repli mémoire</b> quand aucune croyance vive ne subsiste.
/// </summary>
public class AdrDecisionEvidenceTests
{
    private static readonly Position Self = new(250.0, 250.0);

    private static SimulationOptions Options()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Resources.Localized = true;
        return options;
    }

    /// <summary>Agent assoiffé (besoin porteur d'une quête ciblée).</summary>
    private static MindState ThirstyAgent(SimulationOptions options)
    {
        var mind = new MindState(options);
        mind.Needs.RestoreState(hunger: 60.0, thirst: 80.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 60.0);
        return mind;
    }

    /// <summary>Croyance de gisement (provenance perception, confiance forte, datée).</summary>
    private static void BelieveNode(MindState mind, SimulationOptions options, string label, double x, double y, double amount, ulong tick)
    {
        mind.Beliefs.ApplyObservation(
            new Fact(
                $"node-{label}",
                KnowledgeBuilder.NodeStatePredicate,
                string.Create(CultureInfo.InvariantCulture, $"{x:0.00},{y:0.00},{amount:0.###}")),
            1.0,
            "perception-1",
            options.Agents.Beliefs,
            tick);
    }

    /// <summary>Souvenir de gisement en mémoire (aucune croyance correspondante).</summary>
    private static void RememberNode(MindState mind, string label, double x, double y, double amount, ulong tick)
    {
        mind.Memory.Store(
            MemoryCategory.Observation,
            "perception-1",
            string.Create(CultureInfo.InvariantCulture, $"node|{label}|{x:0.00}|{y:0.00}|{amount:0.###}|1.000"),
            1.0,
            tick);
    }

    /// <summary>Enregistre une décision du tick courant portant la cible et ses preuves (appel du pipeline).</summary>
    private static DecisionRecord Decide(MindState mind, DesireKind kind, GoalTarget target, IReadOnlyList<DecisionEvidence> evidence, ulong tick)
    {
        var chosen = new UtilityScore(kind, Benefit: 1.0, Cost: 0.0, Risk: 0.0, Confidence: 1.0, PersonalityModifier: 1.0, Urgency: 1.0, Utility: 0.5);
        IReadOnlyList<UtilityScore> scores = [chosen];
        mind.RecordDecision(scores, chosen, deliberated: true, interrupted: false, tick, entityId: 1, target, evidence);
        return mind.LastDecisionRecord!;
    }

    [Fact]
    public void Decision_BuiltOnABelief_CarriesPerceptionEvidence()
    {
        // AC8 : la cible provient d'une croyance de perception — la preuve en reporte
        // la provenance, le sujet complet, la confiance et le tick d'observation, ce
        // qui remonte la chaîne action ← but ← croyance ← perception.
        SimulationOptions options = Options();
        MindState mind = ThirstyAgent(options);
        BelieveNode(mind, options, "resource-water-1", x: 320.0, y: 250.0, amount: 100.0, tick: 9);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, currentTick: 10, options.Agents.Beliefs, options.Resources.InteractionRadius);
        GoalTarget target = knowledge.WaterSource!.Value;
        IReadOnlyList<DecisionEvidence> evidence = CognitionPipeline.EvidenceFor(mind, target);

        DecisionRecord record = Decide(mind, DesireKind.SeekWater, target, evidence, tick: 10);

        // La chaîne est complète : action ← but ← croyance.
        Assert.Equal(DesireKind.SeekWater, record.Chosen);
        Assert.Equal("resource-water-1", record.Target?.Label);
        Assert.Equal(9UL, record.Target?.ObservedTick);

        DecisionEvidence fact = Assert.Single(record.Evidence!);
        Assert.Equal("perception-1", fact.Source);
        Assert.Equal("node-resource-water-1.state", fact.Subject);
        Assert.Equal(1.0, fact.Confidence, 10);
        Assert.Equal(9UL, fact.ObservedTick);
    }

    [Fact]
    public void Decision_FallingBackToMemory_CarriesMemoryEvidence()
    {
        // AC8 : sans croyance vive correspondante, la cible provient de la mémoire
        // rappelée — la preuve le déclare explicitement (source « memory »), avec le
        // sujet mémorisé, la confiance bornée par la salience et le tick de stockage.
        SimulationOptions options = Options();
        MindState mind = ThirstyAgent(options);
        RememberNode(mind, "resource-water-1", x: 400.0, y: 250.0, amount: 80.0, tick: 5);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, currentTick: 6, options.Agents.Beliefs, options.Resources.InteractionRadius);
        GoalTarget target = knowledge.WaterSource!.Value;

        // Garantie de la voie mémoire : aucune croyance de gisement ne subsiste.
        Assert.False(mind.Beliefs.TryGet(
            new Fact("node-resource-water-1", KnowledgeBuilder.NodeStatePredicate, "400.00,250.00,80"),
            out _));

        IReadOnlyList<DecisionEvidence> evidence = CognitionPipeline.EvidenceFor(mind, target);
        DecisionRecord record = Decide(mind, DesireKind.SeekWater, target, evidence, tick: 6);

        Assert.Equal(DesireKind.SeekWater, record.Chosen);
        Assert.Equal("resource-water-1", record.Target?.Label);

        DecisionEvidence fact = Assert.Single(record.Evidence!);
        Assert.Equal("memory", fact.Source);
        Assert.Equal("node-resource-water-1", fact.Subject);
        Assert.True(fact.Confidence > 0.0 && fact.Confidence <= 1.0);
        Assert.Equal(5UL, fact.ObservedTick);
    }

    [Fact]
    public void DecisionMadeEvent_PublishesTheEvidenceChain()
    {
        // AC8 : l'événement `decision_made` publie lui aussi les preuves (et non
        // seulement cible/candidats rejetés/résultat) — le flux observable porte la
        // chaîne causale complète pour ECHOS.
        SimulationOptions options = Options();
        MindState mind = ThirstyAgent(options);
        BelieveNode(mind, options, "resource-water-1", x: 320.0, y: 250.0, amount: 100.0, tick: 9);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, currentTick: 10, options.Agents.Beliefs, options.Resources.InteractionRadius);
        GoalTarget target = knowledge.WaterSource!.Value;
        Decide(mind, DesireKind.SeekWater, target, CognitionPipeline.EvidenceFor(mind, target), tick: 10);

        ExternalEvent decision = EventSensor.DecisionMade(tick: 10, agentId: 1, mind);
        JsonObject eventJson = ObservabilitySerializer.EventMessage(decision);
        var value = (JsonObject)eventJson["value"]!;

        JsonArray evidence = Assert.IsType<JsonArray>(value["evidence"]);
        var fact = Assert.IsType<JsonObject>(Assert.Single(evidence));
        Assert.Equal("perception-1", (string?)fact["source"]);
        Assert.Equal("node-resource-water-1.state", (string?)fact["subject"]);
        Assert.Equal(1.0, (double?)fact["confidence"] ?? 0.0, 10);
        Assert.Equal(9UL, (ulong?)fact["observedTick"]);
    }
}
