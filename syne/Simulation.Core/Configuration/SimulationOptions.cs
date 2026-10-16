// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Options de simulation : valeurs typées et défauts de toutes les sections de configuration.

namespace Simulation.Core.Configuration;

/// <summary>
/// Options de simulation. Structure parallèle au fichier config.json :
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
    /// <summary>Taille des cellules de la description Unreal (unités monde).</summary>
    public double WorldCellSize { get; set; } = 10;
    public int MaxTicks { get; set; } = 1_000_000;
    public int TicksPerSecond { get; set; } = 10;

    /// <summary>
    /// Secondes simulées par tick (« échelle temporelle configurable »,
    ///— le second levier de temporalité, à côté de <see cref="TicksPerSecond"/>.
    /// Défaut <b>60</b> : 1 tick = 1 minute simulée (temporalité historique,
    ///— neutre par défaut, checksums dorés inchangés. Le profil
    /// <c>prism</c> porte 5 (1 tick = 5 s simulées, ratio R = 30 à 6 TPS).
    /// Plage validée [1, 3600]. Les quantités de classe A (taux de besoins,
    /// décroissances, régénérations, durées) sont mises à l'échelle une fois au
    /// démarrage du run (<see cref="TemporalScale"/>, <see cref="SimulationClock"/>).
    /// </summary>
    public int SimulatedSecondsPerTick { get; set; } = SimulationClock.DefaultSimulatedSecondsPerTick;
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

    /// <summary>Héritage intergénérationnel (SYNE-063) : mécanismes fins configurables.</summary>
    public InheritanceSettings Inheritance { get; set; } = new();

    /// <summary>Cycle de vie — mortalité par épuisement (SYNE-074).</summary>
    public LifeSettings Life { get; set; } = new();

    /// <summary>Cheminement (SYNE-077) : A* déterministe sur grille rasterisée + cache LRU.</summary>
    public PathfindingSettings Pathfinding { get; set; } = new();
}

public sealed class NeedsSettings
{
    public double HungerRate { get; set; } = 0.5;
    public double ThirstRate { get; set; } = 0.7;
    public double FatigueRate { get; set; } = 0.3;

    /// <summary>
    /// Dérive d'insécurité par tick — <b>le besoin <c>Safety</c> DIMINUE de ce
    /// taux</b>, conformément à la dérive vers l'insécurité décrite par la
    /// monographie (ch. X : la sûreté se dégrade, elle ne s'améliore pas
    /// passivement).
    /// <para>
    /// Correction de réalignement : le signe était inverse (le besoin
    /// <i>augmentait</i> vers 1 et, initialisé à 1, ne pouvait jamais franchir le
    /// seuil de fuite — <c>Flee</c> était inatteignable par dérive). Calibration :
    /// 0,0002/tick, alignée sur la dérive sociale, atteint le seuil (0,5) au tick
    /// ~2500 — soit la fin de l'horizon de calibration — au lieu de saturer un run
    /// court en fuite permanente. La sûreté se rétablit par l'action <c>Flee</c>
    /// (arrivée à une distance de sécurité d'une menace connue).
    /// </para>
    /// </summary>
    public double SafetyDriftRate { get; set; } = 0.0002;

    /// <summary>
    /// Dérive du besoin social — <b>réalignement (arbitrage Phase 0/2, D-14/B-08)</b>.
    /// La calibration B1 avait ramené ce taux à 0,0002 parce que <c>Socialize</c> ne
    /// réduisait <b>aucun</b> besoin : une dérive plus vive produisait un déplacement
    /// muet permanent (jusqu'à 96 % des décisions) au lieu d'une boucle homéostatique.
    /// Depuis le réalignement, <c>Socialize</c> <b>est un exutoire</b>
    /// (<c>socialRecovery</c> appliqué à l'arrivée) : la dérive est rétablie à une
    /// valeur qui produit un <b>cycle</b> (déclenchement au tick ~230, satisfaction à
    /// l'arrivée, redescente sous le seuil) au lieu d'une saturation ou d'un besoin
    /// inerte. Le taux reste un paramètre : les profils peuvent le recalibrer.
    /// </summary>
    public double SocialDriftRate { get; set; } = 0.003;

    /// <summary>
    /// Dérive du besoin de curiosité — même arbitrage que <see cref="SocialDriftRate"/> :
    /// <c>Explore</c> est désormais un exutoire (<c>curiosityRecovery</c> à l'arrivée),
    /// donc la dérive retrouve une valeur de cycle (seuil 0,3 franchi au tick ~150)
    /// plutôt que la valeur de masquage de la calibration B1 (0,0005 → tick ~600).
    /// </summary>
    public double CuriosityDriftRate { get; set; } = 0.002;

    /// <summary>Seuil de déclenchement : un besoin non satisfait lance une action dès ce niveau (« ≥ 50 »).</summary>
    public double HungerTriggerThreshold { get; set; } = 50.0;

    public double ThirstTriggerThreshold { get; set; } = 50.0;

    /// <summary>Seuil de déclenchement du repos (Rest dès fatigue &gt; 70).</summary>
    public double FatigueTriggerThreshold { get; set; } = 70.0;
}

public sealed class PerceptionSettings
{
    /// <summary>Rayon de perception — : défaut 50 (plage 30–70).</summary>
    public int Radius { get; set; } = 50;

    /// <summary>
    /// Portée de perception des <b>gisements</b> (repères statiques) — décision de
    /// modèle du réalignement : les gisements sont des repères du paysage, perçus
    /// de plus loin que les entités mobiles (défaut 200 contre 50). Sans cette
    /// portée, une source localisée dans un monde 500 × 500 est invisible depuis un
    /// point de départ aléatoire et la navigation dirigée n'a aucun objet à
    /// connaître : la boucle perception → mémoire → croyance → but serait vide.
    /// La perception reste <b>partielle</b> (rayon fini, masquage par obstacle) :
    /// la portée décide de ce qui est découvrable, pas de ce qui est vrai.
    /// </summary>
    public int ResourceRadius { get; set; } = 200;
    public double ConfidenceFalloff { get; set; } = 0.3;
    /// <summary>Perception étagée : rotation en groupes de <c>RotationInterval</c>.</summary>
    public int RotationInterval { get; set; } = 4;
    /// <summary>Ligne de vue : un obstacle masque la perception (SYNE-011).</summary>
    public bool LineOfSight { get; set; } = true;

    /// <summary>
    /// Taille des cellules de la grille spatiale, en unités monde. Volontairement
    /// <b>distincte</b> de <see cref="Radius"/> : la taille de cellule décide de
    /// l'ordre dans lequel <c>SpatialGrid.QueryCircle</c> parcourt les entités
    /// voisines, donc de l'ordre de consideration des perceptions — et par là de
    /// la trajectoire. Elle ne doit donc pas être un effet de bord d'un réglage
    /// de perception. C'est un paramètre de performance, à figer par graine.
    /// Valeur par défaut alignée sur le rayon de perception historique, afin de
    /// préserver la trajectoire de référence.
    /// </summary>
    public double SpatialCellSize { get; set; } = 50;
}

public sealed class MemorySettings
{
    public int MaxCapacity { get; set; } = 1000;
    public double RecallThreshold { get; set; } = 0.01;
    /// <summary>Décroissance des souvenirs d'observation.</summary>
    public double ObservationDecayRate { get; set; } = 0.01;
    public double EventDecayRate { get; set; } = 0.005;
    public double InteractionDecayRate { get; set; } = 0.002;
}

public sealed class BeliefSettings
{
    /// <summary>Force de révision : <c>belief = belief + (signal − belief) × strength</c>.</summary>
    public double UpdateStrength { get; set; } = 0.3;
    /// <summary>Plafond de variation de confiance par snap (« plafond par snap »).</summary>
    public double MaxChangePerSnap { get; set; } = 0.5;
    public double AlignBonus { get; set; } = 0.2;
    public double ConflictPenalty { get; set; } = 0.1;
    public ulong ExpiryTicks { get; set; } = 100;
    public double ExpiredCap { get; set; } = 0.4;
    public double TimeDecayPerTick { get; set; } = 0.999;
}

public sealed class TrustSettings
{
    /// <summary>Confiance d'une première rencontre.</summary>
    public double InitialTrust { get; set; } = 0.5;

    /// <summary>Décroissance de confiance par tick sans interaction (trustDecay).</summary>
    public double DecayFactorPerTick { get; set; } = 0.9;

    /// <summary>Bonus de confiance après une vérité constatée (plafond 1.0).</summary>
    public double TruthBonus { get; set; } = 0.05;

    /// <summary>Pénalité de confiance après un mensonge constaté (plancher 0.0).</summary>
    public double LiePenalty { get; set; } = 0.2;

    /// <summary>Bonus de confiance quand un engagement est tenu (D5, CommitmentStatus.Fulfilled).</summary>
    public double CommitmentBonus { get; set; } = 0.10;

    /// <summary>Pénalité de confiance quand un engagement est rompu (D5, CommitmentStatus.Broken) — distincte du mensonge factuel.</summary>
    public double CommitmentPenalty { get; set; } = 0.15;
}

public sealed class ActionSettings
{
    /// <summary>
    /// Coût énergétique d'un déplacement — <b>calibration B1 </b> :
    /// 0,5 → 0,03. Le mouvement est l'action majoritaire (75 à 98 % des ticks en
    /// régime établi) : à 0,5 (puis 0,05 en D1) son coût dépasse le revenu
    /// amorti de Eat/Drink (≈ 0,057/tick) et l'énergie moyenne décroît sans borne
    /// jusqu'à l'épuisement (extinction lente).
    /// </summary>
    public double MoveEnergyCost { get; set; } = 0.03;

    /// <summary>Énergie regagnée par Repos — calibration B1 (hérité de D1).</summary>
    public double RestEnergyGain { get; set; } = 1.5;

    /// <summary>Fatigue récupérée par Repos — calibration B1 (hérité de D1).</summary>
    public double RestFatigueRecovery { get; set; } = 2.0;
    public DeliberationSettings Deliberation { get; set; } = new();
    public InterruptionSettings Interruption { get; set; } = new();

    /// <summary>Bibliothèque de plans candidats par objectif (D3, — désactivée par défaut, trajectoire de référence inchangée).</summary>
    public PlansSettings Plans { get; set; } = new();

    /// <summary>Inventaire des entités (D8, — désactivé par défaut).</summary>
    public InventorySettings Inventory { get; set; } = new();

    /// <summary>Contrôle de saillance (D2, — désactivé par défaut).</summary>
    public SalienceSettings Salience { get; set; } = new();

    /// <summary>Engagements communicationnels (D5, — désactivés par défaut).</summary>
    public CommitmentSettings Commitments { get; set; } = new();

    /// <summary>Catalogue déclaratif des actions (SYNE-040).</summary>
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
        ["eat"] = new ActionEntrySettings { EnergyCost = 0.2, HungerRecovery = 30.0, Reserve = World.ResourceKind.Food, EnergyRecovery = 2.0 },
        ["drink"] = new ActionEntrySettings { EnergyCost = 0.2, ThirstRecovery = 30.0, Reserve = World.ResourceKind.Water, EnergyRecovery = 1.0 },
        ["rest"] = new ActionEntrySettings(),
        // Exutoires de besoin (réalignement, S-13/B-08) : Socialize et Explore
        // étaient déclarés « mouvement » sans qu'aucune entrée du catalogue ne
        // réduise Social/Curiosity — le besoin entretenait un manque sans le
        // réduire. Les satisfactions sont déclarées ici et appliquées sur
        // l'arrivée à la cible connue (pair cru / repère cru).
        ["flee"] = new ActionEntrySettings { Movement = true, SafetyRecovery = 0.30 },
        ["socialize"] = new ActionEntrySettings { Movement = true, SocialRecovery = 0.35 },
        ["explore"] = new ActionEntrySettings { Movement = true, CuriosityRecovery = 0.50 },
        // Primitives atomiques D7 (engineVersion 0.14.0) :
        // déclarées pour la complétude du catalogue (chaque DesireKind DOIT avoir une
        // entrée), mais inertes tant que agents.actions.plans.enabled = false (D3) —
        // elles ne sont jamais générées par la délibération par défaut.
        ["take"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["give"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["trade"] = new ActionEntrySettings { EnergyCost = 0.2 },
        ["attack"] = new ActionEntrySettings { EnergyCost = 0.5 },
        ["defend"] = new ActionEntrySettings(),
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

    /// <summary>Rétablissement de la sûreté (0-1) — exutoire de <c>Flee</c> (réalignement, S-13).</summary>
    public double? SafetyRecovery { get; set; }

    /// <summary>Réduction du besoin social (0-1) — exutoire de <c>Socialize</c> (réalignement, B-08).</summary>
    public double? SocialRecovery { get; set; }

    /// <summary>Réduction du besoin de curiosité (0-1) — exutoire de <c>Explore</c> (réalignement, B-08).</summary>
    public double? CuriosityRecovery { get; set; }

    /// <summary>Réserve globale requise/consommée (ex. Eat → Food).</summary>
    public World.ResourceKind? Reserve { get; set; }

    /// <summary>Quantité consommée de la réserve à chaque exécution (défaut 1.0).</summary>
    public double? ReserveConsumption { get; set; }
}

public sealed class DeliberationSettings
{
    /// <summary>Fréquence de délibération : ticks entre deux délibérations (défaut 10).</summary>
    public int IntervalTicks { get; set; } = 10;

    /// <summary>Bonus d'alignement avec l'objectif courant (×1.2).</summary>
    public double AlignBonus { get; set; } = 1.2;

    /// <summary>Bonification maximale d'alignement avec l'objectif collectif (SYNE-076, ×1.2 par défaut).</summary>
    public double CollectiveAlignBonus { get; set; } = 1.2;

    /// <summary>Marge anti-oscillation (hystérésis) : passer à une nouvelle action seulement si elle dépasse l'action courante de cette marge (défaut 0.05).</summary>
    public double ActionSwitchMargin { get; set; } = 0.05;

    /// <summary>Marge de conflit de priorités : candidats à moins de cette marge du maximum → résolution probabiliste confiance×force.</summary>
    public double ConflictTieMargin { get; set; } = 0.5;
}

public sealed class InterruptionSettings
{
    /// <summary>Interruptions actives.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Marge : interruption seulement si l'utilité du besoin critique surpasse l'action courante de cette marge (défaut 10).</summary>
    public double UtilityExcessMargin { get; set; } = 10.0;

    /// <summary>Seuil de faim critique (défaut 85).</summary>
    public double CriticalHunger { get; set; } = 85.0;

    /// <summary>Seuil d'énergie critique (défaut 10).</summary>
    public double CriticalEnergy { get; set; } = 10.0;
}

/// <summary>
/// Bibliothèque de plans candidats par objectif (D3,
/// engineVersion 0.14.0). Désactivée par défaut : la délibération ne voit que les
/// objectifs de besoin (comportement historique). Activée, elle reçoit aussi des
/// candidats de manipulation (Prendre/Donner/Échanger) — see DesireFactory.ResolveTerminal.
/// </summary>
public sealed class PlansSettings
{
    /// <summary>Bibliothèque de plans active (D3).</summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Inventaire des entités (D8, engineVersion 0.14.0).
/// Désactivé par défaut : Eat/Drink consomment directement les réserves mondiales
/// (comportement historique), la trajectoire du scénario de référence est préservée.
/// Activé, les primitives Prendre/Donner/Échanger (D7) opèrent sur une capacité de
/// poids par entité, et Consommer a un rendement dégradé hors stock (règle d'incitation).
/// </summary>
public sealed class InventorySettings
{
    /// <summary>Inventaire actif (D8) — les primitives Take/Give/Trade opèrent dessus.</summary>
    public bool Enabled { get; set; }

    /// <summary>Capacité de poids par entité (capacitePoids — défaut posé, calibrable).</summary>
    public double CapacityWeight { get; set; } = 20.0;

    /// <summary>Quantité par primitive Prendre (transfert réserve → inventaire).</summary>
    public double TakeAmount { get; set; } = 5.0;

    /// <summary>Quantité par primitive Donner (transfert inventaire → cible, don sans contrepartie).</summary>
    public double GiveAmount { get; set; } = 1.0;

    /// <summary>Quantité par primitive Échanger (1 contre 1, Food ↔ Water V0.1).</summary>
    public double TradeAmount { get; set; } = 1.0;
}

/// <summary>
/// Contrôle de saillance (D2, engineVersion
/// 0.14.0) : étape 3bis de la boucle cognitive — entre deux délibérations, une
/// entité poursuit son intention sauf saillance (besoin franchi, condition
/// d'interruption) ou filet de sécurité périodique. Désactivé par défaut.
/// </summary>
public sealed class SalienceSettings
{
    /// <summary>Contrôle de saillance actif (D2).</summary>
    public bool Enabled { get; set; }

    /// <summary>Seuil de score déclenchant la délibération complète.</summary>
    public double ReconsiderThreshold { get; set; } = 1.0;

    /// <summary>Poids d'un franchissement de seuil de besoin depuis la dernière délibération.</summary>
    public double NeedThresholdWeight { get; set; } = 1.0;

    /// <summary>Poids d'une condition d'interruption remplie (généralisation).</summary>
    public double InterruptionConditionWeight { get; set; } = 2.0;

    /// <summary>Filet de sécurité : reconsidération forcée tous les N ticks sans saillance (0 = désactivé).</summary>
    public int ForcedReconsiderationTicks { get; set; } = 50;
}

/// <summary>
/// Engagements communicationnels (D5,
/// engineVersion 0.14.0) : structure Commitment créée à la réception d'une réponse
/// positive à une requête, objectif candidat, impact distinct sur la confiance.
/// Désactivé par défaut.
/// </summary>
public sealed class CommitmentSettings
{
    /// <summary>Engagements actifs (D5).</summary>
    public bool Enabled { get; set; }

    /// <summary>Durée de validité d'un engagement : non honoré au-delà → Broken (expiryTick).</summary>
    public ulong ExpiryTicks { get; set; } = 100;
}

public sealed class ResourceSettings
{
    /// <summary>
    /// Gisements <b>localisés</b> actifs — décision de modèle du réalignement
    /// (Phase 0) : les ressources sont des sources spatiales que l'agent doit
    /// découvrir, mémoriser, croire et rejoindre, et non un stock mondial abstrait.
    /// Les réserves globales (<c>resources.food/water/wood/mineral</c>) ne sont plus
    /// qu'un <b>agrégat publié</b> (somme des gisements) pour l'observabilité : la
    /// consommation et la régénération se font au gisement.
    /// <para>
    /// Faux = modèle historique (réserve mondiale unique) : aucune source
    /// spatiale n'est posée et Eat/Drink prélèvent la réserve globale. Les deux
    /// modèles sont testés ; le profil de référence porte le modèle localisé.
    /// </para>
    /// </summary>
    public bool Localized { get; set; } = true;

    /// <summary>Nombre de gisements par type (doit suivre la description du monde : 3).</summary>
    public int NodesPerType { get; set; } = 3;

    /// <summary>Quantité initiale d'un gisement (égale à la quantité publiée par la description du monde).</summary>
    public double NodeInitialAmount { get; set; } = 100.0;

    /// <summary>Capacité d'un gisement (plafond de régénération).</summary>
    public double NodeCapacity { get; set; } = 100.0;

    /// <summary>Régénération d'un gisement par tick (module saisonnier appliqué).</summary>
    public double NodeRegenerationRate { get; set; } = 1.0;

    /// <summary>
    /// Rayon d'interaction avec un gisement : distance maximale à laquelle une
    /// action terminale (Eat/Drink/Take) prélève la source, et à laquelle
    /// l'arrivée à une destination est considérée atteinte.
    /// </summary>
    public double InteractionRadius { get; set; } = 2.0;

    /// <summary>
    /// Nourriture — <b>calibration B1 </b> : réserve initiale 20 000 et
    /// régénération nette 20/tick, **sans** dégradation (« degradationTick »
    /// neutralisé) : avec une dégradation de période, la régénération est
    /// intégralement annulée en fin de période (apport net nul) et la réserve se
    /// réduit à sa valeur initiale — 100 agents épuisent alors les 10 000 de D1
    /// avant t2000. L'horizon de calibration est 2500 ticks.
    /// </summary>
    public ResourceSpec Food { get; set; } = new() { Initial = 20_000, RegenerationRate = 20, DegradationTick = null };

    /// <summary>Eau — calibration B1 : réserve 20 000, régénération nette 10/tick.</summary>
    public ResourceSpec Water { get; set; } = new() { Initial = 20_000, RegenerationRate = 10 };
    public ResourceSpec Wood { get; set; } = new() { Initial = 50, RegenerationRate = 0.1 };
    public ResourceSpec Mineral { get; set; } = new() { Initial = 0, RegenerationRate = 0 };

    /// <summary>Niveaux initiaux des quatre réserves (SYNE-070), ordre stable du type.</summary>
    internal Dictionary<World.ResourceKind, double> ToStocks()
    {
        return new Dictionary<World.ResourceKind, double>
        {
            [World.ResourceKind.Food] = Food.Initial,
            [World.ResourceKind.Water] = Water.Initial,
            [World.ResourceKind.Wood] = Wood.Initial,
            [World.ResourceKind.Mineral] = Mineral.Initial,
        };
    }

    /// <summary>Spécification d'une ressource par type (SYNE-070) — résolution garantie.</summary>
    internal ResourceSpec Spec(World.ResourceKind kind)
    {
        return kind switch
        {
            World.ResourceKind.Food => Food,
            World.ResourceKind.Water => Water,
            World.ResourceKind.Wood => Wood,
            World.ResourceKind.Mineral => Mineral,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Type de ressource inconnu."),
        };
    }
}

public sealed class ResourceSpec
{
    public int Initial { get; set; }
    public double RegenerationRate { get; set; }
    public int? DegradationTick { get; set; }
}

public sealed class CommunicationSettings
{
    /// <summary>Max d'envois par entité et par tick (5 ; calibration B1 : 1).</summary>
    public int MaxSendsPerTick { get; set; } = 1;

    /// <summary>Max de réceptions traitées par tick et par entité (3 ; calibration B1 : 1).</summary>
    public int MaxReceivesPerTick { get; set; } = 1;

    /// <summary>Probabilité d'incompréhension d'un message reçu (0.05).</summary>
    public double IncomprehensionRate { get; set; } = 0.05;

    /// <summary>Décroissance de confiance inter-entités (0.9, valeurs relatives au trust).</summary>
    public double TrustDecay { get; set; } = 0.9;

    /// <summary>Portée effective de transmission d'une pulsation (20 u. héritées du prototype, configurable).</summary>
    public int TransmissionRange { get; set; } = 55;

    /// <summary>Relais entité-à-entité actif (SYNE-050) — neutralisé en calibration B1 avec les coûts énergétiques.</summary>
    public bool RelayEnabled { get; set; } = false;

    /// <summary>Nombre maximal de sauts d'un message relayé (anti-boucle, borne de dégradation).</summary>
    public int MaxHops { get; set; } = 2;

    /// <summary>
    /// Coût énergétique d'émission — : <c>sendEnergyCost + payload × sendEnergyPayloadFactor</c>
    /// (0.5 + p×0.1 hérités ; **neutres depuis la calibration B1** : à ~0,2 envoi/agent/tick
    /// l'envoi coûtait ~0,12 énergie/tick, soit 4× le coût de déplacement recalibré).
    /// </summary>
    public double SendEnergyCost { get; set; } = 0;

    public double SendEnergyPayloadFactor { get; set; } = 0;

    /// <summary>Coût énergétique de réception — (neutre depuis la calibration B1).</summary>
    public double ReceiveEnergyCost { get; set; } = 0;

    public double ReceiveEnergyPayloadFactor { get; set; } = 0;

    /// <summary>Décroissance de confiance par relais — : <c>confidence × hopConfidenceDecay</c> (0.9, ≈ 10 %/hop).</summary>
    public double HopConfidenceDecay { get; set; } = 0.9;
}

public sealed class WorldSettings
{
    /// <summary>
    /// Mètres par unité SYNE — clé <b>informative</b>, jamais
    /// utilisée dans un calcul du moteur : elle est seulement transmise aux
    /// clients (PRISM : échelle k = 100 uu / unité, 1 unité = 1 m).
    /// Défaut 1,0 (unité = mètre) ; doit être &gt; 0.
    /// </summary>
    public double MetersPerUnit { get; set; } = 1.0;

    /// <summary>
    /// Cycle de saisons (SYNE-072) : objet <c>world.seasons</c> — l'ancien drapeau
    /// booléen homonyme (mort, jamais consommé) est remplacé par ce bloc actif.
    /// Voir <see cref="SeasonSettings"/>.
    /// </summary>
    public SeasonSettings Seasons { get; set; } = new();
    public bool Events { get; set; }

    /// <summary>
    /// Active le placement des obstacles statiques configurés (SYNE-071).
    /// Les constructions sont des obstacles statiques de la grille :
    /// disposés par <see cref="ObstacleLayout"/> à l'init, poés/retirés en cours de run
    /// par <c>World.PlaceConstruction</c>/<c>RemoveConstruction</c> (modification
    /// d'environnement tracée, événement <c>world.construction_placed</c>).
    /// </summary>
    public bool Obstacles { get; set; }

    /// <summary>
    /// Layout initial des obstacles statiques (disques) posés à l'init quand
    /// <see cref="Obstacles"/> est vrai (SYNE-071).
    /// </summary>
    public List<StaticObstacleSettings> ObstacleLayout { get; set; } = [];

    /// <summary>
    /// Zones de territoire (SYNE-073) :
    /// bloc <c>world.territories</c>. V0.1 : concept d'**observation** — la
    /// présence des entités dans une zone délimite le territoire effectif,
    /// suivi par la boucle et émis sans aucun comportement agentique.
    /// Désactivé par défaut ⇒ trajectoire du scénario de référence inchangée.
    /// </summary>
    public TerritorySettings Territories { get; set; } = new();

    /// <summary>
    /// Livres (SYNE-121) : bloc
    /// <c>world.books</c>. V0.1 : écriture/consultation via l'API de la boucle
    /// (coût en énergie payé par l'auteur ; bénéfice de lecture
    /// posé en principe —), activités seulement si
    /// <c>enabled</c>. Désactivé par défaut ⇒ trajectoire du scénario de référence
    /// inchangée (re-pin contractuel).
    /// </summary>
    public BookSettings Books { get; set; } = new();
}

/// <summary>
/// Définition d'une saison du cycle (SYNE-072, <c>world.seasons.cycle[]</c>) :
/// facteurs de régénération par ressource appliqués en fin de tick quand le
/// cycle est actif (<c>world.seasons.enabled</c>). Facteur 1.0 = taux nominal ;
/// &gt; 1 = saison favorable, &lt; 1 = saison défavorable. Pur (0 tirage PRNG,
/// la saison courante et son facteur sont des fonctions
/// déterministes du tick.
/// </summary>
public sealed class SeasonDefinition
{
    /// <summary>Nom de la saison (clé JSON camelCase) : <c>spring</c>, <c>summer</c>, <c>autumn</c>, <c>winter</c>.</summary>
    public string Name { get; set; } = string.Empty;

    public double FoodFactor { get; set; } = 1.0;
    public double WaterFactor { get; set; } = 1.0;
    public double WoodFactor { get; set; } = 1.0;
    public double MineralFactor { get; set; } = 1.0;
}

/// <summary>
/// Cycle de saisons (SYNE-072, <c>world.seasons</c>) : l'ancien drapeau booléen
/// homonyme (mort, jamais consommé aucun comportement) devient ce bloc actif —
/// <c>enabled</c> remplit désormais ce rôle. Cycle déterministe de 4 saisons :
/// saison(tick) = (indexInitial + tick / seasonLengthTicks) mod 4, sans aucun
/// tirage PRNG ; les facteurs de régénération sont appliqués
/// en fin de tick sur le cycle des ressources (SYNE-070). Saisons désactivées par
/// défaut ⇒ trajectoire du scénario de référence inchangée (re-pin contractuel).
/// </summary>
public sealed class SeasonSettings
{
    /// <summary>Cycle de saisons actif (régénération modulée + événement <c>world.season_changed</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Saison du tick 0 (clé JSON camelCase, défaut <c>spring</c>).</summary>
    public string InitialSeason { get; set; } = World.Seasons.Name(World.Season.Spring);

    /// <summary>Durée d'une saison en ticks (défaut 360, temps simulé &gt; 6 h simulées à 1 tick/min).</summary>
    public int SeasonLengthTicks { get; set; } = 360;

    /// <summary>
    /// Les 4 définitions du cycle (une par saison, ordre indifférent) — défaut :
    /// spring (toutes ressources ×1), summer (eau ×1,2), autumn (bois ×1,2
    /// + nourriture ×1,1), winter (nourriture ×0,8, eau ×0,9) — valeurs V0.1
    /// de premier jet, calibrées en SYNE-120.
    /// </summary>
    public List<SeasonDefinition> Cycle { get; set; } =
    [
        new() { Name = World.Seasons.Name(World.Season.Spring), FoodFactor = 1.0, WaterFactor = 1.0, WoodFactor = 1.0, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Summer), FoodFactor = 1.0, WaterFactor = 1.2, WoodFactor = 1.0, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Autumn), FoodFactor = 1.1, WaterFactor = 1.0, WoodFactor = 1.2, MineralFactor = 1.0 },
        new() { Name = World.Seasons.Name(World.Season.Winter), FoodFactor = 0.8, WaterFactor = 0.9, WoodFactor = 1.0, MineralFactor = 1.0 },
    ];

    /// <summary>Facteurs de régénération des 4 ressources pour un tick donné (cycle résolu, déterminisme).</summary>
    public SeasonFactors Factors(ulong tick)
    {
        World.Season season = At(tick);
        double food = 1.0, water = 1.0, wood = 1.0, mineral = 1.0;
        foreach (SeasonDefinition definition in Cycle)
        {
            if (DefinitionsByName.TryGetValue(definition.Name, out World.Season matches) && matches == season)
            {
                food = definition.FoodFactor;
                water = definition.WaterFactor;
                wood = definition.WoodFactor;
                mineral = definition.MineralFactor;
                break;
            }
        }

        return new SeasonFactors(food, water, wood, mineral);
    }

    /// <summary>Saison courante au tick <paramref name="tick"/> — fonction pure du tick (0 PRNG).</summary>
    public World.Season At(ulong tick)
    {
        int initialIndex = (World.Seasons.TryParse(InitialSeason) ?? World.Season.Spring) switch
        {
            World.Season.Spring => 0,
            World.Season.Summer => 1,
            World.Season.Autumn => 2,
            World.Season.Winter => 3,
            _ => 0,
        };
        int length = SeasonLengthTicks > 0 ? SeasonLengthTicks : 1;
        return (World.Season)((initialIndex + (int)(tick / (ulong)length)) % World.Seasons.Count);
    }

    private static readonly IReadOnlyDictionary<string, World.Season> DefinitionsByName =
        new Dictionary<string, World.Season>(StringComparer.Ordinal)
        {
            [World.Seasons.Name(World.Season.Spring)] = World.Season.Spring,
            [World.Seasons.Name(World.Season.Summer)] = World.Season.Summer,
            [World.Seasons.Name(World.Season.Autumn)] = World.Season.Autumn,
            [World.Seasons.Name(World.Season.Winter)] = World.Season.Winter,
        };
}

/// <summary>Facteurs de régénération des 4 ressources pour le cycle de saisons (SYNE-072).</summary>
public readonly record struct SeasonFactors(double Food, double Water, double Wood, double Mineral)
{
    /// <summary>Facteur applicable à une ressource donnée (repli 1.0 si inconnu).</summary>
    public double For(World.ResourceKind kind) => kind switch
    {
        World.ResourceKind.Food => Food,
        World.ResourceKind.Water => Water,
        World.ResourceKind.Wood => Wood,
        World.ResourceKind.Mineral => Mineral,
        _ => 1.0,
    };
}

/// <summary>Changement de saison entre deux ticks (SYNE-072, événement <c>world.season_changed</c>).</summary>
public readonly record struct SeasonChange(World.Season Previous, World.Season Current);

/// <summary>
/// Pose d'un obstacle statique issu de la configuration <c>world.obstacleLayout</c>
/// (SYNE-071) : disque {Position, Radius}. Une construction posée
/// modifie la perception et le mouvement — modèle figé par la.
/// </summary>
public sealed class StaticObstacleSettings
{
    public string Id { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; } = 10.0;
}

/// <summary>
/// Définition d'une zone de territoire (SYNE-073, <c>world.territories.zones[]</c>,
/// disque « point de survie » {Center, Radius}. La présence d'une
/// entité dans le disque la délimite comme membre du territoire effectif —
/// concept d'observation V0.1 (aucun comportement agentique, 0 tirage PRNG).
/// </summary>
public sealed class TerritoryZoneDefinition
{
    public string Id { get; set; } = string.Empty;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; } = 20.0;
}

/// <summary>
/// Zones de territoire (SYNE-073) :
/// <c>world.territories</c>. V0.1 : la présence d'une entité dans une zone délimite
/// le **territoire effectif** (vue d'observation) — appartenance suivie en fin de
/// tick (0 tirage PRNG) et émise en <c>world.territory_membership_changed</c>.
/// Désactivé par défaut ⇒ trajectoire du scénario de référence inchangée (re-pin
/// contractuel). Les sources spatiales de ressources autour des points de survie
/// (ressources du territoire) restent **hors V0.1**.
/// </summary>
public sealed class TerritorySettings
{
    /// <summary>Suivi du territoire actif (appartenance + événements + snapshot <c>territories[]</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Les zones (disques) du territoire, posées à l'init dans cet ordre (déterministe).</summary>
    public List<TerritoryZoneDefinition> Zones { get; set; } = [];
}


/// <summary>
/// Livres (SYNE-121) : <c>world.books</c>.
/// L'écriture d'un livre matérialise une connaissance à un instant T au **coût**
/// (énergie) payé par l'auteur ; la lecture produit un **bénéfice** posé en
/// principe (structure figée) — le bénéfice cognitif chiffré
/// dépend du moteur de mémoire et reste reporté (SYNE-131).
/// Chiffres V0.1 de premier jet, calibrés en SYNE-120.
/// Désactivé par défaut ⇒ trajectoire du scénario de référence inchangée.
/// </summary>
public sealed class BookSettings
{
    /// <summary>Activité livres (écriture/consultation, événements <c>world.book_*</c>, snapshot <c>books[]</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Coût d'écriture en énergie payé par l'auteur.</summary>
    public double WriteCostEnergy { get; set; } = 20.0;


    /// <summary>Bénéfice de lecture posé en principe — effet cognitif reporté (mine mémoire).</summary>
    public double ReadBenefit { get; set; } = 1.0;
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
/// Formation émergente des groupes (SYNE-060/061, décisions
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

    /// <summary>Bonus d'affinité quand deux entités partagent leur objectif courant (buts partagés).</summary>
    public double GoalAlignmentBonus { get; set; } = 0.2;

    /// <summary>Quorum : une décision collective est adoptée quand ≥ cette fraction des membres partage la même intention.</summary>
    public double ConsensusThreshold { get; set; } = 0.5;

    /// <summary>
    /// La cohésion exige une <b>proximité spatiale actuelle</b> des deux membres —
    /// arbitrage de modèle du réalignement (ch. VII vs §2b.3) : un groupe est une
    /// structure <i>locale</i>, pas seulement une relation entretenue. Faux = ancienne
    /// règle (deux membres distants pouvaient rester liés par confiance et affinité
    /// persistantes).
    /// </summary>
    public bool ProximityRequired { get; set; } = true;

    /// <summary>Distance maximale entre deux membres pour qu'un lien de cohésion soit valide.</summary>
    public double ProximityRadius { get; set; } = 60.0;
}

/// <summary>
/// Cycle de vie — naissance par fusion consentie (SYNE-062/075,
/// fusion rare et volontaire, décision
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

    /// <summary>Consentement : confiance réciproque minimale du couple pour fusionner.</summary>
    public double ConsentTrustThreshold { get; set; } = 0.6;

    /// <summary>Plafond de naissances par tick (0 = aucune, garde-fou de population).</summary>
    public int MaxBirthsPerTick { get; set; } = 1;

    /// <summary>Proximité physique : distance maximale entre les parents pour fusionner (SYNE-075).</summary>
    public double MergeRange { get; set; } = 40.0;

    /// <summary>Énergie minimale des deux parents pour fusionner (SYNE-075).</summary>
    public double MergeMinimumEnergy { get; set; } = 30.0;

    /// <summary>La ligne de vue entre les parents doit être dégagée (SYNE-075, SYNE-011).</summary>
    public bool RequireLineOfSight { get; set; } = true;

    /// <summary>Ni l'un ni l'autre des parents ne doit être en état critique (SYNE-075 : faim/énergie critiques).</summary>
    public bool RequireNoCriticalNeed { get; set; } = true;
}

/// <summary>
/// Mécanismes fins d'héritage des traits (SYNE-063,
/// la structure (fusion + transmission) est figée ; la réadaptation/
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
/// Cycle de vie — mortalité (SYNE-074, -6.2.5) :
/// une entité dont l'énergie atteint 0 (épuisement) meurt : retrait du monde
/// (grille spatiale + index), des groupes et de la cognition, puis émission d'un
/// événement <c>agent_died</c>.
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
/// Cheminement (SYNE-077) : A* déterministe sur grille
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