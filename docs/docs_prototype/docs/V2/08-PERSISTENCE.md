# Persistance V2 (SQLite)

## 1. Justification

### 1.1 Pourquoi SQLite (et non JSON comme V1)

| Aspect | JSON (V1) | SQLite (V2) |
|--------|-----------|-----------|
| **Vitesse de requête** | O(n) fichier entier | Requêtes indexées O(1) |
| **1000 agents** | Fichier 100 Mo+ | Indexation efficace |
| **Chargements partiels** | Tout ou rien | Charger des données agent spécifiques |
| **Transactions** | Gestion manuelle JSON | ACID garanti |
| **Passage à l'échelle** | Lent pour les grands états | Gère facilement 100k+ enregistrements |
| **Analytique** | Analyse manuelle | Les requêtes SQL fonctionnent nativement |
| **Migrations** | Compat version manuelle | Versionnage de schéma intégré |
| **Lectures concurrentes** | Impossible | Plusieurs lecteurs en sécurité |

---

## 2. Schéma de base de données v2.0

### 2.1 Tables principales

#### `simulation_state`
```sql
CREATE TABLE simulation_state (
    tick INTEGER PRIMARY KEY,
    seed TEXT NOT NULL,
    world_time_minutes INTEGER,
    world_time_simulated_hours REAL,
    current_season TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    game_state TEXT  -- "running", "paused", "ended"
);
```

#### `agents`
```sql
CREATE TABLE agents (
    id TEXT PRIMARY KEY,
    name TEXT,
    species TEXT NOT NULL,
    position_x REAL NOT NULL,
    position_y REAL NOT NULL,
    health REAL CHECK(health >= 0 AND health <= 100),
    energy REAL CHECK(energy >= 0 AND energy <= 100),
    hunger REAL CHECK(hunger >= 0 AND hunger <= 100),
    thirst REAL CHECK(thirst >= 0 AND thirst <= 100),
    age INTEGER,
    
    -- Traits
    aggression REAL CHECK(aggression >= 0 AND aggression <= 1),
    sociability REAL CHECK(sociability >= 0 AND sociability <= 1),
    prudence REAL CHECK(prudence >= 0 AND prudence <= 1),
    ambition REAL CHECK(ambition >= 0 AND ambition <= 1),
    
    -- State
    is_alive BOOLEAN DEFAULT 1,
    created_tick INTEGER,
    died_tick INTEGER,
    
    FOREIGN KEY(species) REFERENCES species(name)
);
CREATE INDEX idx_agents_position ON agents(position_x, position_y);
CREATE INDEX idx_agents_alive ON agents(is_alive);
```

#### `agent_beliefs`
```sql
CREATE TABLE agent_beliefs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    agent_id TEXT NOT NULL,
    fact_type TEXT NOT NULL,  -- "position", "resource", "agent", "event"
    fact_description TEXT,
    confidence REAL CHECK(confidence >= 0 AND confidence <= 1),
    source TEXT,  -- "direct_perception", "memory", "communication", "inference"
    source_agent_id TEXT,  -- If from communication
    created_tick INTEGER NOT NULL,
    last_confirmed_tick INTEGER,
    expiry_tick INTEGER,
    
    FOREIGN KEY(agent_id) REFERENCES agents(id),
    FOREIGN KEY(source_agent_id) REFERENCES agents(id)
);
CREATE INDEX idx_beliefs_agent ON agent_beliefs(agent_id);
CREATE INDEX idx_beliefs_confidence ON agent_beliefs(agent_id, confidence DESC);
CREATE INDEX idx_beliefs_expiry ON agent_beliefs(expiry_tick);
```

#### `agent_memories`
```sql
CREATE TABLE agent_memories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    agent_id TEXT NOT NULL,
    type TEXT NOT NULL,  -- "observation", "event", "interaction"
    data_json TEXT NOT NULL,
    created_tick INTEGER NOT NULL,
    decay_rate REAL DEFAULT 0.01,
    current_confidence REAL CHECK(current_confidence >= 0 AND current_confidence <= 1),
    
    FOREIGN KEY(agent_id) REFERENCES agents(id)
);
CREATE INDEX idx_memories_agent ON agent_memories(agent_id);
CREATE INDEX idx_memories_type ON agent_memories(agent_id, type);
CREATE INDEX idx_memories_age ON agent_memories(created_tick DESC);
```

#### `agent_relationships`
```sql
CREATE TABLE agent_relationships (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    agent_id TEXT NOT NULL,
    target_agent_id TEXT NOT NULL,
    trust_level REAL CHECK(trust_level >= 0 AND trust_level <= 1),
    familiarity_count INTEGER DEFAULT 0,
    last_interaction_tick INTEGER,
    relationship_type TEXT,  -- "neutral", "ally", "friend", "enemy", "unknown"
    
    UNIQUE(agent_id, target_agent_id),
    FOREIGN KEY(agent_id) REFERENCES agents(id),
    FOREIGN KEY(target_agent_id) REFERENCES agents(id)
);
CREATE INDEX idx_relationships_agent ON agent_relationships(agent_id);
```

#### `groups`
```sql
CREATE TABLE groups (
    id TEXT PRIMARY KEY,
    name TEXT,
    leader_id TEXT NOT NULL,
    objective TEXT NOT NULL,  -- "hunting", "gathering", "defense", "settlement", etc.
    created_tick INTEGER NOT NULL,
    dissolved_tick INTEGER,
    status TEXT,  -- "active", "suspended", "dissolved"
    
    FOREIGN KEY(leader_id) REFERENCES agents(id)
);
CREATE INDEX idx_groups_status ON groups(status);
```

#### `group_memberships`
```sql
CREATE TABLE group_memberships (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    group_id TEXT NOT NULL,
    agent_id TEXT NOT NULL,
    role TEXT,  -- "leader", "scout", "gatherer", "hunter", "supporter"
    joined_tick INTEGER NOT NULL,
    left_tick INTEGER,
    
    UNIQUE(group_id, agent_id),
    FOREIGN KEY(group_id) REFERENCES groups(id),
    FOREIGN KEY(agent_id) REFERENCES agents(id)
);
CREATE INDEX idx_memberships_group ON group_memberships(group_id);
CREATE INDEX idx_memberships_agent ON group_memberships(agent_id);
```

#### `resources`
```sql
CREATE TABLE resources (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,  -- "food", "water", "wood", "mineral"
    position_x REAL NOT NULL,
    position_y REAL NOT NULL,
    quantity REAL NOT NULL CHECK(quantity >= 0),
    capacity REAL NOT NULL,
    regeneration_rate REAL DEFAULT 0,
    degradation_rate REAL DEFAULT 0,
    last_harvested_tick INTEGER,
    
    FOREIGN KEY(type) REFERENCES resource_types(name)
);
CREATE INDEX idx_resources_position ON resources(position_x, position_y);
CREATE INDEX idx_resources_type ON resources(type);
```

#### `obstacles`
```sql
CREATE TABLE obstacles (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,  -- "wall", "mountain", "cliff"
    position_x REAL NOT NULL,
    position_y REAL NOT NULL,
    width REAL NOT NULL,
    height REAL NOT NULL,
    passable BOOLEAN DEFAULT 0,
    
    FOREIGN KEY(type) REFERENCES obstacle_types(name)
);
CREATE INDEX idx_obstacles_position ON obstacles(position_x, position_y);
```

#### `events_log`
```sql
CREATE TABLE events_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tick INTEGER NOT NULL,
    type TEXT NOT NULL,  -- "AgentCreated", "AgentDied", "ActionStarted", "ResourceConsumed", etc.
    agent_id TEXT,
    data_json TEXT,
    
    FOREIGN KEY(agent_id) REFERENCES agents(id)
);
CREATE INDEX idx_events_tick ON events_log(tick);
CREATE INDEX idx_events_type ON events_log(type);
CREATE INDEX idx_events_agent ON events_log(agent_id);
```

#### `communication_log`
```sql
CREATE TABLE communication_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tick INTEGER NOT NULL,
    sender_id TEXT NOT NULL,
    receiver_id TEXT,
    message_type TEXT NOT NULL,  -- "Information", "Request", "Response", "Announcement", "Warning", "Trading"
    content_type TEXT,
    payload_json TEXT,
    confidence REAL CHECK(confidence >= 0 AND confidence <= 1),
    energy_cost REAL,
    
    FOREIGN KEY(sender_id) REFERENCES agents(id),
    FOREIGN KEY(receiver_id) REFERENCES agents(id)
);
CREATE INDEX idx_comms_sender ON communication_log(sender_id);
CREATE INDEX idx_comms_receiver ON communication_log(receiver_id);
CREATE INDEX idx_comms_tick ON communication_log(tick);
```

### 2.2 Tables de configuration

#### `species`
```sql
CREATE TABLE species (
    name TEXT PRIMARY KEY,
    perception_radius REAL,
    movement_speed REAL,
    energy_consumption_rate REAL,
    dehydration_rate REAL,
    hunger_rate REAL,
    base_metabolism REAL
);
```

#### `resource_types`
```sql
CREATE TABLE resource_types (
    name TEXT PRIMARY KEY,
    color TEXT,
    energy_per_unit REAL,
    hydration_per_unit REAL,
    is_consumable BOOLEAN
);
```

#### `obstacle_types`
```sql
CREATE TABLE obstacle_types (
    name TEXT PRIMARY KEY,
    color TEXT,
    is_permeable BOOLEAN
);
```

---

## 3. Opérations de sauvegarde/chargement

### 3.1 Procédure de sauvegarde

```csharp
public class SQLitePersistence
{
    private SQLiteConnection connection;
    
    public void Save(World world, string filePath)
    {
        using (var conn = new SQLiteConnection($"Data Source={filePath}"))
        {
            conn.Open();
            
            // Create schema if not exists
            InitializeSchema(conn);
            
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    // Save simulation state
                    SaveSimulationState(conn, world);
                    
                    // Save all agents
                    SaveAgents(conn, world.agents);
                    
                    // Save agent beliefs, memories, relationships
                    SaveAgentInternal(conn, world.agents);
                    
                    // Save groups & memberships
                    SaveGroups(conn, world.groups);
                    
                    // Save resources
                    SaveResources(conn, world.resources);
                    
                    // Save obstacles
                    SaveObstacles(conn, world.obstacles);
                    
                    // Save events log (last 1000 events)
                    SaveEventsLog(conn, world.eventLog);
                    
                    transaction.Commit();
                    
                    Logger.Info($"World saved to {filePath}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error($"Save failed: {ex.Message}");
                    throw;
                }
            }
        }
    }
    
    private void SaveSimulationState(SQLiteConnection conn, World world)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO simulation_state 
            (tick, seed, world_time_minutes, current_season, game_state)
            VALUES (@tick, @seed, @time, @season, @state)
        ";
        cmd.Parameters.AddWithValue("@tick", world.tick);
        cmd.Parameters.AddWithValue("@seed", world.seed);
        cmd.Parameters.AddWithValue("@time", world.simulatedTimeMinutes);
        cmd.Parameters.AddWithValue("@season", world.currentSeason.ToString());
        cmd.Parameters.AddWithValue("@state", "running");
        
        cmd.ExecuteNonQuery();
    }
    
    private void SaveAgents(SQLiteConnection conn, List<Agent> agents)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO agents 
            (id, name, species, position_x, position_y, health, energy, 
             hunger, thirst, age, aggression, sociability, prudence, ambition, 
             is_alive, created_tick, died_tick)
            VALUES (@id, @name, @species, @x, @y, @health, @energy, 
                    @hunger, @thirst, @age, @agg, @soc, @pru, @amb, 
                    @alive, @created, @died)
        ";
        
        foreach (var agent in agents)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", agent.id);
            cmd.Parameters.AddWithValue("@name", agent.name ?? "");
            cmd.Parameters.AddWithValue("@species", agent.species);
            cmd.Parameters.AddWithValue("@x", agent.position.x);
            cmd.Parameters.AddWithValue("@y", agent.position.y);
            cmd.Parameters.AddWithValue("@health", agent.state.health);
            cmd.Parameters.AddWithValue("@energy", agent.state.energy);
            cmd.Parameters.AddWithValue("@hunger", agent.state.hunger);
            cmd.Parameters.AddWithValue("@thirst", agent.state.thirst);
            cmd.Parameters.AddWithValue("@age", agent.age);
            cmd.Parameters.AddWithValue("@agg", agent.traits.aggression);
            cmd.Parameters.AddWithValue("@soc", agent.traits.sociability);
            cmd.Parameters.AddWithValue("@pru", agent.traits.prudence);
            cmd.Parameters.AddWithValue("@amb", agent.traits.ambition);
            cmd.Parameters.AddWithValue("@alive", agent.isAlive);
            cmd.Parameters.AddWithValue("@created", agent.createdTick);
            cmd.Parameters.AddWithValue("@died", agent.diedTick ?? (object)DBNull.Value);
            
            cmd.ExecuteNonQuery();
        }
    }
    
    // SaveAgentInternal(beliefs, memories, relationships), SaveGroups, SaveResources, etc.
    // Similar pattern for each entity type
}
```

### 3.2 Procédure de chargement

```csharp
public World Load(string filePath)
{
    using (var conn = new SQLiteConnection($"Data Source={filePath}"))
    {
        conn.Open();
        
        // Load simulation state
        var simState = LoadSimulationState(conn);
        var world = new World(seed: simState.seed, tick: simState.tick);
        
        // Load agents
        world.agents = LoadAgents(conn);
        
        // Load agent internals (beliefs, memories, relationships)
        foreach (var agent in world.agents)
        {
            LoadAgentBeliefs(conn, agent);
            LoadAgentMemories(conn, agent);
            LoadAgentRelationships(conn, agent);
        }
        
        // Load groups
        world.groups = LoadGroups(conn);
        
        // Load resources, obstacles
        world.resources = LoadResources(conn);
        world.obstacles = LoadObstacles(conn);
        
        Logger.Info($"World loaded from {filePath} at tick {world.tick}");
        
        return world;
    }
}
```

---

## 4. Garantie de déterminisme

### 4.1 Reproduction bit à bit

Tester le cycle sauvegarde/chargement :

```csharp
[Test]
public void SaveLoadProducesIdenticalResults()
{
    // Run 100 ticks
    world.RunTicks(100);
    var state100 = world.Snapshot();
    
    // Save
    persistence.Save(world, "test.db");
    
    // Load
    world = persistence.Load("test.db");
    
    // Run next 50 ticks
    world.RunTicks(50);
    var state150_fromContinue = world.Snapshot();
    
    // Run fresh without save
    world2.Seed = state100.seed;
    world2.RestoreState(state100);
    world2.RunTicks(50);
    var state150_fromFresh = world2.Snapshot();
    
    // Should be identical
    Assert.AreEqual(state150_fromContinue, state150_fromFresh);
}
```

### 4.2 Stabilité du PRNG

Le PRNG `xoshiro256**` doit être persisté :

```sql
CREATE TABLE prng_state (
    id INTEGER PRIMARY KEY,
    state_a BIGINT,
    state_b BIGINT,
    state_c BIGINT,
    state_d BIGINT
);
```

```csharp
// Save PRNG state
public void SavePRNGState(SQLiteConnection conn, PRNG prng)
{
    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT OR REPLACE INTO prng_state 
        (id, state_a, state_b, state_c, state_d)
        VALUES (1, @a, @b, @c, @d)
    ";
    cmd.Parameters.AddWithValue("@a", prng.state[0]);
    cmd.Parameters.AddWithValue("@b", prng.state[1]);
    cmd.Parameters.AddWithValue("@c", prng.state[2]);
    cmd.Parameters.AddWithValue("@d", prng.state[3]);
    
    cmd.ExecuteNonQuery();
}
```

---

## 5. Migration de schéma

### 5.1 Versionnage

```sql
CREATE TABLE schema_version (
    version INTEGER PRIMARY KEY,
    applied_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### 5.2 Stratégie de migration

```csharp
public class MigrationManager
{
    public void EnsureLatestSchema(SQLiteConnection conn)
    {
        int currentVersion = GetCurrentSchemaVersion(conn);
        
        if (currentVersion < 1)
        {
            ApplyMigration_v1_Initial(conn);
        }
        
        if (currentVersion < 2)
        {
            ApplyMigration_v2_AddGroups(conn);
        }
        
        // Future migrations...
    }
    
    private void ApplyMigration_v1_Initial(SQLiteConnection conn)
    {
        // Create all base tables
        ExecuteSQL(conn, SchemaV1);
        RecordMigration(conn, 1);
    }
    
    private void ApplyMigration_v2_AddGroups(SQLiteConnection conn)
    {
        // ALTER TABLE agent_beliefs ADD COLUMN ...
        ExecuteSQL(conn, MigrationSQL_v2);
        RecordMigration(conn, 2);
    }
}
```

---

## 6. Optimisation des performances

### 6.1 Stratégie d'indexation

```sql
-- Frequently queried
CREATE INDEX idx_agents_position ON agents(position_x, position_y);
CREATE INDEX idx_agents_alive ON agents(is_alive);

-- Belief searches
CREATE INDEX idx_beliefs_agent ON agent_beliefs(agent_id);
CREATE INDEX idx_beliefs_confidence ON agent_beliefs(agent_id, confidence DESC);

-- Relationship queries
CREATE INDEX idx_relationships_agent ON agent_relationships(agent_id);

-- Resource proximity
CREATE INDEX idx_resources_position ON resources(position_x, position_y);

-- Event analysis
CREATE INDEX idx_events_tick ON events_log(tick);
CREATE INDEX idx_events_type ON events_log(type);
```

### 6.2 Inserts par lots

```csharp
// Instead of 1000 individual INSERTs
using (var transaction = conn.BeginTransaction())
{
    foreach (var agent in agents)
    {
        InsertAgent(conn, agent);
    }
    transaction.Commit();
}

// Batching reduces roundtrips from 1000 to 1
```

### 6.3 Vacuum et optimisation

```csharp
public void OptimizeDatabase()
{
    using (var conn = new SQLiteConnection(connectionString))
    {
        conn.Open();
        
        // Rebuild indices
        conn.ExecuteNonQuery("REINDEX;");
        
        // Optimize query planner
        conn.ExecuteNonQuery("ANALYZE;");
        
        // Compact database
        conn.ExecuteNonQuery("VACUUM;");
    }
}
```

---

## 7. Configuration (config.json)

```json
{
  "persistence": {
    "enabled": true,
    "databasePath": "world_saves/",
    "defaultDatabase": "world.db",
    "autoSaveEveryNTicks": 1000,
    "maxBackups": 5,
    "compressionEnabled": false,
    "vacuumOnSave": true,
    "indexingStrategy": "full"
  }
}
```

---

## 8. Utilisation de l'API

```csharp
// Save
var persistence = new SQLitePersistence("world.db");
persistence.Save(world);

// Load
var world = persistence.Load("world.db");

// Auto-save every 1000 ticks
world.OnTickComplete += (tick) => {
    if (tick % 1000 == 0) {
        persistence.Save(world);
    }
};
```
