# Système de décision avancé — Utilité multidimensionnelle

## 1. Vue générale

Chaque agent fait une **décision utilitaire** à chaque tick basée sur :

- **Belief** (ce que l'agent croit)
- **Needs** (faim, soif, fatigue, etc.)
- **Goals** (objectifs générés des besoins)
- **Traits** (personnalité affectant les préférences)
- **Context** (monde state localement observable)

**Formule base** :

```
utility = (benefit - cost - risk) * confidence * personality_modifier + urgency
```

---

## 2. Évaluation de l'utilité en détail

### 2.1 Composants multidimensionnels

```csharp
public class UtilityEvaluator
{
    public class UtilityBreakdown
    {
        public float Benefit { get; set; }        // Positive value
        public float Cost { get; set; }           // Resource cost
        public float Risk { get; set; }           // Danger/uncertainty
        public float Confidence { get; set; }     // Agent's certainty
        public float Urgency { get; set; }        // Time sensitivity
        public float PersonalityMod { get; set; } // Trait-based adjustment
        
        public float Total =>
            ((Benefit - Cost - Risk) * Confidence * PersonalityMod) + Urgency;
    }
    
    public UtilityBreakdown Evaluate(
        Agent agent,
        Action action,
        Goal goal,
        World world,
        ulong tick)
    {
        var breakdown = new UtilityBreakdown();
        
        // 1. BENEFIT
        breakdown.Benefit = CalculateBenefit(agent, action, goal);
        
        // 2. COST
        breakdown.Cost = CalculateCost(agent, action, world);
        
        // 3. RISK
        breakdown.Risk = CalculateRisk(agent, action, world);
        
        // 4. CONFIDENCE
        breakdown.Confidence = CalculateConfidence(agent, action, world);
        
        // 5. URGENCY
        breakdown.Urgency = CalculateUrgency(agent, goal);
        
        // 6. PERSONALITY
        breakdown.PersonalityMod = CalculatePersonalityModifier(agent, action);
        
        return breakdown;
    }
}
```

### 2.2 Calcul du bénéfice

```csharp
private float CalculateBenefit(Agent agent, Action action, Goal goal)
{
    float benefit = 0;
    
    switch (action.Type)
    {
        case "Eat":
            // Reduction in hunger
            benefit = MathF.Min(agent.Needs["Hunger"].Level, 30);
            break;
            
        case "Drink":
            benefit = MathF.Min(agent.Needs["Thirst"].Level, 25);
            break;
            
        case "Explore":
            // Reduce curiosity + potential discovery
            benefit = agent.Needs["Curiosity"].Level * 0.5f + 10;
            break;
            
        case "Rest":
            benefit = MathF.Min(agent.Needs["Fatigue"].Level, 40);
            break;
            
        case "SocialInteract":
            benefit = agent.Needs["Social"].Level * 0.8f;
            break;
            
        case "Gather":
            // Benefit depends on how much can gather
            benefit = action.Metadata["ResourceAmount"] as float? ?? 10;
            break;
            
        default:
            benefit = 5;
            break;
    }
    
    // Goal alignment bonus
    if (action.SupportsGoal(goal))
        benefit *= 1.2f;
    
    return benefit;
}
```

### 2.3 Calcul du coût

```csharp
private float CalculateCost(Agent agent, Action action, World world)
{
    float cost = 0;
    
    // Energy cost
    var energyCost = action.EnergyCost;
    cost += energyCost / 10;  // Normalized to 0-10 range
    
    // Movement cost (distance × speed)
    if (action is MoveToAction moveAction)
    {
        var distance = Vector2.Distance(agent.Position, moveAction.Target);
        var travelCost = distance / agent.Traits.MovementSpeed;
        cost += travelCost;
    }
    
    // Opportunity cost (what am I not doing?)
    var currentNeed = GetMostUrgentNeed(agent);
    if (currentNeed.Id != action.PrimaryNeed)
    {
        cost += currentNeed.Level / 20;  // High needs = high opportunity cost
    }
    
    // Communication cost (energy to broadcast message)
    if (action is CommunicateAction commAction)
    {
        var messageSize = commAction.Message.Length / 100.0f;
        cost += messageSize;
    }
    
    return MathF.Max(0, cost);
}
```

### 2.4 Calcul du risque

```csharp
private float CalculateRisk(Agent agent, Action action, World world)
{
    float risk = 0;
    
    // Threat assessment
    var nearby = world.SpatialGrid.QueryRadius(agent.Position, 50);
    var threats = nearby.OfType<Agent>()
        .Where(a => agent.Relationships.GetTrust(a.Id) < 0.3f)
        .ToList();
    
    if (threats.Count > 0)
    {
        risk += threats.Count * 5;  // More threats = more risk
    }
    
    // Action-specific risks
    switch (action.Type)
    {
        case "Explore":
            // Exploring unknown = higher risk
            risk += 10;
            break;
            
        case "Trade":
            // Trade with unknown = risk of being cheated
            var tradingWith = world.Agents.FirstOrDefault(a => a.Id == action.Metadata["TargetAgent"] as string);
            if (tradingWith != null)
            {
                var trust = agent.Relationships.GetTrust(tradingWith.Id);
                risk += (1 - trust) * 15;  // Low trust = higher risk
            }
            break;
            
        case "Attack":
            // Combat risk
            var target = world.Agents.FirstOrDefault(a => a.Id == action.Metadata["TargetAgent"] as string);
            if (target != null)
            {
                var strengthRatio = agent.Traits.Strength / (target.Traits.Strength + 0.1f);
                risk += (1 - strengthRatio) * 20;  // Weaker target = less risk
            }
            break;
    }
    
    // Environmental risk (low energy, weak, etc.)
    if (agent.Energy < 20)
        risk += 5;
    
    return MathF.Max(0, risk);
}
```

### 2.5 Calcul de la confiance

```csharp
private float CalculateConfidence(Agent agent, Action action, World world)
{
    float confidence = 0.5f;  // Base confidence
    
    // Knowledge-based confidence
    var beliefs = agent.Beliefs.GetBeliefs();
    
    switch (action.Type)
    {
        case "MoveTo":
            var target = action.Metadata["Target"] as Vector2?;
            var targetBeliefs = beliefs.Where(b => b.Fact.Subject == target?.ToString()).ToList();
            confidence = targetBeliefs.Average(b => b.Confidence);
            break;
            
        case "Eat":
            var foodBeliefs = beliefs.Where(b => b.Fact.Predicate == "food_location").ToList();
            if (foodBeliefs.Count > 0)
                confidence = foodBeliefs.Average(b => b.Confidence);
            else
                confidence = 0.3f;  // Low confidence if no food knowledge
            break;
            
        case "Trade":
            var tradingWith = world.Agents.FirstOrDefault(a => a.Id == action.Metadata["TargetAgent"] as string);
            if (tradingWith != null)
            {
                var trust = agent.Relationships.GetTrust(tradingWith.Id);
                confidence = 0.5f + trust * 0.5f;  // Trust affects confidence
            }
            break;
    }
    
    // Action success history
    var actionHistory = agent.ActionHistory.Where(ah => ah.ActionType == action.Type).ToList();
    if (actionHistory.Count > 0)
    {
        var successRate = actionHistory.Count(ah => ah.Success) / (float)actionHistory.Count;
        confidence *= (0.5f + successRate * 0.5f);  // History modifies confidence
    }
    
    return MathF.Min(1.0f, MathF.Max(0.0f, confidence));
}
```

### 2.6 Calcul de l'urgence

```csharp
private float CalculateUrgency(Agent agent, Goal goal)
{
    float urgency = 0;
    
    // Urgency based on need level
    var need = agent.Needs.Needs.Values.FirstOrDefault(n => n.Id == goal.PrimaryNeed);
    if (need != null)
    {
        // Sigmoid curve: small effect at low levels, sharp increase near max
        urgency = 1 / (1 + MathF.Exp(-0.1f * (need.Level - 50)));
        urgency *= 20;  // Scale to 0-20 range
    }
    
    // Time-based urgency (goals get more urgent if not addressed)
    var goalAge = agent.CurrentTick - goal.CreatedAt;
    if (goalAge > 100)
        urgency += 5;  // Old goals become more urgent
    
    // Critical threshold
    if (agent.Energy < 10 || agent.Needs["Hunger"].Level > 90)
        urgency += 10;  // Critical state = high urgency
    
    return urgency;
}
```

### 2.7 Modificateur de personnalité

```csharp
private float CalculatePersonalityModifier(Agent agent, Action action)
{
    var traits = agent.Traits;
    float mod = 1.0f;
    
    // Bravery affects risky actions
    if (action.IsRisky)
        mod *= (0.5f + traits.Bravery);  // Range: 0.5 (coward) to 1.5 (brave)
    
    // Curiosity affects exploration
    if (action.Type == "Explore")
        mod *= (0.5f + traits.Curiosity);
    
    // Sociability affects social actions
    if (action.IsSocial)
        mod *= (0.5f + traits.Sociability);
    
    // Greed affects resource gathering
    if (action.Type == "Gather")
        mod *= (0.5f + traits.Greed);
    
    // Pessimism affects risk-taking
    if (action.IsRisky)
        mod *= (2.0f - traits.Pessimism);  // Pessimistic = reduce risky actions
    
    return MathF.Max(0.1f, mod);
}
```

---

## 3. Flux de décision (exemple complet)

```
Agent "Charlie" at tick 5000

STEP 1: Calculate needs
  Hunger: 68 (up from 65 last tick)
  Thirst: 45
  Fatigue: 30
  Safety: 15 (trusted agents nearby)
  Social: 52
  Curiosity: 40

STEP 2: Unmet needs (threshold 50)
  Hunger (68), Thirst (45), Social (52) → Unmet
  
STEP 3: Generate goals
  From Hunger: Goal "Eat" (priority 0.8)
  From Thirst: Goal "Drink" (priority 0.6, but water not nearby → low feasibility)
  From Social: Goal "Interact" (priority 0.7)
  
STEP 4: Filter feasible goals
  - Eat: Food nearby? YES → Keep
  - Drink: Water nearby? NO → Remove
  - Interact: Agents nearby? YES, trusted? YES → Keep
  
  Feasible: [Eat (0.8), Interact (0.7)]

STEP 5: Possible actions per goal
  For "Eat" goal:
    - MoveTo food
    - Eat if at food
  For "Interact" goal:
    - MoveTo agent
    - Communicate
    - CooperateOnTask

STEP 6: Score each action
  ─────────────────────────────────────────────────────────
  Action "MoveTo food"
    Benefit:        18 (hunger relief)
    Cost:           4 (movement)
    Risk:           2 (passing untrusted agent)
    Confidence:     0.85 (good belief about food location)
    Urgency:        6 (hunger moderately high)
    Personality:    1.1 (Charlie is slightly brave)
    
    Utility = (18 - 4 - 2) * 0.85 * 1.1 + 6 = 17.22
  
  ─────────────────────────────────────────────────────────
  Action "Communicate with David"
    Benefit:        8 (social need relief)
    Cost:           1 (energy to send message)
    Risk:           1 (David is acquaintance, not close friend)
    Confidence:     0.7 (David might not respond)
    Urgency:        3 (social need less urgent)
    Personality:    0.8 (Charlie is slightly introverted)
    
    Utility = (8 - 1 - 1) * 0.7 * 0.8 + 3 = 6.36
  
  ─────────────────────────────────────────────────────────
  Action "MoveTo David"
    Benefit:        8
    Cost:           5 (longer movement)
    Risk:           1
    Confidence:     0.9 (David visible)
    Urgency:        3
    Personality:    0.8
    
    Utility = (8 - 5 - 1) * 0.9 * 0.8 + 3 = 4.44
  
  ─────────────────────────────────────────────────────────
  Action "Rest"
    Benefit:        3 (slight fatigue relief)
    Cost:           0
    Risk:           0
    Confidence:     1.0 (always works)
    Urgency:        1 (fatigue low)
    Personality:    1.0
    
    Utility = (3 - 0 - 0) * 1.0 * 1.0 + 1 = 4.0

STEP 7: Select best action
  Scores:
    MoveTo food:        17.22  ← SELECTED
    Communicate David:   6.36
    MoveTo David:        4.44
    Rest:                4.0

STEP 8: Execute
  - If already executing MoveTo food, continue
  - Else, start new MoveTo action toward (65, 80)
  - Set heading toward food
  - Move 3 units this tick

STEP 9: Record decision
  Decision recorded in decision_traces table
```

---

## 4. Réévaluation dynamique (interruption)

```csharp
public void CheckForInterruption(Agent agent, World world, ulong tick)
{
    var currentAction = agent.CurrentAction;
    if (currentAction == null)
        return;
    
    // Check if dramatic need change
    var unmetNeeds = agent.Needs.GetUnmetNeeds();
    var topNeed = unmetNeeds.First();
    
    if (topNeed.Level > 85)  // Critical need
    {
        var criticalGoal = GoalSystem.GenerateGoals(new[] { topNeed }).First();
        var criticalUtility = EvaluateGoal(agent, criticalGoal, world);
        
        // Get current action's utility
        var currentUtility = currentAction.CurrentGoal != null
            ? EvaluateGoal(agent, currentAction.CurrentGoal, world)
            : 0;
        
        if (criticalUtility > currentUtility + 10)  // Interrupt threshold
        {
            // Interrupt current action
            currentAction.Interrupt();
            
            // Start new action for critical goal
            var newAction = SelectBestAction(agent, new[] { criticalGoal }, world);
            agent.StartAction(newAction);
            
            Log.Warning("Agent {AgentId} interrupted action due to {Need}={Level}",
                agent.Id, topNeed.Id, topNeed.Level);
        }
    }
}
```

---

## 5. Système de traits

```csharp
public class Traits
{
    // Personality traits (0-2 range)
    public float Bravery { get; set; } = 1.0f;           // Risk tolerance
    public float Curiosity { get; set; } = 1.0f;         // Exploration drive
    public float Sociability { get; set; } = 1.0f;       // Group preference
    public float Greed { get; set; } = 1.0f;             // Resource focus
    public float Pessimism { get; set; } = 1.0f;         // Risk aversion
    public float Aggression { get; set; } = 1.0f;        // Combat willingness
    
    // Capabilities (0-2 range)
    public float Strength { get; set; } = 1.0f;
    public float Speed { get; set; } = 1.0f;
    public float Intelligence { get; set; } = 1.0f;      // Affects decision quality
    public float Perception { get; set; } = 1.0f;        // Affects sensor accuracy
}

// Initialize with variance
public static Traits Randomize()
{
    return new Traits
    {
        Bravery = 0.5f + Random.Shared.NextSingle(),
        Curiosity = 0.5f + Random.Shared.NextSingle(),
        Sociability = 0.5f + Random.Shared.NextSingle(),
        // ...
    };
}
```

---

## 6. Historique de décision et apprentissage

```csharp
public class DecisionHistory
{
    public List<DecisionRecord> Decisions { get; } = new();
    
    public float GetSuccessRate(string actionType)
    {
        var decisions = Decisions.Where(d => d.ChosenAction == actionType).ToList();
        if (decisions.Count == 0) return 0.5f;  // Neutral if no history
        
        return decisions.Count(d => d.WasSuccessful) / (float)decisions.Count;
    }
    
    // Track if chosen action was beneficial
    public void RecordOutcome(string decisionId, bool wasSuccessful, float actualBenefit)
    {
        var decision = Decisions.FirstOrDefault(d => d.Id == decisionId);
        if (decision != null)
        {
            decision.WasSuccessful = wasSuccessful;
            decision.ActualBenefit = actualBenefit;
        }
    }
}

public class DecisionRecord
{
    public string Id { get; set; }
    public ulong Tick { get; set; }
    public string ChosenAction { get; set; }
    public float PredictedUtility { get; set; }
    public bool WasSuccessful { get; set; }
    public float ActualBenefit { get; set; }
}
```

---

## 7. Résumé

La prise de décision V2 est **sophistiquée mais maniable** :

- Scoring d'utilité multi-facteurs (bénéfice, coût, risque, confiance, urgence, personnalité)
- Variance basée sur les traits → comportements diversifiés à partir des mêmes règles
- Intégration des croyances en temps réel → décisions fondées sur une observabilité partielle
- Interruption dynamique → les besoins à haute priorité supplantent les actions planifiées
- Historique de décision → les agents « apprennent » ce qui fonctionne

Cela crée une **diversité stratégique émergente** : même situation → différents agents → différentes décisions → interactions → dynamiques complexes.

