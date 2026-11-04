# SIMULATION_LOOP — Tick, ordre d'exécution, mises à jour et conflits

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION EN COURS — PRISM-026, 040–042, 050–056, 070–072 (SOCLE)]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`DATA_MODEL.md`](DATA_MODEL.md), [`DETERMINISM.md`](DETERMINISM.md), [`API_CONTRACTS.md`](API_CONTRACTS.md)
**Source Monographie** : —

---

## 1. Le tick

Le monde avance **au tick**. SYNE est l'horloge maîtresse : PRISM exécute le tick `t` quand il reçoit `TickRequest(t, …)`. Il n'y a pas de temps réel ; un run headless va aussi vite que possible.

### 1.1 Décalage causal

À un tick `t`, SYNE lit les observations et événements **produits au tick `t − 1`**, délibère, puis émet les commandes du tick `t`. PRISM les applique pendant `t` et produit les observations de `t`. Une action décidée sur la base d'une perception a donc toujours au moins un tick de latence : c'est voulu (ordre causal strict, comme `SYNE/DETERMINISM.md` §5).

## 2. Ordre fixe d'un tick PRISM

| # | Phase | Contenu | Parallélisme |
| :-- | :-- | :-- | :-- |
| 1 | **Réception** | Validation de version, d'identifiants, d'idempotence (`commandId` déjà vu → résultat rejoué) | séquentiel |
| 2 | **Cycle de vie** | `Incarner` / `Retirer` ; inventaire d'un agent retiré déposé au sol | séquentiel |
| 3 | **Arbitrage** | Clé de priorité par agent `Mix64(seedConflits, t, agentId)` ; ordre croissant de clé | séquentiel |
| 4 | **Démarrage / annulation** | Chaque commande ouvre ou remplace une macro-action | séquentiel (ordre §3) |
| 5 | **Avancement des macro-actions** | Un pas par agent actif (déplacement, minage, fabrication…) ; produit des *intentions d'écriture* | agents en parallèle, lecture seule |
| 6 | **Application des écritures** | Résolution des conflits (§4) puis écriture des blocs, inventaires, conteneurs | séquentiel, ordre de priorité |
| 7 | **Mises à jour programmées** | Tous les événements d'échéance `≤ t` (roue temporelle) | par région |
| 8 | **Mises à jour aléatoires** | `randomTicksPerChunkPerTick` colonnes tirées par région (hasard à compteur) | par région en parallèle |
| 9 | **Usure, péremption, chantiers** | Péremption des conteneurs, durabilité, intégrité des structures, avancement des chantiers | par entité |
| 10 | **Calendrier** | Jour, saison ; événements `day_changed`, `season_changed` | séquentiel |
| 11 | **Perception** | Pour chaque agent : observations différentielles à trois niveaux | agents en parallèle, lecture seule |
| 12 | **Sortie** | Événements, empreintes (intervalle), audit de conservation (build de test), flux de lecture | séquentiel |

Les phases 5 et 11 sont en **lecture seule** sur le monde : seule la phase 6 (et 7–9) l'écrit. Cela garantit qu'aucun résultat ne dépend de l'ordre d'exécution des threads. PRISM-072 implémente `IWorldHost.Step` : validation et copie de la requête, progression consécutive des ticks et exécution d'un handler par phase dans cet ordre. Les commandes sont prévalidées en phase 1, exécutées en phase 4 et leurs résultats sont publiés ; le rejeu idempotent n'exécute pas de nouveau le handler. Les handlers spécifiques aux systèmes du monde se branchent sur cet orchestrateur au fil de leurs issues d'intégration.

## 3. Macro-actions

Une macro-action est une machine à états exécutée par PRISM sur plusieurs ticks ; une seule est active par agent. Le socle `MacroActionSystem` exécute `ObtenirObjet`, `Fabriquer` et `Cultiver` à partir d'étapes confirmées par le tick appelant ; les sources d'`ObtenirObjet` viennent exclusivement des observations transmises, jamais d'un scan global. Les échecs terminent la macro avec leur raison et conservent les quantités déjà acquises.

| Macro-action | Étapes internes |
| :-- | :-- |
| `ObtenirObjet(type|tag, qty, hint)` | Se rendre à l'indice → vérifier la source *perçue* → s'orienter → miner/récolter/prendre → répéter jusqu'à `qty` → (option) revenir |
| `Fabriquer(recette)` | Aller à la station → déposer les entrées → attendre `durationTicks` (réduit par la maîtrise) → récupérer les sorties |
| `Cultiver(parcelle)` | Labourer → planter → arroser/s'assurer de l'eau → signaler la maturité (événement) → récolter sur ordre |
| `DemarrerChantier(gabarit, position)` | Créer le chantier ; puis `Contribuer(chantier)` par apports de matériaux ou de travail |
| `Demander / Offrir` | Les accords vivent dans SYNE ; PRISM n'exécute que `Transferer` après accord |

Chaque étape a une **durée en ticks** et une **liste d'échecs motivés** (`API_CONTRACTS.md` §4). Un échec n'efface pas le progrès déjà accompli (objets déjà minés restent dans l'inventaire).

## 4. Conflits

Deux agents peuvent viser le même bloc, la même case de pose, le même dernier objet d'un conteneur, la même place à une station.

- La résolution est **déterministe mais équitable** : l'ordre de priorité est une fonction de `(seed, tick, agentId)` ; l'agent de plus petit identifiant ne gagne pas systématiquement.
- Le **perdant** reçoit un événement `action_failed` avec la raison (`blocked_by_other`, `target_gone`, `station_busy`).
- Une station est utilisée par **un agent à la fois** par défaut (`stationCapacity`, paramètre).
- Aucune collision entre agents : ils peuvent se croiser et partager une case (Q-08).

## 5. Mises à jour du monde

| Mécanisme | Principe | Usage |
| :-- | :-- | :-- |
| **Programmées** | Un événement est inscrit pour un tick donné dans une **roue temporelle** (seaux par tick) | Stades de croissance des cultures, achèvement de fabrication à l'établi, expiration |
| **Aléatoires** | Par tick et par région horizontale, `randomTicksPerChunkPerTick` (défaut 3) colonnes distinctes tirées par une permutation dérivée de `(seed, tick, regionId)` | Phénomènes diffus : propagation de l'herbe, repousse |

**Croissance des cultures (décision Q-09).** Les cultures utilisent des **mises à jour programmées** (durée de stade + gigue tirée une fois à la plantation) ; les mises à jour aléatoires par région restent pour l'environnement. Les deux mécanismes sont benchmarkés (B3) sur un monde entièrement cultivé.

**Règle d'évolution (exemple)** : `wheat_crop` stade 0→3 en 3 transitions de `stageTicks ± jitter`, sous conditions (sol `farmland`, eau à ≤ `waterRadius` blocs, lumière non exigée). Si une condition disparaît, la croissance **s'arrête** (pas de régression) ; une culture non récoltée **se flétrit** après `witherAfterTicks` (risque de récolte manquée).

## 6. Calendrier

| Paramètre | Défaut (hypothèse) | Rôle |
| :-- | :-- | :-- |
| `dayLengthTicks` | 480 | Longueur du jour |
| `seasonLengthDays` | 4 | Jours par saison |
| Saisons | 4, cycle fixe | Modulent la croissance (facteur par saison) |
| Heure | `tick mod dayLengthTicks` | Jour/nuit (visibilité, exposition) |

Le calendrier est **exprimé en ticks**, indépendant du `simulatedSecondsPerTick` de SYNE. Le profil de référence V2 recommande `simulatedSecondsPerTick = 180` pour qu'un jour de 480 ticks corresponde à 24 h simulées (cohérence des taux de besoins de SYNE). Cette cohérence est une **contrainte de profil**, vérifiée au démarrage et signalée en avertissement si elle est rompue.

`WorldCalendar` calcule le jour (à partir de 1), le tick dans le jour, le jour dans la saison et la saison cyclique printemps/été/automne/hiver. L'avancement émet `day_changed` à chaque frontière journalière, puis `season_changed` à la frontière saisonnière ; un saut sur plusieurs jours restitue les événements dans l'ordre. Les événements peuvent être consommés par la phase Calendrier de la boucle.

`WorldUpdateScheduler` dérive la gigue d'une clé de bloc, du tick d'ancrage du cycle actif (conservé dans l'événement) et de l'étape via le flux `growthJitter`, sans état aléatoire mutable. Une culture qui sort de dormance commence un nouveau cycle de croissance. Les délais planifiés sont au moins d'un tick.

`RandomTickProcessor` choisit des colonnes distinctes par région à partir d'une permutation déterministe. Le calcul des mises à jour est parallèle et en lecture seule ; les écritures retournées sont appliquées ensuite en ordre croissant de région puis d'ordinal, de sorte que le nombre de workers ne change pas le résultat. `GrassSpreadSystem` est un consommateur de ces échantillons : une terre exposée peut devenir de l'herbe si une colonne cardinale voisine en porte déjà, selon `updates.grassSpreadChancePermille` et le flux indépendant `grassSpread`.

`CropGrowthSystem` plante une culture sur le matériau de support déclaré, vérifie l'eau dans le rayon Manhattan configuré et programme chaque stade selon le facteur saisonnier et la gigue déterministe. Les cultures dormantes peuvent être reprogrammées lors d'un changement de saison. L'arrivée au dernier stade publie un événement `crop_ripe` ; le flétrissement est programmé après le délai défini. `GrowthRuleRuntimeCompiler` relie les règles JSON compilées aux identifiants stables de matériaux. La récolte reste une action séparée.

La péremption est calculée paresseusement à l'accès, au transfert ou lors d'un sweep : `PerishableStackService` matérialise la fraîcheur au taux du contenant occupé pendant l'intervalle. Une pile expirée est retirée et sa destruction est comptée dans `MatterLedger` sous la règle `spoil`.

## 7. Durée des runs

Les runs de référence du dépôt sont calibrés sur **2 500 ticks** (ADR-016 de SYNE). Un cycle agricole complet dure plusieurs centaines de ticks et une saison ≈ 1 920 ticks : les runs de **validation de la V2 durent au moins 5 000 ticks** (≈ 2,6 saisons), et les expériences d'ablation 10 000 ticks. Voir `TESTING.md` §4.

## 8. Vitesse et déplacement

Un agent dispose à chaque tick d'un **budget de mouvement** `speedPermille × moveBudgetPerTick` (défaut 1 000 milli-blocs à vitesse 1,0). Un pas entre cases adjacentes coûte 1 000 (cardinal) ou 1 414 (diagonal, entier) ; un pas dont les deux cases sont sur un chemin divise ce coût par `movement.pathSpeedupPermille` (défaut 2 000 ‰, plage 1 000–10 000). Les matériaux des chemins proviennent du tag `walkable_path` ou des blocs dans la zone d'effet `path_speed` d'un gabarit. Le trait `speed` de SYNE ∈ [0,5 ; 1,5] est transmis une fois, à l'incarnation, en permille.

## Points restés ouverts dans ce document

Q-04, Q-09 (`adr/README.md` §3).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 3 novembre 2026 | Macro-action de construction alignée sur la commande `DemarrerChantier` (puis `Contribuer`) | Audit de cohérence avec le code |
| 2 novembre 2026 | Péremption paresseuse par contenant et propagation d'herbe configurable raccordée aux random ticks | PRISM-044/045 |
| 2 novembre 2026 | Croissance de culture saisonnière et flétrissement, règles JSON liées aux matériaux compilés | PRISM-043 |
| 2 novembre 2026 | Orchestrateur `IWorldHost.Step`, validation/snapshot des requêtes, ordre fixe des 12 phases et dispatch idempotent | PRISM-072 |
| 2 novembre 2026 | Cycle de vie du corps, graphe de déplacement 2.5D et A* entier borné avec cache | PRISM-050–052 |
| 2 novembre 2026 | Perception différentielle plafonnée et arbitrage stateless des conflits implémentés dans le noyau | PRISM-054/055 |
| 2 novembre 2026 | Coût des chemins aménagés lié aux tags et aux gabarits de contenu | PRISM-056 |
| 2 novembre 2026 | Calendrier, ordonnancement à gigue et traitement déterministe des mises à jour aléatoires ajoutés | PRISM-040–042 |
| 2 novembre 2026 | Socle déterministe de roue temporelle et ordre d'échéance/ séquence | PRISM-026 |
| 1er novembre 2026 | Création | Refonte V2 |
