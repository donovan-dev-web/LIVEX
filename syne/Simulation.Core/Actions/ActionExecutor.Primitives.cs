// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : ActionExecutor - partie Primitives.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Navigation;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

public sealed partial class ActionExecutor
{

    /// <summary>
    /// Exécute une primitive D7 si <paramref name="kind"/> en est une, sinon null.
    /// <list type="bullet">
    /// <item><b>Take</b> (D7+D8) : prélève <c>agents.inventory.takeAmount</c> de la
    /// réserve Food (Water si faim faible) vers l'inventaire — échoue si l'inventaire
    /// est inactif, si la réserve est vide ou si la capacité ne couvre pas la quantité.</item>
    /// <item><b>Give</b> (D7) : transfert <c>giveAmount</c> vers l'entité vivante la
    /// plus proche (même sémantique de cible que Socialize) — échoue sans cible,
    /// sans stock ou si la capacité du destinataire est insuffisante.</item>
    /// <item><b>Trade</b> (D7+D8) : échange fixe 1 Food ↔ 1 Water (× takeAmount) avec
    /// l'entité la plus proche — échoue si l'un des deux inventaires ne peut pas payer.
    /// La confiance des deux parties est renforcée (interaction positive réciproque).</item>
    /// <item><b>Attack</b> (D7/D3 temps 1) : inflige <c>5 × aggressivité</c> de dégât
    /// d'énergie à la cible la plus proche — attaque neutre, jamais portée
    /// par un besoin (doctrine de progressivité), réduite de moitié si la cible se
    /// défend ce tick.</item>
    /// <item><b>Defend</b> (D7) : pose le drapeau de défense du tick (réduction de
    /// moitié d'une attaque subie).</item>
    /// </list>
    /// Toutes ces primitives consomment l'énergie du catalogue et échouent en
    /// <see cref="ActionOutcome.Blocked"/> sans aucun effet partiel.
    /// </summary>
    private ActionResult? ExecutePrimitive(
        Entity entity,
        MindState mind,
        DesireKind kind,
        ulong currentTick,
        Func<ulong, MindState?>? mindLookup)
    {
        switch (kind)
        {
            case DesireKind.Take:
                return ExecuteTake(entity, mind);
            case DesireKind.Give:
                return ExecuteGive(entity, mind, mindLookup);
            case DesireKind.Trade:
                return ExecuteTrade(entity, mind, mindLookup);
            case DesireKind.Attack:
                return ExecuteAttack(entity, mind, currentTick, mindLookup);
            case DesireKind.Defend:
                return ExecuteDefend(entity, mind);
            default:
                return null;
        }
    }


    private ActionResult ExecuteTake(Entity entity, MindState mind)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Take, "inventaire inactif (agents.inventory.enabled = false)");
        }

        // Réserve ciblée : Food par défaut, Water si la soif domine la faim
        // (recette fixe courte — pas de planificateur).
        ResourceKind reserve = mind.Needs.Hunger >= mind.Needs.Thirst ? ResourceKind.Food : ResourceKind.Water;
        double amount = Math.Min(_options.Agents.Actions.Inventory.TakeAmount, inventory.FreeWeight);
        double available = AvailableAtHand(entity, reserve);
        if (amount <= 0.0 || available <= 0.0)
        {
            return ActionResult.Blocked(
                DesireKind.Take,
                available <= 0.0
                    ? (_options.Resources.Localized ? $"aucun gisement {reserve} à portée" : $"réserve {reserve} vide")
                    : "capacité d'inventaire saturée");
        }

        // Atomicité : prélèvement source d'abord (échec = aucun effet), stockage ensuite.
        if (available < amount)
        {
            return ActionResult.Blocked(DesireKind.Take, $"source {reserve} insuffisante ({available:0.###} < {amount:0.###})");
        }

        ConsumeAtHand(entity, reserve, amount);

        double? stored = inventory.TryTake(reserve, amount);
        if (stored is null)
        {
            // Invariant : amount est plafonné par FreeWeight avant le prélèvement
            // monde, donc TryTake ne peut pas échouer ici. Défensive : signaler
            // sans effet partiel (la réserve monde reste débitée d'un montant
            // stocké nulle part — cas impossible par construction).
            return ActionResult.Blocked(DesireKind.Take, "capacité d'inventaire saturée");
        }

        return MechanicalResult(DesireKind.Take, entity, mind);
    }


    /// <summary>Quantité disponible <b>sous la main</b> pour une primitive d'inventaire (gisement ou réserve mondiale).</summary>
    private double AvailableAtHand(Entity entity, ResourceKind kind)
    {
        if (!_options.Resources.Localized)
        {
            return _stocks.Stock(kind);
        }

        return _world.NearestResourceNode(entity.Position, kind, _options.Resources.InteractionRadius)?.Amount ?? 0.0;
    }


    /// <summary>Prélève la source sous la main (gisement localisé ou réserve mondiale).</summary>
    private void ConsumeAtHand(Entity entity, ResourceKind kind, double amount)
    {
        if (!_options.Resources.Localized)
        {
            _stocks.TryConsume(kind, amount);
            return;
        }

        _world.NearestResourceNode(entity.Position, kind, _options.Resources.InteractionRadius)?.TryConsume(amount);
    }


    private ActionResult ExecuteGive(Entity entity, MindState mind, Func<ulong, MindState?>? mindLookup)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Give, "inventaire inactif (agents.inventory.enabled = false)");
        }

        Entity? recipient = NearestKnownEntity(entity, mind);
        if (recipient is null)
        {
            return ActionResult.Blocked(DesireKind.Give, "aucune entité connue à portée");
        }

        // Don de la ressource détenue en plus grande quantité (recette fixe ;
        // départage par ordre stable du type — déterminisme).
        ResourceKind held = inventory.Snapshot()
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => (int)pair.Key)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (inventory.Amount(held) <= 0.0)
        {
            return ActionResult.Blocked(DesireKind.Give, "inventaire vide");
        }

        MindState? recipientMind = mindLookup?.Invoke(recipient.Id.Value);
        Inventory? recipientInventory = recipientMind?.Inventory;
        if (recipientInventory is null)
        {
            return ActionResult.Blocked(DesireKind.Give, "destinataire sans inventaire");
        }

        double amount = Math.Min(_options.Agents.Actions.Inventory.GiveAmount, inventory.Amount(held));
        if (!inventory.TryGive(held, amount, recipientInventory))
        {
            return ActionResult.Blocked(DesireKind.Give, "capacité du destinataire insuffisante");
        }

        // Le don est une interaction sociale positive réciproque.
        mind.Trust.Interact(recipient.Id.Value);
        recipientMind!.Trust.Interact(entity.Id.Value);
        return MechanicalResult(DesireKind.Give, entity, mind);
    }


    private ActionResult ExecuteTrade(Entity entity, MindState mind, Func<ulong, MindState?>? mindLookup)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Trade, "inventaire inactif (agents.inventory.enabled = false)");
        }

        Entity? partner = NearestKnownEntity(entity, mind);
        if (partner is null)
        {
            return ActionResult.Blocked(DesireKind.Trade, "aucun partenaire connu à portée");
        }

        MindState? partnerMind = mindLookup?.Invoke(partner.Id.Value);
        if (partnerMind?.Inventory is not { } partnerInventory)
        {
            return ActionResult.Blocked(DesireKind.Trade, "partenaire sans inventaire");
        }

        // Échange fixe 1 Food ↔ 1 Water (× takeAmount, recette V0.1).
        double amount = _options.Agents.Actions.Inventory.TradeAmount;
        (ResourceKind mine, ResourceKind theirs) = mind.Needs.Hunger >= mind.Needs.Thirst
            ? (ResourceKind.Water, ResourceKind.Food)
            : (ResourceKind.Food, ResourceKind.Water);
        if (inventory.Amount(mine) < amount || partnerInventory.Amount(theirs) < amount)
        {
            return ActionResult.Blocked(DesireKind.Trade, "stock insuffisant pour l'échange");
        }

        // Atomicité bidirectionnelle : chaque transfert tout-ou-rien, vérifié avant
        // application — si l'un échoue, aucun des deux n'est exécuté.
        if (partnerInventory.FreeWeight < amount || inventory.FreeWeight < amount)
        {
            return ActionResult.Blocked(DesireKind.Trade, "capacité d'inventaire insuffisante");
        }

        if (!inventory.TryGive(mine, amount, partnerInventory)
            || !partnerInventory.TryGive(theirs, amount, inventory))
        {
            return ActionResult.Blocked(DesireKind.Trade, "échec du transfert bidirectionnel");
        }

        // L'échange est une interaction positive réciproque.
        mind.Trust.Interact(partner.Id.Value);
        partnerMind.Trust.Interact(entity.Id.Value);
        return MechanicalResult(DesireKind.Trade, entity, mind);
    }


    private ActionResult ExecuteAttack(
        Entity entity,
        MindState mind,
        ulong currentTick,
        Func<ulong, MindState?>? mindLookup)
    {
        Entity? target = NearestKnownEntity(entity, mind);
        if (target is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "aucune cible connue à portée");
        }

        MindState? targetMind = mindLookup?.Invoke(target.Id.Value);
        if (targetMind is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "cible sans état cognitif");
        }

        double damage = 5.0 * Math.Max(0.0, mind.Factors?.Aggressiveness ?? 1.0);
        if (_commit is { } buffer)
        {
            // Fenêtre causale fermée (ch. III) : le dégât est une conséquence du
            // tick, appliquée en fin d'itération — la défense de la cible est
            // résolue à ce moment, sur le drapeau du tick courant, et l'agent
            // suivant ne voit jamais un état à moitié mis à jour.
            buffer.RequestEnergyDamage(target.Id.Value, targetMind, damage);
            buffer.RequestSafetyThreat(target.Id.Value, targetMind, 0.10);
        }
        else
        {
            targetMind.Needs.ExertEnergy(targetMind.DefendingThisTick ? damage * 0.5 : damage);
            targetMind.Needs.ThreatenSafety(0.10);
        }

        return MechanicalResult(DesireKind.Attack, entity, mind);
    }


    private ActionResult ExecuteDefend(Entity entity, MindState mind)
    {
        mind.DefendingThisTick = true;
        return MechanicalResult(DesireKind.Defend, entity, mind);
    }


    /// <summary>Résultat mécanique standard d'une primitive (coût énergétique du catalogue).</summary>
    private ActionResult MechanicalResult(DesireKind kind, Entity entity, MindState mind)
    {
        ActionDefinition definition = _catalog[kind];
        double energyDelta = 0.0;
        if (definition.EnergyCost > 0.0)
        {
            energyDelta = -definition.EnergyCost;
            mind.Needs.ExertEnergy(definition.EnergyCost);
        }

        return new ActionResult(kind, ActionOutcome.Executed, null, energyDelta, 0.0, 0.0, 0.0, null, 0.0);
    }


    /// <summary>
    /// Entité vivante la plus proche <b>parmi celles que l'agent connaît</b> : la
    /// cible est choisie dans les <b>croyances de position</b> (issues de la
    /// perception ou d'un message), filtrées par la portée perceptuelle et la ligne
    /// de vue. Aucune lecture de la collection globale des entités n'intervient dans
    /// le <i>choix</i> (S-20 / D-09 / B-11) : un pair hors rayon ou masqué n'est pas
    /// candidat.
    /// <para>
    /// La résolution « identifiant connu → entité » est une simple résolution de
    /// référence sur un identifiant déjà cru (et non un parcours de recherche de
    /// cible) ; elle est bornée par le nombre de croyances de l'agent.
    /// </para>
    /// </summary>
    private Entity? NearestKnownEntity(Entity self, MindState mind)
    {
        Entity? nearest = null;
        double bestDistance = double.MaxValue;
        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (!string.Equals(belief.Fact.Predicate, KnowledgeBuilder.EntityPositionPredicate, StringComparison.Ordinal) ||
                !belief.Fact.Subject.StartsWith("entity-", StringComparison.Ordinal) ||
                !KnowledgeBuilder.TryParsePosition(belief.Fact.Value, out Position position))
            {
                continue;
            }

            if (!ulong.TryParse(
                    belief.Fact.Subject["entity-".Length..],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out ulong knownId) ||
                knownId == self.Id.Value)
            {
                continue;
            }

            double distance = self.Position.DistanceTo(position);
            if (distance > _options.Agents.Perception.Radius ||
                !LineOfSight.IsClear(self.Position, position, _world.Obstacles))
            {
                continue;
            }

            if (distance > bestDistance || (distance == bestDistance && nearest is not null && knownId >= nearest.Id.Value))
            {
                continue;
            }

            Entity? candidate = FindEntity(knownId);
            if (candidate is null)
            {
                continue;
            }

            nearest = candidate;
            bestDistance = distance;
        }

        return nearest;
    }


    /// <summary>Résolution d'une entité par identifiant <b>déjà cru</b> (jamais un critère de choix).</summary>
    private Entity? FindEntity(ulong id)
    {
        foreach (Entity entity in _world.Entities)
        {
            if (entity.Id.Value == id)
            {
                return entity;
            }
        }

        return null;
    }
}
