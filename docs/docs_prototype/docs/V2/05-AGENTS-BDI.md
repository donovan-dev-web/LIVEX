# Agents : Architecture BDI interne

## 1. Vue générale

Un **Agent V2** est une entité autonome guidée par le cycle BDI (Belief-Desire-Intention).

```
Tick i:
  ┌─────────────────────────────────────────┐
  │  1. PERCEPTION                          │
  │     Read sensors (nearby entities)      │
  │     ↓                                   │
  │  2. MEMORY UPDATE                       │
  │     Store observations, apply decay     │
  │     ↓                                   │
  │  3. BELIEF REVISION                     │
  │     Update beliefs based on new info    │
  │     ↓                                   │
  │  4. NEEDS & MOTIVATION                  │
  │     Calculate current need levels       │
  │     ↓                                   │
  │  5. GOAL GENERATION                     │
  │     Create goals from unmet needs       │
  │     ↓                                   │
  │  6. GOAL FILTERING                      │
  │     Keep feasible goals only            │
  │     ↓                                   │
  │  7. UTILITY EVALUATION                  │
  │     Score potential actions             │
  │     ↓                                   │
  │  8. DELIBERATION                        │
  │     Choose action with highest utility  │
  │     ↓                                   │
  │  9. ACTION EXECUTION                    │
  │     Execute chosen action (may span     │
  │     multiple ticks)                     │
  │     ↓                                   │
  │ 10. OUTPUT: Decision record, events     │
  └─────────────────────────────────────────┘
```

---

## 2. Structure de la classe Agent

```csharp
public class Agent
{
    // Identity
    public string Id { get; set; }
    public string Name { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Heading { get; set; }      // Direction facing
    public float Energy { get; set; }          // 0-100
    
    // BDI Components
    public PerceptionSystem Perception { get; set; }
    public MemorySystem Memory { get; set; }
    public BeliefStore Beliefs { get; set; }
    public NeedsSystem Needs { get; set; }
    public GoalSystem Goals { get; set; }
    public DecisionSystem Decision { get; set; }
    public ActionSystem CurrentAction { get; set; }
    
    // Communication
    public CommunicationQueue IncomingMessages { get; set; }
    public RelationshipNetwork Relationships { get; set; }
    
    // Inventory
    public Inventory Inventory { get; set; }
    
    // Groups
    public HashSet<string> GroupIds { get; set; }
    
    // Personality/Traits
    public Traits Traits { get; set; }
    
    // Lifecycle
    public AgentStatus Status { get; set; }   // Active, Resting, Dead
    public ulong CreatedTick { get; set; }
    public ulong? DeathTick { get; set; }
    
    // Debug
    public DecisionRecord LastDecision { get; set; }
}

public enum AgentStatus
{
    Active,
    Resting,
    Sleeping,
    Dead
}
```

---

## 3. Système de perception (détaillé)

### 3.1 Concept

Agent perçoit les entités proches via des capteurs à portée limitée.

```csharp
public class PerceptionSystem
{
    public float SensorRadius { get; set; }    // e.g., 50 units
    public float Accuracy { get; set; }        // 0-1, noise in perception
    
    public List<Observation> Perceive(
        Agent agent, 
        World world,
        ulong currentTick)
    {
        var observations = new List<Observation>();
        
        // Spatial query: entities within radius
        var nearby = world.SpatialGrid.QueryRadius(agent.Position, SensorRadius);
        
        foreach (var entity in nearby)
        {
            // Accuracy check
            if (Random.Shared.NextSingle() > Accuracy)
                continue;  // Perception failed
            
            var obs = new Observation
            {
                EntityId = entity.Id,
                EntityType = entity.GetType().Name,
                Position = entity.Position,
                Distance = Vector2.Distance(agent.Position, entity.Position),
                ObservedAt = currentTick,
                Confidence = 1.0f - (Distance / SensorRadius) * 0.3f  // Closer = more confident
            };
            
            // Type-specific attributes
            if (entity is Agent other)
            {
                obs.Attributes = new Dictionary<string, object>
                {
                    { "AgentId", other.Id },
                    { "Energy", other.Energy },
                    { "Status", other.Status },
                    { "Heading", other.Heading }
                };
            }
            else if (entity is Resource resource)
            {
                obs.Attributes = new Dictionary<string, object>
                {
                    { "ResourceType", resource.Type },
                    { "Quantity", resource.Quantity },
                    { "Regeneration", resource.RegenerationRate }
                };
            }
            
            observations.Add(obs);
        }
        
        return observations;
    }
}

public class Observation
{
    public string EntityId { get; set; }
    public string EntityType { get; set; }
    public Vector2 Position { get; set; }
    public float Distance { get; set; }
    public ulong ObservedAt { get; set; }
    public float Confidence { get; set; }      // 0-1
    public Dictionary<string, object> Attributes { get; set; }
}
```

### 3.2 Algorithme de perception (pseudo-code)

```
FUNCTION Perceive(agent, world, tick):
  observations ← []
  
  // Query spatial index
  nearby_entities ← SpatialGrid.QueryRadius(agent.position, sensor_radius)
  
  FOR EACH entity IN nearby_entities:
    // Roll accuracy check
    IF Random() > agent.traits.perception_accuracy:
      CONTINUE  // Missed this entity
    
    // Calculate confidence (closer = more confident)
    distance ← Distance(agent.position, entity.position)
    confidence ← 1.0 - (distance / sensor_radius) * 0.3
    confidence ← Clamp(confidence, 0.7, 1.0)  // Even distant objects have 0.7 min
    
    obs ← Observation(
      entity_id=entity.id,
      entity_type=entity.type,
      position=entity.position,
      confidence=confidence,
      observed_at=tick,
      attributes=ExtractAttributes(entity)
    )
    
    observations.Append(obs)
  
  RETURN observations
```

---

## 4. Système de mémoire (détaillé)

### 4.1 Concept

Les agents conservent un historique personnel d'observations avec une décroissance exponentielle.

```csharp
public class MemorySystem
{
    public const int MaxMemorySize = 1000;
    private Queue<MemoryEntry> _memory;
    
    public float DecayRate { get; set; }       // e.g., 0.05 (5% per tick)
    
    public void StoreObservation(Observation obs, ulong tick)
    {
        var entry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            ObservationData = obs,
            StoredAt = tick,
            Category = MemoryCategory.Observation,
            Salience = 1.0f
        };
        
        _memory.Enqueue(entry);
        
        // Trim if over capacity
        while (_memory.Count > MaxMemorySize)
            _memory.Dequeue();
    }
    
    public List<MemoryEntry> Recall(ulong currentTick, MemoryCategory? filter = null)
    {
        var result = new List<MemoryEntry>();
        
        foreach (var entry in _memory)
        {
            // Apply decay
            ulong age = currentTick - entry.StoredAt;
            entry.Salience = MathF.Exp(-DecayRate * age);
            
            // Filter
            if (filter != null && entry.Category != filter)
                continue;
            
            // Don't return "dead" memories
            if (entry.Salience > 0.01f)
                result.Add(entry);
        }
        
        return result;
    }
}

public class MemoryEntry
{
    public string Id { get; set; }
    public Observation ObservationData { get; set; }
    public ulong StoredAt { get; set; }
    public MemoryCategory Category { get; set; }
    public float Salience { get; set; }        // 0-1, decays over time
}

public enum MemoryCategory
{
    Observation,
    Event,
    Interaction,
    Communication,
    Social
}
```

### 4.2 Algorithme de décroissance

```
FUNCTION ApplyDecay(memory_entries, current_tick, decay_rate):
  FOR EACH entry IN memory_entries:
    age ← current_tick - entry.stored_at
    salience ← exp(-decay_rate * age)
    entry.salience ← salience
    
    IF salience < 0.01:
      MARK entry AS "dead"  // Can be purged

FUNCTION Recall(filter=None):
  recalled ← []
  FOR EACH entry IN memory:
    IF entry.salience > 0.01 AND (filter == None OR entry.category == filter):
      recalled.Append(entry)
  RETURN recalled
```

---

## 5. Belief Store

### 5.1 Concept

Les agents maintiennent des croyances (faits avec un niveau de confiance) distinctes de la vérité terrain.

```csharp
public class BeliefStore
{
    private Dictionary<string, Belief> _beliefs;  // fact_id → belief
    
    public void AddOrUpdateBelief(Belief belief, ulong currentTick)
    {
        var key = belief.Fact.Id;
        
        if (_beliefs.ContainsKey(key))
        {
            var existing = _beliefs[key];
            
            // Belief revision: incoming contradicts existing?
            if (BeliefConflicts(existing, belief))
            {
                // Lower both confidences slightly
                existing.Confidence = MathF.Max(0.1f, existing.Confidence - 0.1f);
                belief.Confidence = MathF.Max(0.1f, belief.Confidence - 0.1f);
            }
            else if (BeliefAligns(existing, belief))
            {
                // Reinforce existing
                existing.Confidence = MathF.Min(1.0f, existing.Confidence + 0.2f);
            }
            
            existing.LastUpdated = currentTick;
            existing.Source = belief.Source;
            
            _beliefs[key] = existing;
        }
        else
        {
            _beliefs[key] = belief;
        }
    }
    
    public List<Belief> GetBeliefs(float minConfidence = 0.0f)
    {
        return _beliefs.Values
            .Where(b => b.Confidence >= minConfidence)
            .ToList();
    }
    
    public bool Believes(string factId, float minConfidence = 0.5f)
    {
        return _beliefs.ContainsKey(factId) && 
               _beliefs[factId].Confidence >= minConfidence;
    }
}

public class Belief
{
    public string Id { get; set; }
    public Fact Fact { get; set; }             // What is believed
    public float Confidence { get; set; }      // 0-1, how sure
    public string Source { get; set; }         // "observation", "hearsay", "inference"
    public ulong CreatedAt { get; set; }
    public ulong LastUpdated { get; set; }
    public ulong? ExpiryTick { get; set; }     // Belief becomes suspect after this
}

public class Fact
{
    public string Id { get; set; }
    public string Subject { get; set; }        // Entity this fact is about
    public string Predicate { get; set; }      // What property (e.g., "position", "energy")
    public object Value { get; set; }          // Actual value
}
```

### 5.2 Règles de révision des croyances

```
FUNCTION BeliefRevision(existing_belief, new_belief):
  IF ConflictingBeliefs(existing, new):
    // Lower confidence in both
    existing.confidence ← max(0.1, existing.confidence - 0.1)
    new.confidence ← max(0.1, new.confidence - 0.1)
  
  ELSE IF AlignedBeliefs(existing, new):
    // Reinforce via convergence
    existing.confidence ← min(1.0, existing.confidence + 0.2)
  
  ELSE IF DifferentSources(existing, new):
    // Different sources = ambiguous
    existing.confidence ← (existing.confidence + new.confidence) / 2
  
  UPDATE existing.last_updated ← current_tick
  UPDATE existing.source ← new.source
```

---

## 6. Système de besoins

### 6.1 Concept

Un agent possède plusieurs besoins qui orientent la formation d'objectifs.

```csharp
public class NeedsSystem
{
    public Dictionary<string, Need> Needs { get; private set; }
    
    public NeedsSystem(Agent agent)
    {
        Needs = new Dictionary<string, Need>
        {
            { "Hunger", new Need { Id = "Hunger", MaxLevel = 100, DecayPerTick = 0.5f } },
            { "Thirst", new Need { Id = "Thirst", MaxLevel = 100, DecayPerTick = 0.7f } },
            { "Fatigue", new Need { Id = "Fatigue", MaxLevel = 100, DecayPerTick = 0.3f } },
            { "Safety", new Need { Id = "Safety", MaxLevel = 100, DecayPerTick = 0.2f } },
            { "Social", new Need { Id = "Social", MaxLevel = 100, DecayPerTick = 0.1f } },
            { "Curiosity", new Need { Id = "Curiosity", MaxLevel = 100, DecayPerTick = 0.2f } }
        };
    }
    
    public void UpdateNeeds(Agent agent, World world, ulong tick)
    {
        // Hunger increases naturally
        Needs["Hunger"].Level = MathF.Min(100, Needs["Hunger"].Level + Needs["Hunger"].DecayPerTick);
        Needs["Thirst"].Level = MathF.Min(100, Needs["Thirst"].Level + Needs["Thirst"].DecayPerTick);
        
        // Fatigue decreases if resting
        if (agent.Status == AgentStatus.Resting)
            Needs["Fatigue"].Level = MathF.Max(0, Needs["Fatigue"].Level - 2.0f);
        else
            Needs["Fatigue"].Level = MathF.Min(100, Needs["Fatigue"].Level + Needs["Fatigue"].DecayPerTick);
        
        // Safety based on environment (danger nearby?)
        var threats = world.SpatialGrid.QueryRadius(agent.Position, 30).OfType<Agent>()
            .Where(a => a.Relationships.GetTrust(agent.Id) < 0.3f);  // Untrusted agents
        if (threats.Any())
            Needs["Safety"].Level = MathF.Min(100, Needs["Safety"].Level + 5.0f);
        else
            Needs["Safety"].Level = MathF.Max(0, Needs["Safety"].Level - 1.0f);
        
        // Curiosity increases in novel environments
        var novelty = CalculateNovelty(agent, world);
        Needs["Curiosity"].Level += novelty;
    }
    
    public List<Need> GetUnmetNeeds(float threshold = 50.0f)
    {
        return Needs.Values.Where(n => n.Level > threshold).ToList();
    }
}

public class Need
{
    public string Id { get; set; }
    public float Level { get; set; }           // 0-100
    public float MaxLevel { get; set; } = 100;
    public float DecayPerTick { get; set; }    // Increases need per tick
}
```

---

## 7. Cycle de décision (haut niveau)

### 7.1 Pseudo-code

```
PROCEDURE TickAgent(agent, world, tick):
  
  // Step 1: Perception
  observations ← agent.perception.Perceive(agent, world, tick)
  
  // Step 2: Memory & belief update
  FOR EACH observation IN observations:
    agent.memory.StoreObservation(observation, tick)
    
    // Convert observation to belief
    belief ← ObservationToBelief(observation)
    agent.beliefs.AddOrUpdate(belief, tick)
  
  // Step 3: Process incoming messages
  messages ← agent.inbox.DequeueAll()
  FOR EACH message IN messages:
    // Update beliefs from message
    ReceiveMessage(agent, message, tick)
    // Adjust trust in sender
    UpdateTrust(agent, message.sender, message.veracity)
  
  // Step 4: Calculate needs
  agent.needs.UpdateNeeds(agent, world, tick)
  
  // Step 5: Goal generation
  unmet_needs ← agent.needs.GetUnmetNeeds(threshold=50)
  candidate_goals ← GenerateGoals(unmet_needs, agent)
  
  // Step 6: Filter for feasibility
  feasible_goals ← []
  FOR EACH goal IN candidate_goals:
    IF CanAchieve(agent, goal):
      feasible_goals.Append(goal)
  
  // Step 7: Utility evaluation
  action_scores ← []
  FOR EACH goal IN feasible_goals:
    FOR EACH action IN PossibleActions(agent, goal):
      score ← EvaluateUtility(agent, action, goal, tick)
      action_scores.Append((action, score))
  
  // Step 8: Deliberation
  best_action ← SelectHighestUtility(action_scores)
  
  // Step 9: Execute action
  IF CurrentAction exists AND NOT Interrupted:
    Continue()
  ELSE:
    Start(best_action)
  
  // Step 10: Record decision
  decision_record ← DecisionRecord(
    tick=tick,
    beliefs=agent.beliefs.GetBeliefs(),
    goals=feasible_goals,
    utility_scores=action_scores,
    chosen_action=best_action,
    reason="highest utility"
  )
  agent.last_decision ← decision_record
```

### 7.2 Squelette d'implémentation C#

```csharp
public class Agent
{
    public void Tick(World world, ulong tick)
    {
        // 1. Perceive
        var observations = Perception.Perceive(this, world, tick);
        
        // 2. Memory + Beliefs
        foreach (var obs in observations)
        {
            Memory.StoreObservation(obs, tick);
            var belief = ObservationToBelief(obs);
            Beliefs.AddOrUpdateBelief(belief, tick);
        }
        
        // 3. Process messages
        ProcessIncomingMessages(tick);
        
        // 4. Update needs
        Needs.UpdateNeeds(this, world, tick);
        
        // 5-6. Goal generation & filtering
        var unmetNeeds = Needs.GetUnmetNeeds();
        var candidateGoals = GoalSystem.GenerateGoals(unmetNeeds, this);
        var feasibleGoals = candidateGoals.Where(g => CanAchieve(g)).ToList();
        
        // 7-8. Utility & deliberation
        var actionScores = EvaluateActions(feasibleGoals);
        var bestAction = actionScores.OrderByDescending(a => a.Score).First().Action;
        
        // 9. Execute
        if (CurrentAction != null && !CurrentAction.IsInterrupted(this))
            CurrentAction.Update(this, world);
        else
            StartAction(bestAction);
        
        // 10. Record
        LastDecision = new DecisionRecord
        {
            Tick = tick,
            Beliefs = Beliefs.GetBeliefs(),
            Goals = feasibleGoals,
            ActionScores = actionScores,
            ChosenAction = bestAction
        };
    }
}
```

---

## 8. Exemple complet de flux pseudo-code

```
TICK 5000:
  
  Agent "Alice" at position (50, 75)
  
  1. PERCEPTION
     - Scan radius 50 units
     - Find: Resource (Food, pos 60,80, dist 13, conf 0.95)
     - Find: Agent Bob (pos 40,70, dist 15, conf 0.93)
     - Find: Obstacle (pos 70,75, dist 20, conf 0.97)
  
  2. MEMORY
     - Store 3 observations in memory
     - Apply decay to 50 old memories (oldest salience ≈ 0.05)
  
  3. BELIEFS
     - Add belief: "Food at (60,80)" confidence 0.95
     - Add belief: "Bob nearby" confidence 0.93
     - Revise belief "Bob status": was "resting", now "moving" → confidence 0.8
  
  4. NEEDS
     - Hunger = 75/100 (up from 72)
     - Thirst = 82/100
     - Fatigue = 45/100 (rested recently)
     - Safety = 25/100 (Bob is trusted)
  
  5-6. GOALS
     - Unmet needs: [Hunger(75), Thirst(82), Fatigue(45)]
     - Generate: [Eat, Drink, Rest]
     - Feasible: [Eat(yes - food nearby), Drink(no - no water nearby), Rest(yes)]
  
  7-8. UTILITY EVALUATION
     Action: Eat Food
       benefit = +20 (hunger relief)
       cost = -3 (walking to food)
       risk = -1 (passing by Bob)
        confidence = 0.95
        personality = 0.9 (Alice is brave)
        urgency = 1.82 (moderate hunger, time-sensitive)
        utility = (20 - 3 - 1) * 0.95 * 0.9 + 1.82 = 15.5
     
     Action: Rest
       benefit = +5 (fatigue relief)
       cost = 0
       risk = 0
       confidence = 1.0
       utility = 5.0
     
      Best action: Eat (15.5 > 5.0)
  
  9. EXECUTE
     - Previous action was "MoveTo Food"
     - Continue: move 3 steps toward food
     - New position: (53, 76.5)
  
  10. RECORD
     - Decision trace saved
     - Events: ["MovedTo(53, 76.5)", "ApproachFood"]
```

