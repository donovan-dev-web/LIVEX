namespace Simulation.Core.Configuration;

/// <summary>
/// Facteurs d'échelle temporelle (ADR-017 « échelle temporelle configurable ») :
/// conversion des quantités de <b>classe A</b> (processus continus du temps
/// simulé) en valeurs <b>par tick effectif</b>, calculée <b>une fois au
/// démarrage du run</b> de façon déterministe à partir de
/// <c>dt = simulatedSecondsPerTick / 60</c>.
///
/// <list type="bullet">
/// <item><b>A additif</b> (taux, régénérations, coûts énergétiques de
/// mouvement/repos) : valeur × <c>dt</c> — les valeurs de configuration
/// gardent leur sens « par minute simulée ».</item>
/// <item><b>A multiplicatif</b> (décroissances par tick : croyances,
/// confiance) : facteur ^ <c>dt</c>.</item>
/// <item><b>A période</b> (durées en ticks : expiryTicks, degradationTick,
/// seasonLengthTicks) : période_ticks = période_min / <c>dt</c>.</item>
/// </list>
///
/// <para>
/// <b>Neutralité par défaut</b> : à <c>dt == 1</c> (profil par défaut et
/// <c>reference</c>), chaque opération retourne <b>l'instance d'entrée</b> —
/// aucune copie, aucune arithmétique : les checksums dorés restent bit-à-bit
/// inchangés (DETERMINISM.md, pin contractuel ADR-016).
/// </para>
///
/// <para>
/// Les classes B (cadences mécaniques en ticks : délibération, rotation de
/// perception, revue de groupes, budgets de communication, sauvegarde, A*,
/// portées) et C (effets ponctuels par action : energyRecovery Eat/Drink,
/// pénalités de confiance) ne sont <b>jamais</b> passées par ici — cf.
/// l'inventaire A/B/C (docs/docs-syne/TICK_QUANTITIES.md).
/// </para>
/// </summary>
public static class TemporalScale
{
    /// <summary>Vrai si le pas temporel est l'unité historique (1 tick = 1 minute) — court-circuit bit-exact.</summary>
    public static bool IsUnitStep(double dt) => dt == 1.0;

    /// <summary>Élément d'un facteur multiplicatif par tick : <c>facteur ^ dt</c>, strictement <c>facteur</c> à <c>dt == 1</c>.</summary>
    public static double DecayPerTick(double factorPerTick, double dt)
    {
        if (IsUnitStep(dt))
        {
            return factorPerTick;
        }

        return Math.Pow(factorPerTick, dt);
    }

    /// <summary>Période en ticks effectifs : <c>période_min / dt</c>, strictement identique à <c>dt == 1</c>.</summary>
    public static ulong PeriodTicks(ulong periodTicks, double dt)
    {
        if (IsUnitStep(dt))
        {
            return periodTicks;
        }

        return (ulong)Math.Max(1.0, Math.Round(periodTicks / dt));
    }

    /// <inheritdoc cref="PeriodTicks(ulong, double)"/>
    public static int PeriodTicks(int periodTicks, double dt)
    {
        if (IsUnitStep(dt))
        {
            return periodTicks;
        }

        return (int)Math.Max(1.0, Math.Round(periodTicks / dt));
    }

    /// <summary>Taux par tick effectif : <c>taux × dt</c>, strictement identique à <c>dt == 1</c> (×1.0 exact).</summary>
    public static double RatePerTick(double ratePerMinute, double dt) => ratePerMinute * dt;

    /// <summary>
    /// Besoins (classe A : hungerRate, thirstRate, fatigueRate,
    /// safetyDriftRate, socialDriftRate, curiosityDriftRate) — les seuils de
    /// déclenchement (niveaux) restent inchangés (classe B).
    /// </summary>
    public static NeedsSettings ScaleNeeds(NeedsSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new NeedsSettings
        {
            HungerRate = RatePerTick(settings.HungerRate, dt),
            ThirstRate = RatePerTick(settings.ThirstRate, dt),
            FatigueRate = RatePerTick(settings.FatigueRate, dt),
            SafetyDriftRate = RatePerTick(settings.SafetyDriftRate, dt),
            SocialDriftRate = RatePerTick(settings.SocialDriftRate, dt),
            CuriosityDriftRate = RatePerTick(settings.CuriosityDriftRate, dt),
            HungerTriggerThreshold = settings.HungerTriggerThreshold,
            ThirstTriggerThreshold = settings.ThirstTriggerThreshold,
            FatigueTriggerThreshold = settings.FatigueTriggerThreshold,
        };
    }

    /// <summary>
    /// Mémoire (classe A : observationDecayRate, eventDecayRate,
    /// interactionDecayRate) — la salience est
    /// <c>exp(−decayRate × âge_en_ticks)</c>, donc le taux effectif par tick est
    /// <c>decayRate × dt</c> (même durée d'oubli en temps simulé).
    /// </summary>
    public static MemorySettings ScaleMemory(MemorySettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new MemorySettings
        {
            MaxCapacity = settings.MaxCapacity,
            RecallThreshold = settings.RecallThreshold,
            ObservationDecayRate = RatePerTick(settings.ObservationDecayRate, dt),
            EventDecayRate = RatePerTick(settings.EventDecayRate, dt),
            InteractionDecayRate = RatePerTick(settings.InteractionDecayRate, dt),
        };
    }

    /// <summary>
    /// Croyances (classe A : timeDecayPerTick multiplicatif → <c>^dt</c> ;
    /// expiryTicks période → <c>/dt</c>).
    /// </summary>
    public static BeliefSettings ScaleBeliefs(BeliefSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new BeliefSettings
        {
            UpdateStrength = settings.UpdateStrength,
            MaxChangePerSnap = settings.MaxChangePerSnap,
            AlignBonus = settings.AlignBonus,
            ConflictPenalty = settings.ConflictPenalty,
            ExpiryTicks = PeriodTicks(settings.ExpiryTicks, dt),
            ExpiredCap = settings.ExpiredCap,
            TimeDecayPerTick = DecayPerTick(settings.TimeDecayPerTick, dt),
        };
    }

    /// <summary>
    /// Confiance (classe A : decayFactorPerTick multiplicatif → <c>^dt</c>) —
    /// les bonus/pénalités ponctuels (classe C) restent inchangés.
    /// </summary>
    public static TrustSettings ScaleTrust(TrustSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new TrustSettings
        {
            InitialTrust = settings.InitialTrust,
            DecayFactorPerTick = DecayPerTick(settings.DecayFactorPerTick, dt),
            TruthBonus = settings.TruthBonus,
            LiePenalty = settings.LiePenalty,
            CommitmentBonus = settings.CommitmentBonus,
            CommitmentPenalty = settings.CommitmentPenalty,
        };
    }

    /// <summary>
    /// Actions (classe A : moveEnergyCost, restEnergyGain,
    /// restFatigueRecovery × dt) — les coûts/effets explicites du catalogue
    /// (classe C, ex. energyCost eat = 0,2) et les poids d'utilité (classe B)
    /// restent inchangés. Les sous-blocs immuables sont partagés (jamais
    /// mutés par le moteur).
    /// </summary>
    public static ActionSettings ScaleActions(ActionSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new ActionSettings
        {
            MoveEnergyCost = RatePerTick(settings.MoveEnergyCost, dt),
            RestEnergyGain = RatePerTick(settings.RestEnergyGain, dt),
            RestFatigueRecovery = RatePerTick(settings.RestFatigueRecovery, dt),
            Deliberation = settings.Deliberation,
            Interruption = settings.Interruption,
            Plans = settings.Plans,
            Inventory = settings.Inventory,
            Salience = settings.Salience,
            Commitments = settings.Commitments,
            Catalog = settings.Catalog,
        };
    }

    /// <summary>
    /// Ressources (classe A : regenerationRate × dt ; période degradationTick
    /// → <c>/dt</c> — la réserve perdue à chaque période reste la
    /// régénération cumulée de la période, donc la même en temps simulé).
    /// </summary>
    public static ResourceSettings ScaleResources(ResourceSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new ResourceSettings
        {
            Food = ScaleSpec(settings.Food, dt),
            Water = ScaleSpec(settings.Water, dt),
            Wood = ScaleSpec(settings.Wood, dt),
            Mineral = ScaleSpec(settings.Mineral, dt),
        };
    }

    /// <summary>
    /// Saisons (classe A : la durée d'une saison est exprimée en minutes
    /// simulées — <c>seasonLengthTicks / dt</c> ticks effectifs ; 360 minutes
    /// au défaut, soit 4 320 ticks à 5 s/tick — profil <c>prism</c>).
    /// </summary>
    public static SeasonSettings ScaleSeasons(SeasonSettings settings, double dt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsUnitStep(dt))
        {
            return settings;
        }

        return new SeasonSettings
        {
            Enabled = settings.Enabled,
            InitialSeason = settings.InitialSeason,
            SeasonLengthTicks = PeriodTicks(settings.SeasonLengthTicks, dt),
            Cycle = settings.Cycle,
        };
    }

    /// <summary>
    /// Options « effectives » d'un run (ADR-017 §4.2) : copie de premier niveau
    /// de <paramref name="options"/> dont le sous-arbre <c>agents</c> porte les
    /// réglages de classe A mis à l'échelle (besoins, mémoire, croyances,
    /// confiance, actions) — les autres sections sont partagées telles quelles.
    /// Le profil d'origine n'est jamais muté (deux runs construits depuis la
    /// même instance de config ne peuvent pas se double-scaler).
    /// </summary>
    internal static SimulationOptions WithScaledAgentSettings(
        SimulationOptions options,
        NeedsSettings needs,
        MemorySettings memory,
        BeliefSettings beliefs,
        TrustSettings trust,
        ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(options);
        var agents = new AgentSettings
        {
            InitialCount = options.Agents.InitialCount,
            Traits = options.Agents.Traits,
            Needs = needs,
            Perception = options.Agents.Perception,
            Memory = memory,
            Beliefs = beliefs,
            Trust = trust,
            Actions = actions,
            Inheritance = options.Agents.Inheritance,
            Life = options.Agents.Life,
            Pathfinding = options.Agents.Pathfinding,
        };

        return new SimulationOptions
        {
            Simulation = options.Simulation,
            Agents = agents,
            Resources = options.Resources,
            Communication = options.Communication,
            World = options.World,
            Random = options.Random,
            Performance = options.Performance,
            Groups = options.Groups,
            Reproduction = options.Reproduction,
        };
    }

    private static ResourceSpec ScaleSpec(ResourceSpec spec, double dt) => new()
    {
        Initial = spec.Initial,
        RegenerationRate = RatePerTick(spec.RegenerationRate, dt),
        DegradationTick = spec.DegradationTick is { } period ? PeriodTicks(period, dt) : null,
    };
}
