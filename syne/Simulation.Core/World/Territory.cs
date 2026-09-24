namespace Simulation.Core.World;

/// <summary>
/// Zone de territoire (SYNE-073, décision n°21, Monographie §6.5) : disque
/// « point de survie » {Center, Radius} autour duquel la présence des entités
/// délimite le **territoire effectif**. V0.1 : le territoire est un concept
/// d'observation (aucun comportement agentique, aucune revendication) — la
/// présence d'une entité dans la zone la classe comme membre du territoire.
/// Pur (0 tirage PRNG, DETERMINISM.md §3) : l'appartenance est une fonction
/// pure des positions.
/// </summary>
public sealed record Territory
{
    public Territory(string id, Position center, double radius)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Le rayon d'une zone de territoire doit être strictement positif.");
        }

        Id = id;
        Center = center;
        Radius = radius;
    }

    public string Id { get; }

    public Position Center { get; }

    public double Radius { get; }

    /// <summary>L'entité à la position <paramref name="position"/> est dans la zone (disque).</summary>
    public bool Contains(Position position) => position.DistanceTo(Center) <= Radius;
}

/// <summary>Sens d'un changement d'appartenance à une zone de territoire (SYNE-073).</summary>
public enum TerritoryMembershipChangeKind
{
    Entered,
    Left,
}

/// <summary>
/// Changement d'appartenance à une zone de territoire (SYNE-073) : une entité a
/// franchi le disque entre deux ticks. Consommée par l'émetteur d'observabilité
/// (événement <c>world.territory_membership_changed</c>). Ordre d'émission
/// déterministe : par zone (ordre de pose), par entité (id croissant).
/// </summary>
public sealed record TerritoryMembershipChange(TerritoryMembershipChangeKind Kind, Territory Zone, ulong EntityId);