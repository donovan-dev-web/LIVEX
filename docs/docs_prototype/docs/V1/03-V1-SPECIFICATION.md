# Spécification fonctionnelle V1

> Document autoritaire et unique de la V1.
> Les règles, paramètres, formats et critères de réussite y sont centralisés pour éviter toute dérive entre documents.

## 1. Population

Valeur de test initiale recommandée : 20 agents humains.

Le moteur doit permettre de modifier ce nombre sans changer la logique. La population est un paramètre, pas une constante du moteur.

## 2. Monde

Monde initial simple, dimensions configurables.

- Le monde est un plan **2D logique** : `X = horizontal`, `Y = vertical`.
- La représentation 3D (renderer) pourra réutiliser ces coordonnées en `X/Z` ; le Simulation Core reste 2D.
- Toute position est donc notée `{ "x": ..., "y": ... }`.

Contenu minimal :

- zones de terrain
- obstacles (voir §2.1)
- sources de nourriture
- sources d'eau
- agents

### 2.1 Obstacles et zones

Le monde peut contenir des obstacles bloquants (non traversables). Structure :

```text
World.obstacles = [
  { shape: "rect",   x, y, width, height, blocksMovement: true },
  { shape: "circle", x, y, radius,          blocksMovement: true }
]
```

En V1 :
- les obstacles **bloquent le déplacement** (collision simple : le pas est annulé ou glissé le long de la bordure) ;
- ils ne bloquent pas la perception (la ligne de vue sera ajoutée ultérieurement) ;
- le monde reste sinon ouvert.

## 3. Temps

Le temps simulé est indépendant du temps réel.

- Un tick représente `1 minute simulée`.
- Valeur de simulation initiale : `10 ticks / seconde réelle`.
- Donc : **1 seconde réelle = 10 minutes simulées**.
- Cette relation est modifiable par la vitesse de simulation (paramètre `targetTicksPerSecond`).

Le jour/nuit est optionnel en V1.

### 3.1 Ordre causal du tick

Chaque tick suit exactement cet ordre (contrat de simulation) :

```text
1.  Avancer le temps
2.  Mettre à jour l'environnement
3.  Mettre à jour la physiologie
4.  Mettre à jour la perception
5.  Mettre à jour la mémoire
6.  Calculer les besoins
7.  Évaluer les décisions
8.  Mettre à jour les actions
9.  Appliquer les effets au monde
10. Générer les événements
11. Valider l'état
```

## 4. Ressources

### 4.1 FoodSource

```text
Id            : string   (ex: "food-12")
Type          : "food"
Position      : { x, y }
Quantity      : number
MaxQuantity   : number
RegenerationRate : number  (0 en V1)
```

Valeurs initiales V1 : `Quantity = 100`, `MaxQuantity = 100`, `RegenerationRate = 0`.
Les ressources ne se régénèrent donc pas dans la première expérience.

### 4.2 WaterSource

```text
Id            : string   (ex: "water-3")
Type          : "water"
Position      : { x, y }
Capacity      : number
```

Une source d'eau V1 est considérée comme inépuisable : le paramètre global `waterInfinite = true` rend `Capacity` non limitant. Le champ `Capacity` est conservé pour une éventuelle future limite.

## 5. Agent humain

### 5.1 Identité

```text
Id      : string   (ex: "agent-42")
Name    : string   (optionnel)
Species : string   ("human")
Age     : number   (optionnel)
```

Les identifiants sont des chaînes de caractères dans tout le moteur (état, événements, sauvegarde) afin de rester robustes aux espèces et préfixes futurs.

### 5.2 État

```text
Health   : 0..100
Energy   : 0..100
Hunger   : 0..100
Thirst   : 0..100
Position : { x, y }
Inventory: { food: int, water: int }   (minimal, pour Gather/Eat)
```

Interprétation :

- 0 Hunger = pas faim ; 100 Hunger = faim critique
- 0 Thirst = pas soif ; 100 Thirst = soif critique
- 0 Energy = épuisement ; 100 Energy = énergie maximale
- 0 Health = mort

### 5.3 Traits

```text
Aggression  : 0..1
Sociability : 0..1
```

Les traits modifient les scores de décision. Ils ne doivent pas directement exécuter des actions.

Distribution V1 recommandée : `aggression ~ random[0.2, 0.8]`, `sociability ~ random[0.2, 0.8]` (contrôlée par la seed).

## 6. Évolution biologique (physiologie)

Toutes les variables sont bornées entre 0 et 100.

À chaque tick :

```text
Hunger += 0.10
Thirst += 0.15
Energy -= 0.05
```

### 6.1 Conséquences d'une action

```text
Eat réussie   : Hunger -= 35   (et FoodSource.Quantity -= 1)
Drink réussie : Thirst -= 50
Rest          : Energy += 0.50 / tick
```

Les résultats sont bornés (Hunger/Thirst ≥ 0, Energy ≤ 100).

### 6.2 Dégradation

Lorsque les besoins deviennent critiques :

```text
Hunger >= 90     -> Health -= 0.10 / tick
Thirst >= 90     -> Health -= 0.20 / tick
Energy <= 5      -> Health -= 0.02 / tick
```

### 6.3 Mort

```text
Health <= 0  -> Dead
```

Après la mort, l'agent ne prend plus de décisions et n'exécute plus d'action. Il reste dans le monde jusqu'à traitement explicite de sa dépouille dans une version future.

### 6.4 Principe

La biologie ne choisit jamais directement une action. Elle modifie l'état, ce qui influence indirectement les scores de décision.

## 7. Besoins normalisés

Les besoins utilisés par le système de décision sont normalisés entre 0 et 1 :

```text
FoodNeed   = Hunger / 100
WaterNeed  = Thirst / 100
RestNeed   = 1 - Energy / 100
SafetyNeed = max(threatComponent, lowHealthComponent)   (voir §7.1)
```

`SafetyNeed` est **fonctionnel en V1** et déclenche l'action Flee.

### 7.1 Calcul de SafetyNeed

Paramètre `dangerRange` (par défaut = `perceptionRange`).

```text
threatComponent = max over perceived alive agents i of:
    aggression_i * (1 - distance_i / dangerRange)        (0 si aucun)

lowHealthComponent = 1 - Health / 100

SafetyNeed = clamp( max(threatComponent, 0.5 * lowHealthComponent), 0, 1 )
```

Sans danger perçu et avec la santé pleine, `SafetyNeed = 0`.

## 8. Perception

### 8.1 Rayon

```text
PerceptionRange = 30 m
```

### 8.2 Entités perceptibles

- autres agents vivants
- FoodSource
- WaterSource
- éléments environnementaux pertinents

### 8.3 Information perçue (Observation)

```text
EntityId
EntityType
Distance
Direction
Position { x, y }
ObservableProperties
TickObserved
```

Un agent ne reçoit pas automatiquement les informations internes des autres agents.

## 9. Mémoire

La mémoire conserve, par entité connue :

```text
EntityId
LastKnownPosition { x, y }
LastSeenTick
Confidence
```

La confiance diminue avec le temps selon `confidence -= confidenceDecayPerTick` (paramètre, borné ≥ 0). Une information mémorisée peut donc devenir obsolète. Cela permettra plus tard de simuler l'incertitude et les erreurs.

## 10. Capacités et actions

Une capacité définit ce qu'un agent peut faire ; une action est l'intention exécutable choisie. La V1 expose **9 capacités**. Les 5 premières forment le socle minimal ; les 4 suivantes sont également disponibles en V1.

```text
MoveTo, Eat, Drink, Rest, Explore, Gather, Talk, Attack, Flee
```

Une action peut avoir les états : `Pending`, `Executing`, `Completed`, `Cancelled`, `Failed`.

### 10.1 MoveTo

Déplacer l'agent vers une destination (position ou entité cible).

- Préconditions : agent vivant ; destination valide.
- Fin : distance à la cible ≤ interaction threshold.
- Échec : destination invalide ; cible supprimée ; agent mort.

### 10.2 Eat

Consommer de la nourriture.

- Préconditions : agent vivant ; cible FoodSource ; distance ≤ `InteractionRange` ; `Quantity > 0`.
- Effets : `FoodSource.Quantity -= 1`, `Hunger -= 35`.
- Durée : 1 tick.

### 10.3 Drink

- Préconditions : agent vivant ; cible WaterSource ; distance ≤ `InteractionRange`.
- Effets : `Thirst -= 50`.
- Durée : 1 tick.

### 10.4 Rest

Récupérer de l'énergie.

- Préconditions : agent vivant.
- Effets : `Energy += 0.50 / tick`.
- Continue jusqu'à énergie suffisante, interruption ou changement de contexte.

### 10.5 Explore

Déplacer l'agent vers une zone inconnue (destination aléatoire valide autour de l'agent). Sert la découverte de ressources.

- Préconditions : agent vivant.

### 10.6 Gather

Collecter une ressource pour la stocker dans l'inventaire minimal de l'agent.

- Préconditions : agent vivant ; cible FoodSource ; distance ≤ `InteractionRange` ; `Quantity > 0`.
- Effets : `FoodSource.Quantity -= 1`, `Inventory.food += 1`.
- `Eat` peut consommer depuis l'inventaire (`Inventory.food > 0`) ou directement depuis une source à portée.
- Durée : 1 tick.

### 10.7 Talk

Créer/renforcer un lien social avec un agent perçu à portée.

- Préconditions : agent vivant ; cible agent vivant ; distance ≤ `InteractionRange`.
- Effets : `bond += talkGain * (1 - bond)` (stocké en mémoire sociale, `bond` 0..1), et influence `SocialUrge` (voir §12.8).
- Durée : 1 tick.

### 10.8 Attack

Modifier l'état d'un autre agent selon les règles de combat minimales V1.

- Préconditions : agent vivant ; cible agent vivant ; distance ≤ `InteractionRange`.
- Effets : `target.Health -= attackBase * Aggression` par tick d'exécution (la cible peut à son tour fuir ou riposter).
- La décision Attack est favorisée par un trait `Aggression` élevé et un contexte de compétition de ressources.

### 10.9 Flee

Chercher une position plus sûre (s'éloigner de la source de danger perçue).

- Préconditions : agent vivant ; `SafetyNeed` élevé (danger perçu).
- La décision Flee est favorisée par un `SafetyNeed` élevé et un trait `Aggression` faible de l'agent fuyant.

### 10.10 Interruptions

Une action peut être annulée si :

- l'agent meurt ;
- la cible devient invalide ;
- une précondition critique disparaît ;
- une nouvelle décision remplace l'action (ex. danger perçu → Flee annule MoveTo).

### 10.11 Émergence

Les actions sont génériques. Il n'existe pas `FormVillage()`, `HuntAsGroup()`, `CreateTradeRoute()`. Ces phénomènes pourront éventuellement apparaître plus tard à partir des actions élémentaires et des interactions.

## 11. Déplacement

- `WalkSpeed = 0.25 m/s` (soit **15 m par tick** : inférieur à `perceptionRange` = 30 m, évite les angles morts de perception et permet une arrivée précise à `InteractionRange`).
- Un agent peut se déplacer vers une position ou une entité cible.
- Le pathfinding complexe est hors périmètre V1.
- `InteractionRange = 2 m` : distance minimale pour consommer/interagir.

## 12. Système de décision (Utility AI)

### 12.1 Approche

À chaque tick, l'agent :

```text
1. récupère son contexte
2. construit les actions candidates
3. élimine les actions invalides
4. calcule un score
5. sélectionne l'action au meilleur score
6. démarre ou conserve l'action
```

### 12.2 Contexte

`State`, `Needs`, `Perception`, `Memory`, `Traits`, `Capabilities`.

### 12.3 Score

Conceptuellement :

```text
Score =
    NeedUtil
    * DistanceUtil
    * AvailUtil
    * DangerUtil
    * CostUtil
    * TraitUtil
```

Tous les facteurs sont normalisés entre 0 et 1 et définis de façon déterministe en §12.8.

### 12.4 Exemples par action

- **Eat** : dépend de `FoodNeed`, `Distance`, `FoodAvailability` (nourriture proche préférée).
- **Drink** : dépend de `WaterNeed`, `Distance`, `WaterAvailability`.
- **Rest** : dépend de `RestNeed` (plus l'énergie est basse, plus Rest devient intéressant).
- **Explore** : intéressant quand aucun besoin urgent n'est présent et aucune cible intéressante n'est disponible.
- **Flee** : devient dominante quand `SafetyNeed` (danger perçu) est élevé.
- **Attack** : score accru par `Aggression` élevé en contexte de compétition.
- **Talk / Gather** : scores modulés par `Sociability` et disponibilité de cible.

### 12.5 Sélection et hystérésis

L'action avec le score maximal est sélectionnée. En cas d'égalité, le moteur utilise la randomisation contrôlée par la seed.

Une action en cours peut être conservée si elle reste valide et qu'aucune nouvelle action ne la dépasse suffisamment (marge `actionSwitchMargin`, paramètre). Il n'existe pas de priorité absolue codée du type `if hunger then eat` : les besoins influencent les scores.

### 12.6 Explicabilité

Chaque décision produit un `DecisionRecord` :

```text
AgentId, Tick, CandidateActions, Scores, SelectedAction
```

Exemple :

```text
Eat       0.84
Drink     0.31
Rest      0.12
Explore   0.08

Selected = Eat
```

### 12.7 Fréquence

`DecisionInterval = 1 tick` (réévaluée chaque minute simulée).

### 12.8 Formules de référence (déterministes)

Pour chaque action candidate `a`, `Score(a)` est le produit des facteurs suivants, tous bornés 0..1 :

```text
Score(a) =
    NeedUtil(a)
  * DistanceUtil(a)
  * AvailUtil(a)
  * DangerUtil(a)
  * CostUtil(a)
  * TraitUtil(a)
```

Définitions :

- **NeedUtil(a)** : besoin adressé par l'action
  - Eat    → FoodNeed
  - Drink  → WaterNeed
  - Rest   → RestNeed
  - Flee   → SafetyNeed
  - Attack → CombatUrge = Aggression * ResourceScarcity
                            (ResourceScarcity = clamp(1 - nourriture_dispo / besoin_pop, 0, 1))
  - Talk   → SocialUrge = Sociability * (1 - ageDernierSocial)
  - Gather → StockUrge  = 0.3 + 0.7 * min(FoodNeed, WaterNeed)
  - Explore→ ExploreUrge = 1 - max(FoodNeed, WaterNeed, RestNeed, SafetyNeed)
  - MoveTo → 0 (utilité portée par l'action cible vers laquelle il se dirige ; voir DistanceUtil)

- **DistanceUtil(a)** : `1 - clamp(distance / perceptionRange, 0, 1)` pour les actions à cible ; `1` pour Rest/Explore (pas de cible externe).

- **AvailUtil(a)** : Eat → `clamp(food.quantity / eatBatch, 0, 1)` (eatBatch = 1) ; Drink → 1 (eau infinie) ; autres → 1.

- **DangerUtil(a)** : Flee → SafetyNeed ; sinon → `1 - 0.5 * SafetyNeed`.

- **CostUtil(a)** (tableau) : MoveTo=1.0, Eat=1.0, Drink=1.0, Rest=1.1, Explore=1.0, Gather=0.9, Talk=0.95, Attack=0.8, Flee=0.7.

- **TraitUtil(a)** :
  - Attack → `0.5 + 0.5 * Aggression`
  - Talk   → `0.5 + 0.5 * Sociability`
  - Flee   → `0.5 + 0.5 * (1 - Aggression)`
  - autres → 1.0

Le score est ainsi déterministe pour un état et une seed donnés. Ces formules sont des références de tuning, ajustables via les paramètres.

## 13. Aléatoire et reproductibilité

Toutes les opérations aléatoires utilisent la seed de la simulation. Le moteur ne doit pas utiliser de sources aléatoires non contrôlées dans le cœur de la simulation.

**PRNG imposé** : la V1 utilise **xoshiro256\*\*** (ensemencé via splitmix64). `System.Random` est interdit car non garanti stable entre versions .NET. L'état complet (256 bits) du générateur doit être sérialisé dans les sauvegardes (`simulation.rngState`) pour permettre une reprise exacte.

La V1 doit être reproductible avec une même seed et les mêmes paramètres, y compris l'état du RNG.

## 14. Persistance

Une sauvegarde décrit uniquement l'état logique de la simulation (aucune donnée de renderer). Elle doit permettre de restaurer :

- temps simulé ;
- seed (et état du RNG) ;
- état du monde ;
- agents (état, traits, mémoire, action en cours) ;
- ressources ;
- configuration ;
- version du schéma.

### 14.1 Format

```json
{
  "schemaVersion": 1,
  "simulationVersion": "0.1.0",
  "simulation": {
    "seed": 12345,
    "tick": 1250,
    "simulatedTimeMinutes": 1250,
    "rngState": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
  },
  "world": { "width": 500, "height": 500 },
  "resources": [],
  "agents": []
}
```

### 14.2 Agent

```json
{
  "id": "agent-42",
  "species": "human",
  "name": "Agent 42",
  "age": 27,
  "state": {
    "health": 87.2,
    "energy": 63.4,
    "hunger": 71.8,
    "thirst": 30.2,
    "position": { "x": 42.4, "y": 18.7 }
  },
  "traits": { "aggression": 0.42, "sociability": 0.71 },
  "inventory": { "food": 0, "water": 0 },
  "memory": [],
  "currentAction": null
}
```

### 14.3 Resource

```json
{
  "id": "food-12",
  "type": "food",
  "position": { "x": 80, "y": 120 },
  "quantity": 54,
  "maxQuantity": 100,
  "regenerationRate": 0
}
```

```json
{
  "id": "water-3",
  "type": "water",
  "position": { "x": 100, "y": 200 },
  "capacity": 1000
}
```

### 14.4 Mémoire

```json
{
  "entityId": "water-3",
  "lastKnownPosition": { "x": 100, "y": 200 },
  "lastSeenTick": 980,
  "confidence": 0.64
}
```

### 14.5 Action en cours

```json
{
  "type": "MoveTo",
  "targetEntityId": "food-12",
  "state": "Executing",
  "startTick": 1240
}
```

### 14.6 Versioning

Toute modification incompatible doit augmenter `schemaVersion`. Le moteur doit refuser ou migrer explicitement les sauvegardes incompatibles.

## 15. Principes anti-script

Il est interdit en V1 d'ajouter des règles du type :

```text
At tick 1000 -> create event
At day 5 -> agents gather
If population > X -> create village
```

Les phénomènes collectifs doivent émerger des règles locales.

## 16. Paramètres (valeurs par défaut V1)

Ces valeurs sont externalisées dans la configuration (`config.json`) et ne doivent pas être codées en dur.

| Groupe | Paramètre | Valeur | Unité |
|---|---:|---:|---|
| World | width | 500 | m |
| World | height | 500 | m |
| Population | initialAgents | 20 | agents |
| Population | initialFoodSources | 10 | sources |
| Population | initialWaterSources | 5 | sources |
| Simulation | simulatedMinutesPerTick | 1 | minute |
| Simulation | targetTicksPerSecond | 10 | tick/s |
| Simulation | decisionIntervalTicks | 1 | tick |
| Simulation | seed | configurable | - |
| Agent | initialHealth | 100 | - |
| Agent | initialEnergy | 80 | - |
| Agent | initialHunger | 20 | - |
| Agent | initialThirst | 20 | - |
| Agent | perceptionRange | 30 | m |
| Agent | interactionRange | 2 | m |
| Agent | walkSpeed | 0.25 | m/s |
| Agent | dangerRange | 30 | m |
| Physiology | hungerIncreasePerTick | +0.10 | /tick |
| Physiology | thirstIncreasePerTick | +0.15 | /tick |
| Physiology | energyDecreasePerTick | -0.05 | /tick |
| Physiology | restEnergyGainPerTick | +0.50 | /tick |
| Physiology | eatHungerReduction | -35 | - |
| Physiology | drinkThirstReduction | -50 | - |
| Damage | hungerDamageThreshold | 90 | - |
| Damage | hungerDamagePerTick | 0.10 | /tick |
| Damage | thirstDamageThreshold | 90 | - |
| Damage | thirstDamagePerTick | 0.20 | /tick |
| Damage | exhaustionThreshold | 5 | - |
| Damage | exhaustionDamagePerTick | 0.02 | /tick |
| Resources | foodInitialQuantity | 100 | - |
| Resources | foodMaxQuantity | 100 | - |
| Resources | foodRegenerationRate | 0 | - |
| Resources | waterInfinite | true | - |
| Decision | decisionIntervalTicks | 1 | tick |
| Decision | actionSwitchMargin | 0.05 | - |
| Decision | dangerRange | 30 | m |
| Decision | attackBase | 5 | health/tick |
| Decision | talkGain | 0.10 | - |
| Memory | confidenceDecayPerTick | 0.01 | /tick |

### 16.1 Exemple de configuration

```json
{
  "simulation": {
    "seed": 12345,
    "simulatedMinutesPerTick": 1,
    "targetTicksPerSecond": 10
  },
  "world": { "width": 500, "height": 500 },
  "population": {
    "initialAgents": 20,
    "initialFoodSources": 10,
    "initialWaterSources": 5
  },
  "agent": {
    "health": 100,
    "energy": 80,
    "hunger": 20,
    "thirst": 20,
    "perceptionRange": 30,
    "interactionRange": 2,
    "walkSpeed": 0.25,
    "dangerRange": 30
  },
  "biology": {
    "hungerIncreasePerTick": 0.10,
    "thirstIncreasePerTick": 0.15,
    "energyDecreasePerTick": 0.05,
    "restEnergyGainPerTick": 0.50,
    "eatHungerReduction": 35,
    "drinkThirstReduction": 50,
    "hungerDamageThreshold": 90,
    "hungerDamagePerTick": 0.10,
    "thirstDamageThreshold": 90,
    "thirstDamagePerTick": 0.20,
    "exhaustionThreshold": 5,
    "exhaustionDamagePerTick": 0.02
  },
  "resources": {
    "foodInitialQuantity": 100,
    "foodMaxQuantity": 100,
    "foodRegenerationRate": 0,
    "waterInfinite": true
  },
  "decision": {
    "decisionIntervalTicks": 1,
    "actionSwitchMargin": 0.05,
    "dangerRange": 30,
    "attackBase": 5,
    "talkGain": 0.10
  },
  "memory": {
    "confidenceDecayPerTick": 0.01
  }
}
```

### 16.2 Séparation configuration / état

```text
config.json  = comment le monde fonctionne (règles)
save.json    = ce qui est arrivé au monde (état)
```

La seed appartient à la configuration d'une expérience et doit être enregistrée dans les sauvegardes.

### 16.3 Philosophie du tuning

Ne pas chercher les valeurs « réalistes » dès la première version. Chercher des valeurs compréhensibles, observables, suffisamment rapides pour produire des événements, faciles à modifier et reproductibles.

## 17. Critères de réussite V1

La V1 est considérée fonctionnelle si :

1. Des agents survivent sans script narratif.
2. Ils recherchent de la nourriture lorsqu'ils ont faim.
3. Ils recherchent de l'eau lorsqu'ils ont soif.
4. Ils se reposent lorsqu'ils sont fatigués.
5. Ils peuvent explorer.
6. Les ressources sont consommées.
7. Les agents peuvent mourir.
8. Une simulation peut être sauvegardée et reprise.
9. Une même seed permet une reproduction raisonnable de l'expérience.
10. Les décisions et événements sont observables (DecisionRecord, événements).
