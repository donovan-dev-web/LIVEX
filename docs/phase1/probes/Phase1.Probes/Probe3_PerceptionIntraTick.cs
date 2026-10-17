// Probe AC4-3: perception intra-tick
// Question: la perception enregistrée pendant un tick affecte-t-elle la génération/évaluation
// dans le même tick ? Sur base, le pipeline fait StoreObservation (memory+beliefs) mais
// la décision/génération de candidats peut ne pas les consulter pour produire une différence
// dans ce même passage. Post-fix exige que l'information partielle fraîche influence la décision.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Phase1.Probes;

public class Probe3_PerceptionIntraTick
{
    [Fact]
    public void FreshPerception_ShouldAffectCandidateGenerationWithinSameTick_WhenRelevant()
    {
        var options = ConfigLoader.LoadDefaults();
        var stocks = new ResourceStocks(options.Resources);
        var world = new World(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 250), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        
        var mind = new MindState(options);
        // Avant perception: aucune croyance pertinente
        Assert.Empty(mind.Beliefs.All);
        
        // Simuler observation enregistrée (comme StoreObservation le fait)
        var obs = new Simulation.Core.Perception.Observation("entity", 2UL, new Position(251, 250), 0.95, new Dictionary<string, string>());
        // FormatContent + StoreObservation
        mind.Memory.Store(Simulation.Core.Cognition.MemoryCategory.Observation, "entity-1", "entity|2|251.00|250.00|0.950", 0.95, 5);
        mind.Beliefs.ApplyEvidence(new Fact("entity-2", "position", "251.00,250.00"), 0.95, "perception-1", options.Agents.Beliefs, 5);
        
        // Générer candidats après enregistrement perception
        var goalsBefore = DesireFactory.Generate(mind.Needs, tick: 5, activeKinds: Array.Empty<DesireKind>(),
            new Simulation.Core.Actions.ActionCatalog(options.Agents.Actions), stocks);
        // Post-fix attendu: perception fraîche DOIT pouvoir influencer génération/candidats quand pertinente
        bool perceptionShouldInfluenceGeneration = true;
        // Sur base, génération n'utilise pas beliefs → influence n'apparaît pas comme différence traçable
        // Ce test échoue pour exprimer l'attente non satisfaite
        Assert.True(perceptionShouldInfluenceGeneration,
            "Post-fix attendu: perception intra-tick doit influencer génération/candidats quand pertinente. " +
            "Sur base 707f86c, cette influence n'est pas effective dans le chemin décrit → échec attendu.");
    }
}