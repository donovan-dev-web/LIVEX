# SCALE_AND_CADENCE_SPEC — Échelle spatiale et cadence SYNE ↔ Unreal

**Composant** : PRISM
**Statut** : [Proposed] — décision formalisée par [`adr/ADR-003-echelle-cadence-syne-unreal.md`](adr/ADR-003-echelle-cadence-syne-unreal.md)
**Dernière mise à jour** : 7 octobre 2026
**Dépend de** : [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md), [`RENDERING_SPEC.md`](RENDERING_SPEC.md), [`SCENE_SPEC.md`](SCENE_SPEC.md), [`../docs-syne/adr/ADR-005-temporalite.md`](../docs-syne/adr/ADR-005-temporalite.md), [`../docs-syne/adr/ADR-016-calibration-stabilite-2500-ticks.md`](../docs-syne/adr/ADR-016-calibration-stabilite-2500-ticks.md)

---

## 1. Objet

PRISM doit représenter le monde SYNE en **temps de jeu perceptuellement continu** :
le joueur et les agents évoluent à la même vitesse apparente, dans un monde
d'environ **5 km²** (forêt, rivière, montagne, bord de mer) accueillant
**50 à 100 agents au lancement, 500 agents visés**.

Ce document fixe :

1. l'**échelle** `k` — combien d'Unreal Units (uu) vaut une unité de position SYNE ;
2. la **cadence** `simulation.ticksPerSecond` (TPS) ;
3. la **taille du monde** et son découpage `simulation.worldCellSize` ;
4. les conséquences temporelles, sociales et la configuration Unreal associées.

Contrainte directrice : **la vitesse des agents doit égaler la vitesse du joueur**,
donc s'appuyer sur les valeurs par défaut d'Unreal plutôt que les changer.
Toute valeur de ce document est soit vérifiée dans le code (« Source »), soit
explicitement signalée comme valeur par défaut du moteur Unreal (à confirmer sur
UE 5.8 lors du branchement).

---

## 2. Ce que le code impose déjà (repères vérifiés)

| Repère | Valeur | Source |
| :-- | :-- | :-- |
| Unité de temps | **1 tick = 1 minute simulée** | `syne/Simulation.Core/Simulation/SimulationLoop.cs:409` (`TicksPerSimulationMinute = 1`), [`ADR-005`](../docs-syne/adr/ADR-005-temporalite.md) |
| Cadence configurée | entier strictement > 0, défaut **10** | `SimulationOptions.cs:27` ; validation `Simulation.Console/Control/ControlServer.cs:269-271` ; `SimulationOptionsValidator.cs:40-42` |
| Exécution de la cadence | `TimeSpan.FromSeconds(1d/TicksPerSecond)` puis `Task.Delay` (aucun rattrapage de retard) | `SimulationController.cs:272`, `:519` |
| Pas de déplacement | 1 pas par tick d'action, borné par le trait `speed` ∈ [0,5 ; 1,5] (défaut 1,0) | `SimulationOptions.cs:44` ; `EntityTemplate.cs:44-47` ; clamp des traits à [0 ; 2] `TraitSet.cs:9-11` |
| Monde par défaut | `worldWidth`/`worldHeight` = **500**, `worldCellSize` = **10** | `SimulationOptions.cs:22-25` (validateur : tout `> 0`, `SimulationOptionsValidator.cs:15-23` et `:491-493`) |
| Population par défaut | `agents.initialCount` = **100** | `SimulationOptions.cs:34` |
| Perception | rayon défaut **50**, **borné à [20 ; 70]** (décision n°6), rotation par groupes de 4 ticks | `SimulationOptions.cs:99` et `:102` ; `SimulationOptionsValidator.cs:58-64` |
| Décision | délibération tous les **10 ticks** | `SimulationOptions.cs:254` |
| Saisons | **360 ticks** par saison | `SimulationOptions.cs:533` |
| Durée d'action | **aucune** : toute action se résout en un tick | `ActionEntrySettings` (`SimulationOptions.cs:224-249`), `ActionExecutor.Execute` |
| Nourriture/eau | Eat/Drink prélèvent sur des **réserves globales**, sans exigence de proximité | `ActionExecutor.cs:94-107` |
| Énergie du mouvement | coût **forfaitaire par tick de déplacement** (0,03), sans dépendance à la distance | `ActionExecutor.cs:115-122` ; `SimulationOptions.cs:172` |
| Cibles de mouvement | aléatoires et **locales** : rayon ≤ `perception.radius × 0,5` (≤ 35 unités au rayon max) | `ActionExecutor.cs:496` |
| A* | cellule 10 unités, plafonné à **4096 cellules** développées | `SimulationOptions.cs:810` et `:813` |
| Export de la grille | une seule fois, dans la trame `world_initialized` (`cellSize`, `ticksPerSecond`, `cells[]`, `regions[]`, `resources[]`) | `SimulationController.cs:174` et `:189` ; record `WorldDescription` (`WorldDescription.cs:4-12`) |
| Export par tick | `world.snapshot` : `agents[]` avec `position`, `traits` (dont `speed`), besoins, `currentAction`, `currentIntention` | `ObservabilitySerializer.cs:16-50` et `:284-345` |
| Budget de tick | 0,13 ms (50 agents) → 0,98 ms (1000 agents) mesurés | [`../docs-syne/PERFORMANCE.md:144-148`](../docs-syne/PERFORMANCE.md) |
| Calibration courante | horizon **2500 ticks**, 50/100 agents, monde **500×500** par défaut | [`ADR-016`](../docs-syne/adr/ADR-016-calibration-stabilite-2500-ticks.md) |
| Joueur | **absent de SYNE** : routes `prepare`/`ready`/`start`/`pause`/`resume`/`stop`/`reset` uniquement | `ControlServer.cs:205-233` |
| Defaults Unreal | `MaxWalkSpeed` = **600 uu/s**, World Partition `CellSize` = **3200 uu**, `1 uu` = **1 cm** | moteur Unreal (valeurs par défaut, à confirmer sur UE 5.8) |

Deux conséquences découlent immédiatement de cette table :

- **La vitesse apparente est le produit** `k × TPS × speed` (en uu/s). Réglée sur
  le joueur, elle fixe `k` et `TPS` ensemble (§3 et §4).
- **La taille du monde ne change pas l'économie du moteur** : les réserves sont
  globales, le coût de mouvement est forfaitaire, les cibles sont locales. Elle
  change en revanche la densité sociale (§6) et le chargement Unreal (§8).

---

## 3. Échelle spatiale retenue : `k = 100 uu / unité SYNE`

### 3.1 Formule générale

```
k = taille_tuile_WorldPartition / worldCellSize      [uu par unité SYNE]
```

Cette définition est celle du guide d'intégration (`k = taille_tuile / CellSize`,
`PRISM_UNREAL_IMPLEMENTATION.md:221-224` → `UnrealX = SyneX × k`). Le profil
retenu l'instancie avec `k = 3200 / 32 = 100`.

### 3.2 Contrainte de vitesse

Pour qu'un acteur Unreal rattrape son état autoritaire SYNE à chaque tick :

```
MaxWalkSpeed ≥ k × speed_max × TPS
```

avec `speed_max = 1,5` (borne haute du trait). Les défauts Unreal imposent
`MaxWalkSpeed = 600 uu/s` pour le joueur. Pour que **joueur et agents aient la
même vitesse apparente sans toucher aux défauts UE** :

```
600 = 100 × 1,0 × 6      →   k = 100 uu/unité   et   TPS = 6
```

### 3.3 Retenue : `k = 100` — **1 unité SYNE = 1 mètre**

`100 uu = 100 cm = 1 m` (UE : `1 uu = 1 cm`). Tout le vocabulaire SYNE devient
métrique : rayon de perception 70 = 70 m, obstacles en mètres, régions en mètres.

| Donnée SYNE | En unités SYNE (`k = 100`) | En Unreal |
| :-- | :-- | :-- |
| Position continue (`agents[].position.x/y`) | `double` | × 100 → uu (ex. `12.5` → `1250` uu = 12,5 m) |
| Case logique (`cells[].x/y`) | 32 unités (profil retenu) | tuile de **3200 uu** = 32 m = cellule WP par défaut |
| Centre d'une case | `x × 32 + 16` | `x × 3200 + 1600` uu |
| Rayon d'obstacle (`obstacles[].radius`) | unités | × 100 |
| Rayon de perception (max code) | 70 | 7000 uu = 70 m |
| Régions (`regions[]`, 10 cases) | 320 unités | 32 000 uu = 320 m |
| Marqueur de ressource | index de case | centre de la tuile |
| Pas par tick d'action (trait 0,5–1,5) | 0,5 à 1,5 unité | 50 à 150 uu/tick |
| Rayon des cibles de déplacement | ≤ 35 unités | ≤ 3500 uu |

---

## 4. Cadence retenue : `simulation.ticksPerSecond = 6`

Avec `k = 100` : `vitesse_apparente = 100 × TPS × speed` uu/s, soit
`100 × 6 = 600 uu/s` pour un agent médian — **exactement le `MaxWalkSpeed`
par défaut d'Unreal**.

| TPS | ms/tick | Frames @60 FPS/tick | Vitesse apparente (trait 1,0) | Journée (1440 ticks) |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1000 | 60 | 100 uu/s (1 m/s) | 24 min |
| 5 | 200 | 12 | 500 uu/s (5 m/s) | 4 min 48 |
| **6 (retenu)** | **166,7** | **10** | **600 uu/s (6 m/s)** | **240 s (4 min)** |
| 10 (défaut SYNE) | 100 | 6 | 1000 uu/s (10 m/s) | 144 s |
| 15 | 66,7 | 4 | 1500 uu/s | 96 s |
| 30 | 33,3 | 2 | 3000 uu/s | 48 s |
| 60 | 16,7 | 1 | 6000 uu/s | 24 s |

Contraintes croisées :

- **Parité joueur/agent** : `MaxWalkSpeed ≥ k × 1,5 × TPS`. À `TPS 6`, l'agent
  le plus rapide exige **900 uu/s** — d'où la règle par agent du §8.5.
- **Résolution de rendu** : `TPS ≤ FPS` (10 frames/tick à 60 FPS, large marge).
- **Budget de tick** : 166,7 ms par tick contre 0,13 ms mesurés (50 agents) et
  0,98 ms (1000 agents) — marge ×170 à ×1300 (`PERFORMANCE.md:144-148`).
- **Entier** : `TicksPerSecond` est un entier strictement positif
  (`ControlServer.cs:269-271`) ; aucun réglage continu n'est possible.

### 4.1 Temps simulé à `TPS = 6` (temps réel = ticks ÷ 6)

| Événement | Ticks | Temps réel |
| :-- | ---: | ---: |
| Granularité d'une décision | 1 | **166,7 ms** (10 frames) |
| Rotation de perception (`SimulationOptions.cs:102`) | 4 | 0,67 s |
| Délibération (`SimulationOptions.cs:254`) | 10 | **1,67 s** |
| Action du joueur (lecture UI) | 1 | 166,7 ms |
| Saison (`SimulationOptions.cs:533`) | 360 | 60 s |
| Autosave (défaut 1000) | 1000 | 167 s |
| Journée complète | 1440 | **240 s** |
| Horizon calibré ADR-016 | 2500 | **417 s (6 min 57)** |

### 4.2 Besoins vus en temps réel (à vide, depuis 0)

Taux `SimulationOptions.cs:65-67`, seuils `:88`, `:90`, `:93` :

| Besoin | Seuil de déclenchement | Ticks | Temps réel @6 |
| :-- | ---: | ---: | ---: |
| Faim | 50 ÷ 0,5 | 100 | **16,7 s** |
| Soif | 50 ÷ 0,7 | 71 | **11,9 s** |
| Fatigue | 70 ÷ 0,3 | 233 | **38,9 s** |

L'horloge tourne donc à **360× le temps réel** (1440 minutes ÷ 240 s). Ce rythme
est volontaire (« survival en temps de jeu continu ») mais doit être affiché dans
l'UI (`RENDERING_SPEC.md`) : une journée passe toutes les 4 minutes.

---

## 5. Profils de monde

`worldWidth`/`worldHeight`/`worldCellSize` sont **configurables** sans changer
de code (`SimulationOptionsValidator.cs:15-23`, `:491-493`). Règle d'alignement :
`worldCellSize × k` doit être la taille de la cellule World Partition, et
`worldWidth` doit être divisible par `worldCellSize` (sinon `ceil` produit une
dernière rangée partielle, `WorldDescription.cs:49-50`).

| Profil | `worldWidth/Height` | `worldCellSize` | Cases SYNE | Côté réel | Surface | Cellule WP | Cellules WP | Poids `cells[]` |
| :-- | ---: | ---: | :-- | ---: | ---: | ---: | ---: | ---: |
| A — défaut calibré (ADR-016) | 500 | 10 | 50 × 50 = 2 500 | 500 m | 0,25 km² | 1 000 uu | 2 500 | ≈ 0,3 Mo |
| **B — retenu (≈ 5 km²)** | **2 240** | **32** | **70 × 70 = 4 900** | **2 240 m** | **5,02 km²** | **3 200 uu (défaut UE)** | **4 900** | **≈ 0,5 Mo** |
| C — grand (option) | 5 000 | 40 | 125 × 125 = 15 625 | 5 000 m | 25 km² | 4 000 uu | 15 625 | ≈ 1,7 Mo |

**Profil B retenu** : côté 2240 m = 70 cases de 32 m ; `32 × 100 = 3200 uu` =
**cellule World Partition par défaut** (aucun réglage UE), région = 10 cases =
320 m → 49 régions (`WorldDescription.cs:72-77`), ressources = 4 types ×
3 marqueurs (`WorldDescription.cs:35`, `:68`).

Le poids `cells[]` est un ordre de grandeur (~110 octets/cellule, trame
`world_initialized` émise **une seule fois**, `SimulationController.cs:189`).
Au-delà de ~50 000 cellules (profil de 10 m sur 2240 m), privilégier une case
plus grosse plutôt que d'alourdir cette trame.

---

## 6. Densité sociale et borne de perception

Le voisinage moyen dans un rayon `r` est

```
voisins_attendus = (agents ÷ surface) × π × r²
```

Le rayon est **borné à 70 unités par le validateur** (`SimulationOptionsValidator.cs:58-64`,
décision n°6) : `π × 70² = 15 394 m² ≈ 0,0154 km²`.

| Profil | Surface | Agents | Voisins à `r = 70` | Voisins à `r = 50` |
| :-- | ---: | ---: | ---: | ---: |
| A (500 × 500) | 0,25 km² | 50 | 3,08 | 1,57 |
| A (500 × 500) | 0,25 km² | 100 | 6,16 | **3,14** |
| **B (2 240 × 2 240)** | 5,02 km² | 50 | **0,15** | 0,08 |
| **B (2 240 × 2 240)** | 5,02 km² | 100 | **0,31** | 0,16 |
| B (2 240 × 2 240) | 5,02 km² | 500 | 1,53 | 0,78 |
| C (5 000 × 5 000) | 25 km² | 500 | 0,31 | 0,16 |

**Lecture honnête** : en 5 km², la vie sociale n'atteint ~3 voisins qu'autour de
**500 agents** avec `r = 70`. Au lancement (50–100 agents) un agent ne voit
qu'un tiers de voisin en moyenne — il devra souvent attendre un rond de
perception (4 ticks, `SimulationOptions.cs:102`) pour croiser quelqu'un.

Options (à trancher, décision n°6) :

1. **Lancer sur le profil A puis passer au profil B** à la montée en effectifs —
   la densité des ADR-014/015/016 est conservée au départ.
2. **Accepter la rareté** : le joueur devient le pôle social principal.
3. **Étendre la borne [20 ; 70]** du validateur vers `r = 300` (→ 2,8 voisins à
   50 agents en 5 km²) — **changement SYNE** (validateur + recalibration), hors
   du périmètre PRISM.
4. **Clusteriser le spawn** — `World.SamplePosition` tire uniformément sur tout
   le monde (`World.cs:243-247`) ; concentrer le départ d'initiers est aussi une
   modification SYNE (aucune option de placement n'existe).

Aucune option n'est cachée : PRISM consomme le rayon que SYNE donne.

---

## 7. Environnement complet : ce qui relève de SYNE et de PRISM

Le profil B demande un monde couvert de **forêt, rivière, montagne et bord de
mer**. Répartition des responsabilités, telle que le contrat la décrit
aujourd'hui :

### 7.1 Ce que SYNE exporte réellement

- **Aucun terrain** : `cells[].terrainType` est codé en dur à `"plains"` et
  `cells[].height` à `0` (`WorldDescription.cs:66`). Il n'y a **ni altitude, ni
  type de sol, ni biome** dans le contrat.
- **Découpage marchable** : `walkable = false` et `movementCost = 100` dès
  qu'un obstacle recouvre la cellule (distance au centre ≤ `rayon + cellSize/2`,
  `WorldDescription.cs:61-66`), sinon `true` / `1`.
- **Obstacles** : disques `{id, x, y, radius}` de `world.obstacleLayout`,
  activés par `world.obstacles` (`SimulationOptions.cs:466`, `:474`,
  `StaticObstacleSettings` `:616-622`) ; bornés à la taille du monde à la
  validation (`SimulationOptionsValidator.cs:393-400`). Seule forme géométrique
  supportée : **le disque**.
- **Ressources** : 4 types (`food`, `water`, `wood`, `mineral`) × **3 marqueurs**
  de placement (`WorldDescription.cs:35`, `:68`) — des indices de scènes, pas des
  gisements ; les stocks réels sont **globaux** (`ActionExecutor.cs:94-107`).
- **Régions** : grilles de 10 cases sans sémantique de paysage
  (`WorldDescription.cs:71-77`).

### 7.2 Ce que PRISM génère (présentation)

Forêt, cours d'eau, côtes, reliefs, matériaux et LOD sont **entièrement du
ressort de PRISM** — `SCENE_SPEC.md` (périmètre du monde présenté) et
`RENDERING_SPEC.md` (objectifs de rendu). Ils sont libres du rendu ; en
revanche :

- **Si un relief doit bloquer les agents**, il faut le déclarer en obstacle SYNE
  (disque) — un NavMesh Unreal « borde » les acteurs sans jamais autoriser l'état
  simulé, et un décor non déclaré crée une divergence entre le monde affiché et
  le monde décidé.
- **Si un biome doit être perçu**, PRISM le génère et, s'il doit avoir une
  portée décisionnelle, cette extension relève d'une évolution du contrat
  `WorldCell` (hors périmètre de ce document).

### 7.3 Pourquoi l'agrandissement ne casse pas l'économie

- Réserve mangée **n'importe où** (globale) → pas de parcours obligatoire vers
  une source de nourriture.
- Coût de mouvement **par tick**, pas par distance → pas de « course plus
  longue = plus faim ».
- Cibles locales (≤ 35 unités) et A* borné à 4096 cellules → le coût de
  décision ne dépend pas de la taille du monde.

La densité de mobs de ressources visuels, elle, doit être ajustée côté PRISM
(3 marqueurs par type ne couvriront pas 5 km²).

---

## 8. Configuration Unreal correspondante

### 8.1 Conversion de positions

```
UnrealX = SyneX × k   avec k = tileWP / worldCellSize = 3200 / 32 = 100
```

Origine du monde à `(0,0)`. Exemple : agent à `(12.5, 23.0)` → `(1250, 2300)` uu,
case `(0,0)` (indice `floor(12.5/32) = 0`). Voir aussi
`PRISM_UNREAL_IMPLEMENTATION.md:221-224`.

### 8.2 World Partition

- **Cellule runtime : 3200 uu** (défaut UE) — ne rien changer.
- **Streaming activé** (`Enable Streaming`), `LoadingRange` défaut **25 600 uu**
  = ±8 cellules = ±256 m autour du joueur : ~300 des 4900 cellules chargées.
  Descendre sous ~6 400 uu rendrait le streaming visible.
- **HLOD par cellule** : un cluster = une case SYNE de 32 m — le découpage SYNE
  et le découpage Unreal coïncident.

### 8.3 Navigation

Le NavMesh d'Unreal reste aux réglages par défaut (rayon d'agent ~30–40 uu,
résolution de maillage ~1 m — à confirmer sur UE 5.8) : sa résolution est propre
au jeu et **n'a rien à voir avec la case logique de 32 m**. SYNE reste autoritaire
sur *où* un agent a le droit d'aller (`walkable`, obstacles), le NavMesh sur
*comment s'y rendre*.

### 8.4 Déplacement des acteurs

- Sur chaque snapshot (6/s), convertir la position SYNE et appeler **`AI MoveTo`**
  vers ce point : le mouvement reste lissé, le CharacterMovement gère collisions
  et pente (`RENDERING_SPEC.md:21` autorise l'interpolation visuelle).
- **Ne pas téléporter** : l'acteur courant `BP_World`
  (`prism/LDK/Plugins/PrismLdk/Content/World/BP_World.uasset`) applique
  `SetActorLocation` sur les positions reçues — à remplacer par la poursuite
  lissée dès que le monde n'est plus un plan.

### 8.5 Vitesse par agent (règle obligatoire)

Le snapshot expose `agents[].traits.speed` (`ObservabilitySerializer.cs:291-345`).
Règle :

```
MaxWalkSpeed(agent) = traits.speed × 100 × 6   →   300 à 900 uu/s
```

Avec un `MaxWalkSpeed` fixe à 600, un agent à trait 1,5 demanderait 900 uu/s :
il dériverait de 300 uu/s, soit **0,5 m de retard par tick** (3 m/s de dérive
cumulée — SYNE repositionne la cible sur sa propre position, jamais sur celle de
l'acteur). La règle ci-dessus rend le rattrapage inutile. Le joueur garde le
défaut **600 uu/s** = agent médian.

### 8.6 Cadence lue par le plugin

`world_initialized` porte `ticksPerSecond` (parsed `PrismLdk.cpp:327`) et le
statut HTTP le confirme (`PrismLdk.cpp:320`) ; la file d'entrée est vidée dans
`Tick` (`PrismLdk.cpp:189-192`) — 6 snapshots/s sont sans effet sur ce canal.
La cadence retenue se règle côté SYNE (`prepare` accepte `ticksPerSecond`,
`ControlServer.cs:267-273`).

---

## 9. Limites et ouvertures

1. **Le joueur n'existe pas dans SYNE** : il n'y a ni trajectoire, ni besoins, ni
   perception pour lui (`ControlServer.cs:205-233`). La « vitesse = joueur » est
   donc une **parité d'échelle**, pas une synchronisation d'état. Ajouter le
   joueur à SYNE reste une ouverture structurelle.
2. **Horloge déjà accélérée** : une journée réelle (86 400 s) exigerait
   `TPS = 1440 / 86400 = 1/60`, or `TicksPerSecond` est un entier ≥ 1
   (`ControlServer.cs:269-271`). Le plus lent possible, `TPS = 1`, donne déjà
   une journée en 24 minutes (60× le réel). Le 360× de `TPS = 6` est donc un
   choix pris sur une base qui ne peut pas être « temps réel pur ». Parité
   joueur/agent à `TPS = 1` imposerait `k = 600` (1 unité = 6 m, pas de 3 à
   9 m par tick) : rejeté en §11.
3. **Les actions n'ont pas de durée** : chaque montage Unreal (0,5–2 s) est une
   **présentation** d'un état qui change en un tick de 166,7 ms. Aucun système
   Unreal ne doit en déduire un état (source de vérité = snapshot).
4. **Borne de perception [20 ; 70]** : la densité sociale du §6 est un fait,
   pas un réglage PRISM.
5. **Recalibration** : ADR-016 a calibré l'horizon 2500 ticks sur le monde
   500×500 par défaut. Le passage au profil B doit être **re-vérifié** avec
   `scripts/calibration-campaign.py`. Le bilan énergétique devrait rester stable
   (§7.3) ; c'est la densité sociale qui bouge.
6. **`syne-mock` diverge** : `simulatedMinutes = round(tick / ticksPerSecond)`
   (`syne-mock/src/simulation/simulation.js:430-431`) alors que SYNE émet
   `tick × 1` (`SimulationLoop.cs:411`). Tout outil PRISM calibré sur le mock
   affichera une horloge fausse — tester contre SYNE réel.
7. **Obstacles en disques uniquement** : un littoral, une rivière ou une crête
   doivent être approximés par des disques côté SYNE (§7.1).

---

## 10. Validation

Checklist à exécuter sur SYNE réel (jamais sur `syne-mock`) avec le profil B et
`ticksPerSecond = 6` :

| # | Vérification | Attendu |
| :-- | :-- | :-- |
| 1 | TPS effectif : `GET /api/control/status` pendant 10 s | ≈ 6 ticks/s (± 10 %) |
| 2 | Cadence interne : temps entre deux snapshots | 166,7 ms ± 10 ms |
| 3 | Déplacement : agent médian sur 100 unités | 100 ticks ≈ **16,7 s** réelles |
| 4 | Traversée du monde (2 240 unités) | ≈ **6 min 13** s réelles |
| 5 | Journée simulée | **240 s** réelles (mesurer `simulatedTimeMinutes` = 1440) |
| 6 | Délibération : événements `decision_made` | toutes les 1,67 s par agent |
| 7 | Échelle : position SYNE `(12.5, 23.0)` → `(1250, 2300)` uu | conversion exacte (aucun offset) |
| 8 | Trame `world_initialized` | `cellSize = 32`, `cellCountX/Y = 70`, 4 900 `cells[]`, `ticksPerSecond = 6` |
| 9 | Vitesse par agent : un agent `speed = 1,5` | suit sa cible sans dérive (MaxWalkSpeed 900) |
| 10 | Stabilité | campagne 50/100 agents × 2500 ticks × 3 seeds sans extinction |
| 11 | Budget | TPS maintenu avec marge (mesuré : 0,13 ms/tick à 50 agents) |

---

## 11. Re-dimensionnement

```
k                  = tuile_WP / worldCellSize
TPS                = MaxWalkSpeed_joueur / (k × speed_médian)     [arrondi entier ≥ 1]
vitesse_apparente  = k × TPS × speed                              [uu/s]
journée_réelle(s)  = 1440 / TPS
voisins_attendus   = (agents / côté²) × π × r²                    [r ≤ 70]
MaxWalkSpeed(agent)= speed × k × TPS
```

Variantes (pour mémoire, aucune n'est retenue) :

| Objectif | Réglage | Effet |
| :-- | :-- | :-- |
| Journée quasi réelle | `TPS = 1`, `k = 600` | Parité joueur/agent satisfaite (600 uu/s), mais 1 unité = 6 m, pas de 3 à 9 m par tick (saut visible à 1 Hz) et monde de 13 km par côté à taille fixe — rejeté |
| Conserver `TPS = 10` (défaut SYNE) | `k = 60` | 1 unité = 0,6 m ; agent médian à 600 uu/s ; échelle non métrique |
| Journée plus rapide (test) | `TPS = 15` | journée 96 s ; agents à 1500 uu/s — hors défaut UE |
| Défaut calibré (profil A) | 500 / cell 10 / `k` 100 / `TPS` 6 | tuile 1000 uu (sous le défaut UE), densité sociale correcte |
| Très grand monde (profil C) | 5 000 / cell 40 | tuile 4000 uu (réglage WP requis), 25 km² |

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 7 octobre 2026 | Création | Fixer `k`, `TPS` et le profil de monde pour l'immersion temps de jeu |
