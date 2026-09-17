# Vue d'ensemble des phases V2

## Objectif général

Transformer le moteur de simulation V1 (comportements émergents basiques) en une plateforme de simulation **persistante de jeu vidéo 3D**, capable de supporter **500–1000 agents autonomes** avec cognition BDI complète (perceptions, mémoire, croyances, objectifs, décisions intentionnelles).

---

## Structure : 12 phases + sous-phases par composant

Les phases V2 suivent le même pattern que V1 (conception → implémentation modulaire → tests → déploiement), mais chaque grande phase est **divisée en sous-phases** par composant (Engine, Analyzer, Web UI, Godot Renderer).

```text
Phase N : Sujet principal
├── N.1 : Moteur de simulation (Simulation.Core + Simulation.Console)
├── N.2 : Analyzer (.NET, métriques)
├── N.3 : Web UI (React + TypeScript)
└── N.4 : Renderer 3D (Godot)
```

---

## Phases détaillées

### **Phase 0 : Conception et architecture (Étape préalable)**

Établir les principes, ADRs, et spécifications détaillées.

- **0.1** (Engine) : Architecture BDI, diagrammes système, modèle de données
- **0.2** (Analyzer) : Nouvelles métriques d'émergence, stratégie de persistence
- **0.3** (Web UI) : Wireframes, composants pour visualiser beliefs/goals/relations
- **0.4** (Godot) : Adaptation du renderer pour le monde persistant

**Livrables** : ADRs, spécifications techniques, diagrammes UML

---

### **Phase 1 : Architecture cognitive BDI et perception**

Construire le fondement : un agent capable de **percevoir, mémoriser et croire**.

#### 1.1 — Moteur de simulation

- **Perception v2** : rayon local, types d'entités (agents, ressources, obstacles)
- **Internal State** : amélioration vs V1 (ajouter traits persistants, historique récent)
- **Beliefs** : représentation structurée de ce que l'agent croit connaître
  - Fait connu (type, position, état)
  - Timestamp de dernière observation
  - Confiance/certitude (0–1)
- **Architecture modulaire** : Perception System, Memory System, Belief System
- **BDD SQLite** : schéma pour agents, beliefs, observations

**Livrables** : 
- Classes `Perception`, `Belief`, `BeliefStore`
- Système de perception déterministe et testable
- Schéma SQLite v1 (agents, beliefs, observations)

#### 1.2 — Analyzer

- Parser les beliefs depuis le flux de simulation
- Métriques basiques : nombre de beliefs par agent, taux de certitude moyen
- Export JSON pour validation

#### 1.3 — Web UI

- Afficher beliefs d'un agent sélectionné (liste + timeline)
- Couleur certitude
- Inspector amélioré

#### 1.4 — Godot

- Support étendu : affichage beliefs agent (debug mode)
- Pas de changement majeur au rendering

---

### **Phase 2 : Mémoire, croyances avancées et observations**

Enrichir la mémoire pour supporter **mémorisation d'événements, divergence informationnelle, oubli**.

#### 2.1 — Moteur de simulation

- **Memory System v2** :
  - Observations datées (type, position, propriétés)
  - Décay automatique (oubli progressif ou seuil d'âge)
  - Catégories : facts, events, interactions
- **Belief update** : révision des croyances vs observations réelles
- **Partial observability** : agent ne connaît que ce qu'il perçoit (rupture avec V1)
- **BDD** : tables `agent_observations`, `agent_memories`, `memory_decay_config`

**Livrables** :
- Classes `Observation`, `MemoryEntry`, `MemorySystem`
- Règles de décay configurable
- Schéma SQLite extended

#### 2.2 — Analyzer

- Détection d'inconsistances (monde réel vs beliefs agent)
- Métriques : diversité informationnelle, taux d'oubli
- Historique beliefs vs réalité (validation de dérive)

#### 2.3 — Web UI

- Timeline beliefs + observations (graphe temporel)
- Comparaison agent belief vs world truth
- Debug view : "Ce que A croit" vs "Ce qui est réel"

#### 2.4 — Godot

- HUD amélioré : beliefs agent, observations récentes

---

### **Phase 3 : Système de décision et utilité avancée**

Remplacer Utility AI V1 par **délibération BDI complète avec fonctions d'utilité multidimensionnelles**.

#### 3.1 — Moteur de simulation

- **Needs System v2** :
  - Besoins classiques (faim, soif, fatigue, sécurité)
  - Nouveaux : besoin social, curiosité
- **Goals Generation** :
  - À partir des besoins non satisfaits
  - Filtrage par faisabilité (skills, ressources proches)
  - Priorité dynamique
- **Utility Function v2** :
  ```
  utility = (need_satisfaction - costs) * confidence_factor + risk_modifier
  ```
  - Factorise besoins, distance, danger, confiance en beliefs, risque
  - Pondérations par traits d'agent (aggression, socialité, prudence, ambtition)
- **Decision Record** : trace complète (goals, scores, intention choisie)
- **DecisionSystem** : nouveau module orchestrant perception→belief→goal→utility→intention

**Livrables** :
- Classes `Need`, `Goal`, `UtilityEvaluator`, `DecisionRecord`
- Fonctions utilitaires complètes + tests
- Configuration de pondérations par trait

#### 3.2 — Analyzer

- Métriques de décision : convergence/divergence des choix, stabilité intentions
- Analyse de coûts de décision par type
- Comparaison utilité estimée vs réelle

#### 3.3 — Web UI

- Panneaux : besoins, goals générés, scores utilité, intention sélectionnée
- Heatmap traits × besoins

#### 3.4 — Godot

- Affichage intention courante + goals en attente

---

### **Phase 4 : Actions, intentions et exécution**

Implémenter **Actions modulaires guidées par intentions BDI**.

#### 4.1 — Moteur de simulation

- **Actions v2** (reprendre de V1 mais par intention) :
  - MoveTo, Eat, Drink, Rest, Explore, Gather
  - Ajouter : CommunicateTo, Wait, Think, Observe, Trade
  - Actions composées (séquences simples, pas de behaviour tree complexe)
- **Intention** : binding entre goal et action
- **Action execution** :
  - État : Pending → Executing → Completed/Failed/Cancelled
  - Interruption intelligente : si goal plus valide ou intention nouvelle
- **ActionSystem v2** : orchestration intention→action, gestion du cycle
- **Event emission** : ActionStarted, ActionProgressed, ActionCompleted avec traces

**Livrables** :
- Classes `Action`, `Intention`, `ActionExecutor`
- 12+ actions implémentées, testables
- Gestion interruption robuste

#### 4.2 — Analyzer

- Événements action détaillés : succès/échec/interruption
- Métriques : taux de réussite par action, durée moyenne, interruptions par agent

#### 4.3 — Web UI

- Action timeline : historique actions + résultats
- Progression action courante

#### 4.4 — Godot

- Animations actions (V1 continuait), synchronisation avec état

---

### **Phase 5 : Communication inter-agents et protocole**

Implémenter **communication locale structurée** avec portée, fiabilité, coûts.

#### 5.1 — Moteur de simulation

- **Communication Protocol** : spécification formelle
  - Types de messages : Information, Request, Response, Announcement
  - Payload structuré (sender, timestamp, content, confidence)
  - Portée : rayon local (configurable)
  - Coût : énergie/time dépendant du message
- **Message reception** :
  - Agents reçoivent messages pertinents (rayon + type)
  - Intégration en beliefs (message source crédible ?)
  - Mémorisation avec source et timestamp
- **Communication Channel** : service centralisé ou local per-agent
- **BDD** : tables `messages`, `communication_log`, `agent_trust_by_communicant`

**Livrables** :
- Spécification protocole (doc + schémas)
- Classes `Message`, `CommunicationSystem`, `MessageQueue`
- Implémentation portée + coûts

#### 5.2 — Analyzer

- Graphe de communication : qui parle à qui, fréquence
- Efficacité communication : messages → changements de décision
- Propagation d'information (BFS dans graphe social)

#### 5.3 — Web UI

- Réseau social : nodes agents, edges communication
- Message log filtered par agent
- Heatmap information diffusion

#### 5.4 — Godot

- HUD : messages agent, interlocuteurs

---

### **Phase 6 : Groupes et coalitions explicites**

Permettre aux agents de **s'inscrire volontairement à des groupes** avec objectifs/rôles partagés.

#### 6.1 — Moteur de simulation

- **Group** : entité contenant agents + objectif commun
  - Création explicite (agent A crée, B rejoint)
  - Membres, rôles (leader, scout, gatherer)
  - Ressources partagées (inventaire groupe)
  - Dissolution si objectif atteint ou + assez de membres
- **GroupDecision** : délibération groupe sur actions (vote, leader)
- **Coalition** : groupe temporaire pour une tâche (ad-hoc)
- **BDD** : tables `groups`, `group_memberships`, `group_objectives`, `coalitions`

**Livrables** :
- Classes `Group`, `Coalition`, `GroupMembership`
- Système inscription/désinscription
- Partage de ressources groupe

#### 6.2 — Analyzer

- Métriques groupe : taille, durée, efficacité (objectif atteint ?)
- Turnover membership
- Comparaison groupe vs agent solo (performance)

#### 6.3 — Web UI

- Visualisation groupes (clusters de nodes)
- Historique formation/dissolution

#### 6.4 — Godot

- Coloration agents par groupe

---

### **Phase 7 : Dynamique systémique (ressources, production, rétroactions)**

Enrichir **monde dynamique** avec cycles ressources et conséquences à long terme.

#### 7.1 — Moteur de simulation

- **Resources v2** :
  - Stock (nourriture, eau, bois, minéraux)
  - Production/Régénération (rate configurable par ressource)
  - Dégradation/Usure (ressources se dégradent si non utilisées)
  - Localisation (gisements, points de concentration)
- **Consumption** : agents consomment ressources (avec traçabilité)
- **Events d'environnement** :
  - Catastrophes (sécheresse, épidémie, tremblement)
  - Cycles (saisons, jour/nuit)
  - Déclencheurs de rétroactions
- **Feedback loops** :
  - Surexploitation → ressource épuisée → migration agents → colonisation nouvelle zone
  - Événement catastrophe → besoin sécurité ↑ → groupes formation → restructuration
- **BDD** : tables `resources`, `resource_deposits`, `consumption_log`, `environment_events`

**Livrables** :
- Système production/régénération configurable
- Classes `ResourceDeposit`, `EnvironmentEvent`
- Simulation cycles (saisons, etc.)

#### 7.2 — Analyzer

- Métriques ressources : consommation vs régénération, cycles
- Stabilité de l'écosystème (ressources durables ?)
- Événements catastrophe impact

#### 7.3 — Web UI

- Graphes consommation vs régénération (séries temporelles)
- Heatmap ressources au temps T
- Journal environnemental

#### 7.4 — Godot

- Visualisation ressources (texture, quantité)
- Événements catastrophe (effets visuels)

---

### **Phase 8 : Monde partiellement observable (implications et intégration)**

**Intégration complète** de l'observabilité partielle dans tous les systèmes.

#### 8.1 — Moteur de simulation

- **Partial observability** :
  - Agents ne voient que rayon perception (rupture totale avec V1)
  - Monde réel existe (complet)
  - Agents reconstruisent via perception + mémoire + croyances
- **Implication** : décisions fondées sur **informations incomplètes**
- **Erreur possible** : agent croit quelque chose faux → décisions "mauvaises"
- **Test** : valider que 1000 agents V2 < 500 agents V1 (perf)

**Livrables** :
- Test intégration observabilité partielle
- Benchmark (50 agents ~1000 ticks)

#### 8.2 — Analyzer

- Métriques divergence : écarts belief vs réalité, impact sur décisions
- Efficacité simulation : émergence riche ?

#### 8.3 — Web UI

- Debug toggle : afficher world truth vs agent beliefs

#### 8.4 — Godot

- Camera option : simuler vision agnet (brouillard de guerre)

---

### **Phase 9 : Optimisation et scaling (50 → 500 → 1000 agents)**

**Passer à l'échelle sans casser la physique du jeu.**

#### 9.1 — Moteur de simulation

- **Profiling** : identifier goulots (perception, decision, communication)
- **LOD (Level of Detail)** :
  - Agents loin : décisions moins fréquentes
  - Perception optimisée (spatial grid)
- **Spatial partitioning** :
  - Grid/QuadTree pour requêtes proximité rapides
  - Perception et communication utilisent spatial index
- **Batch processing** :
  - Décisions parallélisables par cellule spatiale
  - Communication asynchrone
- **Scaling tests** :
  - Benchmarks : 50 agents (baseline)
  - Benchmarks : 500 agents (stress)
  - Benchmarks : 1000 agents (limit)
- **Metrics** : ticks/seconde, latence perception, latence décision

**Livrables** :
- Profiling report
- Spatial grid + LOD
- Benchmarks suite (xUnit)
- Documentation performance (doc)

#### 9.2 — Analyzer

- Aggregated metrics pour 1000 agents
- Sampling intelligente (downsampling si trop d'events)

#### 9.3 — Web UI

- Pagination agents, graphes downsampled

#### 9.4 — Godot

- Culling agents lointains, LOD meshes

---

### **Phase 10 : Tests et couverture (viser ≥80%)**

**Assurance qualité** : tester tous les systèmes BDI + interactions.

#### 10.1 — Moteur de simulation

- **Unit tests** :
  - Perception : détection correcte entités
  - Memory : oubli, decay
  - Beliefs : révision vs observations
  - Goals : génération cohérente
  - Utility : scoring multiobjectifs
  - Communication : portée, coûts
  - Groups : inscription/désinscription
  - Resources : production, consommation
  - Actions : exécution, interruption
- **Integration tests** :
  - Cycle complet perception→decision→action→consequence
  - Multi-agent scenarios (coopération, conflit)
  - Partial observability impact
  - Communication efficacité
- **Couverture** : mesure `dotnet test` avec `XPlat Code Coverage`
- **Seuil** : Simulation.Core ≥80 %, Analyzer.Core ≥80 %

**Livrables** :
- Test suite (100+ tests)
- Coverage report
- CI gate `Threshold=80`

#### 10.2 — Analyzer

- Tests agrégation métriques
- Tests comparaison runs
- Coverage ≥80 %

#### 10.3 — Web UI

- Vitest (11 tests V1 + étendre pour nouveaux composants)
- ESLint, Prettier checks

#### 10.4 — Godot

- Import validation (pas de breaking changes)
- Manual playtest (connexion WS, sync state)

---

### **Phase 11 : Analyzer repensé et métriques émergence V2**

**Refondre Analyzer** pour mesurer émergence BDI avancée.

#### 11.1 — Moteur de simulation

- Nouvelles événement types pour Analyzer
- Export beliefs, goals, relations dans snapshot

#### 11.2 — Analyzer v2

- **Nouvelles métriques** :
  - **Cognitive diversity** : variance beliefs, goals, intentions par population
  - **Information propagation** : BFS message spreading, taux d'adoption info
  - **Social complexity** : graphe relations, clustering, centralité
  - **Goal convergence** : agents visent-ils mêmes buts ?
  - **Belief accuracy** : divergence beliefs vs réalité, par agent
  - **Group effectiveness** : groupes vs solo (performance)
  - **Feedback loops** : cycles action→consequence→decision (détecter boucles)
- **Comparaison runs** :
  - Run A (config1) vs Run B (config2) : quels phénomènes diffèrent ?
  - Reproductibilité avec seed

**Livrables** :
- Analyzer v2 (métriques enrichies)
- Tests métriques (16+ tests)
- REST API endpoints pour nouvelles métriques
- Coverage ≥80 %

#### 11.3 — Web UI

- Panneaux rapports V2 : cognitive diversity, information propagation, social graphs
- Comparateur runs avancé

#### 11.4 — Godot

- Heatmaps: belief distribution, goal clusters, social network

---

### **Phase 12 : CI/CD, persistance BD et déploiement**

**Finaliser infrastructure**, migration BDD, déploiement.

#### 12.1 — Moteur de simulation

- **Persistance v2** :
  - Schéma SQLite complet (agents, beliefs, memories, groups, resources, relationships, events log)
  - Exportateur : sim state → SQLite
  - Importateur : SQLite → sim state (reprise exacte)
  - Migration schéma si évolution
- **Save/Load** :
  - Persister tout état simulation (agents, beliefs, memories, groups, resource state, ticks)
  - Tester reprises exactes (tick-perfect comme V1)
  - Reprise après 100 ticks = résultats identiques
- **Docker** : image .NET 10 + runtime, serve + SQLite embarqué
- **CI pipeline** (`.github/workflows/ci.yml`) :
  - Build, tests, coverage seuillée (80%)
  - Lint C# (dotnet format)
  - Docker build/push

**Livrables** :
- Schéma SQLite v1 (complet)
- Save/Load impl + tests
- Docker image
- CI workflow

#### 12.2 — Analyzer v2

- Connection à SQLite shared (lecture metrics)
- Docker image
- REST API (endpoints complets)
- Logging structure dans SQLite
- BDD design finalisé

#### 12.3 — Web UI

- Pages rapports V2
- Connexion analyzer API v2
- Docker image (nginx serve)

#### 12.4 — Godot

- Build template Godot pour packaging

---

## Résumé phases

| Phase | Thème | Statut | Sous-phases |
|-------|-------|--------|-------------|
| 0 | Conception | À faire | Engine, Analyzer, Web, Godot |
| 1 | BDI + Perception | À faire | Engine (core), Analyzer, Web, Godot |
| 2 | Mémoire + Beliefs | À faire | Engine, Analyzer, Web, Godot |
| 3 | Décision + Utilité | À faire | Engine (core), Analyzer, Web, Godot |
| 4 | Actions + Intentions | À faire | Engine, Analyzer, Web, Godot |
| 5 | Communication | À faire | Engine, Analyzer, Web, Godot |
| 6 | Groupes + Coalitions | À faire | Engine, Analyzer, Web, Godot |
| 7 | Dynamique systémique | À faire | Engine, Analyzer, Web, Godot |
| 8 | Observabilité partielle | À faire | Engine, Analyzer, Web, Godot |
| 9 | Scaling 50→1000 agents | À faire | Engine (core), Analyzer, Web, Godot |
| 10 | Tests + Couverture | À faire | Engine, Analyzer, Web, Godot |
| 11 | Analyzer repensé | À faire | Engine, Analyzer, Web, Godot |
| 12 | CI/CD + Persistance BD | À faire | Engine (core), Analyzer, Web, Godot |

---

## Notes importantes

- Chaque phase produit **livrables tangibles** (code, tests, docs)
- **Under-phases** sont exécutées **en parallèle** (planche à étapes)
- **Dépendances** : Phase 1 doit terminer avant Phase 2, etc. (ordre logique)
- **Couverture tests** : dès Phase 1, accumule à Phase 10 (minimal 80%)
- **Documentation** : mise à jour progressive (README, spécifications)

