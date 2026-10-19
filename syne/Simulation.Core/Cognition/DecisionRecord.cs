// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Trace causale d'une décision (SYNE-031/032/033 + réalignement Phase 4) : état de la décision, action
//        choisie, cible connue, score décomposé, candidats rejetés, faits sources et résultat d'exécution.

namespace Simulation.Core.Cognition;

/// <summary>
/// État de la décision du tick : <see cref="New"/> (délibération complète),
/// <see cref="Interrupted"/> (besoin critique reprenant la main),
/// <see cref="Holdover"/> (intention poursuivie entre deux délibérations, le
/// détail publié provient d'une décision antérieure) et <see cref="None"/>
/// (aucune décision — un tick sans cognition n'est pas un zéro).
/// </summary>
public enum DecisionState
{
    New = 0,
    Holdover = 1,
    Interrupted = 2,
    None = 3,
}

/// <summary>
/// Fait justificatif d'une décision : provenance (perception, mémoire, message,
/// relation), sujet observé, âge et confiance au moment du choix. C'est ce qui
/// permet de remonter <c>action ← but ← croyance ← mémoire ← perception</c> au
/// lieu de ne montrer qu'une arithmétique de score (D-06).
/// </summary>
public sealed record DecisionEvidence(
    string Source,
    string Subject,
    double Confidence,
    ulong ObservedTick);

/// <summary>
/// Trace complète d'une décision (SYNE-031/032/033, étendue au réalignement) :
/// tick, entité, état (nouvelle / maintenue / interruptive), action choisie,
/// utilité, <b>cible effectivement visée</b> avec son âge et sa confiance,
/// scores décomposés de tous les candidats, candidats <b>rejetés</b>, faits
/// sources et résultat d'exécution.
///
/// <para>
/// <b>Absence ≠ zéro</b> (B-10 / D-03) : <see cref="Utility"/> est <b>nullable</b>.
/// Une absence de décision porte <c>Utility = null</c> et <see cref="Missing"/>
/// vrai — jamais <c>0.0</c> (zéro observé) ni <c>NaN</c> (nombre non fini, qui
/// rendait de surcroît le snapshot non sérialisable en JSON). Une décision
/// réellement prise porte une utilité finie et un état frais.
/// </para>
///
/// <para>
/// Les champs ajoutés le sont <b>en fin de position avec valeur par défaut</b> :
/// une trace sans cible ni preuve reste constructible (traces historiques, tests
/// unitaires de l'arithmétique).
/// </para>
/// </summary>
public sealed record DecisionRecord(
    ulong Tick,
    ulong EntityId,
    bool Deliberated,
    bool Interrupted,
    DesireKind Chosen,
    double? Utility,
    IReadOnlyList<UtilityScore> Scores,
    DecisionState State = DecisionState.New,
    bool Missing = false,
    GoalTarget? Target = null,
    IReadOnlyList<DecisionEvidence>? Evidence = null,
    IReadOnlyList<DesireKind>? Rejected = null,
    string? ResultOutcome = null,
    string? ResultReason = null)
{
    /// <summary>Vrai si la trace décrit une décision <b>du tick courant</b> (et non un report).</summary>
    public bool IsFresh => !Missing && State is DecisionState.New or DecisionState.Interrupted;

    /// <summary>Candidats écartés par la délibération (hors action retenue).</summary>
    public IReadOnlyList<DesireKind> RejectedKinds =>
        Rejected ?? Scores.Where(score => score.Kind != Chosen).Select(score => score.Kind).ToList();
}
