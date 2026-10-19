// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationLoop - partie Resources.

namespace Simulation.Core.Loop;

using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

public sealed partial class SimulationLoop
{

    /// <summary>
    /// Modèle de ressources du run (arbitrage Phase 0). <b>Localisé</b> : les
    /// gisements sont posés à l'init aux emplacements que la description du monde
    /// publie déjà à PRISM (<b>0 tirage du PRNG</b> — LCG local dérivé de la
    /// graine), et les réserves globales deviennent un <b>agrégat publié</b> de ces
    /// gisements. <b>Global</b> : aucune source spatiale, la réserve mondiale reste
    /// la source de Eat/Drink (modèle historique).
    /// </summary>
    private void InitializeResourceModel(World.World world, Simulation.Core.Configuration.SimulationOptions options, double dt)
    {
        _localizedResources = options.Resources.Localized;
        _resourceDt = dt;
        if (!_localizedResources)
        {
            return;
        }

        world.ApplyResourceLayout(options.Resources, options.Random.Seed, options.Simulation.WorldCellSize);
        SyncResourceAggregate();
    }


    /// <summary>
    /// Resynchronise les réserves globales sur la somme des gisements : c'est la vue
    /// publiée par l'observabilité (les séries ECHOS gardent leur forme, mais leur
    /// valeur est désormais la quantité réellement présente dans le monde).
    /// </summary>
    internal void SyncResourceAggregate()
    {
        double food = 0.0, water = 0.0, wood = 0.0, mineral = 0.0;
        foreach (Simulation.Core.World.ResourceNode node in World.ResourceNodes)
        {
            switch (node.Kind)
            {
                case Simulation.Core.World.ResourceKind.Food:
                    food += node.Amount;
                    break;
                case Simulation.Core.World.ResourceKind.Water:
                    water += node.Amount;
                    break;
                case Simulation.Core.World.ResourceKind.Wood:
                    wood += node.Amount;
                    break;
                default:
                    mineral += node.Amount;
                    break;
            }
        }

        Resources.RestoreState(food, water, wood, mineral);
    }


    /// <summary>
    /// Cycle des ressources en fin de tick : régénération des gisements (module
    /// saisonnier × échelle temporelle) et resynchronisation de l'agrégat publié en
    /// modèle localisé ; cycle des réserves globales sinon (SYNE-070).
    /// </summary>
    private void AdvanceResources()
    {
        if (!_localizedResources)
        {
            Resources.ApplyLifecycle(CurrentTick, _effectiveResources, SeasonFactorsForTick());
            return;
        }

        Simulation.Core.Configuration.SeasonFactors? factors = SeasonFactorsForTick();
        foreach (Simulation.Core.World.ResourceNode node in World.ResourceNodes)
        {
            node.Regenerate((factors?.For(node.Kind) ?? 1.0) * _resourceDt);
        }

        SyncResourceAggregate();
    }

    public World.World World { get; }

    /// <summary>
    /// Options du run telles qu'elles ont été fournies à la boucle (avant mise à
    /// l'échelle temporelle) — identité de configuration : le hash canonique d'un
    /// run se calcule sur cet objet (<see cref="Persistence.RunIdentity"/>).
    /// </summary>
    public Simulation.Core.Configuration.SimulationOptions Options => _options;

    /// <summary>
    /// Horloge du run : correspondance tick ⇄ temps simulé issue de
    /// <c>simulation.simulatedSecondsPerTick</c> — exposée aux capteurs
    /// (<see cref="Observability.WorldSnapshot"/>) et aux clients.
    /// </summary>
    public Simulation.Core.Configuration.SimulationClock Clock => _clock;

    /// <summary>Réserves globales de ressources (SYNE-042) : consommées par Eat/Drink, exposées dans le snapshot.</summary>
    public World.ResourceStocks Resources { get; internal set; }


    /// <summary>
    /// Restauration des gisements localisés (persistance) : remplace la disposition
    /// d'init par l'état sauvegardé quand le snapshot en porte, puis resynchronise
    /// l'agrégat publié. Un snapshot sans gisements (modèle global, ou snapshot
    /// antérieur au réalignement) laisse la disposition courante intacte.
    /// </summary>
    internal void RestoreResourceNodes(IReadOnlyList<Persistence.ResourceNodeSnapshotDto>? nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return;
        }

        World.ReplaceResourceNodes(nodes.Select(node => new Simulation.Core.World.ResourceNode(
            node.Id,
            (World.ResourceKind)node.Kind,
            new World.Position(node.X, node.Y),
            node.Amount,
            node.Capacity,
            node.RegenerationRate)));
        SyncResourceAggregate();
    }

    public ulong CurrentTick { get; private set; }

    public Xoshiro256StarStar Rng => _rng;

    public Simulation.Core.Cognition.CognitionPipeline Cognition => _cognition;

    /// <summary>
    /// Collecteur de budgets de tick (SYNE-090) si la boucle en a été munie
    /// (sinon <c>null</c> — aucun coût sur le chemin nominal).
    /// </summary>
    public TickBudgetCollector? Budgets => _budget;

    /// <summary>
    /// Changements de saison survenus au tick courant (SYNE-072) : vidés
    /// (<see cref="ClearSeasonChanges"/>) par la couche d'observabilité après
    /// réémission — même protocole que les modifications d'environnement.
    /// </summary>
    public IReadOnlyList<Configuration.SeasonChange> LastSeasonChanges => _seasonChanges;
}
