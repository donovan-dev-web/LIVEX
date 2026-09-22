# EMERGENCE_INDICATORS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `METRICS_SPEC.md`
**Source Monographie** : §4.4, §4.10.3

---

## 1. Le score d'émergence composite

ECHOS calcule un **score d'émergence** composite (borné sur **[0, 1]**) à partir des métriques :

```text
EmergenceScore = (
    BeliefDiversity × 0.15
    + GoalDiversity × 0.15
    + DiffusionSpeed_Norm × 0.10
    + ClusteringCoefficient × 0.15
    + LoopStrength × 0.20
    + (ActiveGroups / 100) × 0.25
)

avec  DiffusionSpeed_Norm = clamp(1 - InformationDiffusionSpeed / 100, 0, 1)
```

- Chaque terme est **normalisé sur [0, 1]** (`DiffusionSpeed_Norm` convertit la vitesse de diffusion, exprimée en ticks pour atteindre 80 % des entités, en valeur croissante avec la vitesse).
- Les **poids somment à 1.0**, donc le score est borné.
- **Implémenté (issue #379, ECHOS-030)** : moteur `EmergenceIndicators` (`echos/analysis/emergence.py`), valeur de référence 0.7585336 sur `snapshot_analysis.json`, bornes et rejeu bit-à-bit prouvés par le **jalon J3**.

> ⚠️ **Avertissement méthodologique** : ce score est une **heuristique d'observation**, pas une preuve scientifique d'émergence.

> **Convention de neutralité V0.1 (ECHOS-006)** : vitesse de diffusion **non mesurée** (absente ou ≤ 0 — « jamais diffusé », `InformationDiffusionSpeed` neutre) → contribution **0.0** au lieu du max de la formule (une vitesse nulle signifierait une diffusion instantanée, inobservable). La formule s'applique aux vitesses mesurées (> 0).

## 2. Les phénomènes auto-détectés

ECHOS peut signaler automatiquement des phénomènes :

| Phénomène | Identifiant (contrat) | Condition de détection |
| :-- | :-- | :-- |
| Formation de communauté | `CommunityFormation` | `NumberOfCommunities > 2` |
| Dynamiques de rétroaction complexes | `FeedbackLoops` | `IdentifiedLoops > 5` |
| Coordination collective | `CollectiveCoordination` | `GoalConvergence > 0.7` |
| Goulot d'information | `InformationBottleneck` | `NetworkCentrality > 0.3` |
| Dynamiques organisationnelles | `OrganizationalDynamics` | `ActiveGroups > 5 ET MemberTurnoverRate > 0.1` |

**Implémenté (issue #380, ECHOS-031)** : sortie `DetectedPhenomena` — liste de dictionnaires `{identifier, label (français), description, signals}` où `signals` est la **trace des signaux déclencheurs** `[{metric, value, threshold}, …]` (tous les signaux de la condition, avec valeurs observées et seuils). Ordre d'émission stable (ordre du tableau ci-dessus). Sur la fixture d'analyse, seul `InformationBottleneck` est détecté (centralité 1.0 > 0.3).

## 3. La complexité du système

```text
SystemComplexity = (BeliefDiversity + GoalDiversity + DiffusionSpeed) / 3
```

Approximation de la **complexité de Kolmogorov** à partir de la diversité des représentations.

**Implémenté (issue #382, ECHOS-033)** : `DiffusionSpeed` = `InformationDiffusionSpeed` brute (ticks). La formule mélange des échelles (entropies ~1.5–1.9 et ticks, ex. 10.0 sur la fixture → 4.5023) : résultat **non borné**, conformément à la spec — une normalisation optionnelle (ex. via `DiffusionSpeed_Norm`) est notée comme dérivée possible.

## 4. L'indice d'imprévisibilité

```text
UnpredictabilityIndex = LoopStrength × DecisionVariability
```

Mesure de la capacité du système à produire des résultats non anticipés.

## 5. Décisions d'implémentation (jalon ECHOS ph3)

| Point | Décision | Justification |
| :-- | :-- | :-- |
| **Formule `EmergenceScore`** | Formule ECHOS (§1), **sans** la division `/5` du prototype Monographie §4.4.1 | Les poids somment à 1.0 → score déjà borné [0,1] ; le `/5` du prototype est contradictoire (max 0.2). Validée par la valeur de référence 0.7585336 |
| **`DecisionVariability`** | Résolue [OUVERTE] : `UnpredictabilityIndex = LoopStrength × DecisionDiversity` | `DecisionVariability` n'est produit par **aucun** moteur (et le prototype se contredit lui-même, propriété `DecisionDiversity` vs référence `DecisionVariability`) ; `DecisionDiversity` (actions distinctes / entités vivantes) est la mesure la plus proche de la « variabilité des décisions » |
| **`SystemComplexity`** | Formule littérale (§3) | Spec inchangée ; incohérence d'échelle documentée (voir §3) |
| **`DiffusionSpeed_Norm`** | Neutralité étendue : vitesse non mesurée → 0.0 | Convention ECHOS-006 (données manquantes → neutres) ; voir encadré §1 |

---

## Points restés ouverts dans ce document
- Les poids du score composite (0.15/0.10/0.20/0.25) sont [HÉRITÉ] de la Monographie — leur sensibilité pourra être étudiée sans changer la formule (valeur de référence conservée pour comparaison).
- Normalisation éventuelle de `SystemComplexity` (échelles hétérogènes, §3) — dérivée possible, non tranchée.