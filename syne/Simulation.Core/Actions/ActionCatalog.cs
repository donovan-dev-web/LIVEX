using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

/// <summary>
/// Définition déclarative d'une action du catalogue (SYNE-040) : coût énergétique,
/// effets sur les besoins, dépendance à une réserve. Issue de la configuration
/// <c>agents.actions.catalog</c> (CONFIGURATION.md §6.2) — une action est déclarée
/// en config, ses effets sont appliqués atomiquement par tick par l'exécuteur.
/// </summary>
public sealed record ActionDefinition(
    Cognition.DesireKind Kind,
    string Name,
    bool Movement,
    double EnergyCost,
    double EnergyRecovery,
    double FatigueRecovery,
    double HungerRecovery,
    double ThirstRecovery,
    ResourceKind? RequiresReserve,
    double ReserveConsumption)
{
    /// <summary>Viable si aucune réserve requise ou si la réserve est disponible.</summary>
    public bool IsViable(ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(stocks);
        return RequiresReserve is not { } reserve || !stocks.IsEmpty(reserve);
    }

    /// <summary>Nom canonique de l'action (représentation déclarative de débug).</summary>
    public override string ToString() => Name;
}

/// <summary>Issue de l'exécution d'une action (mécanique déclarative, SYNE-040).</summary>
public enum ActionOutcome
{
    /// <summary>Effets appliqués.</summary>
    Executed = 0,

    /// <summary>Action impossible (réserve vide, position bloquée) — pas d'effet.</summary>
    Blocked = 1,
}

/// <summary>Résultat d'une exécution atomique d'action (une par entité par tick, SYNE-040).</summary>
public sealed record ActionResult(
    Cognition.DesireKind Kind,
    ActionOutcome Outcome,
    string? Reason,
    double EnergyDelta,
    double HungerDelta,
    double ThirstDelta,
    double FatigueDelta,
    ResourceKind? ReserveConsumed,
    double ReserveConsumedAmount)
{
    public static ActionResult Blocked(Cognition.DesireKind kind, string reason) =>
        new(kind, ActionOutcome.Blocked, reason, 0.0, 0.0, 0.0, 0.0, null, 0.0);
}

/// <summary>
/// Catalogue déclaratif des actions (SYNE-040) : pour chaque <see cref="Cognition.DesireKind"/>,
/// une <see cref="ActionDefinition"/> issue de la configuration <c>agents.actions.catalog</c>.
/// L'ordre du catalogue (ordre de l'enum) est l'ordre stable de départage (décision n°22),
/// les actions sont vérifiées viables contre l'état des réserves.
/// </summary>
public sealed class ActionCatalog
{
    private readonly IReadOnlyDictionary<Cognition.DesireKind, ActionDefinition> _definitions;

    public ActionCatalog(ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var definitions = new Dictionary<Cognition.DesireKind, ActionDefinition>();
        foreach (Cognition.DesireKind kind in Enum.GetValues<Cognition.DesireKind>())
        {
            string name = ToCamelCase(kind.ToString());
            if (!actions.Catalog.Entries.TryGetValue(name, out ActionEntrySettings? entry))
            {
                throw new InvalidOperationException(
                    $"Catalogue d'actions incomplet : aucune entrée déclarée pour « {name} ».");
            }

            definitions[kind] = new ActionDefinition(
                kind,
                name,
                entry.Movement,
                // Le coût par défaut est celui du mouvement ; il ne s'applique
                // qu'aux actions de déplacement déclarées comme telles.
                entry.EnergyCost ?? (entry.Movement ? actions.MoveEnergyCost : 0.0),
                // Seul Rest bénéficie des gains de repos par défaut (les autres
                // actions ne récupèrent rien sans déclaration explicite).
                entry.EnergyRecovery ?? (kind == Cognition.DesireKind.Rest ? actions.RestEnergyGain : 0.0),
                entry.FatigueRecovery ?? (kind == Cognition.DesireKind.Rest ? actions.RestFatigueRecovery : 0.0),
                entry.HungerRecovery ?? 0.0,
                entry.ThirstRecovery ?? 0.0,
                entry.Reserve,
                entry.ReserveConsumption ?? 1.0);
        }

        _definitions = definitions;
    }

    public ActionDefinition this[Cognition.DesireKind kind] => _definitions[kind];

    public IReadOnlyList<ActionDefinition> Definitions =>
        Enum.GetValues<Cognition.DesireKind>().Select(kind => _definitions[kind]).ToList();

    public bool IsViable(Cognition.DesireKind kind, ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(stocks);
        return _definitions[kind].IsViable(stocks);
    }

    private static string ToCamelCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
        {
            return pascal;
        }

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }
}