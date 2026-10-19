// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (BookSettings).

namespace Simulation.Core.Configuration;

public sealed class BookSettings
{
    /// <summary>Activité livres (écriture/consultation, événements <c>world.book_*</c>, snapshot <c>books[]</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Coût d'écriture en énergie payé par l'auteur.</summary>
    public double WriteCostEnergy { get; set; } = 20.0;


    /// <summary>Bénéfice de lecture posé en principe — effet cognitif reporté (mine mémoire).</summary>
    public double ReadBenefit { get; set; } = 1.0;
}
