namespace Simulation.Core.Entities;

/// <summary>
/// Identifiant d'entité (ulong séquentiel déterministe, DATA_MODEL.md §3.1).
/// </summary>
public readonly record struct EntityId(ulong Value)
{
    public override string ToString() => Value.ToString("D5");
}