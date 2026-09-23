# EXPERIMENT_COMPARISON.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 23 septembre 2026
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

## 4. Implémentation livrée (jalon ECHOS ph7, ECHOS-070 → 072)

Module **`echos/echos/analysis/reproducibility.py`** et endpoint **`GET /api/compare`** (API_REST.md §3.9) :

- **`IsReproducible`** = même **seed** ET même **version** du moteur ET contenu observé **bit-à-bit identique** (empreinte SHA-256 canonique du run : séries de métriques, résumés de tick, événements, contextes `agents`/`groups`/`phenomena`, traces de décision — l'étiquette `run_id` est exclue).
- **`ReproducibilityScore`** = `1.0` si reproductible, sinon `1.0 − (CognitiveDiff + SocialDiff)/2` (arrondi 6 décimales).
- **`CognitiveDiff`** : norme L2 **normalisée** (bornée [0, 1]) entre les distributions de croyances (clés `subject|predicate|value`, somme = 1 — comparable entre populations de tailles différentes), au dernier contexte `agents`.
- **`SocialDiff`** : norme L2 normalisée entre les réseaux de confiance (poids de paire = moyenne des reliances des deux directions, réseau non orienté, clés `min|max`).
- **Export comparatif** (`format=csv`, ECHOS-072) : séries comparatives **alignées** sur les ticks/métriques communs — colonnes `tick,engine,metric,run_a_value,run_b_value,diff` (`diff = run_b_value − run_a_value`) ; `format=json` renvoie métadonnées + distances + séries alignées.
- **Déterminisme ECHOS** (ECHOS-071) : fonctions pures, clés triées, aucune dépendance temporelle ni PRNG — mêmes runs ⇒ mêmes méta-métriques (deux magasins peuplés du même protocole => valeurs strictement identiques) ; 11 tests `tests/test_compare.py` (reproductibilité, divergence de seed, distance nulle/non-nulle sur croyances/confiance, stabilité entre runs et entre appels, séries alignées JSON/CSV, erreurs 404/400/422).

---

## Points restés ouverts dans ce document
- Aucun. Le schéma exact des exports comparatif (CSV/JSON) est stabilisé à l'implémentation ph7 (ECHOS-072) ; le Parquet « séries lourdes » reste optionnel (V0.1+).