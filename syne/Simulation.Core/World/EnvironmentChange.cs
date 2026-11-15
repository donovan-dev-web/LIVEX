// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

/// <summary>Sens d'une modification d'environnement (SYNE-071).</summary>
public enum EnvironmentChangeKind
{
    Added,
    Removed,
}

/// <summary>
/// Modification d'environnement tracée (SYNE-071). La géométrie a quitté SYNE (étape 3c) :
/// le dernier producteur de ces changements est PRISM, et le flux reste vide côté moteur.
/// </summary>
public sealed record EnvironmentChange(EnvironmentChangeKind Kind, Obstacle Obstacle);
