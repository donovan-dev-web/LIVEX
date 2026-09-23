namespace Simulation.Core.Configuration;

/// <summary>
/// Options de simulation (Annexe H). Structure parallèle au fichier config.json :
/// les propriétés absentes d'un fichier partiel conservent les défauts intégrés.
/// </summary>
public sealed class SimulationOptions
{
    public SimulationSettings Simulation { get; set; } = new();
    public AgentSettings Agents { get; set; } = new();
    public ResourceSettings Resources { get; set; } = new();
    public CommunicationSettings Communication { get; set; } = new();
    public WorldSettings World { get; set; } = new();
    public RandomSettings Random { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public GroupSettings Groups { get; set; } = new();
    public ReproductionSettings Reproduction { get; set; } = new();
}

public sealed class SimulationSettings
{
    public int WorldWidth { get; set; } = 500;
    public int WorldHeight { get; set; } = 500;
    public int MaxTicks { get; set; } = 1_000_000;
    public int TicksPerSecond { get; set; } = 10;
    public int AutoSaveEveryNTicks { get; set; } = 1000;
    public int MaxBackups { get; set; } = 5;
}

public sealed class AgentSettings
{
    public int InitialCount { get; set; } = 100;
    public Dictionary<string, double> Traits { get; set; } = new()
    {
        ["bravery"] = 1.0,
        ["curiosity"] = 1.0,
        ["sociability"] = 1.0,
        ["greed"] = 1.0,
        ["pessimism"] = 1.0,
        ["aggressiveness"] = 1.0,
        ["strength"] = 1.0,
        ["speed"] = 1.0,
    };
    public NeedsSettings Needs { get; set; } = new();
    public PerceptionSettings Perception { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
    public BeliefSettings Beliefs { get; set; } = new();
    public TrustSettings Trust { get; set; } = new();
    public ActionSettings Actions { get; set; } = new();

    /// <summary>Héritage intergénérationnel (SYNE-063, décision n°16) : mécanismes fins configurables (§6.6.3).</summary>
    public InheritanceSettings Inheritance { get; set; } = new();

    /// <summary>Cycle de vie — mortalité par épuisement (SYNE-074, SYSTEM_SPEC.md §8, Monographie §6.2.4).</summary>
    public LifeSettings Life { get; set; } = new();

    /// <summary>Cheminement (SYNE-077, Monographie §6.2.12) : A* déterministe sur grille rasterisée + cache LRU.</summary>
    public PathfindingSettings Pathfinding { get; set; } = new();
}

public sealed class NeedsSettings
{
    public double HungerRate { get; set; } = 0.5;
    public double ThirstRate { get; set; } = 0.7;
    public double FatigueRate { get; set; } = 0.3;
    /// <summary>Dérive d'élan des besoins sociaux (V0.1, calibration prototype).</summary>
    public double SafetyDriftRate { get; set; } = 0.001;
    public double SocialDriftRate { get; set; } = 0.001;
    public double CuriosityDriftRate { get; set; } = 0.002;

    /// <summary>Seuil de déclenchement : un besoin non satisfait lance une action dès ce niveau (décision n°4 : « ≥ 50 »).</summary>
    public double HungerTriggerThreshold { get; set; } = 50.0;

    public double ThirstTriggerThreshold { get; set; } = 50.0;

    /// <summary>Seuil de déclenchement du repos (décision n°4 : Rest dès fatigue &gt; 70).</summary>
    public double FatigueTriggerThreshold { get; set; } = 70.0;
}

public sealed class PerceptionSettings
{
    /// <summary>Rayon de perception — décision n°6 : défaut 50 (plage 30–70).</summary>
    public int Radius { get; set; } = 50;
    public double ConfidenceFalloff { get; set; } = 0.3;
    /// <summary>Perception étagée : rotation en groupes de <c>RotationInterval</c> (COGNITIVE_ARCHITECTURE §3).</summary>
    public int RotationInterval { get; set; } = 4;
    /// <summary>Ligne de vue : un obstacle masque la perception (SYNE-011, ADR-013).</summary>
    public bool LineOfSight { get; set; } = true;
}

public sealed class MemorySettings
{
    public int MaxCapacity { get; set; } = 1000;
    public double RecallThreshold { get; set; } = 0.01;
    /// <summary>Décroissance des souvenirs d'observation (décision n°11).</summary>
    public double ObservationDecayRate { get; set; } = 0.01;
    public double EventDecayRate { get; set; } = 0.005;
    public double InteractionDecayRate { get; set; } = 0.002;
}

public sealed class BeliefSettings
{
    /// <summary>Force de révision : <c>belief = belief + (signal − belief) × strength</c> (décision n°12).</summary>
    public double UpdateStrength { get; set; } = 0.3;
    /// <summary>Plafond de variation de confiance par snap (décision n°12 : « plafond par snap »).</summary>
    public double MaxChangePerSnap { get; set; } = 0.5;
    public double AlignBonus { get; set; } = 0.2;
    public double ConflictPenalty { get; set; } = 0.1;
    public ulong ExpiryTicks { get; set; } = 100;
    public double ExpiredCap { get; set; } = 0.4;
    public double TimeDecayPerTick { get; set; } = 0.999;
}

public sealed class TrustSettings
{
    /// <summary>Confiance d'une première rencontre (décision n°10).</summary>
    public double InitialTrust { get; set; } = 0.5;

    /// <summary>Décroissance de confiance par tick sans interaction (trustDecay, COMMUNICATION_PROTOCOL.md §3).</summary>
    public double DecayFactorPerTick { get; set; } = 0.9;

    /// <summary>Bonus de confiance après une vérité constatée (plafond 1.0).</summary>
    public double TruthBonus { get; set; } = 0.05;

    /// <summary>Pénalité de confiance après un mensonge constaté (plancher 0.0).</summary>
    public double LiePenalty { get; set; } = 0.2;
}

public sealed class ActionSettings
{
    public double MoveEnergyCost { get; set; } = 0.5;
    public double RestEnergyGain { get; set; } = 0.5;
    public double RestFatigueRecovery { get; set; } = 1.0;
    public DeliberationSettings Deliberation { get; set; } = new();
    public InterruptionSettings Interruption { get; set; } = new();

    /// <summary>Catalogue déclaratif des actions (SYNE-040, CONFIGURATION.md §6.2).</summary>
    public ActionCatalogSettings Catalog { get; set; } = new();
}

public sealed class ActionCatalogSettings
{
    /// <summary>Entrées déclaratives par action (clé = nom de l'<c>DesireKind</c>, camelCase).</summary>
    public Dictionary<string, ActionEntrySettings> Entries { get; set; } = new(StringComparer.Ordinal)
    {
        ["idle"] = new ActionEntrySettings(),
        ["seekFood"] = new ActionEntrySettings { Movement = true },
        ["seekWater"] = new ActionEntrySettings { Movement = true },
        ["eat"] = new ActionEntrySettings { EnergyCost = 0.2, HungerRecovery = 30.0, Reserve = World.ResourceKind.Food },
        ["drink"] = new ActionEntrySettings { EnergyCost = 0.2, ThirstRecovery = 30.0, Reserve = World.ResourceKind.Water },
        ["rest"] = new ActionEntrySettings(),
        ["flee"] = new ActionEntrySettings { Movement = true },
        ["socialize"] = new ActionEntrySettings { Movement = true },
        ["explore"] = new ActionEntrySettings { Movement = true },
    };
}

public sealed class ActionEntrySettings
{
    /// <summary>Action de déplacement (pas déterministe + coût énergie du mouvement).</summary>
    public bool Movement { get; set; }

    /// <summary>Coût énergétique (défaut : <c>actions.moveEnergyCost</c>).</summary>
    public double? EnergyCost { get; set; }

    /// <summary>Énergie récupérée (défaut : <c>actions.restEnergyGain</c>).</summary>
    public double? EnergyRecovery { get; set; }

    /// <summary>Fatigue récupérée (défaut : <c>actions.restFatigueRecovery</c>).</summary>
    public double? FatigueRecovery { get; set; }

    /// <summary>Réduction de la faim (ex. Eat).</summary>
    public double? HungerRecovery { get; set; }

    /// <summary>Réduction de la soif (ex. Drink).</summary>
    public double? ThirstRecovery { get; set; }

    /// <summary>Réserve globale requise/consommée (ex. Eat → Food).</summary>
    public World.ResourceKind? Reserve { get; set; }

    /// <summary>Quantité consommée de la réserve à chaque exécution (défaut 1.0).</summary>
    public double? ReserveConsumption { get; set; }
}

public sealed class DeliberationSettings
{
    /// <summary>Fréquence de délibération : ticks entre deux délibérations (décision n°14, défaut 10).</summary>
    public int IntervalTicks { get; set; } = 10;

    /// <summary>Bonus d'alignement avec l'objectif courant (×1.2, COGNITIVE_ARCHITECTURE.md §6).</summary>
    public double AlignBonus { get; set; } = 1.2;

    /// <summary>Bonification maximale d'alignement avec l'objectif collectif (SYNE-076, ×1.2 par défaut).</summary>
    public double CollectiveAlignBonus { get; set; } = 1.2;

    /// <summary>Marge anti-oscillation (hystérésis) : passer à une nouvelle action seulement si elle dépasse l'action courante de cette marge (défaut 0.05).</summary>
    public double ActionSwitchMargin { get; set; } = 0.05;

    /// <summary>Marge de conflit de priorités : candidats à moins de cette marge du maximum → résolution probabiliste confiance×force (décision n°22).</summary>
    public double ConflictTieMargin { get; set; } = 0.5;
}

public sealed class InterruptionSettings
{
    /// <summary>Interruptions actives (décision n°15).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Marge : interruption seulement si l'utilité du besoin critique surpasse l'action courante de cette marge (défaut 10, COGNITIVE_ARCHITECTURE.md §6).</summary>
    public double UtilityExcessMargin { get; set; } = 10.0;

    /// <summary>Seuil de faim critique (défaut 85, COGNITIVE_ARCHITECTURE.md §6).</summary>
    public double CriticalHunger { get; set; } = 85.0;

    /// <summary>Seuil d'énergie critique (défaut 10).</summary>
    public double CriticalEnergy { get; set; } = 10.0;
}

public sealed class ResourceSettings
{
    public ResourceSpec Food { get; set; } = new() { Initial = 100, RegenerationRate = 0, DegradationTick = 100 };
    public ResourceSpec Water { get; set; } = new() { Initial = 1000, RegenerationRate = 5 };
    public ResourceSpec Wood { get; set; } = new() { Initial = 50, RegenerationRate = 0.1 };
}

public sealed class ResourceSpec
{
    public int Initial { get; set; }
    public double RegenerationRate { get; set; }
    public int? DegradationTick { get; set; }
}

public sealed class CommunicationSettings
{
    /// <summary>Max d'envois par entité et par tick (Annexe H : 5).</summary>
    public int MaxSendsPerTick { get; set; } = 5;

    /// <summary>Max de réceptions traitées par tick et par entité (Annexe H : 3).</summary>
    public int MaxReceivesPerTick { get; set; } = 3;

    /// <summary>Probabilité d'incompréhension d'un message reçu (Annexe H : 0.05).</summary>
    public double IncomprehensionRate { get; set; } = 0.05;

    /// <summary>Décroissance de confiance inter-entités (Annexe H : 0.9, valeurs relatives au trust).</summary>
    public double TrustDecay { get; set; } = 0.9;

    /// <summary>Portée effective de transmission d'une pulsation (décision n°7 : 20 u. héritées du prototype, configurable).</summary>
    public int TransmissionRange { get; set; } = 55;

    /// <summary>Relais entité-à-entité actif (SYNE-050, COMMUNICATION_PROTOCOL.md §4).</summary>
    public bool RelayEnabled { get; set; } = true;

    /// <summary>Nombre maximal de sauts d'un message relayé (anti-boucle, borne de dégradation).</summary>
    public int MaxHops { get; set; } = 2;

    /// <summary>Coût énergétique d'émission — décision n°9 : <c>sendEnergyCost + payload × sendEnergyPayloadFactor</c> (0.5 + p×0.1).</summary>
    public double SendEnergyCost { get; set; } = 0.5;

    public double SendEnergyPayloadFactor { get; set; } = 0.1;

    /// <summary>Coût énergétique de réception — décision n°9 : <c>receiveEnergyCost + payload × receiveEnergyPayloadFactor</c> (0.2 + p×0.05).</summary>
    public double ReceiveEnergyCost { get; set; } = 0.2;

    public double ReceiveEnergyPayloadFactor { get; set; } = 0.05;

    /// <summary>Décroissance de confiance par relais — décision n°10 : <c>confidence × hopConfidenceDecay</c> (0.9, ≈ 10 %/hop).</summary>
    public double HopConfidenceDecay { get; set; } = 0.9;
}

public sealed class WorldSettings
{
    public bool Seasons { get; set; }
    public bool Events { get; set; }
    public bool Obstacles { get; set; }
}

public sealed class RandomSettings
{
    public ulong Seed { get; set; } = 12_345;
    public string Engine { get; set; } = "xoshiro256**";
}

public sealed class PerformanceSettings
{
    public bool ParallelPerception { get; set; } = true;
    public bool SpatialGrid { get; set; } = true;
    public bool DecisionCaching { get; set; } = true;
    public bool BatchCommunication { get; set; } = true;
}

/// <summary>
/// Formation émergente des groupes (SYNE-060/061, SYSTEM_SPEC.md §5, décisions
/// n°23, 24) : cohésion = confiance réciproque × affinité (buts partagés +
/// croyances communes). Les structures émergent — aucun script de coalition.
/// </summary>
public sealed class GroupSettings
{
    /// <summary>Formation/dissolution des groupes active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fiducial : révision des groupes tous les N ticks (fréquence LOD déterministe).</summary>
    public int ReviewIntervalTicks { get; set; } = 10;

    /// <summary>Confiance réciproque minimale (min des deux sens) pour un lien social.</summary>
    public double TrustThreshold { get; set; } = 0.3;

    /// <summary>Taille minimale d'un groupe (en dessous : pas de formation / dissolution).</summary>
    public int MinGroupSize { get; set; } = 3;

    /// <summary>Bonus d'affinité par croyance partagée (même fait, confiances ≥ 0.5).</summary>
    public double SharedBeliefBonus { get; set; } = 0.1;

    /// <summary>Bonus d'affinité quand deux entités partagent leur objectif courant (décision n°24 : buts partagés).</summary>
    public double GoalAlignmentBonus { get; set; } = 0.2;

    /// <summary>Quorum : une décision collective est adoptée quand ≥ cette fraction des membres partage la même intention.</summary>
    public double ConsensusThreshold { get; set; } = 0.5;
}

/// <summary>
/// Cycle de vie — naissance par fusion consentie (SYNE-062/075, décisions n°17, 16,
/// SYSTEM_SPEC.md §8, Monographie §6.6.2) : fusion rare et volontaire, décision
/// déterministe sans PRNG. La « fusion consentie » est soumise aux conditions de
/// fidélité comportementale (proximité physique, ligne de vue, énergie suffisante,
/// absence de besoin critique) — SYNE-075.
/// </summary>
public sealed class ReproductionSettings
{
    /// <summary>Naissances par fusion actives.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Une tentative de fusion tous les N ticks (rareté V0.1 → V0.2).</summary>
    public int IntervalTicks { get; set; } = 100;

    /// <summary>Consentement : confiance réciproque minimale du couple pour fusionner (§6.6.2).</summary>
    public double ConsentTrustThreshold { get; set; } = 0.6;

    /// <summary>Plafond de naissances par tick (0 = aucune, garde-fou de population).</summary>
    public int MaxBirthsPerTick { get; set; } = 1;

    /// <summary>Proximité physique : distance maximale entre les parents pour fusionner (SYNE-075).</summary>
    public double MergeRange { get; set; } = 40.0;

    /// <summary>Énergie minimale des deux parents pour fusionner (SYNE-075, Monographie §6.6.2).</summary>
    public double MergeMinimumEnergy { get; set; } = 30.0;

    /// <summary>La ligne de vue entre les parents doit être dégagée (SYNE-075, SYNE-011).</summary>
    public bool RequireLineOfSight { get; set; } = true;

    /// <summary>Ni l'un ni l'autre des parents ne doit être en état critique (SYNE-075 : faim/énergie critiques).</summary>
    public bool RequireNoCriticalNeed { get; set; } = true;
}

/// <summary>
/// Mécanismes fins d'héritage des traits (SYNE-063, décision n°16, DATA_MODEL.md
/// §6.6.3) : la structure (fusion + transmission) est figée ; la réadaptation/
/// dominance/mutation restent configurables (V0.2).
/// </summary>
public sealed class InheritanceSettings
{
    /// <summary>
    /// Dominance dans [0, 1] : 0 = fusion égalitaire (moyenne arithmétique, V0.1) ;
    /// &gt; 0 = le trait du parent « exprimant » (écart au neutre le plus grand)
    /// pèse d'autant plus (dominance pleine à 1).
    /// </summary>
    public double Dominance { get; set; } = 0.0;

    /// <summary>Probabilité de mutation par trait (0 = aucun bruit, V0.1).</summary>
    public double MutationRate { get; set; } = 0.0;

    /// <summary>Amplitude d'une mutation (± <c>mutationMagnitude</c>, borné à [0, 2]).</summary>
    public double MutationMagnitude { get; set; } = 0.1;

    /// <summary>Seuil de salience d'un souvenir parental pour être transmis (ex-V0.1 <c>DefaultSalienceThreshold</c>).</summary>
    public double SalienceThreshold { get; set; } = 0.01;
}

/// <summary>
/// Cycle de vie — mortalité (SYNE-074, SYSTEM_SPEC.md §8, Monographie §6.2.4-6.2.5) :
/// une entité dont l'énergie atteint 0 (épuisement) meurt : retrait du monde
/// (grille spatiale + index), des groupes et de la cognition, puis émission d'un
/// événement <c>agent_died</c> (API_CONTRACTS.md §2.2).
/// </summary>
public sealed class LifeSettings
{
    /// <summary>Mortalité active.</summary>
    public bool DeathEnabled { get; set; } = true;

    /// <summary>Energie fatale : une entité dont l'énergie est ≤ ce seuil meurt (défaut 0 — épuisement total).</summary>
    public double DeathEnergyThreshold { get; set; } = 0.0;

    /// <summary>Cause d'une mort par épuisement (événement <c>agent_died</c>).</summary>
    public string EnergyExhaustionCause { get; set; } = "energy_exhaustion";
}

/// <summary>
/// Cheminement (SYNE-077, Monographie §6.2.12) : A* déterministe sur grille
/// rasterisée (les disques obstacles bloquent leurs cellules), voisinage ordonné
/// + tie-break, cache de chemins LRU borné, repli « sur place » quand
/// aucun chemin n'existe (destination inaccessible ou expansion plafonnée).
/// </summary>
public sealed class PathfindingSettings
{
    /// <summary>Cheminement actif (peut être neutralisé par l'option V0.1).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Taille de cellule de la grille rasterisée (unités monde).</summary>
    public double CellSize { get; set; } = 10.0;

    /// <summary>Plafond d'expansion d'A* (cellules) — repli « sur place » au-delà.</summary>
    public int MaxExpansionCells { get; set; } = 4096;

    /// <summary>Capacité du cache de chemins LRU (mémoïsation source → but).</summary>
    public int CacheCapacity { get; set; } = 256;
}