// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SeasonSettings).

namespace Simulation.Core.Configuration;

public sealed class SeasonSettings
{
    /// <summary>Cycle de saisons actif (régénération modulée + événement <c>world.season_changed</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Saison du tick 0 (clé JSON camelCase, défaut <c>spring</c>).</summary>
    public string InitialSeason { get; set; } = World.Seasons.Name(World.Season.Spring);

    /// <summary>Durée d'une saison en ticks (défaut 360, temps simulé &gt; 6 h simulées à 1 tick/min).</summary>
    public int SeasonLengthTicks { get; set; } = 360;

    /// <summary>
    /// Les 4 définitions du cycle (une par saison, ordre indifférent) — défaut :
    /// spring (toutes ressources ×1), summer (eau ×1,2), autumn (bois ×1,2
    /// + nourriture ×1,1), winter (nourriture ×0,8, eau ×0,9) — valeurs V0.1
    /// de premier jet, calibrées en SYNE-120.
    /// </summary>
    public List<SeasonDefinition> Cycle { get; set; } =
    [
        new() { Name = World.Seasons.Name(World.Season.Spring), FoodFactor = 1.0, WaterFactor = 1.0, WoodFactor = 1.0, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Summer), FoodFactor = 1.0, WaterFactor = 1.2, WoodFactor = 1.0, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Autumn), FoodFactor = 1.1, WaterFactor = 1.0, WoodFactor = 1.2, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Winter), FoodFactor = 0.8, WaterFactor = 0.9, WoodFactor = 1.0, MineralFactor = 1.0 },
    ];

    /// <summary>Facteurs de régénération des 4 ressources pour un tick donné (cycle résolu, déterminisme).</summary>
    public SeasonFactors Factors(ulong tick)
    {
        World.Season season = At(tick);
        double food = 1.0, water = 1.0, wood = 1.0, mineral = 1.0;
        foreach (SeasonDefinition definition in Cycle)
        {
            if (DefinitionsByName.TryGetValue(definition.Name, out World.Season matches) && matches == season)
            {
                food = definition.FoodFactor;
                water = definition.WaterFactor;
                wood = definition.WoodFactor;
                mineral = definition.MineralFactor;
                break;
            }
        }

        return new SeasonFactors(food, water, wood, mineral);
    }

    /// <summary>Saison courante au tick <paramref name="tick"/> — fonction pure du tick (0 PRNG).</summary>
    public World.Season At(ulong tick)
    {
        int initialIndex = (World.Seasons.TryParse(InitialSeason) ?? World.Season.Spring) switch
        {
            World.Season.Spring => 0,
            World.Season.Summer => 1,
            World.Season.Autumn => 2,
            World.Season.Winter => 3,
            _ => 0,
        };
        int length = SeasonLengthTicks > 0 ? SeasonLengthTicks : 1;
        return (World.Season)((initialIndex + (int)(tick / (ulong)length)) % World.Seasons.Count);
    }

    private static readonly IReadOnlyDictionary<string, World.Season> DefinitionsByName =
        new Dictionary<string, World.Season>(StringComparer.Ordinal)
        {
            [World.Seasons.Name(World.Season.Spring)] = World.Season.Spring,
            [World.Seasons.Name(World.Season.Summer)] = World.Season.Summer,
            [World.Seasons.Name(World.Season.Autumn)] = World.Season.Autumn,
            [World.Seasons.Name(World.Season.Winter)] = World.Season.Winter,
        };
}
