// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Système de groupes émergents (SYNE-060/061).

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Social;

/// <summary>Événement discret de formation d'un groupe (SYNE-060).</summary>
public sealed record GroupFormation(ulong GroupId, ulong Tick, IReadOnlyList<ulong> Members, double MeanCohesion);

/// <summary>Événement discret de dissolution d'un groupe (SYNE-060).</summary>
public sealed record GroupDissolution(
    ulong GroupId,
    ulong Tick,
    ulong FormedTick,
    IReadOnlyList<ulong> Members,
    bool Success,
    int MembersIn,
    int MembersOut);

/// <summary>Événement discret de décision collective (SYNE-061, consensus pondéré par la confiance).</summary>
public sealed record GroupDecision(ulong GroupId, ulong Tick, ulong LeaderId, DesireKind Decision, double Consensus);

/// <summary>
/// Système de groupes émergents (SYNE-060/061, décisions
/// n°23/24). Il n'invente aucune coalition scriptée : les groupes naissent de la
/// **cohésion** constatée entre les entités —
/// <c>confiance réciproque × affinité (croyances partagées + buts partagés)</c>.
///
/// Exécuté à fréquence <c>reviewIntervalTicks</c> (LOD déterministe), après le
/// pipeline cognitif et la communication de masse (ordre causal strict) :
///
/// <list type="number">
/// <item><b>Liens sociaux</b> : paires (entité, entité) en confiance réciproque
///  ≥ <c>trustThreshold</c> partageant au moins une croyance ou un but — la
/// « proximité sociale » ;</item>
/// <item><b>Composantes connexes</b> du graphe = candidats groupes (union-find
///  déterministe, racine = plus petit id) ; taille ≥ <c>minGroupSize</c> ;</item>
/// <item><b>Cycle de vie</b> : continuité par ensemble de membres identique
///  (même id, durée cumulée) sinon formation / dissolution ;</item>
/// <item><b>Leader émergent</b> (SYNE-061) : membre de plus forte confiance
///  entrante intra-groupe (départage par id minimal) ;</item>
/// <item><b>Décisions collectives</b> (SYNE-061) : adoption par quorum
///  <c>consensusThreshold</c> sur l'intention dominante des membres —
///  <b>exposées seulement</b> (état du groupe, `LastDecisions`, snapshot et
///  événement <c>group_decision</c>), jamais injectées dans l'utilité
///  individuelle (arbitrage A0-3 du 17/10/2026, qui remplace la propagation
///  SYNE-076) ;</item>
/// <item><b>Distances des paires liées</b> (A0-3) : la liaison n'a aucune clause
///  de proximité explicite — la décroissance de confiance (0,9/tick sans
///  interaction) éteint de lui-même un lien distants — et la distance des paires
///  liées est <b>instrumentée</b> (max/moyenne par groupe, <c>null</c> = non
///  mesuré).</item>
/// </list>
///
/// Déterminisme : aucun tirage du PRNG global ; toutes les itérations suivent
/// des ordres triés par identifiant.
/// </summary>
public sealed partial class GroupSystem
{
    private readonly GroupSettings _settings;
    private readonly Dictionary<ulong, Group> _groups = new();
    private readonly List<GroupFormation> _lastFormed = new();
    private readonly List<GroupDissolution> _lastDissolved = new();
    private readonly List<GroupDecision> _lastDecisions = new();
    private ulong _nextGroupId = 1;

    public GroupSystem(GroupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public GroupSettings Settings => _settings;

    /// <summary>Prochain identifiant de groupe (persistance bit-à-bit).</summary>
    internal ulong NextGroupId => _nextGroupId;

    /// <summary>Restauration des groupes actifs et du compteur d'identifiants (persistance bit-à-bit).</summary>
    internal void RestoreState(IEnumerable<Group> groups, ulong nextGroupId)
    {
        ArgumentNullException.ThrowIfNull(groups);
        _groups.Clear();
        foreach (Group group in groups)
        {
            _groups[group.Id] = group;
        }

        _nextGroupId = Math.Max(nextGroupId, _groups.Keys.Count == 0 ? 1 : _groups.Keys.Max() + 1);
    }

    /// <summary>Groupes actifs (ordre d'identifiant).</summary>
    public IReadOnlyList<Group> Active =>
        _groups.Values.OrderBy(group => group.Id).ToList();

    /// <summary>Formations du tick courant (ordre de formation).</summary>
    public IReadOnlyList<GroupFormation> LastFormed => _lastFormed;

    /// <summary>Dissolutions du tick courant (ordre de dissolution).</summary>
    public IReadOnlyList<GroupDissolution> LastDissolved => _lastDissolved;

    /// <summary>Décisions collectives adoptées au tick courant.</summary>
    public IReadOnlyList<GroupDecision> LastDecisions => _lastDecisions;

    /// <summary>
    /// Révision des groupes au tick courant (LOD <c>reviewIntervalTicks</c>),
    /// sans positions : la contrainte de proximité spatiale n'est alors
    /// <b>pas évaluable</b> et est réputée satisfaite (comportement historique des
    /// appels hors moteur). Le pipeline, lui, fournit toujours les positions
    /// finales du tick.
    /// </summary>
    public void Step(ulong tick, IReadOnlyDictionary<ulong, MindState> minds) => Step(tick, minds, positions: null);

    /// <summary>
    /// Révision des groupes au tick courant (LOD <c>reviewIntervalTicks</c>).
    /// <paramref name="positions"/> porte les positions <b>finales</b> du tick et
    /// sert uniquement à <b>instrumenter</b> la distance des paires liées
    /// (A0-3) : la liaison n'a plus aucune clause de proximité explicite.
    /// </summary>
    public void Step(
        ulong tick,
        IReadOnlyDictionary<ulong, MindState> minds,
        IReadOnlyDictionary<ulong, Position>? positions)
    {
        ArgumentNullException.ThrowIfNull(minds);
        _lastFormed.Clear();
        _lastDissolved.Clear();
        _lastDecisions.Clear();

        if (!_settings.Enabled || tick == 0 || tick % (ulong)_settings.ReviewIntervalTicks != 0)
        {
            return;
        }

        Dictionary<ulong, Belief[]> sortedBeliefs = SortedBeliefsOf(minds);
        IReadOnlyList<(List<ulong> Members, double? MaxBondDistance, double? MeanBondDistance)> components =
            ComputeComponents(minds, sortedBeliefs, positions);
        var current = new List<(List<ulong> Members, double MeanCohesion, double? MaxBondDistance, double? MeanBondDistance)>(components.Count);
        foreach ((List<ulong> members, double? maxBondDistance, double? meanBondDistance) in components)
        {
            if (members.Count < _settings.MinGroupSize)
            {
                continue;
            }

            current.Add((members, MeanCohesionOf(members, minds, sortedBeliefs), maxBondDistance, meanBondDistance));
        }

        var unmatched = new HashSet<int>(Enumerable.Range(0, current.Count));
        var dissolved = new List<ulong>();

        foreach (Group group in _groups.Values.OrderBy(group => group.Id).ToList())
        {
            int match = FindMemberSet(current, unmatched, group.Members);
            if (match >= 0)
            {
                unmatched.Remove(match);
                group.UpdateMembers(current[match].Members);
                // La cohésion doit suivre les membres *et* leur évolution : la
                // laisser figée à la formation exposait un `meanCohesion` périmé
                // dans le snapshot et les formations/dissolutions rapportées.
                group.MeanCohesion = current[match].MeanCohesion;
                group.MaxBondDistance = current[match].MaxBondDistance;
                group.MeanBondDistance = current[match].MeanBondDistance;
                RefreshEmergent(group, minds, tick);
            }
            else
            {
                dissolved.Add(group.Id);
            }
        }

        foreach (ulong groupId in dissolved)
        {
            if (_groups.Remove(groupId, out Group? old))
            {
                _lastDissolved.Add(new GroupDissolution(
                    old.Id,
                    tick,
                    old.BornTick,
                    old.Members,
                    old.HadDecision,
                    old.Members.Count,
                    old.Members.Count));
            }
        }

        foreach (int index in unmatched.OrderBy(i => i))
        {
            var group = new Group(_nextGroupId++, tick, current[index].Members)
            {
                MeanCohesion = current[index].MeanCohesion,
                MaxBondDistance = current[index].MaxBondDistance,
                MeanBondDistance = current[index].MeanBondDistance,
            };
            RefreshEmergent(group, minds, tick);
            _groups[group.Id] = group;
            _lastFormed.Add(new GroupFormation(
                group.Id,
                tick,
                group.Members,
                group.MeanCohesion));
        }
    }
}
