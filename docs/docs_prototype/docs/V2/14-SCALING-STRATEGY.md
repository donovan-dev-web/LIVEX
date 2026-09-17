# Stratégie de scaling — 50 → 500 → 1000 agents

## 1. Contexte et contraintes

**Objectif** :
- 50 agents (baseline) : 30+ ticks/sec
- 500 agents (stress test) : 20+ ticks/sec
- 1000 agents (limite) : 10+ ticks/sec

**Baseline** (V1) : ~50 agents à 25 ticks/sec

**Complexité ajoutée par V2** :
- BDI complète (perception, mémoire, croyances, objectifs, utilité)
- Communication locale (diffusion messages)
- Groupes et coalitions
- Observabilité partielle (chaque agent calcule son propre modèle du monde)

---

## 2. Goulots d'étranglement identifiés

### 2.1 Perception (actuellement O(n) par agent)

**Problème** : Chaque agent scanne tous les autres agents.

```
50 agents:    50 * 50 = 2,500 comparaisons/tick
500 agents:   500 * 500 = 250,000 comparaisons/tick
1000 agents:  1000 * 1000 = 1,000,000 comparaisons/tick
```

**Solution** : Spatial Grid

```csharp
public class SpatialGrid
{
    private Dictionary<int, List<Entity>> _cells;
    private float _cellSize;  // e.g., 50 units
    
    public List<Entity> QueryRadius(Vector2 pos, float radius)
    {
        var result = new List<Entity>();
        
        // Only check nearby cells
        var cellX = (int)(pos.X / _cellSize);
        var cellY = (int)(pos.Y / _cellSize);
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var key = (cellX + dx, cellY + dy).GetHashCode();
                if (_cells.TryGetValue(key, out var cellEntities))
                {
                    result.AddRange(cellEntities);
                }
            }
        }
        
        // Filter by actual distance
        return result.Where(e => Vector2.Distance(pos, e.Position) < radius).ToList();
    }
}
```

**Gain** : O(1) en moyenne (dépend du réglage de la taille de la grille)

### 2.2 Prise de décision (évaluation de l'utilité de toutes les actions)

**Problème** : Chaque agent score chaque action pour chaque goal.

```
Agents: 500
Goals/agent: 3
Actions/goal: 5
Utility evals: 500 * 3 * 5 = 7,500/tick
```

**Solution** : Mise en cache des actions + élagage précoce

```csharp
public class DecisionSystem
{
    public Action SelectAction(Agent agent)
    {
        // Cache: if goals haven't changed, reuse utility scores
        if (agent.GoalsChanged)
        {
            // Recompute all scores
            var scores = EvaluateAllActions(agent);
            agent.ActionCache = scores;
            agent.GoalsChanged = false;
        }
        else
        {
            // Use cached scores
            var scores = agent.ActionCache;
        }
        
        // Early termination: if score > 90, just pick it
        var best = scores.OrderByDescending(s => s.Value).First();
        if (best.Value > 0.9)
            return best.Key;
        
        return best.Key;
    }
}
```

**Gain** : évite le calcul lorsque les objectifs sont stables

### 2.3 Communication (diffusion à tous les agents proches)

**Problème** : les messages sont diffusés à tous les agents dans le rayon.

```
Message from agent A to 50 nearby agents
500 agents * 5 messages each = 2,500 message processes/tick
```

**Solution** : Traitement par lots des messages

```csharp
public class CommunicationSystem
{
    private Queue<Message> _pendingMessages;
    
    public void BroadcastMessage(Message msg, float radius)
    {
        // Enqueue, don't process immediately
        _pendingMessages.Enqueue(msg);
    }
    
    public void ProcessAllMessages(World world)
    {
        // Process ALL messages at once (after all agents have sent)
        while (_pendingMessages.TryDequeue(out var msg))
        {
            var receivers = world.SpatialGrid.QueryRadius(msg.SenderPos, msg.Radius);
            foreach (var receiver in receivers)
            {
                receiver.ReceiveMessage(msg);
            }
        }
    }
}
```

**Gain** : grille spatiale + un seul passage par lot

---

## 3. Niveau de détail (LOD)

### 3.1 Concept

Les agents éloignés décident moins souvent.

```csharp
public class LevelOfDetail
{
    private float[] DecisionFrequency = { 1.0f, 0.5f, 0.25f };  // By zone
    private float[] ZoneDistances = { 100, 200, 400 };
    
    public bool ShouldDecide(Agent agent, Agent other, ulong tick)
    {
        var distance = Vector2.Distance(agent.Position, other.Position);
        
        int zone = 0;
        if (distance > ZoneDistances[0]) zone = 1;
        if (distance > ZoneDistances[1]) zone = 2;
        
        // Decide every Nth tick based on zone
        var tickModulo = (int)(1 / DecisionFrequency[zone]);
        return (tick % tickModulo) == 0;
    }
}
```

**Effet** :
- Agents proches (zone 0) : à chaque tick
- Distance moyenne (zone 1) : tous les 2 ticks
- Éloignés (zone 2) : tous les 4 ticks

**Gain** : réduction de 2x du calcul des agents éloignés

---

## 4. Objectifs de profiling

### 4.1 Répartition du tick (cible 1000 agents)

```
Total budget per tick: 100ms (10 ticks/sec)

Perception:        20ms (spatial grid query)
Memory/Beliefs:    15ms (store, decay, update)
Needs/Goals:       10ms (calculate needs, generate goals)
Utility:           20ms (score actions, LOD filtering)
Communication:     15ms (process message batch)
Actions/Movement:  15ms (execute, update positions)
Events:            5ms (log, notify)

Total:             100ms ✓
```

### 4.2 Commandes de profiling

```bash
# .NET profiling
dotnet run --configuration Release -- --profile --agents 500

# Output: timing per subsystem
# Perception:       18.5ms (target 20ms)
# Decision:         22.3ms (target 30ms)
# ...

# Godot profiling (built-in profiler)
# Rendering:        8ms
# Physics:          2ms
# Logic:            5ms
```

---

## 5. Réglage de la grille spatiale

### 5.1 Optimisation de la taille de la grille

```csharp
public class SpatialGridTuning
{
    // World is 500x500 units
    private float OptimalCellSize(int agentCount)
    {
        // Rule of thumb: cell should contain ~5-10 agents on average
        var worldArea = 500 * 500;
        var agentsPerCell = 7;
        var cellArea = worldArea / (agentCount / agentsPerCell);
        return MathF.Sqrt(cellArea);
    }
    
    // 50 agents: ~119 units/cell
    // 500 agents: ~37 units/cell
    // 1000 agents: ~26 units/cell
}
```

### 5.2 Fréquence de reconstruction de la grille

```csharp
public class SpatialGridUpdate
{
    private ulong LastRebuild;
    private const ulong RebuildInterval = 10;  // Every 10 ticks
    
    public void UpdateGrid(List<Agent> agents, ulong tick)
    {
        if (tick - LastRebuild > RebuildInterval)
        {
            // Full grid rebuild
            Clear();
            foreach (var agent in agents)
                Insert(agent);
            LastRebuild = tick;
        }
        else
        {
            // Incremental update (faster)
            foreach (var agent in agents)
                if (agent.HasMoved)
                    UpdateAgent(agent);
        }
    }
}
```

---

## 6. Optimisation mémoire

### 6.1 Pooling de collections

```csharp
public class ObjectPool<T> where T : class, new()
{
    private Queue<T> _pool = new();
    private int _capacity;
    
    public T Rent()
    {
        return _pool.Count > 0 ? _pool.Dequeue() : new T();
    }
    
    public void Return(T obj)
    {
        if (_pool.Count < _capacity)
            _pool.Enqueue(obj);
    }
}

// Usage:
var obsPool = new ObjectPool<Observation> { Capacity = 10000 };

// In PerceptionSystem:
var obs = obsPool.Rent();
obs.EntityId = "...";
// ... use obs ...
obsPool.Return(obs);  // Reuse
```

**Gain** : réduit la pression du GC de 30-40%

### 6.2 Pré-allocation mémoire

```csharp
// Pre-size collections
var beliefs = new List<Belief>(200);      // Max 200 beliefs/agent
var memories = new Queue<MemoryEntry>(500); // Max 500 entries/agent
var goals = new List<Goal>(10);           // Typical 3-10 active goals
```

---

## 7. Optimisation d'algorithme : échantillonnage de la perception

### 7.1 Perception étagée

Au lieu de faire percevoir tous les agents à chaque tick, on étale la perception :

```csharp
public class StaggeredPerception
{
    public List<Observation> Perceive(Agent agent, World world, ulong tick)
    {
        // Stagger by agent ID
        int moduloHash = agent.Id.GetHashCode() % 4;  // 0-3
        
        // Only perceive every 4 ticks (rotated)
        if ((tick + moduloHash) % 4 != 0)
            return agent.LastObservations;  // Return cached
        
        // Actually perceive
        return FullPerceptionScan(agent, world, tick);
    }
}
```

**Effet** : chaque agent perçoit 4 fois moins souvent, mais de façon étagée sur la population

**Compromis** : perceptions légèrement obsolètes (latence maximale de 4 ticks)

---

## 8. Suite de benchmarks

### 8.1 Scénarios de test

```csharp
[BenchmarkClass]
public class ScalingBenchmarks
{
    [Benchmark]
    [Arguments(50)]
    [Arguments(500)]
    [Arguments(1000)]
    public void TicksPerSecond(int agentCount)
    {
        var world = CreateWorld(agentCount);
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            world.Tick();
        }
        
        sw.Stop();
        var ticksPerSec = 100 / (sw.ElapsedMilliseconds / 1000.0);
        
        Console.WriteLine($"{agentCount} agents: {ticksPerSec:F1} ticks/sec");
    }
    
    [Benchmark]
    public void PerceptionLatency()
    {
        // Measure time to perceive all 1000 agents
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            world.Agents.ForEach(a => a.Perception.Perceive(a, world, (ulong)i));
        }
        sw.Stop();
        
        var avgPerceptionTime = sw.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"Avg perception time: {avgPerceptionTime}ms");
    }
}
```

### 8.2 Résultats attendus

| Agents | Ticks/sec | Perception | Decision | Comm | Total |
|--------|-----------|-----------|----------|------|-------|
| 50     | 30+       | 2ms       | 3ms      | 1ms  | 6ms   |
| 500    | 20+       | 12ms      | 15ms     | 8ms  | 35ms  |
| 1000   | 10+       | 25ms      | 30ms     | 18ms | 73ms  |

---

## 9. Feuille de route d'optimisation progressive

### Phase 9a : Profiling de référence

- [ ] Exécuter les scénarios 50, 500 et 1000 agents
- [ ] Identifier les goulots d'étranglement (profiler)
- [ ] Documenter les timings actuels

### Phase 9b : Grille spatiale

- [ ] Implémenter la grille spatiale
- [ ] Régler la taille des cellules
- [ ] Cible : accélération de 50 % de la perception

### Phase 9c : Cache de décision

- [ ] Implémenter le cache d'actions
- [ ] Ajouter l'arrêt précoce
- [ ] Cible : accélération de 30 % des décisions

### Phase 9d : Traitement par lots des messages

- [ ] Mettre les messages en file d'attente au lieu d'un traitement immédiat
- [ ] Regrouper les requêtes spatiales
- [ ] Cible : accélération de 40 % de la communication

### Phase 9e : LOD + perception étagée

- [ ] Implémenter le LOD par zones
- [ ] Étaler la perception sur la population
- [ ] Cible : accélération globale de 20 %

### Phase 9f : Validation

- [ ] Relancer les benchmarks 50/500/1000
- [ ] Vérifier que les cibles sont atteintes
- [ ] Profiler à nouveau pour les points chauds restants

---

## 10. Problèmes de scaling et atténuations

| Problème | Impact | Atténuation |
|-------|--------|-----------|
| Croissance mémoire (agents × attributs) | 1000 agents @ 500 KB/agent = 500 MB | Pooling d'objets, compression |
| Latence des requêtes SQLite (persistance) | Des milliers de relations → sauvegardes lentes | Traitement par lots, transactions, index |
| Bande passante réseau (WebSocket) | Instantanés volumineux à chaque tick | Compression, mises à jour delta |
| Rendu Godot (1000 sprites) | Goulot d'étranglement CPU → GPU | Culling, rendu LOD, batching |
| Divergence des croyances (1000 modèles uniques) | Complexité de vérification | Échantillonnage, validation statistique |

---

## 11. Résumé des cibles de performance

**Critères Go/No-Go** (achèvement de la phase 9) :

- [ ] 50 agents : ≥30 ticks/sec
- [ ] 500 agents : ≥20 ticks/sec
- [ ] 1000 agents : ≥10 ticks/sec
- [ ] Mémoire : <1 GB pour 1000 agents
- [ ] CPU : <1 cœur saturé
- [ ] Aucune fuite mémoire (exécution de 24 heures)