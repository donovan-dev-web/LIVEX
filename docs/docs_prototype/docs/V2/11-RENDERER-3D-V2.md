# Renderer 3D Godot V2 — Visualisation BDI

## 1. Vue générale

App Godot C# pour visualiser simulation V2 en temps réel.

**Caractéristiques V2 vs V1** :

- V1 : Simples sprites d'agents, feedback minimal
- V2 : Agents = agents cognitifs avec visualisation de croyances, heatmaps de confiance, coloration par groupe

---

## 2. Architecture Godot

```
res://
├── scenes/
│   ├── Main.tscn (scène root)
│   ├── World.tscn (canvas de simulation)
│   ├── Agent.tscn (prefab agent)
│   ├── Resource.tscn (prefab ressource)
│   ├── Obstacle.tscn (prefab obstacle)
│   ├── UI/
│   │   ├── HUD.tscn (UI superposée)
│   │   ├── BeliefViewer.tscn (croyances agent sélectionné)
│   │   ├── GroupPanel.tscn (info groupes)
│   │   └── CommunicationLog.tscn (historique messages)
│   └── Visuals/
│       ├── ParticleEffects.tscn (émotions, etc)
│       └── SelectionOutline.tscn
├── scripts/
│   ├── WorldRenderer.cs (orchestrer rendu)
│   ├── AgentRenderer.cs (visuels par agent)
│   ├── ResourceRenderer.cs
│   ├── HUDController.cs
│   ├── CameraController.cs
│   └── VisualizationManager.cs (croyances, confiance, groupes)
└── assets/
    ├── sprites/
    │   ├── agent.png
    │   ├── agent_selected.png
    │   └── emotions/
    │       ├── happy.png
    │       ├── hungry.png
    │       ├── scared.png
    │       └── thinking.png
    └── sounds/
        └── select.ogg
```

---

## 3. Visualisation de l'agent

### 3.1 Agent scene (GDScript + C#)

```gdscript
# Agent.tscn C# backing (AgentRenderer.cs)
public class AgentRenderer : Node2D
{
    private Agent _agent;
    private Sprite2D _sprite;
    private LineDrawer _headingLine;
    private Label _nameLabel;
    private ProgressBar _energyBar;
    
    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _headingLine = GetNode<LineDrawer>("HeadingLine");
        _nameLabel = GetNode<Label>("NameLabel");
        _energyBar = GetNode<ProgressBar>("EnergyBar");
    }
    
    public void SetAgent(Agent agent)
    {
        _agent = agent;
        _nameLabel.Text = agent.Name;
    }
    
    public override void _Process(double delta)
    {
        if (_agent == null) return;
        
        // Follow agent position
        GlobalPosition = new Vector2(_agent.Position.X, _agent.Position.Y);
        
        // Update heading line
        var headingEnd = _agent.Position + _agent.Heading.Normalized() * 15;
        _headingLine.Draw(_agent.Position, headingEnd);
        
        // Color by status/group
        UpdateColor();
        
        // Energy bar
        _energyBar.Value = _agent.Energy;
        
        // Selection outline
        if (IsSelected)
            ModulateColor = Colors.White;
        else
            ModulateColor = Colors.Gray;
    }
    
    private void UpdateColor()
    {
        // Color by group membership
        if (_agent.GroupIds.Count > 0)
        {
            var groupId = _agent.GroupIds.First();
            _sprite.Modulate = GetGroupColor(groupId);
        }
        else if (_agent.Energy < 20)
        {
            _sprite.Modulate = Colors.Red;  // Low energy = red
        }
        else if (_agent.Needs["Hunger"].Level > 80)
        {
            _sprite.Modulate = new Color(1, 0.5f, 0);  // Orange = hungry
        }
        else
        {
            _sprite.Modulate = Colors.Green;  // Healthy
        }
    }
    
    private Color GetGroupColor(string groupId)
    {
        // Cycle through colors per group
        var hash = groupId.GetHashCode();
        var hue = (hash % 360) / 360.0f;
        return Color.FromHsv(hue, 0.7f, 1.0f);
    }
    
    public void ShowBeliefs()
    {
        // Show speech bubble with top belief
        var topBelief = _agent.Beliefs.GetBeliefs(0.8f).FirstOrDefault();
        if (topBelief != null)
        {
            var bubble = new Label
            {
                Text = $"{topBelief.Fact.Predicate} ✓",
                AddThemeColorOverride = "font_color",
                LabelSettings = new LabelSettings { FontSize = 8 }
            };
            AddChild(bubble);
            GetTree().CreateTimer(2.0).Timeout += () => bubble.QueueFree();
        }
    }
}
```

### 3.2 Sélection et inspection d'agent

```csharp
public class WorldRenderer : Node2D
{
    private AgentRenderer _selectedAgent;
    private BeliefViewer _beliefViewer;
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            var pos = GetGlobalMousePosition();
            
            // Find agent at mouse position
            var agent = GetAgentAtPosition(pos);
            if (agent != null)
            {
                SelectAgent(agent);
            }
        }
    }
    
    private void SelectAgent(AgentRenderer renderer)
    {
        if (_selectedAgent != null)
            _selectedAgent.Modulate = Colors.Gray;
        
        _selectedAgent = renderer;
        _selectedAgent.Modulate = Colors.White;
        
        // Show belief viewer
        _beliefViewer.SetAgent(renderer.Agent);
    }
    
    private AgentRenderer GetAgentAtPosition(Vector2 pos)
    {
        // Raycast for agent
        var space = GetWorld2D().DirectSpaceState;
        var query = PhysicsShapeQueryParameters2D.New();
        query.Shape = CircleShape2D.New() { Radius = 5 };
        query.Transform = new Transform2D(0, pos);
        
        var results = space.IntersectShape(query);
        foreach (var result in results)
        {
            if (result["collider"] is Node2D collider)
                return collider.GetParent<AgentRenderer>();
        }
        return null;
    }
}
```

---

## 4. Visualisation des croyances

### 4.1 UI des bulles de croyances

```csharp
public class BeliefViewer : Panel
{
    private Agent _agent;
    private ItemList _beliefList;
    
    public override void _Ready()
    {
        _beliefList = GetNode<ItemList>("BeliefList");
    }
    
    public void SetAgent(Agent agent)
    {
        _agent = agent;
        RefreshBeliefs();
    }
    
    private void RefreshBeliefs()
    {
        _beliefList.Clear();
        
        var beliefs = _agent.Beliefs.GetBeliefs().OrderByDescending(b => b.Confidence);
        
        foreach (var belief in beliefs)
        {
            var icon = GetConfidenceIcon(belief.Confidence);
            var text = $"{belief.Fact.Subject}.{belief.Fact.Predicate} = {belief.Fact.Value}";
            var tooltip = $"Confidence: {belief.Confidence:P}\nSource: {belief.Source}\nAge: {belief.Age} ticks";
            
            _beliefList.AddItem($"{text}", icon);
            _beliefList.SetItemTooltip(_beliefList.ItemCount - 1, tooltip);
        }
    }
    
    private Texture2D GetConfidenceIcon(float confidence)
    {
        if (confidence > 0.8f)
            return GD.Load<Texture2D>("res://assets/sprites/confidence_high.png");
        if (confidence > 0.5f)
            return GD.Load<Texture2D>("res://assets/sprites/confidence_medium.png");
        return GD.Load<Texture2D>("res://assets/sprites/confidence_low.png");
    }
}
```

### 4.2 Heatmap des croyances (spatiale)

```csharp
public class BeliefHeatmapRenderer
{
    private World _world;
    private CanvasLayer _heatmapLayer;
    
    public void DrawBeliefHeatmap(string factPredicate, CanvasItem canvas)
    {
        // Create 2D grid heatmap
        var width = 50;
        var height = 50;
        var cellSize = 10;
        
        // Sample agent beliefs at grid positions
        var grid = new float[width, height];
        
        foreach (var agent in _world.Agents)
        {
            var belief = agent.Beliefs.GetBeliefs()
                .FirstOrDefault(b => b.Fact.Predicate == factPredicate);
            
            if (belief == null) continue;
            
            var gridX = (int)(agent.Position.X / cellSize);
            var gridY = (int)(agent.Position.Y / cellSize);
            
            if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height)
                grid[gridX, gridY] = belief.Confidence;
        }
        
        // Draw heatmap
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var confidence = grid[x, y];
                var color = Color.FromHsv(120 - (confidence * 120), 1, confidence);  // Red=low, Green=high
                
                canvas.DrawRect(
                    new Rect2(x * cellSize, y * cellSize, cellSize, cellSize),
                    color
                );
            }
        }
    }
}
```

---

## 5. Visualisation du réseau social

### 5.1 Superposition de la heatmap de confiance

```csharp
public class TrustHeatmapRenderer : CanvasLayer
{
    private World _world;
    private Texture2D _heatmapTexture;
    
    public void DrawTrustHeatmap()
    {
        // Generate texture from trust matrix
        var image = Image.Create(256, 256, false, Image.Format.Rgb8);
        
        for (int i = 0; i < _world.Agents.Count; i++)
        {
            for (int j = 0; j < _world.Agents.Count; j++)
            {
                var trust = _world.Agents[i].Relationships.GetTrust(_world.Agents[j].Id);
                
                var x = (i * 256) / _world.Agents.Count;
                var y = (j * 256) / _world.Agents.Count;
                
                // Red = low trust, Green = high trust
                var color = Color.FromHsv(120 - (trust * 120), 1, 1);
                image.SetPixel(x, y, color);
            }
        }
        
        _heatmapTexture = ImageTexture.CreateFromImage(image);
    }
    
    public override void _Draw()
    {
        DrawTexture(_heatmapTexture, Vector2.Zero);
    }
}
```

### 5.2 Flèches de relations

```csharp
public class RelationshipLineDrawer : Node2D
{
    private World _world;
    private float _minimumTrustToDisplay = 0.3f;
    
    public override void _Draw()
    {
        // Draw arrows between agents showing high-trust relationships
        foreach (var agent in _world.Agents)
        {
            var trustedAgents = agent.Relationships
                .GetAll()
                .Where(r => r.TrustLevel >= _minimumTrustToDisplay)
                .ToList();
            
            foreach (var rel in trustedAgents)
            {
                var target = _world.Agents.FirstOrDefault(a => a.Id == rel.TargetAgentId);
                if (target == null) continue;
                
                var color = rel.TrustLevel > 0.7 ? Colors.Green : Colors.Orange;
                
                DrawLine(
                    agent.Position,
                    target.Position,
                    color,
                    2.0f
                );
                
                // Arrow head
                DrawArrowHead(agent.Position, target.Position, color);
            }
        }
    }
}
```

---

## 6. Visualisation des groupes

### 6.1 Coloration des groupes

```csharp
public void UpdateAgentColor(Agent agent)
{
    if (agent.GroupIds.Count == 0)
    {
        // No group = default color by health
        agentRenderer.Modulate = GetHealthColor(agent);
    }
    else
    {
        // In group = group color
        var groupId = agent.GroupIds.First();
        agentRenderer.Modulate = GetGroupColor(groupId);
        
        // Highlight: bright for leader, dim for members
        if (groupManager.IsLeader(agent.Id, groupId))
            agentRenderer.Modulate = agentRenderer.Modulate.Lightened(0.3f);
    }
}

private Color GetGroupColor(string groupId)
{
    var hash = groupId.GetHashCode();
    var hue = (hash & 0xFF) / 255.0f * 360;
    return Color.FromHsv(hue, 0.8f, 1.0f);
}
```

### 6.2 Panneau des groupes

```csharp
public class GroupPanel : Panel
{
    private World _world;
    private ItemList _groupList;
    
    public override void _Ready()
    {
        _groupList = GetNode<ItemList>("GroupList");
    }
    
    public void RefreshGroups()
    {
        _groupList.Clear();
        
        foreach (var group in _world.Groups.Where(g => g.Status == GroupStatus.Active))
        {
            var text = $"{group.Name} ({group.Members.Count} members)";
            var color = GetGroupColor(group.Id);
            
            _groupList.AddItem(text);
            _groupList.SetItemCustomBgColor(_groupList.ItemCount - 1, color.Darkened(0.5f));
        }
    }
}
```

---

## 7. Visualisation des communications

### 7.1 Flèches de messages

```csharp
public class CommunicationVisualizer : CanvasLayer
{
    private Queue<(Vector2, Vector2, Color, float)> _messageArrows = new();
    
    public void OnMessageSent(Message msg, Vector2 from, Vector2 to)
    {
        var color = GetMessageColor(msg.Type);
        _messageArrows.Enqueue((from, to, color, 0.5f));  // 0.5s lifetime
    }
    
    public override void _Process(double delta)
    {
        var remaining = new Queue<(Vector2, Vector2, Color, float)>();
        
        while (_messageArrows.TryDequeue(out var arrow))
        {
            arrow.Item4 -= (float)delta;
            if (arrow.Item4 > 0)
            {
                remaining.Enqueue(arrow);
                // Draw arrow
                DrawArrow(arrow.Item1, arrow.Item2, arrow.Item3, arrow.Item4);
            }
        }
        
        _messageArrows = remaining;
    }
    
    private Color GetMessageColor(string messageType)
    {
        return messageType switch
        {
            "Warning" => Colors.Red,
            "Information" => Colors.White,
            "Request" => Colors.Yellow,
            "Trade" => Colors.Green,
            _ => Colors.Gray
        };
    }
}
```

---

## 8. Superposition HUD

```csharp
public class HUDController : CanvasLayer
{
    private Label _tickLabel;
    private Label _agentCountLabel;
    private Label _metricsLabel;
    private Metrics _currentMetrics;
    
    public override void _Ready()
    {
        _tickLabel = GetNode<Label>("TickLabel");
        _agentCountLabel = GetNode<Label>("AgentCountLabel");
        _metricsLabel = GetNode<Label>("MetricsLabel");
    }
    
    public void UpdateHUD(ulong tick, World world, Metrics metrics)
    {
        _tickLabel.Text = $"Tick: {tick}";
        _agentCountLabel.Text = $"Agents: {world.Agents.Count}";
        
        _metricsLabel.Text = $"""
            Emergence: {metrics.EmergenceIndicators.EmergenceScore:F2}
            Belief Diversity: {metrics.CognitiveDiversity.BeliefDiversity:F2}
            Phenomena: {string.Join(", ", metrics.EmergenceIndicators.DetectedPhenomena)}
        """;
    }
}
```

---

## 9. Contrôles de caméra

```csharp
public class CameraController : Camera2D
{
    private float _zoomSpeed = 0.1f;
    private float _minZoom = 0.5f;
    private float _maxZoom = 5.0f;
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
                Zoom = new Vector2(Zoom.X - _zoomSpeed, Zoom.Y - _zoomSpeed);
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                Zoom = new Vector2(Zoom.X + _zoomSpeed, Zoom.Y + _zoomSpeed);
            
            Zoom = Zoom.Clamp(new Vector2(_minZoom, _minZoom), new Vector2(_maxZoom, _maxZoom));
        }
        
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Space)
                CenterOnWorld();
        }
    }
    
    private void CenterOnWorld()
    {
        // Center camera on world
        GlobalPosition = new Vector2(250, 250);
        Zoom = Vector2.One;
    }
}
```

---

## 10. Checklist Godot V2

- [ ] Agent prefab with sprite, label, collision
- [ ] World scene with spatial grid rendering
- [ ] Belief viewer (selected agent)
- [ ] Social network heatmap overlay
- [ ] Group coloring (by group ID)
- [ ] Communication arrows (transient)
- [ ] HUD with metrics
- [ ] Camera zoom/pan controls
- [ ] Selection system (click agents)
- [ ] Emergent phenomena indicators (visual cues)

