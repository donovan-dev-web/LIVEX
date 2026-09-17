# LOGGING_INSTRUMENTATION.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.8

---

## 1. Les 3 niveaux de journalisation

| Niveau | Format | Support | Usage |
| :-- | :-- | :-- | :-- |
| **Événements structurés** | Schéma SQLite (`events_log`) | Interrogation ELT | Analyse scientifique |
| **Traces de décision** | Schéma SQLite (`decision_traces`) | BDI complet | Reconstruction causale |
| **Logs texte** | Serilog (console + fichier) | Débogage | Développement |

## 2. Les événements structurés

| Événement | Contenu |
| :-- | :-- |
| `PerceptionEvent` | `ObservedEntityIds`, `Count`, `AverageConfidence` |
| `DecisionEvent` | `ConsideredGoals`, `ActionScores`, `ChosenAction`, `ChosenUtility` |
| `ActionEvent` | `ActionType`, `ActionStatus` (Started/Updated/Completed/Failed), `Outcome` |
| `CommunicationEvent` | `SenderId`, `ReceiverIds`, `MessageType`, `MessageConfidence` |
| `BeliefEvent` | `Fact`, `OldConfidence`, `NewConfidence`, `Source` |
| `GroupEvent` | `GroupId`, `GroupAction` (Formed/Joined/Left/Dissolved) |

## 3. Les traces de décision

Chaque décision est tracée dans `decision_traces` avec le contexte BDI complet (besoins, croyances, scores d'utilité, action choisie) — c'est la brique de l'**analyse causale** (cf. `CAUSAL_ANALYSIS.md`) et la chaîne `Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`.

## 4. Les logs texte (Serilog)

- Rotation quotidienne : `logs/v2_simulation_{date}.log`.
- Format : `"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message}{NewLine}{Exception}"`.
- Tag d'application : "SSE-V2".

| Niveau | Usage |
| :-- | :-- |
| Error | Échec d'action, état incohérent |
| Warning | Chemin bloqué, ressources insuffisantes |
| Information | Résumé de tick, formation de groupe |
| Debug | Décisions d'entités, mises à jour de croyances |
| Verbose | Traces BDI complètes (développement uniquement) |

## 5. Le profilage

ECHOS utilise des **ProfileMarkers** (basés sur `Stopwatch.GetTimestamp()`) pour mesurer les temps d'exécution de chaque sous-système.

Exemple de sortie (500 appels) :

```text
Perception    : 240.51 ms total, 0.48 ms avg
Decision      : 185.23 ms total, 0.37 ms avg
Communication :  92.15 ms total, 0.18 ms avg
Movement      :  45.67 ms total, 0.09 ms avg
Belief Update :  78.34 ms total, 0.16 ms avg
```

## 6. La console de débogage

| Commande | Description |
| :-- | :-- |
| `list-agents` | Liste toutes les entités (position, énergie, faim) |
| `inspect-agent <name>` | Détail complet d'une entité (croyances, objectifs, action) |
| `trace-decision <name>` | Trace de décision détaillée pour un tick spécifique |
| `set-breakpoint <agent> "<condition>"` | Exemple : `"hunger > 90"` |

## 7. L'export

- **Export CSV** des traces de décision : `AgentId, Tick, BeliefCount, GoalCount, ChosenAction, Utility`.
- **Export JSON** des données complètes pour analyse externe.

---

## Points restés ouverts dans ce document
- Aucun. La nomenclature des événements (prototype : `tick_summary`, `agent_spawned`, `agent_died`, `decision_made`) s'élargit à la nomenclature V0.1 (§2) — à aligner sur les types d'`ExternalEvent` de SYNE au moment de l'implémentation.