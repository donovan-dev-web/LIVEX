// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Non-régression du placement initial sur le profil `prism` : le layout d'obstacles est posé avant
//        les entités, et aucune entité ne démarre dans une case que la description du monde publie comme
//        bloquée (même règle que le mock, qui lit `cells[].walkable`).

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Console.Tests;

/// <summary>
/// Le profil <c>prism</c> déclare un layout d'obstacles : PRISM doit recevoir des
/// obstacles à l'initialisation, et le monde ne doit pas se contredire en plaçant
/// une entité sur une case publiée <c>walkable: false</c>. Le placement s'appuie
/// sur la description produite par <see cref="WorldDescriptionBuilder"/>, donc sur
/// la règle exactement telle qu'un client la lit.
/// </summary>
public sealed class PrismObstaclePlacementTests
{
    [Fact]
    public void PrismProfile_DeclaresObstacles()
    {
        SimulationOptions options = SimulationProfiles.Prism();

        Assert.True(options.World.Obstacles);
        Assert.NotEmpty(options.World.ObstacleLayout);
        Assert.Empty(SimulationOptionsValidator.Validate(options));
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(12_345UL)]
    [InlineData(777UL)]
    public void PrismProfile_PlacesNoEntityInACellTheWorldPublishesAsBlocked(ulong seed)
    {
        SimulationOptions options = SimulationProfiles.Prism();
        options.Agents.InitialCount = 100;

        (WorldType world, _) = Control.SimulationFactory.Build(options, seed);
        WorldDescription description = WorldDescriptionBuilder.Build(
            world,
            seed,
            cellSize: options.Simulation.WorldCellSize,
            ticksPerSecond: options.Simulation.TicksPerSecond,
            simulatedSecondsPerTick: options.Simulation.SimulatedSecondsPerTick,
            metersPerUnit: options.World.MetersPerUnit);

        // Le layout doit bloquer des cases, sinon le test ne prouve rien.
        Assert.Contains(description.Cells, cell => !cell.Walkable);

        foreach (Entity entity in world.Entities)
        {
            int cellX = (int)Math.Floor(entity.Position.X / options.Simulation.WorldCellSize);
            int cellY = (int)Math.Floor(entity.Position.Y / options.Simulation.WorldCellSize);
            WorldCell cell = description.Cells.Single(candidate => candidate.X == cellX && candidate.Y == cellY);
            Assert.True(
                cell.Walkable,
                $"entité {entity.Id.Value} en ({entity.Position.X}, {entity.Position.Y}) sur la case bloquée ({cellX}, {cellY}) : {string.Join(", ", cell.Obstacles)}");
        }
    }

    [Fact]
    public void ProfilesWithoutObstacles_AreUnaffectedByThePlacementRule()
    {
        // Sans obstacle, aucune position n'est retirée : le placement reste celui
        // des checksums dorés (le profil de référence ne déclare pas de layout).
        SimulationOptions options = SimulationProfiles.Reference();
        Assert.False(options.World.Obstacles);

        (WorldType world, _) = Control.SimulationFactory.Build(options, 12_345UL);
        Assert.Equal((ulong)options.Agents.InitialCount, (ulong)world.Entities.Count);
        Assert.Empty(world.Obstacles);
    }
}
