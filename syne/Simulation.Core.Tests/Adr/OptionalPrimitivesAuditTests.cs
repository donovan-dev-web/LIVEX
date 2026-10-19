// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Audit BL-19 des primitives optionnelles (plans, inventory, commitments,
//        salience, seasons, territories, books). Garde : aucune d'elles n'est
//        active par défaut (ni dans les défauts intégrés, ni dans le profil de
//        référence), et les primitives d'observation seule (territoires, livres)
//        ne peuvent pas modifier une décision d'agent. L'audit statique de chaque
//        primitive (lecture de son état propre ou d'une abstraction globale
//        déclarée, jamais d'un état privé d'autrui) est consigné dans
//        docs/realignment-evidence/BL19-OPTIONAL-PRIMITIVES-AUDIT.md.

using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Garde de l'audit BL-19 : les sept primitives optionnelles restent
/// <b>désactivées par défaut</b> et les deux primitives purement observatrices
/// (territoires, livres) ne modifient aucune décision. Elles ne peuvent donc pas
/// réintroduire, sans décision explicite, une information globale dans la
/// politique agentique.
/// </summary>
public class OptionalPrimitivesAuditTests
{
    private static SimulationLoop BuildScenario(ulong seed, SimulationOptions? options = null)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= 50; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options ?? ConfigLoader.LoadDefaults());
    }

    /// <summary>Journal de décisions comparable : position, énergie, intention et inventaire par entité.</summary>
    private static string DecisionLog(SimulationLoop loop, int ticks)
    {
        var log = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();
            foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                log.Append(CultureInfo.InvariantCulture,
                    $"{tick}:{entity.Id.Value}:{entity.Position.X:0.00},{entity.Position.Y:0.00};E={mind.Needs.Energy:0.00};I={mind.Intention?.Kind.ToString() ?? "None"};\n");
            }
        }

        return log.ToString();
    }

    private static void AssertAllOptionalPrimitivesOff(SimulationOptions options)
    {
        Assert.False(options.Agents.Actions.Plans.Enabled, "plans doit être désactivé par défaut");
        Assert.False(options.Agents.Actions.Inventory.Enabled, "inventory doit être désactivé par défaut");
        Assert.False(options.Agents.Actions.Salience.Enabled, "salience doit être désactivé par défaut");
        Assert.False(options.Agents.Actions.Commitments.Enabled, "commitments doit être désactivé par défaut");
        Assert.False(options.World.Seasons.Enabled, "seasons doit être désactivé par défaut");
        Assert.False(options.World.Territories.Enabled, "territories doit être désactivé par défaut");
        Assert.False(options.World.Books.Enabled, "books doit être désactivé par défaut");
    }

    [Fact]
    public void Defaults_DisableAllOptionalPrimitives()
    {
        // Défauts intégrés : c'est la base de tout run sans surcouche.
        AssertAllOptionalPrimitivesOff(ConfigLoader.LoadDefaults());

        // Profil de référence rejoué explicitement (chemin HTTP/Launcher) : il ne
        // doit pas activer silencieusement une primitive optionnelle.
        AssertAllOptionalPrimitivesOff(SimulationProfiles.Reference());
    }

    [Fact]
    public void ObservationOnlyPrimitives_ChangeNoAgentDecision()
    {
        // Territoires (SYNE-073) et livres (SYNE-121) sont des primitives
        // d'observation/API : activées, elles n'ajoutent aucun candidat, aucun
        // score, aucune cible. Le journal de décisions doit rester identique.
        SimulationOptions on = ConfigLoader.LoadDefaults();
        on.World.Territories.Enabled = true;
        on.World.Territories.Zones.Add(new TerritoryZoneDefinition
        {
            Id = "zone-audit",
            CenterX = 250,
            CenterY = 250,
            Radius = 60,
        });
        on.World.Books.Enabled = true;

        string withObservation = DecisionLog(BuildScenario(seed: 7, on), ticks: 150);
        string baseline = DecisionLog(BuildScenario(seed: 7), ticks: 150);

        Assert.Equal(baseline, withObservation);
    }

    [Fact]
    public void EnabledActionPrimitives_RemainDeterministic()
    {
        // L'activation des primitives de décision (plans, inventory, salience,
        // commitments) change la trajectoire (elles ajoutent candidats/états),
        // mais reste strictement déterministe : même graine + mêmes drapeaux ⇒
        // même journal. Aucun état global non déterministe n'est injecté.
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        options.Agents.Actions.Plans.Enabled = true;
        options.Agents.Actions.Salience.Enabled = true;
        options.Agents.Actions.Commitments.Enabled = true;

        string first = DecisionLog(BuildScenario(seed: 42, options), ticks: 120);
        string second = DecisionLog(BuildScenario(seed: 42, options), ticks: 120);

        Assert.Equal(first, second);
    }
}
