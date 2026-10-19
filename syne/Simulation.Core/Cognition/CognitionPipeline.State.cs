// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie State.

namespace Simulation.Core.Cognition;

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Performance;
using Simulation.Core.Population;
using Simulation.Core.Social;

public sealed partial class CognitionPipeline
{

    /// <summary>Positions courantes des esprits (post-commit) — cohésion de groupe.</summary>
    private Dictionary<ulong, World.Position> Positions()
    {
        var positions = new Dictionary<ulong, World.Position>(_minds.Count);
        foreach (Entity entity in _world.Entities)
        {
            if (_minds.ContainsKey(entity.Id.Value))
            {
                positions[entity.Id.Value] = entity.Position;
            }
        }

        return positions;
    }


    private MindState GetOrCreate(ulong entityId, Entity entity)
    {
        if (!_minds.TryGetValue(entityId, out MindState? mind))
        {
            mind = new MindState(_options);
            _minds[entityId] = mind;
        }

        return mind;
    }


    /// <summary>
    /// Restauration de l'état cognitif complet (persistance bit-à-bit,
    /// remplace le dictionnaire d'esprits et ré-importe les
    /// groupes émergents. Aucun tirage du PRNG — la suite reste bit-à-bit.
    /// </summary>
    internal void RestoreState(IReadOnlyDictionary<ulong, MindState> minds, IEnumerable<Social.Group> groups, ulong nextGroupId)
    {
        ArgumentNullException.ThrowIfNull(minds);
        ArgumentNullException.ThrowIfNull(groups);

        _minds.Clear();
        foreach ((ulong id, MindState mind) in minds)
        {
            _minds[id] = mind;
        }

        // Le GroupSystem est readonly — restauration en place des groupes actifs
        // et du compteur d'identifiants (persistance bit-à-bit).
        _groups.RestoreState(groups, nextGroupId);
    }
}

