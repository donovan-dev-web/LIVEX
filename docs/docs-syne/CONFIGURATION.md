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
    "simulatedSecondsPerTick": 60,
    "autoSaveEveryNTicks": 1000,
    "maxBackups": 5
  },
  "agents": {
    "initialCount": 100,
    "traits": { "bravery": 1.0, "curiosity": 1.0, "sociability": 1.0, "greed": 1.0,
                 "pessimism": 1.0, "aggressiveness": 1.0, "strength": 1.0, "speed": 1.0 },
    "needs": { "hungerRate": 0.5, "thirstRate": 0.7, "fatigueRate": 0.3,
               "safetyDriftRate": 0.001, "socialDriftRate": 0.0002, "curiosityDriftRate": 0.0005,
               "hungerTriggerThreshold": 50, "thirstTriggerThreshold": 50, "fatigueTriggerThreshold": 70 },
    "perception": { "radius": 50, "confidenceFalloff": 0.3, "rotationInterval": 4, "lineOfSight": true },
    "memory": { "maxCapacity": 1000, "recallThreshold": 0.01,
                "observationDecayRate": 0.01, "eventDecayRate": 0.005, "interactionDecayRate": 0.002 },
    "beliefs": { "updateStrength": 0.3, "maxChangePerSnap": 0.5, "alignBonus": 0.2,
                 "conflictPenalty": 0.1, "expiryTicks": 100, "expiredCap": 0.4, "timeDecayPerTick": 0.999 },
    "actions": { "moveEnergyCost": 0.03, "restEnergyGain": 1.5, "restFatigueRecovery": 2.0,
                 "deliberation": { "intervalTicks": 10, "alignBonus": 1.2,
                                   "actionSwitchMargin": 0.05, "conflictTieMargin": 0.5 },
                 "interruption": { "enabled": true, "utilityExcessMargin": 10.0,
                                   "criticalHunger": 85.0, "criticalEnergy": 10.0 },
                 "catalog": {
                   "idle": { },
                   "seekFood": { "movement": true },
                   "seekWater": { "movement": true },
                   "eat": { "energyCost": 0.2, "hungerRecovery": 30.0, "reserve": "food", "energyRecovery": 2.0 },
                   "drink": { "energyCost": 0.2, "thirstRecovery": 30.0, "reserve": "water", "energyRecovery": 1.0 },
                   "rest": { },
                   "flee": { "movement": true },
                   "socialize": { "movement": true },
                   "explore": { "movement": true }
                 } }
  },
  "resources": {
    "food": { "initial": 20000, "regenerationRate": 20 },
    "water": { "initial": 20000, "regenerationRate": 10 },
    "wood": { "initial": 50, "regenerationRate": 0.1 },
    "mineral": { "initial": 0, "regenerationRate": 0 }
  },
  "communication": {
    "transmissionRange": 55,
    "relayEnabled": false,
    "maxHops": 2,
    "maxSendsPerTick": 1,
    "maxReceivesPerTick": 1,
    "incomprehensionRate": 0.05,
    "trustDecay": 0.9,
    "hopConfidenceDecay": 0.9,
    "sendEnergyCost": 0,
    "sendEnergyPayloadFactor": 0,
    "receiveEnergyCost": 0,
    "receiveEnergyPayloadFactor": 0
  },
  "world": { "metersPerUnit": 1.0, "seasons": { "enabled": false }, "territories": { "enabled": false }, "books": { "enabled": false, "writeCostEnergy": 20.0, "readBenefit": 1.0 }, "events": false, "obstacles": false },
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
| `--serve` | bool | Active l'API HTTP de contrôle et la diffusion WebSocket du run |
| `--serve-port 5181` | int | Port HTTP de contrôle (défaut : 5181, bind `127.0.0.1`) |
| `--observe` | bool | Active l'émission WebSocket (SYNE-080, API_CONTRACTS §2) |
| `--observe-port 5180` | int | Port du serveur WebSocket (défaut : 5180, bind 127.0.0.1) |

(Annexe H.2) Ces ports sont des valeurs par défaut configurables, non des
constantes d'intégration. `--serve` active aussi le flux WebSocket ; utiliser
`--observe-port` pour le configurer. Ces options concernent le moteur .NET :
le mock séparé `syne-mock` dispose de sa propre configuration.

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
  - `actions.catalog` complet : une entrée **obligatoire** pour chaque action (`idle`, `seekFood`, `seekWater`, `eat`, `drink`, `rest`, `flee`, `socialize`, `explore`, `take`, `give`, `trade`, `attack`, `defend` — les 5 primitives atomiques D7 depuis engineVersion 0.14.0) — échec déclaratif si une clé manque.
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
| `agents.actions.catalog.<action>.energyCost` | 0.03 (mouvement) / 0 | n°4, ADR-016 | Coût énergétique par exécution (défaut mouvement = `agents.actions.moveEnergyCost`) |
| `agents.actions.catalog.<action>.energyRecovery` | 1.5 (rest) / **2.0 (eat)** / **1.0 (drink)** / 0 | n°4, ADR-016 | Énergie récupérée — levier de survie D1/B1 : manger/boire rapporte plus qu'il ne coûte |
| `agents.actions.catalog.<action>.fatigueRecovery` | 2.0 (rest) / 0 | n°4, ADR-016 | Fatigue récupérée |
| `agents.actions.catalog.<action>.hungerRecovery` | 0 | n°4 | Faim réduite (ex. eat : 30) |
| `agents.actions.catalog.<action>.thirstRecovery` | 0 | n°4 | Soif réduite (ex. drink : 30) |
| `agents.actions.catalog.<action>.reserve` | — | n°4 | Réserve globale requise/consommée (ex. eat → `food`, drink → `water`) |
| `agents.actions.catalog.<action>.reserveConsumption` | 1.0 | n°4 | Quantité consommée de la réserve par exécution |

Chaque action du catalogue doit être déclarée (liste fermée §6) ; `Eat`/`Drink` sont les
**actions terminales** résolues depuis SeekFood/SeekWater quand la réserve est disponible
(SYNE-042, DATA_MODEL.md §7). Le catalogue inclut depuis engineVersion 0.14.0 les
primitives atomiques D7 (`take`, `give`, `trade`, `attack`, `defend`) — déclarées pour
la complétude (liste fermée), inertes tant que les drapeaux ci-dessous sont éteints.

**Nouvelles clés du jalon ADR cognitifs (engineVersion 0.14.0 — ADR acceptés du
30/09/2026, tous désactivés par défaut : trajectoire de référence et checksums dorés
inchangés)** :

| Clé | Défaut | ADR | Rôle |
| :-- | :-- | :-- | :-- |
| `agents.actions.plans.enabled` | false | D3 | Bibliothèque de plans candidats par objectif (candidats Take/Trade ajoutés à la délibération) |
| `agents.actions.inventory.enabled` | false | D8 | Inventaire des entités (poids/slots) — les primitives Take/Give/Trade opèrent dessus |
| `agents.actions.inventory.capacityWeight` | 20.0 | D8 | Capacité de poids par entité (`capacitePoids` de l'ADR) |
| `agents.actions.inventory.takeAmount` | 5.0 | D7/D8 | Quantité par primitive Prendre (réserve → inventaire) |
| `agents.actions.inventory.giveAmount` | 1.0 | D7 | Quantité par primitive Donner (inventaire → pair) |
| `agents.actions.inventory.tradeAmount` | 1.0 | D7 | Quantité par côté de l'échange fixe 1 Food ↔ 1 Water |
| `agents.actions.salience.enabled` | false | D2 | Contrôle de saillance (étape 3bis) : entre deux délibérations, poursuite de l'intention sauf saillance ou filet de sécurité |
| `agents.actions.salience.reconsiderThreshold` | 1.0 | D2 | Seuil de score déclenchant la délibération complète |
| `agents.actions.salience.needThresholdWeight` | 1.0 | D2 | Poids d'un franchissement de seuil de besoin depuis la dernière délibération |
| `agents.actions.salience.interruptionConditionWeight` | 2.0 | D2 | Poids d'une condition d'interruption remplie (réservé, la condition critique force déjà SEUIL_MAX) |
| `agents.actions.salience.forcedReconsiderationTicks` | 50 | D2 | Filet de sécurité : reconsidération forcée tous les N ticks sans saillance (0 = désactivé) |
| `agents.actions.commitments.enabled` | false | D5 | Engagements communicationnels : Request/Response → `Commitment` → objectif candidat pondéré par la confiance |
| `agents.actions.commitments.expiryTicks` | 100 | D5 | Durée de validité : non honoré au-delà → Broken (pénalité de confiance) |
| `agents.trust.commitmentBonus` | 0.10 | D5 | Bonus de confiance d'un engagement tenu (`Fulfilled`) |
| `agents.trust.commitmentPenalty` | 0.15 | D5 | Pénalité de confiance d'un engagement rompu/expiré — distincte du mensonge factuel (`liePenalty`) |

**Profil de référence recalé (calibration D1, engineVersion 0.13.0 — ADR-015)** :
`configs/simulation/reference.json` et `SimulationProfiles.Reference()` portent
`catalog.eat.energyRecovery = 2.0` et `catalog.drink.energyRecovery = 1.0` —
manger/boire compense le coût métabolique du déplacement vers la ressource (sans
cela : mort lente, énergie moyenne 69 → 49 entre t800 et t1200). Le bénéfice
Eat/Drink de la formule d'utilité est déplafonné (`min(need, 100) × 0.6`, plafond
60, monotone). Les défauts intégrés (`ActionCatalogSettings`) restaient inchangés
à ce jalon : la calibration ne vivait que dans le profil de référence.

**Calibration B1 des défauts (engineVersion 0.15.0 — ADR-016)** : les valeurs
intégrant les défauts **eux-mêmes**, pour qu'un run lancé sans surcouche (dont le
chemin Launcher `--simulation reference`, qui ne fait que pousser
`agents.initialCount` + `ticksPerSecond`) soit viable à horizon 2500 ticks :

| Levier | Avant (0.14.0) | Après (0.15.0) | Pourquoi |
| :-- | :-- | :-- | :-- |
| `agents.actions.moveEnergyCost` | 0,5 | **0,03** | mouvement = 75 à 98 % des ticks en régime établi : à 0,5 (puis 0,05 en D1) son coût dépasse le revenu amorti de Eat/Drink |
| `agents.needs.socialDriftRate` | 0,001 | **0,0002** | besoin **sans mécanisme de satisfaction** : franchissait 0,7 au tick ~700 et déclenchait Socialize en permanence (96 % des décisions en fin de run) |
| `agents.needs.curiosityDriftRate` | 0,002 | **0,0005** | idem (seuil 0,3 franchi au tick ~600 au lieu de ~150) |
| `resources.food` | 100, régén 0, dégrad. 100 | **20 000, régén 20, pas de dégradation** | la dégradation annulait la régénération en fin de période : réserve réduite à sa valeur initiale, épuisée avant t2000 à 100 agents |
| `resources.water` | 1 000, régén 5 | **20 000, régén 10** | idem sans dégradation |
| coûts de communication | 0,5 / 0,2 (+payload), relais, 5/3 | **0, relais coupé, 1/1** | ~0,12 énergie/tick d'envoi, soit 4× le coût de déplacement recalibré |
| `catalog.eat/drink.energyRecovery` | 0 / 0 | **2,0 / 1,0** | hérité de D1 (déjà porté par le profil de référence) |
| `restEnergyGain` / `restFatigueRecovery` | 0,5 / 1 | **1,5 / 2** | hérité de D1 |

`configs/simulation/reference.json` rejoue explicitement les mêmes valeurs (le
chemin HTTP `config ?? ReferenceJson()` ne doit pas dépendre d'un défaut qu'on
oublierait de recaler) ; `configs/simulation/raw.json` reste le **jalon
historique non calibré**, qui reproduit l'ancien comportement — il s'éteint aux
alentours du tick 300, c'est mesuré et documenté (ADR-016).

### 6.3 Clés de communication (jalon SYNE ph5)

| Clé | Défaut | Décision | Rôle |
| :-- | :-- | :-- | :-- |
| `communication.transmissionRange` | 55 | n°7 | Portée effective d'une pulsation (recalibrée au jalon ph6 ; bornes [1, 70] indépendantes de la perception [20, 70]) |
| `communication.relayEnabled` | false (B1) | n°10, ADR-016 | Relais des messages compris au-delà du rayon — coupé en B1 avec les coûts énergétiques |
| `communication.maxHops` | 2 | n°10 | Nombre maximal de sauts avant abandon du relais |
| `communication.maxSendsPerTick` | 1 | Annexe H, ADR-016 | Cap d'émission (envois + relais) par entité et par tick |
| `communication.maxReceivesPerTick` | 1 | Annexe H, ADR-016 | Cap de réception traitée par tick |
| `communication.incomprehensionRate` | 0.05 | Annexe H | Probabilité d'incompréhension (tirage déterministe) |
| `communication.trustDecay` | 0.9 | n°10 | Décroissance de confiance par tick sans interaction |
| `communication.hopConfidenceDecay` | 0.9 | n°10 | Dégradation de confiance par hop (× 0.9) |
| `communication.sendEnergyCost` | 0 (B1) | n°9, ADR-016 | Coût d'émission d'une pulsation (SYNE-052) — **neutre** depuis B1 : à ~0,2 envoi/agent/tick l'envoi coûtait ~0,12 énergie/tick, soit 4× le coût de déplacement recalibré |
| `communication.sendEnergyPayloadFactor` | 0 (B1) | n°9, ADR-016 | Coût d'émission par caractère de payload |
| `communication.receiveEnergyCost` | 0 (B1) | n°9, ADR-016 | Coût de réception d'une pulsation |
| `communication.receiveEnergyPayloadFactor` | 0 (B1) | n°9, ADR-016 | Coût de réception par caractère de payload |

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
| `resources.food.initial` | 20 000 | n°4, ADR-016 | Réserve initiale de nourriture (Eat : −1.0 / exécution) |
| `resources.food.regenerationRate` | 20 | n°4, ADR-016 | Régénération par tick (**apport net**) |
| `resources.food.degradationTick` | absent (inerte) | ADR-016 | Période de dégradation (perte de `rate × période`) — **neutralisée** en B1 : avec une dégradation de période, la régénération est intégralement annulée en fin de période et la réserve se réduit à sa valeur initiale |
| `resources.water.initial` | 20 000 | n°4, ADR-016 | Réserve initiale d'eau (Drink : −1.0 / exécution) |
| `resources.water.regenerationRate` | 10 | n°4, ADR-016 | Régénération par tick |
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

### 6.12 Clés d’échelle temporelle (ADR-017)

| Clé | Défaut | Plage validée | Rôle |
| :-- | :-- | :-- | :-- |
| `simulation.simulatedSecondsPerTick` | `60` | `[1, 3600]` (entier) | Secondes simulées par tick — 60 : 1 tick = 1 minute simulée (ADR-005, comportement historique **inchangé**) ; 5 : profil `prism` (R = 30 à 6 TPS) |
| `world.metersPerUnit` | `1.0` | `> 0` | **Informatif** : mètres par unité SYNE — jamais calculé par le moteur, transmis aux clients (`world_initialized`, PRISM k = 100 uu/unité) |

Règle de conversion (ADR-017, inventaire complet `TICK_QUANTITIES.md`) : `dt = simulatedSecondsPerTick / 60` est appliqué **une fois au démarrage du run** — les quantités de **classe A** (taux de besoins, énergie mouvement/repos, régénérations/dégradation des réserves, décroissances mémoire/croyances/confiance, durée des saisons) sont exprimées **par minute simulée** dans la config et mises à l'échelle en valeurs par tick effectif ; les classes B (cadences en ticks) et C (effets par action) sont inchangées. Le ratio temps réel est `R = ticksPerSecond × simulatedSecondsPerTick` — l'UI doit l'afficher (ADR-005).

Profils enregistrés (`SimulationProfiles`) :

| Profil | Fichier | Monde | TPS | spt | R |
| :-- | :-- | :-- | :-- | :-- | :-- |
| `reference` | `configs/simulation/reference.json` | 500 × 500, cellule 10 | 10 | 60 | 600 (historique : 1 tick = 1 min) |
| `prism` | `configs/simulation/prism.json` | 2 240 × 2 240, cellule 32 | 6 | **5** | **30** |

Exemple : `{"simulation":{"simulatedSecondsPerTick":5}}`.
Validation : valeur entière dans `[1, 3600]` (0, négatif, > 3600 et non entier rejetés) ; `world.metersPerUnit` strictement positif et fini.

Activation du profil `prism` : `--simulation prism` (scénario batch accepté à côté de `reference` ; le profil s'applique **sous** `--config`, priorité §5 — `reference`/aucun scénario reste le défaut intégré, inchangé) ; côté Launcher, le champ « Scénario » de la campagne accepte `prism`.

---

## Points restés ouverts dans ce document
- Valeurs de calibration (taux de besoins, seuils) issues du prototype [HÉRITÉ] — à consolider en décisions numériques.
- Clés de configuration par espèce en V0.1 : format final à stabiliser avec le modèle de paramétrages.
Le monde exporté vers Unreal est discrétisé avec `simulation.worldCellSize`
(défaut `10`). Cette valeur est déterministe et produit `cellCountX/Y` par
arrondi supérieur; elle est incluse dans `world_initialized`.
`world_initialized.world.agents[]` contient les identifiants, espèces et
positions 2D initiales créés par le même processus déterministe que le moteur
réutilise au démarrage. SYNE n'expose pas de types de terrain configurables :
les cellules exportées utilisent pour l'instant `terrainType: "plains"`.
L'aménagement environnemental configurable se limite aux obstacles
(`world.obstacles` / `world.obstacleLayout`) et aux territoires.
`POST /api/control/prepare` peut surcharger la cadence de cette préparation
avec `ticksPerSecond` (entier strictement positif). La valeur est incluse dans
`world_initialized.world.ticksPerSecond` et réutilisée par `start`.
Depuis ADR-017 (contrat description 1.1), `world_initialized.world` inclut
aussi `simulatedSecondsPerTick` et `metersPerUnit` (§6.12).