using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Population;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Tests de la naissance par fusion consentie (SYNE-062, SYSTEM_SPEC.md §8) :
/// consentement = confiance réciproque minimale, rareté (intervalle), plafond de
/// naissances, déterminisme (pas de PRNG) et câblage dans le pipeline.
/// </summary>
public class BirthSystemTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static MindState Mind(SimulationOptions options) => new(options);

    private static void RaiseMutualTrust(MindState self, ulong peer, int interactions)
    {
        for (int i = 0; i < interactions; i++)
        {
            self.Trust.Interact(peer);
        }
    }

    private static (WorldType World, Dictionary<ulong, MindState> Minds) BuildParents(SimulationOptions options)
    {
        var world = new WorldType(WorldSize.From(options));
        var minds = new Dictionary<ulong, MindState>();
        // Parents proches (dist ≈ 20 ≤ mergeRange 40, ligne de vue libre, rayons
        // pleins) pour respecter la fidélité de fusion (SYNE-075).
        for (ulong i = 1; i <= 2; i++)
        {
            Entity entity = MakeEntity(i, new Position(100.0 + (i * 20.0), 100.0));
            world.AddEntity(entity);
            minds[i] = Mind(options);
        }

        return (world, minds);
    }

    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    [Fact]
    public void Step_ProducesBirth_WhenMutualConsentReached()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildParents(options);
        // Premier contact 0.5 puis 2 renforcements de 0.05 → 0.60 (seuil).
        RaiseMutualTrust(minds[1], 2, interactions: 3);
        RaiseMutualTrust(minds[2], 1, interactions: 3);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        BirthObservation birth = Assert.Single(system.LastBirths);
        Assert.Equal(100UL, birth.Tick);
        Assert.Equal(1UL, birth.MotherId);
        Assert.Equal(2UL, birth.FatherId);
        Assert.Equal(3UL, birth.ChildId);
        Assert.Equal(3, world.Entities.Count);

        // L'esprit du nouveau-né est vierge d'interactions mais hérite (mémoire vide ici).
        Assert.True(system.NewbornMinds.ContainsKey(3UL));
        Assert.Equal(0, system.NewbornMinds[3].Trust.Count);
        Assert.Equal(0.0, system.NewbornMinds[3].Needs.Hunger);
    }

    [Fact]
    public void Step_NoBirth_UntilIntervalElapses()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildParents(options);
        RaiseMutualTrust(minds[1], 2, interactions: 3);
        RaiseMutualTrust(minds[2], 1, interactions: 3);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 50, world, minds, options);

        Assert.Empty(system.LastBirths);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void Step_NoBirth_BelowConsentThreshold()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildParents(options);
        // Confiance initiale 0.5 < 0.6 → pas de consentement.
        RaiseMutualTrust(minds[1], 2, interactions: 1);
        RaiseMutualTrust(minds[2], 1, interactions: 1);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
    }

    [Fact]
    public void Step_NoBirth_WhenDisabled()
    {
        SimulationOptions options = Options();
        options.Reproduction.Enabled = false;
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildParents(options);
        RaiseMutualTrust(minds[1], 2, interactions: 3);
        RaiseMutualTrust(minds[2], 1, interactions: 3);

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
    }

    [Fact]
    public void Step_MaxBirthsPerTick_CapsBirths()
    {
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;
        options.Reproduction.MaxBirthsPerTick = 1;

        var world = new WorldType(WorldSize.From(options));
        var minds = new Dictionary<ulong, MindState>();
        // Quatre entités proches (fidélité de fusion respectée : dist ≈ 10).
        for (ulong i = 1; i <= 4; i++)
        {
            Entity entity = MakeEntity(i, new Position(100.0 + (i * 10.0), 100.0));
            world.AddEntity(entity);
            minds[i] = Mind(options);
        }

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        // Seuil 0.0 + proximité : la première paire (1,2) qualifie, mais le plafond impose 1 naissance.
        BirthObservation birth = Assert.Single(system.LastBirths);
        Assert.Equal(1UL, birth.MotherId);
        Assert.Equal(2UL, birth.FatherId);
        Assert.Equal(5UL, birth.ChildId);
    }

    [Fact]
    public void Step_ChildTraits_AreFusedDeterministically()
    {
        SimulationOptions options = Options();
        (WorldType world, Dictionary<ulong, MindState> minds) = BuildParents(options);
        RaiseMutualTrust(minds[1], 2, interactions: 3);
        RaiseMutualTrust(minds[2], 1, interactions: 3);

        TraitSet first = DeterministicBirth(options, world, minds);

        (WorldType world2, Dictionary<ulong, MindState> minds2) = BuildParents(options);
        RaiseMutualTrust(minds2[1], 2, interactions: 3);
        RaiseMutualTrust(minds2[2], 1, interactions: 3);
        TraitSet second = DeterministicBirth(options, world2, minds2);

        Assert.Equal(first.Values.OrderBy(pair => pair.Key), second.Values.OrderBy(pair => pair.Key));
    }

    private static TraitSet DeterministicBirth(
        SimulationOptions options,
        WorldType world,
        Dictionary<ulong, MindState> minds)
    {
        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);
        Entity child = world.Entities.Single(entity => entity.Id.Value == 3);
        return child.Traits;
    }

    [Fact]
    public void Pipeline_BirthRegistersMind_ForNextTick()
    {
        // Intégration SYNE-062 : la naissance est effectuée après la boucle
        // d'itération (ordre causal strict) et l'esprit est enregistré dans le
        // pipeline dès le tick de naissance. La fidélité de fusion est maintenue :
        // entités proches dès le départ (elles dérivent peu en 100 ticks).
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;
        options.Reproduction.MergeRange = 500; // le monde entier : seuls les autres gates s'appliquent
        options.Reproduction.MergeMinimumEnergy = 0; // énergie couverte par les tests unitaires de fidélité
        options.Reproduction.RequireNoCriticalNeed = false; // idem (dérive d'énergie sur 100 ticks)
        options.Agents.Life.DeathEnabled = false; // le scénario teste la naissance, pas la mortalité

        var world = new WorldType(WorldSize.From(options));
        world.AddEntity(MakeEntity(1, new Position(100, 100)));
        world.AddEntity(MakeEntity(2, new Position(120, 100)));

        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(1), options);
        loop.Run(100);

        Assert.Equal(3, world.Entities.Count);
        Assert.True(loop.Cognition.HasMind(3));
        Assert.Equal(100UL, loop.Cognition.Birth.LastBirths.Single().Tick);
    }

    [Fact]
    public void Fidelity_NoBirth_WhenParentsTooFarApart()
    {
        // SYNE-075 : la fusion consentie exige la proximité physique (mergeRange).
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;
        options.Reproduction.MaxBirthsPerTick = 4;

        var world = new WorldType(WorldSize.From(options));
        world.AddEntity(MakeEntity(1, new Position(100, 100)));
        world.AddEntity(MakeEntity(2, new Position(500, 500))); // dist ≈ 565 > 40
        var minds = new Dictionary<ulong, MindState> { [1] = Mind(options), [2] = Mind(options) };

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void Fidelity_NoBirth_WhenObstacleBlocksLineOfSight()
    {
        // SYNE-075 : la ligne de vue entre les parents doit être dégagée.
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;

        var world = new WorldType(WorldSize.From(options));
        world.AddObstacle(new Obstacle("mure", new Position(110, 100), radius: 15));
        world.AddEntity(MakeEntity(1, new Position(100, 100)));
        world.AddEntity(MakeEntity(2, new Position(120, 100))); // segment traversé par le disque
        var minds = new Dictionary<ulong, MindState> { [1] = Mind(options), [2] = Mind(options) };

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void Fidelity_NoBirth_WhenParentEnergyExhausted()
    {
        // SYNE-075 : les deux parents doivent avoir une énergie suffisante.
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;
        options.Reproduction.MergeMinimumEnergy = 50.0;

        var world = new WorldType(WorldSize.From(options));
        world.AddEntity(MakeEntity(1, new Position(100, 100)));
        world.AddEntity(MakeEntity(2, new Position(120, 100)));
        MindState mother = Mind(options);
        mother.Needs.ExertEnergy(200); // énergie 0 < 50
        var minds = new Dictionary<ulong, MindState> { [1] = mother, [2] = Mind(options) };

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void Fidelity_NoBirth_WhenParentInCriticalState()
    {
        // SYNE-075 : la fusion exige l'absence de besoin critique chez les parents.
        SimulationOptions options = Options();
        options.Reproduction.ConsentTrustThreshold = 0.0;

        var world = new WorldType(WorldSize.From(options));
        world.AddEntity(MakeEntity(1, new Position(100, 100)));
        world.AddEntity(MakeEntity(2, new Position(120, 100)));
        MindState father = Mind(options);
        father.Needs.ExertEnergy(200); // énergie 0 < CriticalEnergy (10) → état critique
        var minds = new Dictionary<ulong, MindState> { [1] = Mind(options), [2] = father };

        var system = new BirthSystem(options.Reproduction);
        system.Step(tick: 100, world, minds, options);

        Assert.Empty(system.LastBirths);
        Assert.Equal(2, world.Entities.Count);
    }
}