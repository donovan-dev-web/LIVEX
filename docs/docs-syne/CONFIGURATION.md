# CONFIGURATION.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `DATA_MODEL.md`, `DETERMINISM.md`
**Source Monographie** : Annexe H (configuration et paramètres), §3.6.2 (seed), §3.4 (scheduler)

---

## 1. Principe

La configuration est un **contrat reproductible** : le même `config.json` + même seed + même version moteur doit remplacer la même trajectoire. Toute évolution de la configuration doit suivre `VERSIONING.md`.

## 2. Fichier de configuration V1 (Annexe H)

```json
{
  "simulation": {
    "worldWidth": 500,
    "worldHeight": 500,
    "maxTicks": 1000000,
    "ticksPerSecond": 10,
    "autoSaveEveryNTicks": 1000,
    "maxBackups": 5
  },
  "agents": {
    "initialCount": 100,
    "traits": { "bravery": 1.0, "curiosity": 1.0, "sociability": 1.0, "greed": 1.0,
                 "pessimism": 1.0, "aggressiveness": 1.0, "strength": 1.0, "speed": 1.0 },
    "needs": { "hungerRate": 0.5, "thirstRate": 0.7, "fatigueRate": 0.3,
               "safetyDriftRate": 0.001, "socialDriftRate": 0.001, "curiosityDriftRate": 0.002,
               "hungerTriggerThreshold": 50, "thirstTriggerThreshold": 50, "fatigueTriggerThreshold": 70 },
    "perception": { "radius": 50, "confidenceFalloff": 0.3, "rotationInterval": 4, "lineOfSight": true },
    "memory": { "maxCapacity": 1000, "recallThreshold": 0.01,
                "observationDecayRate": 0.01, "eventDecayRate": 0.005, "interactionDecayRate": 0.002 },
    "beliefs": { "updateStrength": 0.3, "maxChangePerSnap": 0.5, "alignBonus": 0.2,
                 "conflictPenalty": 0.1, "expiryTicks": 100, "expiredCap": 0.4, "timeDecayPerTick": 0.999 },
    "actions": { "moveEnergyCost": 0.5, "restEnergyGain": 0.5, "restFatigueRecovery": 1.0,
                 "deliberation": { "intervalTicks": 10, "alignBonus": 1.2,
                                   "actionSwitchMargin": 0.05, "conflictTieMargin": 0.5 },
                 "interruption": { "enabled": true, "utilityExcessMargin": 10.0,
                                   "criticalHunger": 85.0, "criticalEnergy": 10.0 },
                 "catalog": {
                   "idle": { },
                   "seekFood": { "movement": true },
                   "seekWater": { "movement": true },
                   "eat": { "energyCost": 0.2, "hungerRecovery": 30.0, "reserve": "food" },
                   "drink": { "energyCost": 0.2, "thirstRecovery": 30.0, "reserve": "water" },
                   "rest": { },
                   "flee": { "movement": true },
                   "socialize": { "movement": true },
                   "explore": { "movement": true }
                 } }
  },
  "resources": {
    "food": { "initial": 100, "regenerationRate": 0, "degradationTick": 100 },
    "water": { "initial": 1000, "regenerationRate": 5 },
    "wood": { "initial": 50, "regenerationRate": 0.1 },
    "mineral": { "initial": 0, "regenerationRate": 0 }
  },
  "communication": {
    "transmissionRange": 55,
    "relayEnabled": true,
    "maxHops": 2,
    "maxSendsPerTick": 5,
    "maxReceivesPerTick": 3,
    "incomprehensionRate": 0.05,
    "trustDecay": 0.9,
    "hopConfidenceDecay": 0.9,
    "sendEnergyCost": 0.5,
    "sendEnergyPayloadFactor": 0.1,
    "receiveEnergyCost": 0.2,
    "receiveEnergyPayloadFactor": 0.05
  },
  "world": { "seasons": { "enabled": false }, "territories": { "enabled": false }, "books": { "enabled": false, "writeCostEnergy": 20.0, "readBenefit": 1.0 }, "events": false, "obstacles": false },
  "groups": {
    "enabled": true,
    "reviewIntervalTicks": 10,
    "trustThreshold": 0.3,
    "minGroupSize": 3,
    "sharedBeliefBonus": 0.1,
    "goalAlignmentBonus": 0.2,
    "consensusThreshold": 0.5
  },
  "reproduction": {
    "enabled": true,
    "intervalTicks": 100,
    "consentTrustThreshold": 0.6,
    "maxBirthsPerTick": 1
  },
  "agents": {
    "inheritance": { "salienceThreshold": 0.01 }
  },
  "random": { "seed": 12345, "engine": "xoshiro256**" },
  "performance": {
    "parallelPerception": true,
    "spatialGrid": true,
    "decisionCaching": true,
    "batchCommunication": true
  }
}
```

## 3. Flags expérimentaux (ligne de commande)

| Flag | Valeur | Effet |
| :-- | :-- | :-- |
| `--headless` | bool | Pas de console |
| `--world-size 500 500` | int int | Dimensions du monde |
| `--seed 12345` | int | Seed du PRNG |
| `--max-ticks 2000` | int | Nombre de ticks à exécuter |
| `--config path/to/config.json` | string | Fichier de configuration |
| `--observe` | bool | Active l'émission WebSocket (SYNE-080, API_CONTRACTS §2) |
| `--observe-port 5180` | int | Port du serveur WebSocket (défaut : 5180, bind 127.0.0.1) |

(Annexe H.2)

## 4. Configuration par espèce (Monographie §3.12.5)

```json
{
  "species": "Human",
  "consumption_rate": 0.5,
  "dehydration_rate": 0.3,
  "base_metabolism": 0.1,
  "movement_cost": 0.2
}
```

> V0.1 : paramétrages (« Entité A » / « Entité B ») plutôt que classes codées en dur (cf. `DATA_MODEL.md`).

## 5. Priorité de chargement

1. `config.json` défaut intégré.
2. `--config <fichier>` (surcouche).
3. FLAGS CLI pour overrides (seed, taille, ticks).

## 6. Validation de configuration

- Validation des plages à l'import :
  - `perception.radius` ∈ [20, 70] (décision n°6) ; `rotationInterval` ≥ 1 ;
  - `memory.maxCapacity` > 0 ; taux de décroissance ≥ 0 ;
  - `beliefs.updateStrength` ∈ [0, 1] ;
  - `actions.deliberation.intervalTicks` ≥ 1 ; `alignBonus` > 0 ; `actionSwitchMargin`/`conflictTieMargin` ≥ 0 ;
  - `actions.interruption.utilityExcessMargin` ≥ 0 ; `criticalHunger` ∈ (0, 100] ; `criticalEnergy` ∈ [0, 100) ;
  - `needs.hungerTriggerThreshold`/`thirstTriggerThreshold`/`fatigueTriggerThreshold` ∈ (0, 100] ;
  - `actions.catalog` complet : une entrée **obligatoire** pour chaque action (`idle`, `seekFood`, `seekWater`, `eat`, `drink`, `rest`, `flee`, `socialize`, `explore`) — échec déclaratif si une clé manque.
  - `communication.transmissionRange` ∈ (0, 70] ; `maxSendsPerTick`/`maxReceivesPerTick` ≥ 0 ; `maxHops` ≥ 1 ; `incomprehensionRate`/`hopConfidenceDecay`/`trustDecay` ∈ [0, 1] ; coûts (base + facteurs) ≥ 0 (SYNE-052/053) ; `transmissionRange` > `perceptionRange` invalide (la perception doit rester strictement supérieure).
   - `groups.enabled`/`reproduction.enabled` booléens ; `reviewIntervalTicks`/`intervalTicks` ≥ 1 ; `trustThreshold`/`consensusThreshold`/`consentTrustThreshold` ∈ [0, 1] ; `minGroupSize` ≥ 2 ; `maxBirthsPerTick` ≥ 1 ; `agents.inheritance.salienceThreshold` > 0.
  - `resources.<type>.initial` ≥ 0 ; `regenerationRate` ≥ 0 ; `degradationTick` > 0 ou absent (SYNE-070).
  - dimensions `worldWidth`/`worldHeight` > 0 ; `maxTicks` > 0 ; traits dans [0, 2] ; moteur `"xoshiro256**"` exclusif.
- Une configuration invalide stoppe avec un message d'erreur explicite (code de sortie 2).

### 6.1 Clés de décision — Décision + Utilité (jalon SYNE ph3)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `agents.actions.deliberation.intervalTicks` | 10 | n°14 | Fréquence de délibération (ticks entre deux) — LOD, défaut haute |
| `agents.actions.deliberation.alignBonus` | 1.2 | n°13 | Bonus ×1.2 si l'action rejoint l'objectif courant (COGNITIVE_ARCHITECTURE §6) |
| `agents.actions.deliberation.actionSwitchMargin` | 0.05 | n°13 | Hystérésis anti-oscillation |
| `agents.actions.deliberation.conflictTieMargin` | 0.5 | n°22 | Marge de conflit de priorités → résolution probabiliste force × confiance |
| `agents.actions.interruption.enabled` | true | n°15 | Interruptions d'action actives |
| `agents.actions.interruption.utilityExcessMargin` | 10.0 | n°15 | Marge d'utilité requise pour interrompre (besoin critique) |
| `agents.actions.interruption.criticalHunger` | 85.0 | n°6 | Seuil de faim critique (COGNITIVE_ARCHITECTURE §6) |
| `agents.actions.interruption.criticalEnergy` | 10.0 | n°6 | Seuil d'énergie critique |

### 6.2 Clés d'actions déclaratives + seuils de besoins (jalon SYNE ph4)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `agents.needs.hungerTriggerThreshold` | 50 | n°4 | Déclenchement du besoin de faim (≥) |
| `agents.needs.thirstTriggerThreshold` | 50 | n°4 | Déclenchement du besoin de soif (≥) |
| `agents.needs.fatigueTriggerThreshold` | 70 | n°4 | Déclenchement du besoin de repos (>) |
| `agents.actions.catalog.<action>.movement` | false | n°4 | Action de déplacement (pas déterministe + coût d'énergie) |
| `agents.actions.catalog.<action>.energyCost` | 0.5 (mouvement) / 0 | n°4 | Coût énergétique par exécution |
| `agents.actions.catalog.<action>.energyRecovery` | 0.5 (rest) / 0 | n°4 | Énergie récupérée (ex. rest) |
| `agents.actions.catalog.<action>.fatigueRecovery` | 1.0 (rest) / 0 | n°4 | Fatigue récupérée |
| `agents.actions.catalog.<action>.hungerRecovery` | 0 | n°4 | Faim réduite (ex. eat : 30) |
| `agents.actions.catalog.<action>.thirstRecovery` | 0 | n°4 | Soif réduite (ex. drink : 30) |
| `agents.actions.catalog.<action>.reserve` | — | n°4 | Réserve globale requise/consommée (ex. eat → `food`, drink → `water`) |
| `agents.actions.catalog.<action>.reserveConsumption` | 1.0 | n°4 | Quantité consommée de la réserve par exécution |

Chaque action du catalogue doit être déclarée (liste fermée §6) ; `Eat`/`Drink` sont les
**actions terminales** résolues depuis SeekFood/SeekWater quand la réserve est disponible
(SYNE-042, DATA_MODEL.md §7).

### 6.3 Clés de communication (jalon SYNE ph5)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `communication.transmissionRange` | 55 | n°7 | Portée effective d'une pulsation (recalibrée au jalon ph6 ; bornes [1, 70] indépendantes de la perception [20, 70]) |
| `communication.relayEnabled` | true | n°10 | Relais des messages compris au-delà du rayon |
| `communication.maxHops` | 2 | n°10 | Nombre maximal de sauts avant abandon du relais |
| `communication.maxSendsPerTick` | 5 | Annexe H | Cap d'émission (envois + relais) par entité et par tick |
| `communication.maxReceivesPerTick` | 3 | Annexe H | Cap de réception traitée par tick |
| `communication.incomprehensionRate` | 0.05 | Annexe H | Probabilité d'incompréhension (tirage déterministe) |
| `communication.trustDecay` | 0.9 | n°10 | Décroissance de confiance par tick sans interaction |
| `communication.hopConfidenceDecay` | 0.9 | n°10 | Dégradation de confiance par hop (× 0.9) |
| `communication.sendEnergyCost` | 0.5 | n°9 | Coût d'émission d'une pulsation (SYNE-052) |
| `communication.sendEnergyPayloadFactor` | 0.1 | n°9 | Coût d'émission par caractère de payload |
| `communication.receiveEnergyCost` | 0.2 | n°9 | Coût de réception d'une pulsation |
| `communication.receiveEnergyPayloadFactor` | 0.05 | n°9 | Coût de réception par caractère de payload |

> `transmissionRange` défaut **55** depuis le jalon SYNE ph6 (calibration : à 20 u. les pulsations
> du scénario défaut n'atteignaient aucune entité — aucun tapis de confiance ne se formait
> (SYNE-060) ; à 55 u. la confiance réciproque émerge au voisinage percepçable). Validation en
> **bornes indépendantes** : `transmissionRange` ∈ [1, 70] et `agents.perception.radius` ∈ [20, 70]
> — la portée de pulsation peut donc dépasser le rayon de perception sans violation de config.

### 6.4 Clés de groupes — réseau social émergent (jalon SYNE ph6)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `groups.enabled` | true | n°24 | Active la révision périodique des groupes |
| `groups.reviewIntervalTicks` | 10 | — | Fréquence (LOD) de révision des composantes |
| `groups.trustThreshold` | 0.3 | n°24 | Confiance réciproque minimale d'un lien social |
| `groups.minGroupSize` | 3 | n°24 | Taille minimale d'un groupe émergent |
| `groups.sharedBeliefBonus` | 0.10 | n°24 | Bonus d'affinité par croyance partagée |
| `groups.goalAlignmentBonus` | 0.20 | n°24 | Bonus d'affinité par but partagé |
| `groups.consensusThreshold` | 0.5 | n°24 | Quorum pondéré d'une décision collective |

(`SOCIAL_NETWORK.md` §9)

### 6.5 Clés de reproduction — naissance par fusion consentie (SYNE-062)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `reproduction.enabled` | true | n°17 | Active la naissance par fusion consentie |
| `reproduction.intervalTicks` | 100 | n°17 | Fréquence de passe candidature de fusion |
| `reproduction.consentTrustThreshold` | 0.6 | n°17 | Confiance réciproque requise → consentement (couplé à CHILDREARING, jalon ph7) |
| `reproduction.maxBirthsPerTick` | 1 | n°17 | Nombre maximal de naissances par passe |

### 6.6 Clés d'héritage — mécanismes fins (SYNE-063, §6.6.3)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `agents.inheritance.salienceThreshold` | 0.01 | n°16 | Salience minimale d'un souvenir parental transmis (ex-`DefaultSalienceThreshold`) |
| (mécanismes fins dominante/mutation) | n°16 | §6.6.3 | Configurés par `InheritanceSettings` (dominance [0,1], taux de mutation ≥ 0) — défauts 0.5 / 0.01 |

### 6.7 Clés de ressources — cycle de vie (SYNE-070)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `resources.food.initial` | 100 | n°4 | Réserve initiale de nourriture (Eat : −1.0 / exécution) |
| `resources.food.regenerationRate` | 0 | n°4 | Régénération par tick |
| `resources.food.degradationTick` | 100 | n°4 | Période de dégradation (perte de `rate × période` ; inerte sans taux) |
| `resources.water.initial` | 1000 | n°4 | Réserve initiale d'eau (Drink : −1.0 / exécution) |
| `resources.water.regenerationRate` | 5 | n°4 | Régénération par tick |
| `resources.wood.initial` | 50 | n°4 | Réserve initiale de bois (consommation à la mécanique agentique des constructions — **ouverte**, §6.8) |
| `resources.wood.regenerationRate` | 0.1 | n°4 | Régénération par tick |
| `resources.mineral.initial` | 0 | n°2.4 | Réserve initiale de minéraux (4ᵉ type, SYNE-070) |
| `resources.mineral.regenerationRate` | 0 | n°2.4 | Régénération par tick |

Validation §6 : `initial` ≥ 0 ; `regenerationRate` ≥ 0 ; `degradationTick` > 0 ou absent.

### 6.8 Clés d'environnement — constructions = obstacles statiques (SYNE-071)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `world.obstacles` | `false` | n°20 | Active le monde avec obstacles (flag vivant ; appelle `ApplyConfiguredLayout` au build CLI/serveur) |
| `world.obstacleLayout[]` | `[]` | n°20 | Layout **initial** des obstacles/constructions — liste de disques |
| `world.obstacleLayout[].id` | — (requis) | n°20 | Identifiant unique de l'obstacle (`targetId` des événements, clé de retrait) |
| `world.obstacleLayout[].x` | — (requis) | n°20 | Abscisse du centre (dans `[0, worldWidth]`) |
| `world.obstacleLayout[].y` | — (requis) | n°20 | Ordonnée du centre (dans `[0, worldHeight]`) |
| `world.obstacleLayout[].radius` | 10 | n°20 | Rayon de la zone bloquante (perception/ligne de vue/mouvement, ADR-013) |

Exemple (extrait) :

```json
{
  "world": {
    "obstacles": true,
    "obstacleLayout": [
      { "id": "maison-1", "x": 100, "y": 100, "radius": 10 },
      { "id": "rocher-bas", "x": 480, "y": 490, "radius": 20 }
    ]
  }
}
```

Validation §6 : `world.obstacleLayout` rejeté si `world.obstacles` est `false` ; par entrée `id` non vide, `radius` > 0, `x`/`y` dans le monde. Mutations dynamiques (`AddObstacle`, `PlaceConstruction`/`RemoveConstruction`) validées à l'exécution (bornes + id unique) — la **mécanique agentique** d'une construction (qui, coût en bois/minéraux, durée) reste **ouverte** (décision n°20, §2.20).

### 6.9 Clés d'environnement — cycle de saisons (SYNE-072)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `world.seasons` | `{enabled: false}` | n°4 | Bloc du cycle de saisons — **l'ancien drapeau booléen homonyme, mort (jamais consommé), devient cet objet** ; `enabled` = cycle actif (modulation de la régénération + événement `world.season_changed`) |
| `world.seasons.enabled` | `false` | n°4 | Cycle actif (Saisons désactivées par défaut ⇒ trajectoire du scénario de référence inchangée) |
| `world.seasons.seasonLengthTicks` | `360` | n°4 | Durée d'une saison en ticks (≥ 1) |
| `world.seasons.initialSeason` | `spring` | n°4 | Saison du tick 0 — `spring`, `summer`, `autumn` ou `winter` |
| `world.seasons.cycle[]` | 4 saisons × facteurs ×1 | n°4 | Les 4 définitions (une par saison) : `name`, `foodFactor`, `waterFactor`, `woodFactor`, `mineralFactor` — facteur &gt; 1 = saison favorable, &lt; 1 = défavorable, appliqué à la régénération/dégradation en fin de tick (SYNE-070) |

Saison courante (fonction pure du tick, 0 tirage PRNG — DETERMINISM.md §3) :
`saison(tick) = (initialSeasonIndex + tick / seasonLengthTicks) mod 4`.

Exemple (extrait) :

```json
{
  "world": {
    "seasons": {
      "enabled": true,
      "seasonLengthTicks": 360,
      "initialSeason": "spring",
      "cycle": [
        { "name": "spring", "foodFactor": 1.0, "waterFactor": 1.0, "woodFactor": 1.0, "mineralFactor": 1.0 },
        { "name": "summer", "foodFactor": 1.0, "waterFactor": 1.2, "woodFactor": 1.0, "mineralFactor": 1.0 },
        { "name": "autumn", "foodFactor": 1.1, "waterFactor": 1.0, "woodFactor": 1.2, "mineralFactor": 1.0 },
        { "name": "winter", "foodFactor": 0.8, "waterFactor": 0.9, "woodFactor": 1.0, "mineralFactor": 1.0 }
      ]
    }
  }
}
```

Validation §6 : `seasonLengthTicks` &gt; 0 ; `initialSeason` dans {spring, summer, autumn, winter} ; `cycle` = les 4 saisons **exactement une fois** ; facteurs ≥ 0.

### 6.10 Clés d'environnement — territoires (SYNE-073)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `world.territories` | `{enabled: false}` | n°21 | Bloc du suivi de territoire — zones « points de survie » dont la **présence des entités** délimite le territoire effectif (décision n°21) ; `enabled` = suivi actif (appartenance + événement `world.territory_membership_changed` + snapshot `territories[]`) |
| `world.territories.enabled` | `false` | n°21 | Suivi actif (désactivé par défaut ⇒ trajectoire du scénario de référence inchangée) |
| `world.territories.zones[]` | `[]` | n°21 | Zones (disques « points de survie ») suivies — liste de `{id, centerX, centerY, radius}` |
| `world.territories.zones[].id` | — (requis) | n°21 | Identifiant unique de la zone (`targetId` des événements, clé du snapshot) |
| `world.territories.zones[].centerX` | — (requis) | n°21 | Abscisse du centre (dans `[0, worldWidth]`) |
| `world.territories.zones[].centerY` | — (requis) | n°21 | Ordonnée du centre (dans `[0, worldHeight]`) |
| `world.territories.zones[].radius` | `20` | n°21 | Rayon du disque (la présence dans le disque délimite le territoire effectif) |

Appartenance (0 tirage PRNG, DETERMINISM.md §3) : `entité ∈ zone ⟺ distance(entité, centre) ≤ radius`, suivie en fin de tick — `Entered`/`Left` tracés par zone (ordre de pose) et identifiant croissant, drainés `world.territory_membership_changed` (API_CONTRACTS §2.2).

Exemple (extrait) :

```json
{
  "world": {
    "territories": {
      "enabled": true,
      "zones": [
        { "id": "camp", "centerX": 250, "centerY": 250, "radius": 40 },
        { "id": "foret", "centerX": 100, "centerY": 400, "radius": 25 }
      ]
    }
  }
}
```

Validation §6 : `zones` rejetée si `world.territories.enabled` est `false` ; par zone : `id` non vide et unique, `radius` &gt; 0, `centerX`/`centerY` dans le monde. Le suivi est **purement observationnel** (aucun comportement agentique, aucune revendication) — les **sources spatiales de ressources** du territoire restent **hors V0.1** (§6.7).


### 6.11 Clés d’environnement — livres (SYNE-121)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `world.books` | `{enabled: false}` | n°18/19 | Active les livres et leurs événements/snapshot additifs |
| `world.books.enabled` | `false` | n°18/19 | Désactivé par défaut ; ne change pas le run de référence |
| `world.books.writeCostEnergy` | `20.0` | n°18 | Énergie débitée à l’auteur à l’écriture ; valeur provisoire, SYNE-120 |
| `world.books.readBenefit` | `1.0` | n°19 | Bénéfice de principe tracé à la lecture ; son effet cognitif attend le moteur mémoire |

Exemple : `{"world":{"books":{"enabled":true,"writeCostEnergy":20,"readBenefit":1}}}`.
Les coûts doivent être positifs ou nuls. En V0.1, l’écriture et la lecture sont des appels explicites de l’API du moteur ; l’accès spatial, le temps de rédaction et l’effet cognitif détaillé restent hors de ce sous-jalon.

---

## Points restés ouverts dans ce document
- Valeurs de calibration (taux de besoins, seuils) issues du prototype [HÉRITÉ] — à consolider en décisions numériques.
- Clés de configuration par espèce en V0.1 : format final à stabiliser avec le modèle de paramétrages.