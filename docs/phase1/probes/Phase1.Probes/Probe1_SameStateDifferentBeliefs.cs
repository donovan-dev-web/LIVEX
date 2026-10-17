// Probe AC4-1: mêmes candidats/choix malgré croyances différentes
// Sur base 707f86c, BeliefSet est stocké mais la génération/évaluation de candidats
// ne consulte pas les croyances pour produire des différences traçables.
// Attente post-fix (ADR-018): à état physique identique, connaissances/croyances différentes
// doivent pouvoir produire des candidats/choix différents. Ce test échoue sur base car
// cette attente n'est pas satisfaite.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Phase1.Probes;

public class Probe1_SameStateDifferentBeliefs
{
    [Fact]
    public void SamePhysicalState_DifferentBeliefs_ShouldProduceDifferentDecisions_WhenRelevant()
    {
        var options = ConfigLoader.LoadDefaults();
        var stocks = new ResourceStocks(options.Resources);
        var world = new World(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 250), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        
        // Esprit A: sans croyance spécifique sur une ressource
        var mindA = new MindState(options);
        // Esprit B: avec croyance sur une ressource existante
        var mindB = new MindState(options);
        mindB.Beliefs.ApplyEvidence(
            new Fact("node-resource-water-1", "state", "present"),
            confidence: 0.9,
            source: "perception-1",
            options.Agents.Beliefs,
            currentTick: 1);
        
        // Génération des objectifs via DesireFactory (ne consulte pas Beliefs)
        var goalsA = DesireFactory.Generate(mindA.Needs, tick: 5, activeKinds: Array.Empty<DesireKind>(), 
            new Simulation.Core.Actions.ActionCatalog(options.Agents.Actions), stocks);
        var goalsB = DesireFactory.Generate(mindB.Needs, tick: 5, activeKinds: Array.Empty<DesireKind>(),
            new Simulation.Core.Actions.ActionCatalog(options.Agents.Actions), stocks);
        
        // Post-fix attendu: des croyances pertinentes DEVRAIENT affecter la génération/évaluation
        // Sur base 707f86c, ce n'est pas le cas → les ensembles sont identiques
        bool beliefsRelevant = true;
        bool choicesDiffer = !goalsA.SequenceEqual(goalsB, new GoalComparer());
        
        // Cette assertion échoue sur base : beliefsRelevant est vrai (attendu post-fix)
        // mais choicesDiffer est faux car beliefs ne sont pas consultées
        Assert.True(beliefsRelevant && choicesDiffer,
            "Post-fix attendu: croyances différentes pertinentes doivent produire différences traçables. " +
            "Sur base 707f86c, Beliefs n'est pas consulté dans la génération → échec attendu.");
    }
    
    private class GoalComparer : IEqualityComparer<Goal>
    {
        public bool Equals(Goal x, Goal y) => x.Kind == y.Kind && x.BornTick == y.BornTick;
        public int GetHashCode(Goal obj) => HashCode.Combine(obj.Kind, obj.BornTick);
    }
}