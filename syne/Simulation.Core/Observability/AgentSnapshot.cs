using Simulation.Core.Entities;

namespace Simulation.Core.Observability;

/// <summary>Observation d'une croyance (fait + confiance) — snapshot agent, API_CONTRACTS.md §2.1.</summary>
public sealed record BeliefObservation(string Subject, string Predicate, string Value, double Confidence);

/// <summary>Observation d'un objectif (type + âge en ticks) — snapshot agent.</summary>
public sealed record GoalObservation(string Kind, ulong Age);

/// <summary>Observation d'une relation de confiance (pair + niveau 0-1) — snapshot agent.</summary>
public sealed record TrustObservation(string PeerId, double Trust);

/// <summary>
/// Observation d'un engagement (D5, ADR « Engagements Communicationnels ») —
/// champ additif du snapshot agent (contrat 0.3.0), émis sous drapeau
/// <c>agents.actions.commitments.enabled</c>.
/// </summary>
public sealed record CommitmentObservation(
    string ToEntityId,
    string RequestType,
    string Status,
    ulong CreatedTick,
    ulong ExpiryTick);

/// <summary>
/// État d'une entité dans un <see cref="WorldSnapshot"/> (API_CONTRACTS.md §2.1).
/// V0.1 : identité, espèce, position, besoins, intention, et cognition observable
/// (traits, croyances, objectifs, relations de confiance, volume mémoire) —
/// nourrit les moteurs de métriques ECHOS (jalon U2).
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
        string currentIntention,
        string currentAction,
        IReadOnlyDictionary<string, double>? traits = null,
        IReadOnlyList<BeliefObservation>? beliefs = null,
        IReadOnlyList<GoalObservation>? goals = null,
        IReadOnlyList<TrustObservation>? trust = null,
        int memoryCount = 0,
        IReadOnlyDictionary<string, double>? inventory = null,
        IReadOnlyList<CommitmentObservation>? commitments = null)
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
        CurrentAction = currentAction;
        Traits = traits ?? new Dictionary<string, double>(StringComparer.Ordinal);
        Beliefs = beliefs ?? [];
        Goals = goals ?? [];
        Trust = trust ?? [];
        MemoryCount = memoryCount;
        Inventory = inventory ?? new Dictionary<string, double>(StringComparer.Ordinal);
        Commitments = commitments ?? [];
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

    /// <summary>Action atomique exécutée pendant le tick représenté.</summary>
    public string CurrentAction { get; }

    /// <summary>Traits de l'entité (par nom, ordre stable de DATA_MODEL.md §3.2).</summary>
    public IReadOnlyDictionary<string, double> Traits { get; }

    /// <summary>Croyances de l'entité (triées par fait — déterminisme d'émission).</summary>
    public IReadOnlyList<BeliefObservation> Beliefs { get; }

    /// <summary>Objectif courant de l'entité (intention).</summary>
    public IReadOnlyList<GoalObservation> Goals { get; }

    /// <summary>Relations de confiance (triées par pair — déterminisme d'émission).</summary>
    public IReadOnlyList<TrustObservation> Trust { get; }

    /// <summary>Nombre de souvenirs en mémoire (décision n°11).</summary>
    public int MemoryCount { get; }

    /// <summary>
    /// Inventaire (D8) : quantité par type de ressource détenu (clé = type
    /// camelCase minuscule, ordre stable du type). Vide si l'inventaire est
    /// désactivé — l'ADR « Inventaire » porte les quantités, poids = somme.
    /// </summary>
    public IReadOnlyDictionary<string, double> Inventory { get; }

    /// <summary>Engagements actifs ou résolus récents (D5) — ordre de création.</summary>
    public IReadOnlyList<CommitmentObservation> Commitments { get; }

    public static AgentSnapshot From(
        Simulation.Core.Entities.Entity entity,
        Cognition.MindState mind,
        ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mind);

        var traits = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach ((string name, double value) in entity.Traits.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            traits[name] = value;
        }

        IReadOnlyList<Cognition.Belief> beliefs = mind.Beliefs.OrderedByFact();
        var beliefObs = new List<BeliefObservation>(beliefs.Count);
        foreach (Cognition.Belief belief in beliefs)
        {
            beliefObs.Add(new BeliefObservation(
                belief.Fact.Subject,
                belief.Fact.Predicate,
                belief.Fact.Value,
                belief.Confidence));
        }

        IReadOnlyList<GoalObservation> goals;
        if (mind.Intention is { } intention)
        {
            goals =
            [
                new GoalObservation(intention.Kind.ToString(), intention.Age(currentTick)),
            ];
        }
        else
        {
            goals = [];
        }

        var trustObs = new List<TrustObservation>();
        if (mind.Trust is { } trust)
        {
            foreach ((ulong peerId, double level) in trust.Snapshot())
            {
                trustObs.Add(new TrustObservation(
                    peerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    level));
            }
        }

        var inventoryObs = new Dictionary<string, double>(StringComparer.Ordinal);
        if (mind.Inventory is { } inventory)
        {
            foreach ((Simulation.Core.World.ResourceKind kind, double amount) in inventory.Snapshot())
            {
                inventoryObs[kind.ToString().ToLowerInvariant()] = amount;
            }
        }

        var commitmentObs = new List<CommitmentObservation>(mind.Commitments.Count);
        foreach (Cognition.Commitment commitment in mind.Commitments)
        {
            commitmentObs.Add(new CommitmentObservation(
                commitment.ToEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                commitment.RequestType,
                commitment.Status.ToString(),
                commitment.CreatedTick,
                commitment.ExpiryTick));
        }

        return new AgentSnapshot(
            entity.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entity.Species,
            entity.Position.X,
            entity.Position.Y,
            mind.Needs.Energy,
            mind.Needs.Hunger,
            mind.Needs.Thirst,
            mind.Needs.Fatigue,
            mind.Intention?.Kind.ToString() ?? "Idle",
            mind.LastActionResult?.Kind.ToString() ?? "Idle",
            traits,
            beliefObs,
            goals,
            trustObs,
            mind.Memory.Count,
            inventoryObs,
            commitmentObs);
    }
}