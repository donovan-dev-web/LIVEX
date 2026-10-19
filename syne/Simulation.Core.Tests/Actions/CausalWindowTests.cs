// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gate de la fenêtre causale (S-15/S-31/D-11/B-06) : les conséquences d'une action ne touchent le monde
//        qu'au commit de fin de tick, et le dégât porté à un pair y est résolu sur l'état effectif de la cible.

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Contrat du chapitre III §1 : « les conséquences d'une action dans un tick
/// n'affectent le monde qu'en fin de tick, et n'influencent donc les décisions
/// qu'au tick suivant ». Sans le tampon, l'ordre des identifiants devenait une
/// loi causale non déclarée.
/// </summary>
public class CausalWindowTests
{
    private static Entity MakeEntity(ulong id, Position position)
    {
        var traits = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["bravery"] = 1.0,
            ["curiosity"] = 1.0,
            ["sociability"] = 1.0,
            ["greed"] = 1.0,
            ["pessimism"] = 1.0,
            ["aggression"] = 1.0,
            ["strength"] = 1.0,
            ["speed"] = 1.0,
        };
        return new Entity(new EntityId(id), "Entité A", name: null, position, new TraitSet(traits), bornAt: 0);
    }

    private static (Simulation.Core.World.World World, TickCommitBuffer Buffer) Build(params (ulong Id, Position Position)[] entities)
    {
        var world = new Simulation.Core.World.World(new WorldSize(500, 500));
        var buffer = new TickCommitBuffer();
        foreach ((ulong id, Position position) in entities)
        {
            world.AddEntity(MakeEntity(id, position));
        }

        return (world, buffer);
    }

    [Fact]
    public void RequestedMove_IsNotVisibleBeforeTheCommit()
    {
        (Simulation.Core.World.World world, TickCommitBuffer buffer) = Build((1, new Position(100, 100)));
        Entity mover = world.Entities.Single();

        buffer.RequestMove(mover, new Position(101, 100));

        // Pendant le tick, la position publiée ne bouge pas : un agent traité plus tard
        // dans le même tick ne peut pas observer ce déplacement.
        Assert.Equal(100.0, mover.Position.X, 10);
        Assert.Equal(1, buffer.PendingMoveCount);

        buffer.Commit(world);

        Assert.Equal(101.0, mover.Position.X, 10);
        Assert.Equal(0, buffer.PendingMoveCount);
    }

    [Fact]
    public void Commit_AppliesMovesInIdentifierOrder_NotInRequestOrder()
    {
        // Deux déplacements demandés dans l'ordre inverse des identifiants : l'ordre
        // d'application est celui des identifiants (déterminisme), l'ordre d'insertion
        // ne servant qu'à départager un même identifiant.
        (Simulation.Core.World.World world, TickCommitBuffer buffer) = Build(
            (1, new Position(100, 100)),
            (2, new Position(100, 100)));
        Entity first = world.Entities.Single(entity => entity.Id.Value == 1);
        Entity second = world.Entities.Single(entity => entity.Id.Value == 2);

        buffer.RequestMove(second, new Position(120, 100));
        buffer.RequestMove(first, new Position(80, 100));

        buffer.Commit(world);

        Assert.Equal(80.0, first.Position.X, 10);
        Assert.Equal(120.0, second.Position.X, 10);
    }

    [Fact]
    public void Commit_ResolvesDefenseOnTheTargetsEffectiveState()
    {
        var options = ConfigLoader.LoadDefaults();
        (Simulation.Core.World.World world, TickCommitBuffer buffer) = Build(
            (1, new Position(100, 100)),
            (2, new Position(100, 100)));
        Entity attacked = world.Entities.Single(entity => entity.Id.Value == 2);
        MindState target = new(options);

        // La cible s'est déclarée en défense pendant le tick : le tampon applique la
        // réduction au commit, sur l'état effectif (et non sur un état intermédiaire).
        target.DefendingThisTick = true;
        buffer.RequestEnergyDamage(attacked.Id.Value, target, damage: 10.0);

        buffer.Commit(world);

        Assert.Equal(95.0, target.Needs.Energy, 10);
    }

    [Fact]
    public void RequestedThreat_LowersSafetyAtTheCommit()
    {
        var options = ConfigLoader.LoadDefaults();
        (Simulation.Core.World.World world, TickCommitBuffer buffer) = Build((1, new Position(100, 100)));
        MindState target = new(options);
        double safetyBefore = target.Needs.Safety;

        buffer.RequestSafetyThreat(targetId: 1, target, amount: 0.25);

        Assert.Equal(safetyBefore, target.Needs.Safety, 10); // rien pendant le tick
        Assert.Equal(1, buffer.PendingEffectCount);

        buffer.Commit(world);

        Assert.Equal(safetyBefore - 0.25, target.Needs.Safety, 10);
        Assert.Equal(0, buffer.PendingEffectCount);
    }

    [Fact]
    public void NullEffects_AreNotQueued_AndClearDiscardsTheTick()
    {
        (Simulation.Core.World.World world, TickCommitBuffer buffer) = Build((1, new Position(100, 100)));
        Entity entity = world.Entities.Single();
        MindState target = new(ConfigLoader.LoadDefaults());

        buffer.RequestEnergyDamage(1, target, damage: 0.0);
        buffer.RequestSafetyThreat(1, target, amount: 0.0);
        Assert.Equal(0, buffer.PendingEffectCount);

        buffer.RequestMove(entity, new Position(101, 100));
        buffer.Clear();
        buffer.Commit(world);

        Assert.Equal(100.0, entity.Position.X, 10); // le tick remis à zéro ne bouge rien
        Assert.Equal(0, buffer.PendingMoveCount);
    }
}
