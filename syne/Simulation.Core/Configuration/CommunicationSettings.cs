// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (CommunicationSettings).

namespace Simulation.Core.Configuration;

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
