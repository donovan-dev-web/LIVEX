# ADR-011 : Persistance JSON → SQLite

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.12 (ADR-011), Annexe G (Schéma SQLite V2.0)

---

## Contexte

La persistance doit garantir la reproductibilité et la reprise exacte (état complet : monde, entités, PRNG).

## Décision

- **V1** : persistance **JSON** (simple, inspectable).
- **V2** : migration vers **SQLite** pour :
  - transactionnalité (sauvegarde atomique) ;
  - évolutivité (ajout de tables sans refonte) ;
  - requêtes analytiques (pour ECHOS) ;
  - migration (schema versioning).

Le schéma V2.0 comprend **11 tables** (`runs`, `tick_states`, `agents`, `agent_snapshots`, `resources`, `resource_snapshots`, `groups`, `group_memberships`, `events`, `messages`, `metrics`).

## Conséquences

### Positives
- Sauvegardes atomiques et analyse SQL directe par ECHOS.
- L'état du PRNG (`tick_states.rng_state`, 4×ulong) permet la reprise bit-à-bit.

### Négatives
- SQLite s'utilise via un package NuGet (`Microsoft.Data.Sqlite` ou `System.Data.SQLite`) — pas intégré nativement au runtime .NET.

### Risques
- Migration V1 → V2 à documenter (script SQL) — les sauvegardes JSON doivent rester lisibles le temps de la transition.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |