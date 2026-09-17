# Web UI V2 — Interface d'analyse d'émergence

## 1. Vue générale

App React + TypeScript pour monitorer simulation V2 en temps réel.

**Composants principaux** :

- **Tableau de bord** (métriques synthétiques)
- **Inspecteur d'agent** (croyances, objectifs, décisions par agent)
- **Réseau social** (graph de confiance)
- **Explorateur de groupes** (dynamique coalition)
- **Heatmap de communication** (qui parle à qui)
- **Détecteur d'émergence** (phénomènes complexes)
- **Traceur de décisions** (pipeline BDI par agent)
- **Contrôles de scénario** (play, pause, step)

---

## 2. Architecture des composants

```
src/
├── components/
│   ├── Dashboard/
│   │   ├── DashboardPage.tsx
│   │   ├── MetricsPanel.tsx
│   │   ├── KPICards.tsx
│   │   └── TimelineChart.tsx
│   ├── AgentInspector/
│   │   ├── AgentInspector.tsx
│   │   ├── BeliefPanel.tsx
│   │   ├── GoalPanel.tsx
│   │   ├── DecisionBreakdown.tsx
│   │   └── ActionTimeline.tsx
│   ├── SocialNetwork/
│   │   ├── SocialGraph.tsx
│   │   ├── TrustMatrix.tsx
│   │   └── RelationshipExplorer.tsx
│   ├── GroupExplorer/
│   │   ├── GroupList.tsx
│   │   ├── GroupDetail.tsx
│   │   └── MembershipTree.tsx
│   ├── Communication/
│   │   ├── MessageHeatmap.tsx
│   │   ├── MessageLog.tsx
│   │   └── DiffusionVisualization.tsx
│   └── Controls/
│       ├── SimulationControls.tsx
│       ├── SpeedControl.tsx
│       └── RecordingPanel.tsx
├── hooks/
│   ├── useSimulationMetrics.ts
│   ├── useSocialNetwork.ts
│   └── useWebSocket.ts
├── types/
│   ├── simulation.ts
│   └── metrics.ts
└── services/
    ├── api.ts
    └── websocket.ts
```

---

## 3. Composants clés en détail

### 3.1 Tableau de bord

```tsx
export const Dashboard = () => {
    const [metrics, setMetrics] = useState(null);
    const [phenomena, setPhenomena] = useState([]);
    
    useEffect(() => {
        const ws = new WebSocket('ws://localhost:5180/metrics');
        
        ws.onmessage = (event) => {
            const data = JSON.parse(event.data);
            setMetrics(data);
            setPhenomena(data.detectedPhenomena);
        };
        
        return () => ws.close();
    }, []);
    
    return (
        <div className="dashboard">
            <h1>Moniteur de simulation V2</h1>
            
            <KPICards metrics={metrics} />
            
            <div className="grid">
                <Panel title="Diversité cognitive">
                    <MetricGauge
                        value={metrics?.cognitiveDiversity?.beliefDiversity ?? 0}
                        max={1}
                        label="Diversité des croyances (entropie Shannon)"
                    />
                    <MetricGauge
                        value={metrics?.cognitiveDiversity?.goalDiversity ?? 0}
                        max={1}
                        label="Diversité des objectifs"
                    />
                </Panel>
                
                <Panel title="Phénomènes émergents">
                    <div className="phenomena-list">
                        {phenomena.map((p) => (
                            <PhenomenonBadge key={p} label={p} />
                        ))}
                    </div>
                </Panel>
                
                <Panel title="Complexité sociale">
                    <MetricGauge
                        value={metrics?.socialComplexity?.clusteringCoefficient ?? 0}
                        max={1}
                        label="Coefficient de clustering"
                    />
                    <div>Communautés : {metrics?.socialComplexity?.numberOfCommunities ?? 0}</div>
                </Panel>
                
                <Panel title="Propagation d'information">
                    <MetricGauge
                        value={metrics?.infoPropagation?.diffusionSpeed ?? 0}
                        max={100}
                        label="Vitesse de diffusion (ticks pour 80%)"
                    />
                </Panel>
            </div>
            
            <TimelineChart metrics={metrics} />
        </div>
    );
};

const KPICards = ({ metrics }) => (
    <div className="kpi-grid">
        <KPICard
            label="Agents actifs"
            value={metrics?.agentCount ?? 0}
            unit="agents"
        />
        <KPICard
            label="Score d'émergence"
            value={metrics?.emergenceIndicators?.emergenceScore?.toFixed(2) ?? "—"}
            unit="0-1"
            trend={metrics?.emergenceScore?.trend}
        />
        <KPICard
            label="Groupes actifs"
            value={metrics?.groupDynamics?.activeGroups ?? 0}
            unit="groupes"
        />
        <KPICard
            label="Messages/Tick"
            value={metrics?.infoPropagation?.messageVolume?.toFixed(1) ?? 0}
            unit="msg/agent"
        />
    </div>
);
```

### 3.2 Inspecteur d'agent

```tsx
export const AgentInspector = ({ agentId }: { agentId: string }) => {
    const [agent, setAgent] = useState(null);
    const [decision, setDecision] = useState(null);
    
    useEffect(() => {
        const fetch_agent = async () => {
            const res = await fetch(`/api/agents/${agentId}`);
            const data = await res.json();
            setAgent(data);
        };
        
        fetch_agent();
        const interval = setInterval(fetch_agent, 500);  // Rafraîchir chaque 500ms
        return () => clearInterval(interval);
    }, [agentId]);
    
    if (!agent) return <div>Chargement...</div>;
    
    return (
        <div className="agent-inspector">
            <h2>Agent : {agent.name} ({agentId})</h2>
            
            <div className="grid">
                <Panel title="Vitalité">
                    <ProgressBar label="Énergie" value={agent.energy} max={100} />
                    <ProgressBar label="Fatigue" value={agent.needs.fatigue} max={100} />
                    <p>Position : ({agent.position.x.toFixed(1)}, {agent.position.y.toFixed(1)})</p>
                    <p>Statut : {agent.status}</p>
                </Panel>
                
                <BeliefPanel agent={agent} />
                
                <Panel title="Besoins">
                    {Object.entries(agent.needs).map(([name, level]) => (
                        <ProgressBar
                            key={name}
                            label={name}
                            value={level}
                            max={100}
                        />
                    ))}
                </Panel>
                
                <GoalPanel agent={agent} />
            </div>
            
            <DecisionBreakdown agent={agent} />
            
            <ActionTimeline agent={agent} />
        </div>
    );
};

const BeliefPanel = ({ agent }) => (
    <Panel title="Croyances (Top 10)">
        <table>
            <thead>
                <tr>
                    <th>Fait</th>
                    <th>Confiance</th>
                    <th>Source</th>
                    <th>Âge</th>
                </tr>
            </thead>
            <tbody>
                {agent.beliefs.slice(0, 10).map((b) => (
                    <tr key={b.id}>
                        <td>{b.fact.predicate} = {JSON.stringify(b.fact.value)}</td>
                        <td><ConfidenceMeter value={b.confidence} /></td>
                        <td>{b.source}</td>
                        <td>{b.age} ticks</td>
                    </tr>
                ))}
            </tbody>
        </table>
    </Panel>
);

const GoalPanel = ({ agent }) => (
    <Panel title="Objectifs actifs">
        {agent.goals
            .filter((g) => g.status === "Active")
            .map((g) => (
                <div key={g.id} className="goal-item">
                    <strong>{g.type}</strong> (priorité : {g.priority.toFixed(2)})
                    <p>{g.description}</p>
                </div>
            ))}
    </Panel>
);

const DecisionBreakdown = ({ agent }) => (
    <Panel title="Dernière décision">
        {agent.lastDecision && (
            <div>
                <p><strong>Action choisie :</strong> {agent.lastDecision.chosenAction}</p>
                <p><strong>Score d'utilité :</strong> {agent.lastDecision.utility.toFixed(2)}</p>
                
                <h4>Actions évaluées :</h4>
                <table>
                    <thead>
                        <tr>
                            <th>Action</th>
                            <th>Utilité</th>
                            <th>Sélectionnée</th>
                        </tr>
                    </thead>
                    <tbody>
                        {Object.entries(agent.lastDecision.actionScores).map(([action, score]) => (
                            <tr key={action}
                                className={action === agent.lastDecision.chosenAction ? 'selected' : ''}>
                                <td>{action}</td>
                                <td>{(score as number).toFixed(2)}</td>
                                <td>
                                    {action === agent.lastDecision.chosenAction ? '✓' : ''}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        )}
    </Panel>
);
```

### 3.3 Graphe du réseau social

```tsx
import * as d3 from 'd3';

export const SocialGraph = () => {
    const [data, setData] = useState(null);
    const svgRef = useRef(null);
    
    useEffect(() => {
        const fetchNetwork = async () => {
            const res = await fetch('/api/relationships');
            const json = await res.json();
            setData(json);
        };
        
        fetchNetwork();
        const interval = setInterval(fetchNetwork, 2000);  // Rafraîchir toutes les 2s
        return () => clearInterval(interval);
    }, []);
    
    useEffect(() => {
        if (!data || !svgRef.current) return;
        
        const width = 800;
        const height = 600;
        
        const svg = d3.select(svgRef.current)
            .attr('width', width)
            .attr('height', height);
        
        // Simulation de forces
        const simulation = d3.forceSimulation(data.nodes)
            .force('link', d3.forceLink(data.links).distance(80))
            .force('charge', d3.forceManyBody().strength(-300))
            .force('center', d3.forceCenter(width / 2, height / 2));
        
        // Liens (relations de confiance)
        const links = svg.append('g')
            .selectAll('line')
            .data(data.links)
            .enter()
            .append('line')
            .attr('stroke', (d) => {
                const trust = d.trust;
                if (trust > 0.7) return 'green';
                if (trust > 0.4) return 'orange';
                return 'red';
            })
            .attr('stroke-width', (d) => d.trust * 3);
        
        // Nœuds (agents)
        const nodes = svg.append('g')
            .selectAll('circle')
            .data(data.nodes)
            .enter()
            .append('circle')
            .attr('r', 8)
            .attr('fill', (d) => d.color)
            .call(d3.drag()
                .on('start', dragStarted)
                .on('drag', dragged)
                .on('end', dragEnded));
        
        // Étiquettes
        const labels = svg.append('g')
            .selectAll('text')
            .data(data.nodes)
            .enter()
            .append('text')
            .attr('text-anchor', 'middle')
            .attr('dy', '-8px')
            .text((d) => d.id);
        
        simulation.on('tick', () => {
            links
                .attr('x1', (d) => d.source.x)
                .attr('y1', (d) => d.source.y)
                .attr('x2', (d) => d.target.x)
                .attr('y2', (d) => d.target.y);
            
            nodes
                .attr('cx', (d) => d.x)
                .attr('cy', (d) => d.y);
            
            labels
                .attr('x', (d) => d.x)
                .attr('y', (d) => d.y);
        });
        
        function dragStarted(event, d) {
            if (!event.active) simulation.alphaTarget(0.3).restart();
            d.fx = d.x;
            d.fy = d.y;
        }
        
        function dragged(event, d) {
            d.fx = event.x;
            d.fy = event.y;
        }
        
        function dragEnded(event, d) {
            if (!event.active) simulation.alphaTarget(0);
            d.fx = null;
            d.fy = null;
        }
    }, [data]);
    
    return <svg ref={svgRef} />;
};
```

### 3.4 Heatmap de communication

```tsx
export const CommunicationHeatmap = () => {
    const [heatmap, setHeatmap] = useState(null);
    
    useEffect(() => {
        const fetchHeatmap = async () => {
            const res = await fetch('/api/communication-heatmap');
            const json = await res.json();
            setHeatmap(json);
        };
        
        fetchHeatmap();
        const interval = setInterval(fetchHeatmap, 1000);
        return () => clearInterval(interval);
    }, []);
    
    if (!heatmap) return <div>Loading...</div>;
    
    return (
        <div className="heatmap">
            <h3>Communication Heatmap (Who talks to whom)</h3>
            
            <table className="heatmap-table">
                <thead>
                    <tr>
                        <th></th>
                        {heatmap.agents.map((a) => (
                            <th key={a} style={{ writingMode: 'vertical-rl' }}>
                                {a}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {heatmap.agents.map((sender, i) => (
                        <tr key={sender}>
                            <td><strong>{sender}</strong></td>
                            {heatmap.agents.map((receiver, j) => {
                                const value = heatmap.matrix[i][j];
                                const intensity = value / heatmap.max;  // Normalize
                                const color = `rgba(255, 0, 0, ${intensity})`;
                                
                                return (
                                    <td
                                        key={receiver}
                                        style={{ backgroundColor: color, textAlign: 'center' }}
                                        title={`${sender} → ${receiver}: ${value} messages`}
                                    >
                                        {value > 0 ? value : '-'}
                                    </td>
                                );
                            })}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};
```

---

## 4. WebSocket hooks

```typescript
export const useSimulationMetrics = () => {
    const [metrics, setMetrics] = useState(null);
    
    useEffect(() => {
        const ws = new WebSocket('ws://localhost:5180/metrics');
        
        ws.onopen = () => {
            console.log('Connected to metrics stream');
        };
        
        ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                setMetrics(data);
            } catch (e) {
                console.error('Failed to parse metrics:', e);
            }
        };
        
        ws.onerror = (error) => {
            console.error('WebSocket error:', error);
        };
        
        return () => ws.close();
    }, []);
    
    return metrics;
};

export const useSocialNetwork = () => {
    const [network, setNetwork] = useState(null);
    
    useEffect(() => {
        const fetch_network = async () => {
            try {
                const res = await fetch('/api/relationships');
                const json = await res.json();
                setNetwork(json);
            } catch (e) {
                console.error('Failed to fetch network:', e);
            }
        };
        
        fetch_network();
        const interval = setInterval(fetch_network, 2000);
        return () => clearInterval(interval);
    }, []);
    
    return network;
};
```

---

## 5. Types (TypeScript)

```typescript
export interface Agent {
    id: string;
    name: string;
    position: Vector2;
    energy: number;
    status: AgentStatus;
    needs: Record<string, number>;
    beliefs: Belief[];
    goals: Goal[];
    lastDecision: DecisionRecord;
}

export interface Belief {
    id: string;
    fact: Fact;
    confidence: number;
    source: string;
    age: number;
}

export interface Fact {
    id: string;
    subject: string;
    predicate: string;
    value: any;
}

export interface Goal {
    id: string;
    type: string;
    priority: number;
    status: GoalStatus;
    description: string;
}

export interface DecisionRecord {
    tick: number;
    chosenAction: string;
    utility: number;
    actionScores: Record<string, number>;
}

export interface Metrics {
    tick: number;
    agentCount: number;
    cognitiveDiversity: CognitiveDiversityMetrics;
    socialComplexity: SocialComplexityMetrics;
    infoPropagation: InformationPropagationMetrics;
    emergenceIndicators: EmergenceIndicators;
    groupDynamics: GroupDynamicsMetrics;
    detectedPhenomena: string[];
}
```

---

## 6. Styling (Tailwind CSS)

```tsx
// dashboard.css
.dashboard {
    @apply p-8 bg-gray-900 text-white min-h-screen;
}

.kpi-grid {
    @apply grid grid-cols-4 gap-4 mb-8;
}

.grid {
    @apply grid grid-cols-2 gap-6 mb-8;
}

.panel {
    @apply bg-gray-800 border border-gray-700 rounded-lg p-4;
}

.panel h3 {
    @apply text-lg font-bold mb-4 border-b border-gray-700 pb-2;
}

.goal-item {
    @apply mb-3 pb-3 border-b border-gray-700 last:border-b-0;
}

.heatmap-table {
    @apply w-full border-collapse;
}

.heatmap-table td, .heatmap-table th {
    @apply border border-gray-600 p-2 text-center text-sm;
}
```

