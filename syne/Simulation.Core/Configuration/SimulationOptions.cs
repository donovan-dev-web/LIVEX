namespace Simulation.Core.Configuration;

/// <summary>
/// Options de simulation (Annexe H). Structure parallèle au fichier config.json :
/// les propriétés absentes d'un fichier partiel conservent les défauts intégrés.
/// </summary>
public sealed class SimulationOptions
{
    public SimulationSettings Simulation { get; set; } = new();
    public AgentSettings Agents { get; set; } = new();
    public ResourceSettings Resources { get; set; } = new();
    public CommunicationSettings Communication { get; set; } = new();
    public WorldSettings World { get; set; } = new();
    public RandomSettings Random { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
}

public sealed class SimulationSettings
{
    public int WorldWidth { get; set; } = 500;
    public int WorldHeight { get; set; } = 500;
    public int MaxTicks { get; set; } = 1_000_000;
    public int TicksPerSecond { get; set; } = 10;
    public int AutoSaveEveryNTicks { get; set; } = 1000;
    public int MaxBackups { get; set; } = 5;
}

public sealed class AgentSettings
{
    public int InitialCount { get; set; } = 100;
    public Dictionary<string, double> Traits { get; set; } = new()
    {
        ["bravery"] = 1.0,
        ["curiosity"] = 1.0,
        ["sociability"] = 1.0,
        ["greed"] = 1.0,
        ["pessimism"] = 1.0,
        ["aggressiveness"] = 1.0,
        ["strength"] = 1.0,
        ["speed"] = 1.0,
    };
    public NeedsSettings Needs { get; set; } = new();
    public PerceptionSettings Perception { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
}

public sealed class NeedsSettings
{
    public double HungerRate { get; set; } = 0.5;
    public double ThirstRate { get; set; } = 0.7;
    public double FatigueRate { get; set; } = 0.3;
}

public sealed class PerceptionSettings
{
    public int Radius { get; set; } = 30;
    public double ConfidenceDecay { get; set; } = 0.9;
}

public sealed class MemorySettings
{
    public int MaxCapacity { get; set; } = 1000;
    public double DecayRate { get; set; } = 0.01;
}

public sealed class ResourceSettings
{
    public ResourceSpec Food { get; set; } = new() { Initial = 100, RegenerationRate = 0, DegradationTick = 100 };
    public ResourceSpec Water { get; set; } = new() { Initial = 1000, RegenerationRate = 5 };
    public ResourceSpec Wood { get; set; } = new() { Initial = 50, RegenerationRate = 0.1 };
}

public sealed class ResourceSpec
{
    public int Initial { get; set; }
    public double RegenerationRate { get; set; }
    public int? DegradationTick { get; set; }
}

public sealed class CommunicationSettings
{
    public int MaxSendsPerTick { get; set; } = 5;
    public int MaxReceivesPerTick { get; set; } = 3;
    public double IncomprehensionRate { get; set; } = 0.05;
    public double TrustDecay { get; set; } = 0.9;
}

public sealed class WorldSettings
{
    public bool Seasons { get; set; }
    public bool Events { get; set; }
    public bool Obstacles { get; set; }
}

public sealed class RandomSettings
{
    public ulong Seed { get; set; } = 12_345;
    public string Engine { get; set; } = "xoshiro256**";
}

public sealed class PerformanceSettings
{
    public bool ParallelPerception { get; set; } = true;
    public bool SpatialGrid { get; set; } = true;
    public bool DecisionCaching { get; set; } = true;
    public bool BatchCommunication { get; set; } = true;
}