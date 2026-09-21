using Simulation.Core.World;

namespace Simulation.Core.Entities;

/// <summary>
/// Entité : identité, espèce, position, bornAt, traits (DATA_MODEL.md §3).
/// Les invariants position/traits sont validés à la création (SYNE-004).
/// </summary>
public sealed class Entity
{
    private Position _position;

    public Entity(EntityId id, string species, string? name, Position position, TraitSet traits, ulong bornAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        ArgumentNullException.ThrowIfNull(traits);

        Id = id;
        Species = species;
        Name = name;
        _position = position;
        Traits = traits;
        BornAt = bornAt;
    }

    public EntityId Id { get; }

    public string Species { get; }

    public string? Name { get; }

    public Position Position => _position;

    public TraitSet Traits { get; }

    /// <summary>Tick de naissance (0 = entité présente dès le départ de la simulation).</summary>
    public ulong BornAt { get; }

    public ulong Age(ulong currentTick) => currentTick >= BornAt ? currentTick - BornAt : 0;

    internal void SetPosition(Position position)
    {
        _position = position;
    }

    public override string ToString() => $"{Id} ({Species})@{Position.X:F1},{Position.Y:F1}";
}