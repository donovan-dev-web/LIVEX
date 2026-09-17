# Obstacles statiques — Pathfinding et collision

## 1. Vue générale

**Note V2** : Obstacles statiques uniquement (V3 pour dynamiques).

**Objectif** :
- Agents naviguent autour obstacles
- Pathfinding via moteur Godot
- Collisions avec ressources, obstacles, autres agents
- Statique = position/shape ne change pas

---

## 2. Modèle d'obstacle

```csharp
public class Obstacle
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }              // Width, Height
    public ObstacleShape Shape { get; set; }      // Rectangle, Circle, Polygon
    public bool IsWall { get; set; }              // If true, completely blocks
    public float Passability { get; set; }        // 0-1, how easy to pass through
    public ulong CreatedTick { get; set; }
    
    // For collision detection
    public Area2D GodotArea { get; set; }        // Godot physics representation
    
    public bool IsCollidingWith(Vector2 position, float radius)
    {
        // Simple AABB check
        return Math.Abs(Position.X - position.X) < (Size.X + radius) &&
               Math.Abs(Position.Y - position.Y) < (Size.Y + radius);
    }
}

public enum ObstacleShape
{
    Rectangle,
    Circle,
    Polygon,
    Custom
}
```

---

## 3. Intégration du pathfinding

### 3.1 Pathfinding intégré de Godot

Godot fournit `Navigation2D` pour le pathfinding. Nous l'intégrons :

```csharp
public class PathfindingSystem
{
    private Navigation2D _godotNav;  // Reference to Godot scene
    
    public List<Vector2> FindPath(Vector2 from, Vector2 to, float agentRadius = 5)
    {
        // Call Godot's pathfinding
        var path = _godotNav.GetSimplePath(from, to, false);
        
        if (path == null || path.Length == 0)
            return null;  // No path found
        
        return path.Cast<Vector2>().ToList();
    }
    
    public bool CanMoveTo(Vector2 from, Vector2 to, float agentRadius = 5)
    {
        // Quick check: is path clear?
        var path = FindPath(from, to, agentRadius);
        return path != null && path.Count > 0;
    }
}
```

### 3.2 Mise en cache des chemins

```csharp
public class PathCache
{
    private Dictionary<string, CachedPath> _cache;
    private const ulong PathExpiryTicks = 50;  // Paths valid for 50 ticks
    
    public List<Vector2> GetOrComputePath(
        Vector2 from, 
        Vector2 to, 
        PathfindingSystem pf,
        ulong currentTick)
    {
        var key = $"{from}→{to}";
        
        if (_cache.TryGetValue(key, out var cached) && 
            (currentTick - cached.ComputedAt) < PathExpiryTicks)
        {
            return cached.Path;  // Use cached
        }
        
        // Compute new path
        var path = pf.FindPath(from, to);
        _cache[key] = new CachedPath { Path = path, ComputedAt = currentTick };
        
        return path;
    }
}

public class CachedPath
{
    public List<Vector2> Path { get; set; }
    public ulong ComputedAt { get; set; }
}
```

---

## 4. Mouvement avec évitement de collision

### 4.1 Action de déplacement avec pathfinding

```csharp
public class MoveToAction : Action
{
    public Vector2 Target { get; set; }
    public List<Vector2> CurrentPath { get; set; }
    public int PathIndex { get; set; }
    
    public override bool CanExecute(Agent agent, World world)
    {
        // Can reach target?
        return world.Pathfinding.CanMoveTo(agent.Position, Target);
    }
    
    public override void Start(Agent agent, World world, ulong tick)
    {
        // Compute path to target
        CurrentPath = world.PathCache.GetOrComputePath(
            agent.Position, 
            Target, 
            world.Pathfinding, 
            tick
        );
        
        PathIndex = 0;
        Status = ActionStatus.Executing;
    }
    
    public override void Update(Agent agent, World world, ulong tick)
    {
        if (PathIndex >= CurrentPath.Count)
        {
            Complete();
            return;
        }
        
        // Next waypoint
        var waypoint = CurrentPath[PathIndex];
        var direction = (waypoint - agent.Position).Normalized();
        var moveDistance = agent.Traits.MovementSpeed * World.DeltaTime;
        
        var newPosition = agent.Position + direction * moveDistance;
        
        // Check collision
        if (!CheckCollision(agent, newPosition, world))
        {
            agent.Position = newPosition;
            agent.Heading = direction;
        }
        else
        {
            // Blocked, try to find alternate path
            Replan(agent, world, tick);
        }
        
        // Check if reached waypoint
        if (Vector2.Distance(agent.Position, waypoint) < 2)
        {
            PathIndex++;
        }
    }
    
    private bool CheckCollision(Agent agent, Vector2 newPos, World world)
    {
        var agentRadius = 2.5f;  // Agent collision radius
        
        // Check obstacles
        foreach (var obstacle in world.Obstacles)
        {
            if (obstacle.IsCollidingWith(newPos, agentRadius))
                return true;
        }
        
        // Check other agents
        var nearby = world.SpatialGrid.QueryRadius(newPos, agentRadius * 2);
        foreach (var other in nearby.OfType<Agent>())
        {
            if (other.Id != agent.Id && 
                Vector2.Distance(newPos, other.Position) < agentRadius * 2)
                return true;
        }
        
        return false;
    }
    
    private void Replan(Agent agent, World world, ulong tick)
    {
        // Find alternate route
        CurrentPath = world.PathCache.GetOrComputePath(
            agent.Position,
            Target,
            world.Pathfinding,
            tick
        );
        PathIndex = 0;
    }
}
```

---

## 5. Obstacle dans la scène Godot

### 5.1 Configuration de Navigation2D

Dans l'éditeur Godot :

```
Scene: WorldScene
├── Navigation2D (root, handles pathfinding)
│   ├── TileMap (walkable areas)
│   │   └── Built from obstacles
│   ├── StaticBody2D
│   │   └── Polygon2D (obstacle shape)
│   └── StaticBody2D
│       └── CollisionPolygon2D
```

### 5.2 Code C# pour enregistrer les obstacles

```csharp
public class ObstacleRegistry
{
    private Navigation2D _godotNav;
    
    public void RegisterObstacle(Obstacle obs)
    {
        if (obs.Shape == ObstacleShape.Rectangle)
        {
            var polygon = new Polygon2D
            {
                Polygon = GetRectanglePoints(obs.Position, obs.Size)
            };
            
            var body = new StaticBody2D();
            body.AddChild(polygon);
            body.AddChild(new CollisionPolygon2D { Polygon = polygon.Polygon });
            
            _godotNav.AddChild(body);
        }
        else if (obs.Shape == ObstacleShape.Circle)
        {
            var circle = new CircleShape2D { Radius = obs.Size.X };
            var body = new StaticBody2D();
            body.AddChild(circle);
            _godotNav.AddChild(body);
        }
    }
    
    private Vector2[] GetRectanglePoints(Vector2 center, Vector2 size)
    {
        return new[]
        {
            center - size / 2,
            new Vector2(center.X + size.X / 2, center.Y - size.Y / 2),
            center + size / 2,
            new Vector2(center.X - size.X / 2, center.Y + size.Y / 2)
        };
    }
}
```

---

## 6. Stockage des obstacles en persistance

### 6.1 Schéma SQLite

```sql
CREATE TABLE obstacles (
    id TEXT PRIMARY KEY,
    name TEXT,
    position_x REAL,
    position_y REAL,
    size_x REAL,
    size_y REAL,
    shape TEXT,           -- 'rectangle', 'circle', etc.
    is_wall BOOLEAN,
    passability REAL,
    created_tick INTEGER,
    FOREIGN KEY (simulation_id) REFERENCES simulation_state(id)
);

CREATE INDEX idx_obstacles_position ON obstacles(position_x, position_y);
```

### 6.2 Sauvegarde/Chargement

```csharp
public void SaveObstacles(List<Obstacle> obstacles, SQLiteConnection conn)
{
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            INSERT INTO obstacles (id, name, position_x, position_y, size_x, size_y, shape, is_wall, passability, created_tick)
            VALUES (@id, @name, @px, @py, @sx, @sy, @shape, @wall, @pass, @tick)
        ";
        
        foreach (var obs in obstacles)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", obs.Id);
            cmd.Parameters.AddWithValue("@name", obs.Name);
            cmd.Parameters.AddWithValue("@px", obs.Position.X);
            cmd.Parameters.AddWithValue("@py", obs.Position.Y);
            cmd.Parameters.AddWithValue("@sx", obs.Size.X);
            cmd.Parameters.AddWithValue("@sy", obs.Size.Y);
            cmd.Parameters.AddWithValue("@shape", obs.Shape.ToString());
            cmd.Parameters.AddWithValue("@wall", obs.IsWall);
            cmd.Parameters.AddWithValue("@pass", obs.Passability);
            cmd.Parameters.AddWithValue("@tick", obs.CreatedTick);
            
            cmd.ExecuteNonQuery();
        }
    }
}

public List<Obstacle> LoadObstacles(SQLiteConnection conn)
{
    var obstacles = new List<Obstacle>();
    
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT * FROM obstacles";
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                obstacles.Add(new Obstacle
                {
                    Id = reader["id"].ToString(),
                    Name = reader["name"].ToString(),
                    Position = new Vector2(
                        float.Parse(reader["position_x"].ToString()),
                        float.Parse(reader["position_y"].ToString())
                    ),
                    Size = new Vector2(
                        float.Parse(reader["size_x"].ToString()),
                        float.Parse(reader["size_y"].ToString())
                    ),
                    Shape = Enum.Parse<ObstacleShape>(reader["shape"].ToString()),
                    IsWall = bool.Parse(reader["is_wall"].ToString()),
                    Passability = float.Parse(reader["passability"].ToString()),
                    CreatedTick = ulong.Parse(reader["created_tick"].ToString())
                });
            }
        }
    }
    
    return obstacles;
}
```

---

## 7. Perception et obstacles

### 7.1 Blocage de la ligne de visée

Les agents ne peuvent pas percevoir à travers les murs.

```csharp
public class PerceptionSystem
{
    public List<Observation> Perceive(Agent agent, World world, ulong tick)
    {
        var observations = new List<Observation>();
        var nearby = world.SpatialGrid.QueryRadius(agent.Position, SensorRadius);
        
        foreach (var entity in nearby)
        {
            // Check line-of-sight
            if (!HasLineOfSight(agent, entity, world))
                continue;  // Blocked by obstacle
            
            var obs = CreateObservation(agent, entity, tick);
            observations.Add(obs);
        }
        
        return observations;
    }
    
    private bool HasLineOfSight(Agent observer, Entity target, World world)
    {
        var line = new LineSegment(observer.Position, target.Position);
        
        // Check each obstacle
        foreach (var obstacle in world.Obstacles)
        {
            if (obstacle.IsWall && LineSegmentIntersect(line, obstacle))
                return false;  // Blocked
        }
        
        return true;
    }
    
    private bool LineSegmentIntersect(LineSegment line, Obstacle obstacle)
    {
        // AABB intersection for rectangle obstacle
        if (obstacle.Shape == ObstacleShape.Rectangle)
        {
            var rect = new Rect2(
                obstacle.Position - obstacle.Size / 2,
                obstacle.Size
            );
            return rect.HasPoint(line.Start) || 
                   rect.HasPoint(line.End) ||
                   LineIntersectsRect(line, rect);
        }
        
        return false;
    }
}
```

---

## 8. Obstacles dans l'analyseur

### 8.1 Suivi de l'impact sur la navigation

```csharp
public class ObstacleAnalytics
{
    public int TotalObstacles { get; set; }
    public float AveragePathLength { get; set; }     // Longer paths = more obstacles
    public float NavigationDifficulty { get; set; }  // 0-1, how much obstacles impede
    public int PathReplanCount { get; set; }         // How often agents replan
}

public void ComputeObstacleImpact(World world, List<Agent> agents)
{
    var analytics = new ObstacleAnalytics();
    
    analytics.TotalObstacles = world.Obstacles.Count;
    
    // Average path length (without obstacles vs with)
    var directPaths = agents.Select(a => Vector2.Distance(a.Position, a.GoalPosition)).Average();
    var actualPaths = agents.Where(a => a.CurrentAction is MoveToAction)
        .Select(a => (a.CurrentAction as MoveToAction).CurrentPath.Sum(p => Vector2.Distance(a.Position, p)))
        .Average();
    
    analytics.AveragePathLength = actualPaths;
    analytics.NavigationDifficulty = actualPaths / (directPaths + 0.001f);  // Ratio
    
    // Replan tracking
    analytics.PathReplanCount = agents
        .OfType<MoveToAction>()
        .Sum(a => a.ReplannedCount);
}
```

---

## 9. Cas de test

```csharp
[TestClass]
public class ObstacleTests
{
    [TestMethod]
    public void AABB_Collision_Detection()
    {
        var obstacle = new Obstacle
        {
            Position = new Vector2(50, 50),
            Size = new Vector2(20, 20)
        };
        
        Assert.IsTrue(obstacle.IsCollidingWith(new Vector2(50, 50), 5));
        Assert.IsTrue(obstacle.IsCollidingWith(new Vector2(55, 50), 5));
        Assert.IsFalse(obstacle.IsCollidingWith(new Vector2(100, 100), 5));
    }
    
    [TestMethod]
    public void Pathfinding_AroundObstacle()
    {
        var world = new World();
        var obstacle = new Obstacle { Position = new Vector2(50, 50), Size = new Vector2(20, 20) };
        world.AddObstacle(obstacle);
        
        var path = world.Pathfinding.FindPath(new Vector2(0, 0), new Vector2(100, 100));
        
        Assert.IsNotNull(path);
        Assert.IsTrue(path.Count > 2);  // Longer than direct path
    }
    
    [TestMethod]
    public void LineOfSight_Blocked()
    {
        var observer = new Agent { Position = new Vector2(0, 0) };
        var target = new Agent { Position = new Vector2(100, 0) };
        var wall = new Obstacle 
        { 
            Position = new Vector2(50, 0),
            IsWall = true,
            Size = new Vector2(10, 50)
        };
        
        var world = new World();
        world.AddObstacle(wall);
        
        var obs = observer.Perception.Perceive(observer, world, 0);
        
        Assert.IsFalse(obs.Any(o => o.EntityId == target.Id));  // Target blocked
    }
}
```

