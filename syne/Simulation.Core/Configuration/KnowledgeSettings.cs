// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (KnowledgeSettings) — recettes connues des agents.

namespace Simulation.Core.Configuration;

/// <summary>
/// Connaissance déclarée des agents (SYNE-222, source « simple ») : les recettes
/// que tout agent sait au départ, sous <c>agents.knowledge</c>.
/// </summary>
/// <remarks>
/// Désactivé par défaut : la trajectoire du scénario de référence reste
/// <b>bit-à-bit identique</b> tant qu'aucune recette n'est déclarée (même
/// doctrine que <c>agents.inventory.enabled</c> ou <c>agents.plans.enabled</c>).
/// Le savoir des recettes appris par les livres relève de SYNE-224.
/// </remarks>
public sealed class KnowledgeSettings
{
    /// <summary>Active l'injection des recettes déclarées dans les croyances durables.</summary>
    public bool Enabled { get; set; }

    /// <summary>Recettes connues au départ (identifiant unique).</summary>
    public List<RecipeKnowledgeSettings> Recipes { get; set; } = [];

    /// <summary>
    /// Un livre lu transmet les recettes qu'il déclare (SYNE-224) : les lignes
    /// <c>recipe:…</c> de son contenu deviennent un savoir durable, de source
    /// <c>book:&lt;id&gt;</c>. Sans effet tant que <c>world.books.enabled</c> est faux.
    /// </summary>
    public bool LearnFromBooks { get; set; } = true;
}

/// <summary>
/// Recette déclarée : identifiant, station éventuelle, entrées et sorties.
/// </summary>
/// <remarks>
/// L'identifiant est celui du contenu PRISM quand il existe : la connaissance
/// doit désigner la même recette que l'hôte, sinon l'agent croirait fabriquer
/// une chose et en demanderait une autre.
/// </remarks>
public sealed class RecipeKnowledgeSettings
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Station requise (établi, four…) ; <c>null</c> pour une recette à mains nues.</summary>
    public string? Station { get; set; }

    public List<RecipeIngredientSettings> Inputs { get; set; } = [];

    public List<RecipeIngredientSettings> Outputs { get; set; } = [];
}

/// <summary>Ligne d'entrée ou de sortie d'une recette : un objet et une quantité positive.</summary>
public sealed class RecipeIngredientSettings
{
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
}
