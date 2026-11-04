# PERSISTENCE — Points de reprise et reprise bit-à-bit

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION EN COURS — CHECKPOINT HOST/WORLD GÉNÉRÉ (SOCLE)]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`DATA_MODEL.md`](DATA_MODEL.md), [`DETERMINISM.md`](DETERMINISM.md), `../syne/PERSISTENCE.md`
**Source Monographie** : Annexe G (schéma SQLite)

---

## 1. Principes

- La persistance est **séparée** de celle de SYNE et d'ECHOS (chacun possède sa base) mais **couplée par un jeu de points de reprise** : un point de reprise est valide si SYNE et PRISM ont enregistré le **même tick** et leurs empreintes sont consignées dans un manifeste commun.
- La reprise est **bit-à-bit** : un run restauré puis poursuivi produit les mêmes empreintes qu'un run continu (`DETERMINISM.md` §6).
- Stockage : un fichier SQLite par run (`<LIVEX_DATA>/prism/<runId>/world.sqlite`), comme le choix de SYNE (ADR-011), avec compression de chunks par `Deflate`/`Brotli` de la BCL (aucune dépendance de plus).

## 2. Contenu d'un point de reprise

| Table | Contenu |
| :-- | :-- |
| `meta` | `schemaVersion`, `runId`, `seed`, `worldGenVersion`, `contentHash`, version PRISM, `tick` |
| `chunks` | `chunkId`, `ids` et `states` compressés, empreinte ; **seulement les chunks modifiés depuis la génération** (le reste se régénère depuis la graine) |
| `agents` | Position, profil, maîtrises, `activeAction` (machine à états sérialisée) |
| `inventories` | Piles par agent, avec fraîcheur et durabilité |
| `containers` | Piles, capacités, listes d'accès |
| `ground_items` | Objets au sol |
| `sites`, `structures` | Chantiers, structures, intégrité |
| `scheduled` | Contenu de la roue temporelle (échéance, type, cible, paramètres) |
| `rng` | États des flux séquentiels (les flux à compteur n'ont pas d'état) |
| `ledger` | Compteurs d'audit par `ruleId` |
| `commands_seen` | Fenêtre d'idempotence des `commandId` récents |
| `checksums` | Empreintes de run jusqu'à ce tick |

Génération + **différentiel** (chunks modifiés) plutôt qu'un monde dense : un point de reprise est typiquement bien plus petit que 12,6 Mo ; le plein monde reste le pire cas.

## 3. Cadence

| Paramètre | Défaut | Rôle |
| :-- | :-- | :-- |
| `persistence.checkpointIntervalTicks` | 500 | Intervalle de points de reprise automatiques |
| `persistence.keepCheckpoints` | 3 | Nombre de points automatiques récents conservés par run |
| `persistence.onStop` | `true` | Point de reprise à l'arrêt propre |

**État actuel :** si un store SQLite est configuré, le host écrit un checkpoint synchrone tous les 500 ticks, conserve les trois plus récents pour le préfixe du run et sauvegarde aussi à l'arrêt propre. Cette écriture n'est pas encore hors chemin critique ; son coût sera mesuré après l'implémentation complète.

## 4. Reprise

1. Le Launcher relance PRISM avec `--resume <checkpointId>` ; SYNE fait de même avec le **même** point.
2. PRISM charge le pack (le `contentHash` doit correspondre, sinon refus), régénère le monde de base, applique le différentiel de chunks, restaure les tables, **vérifie l'empreinte** et publie `world_ready`.
3. Le premier `TickRequest` reçu doit être `tick + 1`.

## 5. Évolution du schéma

`PRAGMA user_version` ; migrations ascendantes fournies ; un format sans migration impose `MAJOR` (`VERSIONING.md` §3).

## 6. Interventions et rejouabilité

`interventions.log` (DETERMINISM §7) est stocké avec le point de reprise.

### État d'implémentation

`Prism.Host.Persistence.WorldCheckpointStore` crée un fichier SQLite à `user_version=2` et stocke le tick, la seed, le `contentHash`, l'état sérialisé d'un `ICheckpointableWorldHost` et, si un pack a été préparé, le snapshot du monde généré. Les bases v1 sont migrées en ajoutant la colonne de snapshot du monde. `OrderedWorldHost` capture/restaure le tick et l'état des phases checkpointables ; le dispatch sauvegarde aussi sa fenêtre d'idempotence, l'occupation des agents (`ActionDurationTracker`), les actions en attente de résolution et le canal de retour des macro-étapes (format v3, qui relit encore v1 et v2). Avec un store configuré, le runtime sauvegarde à la cadence par défaut de 500 ticks et à l'arrêt, puis ne garde que les trois checkpoints automatiques les plus récents du même run. À la restauration, le monde généré est reconstruit puis son empreinte est recalculée et vérifiée. Le host refuse un `contentHash` différent.

Ce snapshot persiste les colonnes, surcharges, zones, strates, arbres et sites d'apparition. Les phases de cycle de vie et de construction du host capturent les agents (profil, position et emplacements d'inventaire), objets au sol et prochain ID, règles de déplacement, sites de chantier, progression, ACL et gabarits compilés ; les contributions modifient la grille partagée et l'empreinte est recalculée avant publication et sauvegarde. Le cycle de vie capture aussi les ordres de déplacement `Aller` en cours (chemin, prochaine étape, cible et commande d'origine). Le ledger, les réservations de stations, la roue temporelle et l'état des actions différées sont checkpointés ; les structures autres que les chantiers et l'état du RNG ne le sont pas encore. Les anciens checkpoints sans état des phases optionnelles restaurent ces systèmes à vide ; le format de cycle de vie accepte encore la version 1 sans ordres de déplacement. Un test de reprise compare un run continu et un run restauré au milieu d'un déplacement : résultats de tick sérialisés, empreinte du monde et entités sont identiques. La sauvegarde automatique reste synchrone et non couplée à SYNE ; la reprise couplée avec SYNE relève de P9 et PRISM-076 reste partielle pour ce point.

## Points restés ouverts dans ce document

Compression : Deflate vs Brotli à trancher par mesure (B5).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 3 novembre 2026 | Ledger, roue temporelle et actions différées confirmés checkpointés ; RNG et structures hors chantiers explicitement non persistés | Audit de cohérence avec le code |
| 3 novembre 2026 | Ordres de déplacement `Aller` checkpointés ; preuve de reprise identique (tick, empreinte, entités) sur un run interrompu en cours de marche | PRISM-050/076 |
| 2 novembre 2026 | État des chantiers/gabarits raccordé au checkpoint du host ; cadence automatique, rotation, delta régional et empreinte recalculée | PRISM-075/076/091 |
| 2 novembre 2026 | État autonome initial des chantiers (progression, contributeurs, propriétaire et ACL), avant son raccordement au checkpoint du host | PRISM-091/093 |
| 2 novembre 2026 | Cycle de vie des agents, inventaires/objets au sol et règles de déplacement checkpointés dans l'hôte | PRISM-050/075/076 |
| 1er novembre 2026 | Création | Refonte V2 |
