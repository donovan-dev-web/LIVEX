---
title: "Titre de l'article"
description: "Description courte (1-2 phrases), factuelle, SEO-friendly, < ~160 caractères."
date: YYYY-MM-DD
tags: ["tag1", "tag2", "tag3"]
draft: true
---

## Introduction

1-2 paragraphes pour présenter le sujet, le contexte et ce que l'article apporte. Reste factuel, sobre, sans superlatif.

## 1. Section principale

Développer le propos avec des titres numérotés clairs. Utiliser des listes (`-`) pour les points clés, des tableaux pour les données structurées.

| Élément | Détail | Remarque |
| :-- | :-- | :-- |
| Exemple | Valeur | Note datée si chiffre |

## 2. Détails techniques (si applicable)

Expliquer de manière précise, avec références aux fichiers du dépôt (`docs/...`, `API_CONTRACTS.md`, etc.) quand pertinent. Ne pas inventer de captures non existantes.

## 3. Preuves ou validation

Lister les preuves concrètes (tests, campagnes, checksums, oracles, etc.) avec sources datées. Préférer les faits vérifiables aux interprétations.

## Conclusion

Synthèse courte (1-3 phrases). Reformuler l'apport de l'article sans promesse commerciale.

## Notes

- **Source unique** : s'appuyer sur les docs du dépôt (monographie, changelog, VISION, etc.)
- **Ton** : sobre, factuel, sans superlatif. « Illustration conceptuelle » si visuel.
- **Dates** : utiliser `YYYY-MM-DD` (ISO 8601).
- **Tags** : 3-5 tags descriptifs (minuscules, sans accents problématiques).
- **Brouillon** : `draft: true` tant que non publié. Les brouillons sont exclus de l'index et du flux RSS.
- **Langue** : les articles du journal sont FR uniquement (niveau 3 selon I18N).
- **Pas de base/lang dans les liens internes** : utiliser les chemins relatifs bruts, `url()` s'en charge.
- **Règle d'or** : préférer « ce qui est mesuré/rejouable » à « ce qui est supposé ».
