# KANBAN.md — Governance

**Composant** : LIVEX (général) 
**Statut** : [STABLE] 
**Dernière mise à jour** : 21 octobre 2026 

---

## 1. Objectif

Ce document définit le tableau Kanban unifié pour le projet LIVEX, aligné sur la roadmap et les issues des composants SYNE, ECHOS, PRISM et Launcher. Il sert de base unique pour :
- La visualisation de l'avancement du travail.
- Le suivi des issues par milestone et par statut.
- La gestion du flux de travail de la création à la fermeture des issues.
- L'alignement sur les décisions ADR et les jalons de la roadmap.

---

## 2. Colonnes du tableau

| Colonne | Signification | Action typique |
| :-- | :-- | :-- |
| **Backlog** | Issues prêtes à être planifiées | Création d'issue, assignation de milestone, étiquetage |
| **Todo** | Issues planifiées pour le prochain sprint | Déplacement depuis Backlog, priorisation |
| **In Progress** | Travail en cours | Début de la PR, développement |
| **In Review** | Revue en cours | Pull request ouverte, revue en cours |
| **Done** | Terminé et validé | PR fusionnée, issue fermée |

---

## 3. Règles de suivi

- Toute carte `SYNE-*` ou `ECHOS-*` doit avoir **exactement 1 label de milestone** (`milestone/ph*`, `milestone/v0.1`, `milestone/v1`) et **≥ 1 label de type** (`type/*`).
- Chaque carte porte la colonne Kanban correspondante à son état (Backlog → Todo → In Progress → In Review → Done, selon `KANBAN.md` §2).
- Une carte ne passe en **In Review** qu'avec une PR référençant l'issue (lien `Closes #ISSUE-ID`).
- Une carte **status/blocked** doit référencer le blocage (ADR, décision `[OUVERTE]`, donnée manquante).
- **Conformité** : toute carte doit tracer sa **décision** d'origine quand elle existe — une valeur non tranchée reste calibrée plus tard, jamais figée par accident (Monographie §9.6.4).

---

## 4. Alignement sur la roadmap

- Les milestones du tableau (`milestone/ph0` … `milestone/ph11`, `milestone/v0.1`, `milestone/v1`) doivent correspondre aux jalons du `ROADMAP.md` général.
- Chaque jalon atteint (`ROADMAP.md` §4 fin de phase) doit entraîner la mise à jour de ce document et le déplacement des issues correspondantes vers la colonne Done.

---

## 5. Maintenance de ce document

- Document actualisé lors de chaque décision tranchée et de chaque jalon atteint (`ROADMAP.md` §4 fin de phase).
- Les IDs et conventions sont **stables** : aucune réindexation rétroactive ; un ID consommé est conservé (trace), jamais réutilisé.