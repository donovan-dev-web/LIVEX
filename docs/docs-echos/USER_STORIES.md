# USER_STORIES.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `FRONTEND_VISION.md`, `API_REST.md`
**Source Monographie** : §4.2.2, §4.6–4.7, §5.13

---

## 1. Personas

| Persona | Rôle | Besoin central | Priorité V0.1 |
| :-- | :-- | :-- | :-- |
| **Chercheur·se** (Claire) | Sélectionne, lance et juge les runs | Observer sans biaiser, comparer proprement | P0 |
| **Expérimentateur·trice** (Marc) | Répète, varie les paramètres, valide la reproductibilité | Rejouer bit-à-bit, exporter | P0 |
| **Data-analyste** (Léa) | Plonge dans les séries et les graphes causaux | Interroger les données brutes | P1 |
| **Opérateur·trice** (Sam) | Surveille un run long en tête-à-tête | Voir l'état sans déranger le moteur | P1 |
| **Vérificateur·trice / reviewer** (Alex) | Contrôle l'absence de biais | Accéder aux limites et aux logs | P2 |

> Les personas *chercheur* et *data-analyste* se distinguent **volontairement** : le premier agit sur l'interface de pilotage, la seconde exploite l'interface d'analyse. Cette séparation est la traduction directe de `FRONTEND_VISION.md` §2.

## 2. User Stories

### Épique 1 — Observer (P0)

| ID | En tant que | Je veux | Afin de |
| :-- | :-- | :-- | :-- |
| **US-1** | chercheur·se | une **vue d'ensemble** avec 4 KPI (entités actifs, score émergence, groupes, messages/tick) | saisir l'état du monde en 5 secondes |
| **US-2** | chercheur·se | des **jauges** diversité des croyances / objectifs, clustering, vitesse de diffusion | suivre les moteurs temporels |
| **US-3** | chercheur·se | une **timeline** des métriques | voir *quand* les choses se passent |
| **US-4** | opérateur·trice | une **matrice de communication** entité×entité | identifier des paires très actives |
| **US-5** | chercheur·se | un **graphe social** D3 en 3D/2D | visualiser les coalitions émergentes |
| **US-6** | chercheur·se | l'**inspection d'une entité** (sondage 500 ms) | suivre croyances/besoins d'un agent précis |
| **US-7** | chercheur·se | un **explorateur de groupes** (liste + détail + formation/dissolution) | juger la stabilité des groupes |

*Critère d'acceptation transverse (US-1..7)* : les vues sont alimentées **sans interaction du moteur SYNE**, via WebSocket :5180 (`snapshot`/`event`) — GIVEN un run en cours, WHEN je consulte une vue, THEN les données reflètent le dernier snapshot sans polling HTTP intermédiaire.

### Épique 2 — Piloter (P0)

| ID | En tant que | Je veux | Afin de |
| :-- | :-- | :-- | :-- |
| **US-8** | chercheur·se | **play / pause / step** | contrôler le déroulement d'un run |
| **US-9** | chercheur·se | régler la **vitesse** | adapter le rythme d'observation |
| **US-10** | chercheur·se | **redémarrer avec un seed donné** | rejouer une trajectoire bit-à-bit |
| **US-11** | chercheur·se | **calibrer** (calibrage) via l'interface de contrôle | ajuster les paramètres sans coder |
| **US-12** | chercheur·se | **sauvegarder/charger** l'état d'un run | reprendre une expérience |

*Critère d'acceptation (US-8..12)* : les contrôles passent **par ECHOS → SYNE (HTTP :5181)** uniquement. GIVEN un contrôle cliqué, WHEN ECHOS relaie la commande à SYNE, THEN le moteur répond et la vue reflète le nouvel état — l'interface **ne pilote pas SYNE en direct**, elle relaie (contrat `../docs-syne/API_CONTRACTS.md`).

### Épique 3 — Analyser (P1)

| ID | En tant que | Je veux | Afin de |
| :-- | :-- | :-- | :-- |
| **US-13** | data-analyste | **exporter** les métriques d'un run (CSV/JSON) | analyser hors-ligne |
| **US-14** | data-analyste | **comparer deux runs** (`?a={run1}&b={run2}`) | mesurer l'effet d'un paramètre |
| **US-15** | data-analyste | accéder aux **croyances** d'une entité (`/api/beliefs/{agentId}`) | analyser la cognition |
| **US-16** | data-analyste | accéder aux **réseaux de confiance** (`/api/relationships/{agentId}`) | analyser les liens sociaux |
| **US-17** | data-analyste | voir les **phénomènes émergents détectés** (`/api/emergent-phenomena`) | confronter les scores |
| **US-18** | chercheur·se | accéder aux **traces de décision** (DecisionRecord) | reconstruire une causalité |

*Critère d'acceptation (US-13..18)* : les données viennent **de l'API REST ECHOS (port 5000)**, jamais d'un calcul côté interface. GIVEN une requête REST, WHEN ECHOS répond, THEN la charge est une **mesure déjà produite** par les 7 moteurs (cf. `METRICS_SPEC.md`), non recomputée dans le navigateur.

### Épique 4 — Instrumenter (P2)

| ID | En tant que | Je veux | Afin de |
| :-- | :-- | :-- | :-- |
| **US-19** | vérificateur·trice | baseline **structurée** sur les événements | vérifier l'absence de biais |
| **US-20** | vérificateur·trice | le **profilage** des calculs | confirmer les budgets (≥30/20/10 t/s) |
| **US-21** | vérificateur·trice | la **console de débogage** (3 niveaux) | tracer un défaut sans bruit |
| **US-22** | opérateur·trice | les **limites connues** affichées | prévenir une sur-interprétation |

*Critère d'acceptation (US-19..22)* : les outils d'instrumentation sont **séparés** de l'analyse (`LOGGING_INSTRUMENTATION.md`). GIVEN l'affichage d'un score, WHEN un observateur le voit, THEN les limites et la règle « un score n'est pas une preuve » restent accessibles à un clic.

---

## 3. Points restés ouverts dans ce document
- Les personas sont des archétypes V0.1 ; leur validation UX viendra des tests utilisateurs en implémentation (phase 8).
- L'ordre de priorité P0/P1/P2 est aligné sur le `ROADMAP.md` ECHOS (jalons J1–J5) ; il peut évoluer avec la phase 6.
