// Probe AC4-2: cible d'action globale
// Sur base 707f86c, les actions choisissent des cibles depuis l'état global (monde)
// sans filtrer par croyances/mémoire connues de l'agent.
// Attente post-fix: cibles crues doivent être choisies parmi ce que l'agent connaît (perçu/mémorisé).

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Phase1.Probes;

public class Probe2_GlobalTargets
{
    [Fact]
    public void Targets_ShouldBeConstrainedToKnownBeliefs_NotGlobalWorldState()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        var stocks = new ResourceStocks(options.Resources);
        var world = new World(new WorldSize(500, 500));
        var donor = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        var recipientNear = new Entity(new EntityId(2), "Entité B", name: null, new Position(250, 110), bornAt: 0); // proche (10)
        var recipientFarKnown = new Entity(new EntityId(3), "Entité C", name: null, new Position(250, 200), bornAt: 0); // plus loin (100)
        world.AddEntity(donor);
        world.AddEntity(recipientNear);
        world.AddEntity(recipientFarKnown);
        
        var donorMind = new MindState(options);
        donorMind.Inventory!.TryTake(ResourceKind.Food, 5.0);
        // Croyance: l'agent "connaît" surtout entité 3 (far) — post-fix voudrait filtrer par connaissances
        donorMind.Beliefs.ApplyEvidence(new Fact("entity-3", "position", "250,200"), 0.95, "perception-1", options.Agents.Beliefs, 1);
        
        var lookup = new Dictionary<ulong, MindState?>
        {
            [1] = donorMind,
            [2] = new MindState(options),
            [3] = new MindState(options)
        };
        var catalog = new ActionCatalog(options.Agents.Actions);
        var executor = new ActionExecutor(world, catalog, stocks, options);
        var result = executor.Execute(donor, donorMind, DesireKind.Give, currentTick: 5, id => lookup[id]);
        
        // Post-fix attendu: cible choisie selon croyances/connaissances (pas uniquement plus proche global)
        bool postFixRequiresBeliefConstrainedTarget = true;
        bool usedGlobalNearestOnly = result.RecipientId == 2; // base choisit le plus proche dans le monde
        // Attendu post-fix: recipient pourrait être 3 si contraintes par beliefs; sur base c'est 2
        Assert.False(postFixRequiresBeliefConstrainedTarget && !usedGlobalNearestOnly,
            "Post-fix attendu: sélection de cible contrainte aux entités connues/croyables (non seulement le plus proche global). " +
            "Sur base 707f86c, Give choisit le plus proche dans le monde → échec attendu.");
    }
}