using Simulation.Core.Entities;

namespace Simulation.Core.Observability;

/// <summary>
/// État d'une entité dans un <see cref="WorldSnapshot"/> (API_CONTRACTS.md §2.1).
/// V0.1 : identité, position, besoins d'énergie/faim/soif/fatigue et action
/// courante (intention). Représentation pure — sérialisée en camelCase.
/// </summary>
public sealed record AgentSnapshot
{
    public AgentSnapshot(
        string id,
        string species,
        double positionX,
        double positionY,
        double energy,
        double hunger,
        double thirst,
        double fatigue,
        string currentIntention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        Id = id;
        Species = species;
        PositionX = positionX;
        PositionY = positionY;
        Energy = energy;
        Hunger = hunger;
        Thirst = thirst;
        Fatigue = fatigue;
        CurrentIntention = currentIntention;
    }

    public string Id { get; }

    public string Species { get; }

    public double PositionX { get; }

    public double PositionY { get; }

    public double Energy { get; }

    public double Hunger { get; }

    public double Thirst { get; }

    public double Fatigue { get; }

    /// <summary>Action courante de l'entité (intention <see cref="Cognition.DesireKind"/>).</summary>
    public string CurrentIntention { get; }

    public static AgentSnapshot From(Simulation.Core.Entities.Entity entity, Cognition.MindState mind)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mind);
        return new AgentSnapshot(
            entity.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entity.Species,
            entity.Position.X,
            entity.Position.Y,
            mind.Needs.Energy,
            mind.Needs.Hunger,
            mind.Needs.Thirst,
            mind.Needs.Fatigue,
            mind.Intention?.Kind.ToString() ?? "Idle");
    }
}