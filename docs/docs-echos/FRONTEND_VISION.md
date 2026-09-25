---
title: FRONTEND_VISION — vision du frontend ECHOS
---

# FRONTEND_VISION.md

**Composant** : ECHOS
**Statut** : [LIVRÉ]

> **Jalon ph8 (U7)** : la vision est désormais **implémentée** par `echos-ui`
> (PR UI #441, les 6 Écrans A→F). Les dispositions figurant dans ce document
> sont appliquées telles quelles (contrats §3.9, décision locale, relais de
> pilotage, design system §4/§5).
>
> **Résumé du livrable** : étape cible « pas de coquille Electron » reformulée
> en « **implémentation différée post-V0.1** » (décision 23/09/2026) — voir
> `ADR-001-stack-applicative.md` log correctif et `LIVEX/CHANGELOG.md`.
**Dépend de** : `VISION.md`, `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.2.2, §4.7, §5.13 (interface d'analyse)

---

## 1. Positionnement

L'interface d'ECHOS est la **face visible de LIVEX** : c'est elle que l'utilisateur regarde et manie. Elle rassemble en V0.1 l'observation, le pilotage, la calibration et les rapports — héritage direct de l'interface web du prototype (React + TypeScript, §5.13 de la Monographie).

Elle n'est **ni** un moteur, **ni** un laboratoire : elle affiche ce que les moteurs décident et expose ce que les moteurs produisent.

## 2. La distinction fondamentale : interface/pilotage VS analyse/data-science

C'est la règle structurante de tout le front-end ECHOS. Deux mondes cohabitent dans ECHOS, et ils ne doivent **jamais** être confondus :

| | **Partie interface & pilotage** | **Partie analyse & data-science** |
| :-- | :-- | :-- |
| **But** | Voir, comprendre et **piloter** la simulation | Mesurer, comparer et **prouver** des phénomènes |
| **Nature** | Vues, contrôles, rapports humains | Moteurs de métriques, causalité, expériences |
| **Production** | Tableaux de bord, HUD, formulaires de calibration | 7 moteurs, scores composites, graphes causaux |
| **Interaction** | L'utilisateur **agit** (play/pause/step, calibration) | L'utilisateur **lit** des résultats déjà calculés |
| **Vérité** | Reflète l'état courant (observable) | Reflète les mesures stabilisées (analysées) |
| **Temps réel** | Oui — WebSocket, vues vivantes | Différé — calculs agrégés, snapshots |
| **Qui écrit** | L'interface **commande** SYNE (contrôle :5181) | ECHOS **observe** SYNE (WebSocket :5180) |
| **Document** | `UI_DESIGN.md`, `USER_STORIES.md` | `METRICS_SPEC.md`, `CAUSAL_ANALYSIS.md`, `EXPERIMENT_COMPARISON.md` |

Les trois règles non négociables :

1. **L'interface ne calcule jamais de métrique scientifique.** Elle consomme les métriques déjà produites par les moteurs ECHOS (§4.3 Monographie). La seule exception : des calculs de mise en forme (moyennes simples, taux) destinés aux affichages, jamais aux exports d'analyse.
2. **Le pilotage passe par ECHOS qui relaie vers SYNE (HTTP :5181).** L’API ECHOS expose `/api/control/*`, puis relaie via `ControlClient` ; l’interface ne contacte jamais directement SYNE. Le serveur `--serve` doit être actif. En V0.1, `--serve` et `--observe` lancent des instances distinctes.
3. **Un écart visuel n'est pas une preuve.** Quand l'interface montre une tendance, c'est une **invitation à l'analyse**, jamais un résultat scientifique. Le score d'émergence se lit dans la vue d'analyse, pas dans le HUD.

> **Règle d'or** : l'interface **montre**, l'analyse **démontre**. Si une vue du front-end prétend « prouver » une émergence, c'est une erreur de conception (cf. `VISION.md` ECHOS).

## 3. Ce que l'interface expose (V0.1)

| Vue | Contenu | Alimentation | Interdit |
| :-- | :-- | :-- | :-- |
| **KPICards** | 4 cartes : entités actives, score émergence, groupes, messages/tick | Métriques temps réel | Interpréter les scores |
| **MetricsPanel** | Jauges : diversité des croyances/objectifs, coefficient de clustering, vitesse de diffusion | Métriques | Transférer les jauges en conclusions |
| **TimelineChart** | Évolution temporelle des métriques | Séries (cache, sous-échantillonnage) | Rapprocher des séries de causes |
| **AgentInspector** | Sondage d'une entité (500 ms) : croyances, besoins, relations | Snapshot SYNE | Modifier l'entité (lecture seule) |
| **SocialGraph** | Graphe social D3 (sondage 2 s) — force-directed | Différentiel de confiance | Inférer des liens « réels » |
| **GroupExplorer** | Groupes : liste, membres, formation/dissolution | Détection de groupes | Qualifier de « société » |
| **MessageHeatmap** | Matrice entité×entité des communications | Flux d'événements | Issuer des causalités |
| **SimulationControls** | Play / Pause / Step / vitesse / seed / reset | Pilote → SYNE :5181 | Accès hors contrat |
| **RecordingPanel** | Enregistrement des runs, IDs, export de comparaison | Stockage ECHOS | Écrire directement dans SYNE |
| **Console de débogage** | 3 niveaux : structuré / traces / texte (Serilog) | `LOGGING_INSTRUMENTATION.md` | Utiliser les logs comme métriques |

## 4. Les limites assumées de l'interface

- **Anti-triche d'affichage** : l'interface ne donne pas à l'utilisateur plus d'informations que les entités (pas de mode « Dieu » omniscient). Voir `../docs-syne/VISION.md`.
- **Pas de minimap** en V0.1 : la navigation à grande échelle repose sur le zoom (héritage §5.14).
- **Consommation honnête** : les vues live sont bornées (sondage 500 ms / 2 s, sous-échantillonnage `?every=N`) pour ne pas dégrader SYNE.
- **Divergence documentée** : l'interface web du prototype devient **intégrée à ECHOS** en V0.1 (**web local React/Vite servie par FastAPI**) ; le shell Electron est **conservé** — son implémentation est **différée à un horizon ultérieur (post-V0.1)** (correction décision 23/09/2026, en `ARCHITECTURE.md` / `adr/ADR-001-stack-applicative.md`).

---

## Points restés ouverts dans ce document
- Aucun — la distinction interface/analyse est une contrainte ferme de conception.
