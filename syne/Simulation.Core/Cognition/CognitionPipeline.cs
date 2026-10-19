// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Pipeline cognitif BDI (SYNE-010) exécuté à chaque tick dans l'ordre causal strict : perception → mémoire →
//        croyances → besoins → objectifs → faisabilité → utilité → délibération → intention → action (« boucle
//        10/15 étapes », jalon U1).
//
//        Réalignement (Phases 2-4 du plan) :
//        — la génération des candidats et l'évaluation d'utilité consomment la CONNAISSANCE active de l'agent
//          (croyances de gisement et de pair, repli mémoire) et non plus seulement les seuils de besoin ;
//        — les objectifs portent une cible concrète (« eau en P, connue à t ») et le déplacement la poursuit ;
//        — les conséquences d'action sont différées à la fin du tick (fenêtre causale fermée, chapitre III) ;
//        — un message compris alimente une croyance nommée, et la trace décisionnelle porte cible, preuves et
//          candidats rejetés (absence de décision ≠ zéro).

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Performance;
using Simulation.Core.Population;
using Simulation.Core.Social;

namespace Simulation.Core.Cognition;

/// <summary>
/// Pipeline cognitif BDI (SYNE-010) exécuté à
/// chaque tick dans l'ordre causal strict :
/// perception → mémoire → croyances → besoins → objectifs → faisabilité →
/// utilité → délibération → intention → action (« boucle 10/15 étapes », jalon U1).
///
/// Depuis le jalon ph4, l'exécution des actions sort du pipeline : elle est
/// confiée au catalogue déclaratif + exécuteur atomique (SYNE-040/041/042) et
/// les interruptions au déclencheur centralisé (SYNE-043).
///
/// <para>
/// <b>Fenêtre causale (chapitre III §1)</b> : pendant l'itération des entités,
/// aucune mutation de monde n'est appliquée — déplacements et effets portés à un
/// pair sont mis en file dans un <see cref="TickCommitBuffer"/> et appliqués après
/// la boucle, dans l'ordre des identifiants. Ce qu'un agent perçoit à un tick est
/// donc l'état figé en début de tick, et non l'état à moitié mis à jour par un
/// agent traité plus tôt (S-15/S-31/D-11/B-06).
/// </para>
///
/// <b>Déterminisme</b> : aucune consommation du PRNG (l'avance du générateur reste à
/// un tirage par tick), ordre d'itération par identifiant croissant, agrégats en
/// représentation invariante à la culture. La prospection sans repère connu reste
/// un offset pseudo-aléatoire déterministe (hash stable) — déclarée comme telle.
/// </summary>
public sealed partial class CognitionPipeline
{
    private readonly Simulation.Core.World.World _world;
    private readonly SimulationOptions _options;
    private readonly PerceptionSystem _perception;
    private readonly World.ResourceStocks _stocks;
    private readonly ActionCatalog _catalog;
    private readonly ActionExecutor _executor;
    private readonly InterruptionTrigger _interruption;
    private readonly CommunicationSystem _communication;
    private readonly GroupSystem _groups;
    private readonly BirthSystem _birth;
    private readonly DeathSystem _death;
    private readonly TickBudgetCollector? _budget;
    private readonly Dictionary<ulong, MindState> _minds = new();

    // Mémoïsation par tick des fragments canoniques d'observation (sujet
    // « entity-N », position « X,Y »). Pendant l'itération des entités, aucune
    // position ne change (fenêtre causale fermée : les déplacements sont appliqués
    // au commit de fin de tick), donc la même chaîne est valable pour tous les
    // observateurs du tick. Purement identique au résultat : mêmes chaînes, mêmes
    // croyances — seule l'allocation répétée disparaît (à forte densité, un agent
    // perçoit des dizaines de voisins et chaque observation format toutes ses
    // clés).
    private readonly Dictionary<ulong, string> _entitySubjectCache = new();
    private readonly Dictionary<ulong, string> _positionCache = new();
    private ulong _cacheTick = ulong.MaxValue;

    public CognitionPipeline(
        Simulation.Core.World.World world,
        SimulationOptions options,
        World.ResourceStocks stocks,
        TickBudgetCollector? budget = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stocks);
        _world = world;

        // Échelle temporelle : les quantités de classe A (taux de
        // besoins, décroissances mémoire/croyances/confiance, coûts énergétiques
        // de mouvement/repos) sont mises à l'échelle **une fois** ici, de façon
        // déterministe, à partir de `dt = simulatedSecondsPerTick / 60` — le
        // sous-arbre agents des options « effectives » porte ces valeurs par
        // tick effectif. À `dt == 1` (défaut, reference), TemporalScale retourne
        // les instances d'entrée : le run est bit-à-bit identique (pin).
        double dt = Configuration.SimulationClock.From(options.Simulation).SimulatedMinutesPerTick;
        _options = Configuration.TemporalScale.WithScaledAgentSettings(
            options,
            Configuration.TemporalScale.ScaleNeeds(options.Agents.Needs, dt),
            Configuration.TemporalScale.ScaleMemory(options.Agents.Memory, dt),
            Configuration.TemporalScale.ScaleBeliefs(options.Agents.Beliefs, dt),
            Configuration.TemporalScale.ScaleTrust(options.Agents.Trust, dt),
            Configuration.TemporalScale.ScaleActions(options.Agents.Actions, dt));

        _perception = new PerceptionSystem(world, _options.Agents.Perception);
        _stocks = stocks;
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _executor = new ActionExecutor(world, _catalog, stocks, _options);
        _interruption = new InterruptionTrigger(_catalog, stocks);
        _communication = new CommunicationSystem(world, _options.Communication);
        _groups = new GroupSystem(_options.Groups);
        _birth = new BirthSystem(_options.Reproduction);
        _death = new DeathSystem(_options.Agents.Life);
        _budget = budget;
    }

    /// <summary>
    /// Options effectives du run : sous-arbre <c>agents</c> mis à
    /// l'échelle temporelle (classe A par tick effectif), autres sections
    /// partagées avec la config d'origine. La restauration bit-à-bit reconstruit
    /// les esprits avec ces réglages pour rester cohérente avec le run sauvé.
    /// </summary>
    internal SimulationOptions EffectiveOptions => _options;

    public PerceptionSystem Perception => _perception;

    public ActionCatalog Catalog => _catalog;

    public World.ResourceStocks Stocks => _stocks;

    /// <summary>Sous-système de communication (une passe par tick — SYNE-050 → 054).</summary>
    public CommunicationSystem Communication => _communication;

    /// <summary>Sous-système de groupes émergents (SYNE-060/061, révision LOD configurable).</summary>
    public GroupSystem Groups => _groups;

    /// <summary>Sous-système de naissance par fusion consentie (SYNE-062).</summary>
    public BirthSystem Birth => _birth;

    /// <summary>Sous-système de mortalité par épuisement (SYNE-074).</summary>
    public DeathSystem Death => _death;

    public IReadOnlyCollection<MindState> Minds => _minds.Values;

    /// <summary>Paires (identifiant, esprit) triées par identifiant croissant — persistance bit-à-bit.</summary>
    internal IReadOnlyList<(ulong Id, MindState Mind)> MindPairsSorted() =>
        _minds
            .Select(pair => (pair.Key, pair.Value))
            .OrderBy(pair => pair.Key)
            .ToList();

    public MindState MindOf(ulong entityId) => _minds[entityId];

    public bool HasMind(ulong entityId) => _minds.ContainsKey(entityId);

    /// <summary>
    /// Exécute une itération complète du pipeline pour toutes les entités.
    /// <list type="number">
    /// <item><b>Itération des entités</b> (identifiants croissants) : perception sur
    /// l'état figé, cognition, décision, action — les mutations de monde sont mises
    /// en file.</item>
    /// <item><b>Commit des conséquences</b> : déplacements et effets inter-entités
    /// appliqués en une fois.</item>
    /// <item><b>Communication</b> puis réception → <b>croyance</b> ; engagements si
    /// activés.</item>
    /// <item><b>Décroissances</b> (croyances, confiance), <b>groupes</b> (sur les
    /// positions finales), <b>naissances</b>, <b>mortalité</b>.</item>
    /// </list>
    /// </summary>
    public void Step(ulong currentTick)
    {
        if (_cacheTick != currentTick)
        {
            _cacheTick = currentTick;
            _entitySubjectCache.Clear();
            _positionCache.Clear();
        }

        List<Entity> ordered = _world.Entities
            .OrderBy(entity => entity.Id.Value)
            .ToList();

        var commit = new TickCommitBuffer();
        _executor.BeginTick(commit);
        try
        {
            foreach (Entity entity in ordered)
            {
                StepEntity(entity, currentTick);
            }


            // Fenêtre causale fermée (ch. III) : les conséquences d'action —
            // déplacements et dégâts — ne rejoignent le monde qu'ici. Aucun agent
            // n'a donc perçu, décidé ni agi sur un état modifié par un autre agent
            // du même tick, et l'ordre des identifiants n'est plus une loi causale.
            commit.Commit(_world);
        }
        finally
        {
            _executor.BeginTick(null);
            commit.Clear();
        }

        // Communication de masse en une passe (performance.batchCommunication,
        // cartes après perception/décision/action
        // — l'ordre causal est agent par agent puis réseau de communication.
        using (TickPhaseScope communicationScope = _budget?.Begin(TickPhase.Communication) ?? TickPhaseScope.Noop)
        {
            _communication.Step(currentTick, _minds);

            // Réception → croyance (D-13) : un message compris alimente une
            // croyance nommée (provenance = émetteur, confiance = fiabilité de
            // transmission × confiance relationnelle × confiance du témoignage).
            ApplyReceivedMessages(currentTick, ordered);

            // Engagements (D5) : après la
            // diffusion — création d'engagement à la réception d'une Response
            // positive, réponse des entités très sociables aux Request comprises.
            // Désactivé par défaut (trajectoire de référence).
            if (_options.Agents.Actions.Commitments.Enabled)
            {
                ProcessCommitmentMessages(currentTick);
            }
        }

        using (TickPhaseScope eventsScope = _budget?.Begin(TickPhase.EventsGroupsPopulation) ?? TickPhaseScope.Noop)
        {
            foreach (MindState mind in _minds.Values)
            {
                mind.Beliefs.Tick(currentTick, _options.Agents.Beliefs);
                mind.Trust.Tick();
            }

            // Engagements (D5) : résolution du cycle — honororation (le prometteur
            // a porté aide/échange/socialisation ce tick) ou expiration (pénalité
            // de confiance, distincte du mensonge factuel). Ordre : identifiants
            // croissants.
            if (_options.Agents.Actions.Commitments.Enabled)
            {
                ResolveCommitments(currentTick);
            }

            // Groupes émergents (SYNE-060/061) puis naissances (SYNE-062) : après la
            // boucle des entités, la communication de masse et les décréments de
            // croyance/confiance — la population ne mute qu'après l'itération complète
            // (ordre causal strict). La cohésion est évaluée sur les positions
            // <b>finales</b> du tick (post-commit) : elles servent à INSTRUMENTER la
            // distance des paires liées (arbitrage A0-3), jamais à filtrer la liaison.
            // Positions construites seulement quand la revue de groupes a lieu :
            // hors revue (LOD `reviewIntervalTicks`), la table serait un coût par
            // tick pour un système qui n'itère même pas.
            bool groupsReview = _options.Groups.Enabled
                && currentTick > 0
                && currentTick % (ulong)_options.Groups.ReviewIntervalTicks == 0;
            Dictionary<ulong, World.Position>? positions = groupsReview ? Positions() : null;
            _groups.Step(currentTick, _minds, positions);
            _birth.Step(currentTick, _world, _minds, _options);
            foreach ((ulong childId, MindState childMind) in _birth.NewbornMinds)
            {
                _minds[childId] = childMind;
            }

            // Mortalité (SYNE-074) : après les naissances — les nouveau-nés naissent à
            // pleine énergie et ne meurent pas au tick de leur naissance. Retrait du
            // monde, puis purge des esprits et des groupes (dissolution/redimension si
            // passage sous la taille minimale). Tout se fait après l'itération complète.
            IReadOnlyList<ulong> deceased = _death.Step(currentTick, _world, _minds);
            if (deceased.Count > 0)
            {
                _groups.PurgeDeceased(deceased, currentTick, _minds, positions ?? Positions());
                foreach (ulong deathId in deceased)
                {
                    _minds.Remove(deathId);
                }
            }
        }
    }

}
