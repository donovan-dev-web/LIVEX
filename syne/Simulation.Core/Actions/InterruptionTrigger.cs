using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

/// <summary>
/// Déclencheur d'interruption d'action centralisé (SYNE-043, décision n°15) :
/// unique point de décision « l'action en cours est-elle interrompue par un
/// besoin urgent ? ». Les règles sont déclaratives (besoin critique faim/énergie)
/// et évaluées facialement à n'importe quel tick, y compris entre deux
/// délibérations (holdover, COGNITIVE_ARCHITECTURE.md §6).
///
/// Contrat de déterminisme : évalue par utilité (même formule que la délibération),
/// aucune consommation du PRNG ; en cas d'égalité, l'ordre du catalogue tranche.
/// </summary>
public sealed class InterruptionTrigger
{
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;

    public InterruptionTrigger(ActionCatalog catalog, ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);
        _catalog = catalog;
        _stocks = stocks;
    }

    /// <summary>
    /// Évalue les déclencheurs d'interruption pour l'entité. Renvoie les scores
    /// évalués et, si une interruption est justifiée, le candidat qui reprend la
    /// main (<c>null</c> sinon).
    /// </summary>
    public (IReadOnlyList<UtilityScore> Scores, UtilityScore? Chosen) Evaluate(
        InterruptionSettings interruption,
        ActionSettings actions,
        BodyNeeds needs,
        AgentFactors factors,
        Func<DesireKind, double> successRate,
        DesireKind currentKind,
        Goal? currentIntention,
        ulong currentTick,
        ulong entityId)
    {
        ArgumentNullException.ThrowIfNull(interruption);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(factors);
        ArgumentNullException.ThrowIfNull(successRate);

        var urgents = new List<DesireKind>(2);
        if (needs.Energy < interruption.CriticalEnergy && currentKind != DesireKind.Rest)
        {
            urgents.Add(DesireKind.Rest);
        }

        if (needs.Hunger > interruption.CriticalHunger && currentKind != DesireKind.SeekFood && currentKind != DesireKind.Eat)
        {
            urgents.Add(ResolveHungerResponse());
        }

        if (urgents.Count == 0)
        {
            return ([], null);
        }

        ulong goalAge = currentIntention is { } intention ? intention.Age(currentTick) : 0;
        UtilityScore currentScore = UtilityEvaluator.Evaluate(
            currentKind,
            needs,
            factors,
            successRate(currentKind),
            goalAge,
            actions,
            currentKind);

        UtilityScore? best = null;
        foreach (DesireKind kind in urgents)
        {
            UtilityScore candidate = UtilityEvaluator.Evaluate(
                kind,
                needs,
                factors,
                successRate(kind),
                goalAge: 0,
                actions,
                kind);

            if (candidate.Utility > currentScore.Utility + interruption.UtilityExcessMargin &&
                (best is null ||
                 candidate.Utility > best.Value.Utility ||
                 (candidate.Utility == best.Value.Utility && candidate.Kind < best.Value.Kind)))
            {
                best = candidate;
            }
        }

        return best is { } chosen
            ? ([currentScore, chosen], chosen)
            : ([], null);
    }

    /// <summary>
    /// Réponse à la faim critique : l'action terminale Eat si la réserve est
    /// disponible, sinon la poursuite SeekFood (SYNE-042).
    /// </summary>
    private DesireKind ResolveHungerResponse() =>
        _catalog.IsViable(DesireKind.Eat, _stocks) ? DesireKind.Eat : DesireKind.SeekFood;
}