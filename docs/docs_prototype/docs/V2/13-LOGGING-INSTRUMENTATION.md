# Logging et instrumentation V2

## 1. Stratégie logging

**3 niveaux** :

1. **Événements structurés** (ELT-ready)
2. **Décisions détaillées** (trace BDI)
3. **Logs texte** (debugging)

---

## 2. Événements structurés (SQLite events_log)

### 2.1 Schéma

```sql
CREATE TABLE events_log (
    id TEXT PRIMARY KEY,
    simulation_id TEXT,
    tick INTEGER,
    timestamp DATETIME,
    agent_id TEXT,
    event_type TEXT,          -- 'PerceptionEvent', 'DecisionEvent', 'ActionEvent', etc.
    event_category TEXT,      -- 'cognitive', 'social', 'physical', 'resource'
    data JSON,                -- Full event details
    
    FOREIGN KEY (simulation_id) REFERENCES simulation_state(id),
    FOREIGN KEY (agent_id) REFERENCES agents(id)
);

CREATE INDEX idx_events_agent_tick ON events_log(agent_id, tick);
CREATE INDEX idx_events_type ON events_log(event_type);
CREATE INDEX idx_events_category ON events_log(event_category);
```

### 2.2 Types d'événements

```csharp
public abstract class Event
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; }
    public string Category { get; set; }
    public ulong Tick { get; set; }
    public string AgentId { get; set; }
    public Dictionary<string, object> Data { get; set; }
    
    public abstract void ToJson();
}

public class PerceptionEvent : Event
{
    public List<string> ObservedEntityIds { get; set; }
    public int Count { get; set; }
    public float AverageConfidence { get; set; }
}

public class DecisionEvent : Event
{
    public List<string> ConsideredGoals { get; set; }
    public Dictionary<string, float> ActionScores { get; set; }
    public string ChosenAction { get; set; }
    public float ChosenUtility { get; set; }
}

public class ActionEvent : Event
{
    public string ActionType { get; set; }
    public string ActionStatus { get; set; }  // Started, Updated, Completed, Failed
    public Dictionary<string, object> Outcome { get; set; }
}

public class CommunicationEvent : Event
{
    public string SenderId { get; set; }
    public List<string> ReceiverIds { get; set; }
    public string MessageType { get; set; }
    public float MessageConfidence { get; set; }
}

public class BeliefEvent : Event
{
    public string Fact { get; set; }
    public float OldConfidence { get; set; }
    public float NewConfidence { get; set; }
    public string Source { get; set; }
}

public class GroupEvent : Event
{
    public string GroupId { get; set; }
    public string GroupAction { get; set; }  // Formed, MemberJoined, MemberLeft, Dissolved
}
```

---

## 3. Stockage des traces de décision

### 3.1 Schéma SQLite

```sql
CREATE TABLE decision_traces (
    id TEXT PRIMARY KEY,
    simulation_id TEXT,
    tick INTEGER,
    agent_id TEXT,
    
    -- Inputs
    beliefs_count INTEGER,
    avg_belief_confidence REAL,
    active_goals_count INTEGER,
    primary_need TEXT,
    primary_need_level REAL,
    
    -- Evaluation
    candidate_actions TEXT,              -- JSON array
    action_scores TEXT,                  -- JSON {action: score}
    
    -- Output
    chosen_action TEXT,
    chosen_utility REAL,
    decision_reason TEXT,
    
    -- Metadata
    decision_time_ms REAL,
    created_at DATETIME,
    
    FOREIGN KEY (simulation_id) REFERENCES simulation_state(id),
    FOREIGN KEY (agent_id) REFERENCES agents(id)
);

CREATE INDEX idx_decision_traces_agent ON decision_traces(agent_id, tick);
```

### 3.2 Enregistrement des traces de décision

```csharp
public class DecisionRecorder
{
    public void RecordDecision(
        string simId,
        Agent agent,
        ulong tick,
        DecisionContext context,
        Action chosenAction,
        float utility,
        SQLiteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO decision_traces 
                (id, simulation_id, tick, agent_id, beliefs_count, avg_belief_confidence,
                 active_goals_count, primary_need, primary_need_level,
                 candidate_actions, action_scores, chosen_action, chosen_utility,
                 decision_reason, created_at)
                VALUES (@id, @sim, @tick, @agent, @bc, @bconf,
                        @gc, @need, @nlvl,
                        @acts, @scores, @chosen, @util,
                        @reason, @now)
            ";
            
            var beliefs = agent.Beliefs.GetBeliefs();
            var goals = agent.Goals.Goals.Where(g => g.Status == GoalStatus.Active).ToList();
            var topNeed = agent.Needs.Needs.Values
                .OrderByDescending(n => n.Level)
                .First();
            
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@sim", simId);
            cmd.Parameters.AddWithValue("@tick", (long)tick);
            cmd.Parameters.AddWithValue("@agent", agent.Id);
            cmd.Parameters.AddWithValue("@bc", beliefs.Count);
            cmd.Parameters.AddWithValue("@bconf", beliefs.Average(b => b.Confidence));
            cmd.Parameters.AddWithValue("@gc", goals.Count);
            cmd.Parameters.AddWithValue("@need", topNeed.Id);
            cmd.Parameters.AddWithValue("@nlvl", topNeed.Level);
            cmd.Parameters.AddWithValue("@acts", JsonConvert.SerializeObject(
                context.CandidateActions.Select(a => a.Type)
            ));
            cmd.Parameters.AddWithValue("@scores", JsonConvert.SerializeObject(
                context.ActionScores.ToDictionary(a => a.Key.Type, a => a.Value)
            ));
            cmd.Parameters.AddWithValue("@chosen", chosenAction.Type);
            cmd.Parameters.AddWithValue("@util", utility);
            cmd.Parameters.AddWithValue("@reason", "highest utility");
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            
            cmd.ExecuteNonQuery();
        }
    }
}
```

---

## 4. Logging de debug (console/fichier)

### 4.1 Logging structuré (intégration Serilog)

```csharp
// Setup (Startup.cs)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/v2_simulation_.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .Enrich.WithProperty("Application", "SSE-V2")
    .CreateLogger();

// Usage in simulation
Log.Information("Tick {Tick}: Simulation started with {AgentCount} agents", 
    tick, world.Agents.Count);

Log.Debug("Agent {AgentId} perceives {Count} entities at tick {Tick}",
    agent.Id, observations.Count, tick);

Log.Warning("Agent {AgentId} blocked by obstacle at {Position}",
    agent.Id, agent.Position);

Log.Error("Agent {AgentId} action {Action} failed: {Reason}",
    agent.Id, currentAction.Type, "target disappeared");
```

### 4.2 Niveaux de log

| Niveau | Utilisation |
|-------|-------|
| Error | Action échouée, état incohérent, crash évités |
| Warning | Chemin bloqué, ressources insuffisantes, timeout |
| Information | Résumé du tick, formation de groupes, événements majeurs |
| Debug | Décisions des agents, mises à jour de croyances, envois de messages |
| Verbose | Traces BDI complètes (uniquement en développement) |

---

## 5. Streaming de métriques en temps réel

### 5.1 Diffuseur WebSocket

```csharp
public class MetricsWebSocketBroadcaster
{
    private List<WebSocket> _clients = new();
    
    public async Task BroadcastMetrics(WorldSnapshot snapshot)
    {
        var metrics = new
        {
            timestamp = DateTime.Now,
            tick = snapshot.CurrentTick,
            agents = snapshot.Agents.Count,
            resources = snapshot.Resources.Count,
            groups = snapshot.Groups.Count,
            messages_sent = snapshot.MessagesSentThisTick,
            avg_agent_energy = snapshot.Agents.Average(a => a.Energy),
            cognitive_diversity = snapshot.CognitiveDiversity,
            emergent_phenomena = snapshot.EmergentPhenomena
        };
        
        var json = JsonConvert.SerializeObject(metrics);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        var tasks = _clients.Select(client =>
            client.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None)
        );
        
        await Task.WhenAll(tasks);
    }
}
```

### 5.2 Consommation par la Web UI

```typescript
// React hook
const useSimulationMetrics = () => {
    const [metrics, setMetrics] = useState(null);
    
    useEffect(() => {
        const ws = new WebSocket('ws://localhost:5180/metrics');
        ws.onmessage = (event) => {
            const data = JSON.parse(event.data);
            setMetrics(data);
        };
        
        return () => ws.close();
    }, []);
    
    return metrics;
};

// Component
export const MetricsPanel = () => {
    const metrics = useSimulationMetrics();
    
    return (
        <div>
            <h2>Tick: {metrics?.tick}</h2>
            <p>Agents: {metrics?.agents}</p>
            <p>Cognitive Diversity: {metrics?.cognitive_diversity?.toFixed(2)}</p>
            <p>Emergent: {metrics?.emergent_phenomena?.join(', ')}</p>
        </div>
    );
};
```

---

## 6. Instrumentation de profiling

### 6.1 Marqueurs de temps

```csharp
public class ProfileMarkers
{
    private Dictionary<string, (long totalTicks, int count)> _markers = new();
    
    public class Marker : IDisposable
    {
        private string _name;
        private long _startTicks;
        private ProfileMarkers _profiler;
        
        public Marker(ProfileMarkers profiler, string name)
        {
            _profiler = profiler;
            _name = name;
            _startTicks = Stopwatch.GetTimestamp();
        }
        
        public void Dispose()
        {
            var elapsed = Stopwatch.GetTimestamp() - _startTicks;
            _profiler.Record(_name, elapsed);
        }
    }
    
    public Marker Start(string name) => new Marker(this, name);
    
    private void Record(string name, long ticks)
    {
        if (_markers.TryGetValue(name, out var existing))
            _markers[name] = (existing.totalTicks + ticks, existing.count + 1);
        else
            _markers[name] = (ticks, 1);
    }
    
    public void PrintReport()
    {
        Console.WriteLine("=== Profiling Report ===");
        foreach (var (name, (totalTicks, count)) in _markers.OrderByDescending(m => m.Value.totalTicks))
        {
            var ms = totalTicks / (Stopwatch.Frequency / 1000.0);
            var avg = ms / count;
            Console.WriteLine($"{name,-30} {ms:F2}ms total, {avg:F2}ms avg ({count} calls)");
        }
    }
}

// Usage
using (var marker = _profiler.Start("Perception"))
{
    observations = agent.Perception.Perceive(agent, world, tick);
}

using (var marker = _profiler.Start("Decision"))
{
    action = agent.Decision.SelectAction(agent);
}
```

### 6.2 Exemple de sortie de benchmark

```
=== Profiling Report ===
Perception                     240.51ms total, 0.48ms avg (500 calls)
Decision                       185.23ms total, 0.37ms avg (500 calls)
Communication                  92.15ms total, 0.18ms avg (500 calls)
Movement                        45.67ms total, 0.09ms avg (500 calls)
Belief Update                   78.34ms total, 0.16ms avg (500 calls)
```

---

## 7. Interface de débugger

### 7.1 Pause/avance pas à pas de la simulation

```csharp
public class SimulationDebugger
{
    public bool IsPaused { get; set; }
    public bool StepOne { get; set; }
    
    public async Task RunDebug(World world)
    {
        for (ulong tick = 0; tick < MaxTicks; tick++)
        {
            // Check pause
            while (IsPaused && !StepOne)
                await Task.Delay(100);
            
            StepOne = false;
            
            // Run tick with instrumentation
            using (var marker = _profiler.Start("WorldTick"))
            {
                world.Tick();
            }
            
            // Dump state if debug breakpoint hit
            if (tick % 100 == 0)
                DumpWorldState(world, tick);
        }
    }
    
    public void SetBreakpoint(string agentId, string condition)
    {
        // e.g., "hunger > 90"
        _breakpoints[agentId] = condition;
    }
    
    public bool CheckBreakpoint(Agent agent, ulong tick)
    {
        if (!_breakpoints.TryGetValue(agent.Id, out var condition))
            return false;
        
        // Evaluate condition
        return EvaluateCondition(agent, condition);
    }
}
```

### 7.2 Commandes de la console de debug

```
> list-agents
Agent 0: alice, pos (50,50), energy 45, hunger 75
Agent 1: bob, pos (30,70), energy 60, hunger 42
...

> inspect-agent alice
Name: alice
Position: (50, 50)
Energy: 45
Beliefs: 12 (avg confidence 0.68)
Goals: 3 (Eat, Explore, Social)
Current Action: MoveTo at (60, 50)
Recent decisions: [...]

> trace-decision alice
Tick 5000: Decision for alice
  Perceptions: 8 entities
  Top need: Hunger (75)
  Candidate goals: [Eat, Explore]
  Actions evaluated:
    - MoveTo food (utility 15.4)
    - Explore (utility 8.2)
  Chosen: MoveTo food

> set-breakpoint alice "hunger > 90"
Breakpoint set for alice when hunger > 90
```

---

## 8. Export pour analyse

### 8.1 Export CSV des traces de décision

```csharp
public void ExportDecisionTraces(string filepath, SQLiteConnection conn)
{
    using (var writer = new StreamWriter(filepath))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        csv.WriteHeader<DecisionTraceRow>();
        
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM decision_traces ORDER BY tick";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    csv.WriteRecord(new DecisionTraceRow
                    {
                        AgentId = reader["agent_id"].ToString(),
                        Tick = (long)reader["tick"],
                        BeliefCount = (int)reader["beliefs_count"],
                        GoalCount = (int)reader["active_goals_count"],
                        ChosenAction = reader["chosen_action"].ToString(),
                        Utility = (double)reader["chosen_utility"]
                    });
                }
            }
        }
    }
}

public class DecisionTraceRow
{
    public string AgentId { get; set; }
    public long Tick { get; set; }
    public int BeliefCount { get; set; }
    public int GoalCount { get; set; }
    public string ChosenAction { get; set; }
    public double Utility { get; set; }
}
```

---

## 9. Checklist d'instrumentation

- [ ] Les 8 types d'événements enregistrés dans SQLite
- [ ] Traces de décision stockées avec le contexte complet
- [ ] Logging structuré via Serilog (console + fichier)
- [ ] Marqueurs de profiling pour tous les sous-systèmes majeurs
- [ ] Diffuseur WebSocket pour les métriques en temps réel
- [ ] Interface de pause/avance pas à pas pour le debug
- [ ] Outils d'export (CSV, JSON)
- [ ] Porte CI : s'assurer qu'aucune donnée sensible n'est journalisée