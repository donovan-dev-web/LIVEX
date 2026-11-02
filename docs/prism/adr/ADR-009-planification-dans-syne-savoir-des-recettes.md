# ADR-009 : Planification dans SYNE (GOAP/HTN) et savoir des recettes

**Composant** : PRISM (impact SYNE)
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004, ADR-008, `../../syne/COGNITIVE_ARCHITECTURE.md`, `../../syne/adr/ADR-018-realignement-cognition-active.md`
**Source Monographie** : —

---

## Contexte

Une chaîne de production comporte des étapes ordonnées (récolter, tailler, assembler, miner, fondre…). Écrire à la main une bibliothèque de plans par chaîne devient ingérable dès que les recettes se multiplient.

## Décision

1. Le **BDI reste le moteur de décision** : il choisit désirs et intentions. Un **planificateur intégré à la délibération** (GOAP ou HTN ; choix par spike en phase P9) construit la séquence de macro-actions par chaînage arrière sur les **recettes connues de l'agent**.
2. Le plan est **mis en cache** et recalculé **sur événement d'échec** (ressource épuisée, outil cassé, chantier détruit), jamais à chaque tick. L'agent ne délibère que sur événement (fin d'action, échec, perception notable).
3. **Le planificateur vit dans SYNE**, pas dans PRISM : PRISM ne planifie pas pour les agents (information locale, autorité cognitive).
4. **Savoir des recettes, deux variantes** : *simple* (toutes connues, référence de calibrage, **par défaut**) puis *riche* (découverte par essai, observation ou enseignement, activée comme condition d'expérience).
5. **Un LLM dans la boucle est hors périmètre.**

## Conséquences

### Positives
- Les chaînes nouvelles par données n'exigent aucun nouveau plan écrit.
- La variante riche relie transmission du savoir et formation de groupes.

### Négatives
- Un planificateur dans la boucle de délibération pèse sur le budget de SYNE (suivi : PERFORMANCE.md §3).
- La traçabilité `Action ← Intention ← Objectif ← Besoin…` doit être étendue au plan.

### Risques
- Explosion combinatoire pour des recettes à entrées multiples. Mitigation : profondeur bornée, cache, heuristiques par tags.

## Alternatives considérées

- **Plans écrits à la main** : refusé (maintenance).
- **Planification dans PRISM** : refusé (viole l'information locale et l'autorité cognitive).
- **Apprentissage par renforcement** : hors périmètre.

## Validation / rejet

Validé au jalon G5 (agents SYNE accomplissant au moins trois chaînes sans plan scripté). Réouvert si le budget de délibération n'est pas tenable à 1 000 agents.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
