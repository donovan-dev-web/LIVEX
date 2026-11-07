# SYSTEMS_SPEC.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 6 novembre 2026
**Dépend de** : `DATA_MODEL.md`, `COGNITIVE_ARCHITECTURE.md`, `SIMULATION_LOOP.md`
**Source Monographie** : Partie 3 (SYNE), Partie 6 (concepts détaillés)

---

## 1. Objectif

Spécifications fonctionnelles des **sous-systèmes** du moteur SYNE. Chaque section référence la partie de la Monographie à implémenter. Les entités logiques, ressources, groupes, conflits, héritage/fusion, livres sont traités ici à niveau système.

## 2. Sous-systèmes techniques

| Sous-système | Spécification | Source Monographie |
| :-- | :-- | :-- |
| **Spatial (grille)** | Grille uniforme ; `cellSize = sqrt(worldArea / (agentCount / 7))` (V0.1 : paramètre direct, défaut `largeur/10`) ; 9 cellules voisines (fenêtre 3×3) ; mises à jour incrémentales | §3.9.6 |
| **Perception** | Grille + filtre distance/rayon 50 ; perception étagée (`id % 4`) ; **ligne de vue** obstacle (ADR-013) | §3.9 |
| **Navigation** | Pathfinding 2D autour des obstacles, cache de chemins ; indépendant des moteurs graphiques ; appui sur Navigation2D = [HÉRITÉ] (V0.1 : SYNE calcule lui-même) | §3.19 |
| **Scheduler** | Exécution multi-fréquences + LOD décisionnel | §3.4 |
| **Événements** | Ring buffer borné (500 000 événements) ; émission `ExternalEvent` | §7.4.5 |
| **Monde externe** (P9, SYNE-220) | `IWorld` + `PrismWorldAdapter` : PRISM est **autoritaire** sur la géométrie et les entités du monde (`world.externalWorld` / `--external-world`) ; `PrismWorldSynchronizer` alimente l'adaptateur depuis l'auto-observation PRISM. Le monde local reste le défaut (étape 3 de l'extraction à faire) | §3.9, `docs/prism/ADR-013` |
| **Cognition avancée** (P9) | Croyances durables (recettes, chantiers, structures), mémoire spatiale par chunk, planificateur borné, désirs instrumentaux — tous **désactivés par défaut** (§11) | `COGNITIVE_ARCHITECTURE.md`, `DATA_MODEL.md` §5.1/§6 |

## 3. Monde & Environnement

- **Autorité du monde** (P9, ADR-013 côté PRISM) : SYNE décrit son monde en **blocs** — 1 bloc = 1 m = 1 unité SYNE — et ne possède plus de géométrie propre en mode couplé : quand `world.externalWorld` (ou `--external-world`) est actif, `PrismWorldAdapter` fournit entités, gisements, constructions, description du monde et A*, et les dimensions sont **lues** dans l'instantané PRISM. Le monde local reste le défaut ; sa suppression est l'étape 3 de SYNE-220.
- Espace 2D logique, dimensions configurables (défaut 500×500).
- Clamping des positions ; monde non-toroidal.
- Obstacles statiques (cercle en V0.1, rectangle reporté) : blocage mouvement **et** ligne de vue (perception masquée, ADR-013) — depuis le jalon SYNE ph1.
- Ressources : FoodSource / WaterSource (V1 : taux de régénération nul, eau infinie ; V2 : régénération + dégradation).
- Saisons, territoires, événements du monde, obstacles : clés de configuration — `world.seasons` (bloc actif depuis **SYNE-072**, jalon U8 : `{enabled, seasonLengthTicks, initialSeason, cycle[]}` ; l'ancien drapeau booléen homonyme, mort, est remplacé par ce bloc — `enabled` remplit son rôle), `world.territories` (bloc actif depuis **SYNE-073**, jalon U8 — §3.10), `world.events`, `world.obstacles` (défauts Annexe H).

### 3.10 Territoires (Monographie §6.5, décision n°21)

- **Définition (décision n°21, jalon U8)** : le territoire est une **zone délimitée** par la présence des entités autour d'un point de survie. En V0.1 : zone = disque **configurée** `world.territories.zones[]` `{id, centerX, centerY, radius}` (CONFIGURATION §6.10) — territoire effectif = présence d'entités dans le disque (`distance ≤ radius`, bord inclus).
- **Suivi (SYNE-073)** : appartenance **recalculée en fin de tick** (fenêtre causale fermée, `TrackTerritoryMembership` après le cycle de vie) — **function pure des positions**, **0 tirage PRNG**, aucun comportement agentique, aucune revendication ; ordre déterministe (par zone telle que posée, identifiant croissant, **sorties avant entrées**) ; tracées `Entered`/`Left` + `MembersOf(zoneId)`, drainées `world.territory_membership_changed` (agentId = entité, targetId = zone, value `{kind}`, API_CONTRACTS §2.2) ; snapshot `territories[]` `{id, x, y, radius, memberCount, members[]}` (§2.1) si suivi actif.
- **Déterminisme** : suivi désactivé par défaut (`enabled: false`) ⇒ scénario de référence intact (checksums dorés §3 — DETERMINISM.md §3) ; activé, l'altération se limite à des champs **additifs** du snapshot + événements typés, jamais à la cognition.
- **Limite V0.1** : l'ECHOS « frontières fluides / négociation territoriale » reste hors V0.1 ; les **sources spatiales de ressources** par territoire sont reportées (les réserves restent globales, §4).

## 4. Ressources (Monographie §3.18, §6.9)

- **Contrainte d'absence** : la finitude des ressources est un moteur de comportement (compétition, stockage, déplacement).
- V2 : agents en sources (`sources d'eau et de nourriture`), dégradation, puissance de régénération.
- Représentation : entité spatiale (Quantity, Capacity, RegenerationRate, Infinite).
- **V0.1 (SYNE-042, jalon SYNE ph4 puis SYNE-070, jalon U8)** : **réserves globales** partagées
  (`ResourceStocks` : Food 100 / Water 1000 / Wood 50 / **Mineral 0**, régénération décision n°4),
  initialisées depuis `resources.*`, consommées par les actions terminales Eat/Drink et émissées
  dans le snapshot (`resources`, DATA_MODEL §8.1) ; **cycle de vie (SYNE-070)** : régénération
  `+ regenerationRate` par tick et dégradation périodique (`− regenerationRate × degradationTick`
  à chaque période, clamp ≥ 0) appliquées en fin de tick, 0 tirage PRNG ; les **sources spatiales**
  → **V0.1 (SYNE-071, jalon U8)** : les constructions sont implémentées côté **monde** —
  disques statiques `Obstacle {Id, Position, Radius}` (layout `world.obstacleLayout[]`,
  CONFIGURATION §6.8), mutation dynamique validée (`AddObstacle` bornes/id unique + révision
  `ObstacleRevision`), constructions tracées `PlaceConstruction`/`RemoveConstruction`
  (modification d'environnement), grille A\* re-rasterisable (`Refresh()`, no-op déterministe,
  SYNE-071) ; la **mécanique agentique** (qui construit, coût en bois/minéraux, durée) reste
  ouverte (décision n°20). **Cycle de Saisons (SYNE-072, jalon U8)** : `world.seasons` (bloc
  actif) module la **régénération/dégradation** en fin de tick par des facteurs par ressource
  (cycle déterministe de 4 saisons, saison = fonction pure du tick, CONFIGURATION §6.9) et émet
  `world.season_changed` au basculement (API_CONTRACTS §2.2). Les **sources spatiales** de
  ressources restent **hors V0.1** (reportées — le jalon Saisons livre le cycle + facteurs, pas
  les sources spatiales).

> **Mise à jour du 6 novembre 2026 — le modèle localisé est celui de la référence.** L'arbitrage
> A0-2 (BL-35) a fait de `resources.localized = true` le modèle par défaut du profil de référence :
> les gisements sont des **entités spatiales** (`ResourceNode {id, kind, position, amount,
> capacity, regenerationRate}`), un agent ne prélève qu'un gisement **à portée d'interaction**
> (`resources.interactionRadius`) et un échec est motivé (« aucun gisement à portée », « gisement
> épuisé »). Les **réserves globales** (`ResourceStocks`) restent le modèle du mode non localisé et
> la cible des primitives d'inventaire (`take`, `give`, `trade`). Les phrases ci-dessus décrivent
> l'état antérieur ; le contrat d'observabilité publie les deux (`resources`, `resourceNodes`).

## 5. Groupes (Monographie §3.17, §6.7)

- Formation émergente par **cohésion** (proximité sociale/représentation partagée).
- Leader désigné par dynamique (attributs, confiance, contribution).
- Décisions collectives; rôles; dissolution.
- Récupération dans le SQLite : `groups`, `group_memberships`.

## 6. Conflits (Monographie §6.8)

- Combat = action coûteuse en énergie (ADR-009).
- Résolution probabiliste/numerique basée sur Strength et contexte (détails chiffrés à confirmer).
- Un conflit produit des événements typés (observables).

## 7. Communication (vu système — détail dans `COMMUNICATION_PROTOCOL.md`)

- **Locale** (rayon limité) et **non confidentielle** (ADR-008). Pas de canaux privés.
- Monte en 7 types de messages (Information, Request, Response, Announcement, Warning, Trading, Acknowledgement).
- Coûts d'envoi/réception, bande passante (maxSendsPerTick=5, maxReceivesPerTick=3), incompréhension (5%), dégradation de confiance par saut (10%/hop).

## 8. Héritage & Fusion (Monographie §6.6)

- **Naissance** : fusion consentie (décision n°17).
- **Héritage** : transmission de traits/connaissances (via **fusion consentie** §6.6.3, mécanismes fins configurables — décision n°16 **[TRANCHÉE]**).
- **Mort** : dissolution complète (V0.1) — seul un événement de trace subsiste.

## 9. Livres / Persistance de connaissance (Monographie §3.18 (livres))

- **Auteur** : écrit explicitement un livre matérialisant titre/contenu à un instant T. En V0.1 (SYNE-121), `SimulationLoop.WriteBook` débite le coût énergétique `world.books.writeCostEnergy`, poinçonne le tick, stocke le livre et émet `world.book_written` ; valeur par défaut provisoire, calibrage SYNE-120.
- **Lecteur consultant** : `SimulationLoop.ReadBook` enregistre le lecteur distinct et émet `world.book_read` avec `world.books.readBenefit`. Le principe de bénéfice est livré ; l’effet sur cognition/confiance/savoir est différé au futur moteur mémoire (hors U8).
- En V0.1 les opérations sont explicites et observationnelles : durée de rédaction, accès spatial et diffusion agentique restent ouverts.

## 10. Observabilité interne (Monographie §3.15)

- Événements typés : perception, décision, action, communication, naissance/mort, groupe, ressource, conflit.
- `ExternalEvent` émis via WebSocket ; DecisionRecord mémorisé.
- Exigence anti-triche : l'entité ne voit jamais le monde complet (observabilité partielle).

## 11. Cognition P9 — savoir, mémoire spatiale, planification, désirs (6 novembre 2026)

Quatre ajouts de la phase P9, **tous désactivés par défaut** (trajectoire de référence bit-à-bit
identique), qui préparent le gate G5 (« agents planifiant à partir de recettes »).

### 11.1 Savoir des recettes (SYNE-222/224)

- Recettes déclarées (`agents.knowledge`) injectées en **croyances durables** `recipe-<id>`
  (`inputs`, `outputs`, `station`), de source `config` ; un livre peut les transmettre
  (`recipe:` dans le texte, source `book:<id>`, `agents.knowledge.learnFromBooks`).
- Une croyance durable ne décroît pas et n'expire jamais : c'est la distinction **fait observé /
  savoir** (DATA_MODEL §6). Les livres ne font pas apprendre autre chose que des recettes.

### 11.2 Mémoire spatiale par chunk (SYNE-221)

- Un souvenir par région : masque cumulatif des types **exposés**, confiance révisée par la formule
  des croyances, décroissance et oubli, capacité bornée avec éviction déterministe
  (DATA_MODEL §5.1, `CONFIGURATION.md` §6.3).
- Alimentée par les **résumés de région** PRISM (`PrismObservationMapper.ToSpatialChunks`) : c'est
  la première source de connaissance qui ne vient pas d'une perception directe, et elle reste
  locale (une région à la fois).

### 11.3 Planificateur intégré (SYNE-223, décision **ADR-019**)

- Chaînage arrière **borné sur règles de décomposition** (HTN léger, pas GOAP — voir
  [`adr/ADR-019`](adr/ADR-019-planificateur-chainage-arriere-borne.md)) : une intention devient une
  suite d'étapes avec le **vocabulaire d'actions existant** (poursuite → action terminale), cache à
  clé `« <intention>@<cible|-> »`, replanification sur étape bloquée seulement, plans transitoires
  (redérivés après reprise), aucun tirage PRNG.
- Désactivé par défaut (`agents.planner.enabled`) ; la neutralité est mesurée (même trajectoire
  avec et sans planificateur).

### 11.4 Désirs instrumentaux et sécurité alimentaire (SYNE-222)

- `SecureFood` : l'agent dont la faim **projetée** atteindra son seuil à l'horizon d'anticipation, et
  dont la réserve est sous le niveau visé, vise un **stock** — poursuite vers la source de nourriture
  **crue**, puis `take` ; consommer reste `eat`. Le désir s'efface dès que la faim franchit son seuil
  et n'existe que si l'agent **sait** où trouver de la nourriture.
- Le stock devient utilisable : `eat`/`drink` consomment la réserve portée hors de portée d'un
  gisement. Désactivé par défaut et **refusé sans inventaire** (`agents.actions.inventory.enabled`).

---

## Points restés ouverts dans ce document
- Détails chiffrés de résolution des conflits (probabilités, dégâts) — à documentation quand elle sera tranchée.
- **P9, étape 3 de SYNE-220** : suppression du monde local de `Simulation.Core` (l'adaptateur `PrismWorldAdapter` n'est encore qu'une bascule) ; c'est elle qui débloque le retrait des tables du monde en persistance (SYNE-225) et le gate G5.
- **P9, planificateur** : enrichissement des règles de décomposition (plans de recettes, préconditions de station) et couverture de bout en bout du chemin `Blocked` — décision et conditions de réouverture dans ADR-019.
- **Calibrage** : poids relatif du désir instrumental et de `socialize` (SYNE-227, phase P12) ; les mécanismes sont livrés, les valeurs ne sont pas calibrées.
- Modèle d'héritage (fusion consentie) : logique cognitive exacte à détailler.
- Coûts des livres (décisions n°18/19) : modèles économiques — **[tranchés] au niveau hérité/prototype** (coûts héritables, budgets par fusion §6.6.3).
- Cycle des saisons : **livré** (SYNE-072, jalon U8, engineVersion 0.9.0 — fonction pure du tick, `world.seasons`, événement `world.season_changed`) mais **désactivé par défaut** en V0.1 (`world.seasons.enabled: false`, scénario de référence intact). Les événements globaux du monde (type sécheresse/épidémie) restent une mécanique V2. *(réaligné le 30/09/2026 — ce point indiquait auparavant « non activée en V0.1 » sans distinguer le livré du défaut)*