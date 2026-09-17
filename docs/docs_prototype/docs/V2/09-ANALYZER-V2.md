# Analyzer V2 — Métriques d'émergence avancées

## 1. Vue générale

L'Analyzer V2 est un service .NET qui **consomme le flux WebSocket** de simulation et **mesure l'émergence**.

Contrairement à V1 (métriques population/ressources), V2 mesure aussi :

- **Diversité cognitive** (divergence croyances/objectifs entre agents)
- **Propagation d'information** (propagation rumeurs, diffusion info)
- **Complexité sociale** (réseaux de confiance, graphes relationnels)
- **Convergence d'objectifs** (agents visent-ils mêmes objectifs ?)
- **Boucles de rétroaction** (cycles action→conséquence→décision)
- **Dynamique des groupes** (formation, dissolution, efficacité groupes)
- **Indicateurs d'émergence** (signaux de phénomènes complexes)

---

## 2. Architecture Analyzer v2

```text
Service Analyzer (ASP.NET)
├── WebSocket Client
│   └── Se connecte à Simulation (ws://localhost:5180)
│
├── Moteur de métriques
│   ├── CognitiveDiversityMetrics
│   ├── InformationPropagationMetrics
│   ├── SocialComplexityMetrics
│   ├── GoalConvergenceMetrics
│   ├── FeedbackLoopDetector
│   ├── ResourceSustainabilityMetrics
│   └── GroupDynamicsMetrics
│
├── RunStore
│   └── Agrège données par run
│   └── Stocke en SQLite
│
├── REST API (Kestrel)
│   ├── GET /api/runs
│   ├── GET /api/runs/{id}
│   ├── GET /api/runs/{id}/metrics
│   ├── GET /api/compare?a=run1&b=run2
│   ├── GET /api/beliefs/{agentId}
│   ├── GET /api/relationships/{agentId}
│   ├── GET /api/groups
│   └── GET /api/emergent-phenomena
│
└── Diffuseur WebSocket
    └── Envoie les métriques en temps réel à Web UI
```

---

## 3. Détails des métriques

### 3.1 Diversité cognitive

**Définition** : Variation croyances/objectifs/décisions parmi les agents.

```csharp
public class CognitiveDiversityMetrics
{
    // Belief distribution
    public float BeliefDiversity { get; set; }        // Shannon entropy of beliefs
    public float BeliefDisagreement { get; set; }     // % agents disagree on same fact
    public float BeliefConfidenceVariance { get; set; } // Variance in confidence
    
    // Goal distribution
    public float GoalDiversity { get; set; }          // How varied are goals ?
    public float GoalConvergence { get; set; }        // Do agents share goals ?
    public int DistinctGoalTypes { get; set; }        // How many goal types active
    
    // Decision variability
    public float DecisionDiversity { get; set; }      // % agents make different choices
    public float IntentionStability { get; set; }     // How long agents commit to intentions
    
    // Personal trait expression
    public float TraitExpressionDiversity { get; set; } // Variance in behavioral outcomes
}

// Calculation
public CognitiveDiversityMetrics Calculate(List<Agent> agents, ulong tick)
{
    var metrics = new CognitiveDiversityMetrics();
    
    // Belief entropy
    var beliefFacts = agents
        .SelectMany(a => a.beliefs.Select(b => b.fact))
        .GroupBy(f => f)
        .Select(g => (float)g.Count() / agents.Count)
        .ToList();
    
    metrics.BeliefDiversity = ShannonEntropy(beliefFacts);
    
    // Goal diversity
    var activateGoals = agents.SelectMany(a => a.goals.Where(g => g.status == GoalStatus.Active));
    var goalTypes = activateGoals.Select(g => g.type).Distinct().Count();
    metrics.GoalDiversity = ShannonEntropy(
        activateGoals
            .GroupBy(g => g.type)
            .Select(g => (float)g.Count() / activateGoals.Count())
    );
    
    metrics.DistinctGoalTypes = goalTypes;
    
    return metrics;
}

private float ShannonEntropy(List<float> distribution)
{
    return -distribution.Sum(p => p > 0 ? p * MathF.Log2(p) : 0);
}
```

### 3.2 Propagation d'information

**Définition** : Comment info se propage via communication.

```csharp
public class InformationPropagationMetrics
{
    public float MessageVolume { get; set; }          // Messages per agent per tick
    public float InformationDiffusionSpeed { get; set; } // Ticks to reach 80% agents
    public float RumorAccuracyDegradation { get; set; }  // Confidence loss per hop
    public int MaxMessageHops { get; set; }           // Longest chain before lost
    public float NetworkCentrality { get; set; }      // Hub concentration
}

// Calculation
public InformationPropagationMetrics Calculate(List<Message> messages, List<Agent> agents)
{
    var metrics = new InformationPropagationMetrics();
    
    // Message volume
    metrics.MessageVolume = (float)messages.Count / agents.Count;
    
    // Track message origins and destinations
    var messageChains = BuildMessageChains(messages);
    var maxHops = messageChains.Max(chain => chain.hops);
    metrics.MaxMessageHops = maxHops;
    
    // Confidence degradation
    var degradations = messageChains
        .Where(m => m.hops > 1)
        .Select(m => m.confidenceStart - m.confidenceEnd);
    metrics.RumorAccuracyDegradation = degradations.Average();
    
    // Network centrality (hub concentration)
    var senderCounts = messages.GroupBy(m => m.SenderId).Select(g => g.Count());
    metrics.NetworkCentrality = senderCounts.Max() / (float)messages.Count;
    
    return metrics;
}

private List<MessageChain> BuildMessageChains(List<Message> messages)
{
    var chains = new List<MessageChain>();
    var processed = new HashSet<string>();
    
    foreach (var msg in messages.Where(m => processed.Add(m.MessageId)))
    {
        var chain = new MessageChain { originalMessage = msg };
        
        // Find relayed versions
        foreach (var relayed in messages.Where(m => 
            m.Source == "hearsay" && 
            m.Payload.ToString().Contains(msg.Payload.ToString())))
        {
            chain.relayedMessages.Add(relayed);
        }
        
        chains.Add(chain);
    }
    
    return chains;
}
```

### 3.3 Complexité sociale

**Définition** : Structure et patterns du réseau social.

```csharp
public class SocialComplexityMetrics
{
    public float AverageTrustLevel { get; set; }      // Mean trust across all relationships
    public float TrustVariance { get; set; }          // Variance in trust
    public float NetworkDensity { get; set; }         // Edges / possible edges
    public float ClusteringCoefficient { get; set; }  // Tendency to form triangles (A→B→C→A)
    public float AverageCentrality { get; set; }      // Betweenness centrality
    public int NumberOfCommunities { get; set; }      // Detected communities (via Louvain)
    public float CommunityStability { get; set; }     // % stable vs fluctuating
}

// Calculation
public SocialComplexityMetrics Calculate(List<Agent> agents)
{
    var metrics = new SocialComplexityMetrics();
    
    var allRelationships = agents
        .SelectMany(a => a.relationships)
        .ToList();
    
    // Average trust
    metrics.AverageTrustLevel = allRelationships.Average(r => r.trustLevel);
    metrics.TrustVariance = CalculateVariance(allRelationships.Select(r => r.trustLevel));
    
    // Network density
    int possibleEdges = agents.Count * (agents.Count - 1);
    metrics.NetworkDensity = (float)allRelationships.Count / possibleEdges;
    
    // Clustering coefficient
    metrics.ClusteringCoefficient = CalculateClusteringCoefficient(agents);
    
    // Centrality
    metrics.AverageCentrality = CalculateBetweennessCentrality(agents);
    
    // Community detection
    var communities = LouvainCommunityDetection(agents);
    metrics.NumberOfCommunities = communities.Count;
    
    return metrics;
}
```

### 3.4 Convergence d'objectifs

**Définition** : Jusqu'à quel point agents partagent-ils des objectifs ?

```csharp
public class GoalConvergenceMetrics
{
    public float GlobalGoalAlignment { get; set; }    // % agents on same top goal
    public float GoalDiversity { get; set; }          // Shannon entropy of goal distribution
    public float CooperationPotential { get; set; }   // % agents capable of cooperation
    public Dictionary<string, float> GoalTypeCounts { get; set; }  // By goal type
}

// Calculation
public GoalConvergenceMetrics Calculate(List<Agent> agents)
{
    var metrics = new GoalConvergenceMetrics();
    
    var activeGoals = agents
        .SelectMany(a => a.goals.Where(g => g.status == GoalStatus.Active))
        .ToList();
    
    if (activeGoals.Count == 0)
        return metrics;
    
    // Most common goal
    var goalCounts = activeGoals
        .GroupBy(g => g.type)
        .OrderByDescending(g => g.Count())
        .ToList();
    
    var topGoalCount = goalCounts.First().Count();
    metrics.GlobalGoalAlignment = (float)topGoalCount / agents.Count;
    
    // Diversity
    var proportions = goalCounts.Select(g => (float)g.Count() / activeGoals.Count()).ToList();
    metrics.GoalDiversity = ShannonEntropy(proportions);
    
    // Cooperation potential (% with compatible goals)
    var compatiblePairs = 0;
    for (int i = 0; i < agents.Count; i++)
    {
        for (int j = i + 1; j < agents.Count; j++)
        {
            if (GoalsCompatible(agents[i].goals, agents[j].goals))
                compatiblePairs++;
        }
    }
    int totalPairs = agents.Count * (agents.Count - 1) / 2;
    metrics.CooperationPotential = (float)compatiblePairs / totalPairs;
    
    // Goal type distribution
    metrics.GoalTypeCounts = goalCounts.ToDictionary(
        g => g.Key.ToString(),
        g => (float)g.Count() / activeGoals.Count
    );
    
    return metrics;
}
```

### 3.5 Détection de boucles de rétroaction

**Définition** : Identifier cycles action→conséquence→décision.

```csharp
public class FeedbackLoopMetrics
{
    public int IdentifiedLoops { get; set; }          // Number of cycles detected
    public float LoopStrength { get; set; }           // Avg amplification factor
    public float SystemStability { get; set; }        // 1 - divergence from equilibrium
    public List<FeedbackLoop> CriticalLoops { get; set; }
}

public class FeedbackLoop
{
    public string Id { get; set; }
    public List<AgentAction> Cycle { get; set; }      // Chain of actions
    public float AmplificationFactor { get; set; }    // How much it reinforces
    public string Type { get; set; }                  // "positive", "negative"
}

// Simplified detection (full impl requires causal graph)
public FeedbackLoopMetrics Calculate(List<ExternalEvent> events, ulong windowTicks = 100)
{
    var metrics = new FeedbackLoopMetrics();
    var loops = new List<FeedbackLoop>();
    
    // Track action-consequence pairs
    foreach (var agent in world.agents)
    {
        var recentActions = events
            .Where(e => e.agentId == agent.id && e.tick >= currentTick - windowTicks)
            .ToList();
        
        // Simple heuristic: if agent repeats pattern, it's a loop
        var patterns = IdentifyPatterns(recentActions);
        
        foreach (var pattern in patterns)
        {
            if (pattern.frequency > 2)  // Appears multiple times
            {
                loops.Add(new FeedbackLoop
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = DetectLoopType(pattern),
                    AmplificationFactor = CalculateAmplification(pattern)
                });
            }
        }
    }
    
    metrics.IdentifiedLoops = loops.Count;
    metrics.LoopStrength = loops.Average(l => l.AmplificationFactor);
    metrics.CriticalLoops = loops.Where(l => l.AmplificationFactor > 1.5).ToList();
    
    return metrics;
}
```

### 3.6 Dynamique des groupes

**Définition** : Formation, dynamiques, efficacité des groupes.

```csharp
public class GroupDynamicsMetrics
{
    public int ActiveGroups { get; set; }
    public float AverageGroupSize { get; set; }
    public float AverageGroupLifetime { get; set; }   // Ticks
    public float GroupFormationRate { get; set; }     // New groups per 1000 ticks
    public float GroupDissolutionRate { get; set; }
    public float GroupObjectiveSuccessRate { get; set; }
    public float MemberTurnoverRate { get; set; }     // % members leave per 100 ticks
}

// Calculation
public GroupDynamicsMetrics Calculate(List<Group> groups, List<GroupEvent> events)
{
    var metrics = new GroupDynamicsMetrics();
    
    var activeGroups = groups.Where(g => g.status == GroupStatus.Active).ToList();
    metrics.ActiveGroups = activeGroups.Count;
    metrics.AverageGroupSize = activeGroups.Average(g => g.members.Count);
    
    // Formation/dissolution rates
    var formationEvents = events.Where(e => e.type == "GroupFormed").ToList();
    var dissolutionEvents = events.Where(e => e.type == "GroupDissolved").ToList();
    
    metrics.GroupFormationRate = formationEvents.Count / 10.0f;  // Per 1000 ticks
    metrics.GroupDissolutionRate = dissolutionEvents.Count / 10.0f;
    
    // Success rate
    var completedGroups = groups.Where(g => g.status == GroupStatus.Dissolved).ToList();
    var successful = completedGroups.Count(g => g.objectiveAchieved);
    metrics.GroupObjectiveSuccessRate = completedGroups.Count > 0 
        ? (float)successful / completedGroups.Count 
        : 0;
    
    // Turnover
    var totalMemberShips = groups.SelectMany(g => g.memberships).Count();
    var turnoverEvents = events.Where(e => e.type == "MemberLeft").Count();
    metrics.MemberTurnoverRate = totalMemberShips > 0 ? turnoverEvents / totalMemberShips : 0;
    
    return metrics;
}
```

### 3.7 Indicateurs d'émergence

**Définition** : Signaux d'émergence complexe.

```csharp
public class EmergenceIndicators
{
    public float EmergenceScore { get; set; }         // 0–1, composite metric
    public string[] DetectedPhenomena { get; set; }   // List of emergent behaviors
    public float SystemComplexity { get; set; }       // Kolmogorov complexity proxy
    public float UnpredictabilityIndex { get; set; }  // How surprising outcomes are
}

// Calculation
public EmergenceIndicators Calculate(
    CognitiveDiversityMetrics cog,
    InformationPropagationMetrics info,
    SocialComplexityMetrics social,
    FeedbackLoopMetrics feedback,
    GroupDynamicsMetrics groups)
{
    var indicators = new EmergenceIndicators();
    
    // Composite score
    indicators.EmergenceScore = (
        cog.BeliefDiversity * 0.15 +
        cog.GoalDiversity * 0.15 +
        info.InformationDiffusionSpeed * 0.10 +
        social.ClusteringCoefficient * 0.15 +
        feedback.LoopStrength * 0.20 +
        groups.ActiveGroups / 100f * 0.25  // Normalized
    ) / 5;  // Average
    
    // Detected phenomena
    var phenomena = new List<string>();
    
    if (social.NumberOfCommunities > 2)
        phenomena.Add("Community formation");
    
    if (feedback.IdentifiedLoops > 5)
        phenomena.Add("Complex feedback dynamics");
    
    if (cog.GoalConvergence > 0.7)
        phenomena.Add("Collective coordination");
    
    if (info.NetworkCentrality > 0.3)
        phenomena.Add("Information bottleneck");
    
    if (groups.ActiveGroups > 5 && groups.MemberTurnoverRate > 0.1)
        phenomena.Add("Organizational dynamics");
    
    indicators.DetectedPhenomena = phenomena.ToArray();
    
    // Complexity (proxy using entropy measures)
    indicators.SystemComplexity = (cog.BeliefDiversity + cog.GoalDiversity + 
                                   info.InformationDiffusionSpeed) / 3;
    
    // Unpredictability (variability in outcomes)
    indicators.UnpredictabilityIndex = feedback.LoopStrength * cog.DecisionVariability;
    
    return indicators;
}
```

---

## 4. Comparaison entre runs

### 4.1 Analyse comparative

```csharp
public class RunComparison
{
    public string RunIdA { get; set; }
    public string RunIdB { get; set; }
    
    // Differences in metrics (normalized distances)
    public float CognitiveDiversityDifference { get; set; }
    public float SocialComplexityDifference { get; set; }
    public float GoalConvergenceDifference { get; set; }
    public float EmergenceDifference { get; set; }
    
    // Reproducibility
    public bool IsReproducible { get; set; }          // Same seed + config = identical ?
    public float ReproducibilityScore { get; set; }   // 0–1
}

// Calculation
public RunComparison Compare(Run runA, Run runB)
{
    var comp = new RunComparison
    {
        RunIdA = runA.id,
        RunIdB = runB.id,
        IsReproducible = runA.seed == runB.seed && ConfigEqual(runA.config, runB.config)
    };
    
    // Normalized L2 distance
    comp.CognitiveDiversityDifference = MathF.Sqrt(
        MathF.Pow(runA.metrics.cognitiveDiv - runB.metrics.cognitiveDiv, 2)
    );
    
    comp.SocialComplexityDifference = MathF.Sqrt(
        MathF.Pow(runA.metrics.socialComplexity - runB.metrics.socialComplexity, 2)
    );
    
    // ... other metrics
    
    comp.ReproducibilityScore = comp.IsReproducible ? 1.0f : 
        1.0f - (comp.CognitiveDiversityDifference + comp.SocialComplexityDifference) / 2;
    
    return comp;
}
```

---

## 5. API REST

### 5.1 Endpoints

```
GET /api/runs
  → List all recorded runs

GET /api/runs/{id}
  → Full metrics for run id

GET /api/runs/{id}/metrics
  → Latest metrics (JSON)

GET /api/runs/{id}/export
  → Download metrics as CSV/JSON

GET /api/compare?a=run1&b=run2
  → Comparison of two runs

GET /api/beliefs/{agentId}
  → Agent's beliefs at current tick

GET /api/relationships/{agentId}
  → Agent's trust network

GET /api/groups
  → All active groups

GET /api/emergent-phenomena
  → List detected phenomena
```

### 5.2 Example response

```json
{
  "runId": "demo",
  "tick": 5000,
  "metrics": {
    "cognitiveDiversity": {
      "beliefDiversity": 0.75,
      "goalDiversity": 0.62,
      "decisionVariability": 0.58
    },
    "socialComplexity": {
      "averageTrustLevel": 0.52,
      "clusteringCoefficient": 0.34,
      "numberOfCommunities": 3
    },
    "emergenceIndicators": {
      "emergenceScore": 0.68,
      "detectedPhenomena": [
        "Community formation",
        "Organizational dynamics"
      ]
    }
  }
}
```

