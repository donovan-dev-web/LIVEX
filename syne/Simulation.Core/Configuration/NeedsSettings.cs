// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (NeedsSettings).

namespace Simulation.Core.Configuration;

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
