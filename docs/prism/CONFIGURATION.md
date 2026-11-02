# CONFIGURATION — Clés de configuration et profils PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`CONTENT_SPEC.md`](CONTENT_SPEC.md), `../syne/CONFIGURATION.md`, `../NAMING_CONVENTIONS.md`
**Source Monographie** : Annexe H

---

## 1. Principe

La configuration est un **contrat reproductible** : même `config.json`, même seed, mêmes versions, même `contentHash` → même trajectoire. Priorité (comme SYNE) : défauts du code < profil < `config.json` < drapeaux CLI. Clés en **anglais** ; valeurs numériques entières.

## 2. Clés

```json
{
  "world": {
    "size": [256, 256, 64],
    "chunkSize": [16, 16, 16],
    "seaLevelZ": 26,
    "borderWidth": 6,
    "genMaxAttempts": 8
  },
  "time": { "dayLengthTicks": 480, "seasonLengthDays": 4 },
  "perception": { "nearRadius": 16, "midRadiusChunks": 3, "maxObservedPerAgent": 256 },
  "agent": { "inventorySlots": 12, "moveBudgetPerTick": 1000, "transferMaxDistance": 3 },
  "updates": { "randomTicksPerChunkPerTick": 3, "freshnessSweepTicks": 16 },
  "mechanisms": {
    "mastery": true, "perishability": true, "carryLimit": true,
    "toolWear": true, "sharedStructures": true, "structureWear": false
  },
  "knowledge": { "mode": "simple" },
  "persistence": { "checkpointIntervalTicks": 500, "keepCheckpoints": 3, "onStop": true },
  "determinism": { "checksumIntervalTicks": 100, "auditIntervalTicks": 100 },
  "content": { "pack": "content/", "extraPacks": [] },
  "interfaces": { "controlPort": 5182, "dataPort": 5183, "readPort": 5184 },
  "host": { "threads": 0 }
}
```

`host.threads = 0` : automatique (le résultat ne dépend pas du nombre de threads). Les ports sont des **hypothèses** à fixer lors de l'intégration Launcher (SYNE occupe 5180 et 5181).

## 3. Section `mechanisms` : leviers d'ablation

Chaque clé désactive **un** mécanisme de coopération pour une expérience (`TESTING.md` §6). Leur désactivation ne doit pas changer l'API ; elle modifie seulement les règles (par ex. `perishability: false` → `shelfLifeTicks` ignoré ; `carryLimit: false` → `inventorySlots` effectivement illimité). La valeur effective est incluse dans les métadonnées de run.

## 4. Profils

| Profil | Rôle | Particularités |
| :-- | :-- | :-- |
| `reference` | Référence de calibrage | 100 agents, 5 000 ticks, savoir simple, tous mécanismes actifs |
| `scale` | Montée en charge | 1 000 agents, 256 × 256 × 64, mesure de performance |
| `ablation-*` | Un mécanisme désactivé | Dérivés de `reference` |
| `rich-knowledge` | Variante riche | `knowledge.mode = "rich"` |
| `viewer-demo` | Démonstration | Plus petit monde, agents scriptés |

## 5. Validation

Rejet avec erreur structurée si : dimensions non multiples du chunk, `nearRadius ≥ 64`, `dayLengthTicks < 24`, `inventorySlots < 1`, pack absent ou invalide, ports en collision, `mechanisms` incohérents (`structureWear: true` sans `sharedStructures`). Avertissement si `simulatedSecondsPerTick × dayLengthTicks ≠ 86 400` (cohérence temporelle SYNE↔PRISM).

## 6. CLI de l'hôte

`Prism.Host --config <fichier> --profile <nom> --seed <n> --resume <id> --headless` ; `Prism.Viewer --connect <url>` ou `--embedded`.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
