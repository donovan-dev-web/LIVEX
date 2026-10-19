// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : État des besoins d'une entité : 6 besoins (faim, soif, fatigue 0-100 ; sécurité, social, curiosité 0-1) +
//        énergie (0-100).

using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// État des besoins d'une entité :
/// 6 besoins (faim, soif, fatigue 0-100 ; sécurité, social, curiosité 0-1)
/// + énergie (0-100). Dérive par tick selon les taux configurables.
/// </summary>
public sealed class BodyNeeds
{
    public const double MaxResource = 100.0;

    // Échelles / seuils de prototype (besoins
    // déclenchés dès « ≥ 50 » (équivalent des seuils configurables par défaut)).
    public const double HungerThreshold = 50.0;
    public const double ThirstThreshold = 50.0;
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
        Simulation.Core.Cognition.DesireKind.SeekFood or Simulation.Core.Cognition.DesireKind.Eat => Hunger,
        Simulation.Core.Cognition.DesireKind.SeekWater or Simulation.Core.Cognition.DesireKind.Drink => Thirst,
        Simulation.Core.Cognition.DesireKind.Rest => Fatigue,
        Simulation.Core.Cognition.DesireKind.Flee => (1.0 - Safety) * 100.0,
        Simulation.Core.Cognition.DesireKind.Socialize => Social * 100.0,
        Simulation.Core.Cognition.DesireKind.Explore => Curiosity * 100.0,
        _ => 0.0,
    };

    /// <summary>Indicateur « état critique » (énergie &lt; 10 ou faim &gt; 90).</summary>
    public bool IsCritical => Energy < CriticalEnergy || Hunger > CriticalHunger;

    /// <summary>État critique selon les seuils configurables (agents.interruption.*).</summary>
    public bool IsCriticalFor(InterruptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Energy < settings.CriticalEnergy || Hunger > settings.CriticalHunger;
    }

    /// <summary>Le besoin est au-dessus de son seuil de déclenchement (génère un désir).</summary>
    public bool IsTriggered(Simulation.Core.Cognition.DesireKind kind) => kind switch
    {
        Simulation.Core.Cognition.DesireKind.SeekFood or Simulation.Core.Cognition.DesireKind.Eat => Hunger >= HungerThreshold,
        Simulation.Core.Cognition.DesireKind.SeekWater or Simulation.Core.Cognition.DesireKind.Drink => Thirst >= ThirstThreshold,
        Simulation.Core.Cognition.DesireKind.Rest => Fatigue >= FatigueThreshold,
        Simulation.Core.Cognition.DesireKind.Flee => Safety <= SafetyThreshold,
        Simulation.Core.Cognition.DesireKind.Socialize => Social >= SocialThreshold,
        Simulation.Core.Cognition.DesireKind.Explore => Curiosity >= CuriosityThreshold,
        _ => false,
    };

    /// <summary>
    /// Déclenchement selon les seuils configurables (SYNE-042 :
    /// besoins déclenchés dès « ≥ 50 » ; Rest dès fatigue &gt; 70).
    /// </summary>
    public bool IsTriggered(Simulation.Core.Cognition.DesireKind kind, NeedsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return kind switch
        {
            Simulation.Core.Cognition.DesireKind.SeekFood or Simulation.Core.Cognition.DesireKind.Eat =>
                Hunger >= settings.HungerTriggerThreshold,
            Simulation.Core.Cognition.DesireKind.SeekWater or Simulation.Core.Cognition.DesireKind.Drink =>
                Thirst >= settings.ThirstTriggerThreshold,
            Simulation.Core.Cognition.DesireKind.Rest => Fatigue >= settings.FatigueTriggerThreshold,
            Simulation.Core.Cognition.DesireKind.Flee => Safety <= SafetyThreshold,
            Simulation.Core.Cognition.DesireKind.Socialize => Social >= SocialThreshold,
            Simulation.Core.Cognition.DesireKind.Explore => Curiosity >= CuriosityThreshold,
            _ => false,
        };
    }

    /// <summary>
    /// Dérive périodique des besoins (faim +0.5/tick, soif +0.7/tick, fatigue
    /// +0.3/tick). <b>Sûreté</b> : la dérive va vers l'<b>insécurité</b> —
    /// <c>Safety</c> <i>diminue</i> de <c>safetyDriftRate</c> par tick (ch. X : le
    /// besoin se dégrade passivement, il ne se rétablit que par l'action
    /// <c>Flee</c> ou par l'absence de menace). Le signe était inversé dans
    /// l'implémentation historique, ce qui rendait <c>Flee</c> inatteignable par
    /// dérive depuis une sûreté initiale de 1.
    /// </summary>
    public BodyNeeds Advance(NeedsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Hunger = Clamp100(Hunger + settings.HungerRate);
        Thirst = Clamp100(Thirst + settings.ThirstRate);
        Fatigue = Clamp100(Fatigue + settings.FatigueRate);
        Safety = Clamp01(Safety - settings.SafetyDriftRate);
        Social = Clamp01(Social + settings.SocialDriftRate);
        Curiosity = Clamp01(Curiosity + settings.CuriosityDriftRate);
        Energy = Clamp100(Energy);
        return this;
    }

    /// <summary>Rétablissement de la sûreté (exutoire de <c>Flee</c>, réalignement S-13/B-08).</summary>
    public void RecoverSafety(double amount) => Safety = Clamp01(Safety + amount);

    /// <summary>Réduction du besoin social — exutoire de <c>Socialize</c> : le besoin est désormais <b>fermé</b>.</summary>
    public void RecoverSocial(double amount) => Social = Clamp01(Social - amount);

    /// <summary>Réduction du besoin de curiosité — exutoire de <c>Explore</c>.</summary>
    public void RecoverCuriosity(double amount) => Curiosity = Clamp01(Curiosity - amount);

    public void ExertEnergy(double cost) => Energy = Clamp100(Energy - cost);

    public void RecoverEnergy(double gain) => Energy = Clamp100(Energy + gain);

    public void RecoverFatigue(double amount) => Fatigue = Clamp100(Fatigue - amount);

    public void RecoverHunger(double amount) => Hunger = Clamp100(Hunger - amount);

    public void RecoverThirst(double amount) => Thirst = Clamp100(Thirst - amount);

    public void SetCuriosityElapsed(double amount) => Curiosity = Clamp01(Curiosity + amount);

    /// <summary>
    /// Dégradation de la sécurité par une menace perçue (D7 : cible d'une attaque —
    /// le besoin Safety glissait déjà vers le bas dans le prototype, ce mutateur
    /// rend la cause explicite ; l'objectif Flee existe déjà en).
    /// </summary>
    public void ThreatenSafety(double amount) => Safety = Clamp01(Safety - amount);

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

    /// <summary>Restauration d'état complet (persistance bit-à-bit).</summary>
    internal void RestoreState(
        double hunger,
        double thirst,
        double fatigue,
        double safety,
        double social,
        double curiosity,
        double energy)
    {
        Hunger = Clamp100(hunger);
        Thirst = Clamp100(thirst);
        Fatigue = Clamp100(fatigue);
        Safety = Clamp01(safety);
        Social = Clamp01(social);
        Curiosity = Clamp01(curiosity);
        Energy = Clamp100(energy);
    }

    private static double Clamp100(double value) => Math.Clamp(value, 0.0, MaxResource);

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
