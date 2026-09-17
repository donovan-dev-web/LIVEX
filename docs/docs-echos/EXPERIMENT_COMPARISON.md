# EXPERIMENT_COMPARISON.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/DETERMINISM.md`
**Source Monographie** : §4.6

---

## 1. Le principe

ECHOS permet de comparer plusieurs **runs contrôlés** :

```text
Expérience 1 : seed=12345, sociabilité=0.2
Expérience 2 : seed=12345, sociabilité=0.8
```

Même seed et même état initial, **un seul paramètre modifié**. La comparaison permet d'étudier l'effet de cette variable sur les trajectoires. Le déterminisme de SYNE (`../docs-syne/DETERMINISM.md`) garantit que la différence observée provient du paramètre, pas du hasard.

## 2. Les métriques de reproductibilité

| Métrique | Définition |
| :-- | :-- |
| `IsReproducible` | Booléen : même seed ET même config |
| `ReproducibilityScore` | `1.0` si reproductible, sinon `1.0 - (cognitiveDiff + socialDiff) / 2` |
| `CognitiveDiff` | Distance L2 normalisée entre distributions de croyances |
| `SocialDiff` | Distance L2 normalisée entre réseaux sociaux |

## 3. Le format d'export

Les résultats d'une expérience peuvent être exportés en **CSV ou JSON** (et Parquet en V0.1 pour séries lourdes) :

- **Identification du run** : seed, configuration, population, durée, version du moteur.
- **Métriques temporelles** : séries de tous les indicateurs.
- **Événements** : journal complet ou échantillonné.
- **État final** : monde, entités, ressources.

---

## Points restés ouverts dans ce document
- Aucun. Le format d'export final (schéma exact des fichiers CSV/JSON/Parquet) sera stabilisé à l'implémentation avec la couche stockage.