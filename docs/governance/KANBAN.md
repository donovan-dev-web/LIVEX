# KANBAN.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ISSUES.md`, `GITFLOW.md`
**Source Monographie** : Annexe J (feuille de route V2) — pour l'inspiration du flux des jalons

---

## 1. Objectif

Ce document définit le tableau de bord (Kanban) utilisé pour suivre les tâches de LIVEX, en liant issues, branches et jalons issus de la Monographie (ex. Annexe J pour la route V2).

## 2. Colonnes du tableau

| Colonne | Définition | Critère d'entrée | Critère de sortie |
| :-- | :-- | :-- | :-- |
| **Backlog** | Idées non planifiées | — | triée + priorisée |
| **Todo** | Tâches planifiées dans un jalon | labels/priorité posés | assignée + branch `feature/*` créée |
| **In Progress** | Tâche en cours | commit sur la branch | PR ouverte |
| **In Review** | PR en revue | PR ouverte | PR mergée |
| **Done** | Tâche terminée | fusion + pipeline vert + vérif. critère d'acceptation | — |

## 3. Work-in-Progress (WIP) limit

En mode solo, limite indicative :

| Colonne | Limite WIP |
| :-- | :-- |
| In Progress | 3 |
| In Review | 2 |

Ces limites évitent d'entamer des tâches dont le critère d'acceptation (souvent dépendant des jalons de la Monographie) n'est pas mature. Elles sont réévaluées avec l'arrivée de contributeurs.

## 4. Lien issues ↔ Kanban

- Chaque carte est une **issue** (cf. `ISSUES.md`). Aucune carte sans issue derrière.
- Passages de colonnes tracés par les transitions de labels/statut (`status/blocked`, `status/needs-input`, etc.) plutôt que par des colonnes supplémentaires.

## 5. Priorisation

- La priorité suit la **roadmap** (`ROADMAP.md` racine et par composant), elle-même alignée sur les jalons de la Monographie (Annexe J).
- Ordre de tri :
  1. Jalons critiques (contrats, déterminisme, persistance).
  2. Blocants signalés `status/blocked`.
  3. Priorité `P0` → `P1` → ...
  4. Triant secondaire : plus ancienne date de création.

## 6. Revue de flux

- Revue du tableau à chaque jalon atteint dans le projet (fin d'une phase de `ROADMAP.md`).
- Objet de la revue : détecter les cartes `status/blocked`, les colonnes au-dessus des limites WIP, et les `[OUVERT]` des documents qui bloquent l'avancement.

---

## Points restés ouverts dans ce document
- Aucun — document stabilisé ; les limites WIP et la fréquence de revue seront affinées avec l'historique réel du projet.