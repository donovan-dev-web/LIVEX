using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Population;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Unicité des identifiants de naissance (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// <c>BirthSystem.Step</c> recalculait le prochain identifiant en parcourant toute
/// la population à chaque naissance (<c>max(entityId) + 1</c>). Comme ce maximum
/// était figé <b>avant</b> la boucle des couples, plusieurs nouveau-nés d'un même
/// tick (<c>MaxBirthsPerTick &gt;= 2</c>) recevaient le même <c>childId</c> :
/// <c>World.AddEntity</c> levait sur le doublon et <c>NewbornMinds</c> — indexé
/// par <c>childId</c> — perdait une entrée. Le maximum est désormais suivi
/// incrémentalement.
/// </para>
/// </summary>
public class BirthSystemChildIdTests
{
    private static SimulationOptions Options()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Reproduction.Enabled = true;
        options.Reproduction.IntervalTicks = 1;
        options.Reproduction.MaxBirthsPerTick = 8;
        return options;
    }

    private static void RaiseMutualTrust(MindState self, ulong peer, int interactions)
    {
        for (int i = 0; i < interactions; i++)
        {
            self.Trust.Interact(peer);
        }
    }

    /// <summary>
    /// Construit un groupe de 6 agents consentingants, tous proches les uns des
    /// autres (fidélité de fusion : distance ≤ <c>mergeRange</c>) afin que
    /// plusieurs couples qualifient dans le même tick.
    /// </summary>
    private static (WorldType World, Dictionary<ulong, MindState> Minds) BuildConsentingGroup(SimulationOptions options, int count)
    {
        var world = new WorldType(WorldSize.From(options));
        var minds = new Dictionary<ulong, MindState>();

        for (ulong i = 1; i <= (ulong)count; i++)
        {
            Entity entity = new Entity(
                new EntityId(i), $"E{i}", name: null, new Position(100.0 + (i * 20.0), 100.0), TraitSet.NeutralAll, bornAt: 0);
            world.AddEntity(entity);
            minds[i] = new MindState(options);
        }

        // Confiance réciproque : 3 interactions → 0.60 (seuil de consentement 0.60).
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys)
            {
                if (peer != self)
                {
                    RaiseMutualTrust(mind, peer, 3);
                }
            }
        }

        return (world, minds);
    }

    [Fact]
    public void MultipleBirths_InOneTick_GetDistinctChildIds()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildConsentingGroup(options, count: 6);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 1, world, minds, options);

        IReadOnlyList<BirthObservation> births = system.LastBirths;

        // Prérequis du test : plusieurs naissances ont réellement eu lieu.
        Assert.True(births.Count > 1, $"Attendu > 1 naissance, obtenu {births.Count}.");

        ulong[] childIds = births.Select(b => b.ChildId).ToArray();
        Assert.Equal(childIds.Length, childIds.Distinct().Count());
    }

    [Fact]
    public void MultipleBirths_AreAllRegisteredInTheWorld_WithoutIdCollision()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildConsentingGroup(options, count: 6);
        int populationBefore = world.Entities.Count();

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 1, world, minds, options);

        IReadOnlyList<BirthObservation> births = system.LastBirths;

        Assert.Equal(populationBefore + births.Count, world.Entities.Count());

        // Chaque nouveau-né a un esprit : pas de perte dans NewbornMinds.
        Assert.Equal(births.Count, system.NewbornMinds.Count);
        foreach (BirthObservation birth in births)
        {
            Assert.Contains(birth.ChildId, system.NewbornMinds.Keys);
            Assert.Contains(world.Entities, e => e.Id.Value == birth.ChildId);
        }
    }

    [Fact]
    public void ChildIds_StartAboveTheHighestExistingId()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildConsentingGroup(options, count: 4);
        ulong maxBefore = world.Entities.Max(e => e.Id.Value);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 1, world, minds, options);

        foreach (BirthObservation birth in system.LastBirths)
        {
            Assert.True(birth.ChildId > maxBefore, $"childId {birth.ChildId} <= max {maxBefore}.");
        }
    }

    [Fact]
    public void ConsecutiveBirthTicks_NeverReuseAChildId()
    {
        // Un même système réutilisé sur plusieurs intervalles ne doit jamais
        // réattribuer un identifiant déjà pris par un nouveau-né antérieur.
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildConsentingGroup(options, count: 6);

        var system = new BirthSystem(options.Reproduction);
        var seen = new HashSet<ulong>(minds.Keys);

        for (ulong tick = 1; tick <= 5; tick++)
        {
            system.Step(tick, world, minds, options);
            foreach (BirthObservation birth in system.LastBirths)
            {
                Assert.True(seen.Add(birth.ChildId), $"childId {birth.ChildId} réattribué au tick {tick}.");
            }
        }
    }
}
