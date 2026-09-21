# CONFIGURATION.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 21 septembre 2026
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
               "safetyDriftRate": 0.001, "socialDriftRate": 0.001, "curiosityDriftRate": 0.002 },
    "perception": { "radius": 50, "confidenceFalloff": 0.3, "rotationInterval": 4, "lineOfSight": true },
    "memory": { "maxCapacity": 1000, "recallThreshold": 0.01,
                "observationDecayRate": 0.01, "eventDecayRate": 0.005, "interactionDecayRate": 0.002 },
    "beliefs": { "updateStrength": 0.3, "maxChangePerSnap": 0.5, "alignBonus": 0.2,
                 "conflictPenalty": 0.1, "expiryTicks": 100, "expiredCap": 0.4, "timeDecayPerTick": 0.999 },
    "actions": { "moveEnergyCost": 0.5, "restEnergyGain": 0.5, "restFatigueRecovery": 1.0 }
  },
  "resources": {
    "food": { "initial": 100, "regenerationRate": 0, "degradationTick": 100 },
    "water": { "initial": 1000, "regenerationRate": 5 },
    "wood": { "initial": 50, "regenerationRate": 0.1 }
  },
  "communication": {
    "maxSendsPerTick": 5,
    "maxReceivesPerTick": 3,
    "incomprehensionRate": 0.05,
    "trustDecay": 0.9
  },
  "world": { "seasons": false, "events": false, "obstacles": false },
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
  - dimensions `worldWidth`/`worldHeight` > 0 ; `maxTicks` > 0 ; traits dans [0, 2] ; moteur `"xoshiro256**"` exclusif.
- Une configuration invalide stoppe avec un message d'erreur explicite (code de sortie 2).

---

## Points restés ouverts dans ce document
- Valeurs de calibration (taux de besoins, seuils) issues du prototype [HÉRITÉ] — à consolider en décisions numériques.
- Clés de configuration par espèce en V0.1 : format final à stabiliser avec le modèle de paramétrages.