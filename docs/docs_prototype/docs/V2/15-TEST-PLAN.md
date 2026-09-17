# Plan de test V2 — Couverture ≥80%

## 1. Stratégie générale

**Objectif** : 80%+ couverture de code pour tous les modules critiques.

**Outils** :
- **Tests unitaires** : xUnit + Moq (C#)
- **Couverture** : dotnet test --collect:"XPlat Code Coverage"
- **Tests d'intégration** : scénarios de cycle complet
- **Tests de régression** : sauvegarde/chargement bit-parfait

---

## 2. Modules testables et couverture cible

| Module | Type | Tests | Coverage |
|--------|------|-------|----------|
| PerceptionSystem | Unit | 12 | 85% |
| MemorySystem | Unit | 10 | 80% |
| BeliefStore | Unit | 14 | 85% |
| NeedsSystem | Unit | 8 | 80% |
| GoalSystem | Unit | 12 | 82% |
| UtilityEvaluator | Unit | 15 | 85% |
| DecisionSystem | Unit | 10 | 80% |
| ActionSystem | Unit | 20 | 85% |
| CommunicationSystem | Unit | 16 | 85% |
| GroupSystem | Unit | 12 | 80% |
| ResourceSystem | Unit | 10 | 80% |
| EnvironmentSystem | Unit | 8 | 80% |
| BeliefRevision | Unit | 10 | 85% |
| **Simulation.Core** | **Overall** | **~160** | **83%** |
| Analyzer.Metrics | Unit | 20 | 80% |
| **Analyzer** | **Overall** | **~40** | **80%** |
| Web UI Components | Jest | 30 | 80% |
| **Web UI** | **Overall** | **~40** | **80%** |
| **Total** | | **~240** | **≥80%** |

---

## 3. Tests unitaires par système

### 3.1 Tests de PerceptionSystem

```csharp
[TestClass]
public class PerceptionSystemTests
{
    private PerceptionSystem _perception;
    private Mock<World> _world;
    private Mock<ISpatialGrid> _spatialGrid;
    
    [TestInitialize]
    public void Setup()
    {
        _perception = new PerceptionSystem { SensorRadius = 50, Accuracy = 1.0f };
        _spatialGrid = new Mock<ISpatialGrid>();
        _world = new Mock<World>();
        _world.Setup(w => w.SpatialGrid).Returns(_spatialGrid.Object);
    }
    
    [TestMethod]
    public void Perceive_EmptyGrid_ReturnsEmpty()
    {
        var agent = new Agent { Position = new Vector2(0, 0) };
        _spatialGrid.Setup(g => g.QueryRadius(It.IsAny<Vector2>(), It.IsAny<float>()))
            .Returns(new List<Entity>());
        
        var result = _perception.Perceive(agent, _world.Object, 0);
        
        Assert.AreEqual(0, result.Count);
    }
    
    [TestMethod]
    public void Perceive_NearbyAgents_ReturnsObservations()
    {
        var agent = new Agent { Position = new Vector2(0, 0) };
        var target = new Agent { Id = "target1", Position = new Vector2(10, 0) };
        
        _spatialGrid.Setup(g => g.QueryRadius(It.IsAny<Vector2>(), 50))
            .Returns(new List<Entity> { target });
        
        var result = _perception.Perceive(agent, _world.Object, 100);
        
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("target1", result[0].EntityId);
        Assert.IsTrue(result[0].Confidence > 0.8f);  // Close, high confidence
    }
    
    [TestMethod]
    public void Perceive_OutOfRadius_NoObservation()
    {
        var agent = new Agent { Position = new Vector2(0, 0) };
        var far = new Agent { Position = new Vector2(100, 0) };
        
        _spatialGrid.Setup(g => g.QueryRadius(It.IsAny<Vector2>(), 50))
            .Returns(new List<Entity>());  // Far entity not returned
        
        var result = _perception.Perceive(agent, _world.Object, 0);
        
        Assert.AreEqual(0, result.Count);
    }
    
    [TestMethod]
    public void Perceive_InaccuracyRoll_CanMissTarget()
    {
        _perception.Accuracy = 0.5f;  // 50% accuracy
        var agent = new Agent { Position = new Vector2(0, 0) };
        var target = new Agent { Position = new Vector2(10, 0) };
        
        _spatialGrid.Setup(g => g.QueryRadius(It.IsAny<Vector2>(), 50))
            .Returns(new List<Entity> { target });
        
        // Run multiple times
        var results = Enumerable.Range(0, 20)
            .Select(_ => _perception.Perceive(agent, _world.Object, 0).Count)
            .ToList();
        
        // Some should be 0 (missed), some should be 1 (detected)
        Assert.IsTrue(results.Any(c => c == 0));
        Assert.IsTrue(results.Any(c => c == 1));
    }
    
    [TestMethod]
    public void Perceive_ConfidenceByDistance()
    {
        var agent = new Agent { Position = new Vector2(0, 0) };
        var close = new Agent { Id = "close", Position = new Vector2(10, 0) };
        var far = new Agent { Id = "far", Position = new Vector2(40, 0) };
        
        _spatialGrid.Setup(g => g.QueryRadius(It.IsAny<Vector2>(), 50))
            .Returns(new List<Entity> { close, far });
        
        var result = _perception.Perceive(agent, _world.Object, 0);
        
        var closeObs = result.First(o => o.EntityId == "close");
        var farObs = result.First(o => o.EntityId == "far");
        
        Assert.IsTrue(closeObs.Confidence > farObs.Confidence);
    }
    
    // Additional tests...
    // - ExtractAttributes (resource vs agent vs obstacle)
    // - MultipleEntities
    // - SpatialGridEdgeCases
}
```

### 3.2 Tests de MemorySystem

```csharp
[TestClass]
public class MemorySystemTests
{
    private MemorySystem _memory;
    
    [TestInitialize]
    public void Setup()
    {
        _memory = new MemorySystem { DecayRate = 0.05f, MaxMemorySize = 1000 };
    }
    
    [TestMethod]
    public void StoreObservation_AddsToQueue()
    {
        var obs = new Observation { EntityId = "test1", ObservedAt = 100 };
        _memory.StoreObservation(obs, 100);
        
        var recalled = _memory.Recall(100);
        Assert.AreEqual(1, recalled.Count);
    }
    
    [TestMethod]
    public void Decay_ExponentialFalloff()
    {
        var obs = new Observation { EntityId = "test1", ObservedAt = 0 };
        _memory.StoreObservation(obs, 0);
        
        var tick0 = _memory.Recall(0)[0].Salience;       // exp(0) = 1.0
        var tick100 = _memory.Recall(100)[0].Salience;   // exp(-0.05*100) ≈ 0.006
        var tick50 = _memory.Recall(50)[0].Salience;     // exp(-0.05*50) ≈ 0.08
        
        Assert.IsTrue(tick0 > tick50);
        Assert.IsTrue(tick50 > tick100);
    }
    
    [TestMethod]
    public void Decay_DeadMemoriesFiltered()
    {
        var obs = new Observation { EntityId = "test1", ObservedAt = 0 };
        _memory.StoreObservation(obs, 0);
        
        var recent = _memory.Recall(100);
        var ancient = _memory.Recall(10000);  // Very old
        
        Assert.AreEqual(1, recent.Count);
        Assert.AreEqual(0, ancient.Count);  // Filtered out (salience < 0.01)
    }
    
    [TestMethod]
    public void MaxMemorySize_TrimsOldest()
    {
        for (int i = 0; i < 1500; i++)
        {
            var obs = new Observation { EntityId = $"obs{i}", ObservedAt = (ulong)i };
            _memory.StoreObservation(obs, (ulong)i);
        }
        
        var recalled = _memory.Recall(1500);
        Assert.IsTrue(recalled.Count <= 1000);
        
        // Oldest should be gone
        Assert.IsFalse(recalled.Any(r => r.ObservationData.EntityId == "obs0"));
    }
    
    [TestMethod]
    public void RecallByCategory_FiltersCorrectly()
    {
        var obs1 = new Observation { EntityId = "obs1", ObservedAt = 0 };
        var entry = new MemoryEntry { 
            ObservationData = obs1, 
            Category = MemoryCategory.Observation,
            StoredAt = 0
        };
        
        _memory.StoreObservation(obs1, 0);
        
        var observations = _memory.Recall(100, MemoryCategory.Observation);
        var interactions = _memory.Recall(100, MemoryCategory.Interaction);
        
        Assert.AreEqual(1, observations.Count);
        Assert.AreEqual(0, interactions.Count);
    }
    
    // Additional tests...
    // - CategoryTracking
    // - SalienceCalcuation
    // - RecallPerformance (large dataset)
}
```

### 3.3 Tests de BeliefStore

```csharp
[TestClass]
public class BeliefStoreTests
{
    private BeliefStore _beliefs;
    
    [TestInitialize]
    public void Setup()
    {
        _beliefs = new BeliefStore();
    }
    
    [TestMethod]
    public void AddBelief_Stores()
    {
        var belief = new Belief
        {
            Id = "b1",
            Fact = new Fact { Id = "food_at_60_80", Subject = "Food", Predicate = "position" },
            Confidence = 0.9f,
            Source = "observation"
        };
        
        _beliefs.AddOrUpdateBelief(belief, 100);
        
        Assert.IsTrue(_beliefs.Believes("food_at_60_80"));
    }
    
    [TestMethod]
    public void ConflictingBeliefs_LowerConfidence()
    {
        var existing = new Belief
        {
            Fact = new Fact { Id = "bob_status", Value = "resting" },
            Confidence = 0.8f
        };
        _beliefs.AddOrUpdateBelief(existing, 50);
        
        var conflicting = new Belief
        {
            Fact = new Fact { Id = "bob_status", Value = "moving" },  // Conflict!
            Confidence = 0.7f
        };
        _beliefs.AddOrUpdateBelief(conflicting, 100);
        
        var updated = _beliefs.GetBeliefs().First(b => b.Fact.Id == "bob_status");
        Assert.IsTrue(updated.Confidence < 0.8f);  // Confidence lowered
    }
    
    [TestMethod]
    public void AlignedBeliefs_IncreaseConfidence()
    {
        var existing = new Belief
        {
            Fact = new Fact { Id = "food_location", Value = "northeast" },
            Confidence = 0.6f
        };
        _beliefs.AddOrUpdateBelief(existing, 50);
        
        var aligned = new Belief
        {
            Fact = new Fact { Id = "food_location", Value = "northeast" },
            Confidence = 0.8f,
            Source = "communication"
        };
        _beliefs.AddOrUpdateBelief(aligned, 100);
        
        var updated = _beliefs.GetBeliefs().First(b => b.Fact.Id == "food_location");
        Assert.IsTrue(updated.Confidence > 0.6f);
    }
    
    [TestMethod]
    public void BeliefExpiry_CapConfidence()
    {
        var belief = new Belief
        {
            Fact = new Fact { Id = "old_fact", Value = "obsolete" },
            Confidence = 0.9f,
            ExpiryTick = 100
        };
        _beliefs.AddOrUpdateBelief(belief, 50);
        
        // Check at current tick > expiry
        var expired = _beliefs.GetBeliefs().First(b => b.Fact.Id == "old_fact");
        // After expiry, confidence should be capped
        Assert.IsTrue(expired.Confidence <= 0.4f || expired.ExpiryTick == null);
    }
    
    // Additional tests...
    // - MultipleBeliefs
    // - SourceTracking
    // - BeliefRetrieval
}
```

### 3.4 Tests de GoalSystem

```csharp
[TestClass]
public class GoalSystemTests
{
    private GoalSystem _goals;
    private Mock<Agent> _agent;
    
    [TestInitialize]
    public void Setup()
    {
        _agent = new Mock<Agent>();
        _goals = new GoalSystem(_agent.Object);
    }
    
    [TestMethod]
    public void GenerateGoals_FromUnmetNeeds()
    {
        var needs = new List<Need>
        {
            new Need { Id = "Hunger", Level = 75 },
            new Need { Id = "Thirst", Level = 90 }
        };
        
        var goals = _goals.GenerateGoals(needs);
        
        Assert.IsTrue(goals.Any(g => g.Type == "Eat"));
        Assert.IsTrue(goals.Any(g => g.Type == "Drink"));
    }
    
    [TestMethod]
    public void FilterFeasibleGoals_RemovesImpossible()
    {
        var goals = new List<Goal>
        {
            new Goal { Id = "eat", Type = "Eat", Feasibility = 1.0f },
            new Goal { Id = "fly", Type = "Fly", Feasibility = 0.0f }
        };
        
        var feasible = _goals.FilterFeasible(goals);
        
        Assert.AreEqual(1, feasible.Count);
        Assert.AreEqual("eat", feasible[0].Id);
    }
    
    [TestMethod]
    public void AssignPriorities_ByNeed()
    {
        var goals = new List<Goal>
        {
            new Goal { Id = "eat", Need = "Hunger", Urgency = 0.8f },
            new Goal { Id = "explore", Need = "Curiosity", Urgency = 0.2f }
        };
        
        var prioritized = _goals.AssignPriorities(goals);
        
        Assert.AreEqual("eat", prioritized[0].Id);  // Higher urgency first
        Assert.AreEqual("explore", prioritized[1].Id);
    }
    
    // Additional tests...
}
```

### 3.5 Tests de UtilityEvaluator

```csharp
[TestClass]
public class UtilityEvaluatorTests
{
    private UtilityEvaluator _evaluator;
    private Mock<Agent> _agent;
    
    [TestInitialize]
    public void Setup()
    {
        _evaluator = new UtilityEvaluator();
        _agent = new Mock<Agent>();
        _agent.Setup(a => a.Traits).Returns(new Traits { Bravery = 1.0f });
        _agent.Setup(a => a.Energy).Returns(50);
    }
    
    [TestMethod]
    public void EvaluateUtility_ComponentCalculation()
    {
        var action = new Action
        {
            Type = "Eat",
            Benefit = 20,
            Cost = 3,
            Risk = 1
        };
        
        var confidence = 0.95f;
        var utility = _evaluator.Evaluate(_agent.Object, action, confidence, null);
        
        // (20 - 3 - 1) * 0.95 * personality = expected
        Assert.IsTrue(utility > 0);
    }
    
    [TestMethod]
    public void UtilityComparison_HigherIsPreferred()
    {
        var eatAction = new Action { Type = "Eat", Benefit = 20, Cost = 3, Risk = 1 };
        var restAction = new Action { Type = "Rest", Benefit = 5, Cost = 0, Risk = 0 };
        
        var eat_score = _evaluator.Evaluate(_agent.Object, eatAction, 0.95f, null);
        var rest_score = _evaluator.Evaluate(_agent.Object, restAction, 1.0f, null);
        
        Assert.IsTrue(eat_score > rest_score);
    }
    
    [TestMethod]
    public void TraitModifier_AffectsUtility()
    {
        var brave_agent = new Mock<Agent>();
        brave_agent.Setup(a => a.Traits).Returns(new Traits { Bravery = 1.5f });
        
        var cautious_agent = new Mock<Agent>();
        cautious_agent.Setup(a => a.Traits).Returns(new Traits { Bravery = 0.5f });
        
        var risky_action = new Action { Type = "Charge", Benefit = 30, Cost = 10, Risk = 20 };
        
        var brave_score = _evaluator.Evaluate(brave_agent.Object, risky_action, 1.0f, null);
        var cautious_score = _evaluator.Evaluate(cautious_agent.Object, risky_action, 1.0f, null);
        
        Assert.IsTrue(brave_score > cautious_score);  // Brave takes more risk
    }
    
    // Additional tests...
}
```

---

## 4. Tests d'intégration

### 4.1 Cycle BDI complet

```csharp
[TestClass]
public class BDICycleIntegrationTests
{
    private World _world;
    private Agent _agent;
    
    [TestInitialize]
    public void Setup()
    {
        _world = new World();
        _agent = new Agent { Id = "alice", Position = new Vector2(50, 50) };
        _world.AddAgent(_agent);
    }
    
    [TestMethod]
    public void FullCycle_PerceptionToAction()
    {
        // Setup world
        var food = new Resource { Type = "Food", Position = new Vector2(60, 60), Quantity = 50 };
        _world.AddResource(food);
        
        // Set agent hunger high
        _agent.Needs.Needs["Hunger"].Level = 85;
        
        // Run tick
        _agent.Tick(_world, 1);
        
        // Verify: should perceive food
        var beliefs = _agent.Beliefs.GetBeliefs(0.5f);
        Assert.IsTrue(beliefs.Any(b => b.Fact.Subject == "Food"));
        
        // Verify: should generate Eat goal
        var goals = _agent.Goals.Goals.Where(g => g.Status == GoalStatus.Active);
        Assert.IsTrue(goals.Any(g => g.Type == "Eat"));
        
        // Verify: should have chosen action (e.g., MoveTo food)
        Assert.IsNotNull(_agent.CurrentAction);
    }
    
    [TestMethod]
    public void MultiTickAction_MovesToFood()
    {
        // 50 agents, 100 ticks of simulation
        for (int i = 0; i < 100; i++)
        {
            _agent.Tick(_world, (ulong)i);
        }
        
        // After many ticks, agent should have moved
        Assert.AreNotEqual(new Vector2(50, 50), _agent.Position);
    }
    
    [TestMethod]
    public void PartialObservability_AgentsCantSeeThroughWalls()
    {
        var target = new Agent { Id = "bob", Position = new Vector2(100, 50) };
        var obstacle = new Obstacle { Position = new Vector2(70, 50) };  // Between alice and bob
        
        _world.AddAgent(target);
        _world.AddObstacle(obstacle);
        
        _agent.Tick(_world, 1);
        
        // Alice should NOT perceive Bob (blocked by obstacle)
        var beliefs = _agent.Beliefs.GetBeliefs();
        Assert.IsFalse(beliefs.Any(b => b.Fact.Subject == "bob"));
    }
}
```

---

## 5. Tests de régression (sauvegarde/chargement)

### 5.1 Vérification du déterminisme

```csharp
[TestClass]
public class DeterminismTests
{
    [TestMethod]
    public void BitPerfect_Save_Load_Identical()
    {
        // Run 1: Normal
        var world1 = CreateWorld();
        var seed = 12345;
        var rng1 = new Random(seed);
        
        for (int tick = 0; tick < 100; tick++)
        {
            world1.Tick(rng1);
        }
        
        var saved = SaveWorld(world1);
        
        // Run 2: Load from saved
        var world2 = LoadWorld(saved);
        var rng2 = new Random(seed);
        world2.RngState = world1.RngState;  // Restore PRNG state
        
        for (int tick = 0; tick < 50; tick++)
        {
            world2.Tick(rng2);
        }
        
        // Compare: should be byte-for-byte identical
        var dump1 = SerializeWorld(world1);
        var dump2 = SerializeWorld(world2);
        
        Assert.AreEqual(dump1, dump2);
    }
    
    [TestMethod]
    public void FloatingPoint_Consistent()
    {
        var agent = new Agent();
        agent.Position = new Vector2(50.123456f, 75.654321f);
        agent.Energy = 42.987654f;
        
        var saved = SerializeAgent(agent);
        var loaded = DeserializeAgent(saved);
        
        // Floating point should be identical (no rounding)
        Assert.AreEqual(agent.Position.X, loaded.Position.X);
        Assert.AreEqual(agent.Position.Y, loaded.Position.Y);
        Assert.AreEqual(agent.Energy, loaded.Energy);
    }
    
    [TestMethod]
    public void PRNG_State_Restored()
    {
        var rng1 = new Random(999);
        var seq1 = Enumerable.Range(0, 100).Select(_ => rng1.Next()).ToList();
        
        // Save PRNG state
        var state = rng1.GetState();
        
        // Create new PRNG with saved state
        var rng2 = new Random();
        rng2.SetState(state);
        
        var seq2 = Enumerable.Range(0, 100).Select(_ => rng2.Next()).ToList();
        
        // Should produce identical sequence
        CollectionAssert.AreEqual(seq1, seq2);
    }
}
```

---

## 6. Objectifs de couverture par module

| Module | Cible | Pourquoi |
|--------|--------|-----|
| PerceptionSystem | 85% | Logique de perception centrale, cas limites importants |
| MemorySystem | 80% | Calcul du decay, filtrage du rappel |
| BeliefStore | 85% | Règles de révision, gestion de la confiance |
| GoalSystem | 82% | Génération et priorisation des objectifs |
| UtilityEvaluator | 85% | Critique pour le comportement, scoring multi-facteurs |
| DecisionSystem | 80% | Orchestre le pipeline, principalement de la composition |
| ActionSystem | 85% | Exécution multi-tick, interruption |
| CommunicationSystem | 85% | Types de messages, mises à jour de confiance |
| GroupSystem | 80% | Formation, dissolution, rôles |
| **Simulation.Core** | **83%** | **Standard minimum en entreprise** |

---

## 7. Intégration CI/CD

```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 7.0
      
      - name: Run tests
        run: dotnet test --configuration Release --logger "trx" --collect:"XPlat Code Coverage"
      
      - name: Coverage threshold
        run: |
          coverage=$(grep -oP 'Line coverage: \K[\d.]+' coverage.txt)
          if (( $(echo "$coverage < 80" | bc -l) )); then
            echo "Coverage $coverage% below 80% threshold"
            exit 1
          fi
```

---

## 8. Checklist d'exécution des tests

- [ ] Tous les tests unitaires passent (160+ tests)
- [ ] Rapport de couverture ≥80%
- [ ] Tests d'intégration réussis (cycles complets)
- [ ] Tests de régression réussis (déterminisme sauvegarde/chargement)
- [ ] Aucune fuite mémoire (vérification du profiler)
- [ ] Performance conforme aux cibles (résultats de profiling)
- [ ] Porte CI active (bloque le merge si <80%)