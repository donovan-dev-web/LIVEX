# Paramètres de la Simulation (V1)

Référence exhaustive de tous les paramètres de la simulation, classés par catégorie.
Pour chaque paramètre : valeur par défaut, type, rôle, et justification du choix.

Légende :
- **[Config]** : paramètre éditable via `SimulationConfig` (JSON, `SimulationConfig.Load`).
- **[Interne]** : constante codée en dur dans le moteur (pas exposée en configuration V1).

L'ordre d'exécution d'un tick (ordre causal, spec §3.1) est :
`Temps → Environnement → Physiologie → Perception → Mémoire → Besoins → Décision → Actions → Monde → Événements → Validation`.

---

## 1. Simulation (général)

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Simulation.Seed` **[Config]** | `12345` | int | Graine du PRNG `xoshiro256**`. | Valeur fixe arbitraire ; garantit la reproductibilité run-to-run et la reprise exacte (`RngState`). |
| `Simulation.SimulatedMinutesPerTick` **[Config]** | `1` | double | Durée de temps simulée représentée par 1 tick. | 1 tick = 1 minute : convertit vitesses/débits en temps. Sert au calcul de la vitesse de déplacement (`WalkSpeed × 60`). |
| `Simulation.TargetTicksPerSecond` **[Config]** | `10` | double | Cadence temps réel cible du serveur de transport (`WebSocketServer`). `0` = pleine vitesse (headless/benchmark). | 10 tps donne une animation fluide (~6 ticks/s réels observés) sans saturer le réseau. |

---

## 2. Monde / Environnement / Topologie

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `World.Width` **[Config]** | `500` | double | Largeur du monde (unités = « mètres »). | Assez grand pour 20 agents + 15 sources avec une portée de perception de 30, sans saturation. |
| `World.Height` **[Config]** | `500` | double | Hauteur du monde. | Idem. |
| Bords du monde **[Interne]** | — | — | Les agents sont **clampés** aux bords (`World.ClampToWorld`) : le monde n'est pas torique. | Évite la téléportation ; simplifie la topologie V1. |
| Obstacles (`RectObstacle`, `CircleObstacle`) **[Config/Interne]** | aucun par défaut | — | Formes bloquant la locomotion (`BlocksMovement`). | Permettent de modéliser des murs ; non peuplés par défaut. |
| `WaterSource.Capacity` **[Interne]** | `1000` | double | Capacité d'une source d'eau (fixe). | Valeur élevée : l'eau est considérée inépuisable (`Resources.WaterInfinite`). |

---

## 3. Population & Ressources

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Population.InitialAgents` **[Config]** | `20` | int | Nombre d'agents à la création du monde. | Taille de population modeste, stable sur le long terme (biologie permissive). |
| `Population.InitialFoodSources` **[Config]** | `10` | int | Nombre de sources de nourriture. | ~1 source pour 2 agents : pression de ressource légère mais réelle. |
| `Population.InitialWaterSources` **[Config]** | `5` | int | Nombre de sources d'eau. | Moins fréquentes que la nourriture (mais eau inépuisable). |
| `Resources.FoodInitialQuantity` **[Config]** | `100` | double | Quantité initiale d'une source de nourriture. | Source tient ~3 bouchées avant épuisement partiel (voir `EatHungerReduction`). |
| `Resources.FoodMaxQuantity` **[Config]** | `100` | double | Quantité maximale (plafond de régénération). | Égal à l'initial : pas de croissance au-delà. |
| `Resources.FoodRegenerationRate` **[Config]** | `0` | double | Régénération par tick d'une source de nourriture. | `0` par défaut : les sources sont non renouvelables (épuisement possible → rareté émergente). |
| `Resources.WaterInfinite` **[Config]** | `true` | bool | L'eau est inépuisable. | On élimine la contrainte d'eau pour isoler d'abord la dynamique faim/énergie. |
| `Resources.FoodKnownFromStart` **[Config]** | `true` | bool | Toutes les sources de nourriture sont connues d'emblée. | Les agents vont directement se nourrir (pas de cache-cache) ; met l'accent sur les besoins, pas sur l'exploration. |
| `Resources.WaterKnownFromStart` **[Config]** | `true` | bool | Idem pour l'eau. | Idem. |

---

## 4. Agents — État initial

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Agent.Health` **[Config]** | `100` | double | Santé initiale (0–100, `Alive` si > 0). | Pleine santé au départ. |
| `Agent.Energy` **[Config]** | `80` | double | Énergie initiale (0–100). | Légèrement sous le max : le repos devient pertinent tôt. |
| `Agent.Hunger` **[Config]** | `20` | double | Faim initiale (0–100). | Besoin modéré au départ. |
| `Agent.Thirst` **[Config]** | `20` | double | Soif initiale (0–100). | Idem. |
| `Position` **[Interne]** | aléatoire | Vector2 | Position de spawn dans le monde. | Répartition uniforme. |
| `Age` **[Interne]** | `18–60` | int | `Rng.NextInt(43) + 18`. | Tranche d'âge « adulte » plausible ; non utilisée par la logique V1 (réservée). |
| `Aggression` **[Interne]** | `0.2–0.8` | double | `Rng.NextRange(0.2, 0.8)`. Pilote l'attaque, la menace perçue et la fuite. | Répartie : certains agents pacifiques, d'autres dangereux → émergence de conflits. |
| `Sociability` **[Interne]** | `0.2–0.8` | double | `Rng.NextRange(0.2, 0.8)`. Pilote l'attrait de `Talk` (liens sociaux). | Répartie : grappes sociales émergentes possibles. |
| `FoodInventory` / `WaterInventory` **[Interne]** | `0` | int | Réserve transportée (remplie par `Gather`). | Permet de manger sans se déplacer vers une source. |

> **Traits non implémentés en V1** : la spécification évoquait vitesse, force, curiosité, prudence, métabolisme. Seuls `Aggression` et `Sociability` existent dans le modèle (`Model.cs`) ; les autres sont réservés.

---

## 5. Agents — Capacités & Locomotion

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Agent.PerceptionRange` **[Config]** | `30` | double | Rayon d'observation (agents, ressources). | = taille de cellule de la `SpatialGrid` (Phase 7) : la grille partitionne exactement la portée. |
| `Agent.InteractionRange` **[Config]** | `2` | double | Distance à laquelle une action ciblée s'exécute (manger, boire, cueillir, parler, attaquer). | Proximité physique requise ; sinon l'agent se déplace d'abord (locomotion automatique). |
| `Agent.WalkSpeed` **[Config]** | `0.25` | double | Vitesse de base (unités par **minute simulée**). | Combiné à `SimulatedMinutesPerTick=1` → pas de déplacement = `0.25 × 60 = 15` u/tick. |
| `Agent.DangerRange` **[Config]** | `30` | double | Portée d'évaluation de la menace (besoin de sécurité, fuite). | Égale à la perception : un agent agressif n'est une menace que s'il est proche. |
| `Agent.DistanceScale` **[Config]** | `150` | double | Échelle de décroissance douce de l'attractivité avec la distance (`1/(1+d/150)`). | Bien supérieur au monde (500) : les ressources lointaines restent attractives (pas d'effet coupure brutal). |

> `Décision.DangerRange` (voir §9) est **réservé** et non utilisé ; c'est `Agent.DangerRange` qui est utilisé par `NeedSystem` et `Flee`.

---

## 6. Agents — Besoins (NeedSystem)

Les besoins normalisés (0–1) sont recalculés chaque tick à partir de l'état brut :

| Besoin | Formule **[Interne]** | Rôle |
|---|---|---|
| `FoodNeed` | `clamp01(Hunger / 100)` | Urgence de manger. |
| `WaterNeed` | `clamp01(Thirst / 100)` | Urgence de boire. |
| `RestNeed` | `clamp01(1 - Energy / 100)` | Urgence de se reposer. |
| `SafetyNeed` | `max(menace, 0.5 × (1 - Health/100))` clampé | Urgence de fuir. `menace = max(0, Aggression_autrui - 0.5) × (1 - distance/DangerRange)` sur les agents perçus. |

Justification : un agent n'est une menace que s'il est **nettement agressif** (`> 0.5`) et proche ; la santé basse contribue aussi au besoin de sécurité.

---

## 7. Biologie / Physiologie (PhysiologySystem)

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Biology.HungerIncreasePerTick` **[Config]** | `0.10` | double | Faim gagnée par tick. | Monte de 0→90 en ~700 ticks (≈12 min sim) : pression lente et gérable. |
| `Biology.ThirstIncreasePerTick` **[Config]** | `0.15` | double | Soif gagnée par tick. | Légèrement plus vite que la faim. |
| `Biology.EnergyDecreasePerTick` **[Config]** | `0.05` | double | Énergie perdue par tick (métabolisme de base). | Lente : le repos compense vite. |
| `Biology.RestEnergyGainPerTick` **[Config]** | `0.50` | double | Énergie regagnée par tick de repos. | ×10 la perte : se reposer est efficace (incite au comportement « pause »). |
| `Biology.EatHungerReduction` **[Config]** | `35` | double | Faim réduite par bouchée. | Une bouchée calme fortement la faim (35/100). |
| `Biology.DrinkThirstReduction` **[Config]** | `50` | double | Soif réduite par gorgée. | ⚠️ Valeur de config **existante** mais le code `ActionSystem.Drink` utilise le littéral `50` (cohérent par défaut). |
| `Biology.HungerDamageThreshold` **[Config]** | `90` | double | Faim au-delà de laquelle la santé baisse. | Marge de sécurité avant dégât. |
| `Biology.HungerDamagePerTick` **[Config]** | `0.10` | double | Dégât de santé par tick si faim > seuil. | Lent : la mort par faim prend des dizaines de ticks après le seuil. |
| `Biology.ThirstDamageThreshold` **[Config]** | `90` | double | Soif au-delà de laquelle la santé baisse. | Idem faim. |
| `Biology.ThirstDamagePerTick` **[Config]** | `0.20` | double | Dégât de soif par tick. | ×2 la faim : la soif est plus dangereuse. |
| `Biology.ExhaustionThreshold` **[Config]** | `5` | double | Énergie en dessous de laquelle la santé baisse. | Épuisement sévère. |
| `Biology.ExhaustionDamagePerTick` **[Config]** | `0.02` | double | Dégât d'épuisement par tick. | Très lent. |

**Biologie permissive** : avec les défauts, la population reste à ~100 % d'agents vivants sur des milliers de ticks (la nourriture est abondante et l'eau infinie). Pour obtenir des décès, il faut réduire les sources (`InitialFoodSources`) ou activer la régénération nulle couplée à une forte population.

---

## 8. Mémoire (MemorySystem)

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Memory.ConfidenceDecayPerTick` **[Config]** | `0.01` | double | Décroissance de la confiance d'un souvenir par tick. | Perte de mémoire progressive ; un souvenir est oublié quand `Confidence ≤ 0.0001`. |
| Confiance initiale **[Interne]** | `1.0` | double | Confiance à la première vue d'une entité. | Vue = certitude totale. |

---

## 9. Décision / Utility AI (DecisionSystem)

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Decision.DecisionIntervalTicks` **[Config]** | `1` | int | Intervalle entre deux réévaluations. | **Réservé** : `Decide` est appelé à chaque tick dans `Step()`. |
| `Decision.ActionSwitchMargin` **[Config]** | `0.05` | double | Hystérésis : on garde l'action en cours si `score ≥ max × (1 - 0.05)`. | Évite l'oscillation et permet d'atteindre la cible avant de changer d'avis. |
| `Decision.DangerRange` **[Config]** | `30` | double | Portée danger (décision). | **Réservé** (non utilisé ; voir `Agent.DangerRange`). |
| `Decision.AttackBase` **[Config]** | `5` | double | Dégâts de base d'une attaque. | Multiplié par `Aggression` → `5 × Aggression` dégâts par attaque. |
| `Decision.TalkGain` **[Config]** | `0.10` | double | Gain de lien social par interaction `Talk`. | `Bonds[id] += 0.10 × (1 - lien_actuel)` : convergence asymptotique vers 1. |

### Fonction de score **[Interne]**

`score = clamp01(need) × distance × avail × danger × cost × trait`

| Sous-score | Formule | Justification |
|---|---|---|
| `NeedUtil` | dépend du type (ex. `Eat`→`FoodNeed`, `Rest`→`RestNeed`) | Le besoin pertinent pilote l'action. `Attack` utilise la rareté `scarcity = clamp01(1 - totalFood/(pop×35)) × Aggression`. |
| `DistanceUtil` | `1 / (1 + dist / DistanceScale)` | Attrait décroissant doucement avec la distance (pas de coupure). |
| `AvailUtil` | `clamp01(quantity / 20)` pour manger/cueillir | Une source vide n'est pas attractive. |
| `DangerUtil` | `Flee → SafetyNeed` ; sinon `1 - 0.5 × SafetyNeed` | La sécurité domine en fuite, réduit les autres actions si menace. |
| `CostUtil` | `Rest 1.1`, `Eat/Drink/MoveTo/Explore 1.0`, `Gather 0.9`, `Talk 0.95`, `Attack 0.8`, `Flee 0.7` | Coût relatif « utilitariste » des actions. |
| `TraitUtil` | `Attack 0.5+0.5×Aggression`, `Talk 0.5+0.5×Sociability`, `Flee 0.5+0.5×(1-Aggression)`, sinon `1` | Les traits modulent l'attrait des actions sociales/combat. |

**Modulation de survie** (anti-spirale mortelle) **[Interne]** : si `survivalNeed > 0.6`, toute action non (manger/boire) voit son score ×`0.02` ; si `> 0.35`, ×`0.05`. Une attaque est « défensive » si `SafetyNeed > 0.4`.

**Tie-break** : en cas d'égalité des scores (`eps = 1e-9`), un candidat est choisi aléatoirement (`Rng`) → diversité comportementale.

---

## 10. Actions (ActionSystem)

| Action | Effet **[Interne]** | Justification |
|---|---|---|
| Déplacement (`step`) | `WalkSpeed × SimulatedMinutesPerTick × 60 = 15` u/tick. | Vitesse dérivée (voir §5). |
| `Eat` | `food.Quantity -= 1` ; `Hunger -= EatHungerReduction (35)`. | Une bouchée consomme la source et calme la faim. |
| `Drink` | `Thirst -= 50` (littéral). | Gorgée forte ; eau inépuisable. |
| `Rest` | `Energy += RestEnergyGainPerTick (0.5)`. | Récupération. |
| `Gather` | `food.Quantity -= 1` ; `FoodInventory++`. | Stockage pour manger plus tard sans source. |
| `Talk` | `Bonds[id] += TalkGain × (1 - lien)` ; renforce `KnownAgents`. | Tisse des liens sociaux (émergence de groupes). |
| `Attack` | `target.Health -= AttackBase × Aggression (5 × Aggression)`. | Dégâts proportionnels à l'agression ; mort si santé ≤ 0. |
| `Explore` | Destination aléatoire à `dist ∈ [5, PerceptionRange×2 = 60]`. | Découverte ; utile quand les besoins sont bas et tout est connu. |
| `Flee` | S'éloigne de la menace la plus proche (`Aggression × (1 - d/DangerRange)`). | Survie face aux agents agressifs. |
| Portée d'exécution | `InteractionRange (2)` pour `Eat/Drink/Gather/Talk/Attack` : sinon l'agent se déplace d'abord. | Proximité requise pour l'effet. |

---

## 11. Perception (PerceptionSystem)

| Paramètre | Défaut | Rôle | Justification |
|---|---|---|---|
| Portée | `PerceptionRange (30)` | Rayon d'observation des agents et ressources. | Au-delà, l'entité n'est pas perçue (mais peut rester en mémoire). |
| Connaissance | ajout automatique à `KnownFood` / `KnownWater` / `KnownAgents` | Une entité vue devient une cible de décision possible. | Permet d'agir même après disparition (via mémoire). |
| Ligne de vue | **aucune** en V1 | Tout ce qui est dans le rayon est vu (murs non pris en compte pour la perception). | Simplification V1. |

---

## 12. Événements & Transport

| Paramètre | Défaut | Type | Rôle | Justification |
|---|---|---|---|---|
| `Events.EmitDecisions` **[Config]** | `true` | bool | Émet `DecisionMadeEvent` (avec `DecisionRecord`). | Observabilité / Web UI ; lourd en masse (allocation). Désactivé pour le benchmark. |
| `Events.EmitActions` **[Config]** | `true` | bool | Émet les événements d'action (Started/Moved/Completed/Failed). | Idem. |
| `EventBus` capacité **[Interne]** | `500 000` | int | Taille du ring buffer d'événements. | Absorbe un run massif sans perte (vidé par le transport à chaque tick). |
| `WebSocketServer.broadcastEvery` **[Interne]** | `1` | int | Diffuse 1 snapshot tous les N ticks. | `1` = temps réel complet ; le sous-échantillonnage côté analyzer est séparé (`?every=`). |

---

## 13. Persistance

| Paramètre | Défaut | Rôle | Justification |
|---|---|---|---|
| `SaveFile.SimulationVersion` **[Interne]** | `"0.1.0"` | Version de schéma de sauvegarde. | `SimulationConfig.Load` / `Save` rejettent une `schemaVersion` inconnue (reprise exacte garantie). |
| `RngState` | — | État complet PRNG (4 × ulong) | Permet la reprise bit à bit de la simulation. |

---

## 14. Validation (fin de tick)

| Contrainte **[Interne]** | Rôle |
|---|---|
| `Health` clampée à `[0, 100]` | Borne physiologique. |
| `Position` clampée au monde | Empêche de sortir du plateau. |

> **Reproduction / spawn dynamique** : **non implémenté en V1**. La population est fixée à l'initialisation (`InitialAgents`) ; il n'y a pas de naissances ni de morts-ressuscitations. Les seuls changements de population viennent des décès (`Health ≤ 0`). C'est une piste Phase 10+/équilibrage.

---

## Résumé des valeurs par défaut (config `SimulationConfig`)

> Le fichier sérialisé utilise les **noms C# PascalCase** (ceux-ci, attendus par
> `SimulationConfig.Load` via `JsonSerializer` par défaut). Un exemple complet et
> chargeable se trouve dans `docs/V1/15-EXEMPLE-CONFIG.json`.

```jsonc
{
  "simulation": { "seed": 12345, "simulatedMinutesPerTick": 1, "targetTicksPerSecond": 10 },
  "world": { "width": 500, "height": 500 },
  "population": { "initialAgents": 20, "initialFoodSources": 10, "initialWaterSources": 5 },
  "agent": {
    "health": 100, "energy": 80, "hunger": 20, "thirst": 20,
    "perceptionRange": 30, "interactionRange": 2, "walkSpeed": 0.25,
    "dangerRange": 30, "distanceScale": 150
  },
  "biology": {
    "hungerIncreasePerTick": 0.10, "thirstIncreasePerTick": 0.15, "energyDecreasePerTick": 0.05,
    "restEnergyGainPerTick": 0.50, "eatHungerReduction": 35, "drinkThirstReduction": 50,
    "hungerDamageThreshold": 90, "hungerDamagePerTick": 0.10,
    "thirstDamageThreshold": 90, "thirstDamagePerTick": 0.20,
    "exhaustionThreshold": 5, "exhaustionDamagePerTick": 0.02
  },
  "resources": {
    "foodInitialQuantity": 100, "foodMaxQuantity": 100, "foodRegenerationRate": 0,
    "waterInfinite": true, "foodKnownFromStart": true, "waterKnownFromStart": true
  },
  "decision": {
    "decisionIntervalTicks": 1, "actionSwitchMargin": 0.05, "dangerRange": 30,
    "attackBase": 5, "talkGain": 0.10
  },
  "memory": { "confidenceDecayPerTick": 0.01 },
  "events": { "emitDecisions": true, "emitActions": true }
}
```

> Constantes internes non configurables (récap) : `WaterSource.Capacity=1000`, `Age∈[18,60]`, `Aggression∈[0.2,0.8]`, `Sociability∈[0.2,0.8]`, pas de déplacement = `15` u/tick, `Eat` retire `1` de quantité, `Drink` retire `50` de soif, `availUtil` seuil `20`, `scarcity` pop×`35`, `EventBus` capacité `500 000`, seuil d'oubli mémoire `0.0001`, `eps` tie-break `1e-9`.
