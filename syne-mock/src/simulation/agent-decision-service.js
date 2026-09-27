const { round } = require('./deterministic-random');
const { consume } = require('../world/resource-service');

const ACTIONS = [
  'Idle', 'SeekFood', 'SeekWater', 'Rest', 'Flee', 'Socialize',
  'Explore', 'Eat', 'Drink'
];
const MOVEMENT_ACTIONS = new Set(['SeekFood', 'SeekWater', 'Flee', 'Socialize', 'Explore']);

function clamp(value, min = 0, max = 100) {
  return Math.max(min, Math.min(max, value));
}

class AgentDecisionService {
  constructor(settings, stocks, movementService) {
    this.settings = settings;
    this.stocks = stocks;
    this.movementService = movementService;
  }

  updateNeeds(agent) {
    const needs = this.settings.needs;
    agent.hunger = clamp(agent.hunger + needs.hungerRate);
    agent.thirst = clamp(agent.thirst + needs.thirstRate);
    agent.fatigue = clamp(agent.fatigue + needs.fatigueRate);
    agent.safety = clamp(agent.safety + needs.safetyDriftRate, 0, 1);
    agent.social = clamp(agent.social + needs.socialDriftRate, 0, 1);
    agent.curiosity = clamp(agent.curiosity + needs.curiosityDriftRate, 0, 1);
  }

  decide(agent, tick) {
    const needs = this.settings.needs;
    const candidates = ['Idle'];
    if (agent.hunger >= needs.hungerTriggerThreshold)
      candidates.push(this.stocks.food > 0 ? 'Eat' : 'SeekFood');
    if (agent.thirst >= needs.thirstTriggerThreshold)
      candidates.push(this.stocks.water > 0 ? 'Drink' : 'SeekWater');
    if (agent.fatigue >= needs.fatigueTriggerThreshold) candidates.push('Rest');
    if (agent.safety <= 0.5) candidates.push('Flee');
    if (agent.social >= 0.7) candidates.push('Socialize');
    if (agent.curiosity >= 0.3) candidates.push('Explore');

    const deliberationInterval = Math.max(1, this.settings.deliberationIntervalTicks);
    const shouldDeliberate = (tick + agent.id) % deliberationInterval === 0 ||
      agent.currentIntention === 'Idle';

    if (!shouldDeliberate && candidates.includes(agent.currentIntention))
      return { action: agent.currentIntention, deliberated: false, candidates };

    if (!shouldDeliberate && MOVEMENT_ACTIONS.has(agent.currentIntention))
      return { action: agent.currentIntention, deliberated: false, candidates };

    const scored = candidates.map((action, order) => ({
      action,
      order,
      utility: this.utility(agent, action) +
        (agent.currentIntention === action ? 0.05 : 0)
    }));
    scored.sort((left, right) => right.utility - left.utility || left.order - right.order);
    return { action: scored[0].action, utility: scored[0].utility, deliberated: true, candidates };
  }

  utility(agent, action) {
    const need = action === 'Eat' || action === 'SeekFood' ? agent.hunger
      : action === 'Drink' || action === 'SeekWater' ? agent.thirst
        : action === 'Rest' ? agent.fatigue
          : action === 'Flee' ? (1 - agent.safety) * 100
            : action === 'Socialize' ? agent.social * 100
              : action === 'Explore' ? agent.curiosity * 100 : 0;
    const benefit = action === 'Rest' ? Math.min(need, 40)
      : action === 'Explore' ? Math.min(need, 30)
        : Math.min(need, 30);
    const sigmoid = 1 / (1 + Math.exp(-0.1 * (need - 50)));
    const urgency = sigmoid * 20;
    const personality = action === 'Explore' ? 0.5 + this.settings.traits.curiosity
      : ['Eat', 'Drink', 'SeekFood', 'SeekWater'].includes(action)
        ? 0.5 + this.settings.traits.greed
        : action === 'Socialize' ? 0.5 + this.settings.traits.sociability : 1;
    const cost = MOVEMENT_ACTIONS.has(action) ? this.settings.moveEnergyCost
      : action === 'Eat' ? this.settings.eatEnergyCost
        : action === 'Drink' ? this.settings.drinkEnergyCost : 0;
    const risk = action === 'Explore' ? 0.25
      : action === 'Flee' ? 0.3
        : action === 'Socialize' || action === 'Eat' || action === 'Drink' ? 0.1
          : action === 'Rest' ? 0.05
            : ['SeekFood', 'SeekWater'].includes(action) ? 0.15 : 0;
    return ((benefit - cost - risk) * 0.5 * personality) + urgency;
  }

  execute(agent, tick, world, obstacles) {
    this.updateNeeds(agent);
    const selection = this.decide(agent, tick);
    const action = selection.action;
    const before = {
      energy: agent.energy,
      hunger: agent.hunger,
      thirst: agent.thirst,
      fatigue: agent.fatigue
    };
    let outcome = 'executed';
    let cause = null;
    let reserve = null;
    let reserveConsumed = null;

    if (MOVEMENT_ACTIONS.has(action)) {
      this.movementService.move(agent, tick, action, world, obstacles);
      agent.energy = clamp(agent.energy - this.settings.moveEnergyCost);
    } else if (action === 'Eat') {
      if (consume(this.stocks, 'food', this.settings.reserveConsumption)) {
        reserve = 'food';
        reserveConsumed = this.settings.reserveConsumption;
        agent.energy = clamp(agent.energy - this.settings.eatEnergyCost);
        agent.hunger = clamp(agent.hunger - this.settings.eatHungerRecovery);
      } else {
        outcome = 'blocked';
        cause = 'réserve food vide';
      }
    } else if (action === 'Drink') {
      if (consume(this.stocks, 'water', this.settings.reserveConsumption)) {
        reserve = 'water';
        reserveConsumed = this.settings.reserveConsumption;
        agent.energy = clamp(agent.energy - this.settings.drinkEnergyCost);
        agent.thirst = clamp(agent.thirst - this.settings.drinkThirstRecovery);
      } else {
        outcome = 'blocked';
        cause = 'réserve water vide';
      }
    } else if (action === 'Rest') {
      agent.energy = clamp(agent.energy + this.settings.restEnergyGain);
      agent.fatigue = clamp(agent.fatigue - this.settings.restFatigueRecovery);
    }

    agent.currentIntention = action;
    agent.currentAction = outcome === 'blocked' ? 'Idle' : action;
    agent.goals = action === 'Idle' ? [] : [{ kind: action, age: tick }];

    return {
      action,
      outcome,
      cause,
      deliberated: selection.deliberated,
      utility: round(selection.utility ?? this.utility(agent, action)),
      agentId: agent.id,
      energyDelta: round(agent.energy - before.energy),
      hungerDelta: round(agent.hunger - before.hunger),
      thirstDelta: round(agent.thirst - before.thirst),
      fatigueDelta: round(agent.fatigue - before.fatigue),
      ...(reserve ? { reserve, reserveConsumed } : {})
    };
  }
}

module.exports = { AgentDecisionService, ACTIONS, MOVEMENT_ACTIONS };
