using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// État des besoins d'une entité (DATA_MODEL.md §7, décision n°3) :
/// 6 besoins (faim, soif, fatigue 0-100 ; sécurité, social, curiosité 0-1)
/// + énergie (0-100). Dérive par tick selon les taux configurables.
/// </summary>
public sealed class BodyNeeds
{
    public const double MaxResource = 100.0;

    // Échelles / seuils de prototype (DATA_MODEL.md §7, calibration décision n°6).
    public const double HungerThreshold = 60.0;
    public const double ThirstThreshold = 60.0;
    public const double FatigueThreshold = 70.0;
    public const double SafetyThreshold = 0.5;
    public const double SocialThreshold = 0.7;
    public const double CuriosityThreshold = 0.3;
    public const double CriticalHunger = 90.0;
    public const double CriticalEnergy = 10.0;

    public double Hunger { get; private set; }

    public double Thirst { get; private set; }

    public double Fatigue { get; private set; }

    public double Safety { get; private set; } = 1.0;

    public double Social { get; private set; }

    public double Curiosity { get; private set; }

    public double Energy { get; private set; } = 100.0;

    /// <summary>Valeur d'élan du besoin (0-100 homogénéisé) pour la génération d'objectifs.</summary>
    public double Drive(Simulation.Core.Cognition.DesireKind kind) => kind switch
    {
        Simulation.Core.Cognition.DesireKind.SeekFood => Hunger,
        Simulation.Core.Cognition.DesireKind.SeekWater => Thirst,
        Simulation.Core.Cognition.DesireKind.Rest => Fatigue,
        Simulation.Core.Cognition.DesireKind.Flee => (1.0 - Safety) * 100.0,
        Simulation.Core.Cognition.DesireKind.Socialize => Social * 100.0,
        Simulation.Core.Cognition.DesireKind.Explore => Curiosity * 100.0,
        _ => 0.0,
    };

    /// <summary>Indicateur « état critique » (énergie &lt; 10 ou faim &gt; 90, COGNITIVE_ARCHITECTURE.md §6).</summary>
    public bool IsCritical => Energy < CriticalEnergy || Hunger > CriticalHunger;

    /// <summary>État critique selon les seuils configurables (agents.interruption.*, COGNITIVE_ARCHITECTURE.md §6).</summary>
    public bool IsCriticalFor(InterruptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Energy < settings.CriticalEnergy || Hunger > settings.CriticalHunger;
    }

    /// <summary>Le besoin est au-dessus de son seuil de déclenchement (génère un désir).</summary>
    public bool IsTriggered(Simulation.Core.Cognition.DesireKind kind) => kind switch
    {
        Simulation.Core.Cognition.DesireKind.SeekFood => Hunger >= HungerThreshold,
        Simulation.Core.Cognition.DesireKind.SeekWater => Thirst >= ThirstThreshold,
        Simulation.Core.Cognition.DesireKind.Rest => Fatigue >= FatigueThreshold,
        Simulation.Core.Cognition.DesireKind.Flee => Safety <= SafetyThreshold,
        Simulation.Core.Cognition.DesireKind.Socialize => Social >= SocialThreshold,
        Simulation.Core.Cognition.DesireKind.Explore => Curiosity >= CuriosityThreshold,
        _ => false,
    };

    /// <summary>Dérive périodique des besoins (décision n°3 : faim +0.5/tick, soif +0.7/tick, fatigue +0.3/tick).</summary>
    public BodyNeeds Advance(NeedsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Hunger = Clamp100(Hunger + settings.HungerRate);
        Thirst = Clamp100(Thirst + settings.ThirstRate);
        Fatigue = Clamp100(Fatigue + settings.FatigueRate);
        Safety = Clamp01(Safety + settings.SafetyDriftRate);
        Social = Clamp01(Social + settings.SocialDriftRate);
        Curiosity = Clamp01(Curiosity + settings.CuriosityDriftRate);
        Energy = Clamp100(Energy);
        return this;
    }

    public void ExertEnergy(double cost) => Energy = Clamp100(Energy - cost);

    public void RecoverEnergy(double gain) => Energy = Clamp100(Energy + gain);

    public void RecoverFatigue(double amount) => Fatigue = Clamp100(Fatigue - amount);

    public void SetCuriosityElapsed(double amount) => Curiosity = Clamp01(Curiosity + amount);

    /// <summary>État de calibration (tests et V0.1) — accès interne.</summary>
    internal static BodyNeeds FromState(
        double hunger = 0.0,
        double thirst = 0.0,
        double fatigue = 0.0,
        double safety = 1.0,
        double social = 0.0,
        double curiosity = 0.0,
        double energy = 100.0)
    {
        var needs = new BodyNeeds
        {
            Hunger = Clamp100(hunger),
            Thirst = Clamp100(thirst),
            Fatigue = Clamp100(fatigue),
            Safety = Clamp01(safety),
            Social = Clamp01(social),
            Curiosity = Clamp01(curiosity),
            Energy = Clamp100(energy),
        };
        return needs;
    }

    private static double Clamp100(double value) => Math.Clamp(value, 0.0, MaxResource);

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}