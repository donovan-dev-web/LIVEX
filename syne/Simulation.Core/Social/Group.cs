using Simulation.Core.Cognition;

namespace Simulation.Core.Social;

/// <summary>
/// Groupe émergent (SYNE-060, SYSTEM_SPEC.md §5, décisions n°23/24) : ensemble
/// d'entités liées par une cohésion (confiance réciproque × parts communes de
/// croyances / buts partagés). Les structures émergent — aucun script de
/// coalition. Modèle mutable, mis à jour par <see cref="GroupSystem"/> à chaque
/// révision.
/// </summary>
public sealed class Group
{
    /// <summary>
    /// Constructeur d'un groupe nouvellement formé au tick <paramref name="bornTick"/>.
    /// </summary>
    public Group(ulong id, ulong bornTick, IReadOnlyList<ulong> members)
    {
        Id = id;
        BornTick = bornTick;
        Members = members;
    }

    /// <summary>Identifiant séquentiel déterministe (ordre de formation par composante triée).</summary>
    public ulong Id { get; }

    /// <summary>Tick de formation.</summary>
    public ulong BornTick { get; }

    /// <summary>Membres (identifiants triés croissants — déterminisme d'émission).</summary>
    public IReadOnlyList<ulong> Members { get; private set; }

    /// <summary>Cohésion moyenne des liens internes au dernier calcul (confiance × affinité).</summary>
    public double MeanCohesion { get; internal set; }

    /// <summary>Leader émergent (SYNE-061) : confiance interne entrante maximale, départage id minimal.</summary>
    public ulong? LeaderId { get; internal set; }

    /// <summary>Dernière décision collective adoptée (consensus ≥ seuil), sinon <c>null</c>.</summary>
    public DesireKind? Decision { get; internal set; }

    /// <summary>Fraction de membres partageant la décision collective (≥ <see cref="Configuration.GroupSettings.ConsensusThreshold"/>).</summary>
    public double Consensus { get; internal set; }

    /// <summary>Vrai si le groupe a déjà pris au moins une décision collective (success à la dissolution).</summary>
    public bool HadDecision { get; internal set; }

    internal void UpdateMembers(IReadOnlyList<ulong> members)
    {
        Members = members;
    }
}