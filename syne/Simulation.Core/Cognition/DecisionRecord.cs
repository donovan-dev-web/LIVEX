// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Trace complète d'une décision (SYNE-031/032/033) : tick, entité, scores d'utilité par action candidate,
//        action choisie, et les drapeaux de délibération (fréquence configurable) et d'interruption (besoin
//        critique).

namespace Simulation.Core.Cognition;

/// <summary>
/// Trace complète d'une décision (SYNE-031/032/033) :
/// tick, entité, scores d'utilité par action candidate, action choisie, et les
/// drapeaux de délibération (fréquence configurable) et d'interruption (besoin
/// critique). Base de l'analyse causale ECHOS et de l'observabilité.
/// </summary>
public sealed record DecisionRecord(
    ulong Tick,
    ulong EntityId,
    bool Deliberated,
    bool Interrupted,
    DesireKind Chosen,
    double Utility,
    IReadOnlyList<UtilityScore> Scores);