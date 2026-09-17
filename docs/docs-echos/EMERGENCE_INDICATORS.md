# EMERGENCE_INDICATORS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `METRICS_SPEC.md`
**Source Monographie** : §4.4

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

> ⚠️ **Avertissement méthodologique** : ce score est une **heuristique d'observation**, pas une preuve scientifique d'émergence.

## 2. Les phénomènes auto-détectés

ECHOS peut signaler automatiquement des phénomènes :

| Phénomène | Condition de détection |
| :-- | :-- |
| Formation de communauté | `NumberOfCommunities > 2` |
| Dynamiques de rétroaction complexes | `IdentifiedLoops > 5` |
| Coordination collective | `GoalConvergence > 0.7` |
| Goulot d'information | `NetworkCentrality > 0.3` |
| Dynamiques organisationnelles | `ActiveGroups > 5 ET MemberTurnoverRate > 0.1` |

## 3. La complexité du système

```text
SystemComplexity = (BeliefDiversity + GoalDiversity + DiffusionSpeed) / 3
```

Approximation de la **complexité de Kolmogorov** à partir de la diversité des représentations.

## 4. L'indice d'imprévisibilité

```text
UnpredictabilityIndex = LoopStrength × DecisionVariability
```

Mesure de la capacité du système à produire des résultats non anticipés.

---

## Points restés ouverts dans ce document
- Les poids du score composite (0.15/0.10/0.20/0.25) sont [HÉRITÉ] de la Monographie — leur sensibilité pourra être étudiée sans changer la formule (valeur de référence conservée pour comparaison).