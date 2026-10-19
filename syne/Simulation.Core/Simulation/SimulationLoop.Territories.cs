// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationLoop - partie Territories.

namespace Simulation.Core.Loop;

using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

public sealed partial class SimulationLoop
{

    /// <summary>Vide la file des changements d'appartenance (drain d'observabilité).</summary>
    public void ClearTerritoryChanges() => _territoryChanges.Clear();

    /// <summary>
    /// Mutations de livres survenues depuis la dernière collecte (SYNE-121) :
    /// vidées (<see cref="ClearBookChanges"/>) par la couche d'observabilité
    /// après réémission — même protocole que les changements de saison, de
    /// territoire et de constructions (drain).
    /// </summary>
    public IReadOnlyList<Simulation.Core.World.BookChange> LastBookChanges => _bookChanges;


    /// <summary>
    /// Identifiants des entités présentes dans la zone <paramref name="zoneId"/>
    /// au tick courant — ordre croissant (déterministe), initialement vide.
    /// </summary>
    public IReadOnlyList<ulong> MembersOfTerritory(string zoneId)
    {
        if (_territoryMembership.TryGetValue(zoneId, out System.Collections.Generic.HashSet<ulong>? members))
        {
            return members.OrderBy(member => member).ToList();
        }

        return [];
    }


    /// <summary>
    /// Suivi de l'appartenance aux zones de territoire au tick courant (SYNE-073,
    /// la présence d'une entité dans le disque la classe membre du
    /// territoire effectif. Recalculée à chaque tick (les positions sont finales
    /// après les boucles entités) — **0 tirage PRNG** ; chaque différence avec le
    /// tick précédent est **tracée** (<see cref="LastTerritoryChanges"/>) dans un
    /// ordre déterministe : par zone (ordre de pose), par entité (id croissant),
    /// sorties avant entrées. Cycle inactif : aucun tracé, appartenance nulle.
    ///
    /// Coût : une seule passe sur les entités, avec test de zone par entité
    /// (O(entités × zones) *arithmétiques*, sans allocation par zone) — la version
    /// précédente construisait un <c>HashSet</c> par zone et par tick.
    /// </summary>
    private void TrackTerritoryMembership()
    {
        Simulation.Core.Configuration.TerritorySettings territories = _options.World.Territories;
        if (!territories.Enabled)
        {
            _territoryMembership.Clear();
            return;
        }

        IReadOnlyList<World.Territory> zones = World.Territories;
        var current = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<ulong>>(System.StringComparer.Ordinal);
        foreach (World.Territory zone in zones)
        {
            current[zone.Id] = [];
        }

        foreach (Simulation.Core.Entities.Entity entity in World.Entities)
        {
            foreach (World.Territory zone in zones)
            {
                if (zone.Contains(entity.Position))
                {
                    current[zone.Id].Add(entity.Id.Value);
                }
            }
        }

        foreach (World.Territory zone in zones)
        {
            System.Collections.Generic.HashSet<ulong> previous =
                _territoryMembership.TryGetValue(zone.Id, out System.Collections.Generic.HashSet<ulong>? prev) ? prev : [];
            System.Collections.Generic.HashSet<ulong> now = current[zone.Id];

            foreach (ulong left in previous.Where(member => !now.Contains(member)).OrderBy(member => member))
            {
                _territoryChanges.Add(new Simulation.Core.World.TerritoryMembershipChange(
                    Simulation.Core.World.TerritoryMembershipChangeKind.Left, zone, left));
            }

            foreach (ulong entered in now.Where(member => !previous.Contains(member)).OrderBy(member => member))
            {
                _territoryChanges.Add(new Simulation.Core.World.TerritoryMembershipChange(
                    Simulation.Core.World.TerritoryMembershipChangeKind.Entered, zone, entered));
            }
        }

        _territoryMembership = current;
    }


    /// <summary>
    /// Restauration (SYNE-111/112) de l'appartenance effective au tick T. Sans cet
    /// amorçage, le premier tick restauré comparerait l'appartenance sauvegardée à
    /// un état vide et réémettrait une rafale de faux événements
    /// <c>entered</c> pour des entités déjà dans la zone avant la sauvegarde.
    /// </summary>
    internal void RestoreTerritoryMembership(IReadOnlyDictionary<string, IReadOnlyList<ulong>> membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        _territoryMembership = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<ulong>>(
            membership.ToDictionary(
                pair => pair.Key,
                pair => (System.Collections.Generic.HashSet<ulong>)[.. pair.Value],
                System.StringComparer.Ordinal));
        _territoryChanges.Clear();
    }
}
