# Architecture V2

## 1. Vue générale

```text
┌─────────────────────────────────────────────────────────────┐
│                 Simulation Core v2 (.NET 10)                │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Engine                                               │  │
│  │  ├── Perception System                              │  │
│  │  ├── Memory System                                  │  │
│  │  ├── Belief Store                                   │  │
│  │  ├── Needs System                                   │  │
│  │  ├── Goal System                                    │  │
│  │  ├── Decision System                                │  │
│  │  ├── Action System                                  │  │
│  │  ├── Communication System                           │  │
│  │  ├── Group System                                   │  │
│  │  ├── Relationship System                            │  │
│  │  ├── Resource System                                │  │
│  │  ├── Environment System                             │  │
│  │  └── Navigation System (Godot-based)               │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Persistence Layer                                    │  │
│  │  ├── SQLite Connection                              │  │
│  │  ├── State Serializer                               │  │
│  │  └── State Deserializer                             │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Transport Layer (WebSocket)                         │  │
│  │  ├── WorldSnapshot (agent, beliefs, groups, etc.)  │  │
│  │  └── ExternalEvent (new event types for BDI)       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                          ↓
                Transport (WebSocket)
                          ↓
        ┌─────────────────┼─────────────────┐
        ↓                 ↓                 ↓
   Analyzer            Web UI            Godot
   (ASP.NET)         (React+TS)          3D Renderer
                                         (Godot 4.7.2)
```

---

## 2. Modules principaux

### 2.1 Simulation.Core

**Responsabilité** : Moteur complet de simulation, zéro dépendance graphique.

```text
Simulation.Core/
├── World/
│   ├── World.cs (état global, tick counter)
│   ├── WorldState.cs (snapshot persistant)
│   └── WorldConfiguration.cs (paramètres)
│
├── Agents/
│   ├── Agent.cs (entité autonome)
│   ├── AgentState.cs (health, energy, position, etc.)
│   ├── Traits.cs (aggression, sociability, prudence, ambition)
│   └── Species.cs (définit capacités)
│
├── Perception/
│   ├── PerceptionSystem.cs (orchestre perception)
│   ├── Observation.cs (ce qu'on perçoit)
│   ├── PerceptionConfig.cs (radius, types détectables)
│   └── SpatialGrid.cs (optimisation O(1) queries)
│
├── Memory/
│   ├── MemorySystem.cs
│   ├── MemoryEntry.cs
│   ├── ObservationType.cs
│   └── MemoryDecay.cs (calculs exp decay)
│
├── Beliefs/
│   ├── BeliefStore.cs
│   ├── Belief.cs
│   ├── BeliefRevision.cs (update logic)
│   └── BeliefConfidence.cs
│
├── Needs/
│   ├── NeedsSystem.cs
│   ├── NeedsState.cs
│   └── NeedsConfiguration.cs
│
├── Goals/
│   ├── GoalSystem.cs
│   ├── Goal.cs
│   ├── GoalType.cs
│   └── GoalFactory.cs
│
├── Decision/
│   ├── DecisionSystem.cs (orchestre délibération)
│   ├── UtilityEvaluator.cs (scoring multi-critères)
│   ├── DecisionRecord.cs (trace complète)
│   └── DecisionStrategy.cs (plugins possibles)
│
├── Actions/
│   ├── ActionSystem.cs (exécution actions)
│   ├── Action.cs (base classe)
│   ├── Actions/ (implémentations)
│   │   ├── MoveToAction.cs
│   │   ├── EatAction.cs
│   │   ├── DrinkAction.cs
│   │   ├── RestAction.cs
│   │   ├── ExploreAction.cs
│   │   ├── GatherAction.cs
│   │   ├── CommunicateAction.cs
│   │   ├── TradeAction.cs
│   │   ├── AttackAction.cs
│   │   └── FleeAction.cs
│   └── ActionState.cs (enum: Pending, Executing, Complete, etc.)
│
├── Communication/
│   ├── CommunicationSystem.cs
│   ├── Message.cs
│   ├── MessageType.cs
│   ├── CommunicationProtocol.cs (spécification)
│   └── MessageQueue.cs
│
├── Groups/
│   ├── GroupSystem.cs
│   ├── Group.cs
│   ├── GroupMembership.cs
│   ├── GroupObjective.cs
│   └── GroupRole.cs
│
├── Relationships/
│   ├── RelationshipSystem.cs
│   ├── Relationship.cs
│   ├── TrustLevel.cs (0–1)
│   └── RelationshipType.cs
│
├── Resources/
│   ├── ResourceSystem.cs
│   ├── Resource.cs
│   ├── ResourceType.cs
│   ├── ResourceDeposit.cs
│   └── ResourceConfig.cs
│
├── Environment/
│   ├── EnvironmentSystem.cs
│   ├── EnvironmentEvent.cs
│   ├── EventType.cs
│   ├── Obstacle.cs
│   ├── SeasonCycle.cs
│   └── DayNightCycle.cs
│
├── Persistence/
│   ├── SQLiteConnection.cs
│   ├── StateSerializer.cs
│   ├── StateDeserializer.cs
│   ├── MigrationManager.cs (schéma versioning)
│   └── DatabaseSchema.cs
│
├── Transport/
│   ├── WorldSnapshot.cs (v2: inclure beliefs, goals, relations)
│   ├── ExternalEvent.cs (v2: nouveaux types)
│   ├── EventPublisher.cs
│   └── TransportDTO.cs
│
├── Navigation/
│   ├── NavigationService.cs (interface pour Godot pathfinding)
│   ├── Path.cs
│   └── MovementController.cs
│
└── Systems/
    └── SimulationLoop.cs (orchestre tous les systèmes)
```

### 2.2 Simulation.Console

**Responsabilité** : Exécutable de test, modes server, CLI, observabilité.

```text
Simulation.Console/
├── Program.cs (entry point)
├── Commands/
│   ├── ServeCommand.cs (--serve : WebSocket server)
│   ├── LoadCommand.cs (--load saveFile)
│   └── BenchmarkCommand.cs (--benchmark : perf tests)
└── Logging/
    └── ConsoleLogger.cs
```

### 2.3 Analyzer v2

**Responsabilité** : Ingestion événements, calcul métriques BDI, API REST.

```text
Analyzer/
├── Analyzer.Core/
│   ├── Metrics/
│   │   ├── CognitiveDiversityMetrics.cs
│   │   ├── InformationPropagationMetrics.cs
│   │   ├── SocialComplexityMetrics.cs
│   │   ├── GoalConvergenceMetrics.cs
│   │   ├── FeedbackLoopDetector.cs
│   │   ├── ResourceSustainabilityMetrics.cs
│   │   └── GroupDynamicsMetrics.cs
│   ├── RunStore.cs (agrégation incrémentale)
│   ├── TickSample.cs
│   ├── ExperienceData.cs
│   └── ComparisonEngine.cs
│
└── Analyzer.Service/
    ├── Program.cs (ASP.NET Kestrel)
    ├── Endpoints/
    │   ├── /health
    │   ├── /api/runs
    │   ├── /api/runs/{id}
    │   ├── /api/runs/{id}/export
    │   ├── /api/compare?a=X&b=Y
    │   ├── /api/beliefs/{agentId}
    │   ├── /api/relationships/{agentId}
    │   ├── /api/groups
    │   └── /api/emergent-phenomena
    ├── WebSocket/
    │   └── SimClient.cs
    └── Logging/
        └── AnalyzerLogger.cs
```

### 2.4 Web UI v2

**Responsabilité** : Rapports temps réel, visualisation BDI, debug tools.

```text
web-ui/
├── src/
│   ├── types.ts (DTO v2 : beliefs, goals, relations)
│   ├── config.ts (URLs, env)
│   ├── hooks/
│   │   ├── useSimulationSocket.ts (flux direct)
│   │   ├── useRuns.ts (API analyzer)
│   │   ├── useBeliefs.ts (beliefs agent)
│   │   ├── useRelationships.ts (trust networks)
│   │   └── useGroups.ts (group data)
│   ├── components/
│   │   ├── WorldView.tsx (canvas 2D agents)
│   │   ├── MetricsPanel.tsx (graphes population/émergence)
│   │   ├── BeliefInspector.tsx (beliefs agent)
│   │   ├── RelationshipGraph.tsx (trust network viz)
│   │   ├── GroupExplorer.tsx (groups visualization)
│   │   ├── CommunicationLog.tsx (message history)
│   │   └── RunComparison.tsx (compare 2 runs)
│   └── App.tsx
├── tsconfig.json
├── vite.config.ts
└── package.json
```

### 2.5 Godot Renderer v2

**Responsabilité** : Visualisation 3D, contrôle moteur, UI debug.

```text
godot-renderer/
├── project.godot
├── main.tscn (scène unique)
├── scripts/
│   ├── SimClient.cs (connection WS, state reconstruction)
│   ├── CameraController.cs (orbite, zoom, follow)
│   ├── Hud.cs (UI buttons, panels)
│   ├── AgentVisual.cs (capsule + color by action/health)
│   ├── ResourceVisual.cs (sphere + color by type)
│   ├── ObstacleVisual.cs (wall rendering)
│   └── DebugVisualization.cs (belief layers, trust network viz)
├── materials/
│   ├── AgentMaterial.gdshader
│   └── TerrainMaterial.gdshader
└── godot-renderer.csproj
```

---

## 3. Flux de données

### 3.1 Perception → Decision → Action

```
Tick N:

Agent.Perceive()
    └→ detects entities in radius
    └→ creates Observations[]
    
Agent.UpdateMemory()
    └→ applies decay to old entries
    └→ stores new observations
    
Agent.UpdateBeliefs()
    └→ revises beliefs based on observations
    └→ updates confidence/expiry
    
Agent.UpdateNeeds()
    └→ recalculates hunger, thirst, etc.
    
Agent.GenerateGoals()
    └→ creates candidate goals from needs
    
Agent.DecideAction()
    ├→ generates action candidates per goal
    ├→ evaluates utility each action
    ├→ selects highest utility
    └→ forms Intention
    
Agent.ExecuteAction()
    └→ applies effects
    └→ modifies World state
    └→ generates Events
    
World.ApplyConsequences()
    └→ updates resources, positions, etc.
    
Analyzer.ProcessEvents()
    └→ aggregates metrics
    
Transport.BroadcastSnapshot()
    └→ sends to connected clients
```

### 3.2 Réseau de communication

```
Agent A.SendMessage(Message m)
    └→ adds to CommunicationSystem.queue
    
CommunicationSystem.Process()
    └→ for each agent in radius:
        └→ agent.receiveMessage(m)
        └→ agent.updateBeliefs(m.content, confidence=m.confidence*trust)
        
Agent B.ProcessMessages()
    └→ integrates message as new belief
    └→ may trigger new goals
```

### 3.3 Flux de persistance

```
User calls: engine.Save("world.db")

StateSerializer:
    ├→ Serialize world.tick
    ├→ Serialize all agents (+ beliefs, memories, relationships)
    ├→ Serialize all resources
    ├→ Serialize all groups
    ├→ Serialize all obstacles
    └→ Store in SQLite
    
Later: engine.Load("world.db")

StateDeserializer:
    ├→ Restore world properties
    ├→ Recreate all agents
    ├→ Restore beliefs, memories, relationships
    ├→ Restore resources, groups, obstacles
    └→ Resume simulation (tick = loaded tick + 1)
```

---

## 4. Interfaces clés

### 4.1 IPerceptionSystem

```csharp
public interface IPerceptionSystem
{
    List<Observation> GetObservations(Agent agent);
    float GetPerceptionRadius(Agent agent);
    void UpdatePerceptions(World world);
}
```

### 4.2 IMemorySystem

```csharp
public interface IMemorySystem
{
    void StoreObservation(Agent agent, Observation obs);
    List<MemoryEntry> GetMemory(Agent agent, ObservationType type = null);
    void ApplyDecay(Agent agent, ulong currentTick);
}
```

### 4.3 IBeliefStore

```csharp
public interface IBeliefStore
{
    void UpdateBelief(Agent agent, Belief belief);
    Belief GetBelief(Agent agent, string fact);
    List<Belief> GetBeliefs(Agent agent, string domain = null);
    void ReviseBeliefs(Agent agent, Observation obs);
}
```

### 4.4 IDecisionSystem

```csharp
public interface IDecisionSystem
{
    Intention MakeDecision(Agent agent, List<Goal> goals);
    float EvaluateUtility(Agent agent, Goal goal, Action action);
    DecisionRecord GetLastDecision(Agent agent);
}
```

### 4.5 IActionSystem

```csharp
public interface IActionSystem
{
    void StartAction(Agent agent, Action action);
    void UpdateAction(Agent agent);
    void CompleteAction(Agent agent);
    void CancelAction(Agent agent, string reason);
}
```

### 4.6 ICommunicationSystem

```csharp
public interface ICommunicationSystem
{
    void SendMessage(Message message);
    List<Message> ReceiveMessages(Agent agent);
    void ProcessMessages(Agent agent, World world);
}
```

### 4.7 IResourceSystem

```csharp
public interface IResourceSystem
{
    Resource GetResourceAt(Vector2 position);
    void ConsumeResource(Resource resource, float amount);
    void RegenerateResources(World world);
}
```

---

## 5. Configuration et dépendances

### 5.1 Démarrage (Simulation.Console)

```csharp
// Composition root
var world = new World(worldConfig);

var perceptionSystem = new PerceptionSystem();
var memorySystem = new MemorySystem();
var beliefStore = new BeliefStore();
var needsSystem = new NeedsSystem();
var goalSystem = new GoalSystem();
var decisionSystem = new DecisionSystem(utilityEvaluator);
var actionSystem = new ActionSystem();
var communicationSystem = new CommunicationSystem();
var groupSystem = new GroupSystem();
var relationshipSystem = new RelationshipSystem();
var resourceSystem = new ResourceSystem();
var environmentSystem = new EnvironmentSystem();
var navigationService = new NavigationService(); // delegates to Godot

var engine = new SimulationEngine(
    world,
    perceptionSystem,
    memorySystem,
    beliefStore,
    needsSystem,
    goalSystem,
    decisionSystem,
    actionSystem,
    communicationSystem,
    groupSystem,
    relationshipSystem,
    resourceSystem,
    environmentSystem,
    navigationService
);

// Persistence
var persistence = new SQLitePersistence("simulation.db");
engine.SetPersistence(persistence);

// Transport
var wsServer = new WebSocketServer(5180);
engine.SetTransport(wsServer);

// Analyzer
var analyzer = new Analyzer();
wsServer.OnSnapshot += analyzer.ProcessSnapshot;

// Main loop
while (running)
{
    engine.Tick();
    await Task.Delay(100);  // 10 ticks/sec
}
```

---

## 6. Séparation des responsabilités

| Module | Responsabilité | Dépend de |
|--------|-----------------|-----------|
| **Perception** | Déterminer observables | World |
| **Memory** | Conserver historique | Perception |
| **BeliefStore** | Représentation mentale | Memory |
| **Needs** | Calculer tensions | Agent.State |
| **Goals** | Créer objectifs | Needs, Beliefs |
| **Decision** | Choisir action | Goals, Beliefs, Traits |
| **Actions** | Exécuter intentions | Agent.State, World |
| **Communication** | Échanger messages | SpatialGrid |
| **Groups** | Agrégation agents | Communication |
| **Relationships** | Trust networks | Communication, Interactions |
| **Resources** | Éléments consommables | World, Environment |
| **Environment** | Cadre simulation | World |
| **Navigation** | Pathfinding | World |
| **Persistence** | Save/Load | All systems |
| **Transport** | WebSocket broadcast | All systems |

---

## 7. Extensibilité

### 7.1 Plugins de stratégie décisionnelle

Possible remplacer `UtilityEvaluator` par autre implémentation :

```csharp
public interface IDecisionStrategy
{
    Intention SelectAction(Agent agent, List<(Goal goal, List<Action> actions)> candidates);
}

// Pluggable strategies:
- UtilityAIStrategy (v2 default)
- BehaviorTreeStrategy (v3)
- RuleBasedStrategy (testing)
```

### 7.2 Types d'actions extensibles

```csharp
public abstract class Action
{
    public abstract bool CanExecute(Agent agent, World world);
    public abstract void Execute(Agent agent, World world);
    public abstract void Update(Agent agent, World world);
}

// Easy to add new actions:
public class TradeAction : Action { ... }
public class TeachAction : Action { ... }
public class BuildAction : Action { ... }
```

### 7.3 Nouveaux types d'événements

```csharp
// Add to EventPublisher:
public event Action<CustomEvent> OnCustomEvent;

// Analyzer subscribes:
wsServer.OnEvent += analyzer.ProcessEvent;
```

---

## 8. Considérations de performance

### 8.1 Grille spatiale

```csharp
// Pre-compute O(1) nearby entity queries
public class SpatialGrid
{
    public Dictionary<Vector2Int, List<Agent>> cells;
    
    public List<Agent> GetNearby(Vector2 position, float radius)
    {
        // Only check ~9 cells, not all 1000 agents
        return GetCellsInRadius(position, radius)
            .SelectMany(c => c.Value)
            .Where(a => Vector2.Distance(a.pos, position) <= radius)
            .ToList();
    }
}
```

### 8.2 Fréquence de mise à jour LOD

```csharp
// Not every agent decides every tick
public class DecisionLOD
{
    public void UpdateAgents(List<Agent> agents)
    {
        foreach (var agent in agents)
        {
            if (currentTick % agent.decisionFrequency == 0)
            {
                agent.UpdateDecision();
            }
            else
            {
                agent.ContinueCurrentAction();
            }
        }
    }
}
```

### 8.3 Regroupement de messages

```csharp
// Collect messages, process in batch
public class CommunicationBatch
{
    public void ProcessBatch(List<Message> messages, World world)
    {
        var byRecipient = messages.GroupBy(m => m.ReceiverId);
        
        Parallel.ForEach(byRecipient, group =>
        {
            var agent = world.GetAgent(group.Key);
            foreach (var msg in group)
            {
                agent.ReceiveMessage(msg);
            }
        });
    }
}
```

---

## 9. Stratégie de test

### 9.1 Tests unitaires par module

```
Tests/
├── Perception/
│   ├── PerceptionSystemTests.cs
│   └── ObservationTests.cs
├── Memory/
│   ├── MemorySystemTests.cs
│   └── DecayTests.cs
├── Beliefs/
│   ├── BeliefStoreTests.cs
│   └── BeliefRevisionTests.cs
├── Decision/
│   ├── UtilityEvaluatorTests.cs
│   └── DecisionSystemTests.cs
├── Actions/
│   ├── ActionSystemTests.cs
│   ├── MoveToActionTests.cs
│   └── EatActionTests.cs
├── Communication/
│   ├── CommunicationSystemTests.cs
│   └── ProtocolTests.cs
├── Groups/
│   ├── GroupSystemTests.cs
│   └── MembershipTests.cs
├── Resources/
│   ├── ResourceSystemTests.cs
│   └── RegenerationTests.cs
└── Integration/
    ├── FullCycleTests.cs
    └── PersistenceTests.cs
```

### 9.2 Benchmarks (Phase 9)

```
Benchmarks/
├── PerceptionBench.cs (spatial grid)
├── DecisionBench.cs (utility scoring)
├── CommunicationBench.cs (message processing)
└── FullSimulationBench.cs (agents: 50, 500, 1000)
```

---

## 10. Documentation par module

Chaque module a sa propre spécification (cf. docs/V2/):

- `01-VISION.md` — Vue d'ensemble
- `02-CONCEPTUAL-MODEL.md` — Entités et relations
- `03-V2-SPECIFICATION.md` — Comportements formels
- `04-ARCHITECTURE.md` (ce fichier) — Disposition des modules
- `05-AGENTS-BDI.md` — Internes des agents
- `06-DECISION-SYSTEM.md` — Fonctions d'utilité
- `07-COMMUNICATION-PROTOCOL.md` — Spécifications des messages
- `08-PERSISTENCE.md` — Schéma SQLite
- `09-ANALYZER-V2.md` — Détails des métriques
- Etc.

