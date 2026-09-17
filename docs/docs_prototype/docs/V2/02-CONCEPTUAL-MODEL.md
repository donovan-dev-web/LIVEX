# Modèle conceptuel V2

## 1. Entités du monde V2

```text
Monde (Persistant)
├── Agents (autonomes, cognitifs BDI)
├── Ressources (nourriture, eau, bois, etc.)
├── Obstacles (statiques)
├── Groupes (coalitions d'agents)
├── Environnement (événements, cycles)
└── Réseau communication (messages locaux)
```

---

## 2. Agent BDI V2

Contrairement à V1 (agent = state + needs + actions simples), l'agent V2 est **architecturalement** complexe.

### 2.1 Structure complète

```text
Agent
├── Identité
│   ├── Id (unique)
│   ├── Nom (optionnel)
│   ├── Espèce (espèce, détermine capacités)
│   └── Âge
│
├── État interne
│   ├── Santé (0–100)
│   ├── Énergie (0–100)
│   ├── Faim (0–100)
│   ├── Soif (0–100)
│   ├── Position (x, y)
│   └── Traits (agressivité, sociabilité, prudence, ambition, etc.)
│
├── Système perception
│   ├── Rayon perception (configurable)
│   ├── Observations courantes (entités détectées ce tick)
│   └── Capacités sensorielles (vision, ouïe, odorat)
│
├── Système mémoire
│   ├── Historique observations (datées, avec décroissance)
│   ├── Journal événements (actions personnelles passées)
│   └── Historique interactions (avec qui, quand)
│
├── Magasin croyances
│   ├── Faits (agent croit X se trouve à Y)
│   ├── Confiance (0–1, degré certitude)
│   ├── Horodatage (dernière observation)
│   └── Expiration (devient obsolète après N ticks)
│
├── Carte relations
│   ├── Niveaux confiance (envers autres agents)
│   ├── Familiarité (combien de rencontres)
│   ├── Dernières interactions (quand dernière interaction)
│   └── Appartenances groupes
│
├── Évaluateur besoins
│   ├── Pulsion faim (0–1)
│   ├── Pulsion soif (0–1)
│   ├── Besoin sécurité (0–1)
│   ├── Besoin social (0–1)
│   ├── Besoin repos (0–1)
│   └── Besoin curiosité (0–1)
│
├── Système objectifs
│   ├── Objectifs actifs (liste objectifs courants)
│   ├── Priorité objectifs (classement)
│   ├── Statut objectif (poursuivi, en pause, abandonné)
│   └── Historique objectifs
│
├── Système décision
│   ├── Évaluateur utilité (scoring multi-critères)
│   ├── Enregistrement décision (trace dernière décision)
│   └── Intention (action choisie + engagements)
│
├── Système actions
│   ├── Action courante (MoveTo, Eat, Communicate, etc.)
│   ├── Progression action (pourcentage exécution)
│   ├── File actions (prochaines actions)
│   └── Historique actions (actions passées)
│
└── Communication
    ├── Messages entrants (file)
    ├── Messages sortants (à envoyer)
    └── Historique communication (messages reçus/envoyés)
```

---

## 3. Perception

L'agent ne voit **que ce qu'il peut raisonnablement percevoir**.

### 3.1 Types de perceptions

```text
Observation
├── EntityId (identifiant unique)
├── EntityType (Agent, Nourriture, Eau, Obstacle, etc.)
├── Position (x, y) — relatif ou absolu ?
├── Distance (par rapport à l'agent)
├── Direction (angle ou cardinal)
├── KnownProperties (entité spécifique : santé, quantité, etc.)
└── Timestamp (quand observé)
```

### 3.2 Rayon de perception

Chaque agent a un **rayon de perception** (configurable par espèce, stats) :

```
DEFAULT : 30 unités
Peut être modifié par :
  - Acuité (stats)
  - Contexte (fatigue réduit perception)
  - Jour/nuit
```

### 3.3 Observations actuelles vs mémoire

**Ce tick** :
- Agent A perçoit nourriture à 10m
- Observation ajoutée
- Croyance créée/mise à jour : "nourriture à 10m, 100% confiance"

**Prochain tick** :
- Nourriture peut avoir disparu (mangée)
- Croyance devient obsolète (horodatage ancien)
- Mais mémoire la contient encore

---

## 4. Mémoire

La mémoire permet au monde de **diverger entre agents**.

### 4.1 Types de mémoire

```text
Entrée mémoire
├── Type (Observation, Événement, Interaction)
├── Data (ce dont l'agent se souvient)
├── Timestamp (quand observé)
├── Confiance (0–1, certitude en info)
├── Source (perception directe, rumeur, transmission)
└── Taux décroissance (oubli progressif)
```

### 4.2 Decay (oubli)

Les souvenirs s'affaiblissent avec le temps :

```
Confiance(t) = Confiance(0) * exp(-taux_décroissance * ticks_écoulés)
```

Exemple :
```
Tick 100 : Agent A voit nourriture, confiance 1.0
Tick 200 : confiance 0.7 (100 ticks passés)
Tick 500 : confiance 0.0 (oublie complètement)
```

### 4.3 Catégories de mémoire

**Observations** : J'ai vu une source d'eau à cette position
**Événements** : Je me suis battu avec Agent B hier
**Interactions** : J'ai parlé à Agent C, il m'a dit X
**Faits** : Les groupes se forment près des ressources

---

## 5. Croyances

Les croyances sont des **représentations mentales** que l'agent maintient.

### 5.1 Structure

```text
Croyance
├── Fait (nourriture existe à position X)
├── Confiance (0–1, certitude)
├── Source (perception directe, mémoire, ouï-dire)
├── Horodatage (quand confirmée)
└── Domaine (spatial, social, etc.)
```

### 5.2 Croyances vs Réalité

**Monde réel** : nourriture réelle à position (100, 50)

**Agent A croit** : nourriture à (100, 50), confidence 0.9
**Agent B croit** : nourriture à (110, 45), confidence 0.5 (info ancienne)
**Agent C croit** : pas de nourriture là (croyance fausse)

→ **Cette divergence crée l'émergence**.

### 5.3 Révision de croyances

Quand un agent perçoit quelque chose de nouveau :

```
ANCIENNE CROYANCE : nourriture à (100, 50), confiance 0.7
NOUVELLE OBSERVATION : nourriture à (100, 51)
CROYANCE RÉVISÉE : nourriture à (100, 51), confiance 0.95
```

Si contradiction :

```
ANCIENNE CROYANCE : pas d'eau ici
NOUVELLE OBSERVATION : eau trouvée !
CROYANCE RÉVISÉE : eau à (75, 75), confiance 0.8
                (mais un doute subsiste : était-ce caché ?)
```

---

## 6. Besoins

Les besoins génèrent la **pression comportementale**.

### 6.1 Hiérarchie V2

```text
Besoins
├── Physiologiques
│   ├── Faim (0–1)
│   ├── Soif (0–1)
│   ├── Énergie / Repos (0–1)
│   └── Santé (si endommagé)
├── Sécurité
│   ├── Perception danger (0–1)
│   └── Besoin sécurité (0–1)
├── Sociaux
│   ├── Solitude (0–1)
│   ├── Appartenance groupe (0–1)
│   └── Pulsion coopération
├── Cognitifs
│   ├── Curiosité (0–1)
│   ├── Pulsion exploration
│   └── Recherche information
└── Avancés (V3+)
    ├── Accomplissement
    ├── Statut
    └── Reproduction
```

### 6.2 Calcul des besoins

À chaque tick :

```
Faim = min(100, Faim + taux_consommation)
Soif = min(100, Soif + taux_déshydratation)
Énergie = max(0, Énergie - coût_activité)
Dégradation santé = function(blessures, âge, fatigue)
```

Les besoins **ne sont pas des objectifs** — ce sont des **tensions** qui influencent les objectifs.

---

## 7. Objectifs

Les objectifs transforment les besoins en intentions.

### 7.1 Génération

Quand `Faim > 60` :
- Objectifs possibles :
  - Chercher nourriture
  - Communiquer pour aide
  - Voler au groupe
  - Chasser ensemble

Chaque objectif est **candidat** pour sélection.

### 7.2 Structure

```text
Objectif
├── Type (Chercher, Collecter, Communiquer, Échanger, Repos, Explorer)
├── Cible (position ressource, id agent, lieu)
├── Priorité (0–1, générée depuis besoin)
├── Statut (Actif, En pause, Abandonné)
├── Progression (pourcentage réalisation)
├── Échéance (si applicable)
└── Utilité attendue (valeur si atteint)
```

### 7.3 Faisabilité

Un goal est considéré viable si :

```
- Agent a capacité de l'accomplir
- Ressources/info sont accessibles (dans croyances)
- Risque acceptable (selon traits)
```

Sinon → objectif remis en attente ou abandonné.

---

## 8. Intentions

Une **intention** est un **engagement** envers une action.

```text
Intention
├── Selected goal
├── Selected action
├── Target / Parameter (position, agent id, etc.)
├── Commitment level (ferme ou tentative)
└── Interruption conditions (si danger, réabondonner)
```

**Important** : intention ≠ action. Intention est une **décision délibérée**.

Exemple :

```
Goal : Seek Food
Selected Action : MoveTo(foodPosition)
Intention : Move towards food at (85, 42)

→ Action exécutée ce tick
→ Peut être interrompue (danger apparaît)
→ Peut être réévaluée prochain tick (nouveau info)
```

---

## 9. Décision et Utilité

### 9.1 Processus décisionnel BDI

```
PERCEPTION → BELIEF REVISION → GOAL GENERATION → DELIBERATION → INTENTION
     ↓              ↓                 ↓                ↓             ↓
Qu'est ce       Ce que je           Quoi            Comment      Agir
que je vois ?   crois maintenant ?   faire ?         comparer ?   maintenant
```

### 9.2 Fonction d'utilité V2

```
Utility(action A) = 
    BENEFIT(A)          -- satisfaction besoins
    - COST(A)           -- coût énergétique/temps
    - RISK(A)           -- chance d'échec/danger
    × CONFIDENCE(info)  -- confiance beliefs utilisés
    + URGENCY(goal)     -- priorité du goal
    × PERSONALITY()     -- traits modifient calcul
```

Exemple détaillé :

```
Goal : Satisfaire faim (Hunger = 85)
Available actions :
  A) Seek nearby food (in beliefs)
  B) Ask Agent X for help
  C) Attack Agent Y to steal food

Scoring :

A = Seek nearby food
  Benefit = 50 (faim satisfaction modéré, food est-elle encore là ?)
  Cost = 10 (distance 15m)
  Risk = 5 (pas d'obstacles connus)
  Confidence = 0.9 (observation récente, timestamp 5 ticks passés)
  Urgency = 0.8
  Personality : sociability 0.3 (solo preference)
  = (50 - 10 - 5) × 0.9 + 0.8 - 0.3 = 35 * 0.9 + 0.5 = 31.5 + 0.5 = 32

B = Ask Agent X
  Benefit = 40
  Cost = 15 (distance + communication)
  Risk = 10 (uncertain collaboration)
  Confidence = 0.6 (X trustworthy ?)
  Urgency = 0.8
  Personality : sociability 0.8
  = (40 - 15 - 10) × 0.6 + 0.8 + 0.8 = 15 * 0.6 + 1.6 = 9 + 1.6 = 10.6

C = Attack Y
  Benefit = 80 (food guaranteed)
  Cost = 20
  Risk = 60 (combat, Y may fight back, group retaliation)
  Confidence = 1.0 (Y has food)
  Urgency = 0.8
  Personality : aggression 0.5, sociability 0.3
  = (80 - 20 - 60) × 1.0 + 0.8 - 0.3 + 0.5 = 0 + 0.8 - 0.3 + 0.5 = 1

→ A selected (utilité 32 > 10.6 > 1)
```

### 9.3 Variabilité d'agent

Deux agents, contexte identique, traits différents :

**Agent A (sociable, brave)** :
- Attack Y : +0.3 (aggression boost)
- Ask X : +0.5 (sociability boost)
- → Pourrait choisir B (Ask) au lieu de A

**Agent B (solitaire, timide)** :
- Attack Y : -0.2 (risk aversion)
- Ask X : -0.5 (sociability penalty)
- → Choisit A (Seek) sans hésitation

**Même contexte, décisions différentes** → Pas besoin d'énumérer tous les comportements.

---

## 10. Actions

Les actions sont des **capacités exécutables**.

### 10.1 Catalogue V2

```text
Mobility
├── MoveTo(position) — se déplacer
└── Explore(region) — explorer région inconnue

Gathering
├── Eat(foodSource) — consommer nourriture
├── Drink(waterSource) — consommer eau
└── Gather(resource) — collecter pour inventaire

Social
├── Communicate(targetAgent, message) — envoyer message
├── TradeWith(targetAgent, give, receive) — échanger
└── Join(group) — rejoindre groupe

Defense
├── Rest(duration) — regagner énergie
├── Flee(threatAgent) — fuir un danger
└── Attack(targetAgent) — combattre

Inspection
└── Observe(target) — examiner attentivement
```

### 10.2 États d'action

```
Pending → Executing → Completed
                  ↓
              → Failed
                  ↓
              → Cancelled
```

### 10.3 Exécution multi-tick

Certaines actions prennent plusieurs ticks :

```
Action MoveTo(100, 50), distance = 30m, speed = 1m/tick

Tick 1 : action = MoveTo, progress = 0 %
Tick 2 : progress = 3 % (3m parcouru)
...
Tick 30 : progress = 100 % → Completed
```

---

## 11. Communication

Un système d'échange structuré entre agents.

### 11.1 Message structure

```text
Message
├── SenderId (qui envoie)
├── ReceiverId (qui reçoit, peut être broadcast)
├── Type (Information, Request, Response, Announcement)
├── Payload (contenu structuré)
├── Timestamp
├── Confidence (agent croit ?)
└── Source (direct perception, hearsay, etc.)
```

### 11.2 Types de messages

**Information** : "Nourriture à (85, 42)"
**Request** : "Peux-tu m'aider à chasser ?"
**Response** : "Non, trop fatigué."
**Announcement** : "Groupe formé, cherche membres !"

### 11.3 Portée et coûts

- **Portée** : communication locale (rayon configurable)
- **Coût** : énergie pour parler, attention pour écouter
- **Fiabilité** : message peut être mal compris, oublié, transmis incorrectement

---

## 12. Groupes

Agrégation d'agents avec objectif partagé.

### 12.1 Structure

```text
Group
├── GroupId (unique)
├── Name (optionnel)
├── LeaderId (agent qui dirige)
├── ObjectiveGoal (chasse, construction, défense)
├── Members (liste agents + rôles)
├── SharedResources (inventaire groupe)
├── CreationTick
└── Status (Active, Suspended, Dissolved)
```

### 12.2 Rôles dynamiques

```
Roles :
├── Leader (coordonne)
├── Scout (explore)
├── Gatherer (collecte)
├── Hunter (combattant)
├── Supporter (aide)
└── Custom...
```

Un agent peut changer de rôle ou quitter le groupe.

---

## 13. Relations inter-agents

Chaque agent maintient une carte relationnelle.

### 13.1 Dimensions

```text
Relationship (Agent A → Agent B)
├── Trust Level (0–1, confiance en B)
├── Familiarity (nombre interactions)
├── Last Interaction (timestamp)
├── Relationship Type (ally, neutral, enemy)
├── Shared History (événements communs)
└── Current Status (active, dormant, hostile)
```

---

## 14. Ressources

Éléments du monde consommables/productifs.

### 14.1 Types

```text
Resources
├── Food (aliment, quantity, regeneration_rate)
├── Water (boisson, quantity, regeneration_rate)
├── Wood (construction, quantity, regeneration_rate)
├── Minerals (divers, quantity, regeneration_rate)
└── Custom...
```

### 14.2 Propriétés

```
Resource
├── ResourceId
├── Type
├── Position (x, y)
├── Quantity (actuel)
├── Capacity (max)
├── RegenerationRate (tick/ticks)
├── DegradationRate (se détériore si non utilisé)
└── LastHarvestedTick
```

---

## 15. Environnement

Le cadre dans lequel tout se produit.

### 15.1 Propriétés

```
Environment
├── WorldSize (x, y dimensions)
├── Time (tick counter)
├── Season (spring, summer, fall, winter)
├── Weather (affects movement, visibility)
├── Events (catastrophes, anomalies)
└── DayNightCycle (affects perception, behavior)
```

### 15.2 Obstacles statiques

```
Obstacle
├── ObstacleId
├── Type (wall, mountain, cliff)
├── Position (x, y)
├── Size (width, height, collision geometry)
└── Passable (true/false)
```

---

## 16. Monde persistant

Le monde n'est pas une simulation. C'est un **état unique, persistant**.

### 16.1 État complet

```
World State
├── Tick number (progression temporelle)
├── All agents (+ leurs beliefs, memories, goals)
├── All resources (quantities, positions)
├── All groups (composition, objectives)
├── All relationships (trust networks)
├── Event log (historique complet)
└── SQLite database (persistence)
```

### 16.2 Transactions

Chaque action produit des **conséquences atomiques** :

```
Agent A eats from Food B
  → Update Quantity(Food B)
  → Update Hunger(Agent A)
  → Generate Event: FoodConsumed
  → Update WorldState
  → Broadcast to all perceiving agents
```

---

## 17. Synthèse conceptuelle

| Concept | Role | V1 | V2 |
|---------|------|----|----|
| **Perception** | Base données | Simple | Rayon local, datée |
| **Memory** | Historique | Rudimentaire | Observations + decay |
| **Beliefs** | Représentation | N/A | Explicite + confidence |
| **Needs** | Pression | Basique | Hiérarchie riche |
| **Goals** | Objectifs | N/A | Générés de besoins |
| **Utility** | Décision | Simple | Multidimensionnelle |
| **Intention** | Engagement | N/A | Binding goal→action |
| **Actions** | Capacités | 9 | 12+ modulaires |
| **Communication** | Échange | N/A | Protocole structuré |
| **Groups** | Agrégation | N/A | Explicites + dynamiques |
| **Relationships** | Liens | N/A | Trust networks |
| **World** | Persistance | JSON | SQLite |
| **Scaling** | Agents | ~50 | 500–1000 |

