// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (InstrumentalSettings) — désirs instrumentaux et sécurité alimentaire (SYNE-222).

namespace Simulation.Core.Configuration;

/// <summary>
/// Désirs instrumentaux et sécurité alimentaire (SYNE-222, fin) : au lieu de
/// vivre au jour le jour (désir de nourriture déclenché seulement quand la faim
/// a franchi son seuil), l'agent <b>anticipe</b> et vise un <b>moyen</b> — un
/// stock de nourriture en inventaire — avant d'avoir faim.
/// </summary>
/// <remarks>
/// <para>
/// Désactivé par défaut : la trajectoire du scénario de référence reste
/// <b>bit-à-bit identique</b> (même doctrine que <c>agents.planner</c>,
/// <c>agents.knowledge</c> ou <c>agents.actions.inventory</c>).
/// </para>
/// <para>
/// Le moyen n'invente aucun vocabulaire d'action : l'agent <b>va</b> vers une
/// source de nourriture connue (poursuite <c>SecureFood</c>) puis <b>prend</b>
/// dans son inventaire (<c>Take</c>, primitive D8). Sans inventaire actif, la
/// section est refusée par le validateur — un stock sans contenant n'a pas de sens.
/// </para>
/// </remarks>
public sealed class InstrumentalSettings
{
    /// <summary>Active les désirs instrumentaux (faux par défaut : pin de référence).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Horizon d'anticipation en ticks : l'agent se met en quête d'un stock quand
    /// la faim <b>projetée</b> à cet horizon atteindra son seuil de déclenchement.
    /// Classe A (période) : divisé par <c>dt</c> par l'échelle temporelle.
    /// </summary>
    public int AnticipateTicks { get; set; } = 240;

    /// <summary>
    /// Réserve de nourriture (unités en inventaire) en deçà de laquelle l'agent ne
    /// se sent pas en sécurité : c'est le niveau de stock visé par le désir.
    /// </summary>
    public int ReserveTarget { get; set; } = 3;
}
