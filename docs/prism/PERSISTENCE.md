# PERSISTENCE — Points de reprise et reprise bit-à-bit

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
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
| `persistence.keepCheckpoints` | 3 | Points de reprise conservés (le premier et le dernier sont toujours gardés) |
| `persistence.onStop` | `true` | Point de reprise à l'arrêt propre |

L'écriture est **hors chemin critique** : un instantané copie-sur-écriture des chunks *dirty* est pris à la fin du tick et écrit par un thread dédié ; le tick suivant ne l'attend pas, sauf si l'écriture précédente n'est pas finie (alors le tick attend : déterminisme avant vitesse).

## 4. Reprise

1. Le Launcher relance PRISM avec `--resume <checkpointId>` ; SYNE fait de même avec le **même** point.
2. PRISM charge le pack (le `contentHash` doit correspondre, sinon refus), régénère le monde de base, applique le différentiel de chunks, restaure les tables, **vérifie l'empreinte** et publie `world_ready`.
3. Le premier `TickRequest` reçu doit être `tick + 1`.

## 5. Évolution du schéma

`PRAGMA user_version` ; migrations ascendantes fournies ; un format sans migration impose `MAJOR` (`VERSIONING.md` §3).

## 6. Interventions et rejouabilité

`interventions.log` (DETERMINISM §7) est stocké avec le point de reprise.

## Points restés ouverts dans ce document

Compression : Deflate vs Brotli à trancher par mesure (B5).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
