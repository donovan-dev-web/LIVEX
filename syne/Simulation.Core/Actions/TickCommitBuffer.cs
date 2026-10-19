// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Conséquences de tick différées (fenêtre causale fermée, chapitre III) : les mutations de monde et les
//        effets portés à une AUTRE entité sont mis en file pendant l'itération puis appliqués en fin de tick,
//        dans l'ordre des identifiants.

using Simulation.Core.Cognition;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

/// <summary>
/// Tampon des conséquences du tick. Pendant l'itération des entités, une action
/// <b>ne mute plus immédiatement</b> le monde : le déplacement est mis en file et
/// le dégât porté à un pair aussi. Tout est appliqué après la boucle, dans
/// l'ordre des identifiants — de sorte que la perception d'un agent ne voie
/// jamais un effet produit par un agent traité plus tôt dans le même tick.
///
/// <para>
/// Contrat (ch. III §1) : « les conséquences d'une action dans un tick
/// n'affectent le monde qu'en fin de tick, et n'influencent donc les décisions
/// qu'au tick suivant ». Sans ce tampon, l'ordre des identifiants devenait une
/// loi causale non déclarée (S-15/S-31/D-11/B-06).
/// </para>
///
/// <para>
/// <b>Exception déclarée</b> : le prélèvement d'un <b>gisement</b>
/// (<see cref="ResourceNode"/>) reste séquentiel dans l'ordre des identifiants.
/// Il n'est pas différé parce qu'une action terminale (Eat/Drink) doit savoir si
/// elle a réussi <i>avant</i> d'appliquer son effet sur le besoin ; différer le
/// prélèvement imposerait soit une révision a posteriori du résultat, soit une
/// double dépense silencieuse. La conséquence visible est bornée à la quantité
/// d'un stock partagé (comme l'était la réserve globale), jamais à la perception.
/// </para>
///
/// <b>Déterminisme</b> : aucun tirage du PRNG ; les files sont triées par
/// (identifiant, ordre d'insertion) avant application.
/// </summary>
public sealed class TickCommitBuffer
{
    private readonly List<MoveRequest> _moves = [];
    private readonly List<EffectRequest> _effects = [];
    private long _sequence;

    /// <summary>Déplacements en attente d'application (diagnostic/tests).</summary>
    public int PendingMoveCount => _moves.Count;

    /// <summary>Effets inter-entités en attente d'application (diagnostic/tests).</summary>
    public int PendingEffectCount => _effects.Count;

    /// <summary>Vide le tampon (fin de tick ou remise à zéro).</summary>
    public void Clear()
    {
        _moves.Clear();
        _effects.Clear();
        _sequence = 0;
    }

    /// <summary>Met en file un déplacement effectif de l'entité vers <paramref name="position"/>.</summary>
    public void RequestMove(Entity entity, Position position)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _moves.Add(new MoveRequest(entity.Id.Value, _sequence++, entity, position));
    }

    /// <summary>Met en file un dégât d'énergie porté à une autre entité.</summary>
    public void RequestEnergyDamage(ulong targetId, MindState target, double damage)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (damage == 0.0)
        {
            return;
        }

        _effects.Add(new EffectRequest(targetId, _sequence++, target, damage, 0.0));
    }

    /// <summary>Met en file une dégradation de sûreté imposée à une autre entité (menace perçue).</summary>
    public void RequestSafetyThreat(ulong targetId, MindState target, double amount)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (amount == 0.0)
        {
            return;
        }

        _effects.Add(new EffectRequest(targetId, _sequence++, target, 0.0, amount));
    }

    /// <summary>
    /// Applique toutes les conséquences du tick : déplacements (identifiants
    /// croissants, puis ordre d'insertion), puis effets inter-entités. La défense
    /// du tick (<see cref="MindState.DefendingThisTick"/>) est résolue ici, sur
    /// l'état effectif de la cible à la fin de l'itération.
    /// </summary>
    public void Commit(World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_moves.Count > 0)
        {
            _moves.Sort(static (a, b) =>
            {
                int byId = a.EntityId.CompareTo(b.EntityId);
                return byId != 0 ? byId : a.Sequence.CompareTo(b.Sequence);
            });

            foreach (MoveRequest move in _moves)
            {
                world.Grid.Move(move.Entity, move.Position);
            }

            _moves.Clear();
        }

        if (_effects.Count > 0)
        {
            _effects.Sort(static (a, b) =>
            {
                int byId = a.TargetId.CompareTo(b.TargetId);
                return byId != 0 ? byId : a.Sequence.CompareTo(b.Sequence);
            });

            foreach (EffectRequest effect in _effects)
            {
                if (effect.Damage != 0.0)
                {
                    double damage = effect.Target.DefendingThisTick ? effect.Damage * 0.5 : effect.Damage;
                    effect.Target.Needs.ExertEnergy(damage);
                }

                if (effect.Threat != 0.0)
                {
                    effect.Target.Needs.ThreatenSafety(effect.Threat);
                }
            }

            _effects.Clear();
        }

        _sequence = 0;
    }

    private readonly record struct MoveRequest(ulong EntityId, long Sequence, Entity Entity, Position Position);

    private readonly record struct EffectRequest(ulong TargetId, long Sequence, MindState Target, double Damage, double Threat);
}
