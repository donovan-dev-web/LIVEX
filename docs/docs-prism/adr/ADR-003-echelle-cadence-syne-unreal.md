# ADR-003 : Échelle et cadence SYNE ↔ Unreal (k = 100, TPS 6, monde ≈ 5 km²)

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 7 octobre 2026
**Dépend de** : [`ADR-002-choix-unreal-prism-ldk.md`](ADR-002-choix-unreal-prism-ldk.md), [`../SCALE_AND_CADENCE_SPEC.md`](../SCALE_AND_CADENCE_SPEC.md), [`../../adr/ADR-003-api-http-rest.md`](../../adr/ADR-003-api-http-rest.md), [`../../adr/ADR-004-websocket-temps-reel.md`](../../adr/ADR-004-websocket-temps-reel.md), [`../../docs-syne/adr/ADR-005-temporalite.md`](../../docs-syne/adr/ADR-005-temporalite.md)
**Source Monographie** : §2.4.2 (indépendance du rendu), §5.2.3 (moteur), §5.15 (évolution)

---

## Contexte

PRISM doit représenter un monde d'**environ 5 km²** (forêt, rivière, montagne,
bord de mer) pour **50 à 100 agents au lancement, 500 agents visés**, avec une
expérience de **temps de jeu continu** où le joueur et les agents évoluent à la
même vitesse apparente.

Trois contraintes se croisent, toutes vérifiées dans le code :

1. **1 tick = 1 minute simulée** (`SimulationLoop.cs:409`, ADR-005) et
   `simulation.ticksPerSecond` est un entier strictement positif
   (`ControlServer.cs:269-271`), exécuté par `Task.Delay` sans rattrapage
   (`SimulationController.cs:272`, `:519`).
2. **Le pas d'un agent** est borné par le trait `speed` ∈ [0,5 ; 1,5]
   (`EntityTemplate.cs:44-47`) : sa vitesse apparente vaut `k × TPS × speed`
   en Unreal Units par seconde. Le trait est clampé à [0 ; 2]
   (`TraitSet.cs:9-11`).
3. **Unreal par défaut** impose `MaxWalkSpeed = 600 uu/s` pour le joueur et une
   cellule World Partition de `3200 uu` ; `1 uu = 1 cm`. Or la consigne de
   projet est de **ne pas changer les valeurs par défaut du moteur** afin que la
   vitesse des agents égale la vitesse du joueur.

Le dépôt ne tranchait jamais cette correspondance : le guide d'intégration
proposait une échelle « illustrative » (tuile de 100 uu, `k = 10` avec
`CellSize = 10`), sans lien avec la vitesse du joueur ni avec la taille de monde
demandée ; ce guide est réaligné par la présente ADR. La calibration ADR-016
(2500 ticks, 50/100 agents) avait été menée sur le monde par défaut 500 × 500.

## Décision

**Nous retenons `k = 100 uu par unité SYNE` (1 unité = 1 mètre), une cadence de
`simulation.ticksPerSecond = 6`, et un monde de `2 240 × 2 240` unités découpé en
cases de `32` unités (`simulation.worldCellSize = 32`).**

Détail des éléments structurants :

- **Échelle** : `k = tuile_WorldPartition / worldCellSize`. Avec une tuile de
  3200 uu (défaut UE) et `worldCellSize = 32`, `k = 100` → 1 unité SYNE =
  100 uu = 1 m. Toutes les distances SYNE deviennent métriques (perception 70 =
  70 m, obstacles en mètres, régions de 10 cases = 320 m).
- **Cadence** : `TPS = 600 / (k × speed_médian) = 600 / 100 = 6`. Soit
  166,7 ms/tick, 10 frames Unreal à 60 FPS, journée simulée en 240 s,
  délibération toutes les 1,67 s.
- **Monde** : 2 240 m par côté = **5,02 km²**, soit 70 × 70 = 4 900 cases de
  32 m, 49 régions, ≈ 0,5 Mo pour `cells[]` (trame `world_initialized` émise une
  seule fois). La case SYNE de 32 m coïncide **exactement** avec la cellule
  World Partition par défaut (3200 uu) : aucun réglage de streaming.
- **Vitesse par agent** : `MaxWalkSpeed(agent) = agents[].traits.speed × k × TPS`
  (300 à 900 uu/s), lu dans le snapshot (`ObservabilitySerializer.cs:291-345`).
  Le joueur garde le défaut 600 uu/s = agent médian.
- **Déplacement** : `AI MoveTo` vers la position convertie, interpolation
  visuelle (`RENDERING_SPEC.md:21`), pas de téléport (`SetActorLocation` de
  `BP_World` à remplacer).
- **Frontière inchangée** : SYNE reste l'autorité de l'état ; le joueur n'existe
  pas dans SYNE (`ControlServer.cs:205-233`), la parité est une parité
  d'échelle, pas une synchronisation d'état.

Le détail complet (tables, formules, checklist de validation) est dans
[`../SCALE_AND_CADENCE_SPEC.md`](../SCALE_AND_CADENCE_SPEC.md).

## Conséquences

### Positives
- **Aucun réglage hors défaut d'Unreal** pour le joueur, le World Partition et
  le HLOD : 600 uu/s et 3200 uu sont conservés tels quels.
- Échelle métrique : tout jeu de données (perception, obstacles, régions,
  rayons de collision) se pense en mètres, sans conversion mentale.
- Budget de tick largement tenu : 166,7 ms par tick contre 0,13 ms mesurés à
  50 agents (`PERFORMANCE.md:144-148`), soit ×1300 de marge.
- La densité de cellules reste faible (4 900 cases, ≈ 0,5 Mo) et le coût moteur
  ne bouge pas : réserves globales, coût de mouvement forfaitaire par tick,
  cibles bornées à 35 unités, A* plafonné à 4096 cellules.
- La conversion du guide d'intégration (`UnrealX = SyneX × k`,
  `k = taille_tuile / CellSize`) s'instancie à `tile = 3200`, `CellSize = 32` →
  `k = 100`, sans rupture de contrat.

### Négatives
- **Horloge à 360× le réel** : une journée passe en 4 minutes, la faim atteint
  son seuil en ~17 s et la fatigue en ~39 s. Le rythme est « survie en temps de
  jeu », pas simulacre de temps réel ; l'UI doit afficher l'échelle.
- **Densité sociale faible au lancement** : avec la borne `[20 ; 70]` du rayon
  de perception (`SimulationOptionsValidator.cs:58-64`), 50 agents en 5 km² ne
  voient en moyenne que 0,15 voisin (0,31 à 100 agents, 1,53 à 500). La vie de
  groupe n'est riche qu'à population élevée.
- **Actions sans durée** : tout se résout en un tick de 166,7 ms ; les montages
  Unreal de 0,5–2 s sont de la présentation pure.
- Recalibration à refaire : ADR-016 a calibré 2500 ticks sur le monde 500 × 500.

### Risques
- **Vitesse par agent non appliquée** : avec un `MaxWalkSpeed` fixe à 600 uu/s,
  un agent à trait `speed = 1,5` cumule 0,5 m de retard par tick derrière sa
  position autoritaire — divergence croissante entre l'affichage et l'état.
- **Borne de perception tenue pour un réglage PRISM** : elle appartient à SYNE
  (décision n°6) ; l'élargir est un changement moteur, pas un réglage de rendu.
- **Décors non déclarés** : un relief ou un cours d'eau généré côté PRISM mais
  absent de `world.obstacleLayout` crée un monde affiché où les agents marchent
  « à travers » l'obstacle — l'affichage doit refléter les obstacles déclarés.
- **Calibration sur `syne-mock`** : le mock diverge sur l'horloge
  (`simulation.js:430-431` : `round(tick/tps)` au lieu de `tick × 1`).

## Alternatives considérées

- **Conserver `CellSize = 10` et `k = 10` (guide illustratif d'origine)** :
  abandonné — 1 unité = 10 uu = 10 cm, et à `TPS = 6` l'agent médian n'irait
  qu'à 60 uu/s (0,6 m/s) : le monde bouge au ralenti face au joueur.
- **`TPS = 10` (défaut SYNE) et `k = 60`** : refusé — 1 unité = 0,6 m (échelle
  non métrique), journée en 144 s, et `k` s'écarte des multiples de 100 uu.
- **`TPS = 1` (temps réel pur) et `k = 600`** : refusé — la parité de vitesse
  tient, mais 1 unité vaudrait 6 m, le pas par tick deviendrait 3 à 9 m (saut
  visible à 1 Hz) et le monde de 2 240 m passerait à 13 km de côté.
- **Monde 5 000 × 5 000 (25 km²)** : reporté comme variante — cellule WP à
  4000 uu (réglage hors défaut), 15 625 cellules, et densité sociale 5× plus
  faible encore (0,31 voisin pour 500 agents).
- **`worldCellSize = 10` sur 2 240 m** : refusé — 50 176 cellules
  (~5,5 Mo) pour une trame envoyée une fois, sans bénéfice décisionnel.
- **Élargir la borne `[20 ; 70]` du rayon de perception** : reconnu comme le
  seul levier social propre (r = 300 → 2,8 voisins à 50 agents en 5 km²), mais
  refusé ici — c'est une décision SYNE (n°6) qui toucherait validateur et
  calibration. Ouvert en §« Validation ».
- **Changer `MaxWalkSpeed` du joueur à 900+ pour garder `TPS` plus haut** :
  refusé — le joueur reste la référence de vitesse du moteur.
- **Changer `TicksPerSimulationMinute`** (`SimulationLoop.cs:409`) : refusé —
  c'est le contrat temporel de SYNE (ADR-005), paramétrable mais hors périmètre
  PRISM.

## Validation / rejet

- **Checklist d'échelle et de cadence** : les 11 points du
  [`../SCALE_AND_CADENCE_SPEC.md` §10](../SCALE_AND_CADENCE_SPEC.md) doivent
  passer sur **SYNE réel** (TPS effectif 6/s, 166,7 ms entre snapshots,
  conversion `(12.5, 23.0) → (1250, 2300)` uu, traversée de 2 240 unités en
  ≈ 6 min 13, journée en 240 s, `world_initialized` avec `cellSize = 32` et
  4 900 cellules).
- **Calibration** : campagne `scripts/calibration-campaign.py` (50/100 agents ×
  2500 ticks × seeds 12345 / 424242 / 999) sans extinction sur le profil
  monde 2 240 / cellule 32, conformément à ADR-016.
- **Densité sociale** : observation des voisins moyens réels ; si la moyenne
  reste < 1 voisin au lancement, l'une des options du
  [§6](../SCALE_AND_CADENCE_SPEC.md) est activée (profil A au départ, borne de
  perception, ou spawn clusterisé) et cette ADR est révisée.
- **Conditions de réouverture** : un besoin d'horloge plus lente ou plus rapide
  ; une population visée supérieure à 500 agents ; l'intégration d'un joueur
  dans SYNE (ce qui rendrait la parité d'échelle obsolète) ; un budget de rendu
  non tenu à 4 900 cellules / 500 agents.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 7 octobre 2026 | Création | Trancher l'échelle, la cadence et la taille du monde pour l'immersion temps de jeu |
