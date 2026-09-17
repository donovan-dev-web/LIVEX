# Spécification fonctionnelle V2

## 1. Vue générale

Ce document formalise les comportements attendus pour chaque système V2.

---

## 2. Système de Perception (Phase 1)

### 2.1 Responsabilité

Déterminer ce qu'un agent **peut observer** à ce tick.

### 2.2 Entrées

- Agent position (x, y)
- Perception radius (rayon configurable par espèce : 20–50m)
- World state (positions ressources, agents, obstacles)

### 2.3 Sortie

Liste des `Observation` :

```csharp
public class Observation
{
    public string EntityId { get; set; }           // "agent-42", "food-1", etc.
    public EntityType Type { get; set; }           // Agent, Food, Water, Obstacle
    public Vector2 Position { get; set; }          // abs position
    public float Distance { get; set; }            // from agent
    public float Direction { get; set; }           // angle in radians
    public Dictionary<string, object> Properties { get; set; }  // santé, quantité, etc.
    public ulong Timestamp { get; set; }           // tick of observation
}
```

### 2.4 Algorithme

```
FOR each entity in world:
    distance = Vector2.Distance(agent.position, entity.position)
    IF distance <= agent.perceptionRadius:
        Create Observation(entity)
        Add to agent.currentObservations[]
```

### 2.5 Cas limites

- **Obstacles bloquent perception ?** → V2 non, V3 possiblement
- **Jour/nuit affecte radius ?** → À configurer
- **Fatigue affecte perception ?** → Health/Energy modifie radius

---

## 3. Système de Mémoire (Phase 2)

### 3.1 Responsabilité

Conserver et gérer l'historique des observations.

### 3.2 Structure

```csharp
public class MemoryEntry
{
    public string Id { get; set; }
    public ObservationType Type { get; set; }     // Observation, Event, Interaction
    public string Data { get; set; }               // JSON serialized
    public ulong CreatedTick { get; set; }
    public float DecayRate { get; set; }           // exp decay coefficient
    public float CurrentConfidence { get; set; }   // 0–1
}
```

### 3.3 Décay temporel

À chaque tick :

```csharp
currentConfidence = originalConfidence * MathF.Exp(-decayRate * elapsedTicks)
```

Configuration par défaut :
- **Observations** : decay_rate = 0.01 (oubli lent)
- **Events** : decay_rate = 0.005 (très lent)
- **Interactions** : decay_rate = 0.002 (quasi persistent)

### 3.4 Limite mémoire

Max entries per agent : **1000** (configurable)

Au-delà → supprimer entrées les plus anciennes/décayées.

### 3.5 Accès mémoire

```csharp
// Récupérer mémoire par type
agent.memory.GetObservations(entityType: Food)
         .Where(m => m.currentConfidence > 0.5)
         .OrderByDescending(m => m.currentConfidence)

// Plus confiant d'abord
```

---

## 4. Système de Croyances (Phase 2)

### 4.1 Responsabilité

Construire et réviser une **représentation du monde** interne de l'agent.

### 4.2 Structure

```csharp
public class Belief
{
    public string Id { get; set; }
    public string Fact { get; set; }               // "Food at (85, 42)"
    public float Confidence { get; set; }          // 0–1
    public BeliefSource Source { get; set; }       // Direct, Memory, Hearsay
    public ulong LastConfirmedTick { get; set; }
    public ulong ExpiryTick { get; set; }           // Après, belief est suspect
    public string Domain { get; set; }              // Spatial, Social, etc.
}

public enum BeliefSource
{
    DirectPerception,
    Memory,
    Communication,
    Inference
}
```

### 4.3 Révision

À chaque tick, réviser beliefs :

```
1. Nouvelles observations
2. Pour chaque observation O :
   - Chercher belief B correspondant
   - SI trouvé :
       * Augmenter confidence (converger vers 1.0)
       * Mettre à jour expiryTick
   - SINON :
       * Créer nouveau belief
       * Confidence initiale = 0.8
```

Exemple :

```
Tick 100 : Agent A perçoit nourriture à (85, 42)
  → Belief created: "Food at (85, 42)", confidence 0.8, source DirectPerception
  
Tick 101 : Agent A perçoit à nouveau
  → Belief updated: confidence 0.95
  
Tick 110 : Pas de nouvelle observation
  → Confidence diminue (decay)
  → Belief toujours actif mais confiance 0.7
  
Tick 150 : Expiry_tick atteint
  → Belief devient "suspect", confidence max 0.4
```

### 4.4 Contradiction

Si deux beliefs se contredisent :

```
OLD : "No water at (75, 75)"
NEW : Observe water at (75, 75)
ACTION : Discard OLD, create/confirm NEW with high confidence
```

---

## 5. Système de Besoins (Phase 3)

### 5.1 Calcul des besoins

À chaque tick, mettre à jour niveaux besoins basés sur state :

```csharp
public class NeedsState
{
    public float Hunger { get; set; }      // 0–100
    public float Thirst { get; set; }      // 0–100
    public float Fatigue { get; set; }     // 0–100
    public float SafetyNeed { get; set; }  // 0–1 (si danger perçu)
    public float SocialNeed { get; set; }  // 0–1 (loneliness)
    public float CuriosityDrive { get; set; } // 0–1 (exploration)
}
```

### 5.2 Algorithme de mise à jour

```
Hunger += consumption_per_tick
Hunger = Clamp(Hunger, 0, 100)

Thirst += dehydration_per_tick
Thirst = Clamp(Thirst, 0, 100)

Fatigue += activity_cost_this_tick
Fatigue = Clamp(Fatigue, 0, 100)

SafetyNeed = 0.0
FOR each threatening_agent in perception:
    threat_level = threat(agent_stats)
    SafetyNeed += threat_level
SafetyNeed = Clamp(SafetyNeed, 0, 1)

SocialNeed = 1.0 - (friends_nearby / total_known_agents)
CuriosityDrive = 1.0 - (explored_area / total_world_area)
```

### 5.3 Configuration

Par espèce/individu :

```json
{
  "species": "Human",
  "consumption_rate": 0.5,    // hunger per tick
  "dehydration_rate": 0.3,
  "base_metabolism": 0.1,     // fatigue per tick at rest
  "movement_cost": 0.2        // additional per m/tick
}
```

---

## 6. Génération d'Objectifs (Phase 3)

### 6.1 Responsabilité

Créer liste de **goals candidats** à partir des besoins.

### 6.2 Règles

```
IF Hunger > 60 :
    goals.Add(SeekFood(positions from beliefs))
    
IF Thirst > 60 :
    goals.Add(SeekWater(positions from beliefs))
    
IF Fatigue > 70 :
    goals.Add(Rest())
    
IF SafetyNeed > 0.5 :
    goals.Add(Flee(threat_agent))
    
IF SocialNeed > 0.7 :
    goals.Add(Socialize(nearby_agents))
    
IF CuriosityDrive > 0.3 AND not_in_combat :
    goals.Add(Explore(unknown_regions))
```

### 6.3 Factibilité

Pour chaque goal candidate, vérifier :

```
- Agent has necessary capability ?
- Target accessible (in beliefs) ?
- Estimated success rate > 0 ?
- Memeory doesn't contain recent failure ?
```

Si non → drop goal.

### 6.4 Priorité dynamique

```csharp
goal.priority = need_level * success_probability * urgency_factor
```

Goals classés par priorité descendante.

---

## 7. Évaluation d'Utilité (Phase 3)

### 7.1 Processus

Pour chaque goal, générer **action candidates** et scorer.

### 7.2 Génération des candidats

**Goal : SeekFood**
- Actions possibles :
  - MoveTo(position1) : meilleureemoire
  - MoveTo(position2) : mémoire ancienne
  - Explore() : pas de position
  - CommunicateFor(help) : demander aide

**Goal : SafetySocial**
- Actions :
  - Flee(threat)
  - GroupWith(nearby_allies)
  - Hide()

### 7.3 Fonction de scoring

```csharp
public float EvaluateUtility(Goal goal, Action action, Agent agent)
{
    float benefit = CalculateBenefit(goal, action);
    float cost = CalculateCost(action, agent.state);
    float risk = CalculateRisk(action, agent.beliefs);
    float confidence = GetBeliefConfidence(action.target);
    
    // Traits modifient calcul
    float personality_modifier = agent.GetPersonalityModifier(action);
    
    // Urgency bonus
    float urgency = goal.priority;
    
    // Formula
    float utility = (benefit - cost - risk) 
                    * confidence 
                    + urgency 
                    + personality_modifier;
    
    return MathF.Max(0, utility);
}
```

### 7.4 Exemple détaillé

**Contexte :**
```
Hunger = 75
Nearby food observed : position (85, 42)
Agent beliefs : food confidence 0.9
Distance : 20m
Agent traits : aggression 0.3, sociability 0.2, prudence 0.7
```

**Candidats :**

A) Move to food

```
benefit = 60 (hunger satisfaction)
cost = 5 (20m / 4 speed = 5 ticks energy)
risk = 2 (no known threats)
confidence = 0.9
urgency = 0.7
personality = -0.1 (prudence slightly negative for direct approach)

utility = (60 - 5 - 2) * 0.9 + 0.7 - 0.1 = 53 * 0.9 + 0.6 = 47.7 + 0.6 = 48.3
```

B) Ask nearby agent for food

```
benefit = 40 (less guaranteed)
cost = 2 (communication, short)
risk = 5 (social risk, rejection)
confidence = 0.3 (don't know if they'll help)
urgency = 0.7
personality = -0.8 (low sociability, negative modifier)

utility = (40 - 2 - 5) * 0.3 + 0.7 - 0.8 = 33 * 0.3 - 0.1 = 9.9 - 0.1 = 9.8
```

C) Attack nearby agent for food (desperate)

```
benefit = 80 (food guaranteed)
cost = 15 (combat damage, retaliation)
risk = 60 (high combat uncertainty)
confidence = 1.0 (target visible)
urgency = 0.7
personality = -0.4 (low aggression penalty)

utility = (80 - 15 - 60) * 1.0 + 0.7 - 0.4 = 5 + 0.3 = 5.3
```

**Sélection** : A (48.3) > B (9.8) > C (5.3)

→ L'agent choisit Move to food

---

## 8. Sélection d'Intention et d'Action (Phase 4)

### 8.1 Formation d'intention

Quand une action A est sélectionnée (utility maximale) :

```csharp
public class Intention
{
    public Goal Goal { get; set; }
    public Action Action { get; set; }
    public object[] Parameters { get; set; }     // target position, agent id, etc
    public float CommitmentLevel { get; set; }   // 0–1, how firm
    public List<InterruptionCondition> InterruptionConditions { get; set; }
}
```

### 8.2 Interruption logique

L'intention peut être interrompue si :

```
- New perception makes action infeasible
- New urgent need arises (safety threat)
- Action fails (target unreachable)
- Goal completed
```

### 8.3 Exécution d'action (Phase 4)

Une fois intention formed :

```
1. Start action (mark state = Executing)
2. Each tick: Update progress, apply effects
3. Complete or Cancel/Fail
4. Generate events
5. Update beliefs/memory based on result
```

---

## 9. Protocole de Communication (Phase 5)

### 9.1 Structure de message

```csharp
public class Message
{
    public string MessageId { get; set; }
    public string SenderId { get; set; }
    public string ReceiverId { get; set; }           // peut être broadcast
    public MessageType Type { get; set; }            // Information, Request, Response, Announcement, Warning, Trading, Acknowledgement
    public string Content { get; set; }              // JSON payload
    public ulong SendTick { get; set; }
    public float ConfidenceLevel { get; set; }       // sender's confidence in content
    public string Source { get; set; }               // "direct", "hearsay", "memory"
}

public enum MessageType
{
    Information,     // "I saw food at (100, 50)"
    Request,         // "Can you help me hunt ?"
    Response,        // "Yes" / "No"
    Announcement,    // "Group forming, join us !"
    Warning,         // "Danger nearby !"
    Trading,         // "Offer resources X for Y"
    Acknowledgement  // "Message received"
}
```

### 9.2 Portée et distribution

```csharp
// Message sending
float communicationRadius = 20;  // configurable

FOR each agent in world:
    IF Vector2.Distance(sender.pos, agent.pos) <= communicationRadius:
        agent.messageQueue.Enqueue(message)
```

### 9.3 Coûts de communication

```csharp
// Energy cost to send
communication_cost = message_length * energy_per_char
agent.energy -= communication_cost

// Attention cost to process
processing_cost = 0.5 * energy
recipient.energy -= processing_cost
```

### 9.4 Intégration des croyances

Quand message reçu :

```csharp
// Évaluer sender crédibilité
float senderTrust = agent.relationships[senderId].trustLevel;
float messageConfidence = message.confidenceLevel * senderTrust;

// Ajouter belief basé sur message
agent.beliefs.Add(
    fact: message.content,
    confidence: messageConfidence,
    source: Communication,
    fromAgent: senderId
);
```

---

## 10. Système de Groupes (Phase 6)

### 10.1 Structure de groupe

```csharp
public class Group
{
    public string GroupId { get; set; }
    public string Name { get; set; }
    public string LeaderId { get; set; }
    public string Objective { get; set; }           // Hunting, Gathering, Defense
    public List<string> MemberIds { get; set; }
    public Dictionary<string, string> Roles { get; set; }  // memberId → role
    public Dictionary<string, float> SharedResources { get; set; }  // resourceType → quantity
    public ulong CreatedTick { get; set; }
    public GroupStatus Status { get; set; }
}
```

### 10.2 Dynamique d'appartenance

**Adhésion :**

```
Agent A creates group :
    group = new Group(...)
    leader = A
    members = [A]
    
Agent B wants to join :
    B.SendMessage(A, "Can I join your group ?")
    A receives, evaluates:
        - Trust in B ?
        - Need for B's skills ?
    A accepts :
        group.members.Add(B)
        B.groupId = group.id
```

**Départ :**

```
Agent B wants to leave :
    B.LeaveGroup()
    group.members.Remove(B)
    IF B.isLeader :
        Select new leader (highest skill/trust)
```

### 10.3 Décision partagée

Groupe doit décider action (chasse ensemble, migrer, etc) :

```
// Democratic voting
goals = GenerateGoalsForGroup()

FOR each goal:
    scores = []
    FOR each member:
        member_score = member.EvaluateUtility(goal)
        scores.Add(member_score)
    
    average_score = scores.Average()
    
// Select highest scoring goal
bestGoal = goals.MaxBy(g => AverageScore(g))

// Execute together
FOR each member:
    member.AddGoal(bestGoal)
```

### 10.4 Dissolution

Groupe dissolves si :

```
- Objective achieved
- Too few members (< 2)
- Leader dies
- Resource exhaustion
```

---

## 11. Relations (Phase 6)

### 11.1 Réseau de confiance

```csharp
public class Relationship
{
    public string AgentId { get; set; }
    public string TargetAgentId { get; set; }
    public float TrustLevel { get; set; }           // 0–1
    public int InteractionCount { get; set; }
    public ulong LastInteractionTick { get; set; }
    public RelationshipType Type { get; set; }
}

public enum RelationshipType
{
    Neutral,
    Ally,
    Friend,
    Enemy,
    UnknownBeyondSignal
}
```

### 11.2 Évolution de la confiance

```csharp
// Positive interaction
trust += 0.1;
trust = MathF.Min(1.0, trust);

// Negative interaction
trust -= 0.2;
trust = MathF.Max(0.0, trust);

// Decay if no interaction
elapsedTicks = currentTick - lastInteractionTick;
trust *= MathF.Exp(-0.001 * elapsedTicks);
```

### 11.3 Utilisation

Trust modifie :

```
- Utility of cooperative actions
- Belief confidence in information from them
- Likelihood of helping
- Trade willingness
```

---

## 12. Système de Ressources (Phase 7)

### 12.1 Modèle de ressource

```csharp
public class Resource
{
    public string ResourceId { get; set; }
    public ResourceType Type { get; set; }    // Food, Water, Wood, Mineral
    public Vector2 Position { get; set; }
    public float Quantity { get; set; }       // current
    public float Capacity { get; set; }       // max
    public float RegenerationRate { get; set; } // per tick
    public float DegradationRate { get; set; }  // if unused
    public ulong LastHarvestedTick { get; set; }
}
```

### 12.2 Logique de mise à jour

```csharp
// Regeneration
quantity += regenerationRate;
quantity = MathF.Min(quantity, capacity);

// Degradation if stale
ticksSinceHarvest = currentTick - lastHarvestedTick;
IF ticksSinceHarvest > 100:
    degradation = degradationRate * (ticksSinceHarvest - 100);
    quantity -= degradation;
    quantity = MathF.Max(0, quantity);

// Consumption
IF agent.Eat(this):
    quantity -= consumedAmount;
    quantity = MathF.Max(0, quantity);
    lastHarvestedTick = currentTick;
```

### 12.3 Durabilité

Une ressource est viable si :

```
regenerationRate >= average_consumption_rate
```

---

## 13. Événements d'Environnement (Phase 7)

### 13.1 Types d'événements

```csharp
public class EnvironmentEvent
{
    public string EventId { get; set; }
    public EventType Type { get; set; }
    public ulong Tick { get; set; }
    public Vector2 Epicenter { get; set; }
    public float Radius { get; set; }
    public object Data { get; set; }
}

public enum EventType
{
    Drought,        // ressources water diminuent
    Abundance,      // ressources bonus
    Epidemic,       // agents santé ↓
    Earthquake,     // resources disrupted
    SeasonChange,   // shift season
    DayNightCycle   // affect perception/activity
}
```

### 13.2 Application des effets

```
OnEventOccurs(event) :
    FOR each agent in event.radius:
        agent.HandleEvent(event)
        
    FOR each resource in event.radius:
        resource.HandleEvent(event)
```

---

## 14. Intégration de l'Observabilité Partielle (Phase 8)

### 14.1 Vérité du monde vs croyances de l'agent

```
Monde réel :
├── Agent A actually at (50, 50)
├── Food actually at (100, 100)
└── Agent B actually at (80, 60)

Agent A's mental model :
├── "I am at (50, 50)" confidence 1.0
├── "Food at (100, 100)" confidence 0.8
└── "Agent B never seen"
```

### 14.2 Conséquence sur la décision

Agent A acts based on **its beliefs**, not world truth :

```
Agent A believes (mistakenly) :
    Food at (120, 100) [old memory]

Agent A moves to (120, 100)

Result :
    Arrives, finds nothing
    Updates beliefs : "That location is empty"
    Behavior changes based on false discovery
```

### 14.3 Phénomènes émergents

```
Agent A : believes X
Agent B : believes Y (different)
Agent C : believes Z (yet different)

Result : 3 agents can make 3 different decisions in identical situation
         → social complexity emerges
```

---

## 15. Considérations de Mise à l'Échelle (Phase 9)

### 15.1 Optimisation de la perception

Use **spatial grid** :

```
// Pre-compute spatial partitioning
grid.cellSize = 50;  // m
FOR each entity in world :
    cellId = grid.GetCellId(entity.position)
    grid.cells[cellId].entities.Add(entity)

// Fast perception query
FOR each agent :
    cell = grid.GetCell(agent.position)
    FOR nearby_cell in [center + 8 neighbors] :
        FOR entity in nearby_cell.entities :
            IF distance <= perception_radius :
                agent.Perceive(entity)
```

### 15.2 Fréquence de décision LOD

```csharp
// Not all agents decide every tick

FOR each agent:
    decisionFrequency = 1 / LOD
    IF (currentTick % decisionFrequency) == 0:
        agent.UpdateDecision()
    ELSE:
        agent.ContinueCurrentAction()
```

Agents éloignés : LOD = 4 (décision tous les 4 ticks)
Agents dans la zone du joueur : LOD = 1 (décision à chaque tick)

### 15.3 Objectifs de profilage

```
Target performance with 1000 agents :
  - Perception : < 10ms
  - Decision : < 20ms
  - Action execution : < 5ms
  - Communication : < 5ms
  → Total per tick : < 50ms (20 ticks/sec maintenu)
```

---

## 16. Précision Save/Load (Phase 12)

### 16.1 Format de sauvegarde

SQLite avec schéma complet :

```sql
-- Schema v1.0
simulation_state:
  - tick (PRIMARY KEY)
  - seed
  - world_time_minutes
  - current_season
  
agents:
  - id, name, species, position, health, energy, hunger, thirst, ...
  
agent_beliefs:
  - id, agentId, fact, confidence, timestamp, expiryTick, source
  
agent_memories:
  - id, agentId, type, data, timestamp, decayRate, confidence
  
agent_relationships:
  - id, agentId, targetAgentId, trustLevel, lastInteraction, type
  
groups:
  - id, name, leaderId, objective, createdTick, status
  
resources:
  - id, type, position, quantity, capacity, regenerationRate, lastHarvested
  
obstacles:
  - id, position, size, type, passable
  
events_log:
  - id, tick, type, data
```

### 16.2 Algorithme de chargement

```
1. Load simulation_state → Tick, Seed
2. Restore world properties
3. Load and recreate all agents (with state)
4. Load beliefs, memories, relationships
5. Load resources, obstacles
6. Verify integrity
7. Resume from next tick
```

### 16.3 Vérification bit-parfaite

```
Save at tick 1000
   ↓
Resume at tick 1000
   ↓
Run 100 ticks
   ↓
Compare outputs with:
   Tick 1000 + Run 100 ticks (without save)
   ↓
Should be identical (PRNG seed stable)
```

---

## 17. Métriques Analyser V2 (Phase 11)

### 17.1 Nouvelles catégories

```
Cognitive Diversity:
  - Distribution of beliefs across population
  - Disagreement metrics
  - Belief confidence variance

Information Propagation:
  - Message trees (who told whom)
  - Information diffusion speed
  - Rumor accuracy degradation

Social Complexity:
  - Trust network connectivity
  - Clustering coefficient
  - Centrality measures

Goal Convergence:
  - Do agents share goals ?
  - Goal diversity over time
  - Goal success rate by type

Feedback Loop Detection:
  - Identify cycles action→consequence→decision
  - Measure loop strength/feedback magnitude
  - Track system stability

Group Dynamics:
  - Group formation frequency
  - Avg group lifetime
  - Member turnover rate
  - Group effectiveness (goal achievement %)

Resource Sustainability:
  - Resource vs consumption ratio
  - Criticality points (near exhaustion)
  - Recovery time after collapse
```

---

## 18. Synthèse

Cette spécification couvre :

- ✅ Perception + Memory (Phase 2)
- ✅ Beliefs + Needs (Phase 2-3)
- ✅ Goals + Utility (Phase 3)
- ✅ Intentions + Actions (Phase 4)
- ✅ Communication (Phase 5)
- ✅ Groups + Relationships (Phase 6)
- ✅ Resources + Environment (Phase 7)
- ✅ Partial Observability (Phase 8)
- ✅ Scaling strategies (Phase 9)
- ✅ Save/Load (Phase 12)
- ✅ Analyzer metrics (Phase 11)

Chaque système est **précis, testable, et mesurable**.

