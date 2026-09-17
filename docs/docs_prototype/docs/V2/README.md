# V2 Documentation — SSE Emergent Simulation Engine

## 📋 Résumé exécutif

La V2 est une refonte majeure du moteur de simulation vers une architecture **BDI multi-agent complexe** supportant :

- ✅ **500-1000 agents** autonomes avec full cognitive stack
- ✅ **Partial observability** (agents ont beliefs ≠ world reality)
- ✅ **Persistent world** (save/load, 100+ heure gameplay)
- ✅ **Emergent phenomena** (mesurées via 7 métriques)
- ✅ **Local communication** (messages radius-based avec coûts)
- ✅ **Groupes et coalitions** explicites (agents s'inscrivent)

**Calendrier** : ~24 semaines, 2-3 devs, 12 phases

---

## 📁 Structure documentation V2

```
docs/V2/
├── 00-PHASES-OVERVIEW.md          → Vue d'ensemble des phases (12 phases, 4 sous-phases)
├── 01-VISION.md                   → Évolution V1→V2, 5 principes
├── 02-CONCEPTUAL-MODEL.md         → Modèle d'entités, structure BDI
├── 03-V2-SPECIFICATION.md         → Algorithmes formels, exemples
├── 04-ARCHITECTURE.md             → Décomposition modules, interfaces, patterns
├── 05-AGENTS-BDI.md               → Pipeline BDI pseudocode, intérieur des agents
├── 06-DECISION-SYSTEM.md          → Utilité multidimensionnelle, modificateurs de traits
├── 07-COMMUNICATION-PROTOCOL.md   → Types de messages, protocole, exemples
├── 08-PERSISTENCE.md              → Schéma SQLite, save/load, déterminisme
├── 09-ANALYZER-V2.md              → Métriques d'émergence (7 dimensions)
├── 10-WEB-UI-V2.md                → Composants React, streaming WebSocket
├── 11-RENDERER-3D-V2.md           → Godot C#, visualisation croyances, heatmaps confiance
├── 12-OBSTACLES-STATIC.md         → Pathfinding, collision, ligne de vue
├── 13-LOGGING-INSTRUMENTATION.md  → Événements, traces, profilage
├── 14-SCALING-STRATEGY.md         → Grille spatiale, LOD, benchmarks
├── 15-TEST-PLAN.md                → 160+ tests unitaires, couverture 80%
├── 16-ROADMAP.md                  → Tâches détaillées par phase
└── 17-ADR.md                       → 9 décisions architecturales
```

---

## 🎯 Principaux changements V1 → V2

| Aspect | V1 | V2 |
|--------|----|----|
| **Agents** | ~50 agents simples | 500-1000 agents BDI |
| **Prise de décision** | Basée sur règles | BDI + scoring utilité |
| **Modèle du monde** | Agents omniscients | Observabilité partielle |
| **Communication** | Pas de comm inter-agents | Messages structurés, rayon local |
| **Persistance** | Fichiers JSON | SQLite avec ACID |
| **Émergence** | Ressources/population | Dynamiques cognitives, sociales, informationnelles |
| **Persistance** | Basée sur session | Monde persistant (save/load) |
| **Groupes** | Pas de groupes explicites | Coalitions explicites + rôles |
| **Performance** | 50 agents @ 25 ticks/sec | 1000 agents @ 10+ ticks/sec |

---

## 🧠 Architecture BDI

**Flux cognitif de l'agent par tick** :

```
Perception → Mémoire → Croyances → Besoins → Objectifs → Utilité → Délibération → Intention → Action
```

**Détails** :

1. **Perception** (Capteurs locaux)
   - Interroger les entités proches (rayon 50 unités)
   - Retourner les observations avec confiance

2. **Mémoire** (HistoriqueAgent)
   - Stocker les observations avec horodatage
   - Décroissance exponentielle (exp(-0.05*âge))
   - Maximum 1000 entrées/agent

3. **Croyances** (BeliefStore)
   - Fait + confiance + source + expiration
   - Règles de révision (conflit→confiance降低, alignement→renforcement)
   - Observabilité partielle : les agents peuvent avoir de fausses croyances

4. **Besoins** (Physiologiques)
   - Faim, Soif, Fatigue, Sécurité, Social, Curiosité
   - Mise à jour chaque tick (augmentation naturelle, diminution par actions)
   - Niveau 0-100

5. **Objectifs** (Générés)
   - À partir des besoins non satisfaits (niveau > 50)
   - Filtrés par faisabilité (l'agent peut-il l'atteindre ?)
   - Priorisés par urgence

6. **Évaluation d'utilité**
   - Multi-facteurs : `(bénéfice - coût - risque) * confiance * personnalité + urgence`
   - Exemple : Manger = 18 bénéfice, 4 coût, 2 risque, 0.85 confiance → utilité 17.22
   - Traits différents → décisions différentes

7. **Délibération** (DecisionSystem)
   - Sélectionner l'action d'utilité maximale
   - Peut interrompre l'action courante si besoin urgent survient
   - Générer DecisionRecord (traçage)

8. **Intention→Action**
   - Démarrer ou continuer l'action
   - Actions multi-tick (MoveTo prend 5+ ticks)
   - Générer événements (ActionStarted, ActionCompleted)

---

## 🗣️ Protocole de communication

**Types de messages** (7) :

1. **Information** - Partager un fait ("Nourriture à 60,80")
2. **Requête** - Demander de l'aide
3. **Réponse** - Répondre à une requête
4. **Annonce** - Diffuser à tous dans le rayon
5. **Avertissement** - Alerter d'un danger
6. **Échange** - Proposer un échange de ressources
7. **Accusé de réception** - Confirmer la réception d'un message

**Mécaniques** :

- **Rayon** : Messages entendus uniquement dans un rayon de 50 unités
- **Coût** : 5 énergie pour envoyer, 2 pour recevoir
- **Confiance** : Qualité du message diminue par saut (10% par saut)
- **Confiance expéditeur** : Confiance message × niveau de confiance expéditeur
- **Hubs d'information** : Agents de confiance deviennent goulots d'étranglement de communication

---

## 💾 Persistance (SQLite)

**Schéma** (11 tables principales) :

- `simulation_state` - Métadonnées run, tick courant, état PRNG
- `agents` - État agent (position, énergie, traits)
- `agent_beliefs` - Croyances par agent (fait, confiance, source, âge)
- `agent_memories` - Entrées mémoire (observation, saillance)
- `agent_relationships` - Graphe confiance (agent→agent, niveau confiance)
- `groups` - Métadonnées groupe (nom, chef, objectif)
- `group_memberships` - Effectif groupe (agent, rôle, tick_entrée)
- `resources` - Localisations et quantités ressources
- `obstacles` - Obstacles statiques (position, taille, forme)
- `events_log` - Événements structurés (perception, décision, action)
- `decision_traces` - État BDI complet par décision
- 3× tables config (simulation, environnement, monde)

**Processus Save/Load** :

1. Mettre en pause la simulation
2. BEGIN TRANSACTION
3. Sauvegarder toutes les entités (agents, croyances, ressources, groupes, etc.)
4. Sauvegarder l'état PRNG
5. COMMIT
6. ~50MB par sauvegarde (1000 agents)

**Déterminisme** :

- État PRNG persisté → restauration depuis sauvegarde, même séquence
- Test validation bit-à-bit : charger save, exécuter 100 ticks, comparer à exécution continue
- Toutes les valeurs flottantes préservées exactement (pas d'arrondi)

---

## 📊 Métriques d'émergence (7 dimensions)

### 1. Diversité cognitive
- Entropie des croyances (Shannon)
- Variance des objectifs
- Diversité des décisions
- **Interprétation** : Haute diversité = les agents pensent différemment

### 2. Propagation d'information
- Volume de messages
- Vitesse de diffusion (ticks pour atteindre 80%)
- Dégradation précision rumeurs
- Sauts maximum
- **Interprétation** : Diffusion rapide = réseau de communication efficace

### 3. Complexité sociale
- Niveau de confiance moyen
- Coefficient de clustering (tendance à former triangles)
- Communautés détectées (algorithme Louvain)
- Centralité du réseau
- **Interprétation** : Clustering élevé = groupes soudés, hubs

### 4. Convergence d'objectifs
- Alignement global (% agents sur même objectif principal)
- Potentiel coopération (% objectifs compatibles)
- **Interprétation** : Convergence élevée = coordination, faible = diversité

### 5. Boucles de rétroaction
- Cycles identifiés
- Force de boucle (amplification)
- Stabilité système
- **Interprétation** : Boucles fortes = dynamiques auto-renforçantes

### 6. Dynamique des groupes
- Groupes actifs
- Taux formation/dissolution
- Taux succès
- Rotation membres
- **Interprétation** : Rotation élevée = coalitions instables

### 7. Score d'émergence composite
- Somme pondérée de toutes les métriques
- Échelle 0-1
- **Interprétation** : Quelle est la complexité globale de la simulation ?

---

## 🔬 Stratégie de test (couverture 80%+)

**160+ tests unitaires** :

- PerceptionSystem : 12 tests (requêtes spatiales, précision, LOD)
- MemorySystem : 10 tests (décroissance, rappel, catégorisation)
- BeliefStore : 14 tests (révision, expiration, conflit)
- GoalSystem : 12 tests (génération, faisabilité, priorité)
- UtilityEvaluator : 15 tests (scoring, traits, confiance)
- ActionSystem : 20 tests (exécution, interruption, multi-tick)
- CommunicationSystem : 16 tests (diffusion, confiance, dégradation)
- GroupSystem : 12 tests (formation, gestion membres)
- ... (7 autres systèmes)

**Tests d'intégration** :

- Cycle BDI complet (perception→décision→action)
- Coopération multi-agents
- Application observabilité partielle
- Persistance (save/load bit-à-bit)

**Benchmarks** :

- 50 agents @ 30+ ticks/sec
- 500 agents @ 20+ ticks/sec
- 1000 agents @ 10+ ticks/sec

---

## 🚀 Stratégie d'optimisation (Phase 9)

### Goulots d'étranglement & Solutions

| Problème | Impact | Solution | Cible |
|----------|--------|----------|-------|
| Perception O(n²) | 1M comparaisons/tick @ 1000 agents | Grille spatiale | O(1) moyenne |
| Décision O(n*m) | 500 agents × 3 objectifs × 5 actions | Mise en cache actions + élagage | Ignorer si stable |
| Communication | Tous agents dans rayon | Traitement par lots + requêtes spatiales | -40% temps |
| Mémoire (churn) | Pression GC | Pool d'objets | -30% GC |

### Cibles de profilage

- Perception : 20ms (cible)
- Décision : 20ms (cible)
- Communication : 15ms (cible)
- Mouvement : 15ms (cible)
- **Total : <100ms** (10 ticks/sec @ 1000 agents)

---

## 🛠️ Feuille de route d'implémentation

### Phase 0 : Architecture (1 semaine)
- ✅ Docs complètes
- Revue ADR
- Alignement équipe

### Phases 1-4 : BDI Core (8 semaines)
- Perception + Mémoire
- Croyances + révision
- Besoins + Objectifs
- Décisions + Actions

### Phases 5-7 : Systèmes avancés (5 semaines)
- Protocole communication
- Groupes + coalitions
- Ressources + environnement

### Phase 8 : Observabilité (1 semaine)
- Valider observabilité partielle
- Appliquer encapsulation

### Phase 9 : Scaling (3 semaines)
- Grille spatiale
- LOD + staggering
- Benchmarking

### Phases 10-12 : Qualité + Déploiement (5 semaines)
- Tests + couverture
- Métriques Analyzer
- CI/CD + Docker

---

## 📈 Comportements émergents attendus

Avec l'implémentation complète V2, on s'attend à observer :

1. **Réseaux d'information** - Certains agents deviennent sources de confiance
2. **Hubs d'échange** - Centres d'échange de ressources se forment
3. **Instabilité des coalitions** - Groupes se dissolvent objectifs atteints
4. **Cascades de désinformation** - Fausses croyances se propagent puis corrigées
5. **Spécialisation** - Agents se concentrent sur rôles différents
6. **Zones de conflit** - Agents non fiables s'évitent
7. **Résolution collective problèmes** - Agents coopèrent sur objectifs difficiles
8. **Cycles de rétroaction** - Coopération→confiance→plus coopération

---

## 📚 Documents clés à lire en premier

1. **01-VISION.md** - Philosophie + comparaison V1/V2
2. **02-CONCEPTUAL-MODEL.md** - Relations entités
3. **03-V2-SPECIFICATION.md** - Algorithmes formels
4. **04-ARCHITECTURE.md** - Structure code
5. **05-AGENTS-BDI.md** - Pipeline complet pseudocode
6. **16-ROADMAP.md** - Tâches phase par phase

---

## ⚙️ Stack technique

| Composant | Technologie |
|-----------|-------------|
| Engine | .NET 10 C# (Simulation.Core) |
| Persistance | SQLite 3 |
| Analyzer | ASP.NET + Kestrel |
| Web UI | React 18 + TypeScript + D3.js |
| Renderer | Godot 4.x C# |
| CI/CD | GitHub Actions |
| Déploiement | Docker Compose |

---

## 🎓 Fondations académiques

- **Modèle BDI** : Bratman, Rao-Georgeff
- **Émergence** : Waldrop "Complexity", théorie CAS
- **Communication agents** : Langage FIPA Agent Communication
- **Confiance** : Théorie choix social, réputation bayésienne
- **Théorie information** : Entropie Shannon pour diversité

---

## 📞 Questions ?

- Décisions architecturales → voir dossier ADR
- Détails algorithmes → voir 03-SPECIFICATION
- Implémentation → voir 16-ROADMAP avec pseudocode
- Théorie émergence → voir 09-ANALYZER-V2

