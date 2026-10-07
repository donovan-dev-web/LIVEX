# ANALYSIS_FOUNDATIONS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `METRICS_SPEC.md`, `METRICS_DICTIONARY.md`, `EMERGENCE_INDICATORS.md`, `CAUSAL_ANALYSIS.md`, `EXPERIMENT_COMPARISON.md`, `LIMITATIONS.md`
**Source Monographie** : — (document indépendant ; toutes les formules sont alignées sur le code d'implémentation `echos/analysis/`)

---

## 1. Introduction

### 1.1 Rôle de ce document

Ce document expose les **fondements scientifiques et mathématiques** de
l'analyse ECHOS : les équations effectivement implémentées, leur justification,
leur portée exacte et leur interprétation. Il comble un espace que les autres
documents ne couvrent pas :

| Document | Ce qu'il répond |
| :-- | :-- |
| `METRICS_SPEC.md` | *Quoi* mesurer — noms, unités, statuts, dénominateurs |
| `METRICS_DICTIONARY.md` | *Comment* afficher — unités, échelles, conventions de lecture |
| `EMERGENCE_INDICATORS.md` | *Comment* composer — score d'émergence, contributions, phénomènes |
| **`DYNAMIC_VIABILITY_INDEX.md`** | *Comment juger le régime* — **[DRAFT]** DVI : composantes de viabilité, noyau géométrique, couverture κ, régimes, post-run (successeur conceptuel d'`EmergenceScore`, non implémenté) |
| `CAUSAL_ANALYSIS.md` | *Comment* reconstruire — chaîne causale, limites de l'inférence |
| **`ANALYSIS_FOUNDATIONS.md`** | ***Pourquoi* et *avec quelle formule*** — fondements, équations, interprétation |

Ce document fait foi pour la **description des équations**. En cas d'écart avec
le code, le code fait foi — et tout écart doit être corrigé ici (revue de
changement, `METRICS_SPEC.md` §13).

### 1.2 Périmètre

Le périmètre couvre **tout le cœur analytique** d'ECHOS :

- les **outils mathématiques partagés** (§3) — entropie, variance, rapports
  bornés — utilisés par plusieurs moteurs ;
- les **équations des 7 moteurs de métriques** (§4–§10) ;
- les **indices composites** (§11) — `EmergenceScore`, `SystemComplexity`,
  détection de phénomènes ;
- les **méta-métriques de reproductibilité** (§12) — distances, empreintes,
  score de comparaison ;
- les **méthodes statistiques post-run** (§13) — statistiques descriptives,
  pente par moindres carrés, viabilité ;
- l'**analyse causale** (§14) — reconstruction de chaînes hors-ligne.

Les 61 fiches du catalogue (`GET /api/metrics/catalog`) restent la source de
vérité pour les métadonnées (unité, fenêtre, statut, avertissement) ; ce
document explique le **calcul** derrière chaque fiche.

### 1.3 Convention de lecture d'une section d'équation

Chaque équation est présentée selon une trame fixe :

| Champ | Contenu |
| :-- | :-- |
| **Nom** | Identifiant public de l'équation ou de la fonction |
| **Explication globale** | Ce que l'équation mesure, en langage clair |
| **Expression mathématique** | Formule exacte, en écriture scientifique, avec la définition de chaque symbole |
| **Pourquoi cette équation** | Choix scientifique : propriétés recherchées, alternatives écartées |
| **Usage dans le projet** | Fichier:ligne d'implémentation, moteurs et métriques qui l'emploient, constantes associées |
| **Interprétation** | Lecture d'une valeur basse / haute / nulle ; bornes ; comparabilités autorisées |

### 1.4 Notation

- `n` : population considérée (entités vivantes, `aliveCount`).
- `k` : nombre de catégories observées dans une distribution.
- `pᵢ` : probabilité (ou part) de la catégorie *i*, `pᵢ = cᵢ / N` où `cᵢ` est
  l'effectif de la catégorie et `N = Σᵢ cᵢ` le total.
- `log₂` : logarithme en base 2 (bits).
- `[a, b]` : intervalle fermé borné.
- Les valeurs `0.0` publiées distinguent **zéro observé** (mesure réelle) de
  **non mesuré** (`measured = false`, repli neutre) — voir §2.3.

---

## 2. Principes fondateurs de la mesure

Ces principes ne sont pas des équations : ce sont les **contraintes
méthodologiques** qui gouvernent toutes les formules du document. Les ignorer
produit des interprétations fausses, quelles que soient les valeurs affichées.

### 2.1 Neutralité de l'observateur

ECHOS est un **pur observateur** (règle d'or, `LIMITATIONS.md`). Aucune
métrique ne modifie l'état de SYNE. Aucune formule ne dépend d'un générateur
aléatoire, d'un horodatage d'émission ou d'un état interne non publié :
mêmes entrées → mêmes sorties (déterminisme strict, ECHOS-027).

### 2.2 Déterminisme d'émission

Les distributions, comptages et listes sont **triés** (identifiants, clés
lexicographiques) avant émission. Deux exécutions sur le même snapshot
produisent des séries bit-à-bit identiques. Ce déterminisme est la condition
de comparabilité entre runs (§12).

### 2.3 Zéro observé ≠ non mesuré

C'est la distinction la plus importante pour interpréter une courbe :

| Affichage | Signification mathématique | Cause |
| :-- | :-- | :-- |
| `0.0`, `measured = true` | **Zéro observé** — la formule a été exécutée et le résultat est nul | Fenêtre publiée, aucune occurrence |
| `0.0`, `measured = false` | **Non mesuré** — repli neutre, aucune valeur n'a pu être calculée | Donnée absente du snapshot |
| `null` | **Pas d'observation à ce tick** | Tick non couvert par la fenêtre |

Le drapeau `measured` est calculé par le mécanisme `REQUIRES` de chaque
moteur (`_common.py:69-105`) : une métrique n'est déclarée mesurée que si la
clé de contexte dont elle dépend est réellement publiée. Une fenêtre
d'événements **vide** est une observation (« zéro observé ») ; une fenêtre
**absente** est « non mesuré » (`_common.py:108-118`).

**Conséquence d'interprétation** : un `0.0` non mesuré ne doit jamais être lu
comme « le phénomène est absent ». Il indique que **l'instrument n'a rien vu**,
pas que le monde est vide.

### 2.4 Repli neutre

En l'absence de données, les moteurs publient `0.0` (neutre) plutôt que de
lever une erreur ou d'inventer une valeur. Ce repli est **toujours** accompagné
de `measured = false` : le neutre ne doit jamais être confondu avec une mesure.
Aucune formule ne produit d'infini : les divisions par zéro sont neutralisées
par `safe_ratio` (§3.5).

### 2.5 Règle d'or

> **ECHOS ne doit jamais transformer une métrique en vérité scientifique.**
> Un score d'émergence, une entropie ou une densité reste une mesure
> particulière d'un phénomène, jamais une preuve de l'existence d'une
> intelligence, d'une société ou d'un phénomène émergent.
> (`VISION.md`, `LIMITATIONS.md`, ECHOS-032.)

Toute interprétation d'une valeur produite par les équations de ce document
reste **sous responsabilité de l'analyste**. Les statuts `exploratory` du
catalogue signalent les mesures qui n'ont fait l'objet d'aucune campagne de
calibration.

---

## 3. Outils mathématiques partagés

Ces fonctions sont implémentées dans `echos/analysis/_common.py` et réutilisées
par plusieurs moteurs. Elles constituent le socle sur lequel reposent les
métriques des §4–§11.

### 3.1 Entropie de Shannon

**Nom** : Entropie de Shannon — `shannon(counter)`

**Explication globale** : Mesure l'**incertitude** (ou la dispersion) d'une
distribution de catégories. Plus les catégories sont nombreuses et bien
réparties, plus l'entropie est élevée. Une distribution concentrée sur une
seule catégorie a une entropie nulle.

**Expression mathématique** :

```math
H(P) = -\sum_{i=1}^{k} p_i \cdot \log_2(p_i)
```

avec :

- `P = (p₁, p₂, …, pₖ)` : distribution de probabilités, `pᵢ = cᵢ / N`,
  `N = Σᵢ cᵢ` (total des effectifs) ;
- `cᵢ` : effectif observé de la catégorie *i* ;
- `k` : nombre de catégories **observées** (celles dont `cᵢ > 0`) ;
- convention : `0 · log₂(0) = 0` (les catégories absentes ne contribuent pas).

**Bornes** : `H(P) ≥ 0`. Maximum théorique : `H_max = log₂(k)`, atteint lorsque
toutes les catégories ont la même probabilité (`pᵢ = 1/k` pour tout *i*).

**Pourquoi cette équation** :

- L'entropie de Shannon est la mesure d'incertitude **canonique** en théorie de
  l'information (Shannon, 1948). Elle est **additive** sur les sources
  indépendantes et **maximale** pour la distribution uniforme — deux propriétés
  qui la rendent adaptée à la description d'une population cognitive ou
  comportementale.
- Elle est **invariante par permutation** des catégories : seule la répartition
  compte, pas l'ordre des étiquettes. C'est essentiel pour des mesures de
  diversité où l'identité des catégories varie d'un run à l'autre.
- Une alternative (indice de Simpson, Gini) aurait été possible, mais
  l'entropie est la seule à être à la fois **bornée supérieurement par
  log₂(k)** (normalisation naturelle, §3.2) et **additive**, ce qui justifie
  sa réutilisation dans les indices composites (§11).

**Usage dans le projet** : `echos/analysis/_common.py:121-131` (`shannon`).

| Métrique | Moteur | Objet de la distribution |
| :-- | :-- | :-- |
| `BeliefDiversity` | CognitiveDiversity | Triplets (sujet, prédicat, valeur) |
| `GoalDiversity` | CognitiveDiversity, GoalConvergence | Catégories d'objectifs actifs |

**Interprétation** :

- `H = 0` : toute la population est concentrée sur une seule catégorie (aucune
  diversité mesurable).
- `H` croissante : la population se répartit sur plus de catégories, avec une
  répartition plus uniforme.
- **Unité : bits.** Une entropie brute **n'est pas comparable** entre
  populations de tailles différentes ni entre espaces de catégories de
  dimensions différentes : `H = 3 bits` sur 8 catégories uniformes ≠ `H = 3
  bits` sur 16 catégories uniformes (dans le premier cas, `H = log₂(8) = 3` est
  le maximum ; dans le second, `H = 3 < log₂(16) = 4` signale une
  sous-répartition). Pour comparer des dispersions hétérogènes, utiliser la
  version normalisée (§3.2).

### 3.2 Entropie de Shannon normalisée

**Nom** : Entropie normalisée — `shannon_normalized(counter)`

**Explication globale** : L'entropie de Shannon ramenée à son maximum
atteignable pour le nombre de catégories observées. Le résultat est borné
`[0, 1]` : `1.0` signifie répartition parfaitement uniforme, `0.0`
concentration totale.

**Expression mathématique** :

```math
H^*(P) = \frac{H(P)}{\log_2(k)} = \frac{-\sum_{i=1}^{k} p_i \cdot \log_2(p_i)}{\log_2(k)}
```

avec, en plus des symboles de §3.1 :

- `k` : nombre de catégories **observées** (`len(counter)`).

**Bornes** : `H*(P) ∈ [0, 1]`. Cas limites implémentés : `k < 2` → `0.0`
(une seule catégorie ou aucun échantillon : aucune diversité observée).

**Pourquoi cette équation** :

- L'entropie brute croît avec `log₂(k)` : additionner des entropies hétérogènes
  (croyances sur 50 triplets vs objectifs sur 4 catégories) produit une somme
  dominée par le terme au plus grand `k`, ce qui **sature le `clamp` final**
  et détruit toute discrimination dans la partie haute d'un indice composite.
  C'est précisément le problème que la refonte P2 du moteur `EmergenceIndicators`
  a corrigé.
- La normalisation par `log₂(k)` est la **seule** qui donne un intervalle
  `[0, 1]` sans paramètre supplémentaire : elle mesure la fraction de
  l'incertitude maximale **réellement mobilisée** par la distribution.
- La normalisation porte sur les catégories **observées**, pas sur le nombre de
  catégories **possibles** (souvent inconnu du moteur). C'est une limite
  assumée : `H*` mesure la répartition effective, pas la richesse potentielle
  de l'espace.

**Usage dans le projet** : `echos/analysis/_common.py:134-150`
(`shannon_normalized`).

| Métrique | Moteur | Alimente aussi |
| :-- | :-- | :-- |
| `BeliefDiversityNorm` | CognitiveDiversity | `EmergenceScore` (via `ContributionBeliefDiversity`), `SystemComplexity` |
| `GoalDiversityNorm` | CognitiveDiversity | `EmergenceScore` (via `ContributionGoalDiversity`), `SystemComplexity` |
| `ActionDiversity` | CognitiveDiversity | — (sortie propre) |

**Interprétation** :

- `H* = 0` : distribution dégénérée (une seule catégorie observée).
- `H* → 1` : répartition quasi uniforme sur les `k` catégories observées.
- `H* ∈ (0, 1)` : dispersion partielle — la valeur se lit comme la fraction de
  diversité maximale effectivement observée.
- **Unité : fraction [0, 1].** Deux `H*` mesurés sur des populations aux
  espaces de catégories différents sont **comparables** en ordre de grandeur
  (c'est le but de la normalisation), mais pas strictement identiques en
  signification : `H* = 0.8` sur 4 catégories et `H* = 0.8` sur 20 catégories
  décrivent des situations différentes, même si le degré de dispersion relatif
  est le même.

### 3.3 Moyenne arithmétique

**Nom** : Moyenne — `mean(values)`

**Explication globale** : Valeur centrale d'une liste de mesures. C'est le
premier moment empirique d'un échantillon.

**Expression mathématique** :

```math
\bar{x} = \frac{1}{N} \sum_{i=1}^{N} x_i
```

avec :

- `xᵢ` : *i*-ème mesure de la liste ;
- `N` : nombre de mesures (`len(values)`).

**Cas limite implémenté** : liste vide → `0.0` (repli neutre, §2.4).

**Pourquoi cette équation** :

- La moyenne est le **résumé d'ordre 1** le plus interprétable : elle répond à
  « quelle est la valeur typique ? ». Elle est insensible à la forme de la
  distribution (contrairement à la médiane pour les petits échantillons) et
  suffit quand la variance est faible ou quand la question porte sur un total
  rapporté.
- Pour les grandeurs **bornées** (confiances `[0,1]`, ratios de capacité
  `[0,1]`, parts de population), la moyenne reste dans l'intervalle — propriété
  conservée par toute combinaison linéaire de valeurs dans un convexe. Aucune
  saturation possible.
- Le calcul utilise `sum` Python (précision flottante standard) ; le rapport
  de calibration (`calibration.py`) utilise `math.fsum` (somme exacte) pour
  éviter l'accumulation d'erreur sur les longues séries.

**Usage dans le projet** : `echos/analysis/_common.py:153-155` (`mean`).

Utilisée par : `AverageTrustLevel`, `AverageOutDegree`, `AverageGoalAge`,
`AverageCommunitySize`, `AverageGroupLifetime`, `TraitExpressionDiversity`,
`BeliefDisagreement` (moyenne des taux par fait), `RepeatedActionShare`,
`RecoveryTime`, `MemberExitsPerDissolution`, `DissolvedGroupSuccessShare`,
`ResourceFillRatio`, `TheoreticalHopDecay`, `ClusteringCoefficient` (moyenne
des coefficients locaux).

**Interprétation** :

- La moyenne se lit comme la **valeur typique** de la grandeur sur la
  population ou la fenêtre considérée.
- Elle est **sensible aux extrêmes** : un agent extrême tire la moyenne. Pour
  détecter une hétérogénéité, croiser avec une variance (§3.4).
- Comparer deux moyennes n'est légitime que si les **dénominateurs** (champ
  `population` du catalogue) et les **fenêtres** d'observation sont identiques
  ou explicitement rapportés.

### 3.4 Variance de population

**Nom** : Variance — `variance(values)`

**Explication globale** : Dispersion moyenne des mesures autour de leur
moyenne. La variance de population (et non d'échantillon) est retenue : les
moteurs décrivent la **population entière** observée au tick, pas un échantillon
extrait d'une population plus large.

**Expression mathématique** :

```math
\sigma^2 = \frac{1}{N} \sum_{i=1}^{N} (x_i - \bar{x})^2
```

avec :

- `x̄` : moyenne de la liste (§3.3) ;
- `N` : nombre de mesures.

**Convention `ddof=0`** : le dénominateur est `N`, pas `N − 1`. C'est le
dénominateur de la **variance de population** ; la variance d'échantillon
(ddof=1) supposerait que les entités observées sont un tirage aléatoire d'une
plus grande population — hypothèse non vérifiée ici.

**Bornes** : `σ² ≥ 0`. `σ² = 0` : toutes les mesures sont identiques.
`σ² = 0.0` aussi sur liste de longueur `< 2` (repli neutre : un seul point ne
permet pas de mesurer une dispersion).

**Pourquoi cette équation** :

- La variance est le **résumé d'ordre 2** canonique de la dispersion. Elle est
  directement liée à l'entropie pour les distributions discrètes (inégalité de
  Cramér-Rao) et à la déviation standard `σ = √σ²` pour l'unité d'origine.
- La convention `ddof=0` est cohérente avec la philosophie ECHOS : on mesure
  ce qui est **observé**, pas ce qui pourrait exister hors observation. Une
  variance d'échantillon sur-estime la dispersion de la population réelle
  observée.
- L'unité de la variance est le **carré** de l'unité source (`unité²`) — ce
  qui interdit toute lecture comme une valeur de la grandeur de base
  (`METRICS_DICTIONARY.md`).

**Usage dans le projet** : `echos/analysis/_common.py:158-163` (`variance`).

| Métrique | Moteur | Grandeur dont la variance est mesurée |
| :-- | :-- | :-- |
| `BeliefConfidenceVariance` | CognitiveDiversity | Confiances déclarées sur l'ensemble des croyances |
| `TrustVariance` | SocialComplexity | Poids de confiance déclarés sur le graphe |
| `TraitExpressionDiversity` | CognitiveDiversity | Variance par trait, moyennée sur les traits |

**Interprétation** :

- `σ² = 0` : homogénéité parfaite (toutes les confiances identiques, tous les
  traits exprimés à la même valeur).
- `σ²` croissante : hétérogénéité croissante dans la population.
- **Unité : carré de l'unité source.** `BeliefConfidenceVariance` est en
  `unité² (confiance)` — sa valeur ne se lit ni ne se compare à une confiance.
  Pour une lecture dans l'unité d'origine, utiliser l'écart-type `σ = √σ²`.
- La variance est **non robuste aux extrêmes** : un agent atypique peut la
  dominer. Croiser avec la moyenne (§3.3) pour distinguer « dispersion large »
  de « valeurs extrêmes rares ».

### 3.5 Rapport borné

**Nom** : Rapport sûr — `safe_ratio(numerator, denominator)`

**Explication globale** : Division qui neutralise le cas `dénominateur = 0`
plutôt que de produire une valeur infinie ou une erreur.

**Expression mathématique** :

```math
\mathrm{safe\_ratio}(a, b) =
\begin{cases}
a / b & \text{si } b \neq 0 \\[3pt]
0.0 & \text{si } b = 0
\end{cases}
```

**Pourquoi cette équation** :

- Une division par zéro dans un pipeline d'analyse propage `inf` ou `NaN` dans
  les moyennes, les seuils de détection et les indices composites — jusqu'à
  corrompre le score d'émergence entier. L'alternative historique
  (`max(x, 1e-9)`) transformait un cas « rien à mesurer » en une valeur de
  l'ordre de `10¹¹`, qui se propageait silencieusement.
- Le repli `0.0` est **neutre** au sens de §2.4 : il est toujours accompagné
  de `measured = false` quand le dénominateur est structurellement absent.
  Quand le dénominateur est réellement nul (population vide, aucune
  dissolution observée), `0.0` est le résultat mathématique attendu (« aucune
  part à mesurer »).
- Cette neutralisation ne produit **jamais** d'infini — invariant vérifié par
  les tests.

**Usage dans le projet** : `echos/analysis/_common.py:171-179` (`safe_ratio`).

Utilisée par la quasi-totalité des rapports : densité, couverture, parts de
population, taux d'événements, concordances, ratios de capacité.

**Interprétation** :

- `safe_ratio(a, 0) = 0.0` ne signifie pas « rapport nul » mais « **rapport non
  calculable** » — le drapeau `measured` distingue les deux cas.
- Toute lecture d'un ratio doit préalablement vérifier que son dénominateur est
  publié (champ `population` de la fiche catalogue).

### 3.6 Fonction de borne

**Nom** : Borne — `clamp(value, low = 0.0, high = 1.0)`

**Explication globale** : Ramène une valeur dans l'intervalle `[low, high]`.

**Expression mathématique** :

```math
\mathrm{clamp}(x, a, b) = \max\bigl(a,\; \min(b, x)\bigr)
```

**Pourquoi cette équation** :

- Le `clamp` est une **garde de bornes**, pas une normalisation. Il protège les
  indices composites contre les dépassements accidentels (arrondis, valeurs
  héritées hors plage) sans modifier les valeurs déjà dans l'intervalle.
- Il ne doit **jamais** être le mécanisme principal de normalisation : c'est
  exactement le travers que la refonte P2 a corrigé dans `EmergenceScore`,
  où un `clamp` final masquait la saturation des entropies brutes. Depuis P2,
  chaque composante du score est déjà bornée `[0,1]` par sa propre
  normalisation (§3.2, §11.3) ; le `clamp` final n'est plus qu'une sécurité.
- `clamp` est aussi utilisé pour normaliser les contributions individuelles du
  score composite.

**Usage dans le projet** : `echos/analysis/_common.py:166-168` (`clamp`).

Utilisée par : `shannon_normalized`, `ActionDistributionBalance`,
`EmergenceScore`, `SystemComplexity`, `CoverageDelay_Norm`, les six
contributions du composite.

**Interprétation** :

- Une valeur bornée par `clamp` a été **coupée** : sa vraie valeur calculée
  était hors de l'intervalle. Dans une fiche catalogue, la plage théorique est
  publiée (champ `domain`) — si la valeur observée atteint le bord, vérifier
  si le bord est un maximum naturel ou une saturation.

### 3.7 Indice de concordance

**Nom** : Probabilité concordante (indice de Simpson / Herfindahl) —
`GoalCategoryConcordance`

**Explication globale** : Probabilité que deux tirages **indépendants** de la
population tombent sur la **même catégorie**. C'est la concentration de
Herfindahl-Hirschman (HHI) d'une distribution, exprimée comme une probabilité
de collision.

**Expression mathématique** :

```math
C(P) = \sum_{i=1}^{k} p_i^2
```

avec, en plus des symboles de §3.1 :

- `pᵢ = cᵢ / N` : part de la catégorie *i* ;
- `k` : nombre de catégories observées ;
- `N = Σᵢ cᵢ` : total des effectifs.

**Bornes** : `C(P) ∈ [1/k, 1]`.

- `C = 1` : une seule catégorie (`k = 1`) — concordance totale.
- `C = 1/k` : distribution uniforme — concordance minimale.

**Cas limite implémenté** : `N = 0` ou `n < 2` (moins de deux entités) → `0.0`.

**Pourquoi cette équation** :

- La somme des carrés est l'**espérance de la probabilité de collision** pour
  deux tirages indépendants avec remise : `P(X₁ = X₂) = Σᵢ pᵢ²`. C'est une
  grandeur probabiliste directement interprétable, contrairement à un indice
  de concentration brut.
- Elle est **liée à l'entropie** par l'inégalité `C(P) ≥ 2^{−H(P)}` (liaison
  entropie-concentration) : une distribution à forte entropie a une faible
  concordance, et inversement. Les deux mesures sont donc complémentaires sans
  être redondantes — l'une mesure la dispersion, l'autre la collision.
- L'ex-`CooperationPotential` affirmait une « compatibilité par paire » qui
  était **scientifiquement injustifiée** : aucun comportement de coopération
  n'est observé, aucune compatibilité n'est testée. Le renommage P1
  (`GoalCategoryConcordance`) dit exactement ce qui est calculé.

**Usage dans le projet** : `echos/analysis/goal_convergence.py:67-72`.

Métrique : `GoalCategoryConcordance` (moteur `GoalConvergenceMetrics`).
Entrée : distribution des catégories d'objectifs actifs
(`goals[].kind`, repli `currentAction` / `Idle`). Non mesurée si aucune
entité ne déclare d'objectif (`REQUIRES`, §2.3).

**Interprétation** :

- `C → 1` : la population est concentrée sur une catégorie de buts — deux
  entités tirées au hasard ont presque toujours le même type d'objectif.
- `C → 1/k` : les buts sont répartis uniformément — aucune catégorie ne
  domine.
- `C` est une mesure de **concordance de catégories**, pas de coopération ni
  de coordination : le fait que deux entités partagent une catégorie de buts
  ne dit rien de leurs interactions.
- **Unité : fraction [0, 1].** Comparer deux valeurs de `C` n'a de sens que
  si les espaces de catégories sont de taille comparable — `C = 0.5` sur
  `k = 2` (uniforme) et `C = 0.5` sur `k = 10` (très concentré) sont des
  situations radicalement différentes.

---

## 4. Diversité cognitive

Moteur : `CognitiveDiversityMetrics` (`echos/analysis/cognitive_diversity.py`).
Les 12 métriques de ce moteur mesurent la séparation des croyances et des
comportements entre entités.

### 4.1 BeliefDiversity

**Nom** : Diversité des croyances (bits) — `BeliefDiversity`

**Explication globale** : Entropie de Shannon (§3.1) de la distribution des
triplets (sujet, prédicat, valeur) observés dans les croyances de la
population.

**Expression mathématique** :

```math
\mathrm{BeliefDiversity} = H(P_b) = -\sum_{i} p_i \cdot \log_2(p_i)
```

où `P_b` est la distribution des triplets `(subject, predicate, value)` comptés
sur l'ensemble des croyances déclarées par les entités vivantes.

**Pourquoi cette équation** :

- La croyance est un **énoncé** : `(sujet, prédicat, valeur)`. Deux entités qui
  croient la même chose partagent le même triplet ; la dispersion de ces
  triplets mesure la **variété des états de croyance** de la population.
- L'entropie brute (bits) est la mesure native de cette dispersion : elle croît
  avec le nombre d'énoncés distincts et leur uniformité. La version normalisée
  (§4.2) est celle qui alimente les composites.
- La confiance associée à chaque croyance n'entre **pas** dans cette mesure :
  `BeliefDiversity` ne mesure que la variété des énoncés, pas leur degré
  d'adhésion (celui-ci est porté par `BeliefConfidenceVariance`, §4.4).

**Usage dans le projet** : `cognitive_diversity.py:164`, via
`shannon(belief_counter)` (`_common.py:121-131`).

**Interprétation** :

- `BeliefDiversity = 0` : toute la population croit exactement les mêmes
  énoncés (synchronie cognitive totale).
- `BeliefDiversity` croissante : plus d'énoncés distincts, répartis plus
  uniformément.
- **Unité : bits.** Non comparable entre populations de tailles différentes ni
  entre espaces de croyances de dimensions différentes (§3.1). Pour une
  lecture comparable, utiliser `BeliefDiversityNorm` (§4.2).

### 4.2 BeliefDiversityNorm

**Nom** : Diversité des croyances normalisée — `BeliefDiversityNorm`

**Explication globale** : Entropie des croyances ramenée à son maximum
atteignable (§3.2). C'est **elle** qui alimente les indices composites.

**Expression mathématique** :

```math
\mathrm{BeliefDiversityNorm} = \frac{H(P_b)}{\log_2(k_b)}
```

où `k_b` est le nombre de triplets de croyances **observés** (`len(counter)`).

**Pourquoi cette équation** :

- La version brute (§4.1) ne peut pas être additionnée à d'autres termes dans
  un indice composite sans saturer le `clamp` : elle croît avec
  `log₂(k_b)`, qui varie fortement d'un run à l'autre. La normalisation par
  `log₂(k_b)`, bornée `[0,1]`, rend la contribution **additive** et
  **comparable**.
- La normalisation sur les catégories observées (et non possibles) est un choix
  méthodologique assumé : le moteur ne connaît pas l'espace complet des
  croyances possibles dans le monde simulé.

**Usage dans le projet** : `cognitive_diversity.py:168`, via
`shannon_normalized(belief_counter)` (`_common.py:134-150`).

Alimente : `ContributionBeliefDiversity` du `EmergenceScore` (poids 0.15) et
`SystemComplexity` (§11.2, §11.4). Non mesurée si aucune croyance n'est
déclarée (`REQUIRES`, `cognitive_diversity.py:78`).

**Interprétation** :

- `BeliefDiversityNorm = 0` : concentration totale sur un triplet.
- `BeliefDiversityNorm → 1` : répartition quasi uniforme sur les triplets
  observés.
- **Unité : fraction [0, 1].** Comparabilité inter-runs plus grande que la
  version brute, sous réserve que les espaces de croyances soient de
  dimensions comparables (à vérifier via le nombre de triplets publiés).

### 4.3 BeliefDisagreement

**Nom** : Désaccord sur les croyances — `BeliefDisagreement`

**Explication globale** : Part moyenne des croyances qui s'écartent de la
valeur **majoritaire** pour un même fait, moyennée sur les faits qui
admettent au moins deux valeurs distinctes.

**Expression mathématique** :

```math
\mathrm{BeliefDisagreement} = \frac{1}{|F_{\mathrm{div}}|} \sum_{f \in F_{\mathrm{div}}} \frac{n_f - m_f}{n_f}
```

avec :

- `F_div` : ensemble des faits `(sujet, prédicat)` pour lesquels au moins
  **deux valeurs distinctes** sont observées (les faits unanimes sont exclus) ;
- `n_f` : nombre total de croyances déclarées pour le fait *f* ;
- `m_f` : effectif de la valeur **majoritaire** du fait *f* (ex-aequo tranché
  par la valeur lexicographiquement **la plus petite**,
  `cognitive_diversity.py:112`) ;
- `|F_div|` : cardinal de `F_div` ;
- cas limite : `F_div` vide → `0.0` (aucun fait ne prête à divergence).

**Pourquoi cette équation** :

- La diversité de Shannon (§4.1) mesure la **variété** des énoncés pris
  globalement ; elle ne dit pas si deux entités **concourent sur le même fait**
  avec des valeurs différentes. `BeliefDisagreement` cible précisément ce cas :
  c'est un taux de **contestation locale**, pas une dispersion globale.
- Exclure les faits unanimes est essentiel : un fait que tout le monde partage
  ne produit aucun désaccord, et l'inclure diluerait artificiellement le taux.
- Le choix majoritaire déterministe (plus petite valeur en cas d'ex-aequo)
  garantit le déterminisme d'émission (§2.2) sans arbitraire de `max`.

**Usage dans le projet** : `cognitive_diversity.py:92-117`
(`_belief_disagreement`), appelée ligne 169.

**Interprétation** :

- `BeliefDisagreement = 0` : sur chaque fait observé par au moins deux
  entités, toutes les entités déclarent la même valeur (unanimité par fait).
- `BeliefDisagreement → 1` : sur chaque fait contesté, la quasi-totalité des
  croyances s'écarte de la valeur majoritaire (contestation maximale).
- **Unité : fraction [0, 1].** La valeur se lit comme le **taux moyen de
  dissidence** par fait contesté — pas comme une proportion de la population
  totale. Un monde où un seul fait est contesté à 100 % et où tous les autres
  sont unanimes produit `BeliefDisagreement = 1.0`, ce qui ne signifie pas que
  « toute la population est en désaccord ».

### 4.4 BeliefConfidenceVariance

**Nom** : Variance des confiances — `BeliefConfidenceVariance`

**Explication globale** : Variance de population (§3.4) des poids de confiance
déclarés sur l'ensemble des croyances de la population.

**Expression mathématique** :

```math
\mathrm{BeliefConfidenceVariance} = \sigma^2(C) = \frac{1}{N_c} \sum_{j} (c_j - \bar{c})^2
```

avec :

- `cⱼ` : poids de confiance de la *j*-ème croyance déclarée
  (`confidence ∈ [0,1]`) ;
- `c̄` : moyenne de ces confiances (§3.3) ;
- `N_c` : nombre total de croyances déclarées ;
- `ddof=0` (variance de population, §3.4) ;
- cas limite : `N_c = 0` → `0.0` (repli neutre).

**Pourquoi cette équation** :

- La confiance est le **degré d'adhésion** associé à un énoncé. Sa variance
  mesure l'**hétérogénéité des niveaux d'adhésion** dans la population — un
  monde où toutes les entités croient avec la même intensité a une variance
  nulle, indépendamment de la variété des énoncés.
- C'est une mesure d'**hétérogénéité**, pas d'**exactitude** : aucune vérité
  de référence n'est disponible dans le monde simulé pour évaluer si une
  confiance est « juste ». La fiche catalogue le dit explicitement.
- La variance est en `unité² (confiance)` — sa valeur ne se lit pas comme une
  confiance (§3.4).

**Usage dans le projet** : `cognitive_diversity.py:170-172`, via
`variance(confidences)` (`_common.py:158-163`).

**Interprétation** :

- `BeliefConfidenceVariance = 0` : toutes les confiances déclarées sont
  identiques (adhésion homogène).
- `BeliefConfidenceVariance` croissante : coexistence de croyances très
  assurées et de croyances très hésitantes.
- **Unité : `unité² (confiance)`** — carré d'une grandeur bornée `[0,1]`, donc
  `σ² ∈ [0, 0.25]` dans le pire cas (une confiance à 0, une à 1 : `σ² = 0.25`).
  Ne jamais comparer cette valeur à une confiance simple.

### 4.5 GoalDiversity et GoalDiversityNorm

**Nom** : Diversité des objectifs (bits / fraction) — `GoalDiversity`,
`GoalDiversityNorm`

**Explication globale** : Entropie de Shannon (§3.1) et sa version normalisée
(§3.2) de la distribution des **catégories d'objectifs actifs** de la
population.

**Expression mathématique** :

```math
\mathrm{GoalDiversity} = H(P_g) = -\sum_{i} p_i \cdot \log_2(p_i)

\mathrm{GoalDiversityNorm} = \frac{H(P_g)}{\log_2(k_g)}
```

où `P_g` est la distribution des catégories d'objectifs, extraite par
`goal_kinds(agents)` (`_common.py:213-215`) : chaque entité contribue son
premier objectif déclaré (`goals[].kind`), avec repli sur `currentAction`
puis `"Idle"`.

**Pourquoi ces équations** :

- La distribution des **buts** est un des indicateurs les plus directs de
  convergence ou de divergence collective. L'entropie mesure précisément
  cette dispersion.
- La version normalisée (`GoalDiversityNorm`) est celle qui entre dans
  `EmergenceScore` (poids 0.15) et `SystemComplexity` : elle est bornée
  `[0,1]` et additive (§3.2).
- Le repli sur `currentAction` / `Idle` est **supprimé** quand aucune entité
  ne déclare d'objectif : dans ce cas, `GoalDiversity` et `GoalDiversityNorm`
  sont **non mesurées** (`REQUIRES = _goal_observed`,
  `cognitive_diversity.py:75-77`). Présenter une distribution d'actions comme
  une distribution de buts serait une erreur d'interprétation.

**Usage dans le projet** : `cognitive_diversity.py:173-174`.

`GoalDiversityNorm` alimente aussi `GoalConvergenceMetrics.GoalDiversity`
(même calcul, moteur redondant assumé — `goal_convergence.py:76`).

**Interprétation** :

- `GoalDiversity = 0` : toute la population poursuit le même type d'objectif.
- `GoalDiversity` croissante : plus de catégories de buts, réparties plus
  uniformément.
- **Unités** : `GoalDiversity` en `bits` (non comparable inter-populations) ;
  `GoalDiversityNorm` en `fraction [0,1]` (comparable en ordre de grandeur).
- Une `GoalDiversityNorm` élevée indique une **diversité de buts**, pas une
  absence de coordination : deux groupes poursuivant des buts différents mais
  coordonnés en leur sein produisent la même valeur qu'un monde chaotique.

### 4.6 GoalConvergence

**Nom** : Convergence des objectifs — `GoalConvergence`

**Explication globale** : Part de la population dans la catégorie d'objectif
**la plus fréquente**. C'est la probabilité qu'une entité tirée au hasard
appartienne à la catégorie dominante.

**Expression mathématique** :

```math
\mathrm{GoalConvergence} = \frac{\max_i c_i}{n}
```

avec :

- `c_i` : effectif de la catégorie d'objectifs *i* ;
- `n` : population vivante (`aliveCount`) ;
- `max_i(c_i)` : effectif de la catégorie dominante.

**Bornes** : `GoalConvergence ∈ [1/k, 1]` quand la distribution est complète ;
`0.0` si `goal_counter` est vide (repli neutre).

**Pourquoi cette équation** :

- La mesure la plus simple et la plus lisible de **domination d'une catégorie** :
  elle répond directement à la question « quelle part de la population
  converge vers le même but ? ».
- Contrairement à l'entropie (qui mesure la dispersion globale),
  `GoalConvergence` isole la **catégorie dominante** : un monde à 3 catégories
  (60 % / 20 % / 20 %) a une entropie non nulle mais une convergence élevée
  (0.60). Les deux mesures sont complémentaires.
- Le dénominateur est `aliveCount` (population totale), pas le nombre
  d'entités ayant un objectif déclaré : les entités sans objectif (repli
  `Idle`) comptent dans le dénominateur, ce qui **réduit** artificiellement la
  convergence. C'est un choix conservative : la mesure ne surestime jamais la
  convergence d'une population dont une partie est inactive.

**Usage dans le projet** : `cognitive_diversity.py:152`, et
`goal_convergence.py:60` (moteur `GoalConvergenceMetrics`, nom
`GlobalGoalAlignment` dans ce moteur — même formule, deux publications).

Déclenche aussi le phénomène `CollectiveCoordination` (`GoalConvergence > 0.7`,
§11.5).

**Interprétation** :

- `GoalConvergence → 1` : quasi toute la population partage le même type
  d'objectif (convergence forte).
- `GoalConvergence → 1/k` : les buts sont uniformément répartis (aucune
  catégorie ne domine).
- **Unité : fraction [0, 1].** La valeur se lit comme la part de la population
  dans la catégorie dominante — interprétable directement (« 72 % des entités
  cherchent de la nourriture »).
- Une convergence élevée n'implique **ni** coopération **ni** coordination :
  c'est une concordance de catégories, pas un comportement observé (§3.7).

### 4.7 GoalCoverage

**Nom** : Couverture des objectifs — `GoalCoverage`

**Explication globale** : Part d'entités vivantes qui déclarent **au moins un
objectif** à ce tick.

**Expression mathématique** :

```math
\mathrm{GoalCoverage} = \frac{\bigl|\{a \in \mathcal{A} : \mathrm{goals}(a) \neq \emptyset\}\bigr|}{n}
```

avec :

- `A` : ensemble des entités vivantes observées ;
- `goals(a)` : liste des objectifs déclarés par l'entité `a` ;
- `n` : population vivante (`aliveCount`) ;
- cas limite : `n = 0` → `0.0` (`safe_ratio`, §3.5).

**Pourquoi cette équation** :

- La mesure expose le **taux de dotation en objectifs** de la population. Sans
  elle, une `GoalDiversity` calculée sur un sous-ensemble d'entités serait
  indiscernable d'une `GoalDiversity` calculée sur la population entière.
- C'est un **dénominateur de contrôle** : une `GoalConvergence` élevée avec un
  `GoalCoverage` faible signifie que la convergence ne porte que sur une
  minorité d'entités actives — lecture que la seule `GoalConvergence` ne
  permet pas.
- Le nom `GoalCoverage` (P1) remplace un repli silencieux qui fusionnait
  « distribution de buts » et « repli sur l'action courante ».

**Usage dans le projet** : `cognitive_diversity.py:153, 180`.

**Interprétation** :

- `GoalCoverage = 1.0` : toute la population vivante déclare au moins un
  objectif.
- `GoalCoverage` croissante vers 0 : une part croissante de la population est
  sans objectif déclaré (activité `Idle` ou repli sur `currentAction`).
- **Unité : fraction [0, 1].** Lire systématiquement `GoalCoverage` à côté de
  `GoalDiversity` et `GoalConvergence` : ces trois métriques forment un
  triptyque (couverture → diversité → convergence) qui, séparément, prête à
  confusion.

### 4.8 ActionDiversity

**Nom** : Diversité des actions décidées — `ActionDiversity`

**Explication globale** : Entropie de Shannon **normalisée** (§3.2) de la
distribution des actions effectivement décidées au tick, lues dans les
événements `decision_made`.

**Expression mathématique** :

```math
\mathrm{ActionDiversity} = \frac{H(P_a)}{\log_2(k_a)}
```

où `P_a` est la distribution des actions issues de `decision_actions(snapshot)`
(`_common.py:361-376`) — événements `decision_made` du tick courant, champ
`action` (repli `value.intention`).

**Pourquoi cette équation** :

- La mesure porte sur les **décisions observées**, jamais sur les objectifs :
  les deux notions sont distinctes et ne doivent pas être confondues sous la
  même métrique (décision P1, `cognitive_diversity.py:10-13`). Un agent peut
  avoir un objectif stable et produire des actions variées, ou l'inverse.
- L'entropie **normalisée** est retenue (et non brute) parce que la taille du
  répertoire d'actions varie d'un run à l'autre : une normalisation `[0,1]`
  rend la mesure comparable en ordre de grandeur entre runs à répertoires de
  tailles différentes.
- `REQUIRES = event_window_published` : sans fenêtre d'événements publiée,
  la métrique est **non mesurée** — une fenêtre vide est un zéro observé
  (§2.3).

**Usage dans le projet** : `cognitive_diversity.py:177`, via
`shannon_normalized(decision_counter)`. Accompagnée de `DecisionCount`
(dénominateur publié, §4.9).

**Interprétation** :

- `ActionDiversity = 0` : toutes les décisions du tick sont identiques (action
  unique).
- `ActionDiversity → 1` : les décisions se répartissent uniformément sur les
  actions observées.
- **Unité : fraction [0, 1].** Croiser avec `DecisionCount` : une entropie
  calculée sur 2 décisions est statistiquement fragile, quelle que soit sa
  valeur.

### 4.9 DecisionCount

**Nom** : Nombre de décisions — `DecisionCount`

**Explication globale** : Nombre brut d'événements `decision_made` observés
dans la fenêtre d'événements du tick courant.

**Expression mathématique** :

```math
\mathrm{DecisionCount} = \bigl|\{e \in E : \mathrm{type}(e) = \texttt{decision\_made}\}\bigr|
```

où `E` est l'ensemble des événements du snapshot. `DecisionCount = 0.0`
observé (fenêtre publiée) est un **zéro observé** ; sans fenêtre, la métrique
est **non mesurée** (`REQUIRES`, §2.3).

**Pourquoi cette équation** :

- C'est un **dénominateur de contrôle** pour `ActionDiversity` : une entropie
  sans effectif est non interprétable. Publier les deux séparément respecte la
  règle « jamais de ratio sans son dénominateur » (`METRICS_DICTIONARY.md` §1).
- Le comptage est brut (pas de normalisation par population ni par tick) :
  c'est la grandeur élémentaire, dont dérivent les taux.

**Usage dans le projet** : `cognitive_diversity.py:178`.

**Interprétation** :

- `DecisionCount = 0` : aucune décision n'a été décidée à ce tick (ou fenêtre
  non publiée — distinguer via `measured`).
- **Unité : count** (entier). Ne pas confondre `0` observé avec `non mesuré`.

### 4.10 AverageGoalAge

**Nom** : Âge moyen des objectifs — `AverageGoalAge`

**Explication globale** : Moyenne (§3.3) des âges des objectifs actifs de la
population, lus dans `goals[].age`.

**Expression mathématique** :

```math
\mathrm{AverageGoalAge} = \frac{1}{|G|} \sum_{g \in G} \mathrm{age}(g)
```

avec :

- `G` : ensemble des objectifs actifs déclarés par les entités vivantes ;
- `age(g)` : durée d'engagement de l'objectif `g`, en ticks ;
- cas limite : `|G| = 0` → `0.0` (repli neutre).

**Pourquoi cette équation** :

- L'âge d'un objectif est une **durée d'engagement** : il mesure depuis
  combien de temps l'entité poursuit ce but. Le nom `AverageGoalAge` (P1)
  remplace l'ex-`IntentionStability`, qui affirmait une stabilité que la
  formule ne mesurait pas : un âge élevé peut provenir d'un objectif
  **stable** ou d'un objectif **bloqué**, et la mesure seule ne distingue pas
  les deux cas.
- La moyenne est le résumé le plus lisible ; aucune dispersion n'est publiée
  séparément (lacune tracée dans le catalogue).

**Usage dans le projet** : `cognitive_diversity.py:179`, via
`mean(goal_ages)` (`cognitive_diversity.py:120-126`).

**Interprétation** :

- `AverageGoalAge` croissante : les entités poursuivent leurs buts depuis plus
  longtemps (engagement prolongé ou objectifs qui ne se renouvellent pas).
- `AverageGoalAge` faible : rotation rapide des objectifs ou population
  récemment initialisée.
- **Unité : ticks** (durée). Ne pas lire comme une « stabilité » : la
  stabilité supposerait un suivi d'identité d'objectif dans le temps, que
  cette mesure ne fournit pas.

### 4.11 TraitExpressionDiversity

**Nom** : Diversité d'expression des traits — `TraitExpressionDiversity`

**Explication globale** : Moyenne (§3.3), sur les traits présents dans la
population, de la variance de population (§3.4) de chaque trait.

**Expression mathématique** :

```math
\mathrm{TraitExpressionDiversity} = \frac{1}{|\mathcal{T}|} \sum_{t \in \mathcal{T}} \sigma_t^2
```

avec :

- `T` : ensemble des noms de traits déclarés (`traits` dictionnaire des
  agents) ;
- `σ²_t` : variance de population des valeurs du trait `t` sur la population ;
- cas limite : `|T| = 0` → `0.0` (repli neutre).

**Pourquoi cette équation** :

- Les traits sont des **variables individuelles** (tempérament, préférences…)
  dont l'expression peut varier d'une entité à l'autre. La variance par trait
  mesure cette dispersion ; la moyenne sur les traits donne un résumé global
  d'hétérogénéité phénotypique.
- Une seule métrique agrège les traits : publier une variance par trait
  exploserait le nombre de métriques pour un gain d'interprétation limité. La
  moyenne des variances est le compromis retenu.
- L'unité est en `unité² (traits)` — carré de l'unité source du trait, qui
  peut varier d'un trait à l'autre. La moyenne de variances à unités
  hétérogènes est donc une **grandeur composite** : elle mesure un ordre de
  grandeur de dispersion, pas une quantité physique précise.

**Usage dans le projet** : `cognitive_diversity.py:180`, via
`_trait_expression_variance` (`cognitive_diversity.py:129-141`).

**Interprétation** :

- `TraitExpressionDiversity = 0` : tous les traits sont exprimés à la même
  valeur sur toute la population (homogénéité phénotypique totale).
- `TraitExpressionDiversity` croissante : dispersion croissante des expressions
  de traits.
- **Unité : `unité² (traits)`** — ne pas comparer à une valeur de trait. La
  moyenne de variances à unités hétérogènes est un résumé d'ordre de grandeur.

---

## 5. Propagation de l'information

Moteur : `InformationPropagationMetrics`
(`echos/analysis/information_propagation.py`).

Les 6 métriques de ce moteur mesurent la circulation **observée** de
l'information à partir des événements `message_sent`. **Aucune ne prouve qu'une
information ait été reçue** : le moteur observe les émissions, pas les
réceptions.

Constantes :

```math
\texttt{\_DECAY\_PER\_HOP} = 0.9 \quad (\text{taux de rétention par saut, 10 \% de perte})

\texttt{\_DIFFUSION\_COVERAGE} = 0.8 \quad (\text{part de la population qui doit avoir émis})
```

### 5.1 MessageVolume

**Nom** : Volume de messages — `MessageVolume`

**Explication globale** : Nombre de messages envoyés au tick courant,
rapporté à la population vivante.

**Expression mathématique** :

```math
\mathrm{MessageVolume} = \frac{\bigl|\{m \in M : \mathrm{tick}(m) = \mathrm{tick}_{\mathrm{courant}}\}\bigr|}{n}
```

avec :

- `M` : événements `message_sent` de la fenêtre d'événements ;
- `n` : population vivante (`aliveCount`) ;
- `tick_courant` : tick du snapshot analysé ;
- cas limite : `n = 0` → `0.0` (`safe_ratio`, §3.5).

**Pourquoi cette équation** :

- Le volume brut de messages croît mécaniquement avec la population : un monde
  de 100 entités produit plus de messages qu'un monde de 10, à comportement
  identique. Le rapport par `aliveCount` **normalise** la mesure, la rendant
  comparable entre runs de populations différentes.
- L'unité publiée (`messages/entité/tick`) est un **débit par entité** : elle
  se lit comme « combien de messages envoie une entité moyenne à ce tick ».
- `MessageVolume` est exclue de `REQUIRES` : elle compte le tick courant et
  reste mesurée même quand aucun message n'est parti (`0.0` y est une mesure
  réelle, `information_propagation.py:58-59`).

**Usage dans le projet** : `information_propagation.py:137`.

**Interprétation** :

- `MessageVolume = 0` : aucun message envoyé à ce tick (zéro observé).
- `MessageVolume` croissante : débit de communication par entité en hausse.
- **Unité : messages/entité/tick.** Ne pas confondre avec un volume total :
  un `MessageVolume` élevé peut provenir d'une population réduite très
  bavarde ou d'une population nombreuse peu communicative — vérifier
  `aliveCount` ou `SenderCoverage` (§5.6) pour distinguer les cas.

### 5.2 EmitterCoverageDelay

**Nom** : Délai de couverture des émetteurs — `EmitterCoverageDelay`

**Explication globale** : Nombre de ticks écoulés entre le premier message
observé dans la fenêtre et le tick où **80 % des entités vivantes** ont émis
au moins un message.

**Expression mathématique** :

```math
\mathrm{EmitterCoverageDelay} = \mathrm{tick}_{80\%} - \mathrm{tick}_{\mathrm{premier\,message}}
```

avec :

- `tick_premier_message` : tick du premier message observé dans la fenêtre ;
- `tick(80 %)` : premier tick où
  `|{a ∈ A : a a émis au moins un message}| ≥ 0.8 · n` ;
- `_DIFFUSION_COVERAGE = 0.8` : seuil de couverture (constante,
  `information_propagation.py:68`) ;
- cas limites : aucun message → `0.0` ; population nulle → `0.0` ; seuil non
  atteint dans la fenêtre → `0.0` (signalé par `SenderCoverage`).

**Pourquoi cette équation** :

- La mesure est une **amplitude** (différence de ticks), pas un tick absolu.
  La version précédente renvoyait le tick lui-même, ce qui croissait
  linéairement avec la longueur du run : `CoverageDelay_Norm = 1 − délai/100`
  tombait à 0 après le tick 100 quel que soit le comportement réel du réseau,
  et `SystemComplexity` devenait non borné. La correction P1 est documentée
  dans `information_propagation.py:76-90`.
- Le seuil de 80 % (`_DIFFUSION_COVERAGE`) est une **hypothèse de couverture** :
  il faut qu'une grande majorité de la population ait émis pour considérer
  l'information comme « propagée aux émetteurs ». Ce seuil n'est pas calibré
  sur des runs de référence.
- La mesure porte sur les **émetteurs** uniquement — jamais sur les
  réceptions. `SenderCoverage` (§5.6) accompagne la mesure pour que la
  couverture soit lisible (« 18/24 émetteurs »).

**Usage dans le projet** : `information_propagation.py:76-105`
(`_diffusion_speed`), appelée ligne 138.

Alimente `CoverageDelay_Norm` du `EmergenceScore` (poids 0.10, §11.3) et
`SystemComplexity` (§11.4). `REQUIRES = event_window_published`.

**Interprétation** :

- `EmitterCoverageDelay = 0` : le seuil de 80 % est atteint dès le premier
  tick observé (diffusion immédiate parmi les émetteurs) — ou, si
  `measured = false`, la donnée n'est pas disponible.
- `EmitterCoverageDelay` croissante : les émetteurs mettent plus de temps à
  couvrir 80 % de la population.
- **Unité : ticks** (durée). Ne pas lire comme un tick absolu ni comme une
  vitesse. Une valeur élevée signale une **couverture lente des émetteurs**,
  pas nécessairement une information non reçue.

### 5.3 TheoreticalHopDecay

**Nom** : Dégradation théorique par saut — `TheoreticalHopDecay`

**Explication globale** : Transformation déterministe du nombre de sauts des
messages observés, sous l'hypothèse **non calibrée** d'une perte de fiabilité
de 10 % par saut.

**Expression mathématique** :

```math
\mathrm{TheoreticalHopDecay} = \frac{1}{|H|} \sum_{h \in H} \bigl(1 - 0.9^{\,h}\bigr)
```

avec :

- `H` : ensemble des compteurs de sauts (`hops`) des messages observés dans la
  fenêtre ;
- `_DECAY_PER_HOP = 0.9` : taux de rétention par saut (constante,
  `information_propagation.py:65`) — 10 % de perte par saut ;
- cas limite : aucun message avec compteur de sauts → `0.0`.

**Pourquoi cette équation** :

- L'ex-`RumorAccuracyDegradation` affirmait une mesure de précision observée.
  La formule n'est en réalité qu'une **transformation déterministe** du nombre
  de sauts : aucun contenu n'est vérifié à la réception, aucune précision n'est
  mesurée. Le renommage P1 dit exactement ce qui est calculé.
- L'hypothèse de 10 %/saut est un **modèle de dégradation exponentielle**
  simple : après `h` sauts, la fiabilité théorique est `0.9^h`, donc la
  dégradation cumulée est `1 − 0.9^h`. C'est un modèle de référence, pas une
  mesure.
- La moyenne sur l'ensemble des sauts observés donne un résumé de la
  fenêtre : plus les messages ont voyagé loin (hops élevés), plus la
  dégradation théorique est grande.

**Usage dans le projet** : `information_propagation.py:123-125, 141`.

Statut catalogue : `exploratory` — l'hypothèse de 10 %/saut n'a fait l'objet
d'aucune campagne de calibration. `REQUIRES = event_window_published`.

**Interprétation** :

- `TheoreticalHopDecay → 0` : les messages observés ont un nombre de sauts
  faible (circulation locale) — la dégradation théorique est faible.
- `TheoreticalHopDecay → 1` : les messages observés ont beaucoup voyagé (hops
  élevés) — la dégradation théorique est forte.
- **Unité : fraction [0, 1].** Cette valeur est un **modèle**, pas une mesure :
  elle ne dit rien de la précision réelle de l'information propagée. La
  publier comme telle (statut `exploratory`) est une exigence du contrat.

### 5.4 MaxMessageHops

**Nom** : Nombre maximal de sauts — `MaxMessageHops`

**Explication globale** : Plus grand compteur de sauts observé parmi les
messages de la fenêtre.

**Expression mathématique** :

```math
\mathrm{MaxMessageHops} = \max_{m \in M} \mathrm{hops}(m)
```

avec :

- `M` : événements `message_sent` de la fenêtre ;
- `hops(m)` : compteur de sauts du message `m` (champ `value.hops`) ;
- cas limite : aucun message avec compteur de sauts → `0.0`.

**Pourquoi cette équation** :

- La mesure élémentaire de la **profondeur de propagation** : combien de sauts
  le message le plus lointain a-t-il parcourus ?
- Le maximum (et non la moyenne) est retenu parce que la question est «
  quelle est l'étendue maximale observée ? » — la moyenne diluerait un message
  exceptionnellement lointain dans une masse de messages locaux.

**Usage dans le projet** : `information_propagation.py:118-122, 142`.
`REQUIRES = event_window_published`.

**Interprétation** :

- `MaxMessageHops = 0` : aucun message avec compteur de sauts, ou tous les
  messages sont de saut 0 (émission directe).
- `MaxMessageHops` croissante : au moins un message a parcouru plus de sauts
  (circulation plus profonde observée).
- **Unité : sauts** (entier). Mesure la **profondeur maximale observée**, pas
  la profondeur moyenne ni la fiabilité à l'arrivée.

### 5.5 SenderConcentration

**Nom** : Concentration des émetteurs — `SenderConcentration`

**Explication globale** : Part du message le plus productif dans le volume
total de messages observés. C'est la **part du principal émetteur**.

**Expression mathématique** :

```math
\mathrm{SenderConcentration} = \frac{\max_a c_a}{|M|}
```

avec :

- `c_a` : nombre de messages envoyés par l'entité `a` dans la fenêtre ;
- `M` : événements `message_sent` de la fenêtre ;
- `|M|` : volume total de messages ;
- cas limites : aucun message → `0.0` (`safe_ratio`) ;
  `SenderConcentration = 1/k` si `k` émetteurs produisent également.

**Pourquoi cette équation** :

- L'ex-`NetworkCentrality` comptait les émetteurs distincts par message — ce
  qui est l'**inverse** d'une concentration et inversait le sens du phénomène
  « goulot d'information ». La correction P1 publie la part du principal
  émetteur, ce qui est la définition standard de concentration (top-1 share).
- La mesure isole le **hub** : un seul émetteur qui concentre tout le volume
  produit `SenderConcentration → 1.0`, tandis qu'une production également
  répartie sur `k` émetteurs produit `SenderConcentration ≈ 1/k`.

**Usage dans le projet** : `information_propagation.py:127-134, 143`.
Déclenche aussi le phénomène `InformationBottleneck`
(`SenderConcentration > 0.3`, §11.5). `REQUIRES = event_window_published`.

**Interprétation** :

- `SenderConcentration → 1` : un seul émetteur concentre presque tout le
  volume de messages (goulot d'information observé).
- `SenderConcentration → 0` (ou `1/k`) : le volume est réparti entre de
  nombreux émetteurs (aucune concentration).
- **Unité : fraction [0, 1].** La valeur se lit comme la part du principal
  émetteur — interprétable directement (« l'émetteur le plus actif produit
  34 % des messages »).
- La concentration des **émissions** ne dit rien des **réceptions** : un hub
  peut émettre beaucoup sans que son message soit reçu.

### 5.6 SenderCoverage

**Nom** : Couverture des émetteurs — `SenderCoverage`

**Explication globale** : Part d'entités vivantes ayant émis **au moins un
message** dans la fenêtre d'observation.

**Expression mathématique** :

```math
\mathrm{SenderCoverage} = \frac{\bigl|\{a \in \mathcal{A} : a \text{ a émis } \ge 1 \text{ message}\}\bigr|}{n}
```

avec :

- `A` : ensemble des entités vivantes ;
- `n` : population vivante (`aliveCount`) ;
- cas limite : `n = 0` → `0.0` (`safe_ratio`).

**Pourquoi cette équation** :

- La mesure rend lisible le **dénominateur de couverture** d'`EmitterCoverageDelay`
  (§5.2) : « 18/24 émetteurs » est plus interprétable qu'un délai seul.
- Elle distingue « réseau où tout le monde parle » de « réseau où seuls les
  hubs parlent » — lecture que `MessageVolume` (§5.1) ne permet pas : un
  volume élevé peut provenir de peu d'émetteurs très actifs.

**Usage dans le projet** : `information_propagation.py:146`.
`REQUIRES = event_window_published`.

**Interprétation** :

- `SenderCoverage = 1.0` : toute la population vivante a émis au moins un
  message dans la fenêtre.
- `SenderCoverage` croissante vers 0 : une part croissante de la population
  n'a pas émis (silence observé).
- **Unité : fraction [0, 1].** Lire systématiquement à côté de
  `EmitterCoverageDelay` et `SenderConcentration` : les trois forment un
  triptyque (qui émet · à quelle vitesse · avec quelle concentration).

---

## 6. Complexité sociale

Moteur : `SocialComplexityMetrics` (`echos/analysis/social_complexity.py`).

Les 7 métriques de ce moteur mesurent la structure du **graphe de confiance**
déclaré par les entités (`trust`, contrat U2). Le graphe est traité comme
**non orienté** pour les métriques de structure (une arête `a—b` existe dès que
l'une des deux directions déclare une confiance positive).

### 6.1 AverageTrustLevel

**Nom** : Niveau moyen de confiance — `AverageTrustLevel`

**Explication globale** : Moyenne (§3.3) de tous les poids de confiance
déclarés sur le graphe.

**Expression mathématique** :

```math
\mathrm{AverageTrustLevel} = \frac{1}{|R|} \sum_{r \in R} \mathrm{trust}(r)
```

avec :

- `R` : ensemble des relations de confiance déclarées (`trust[].trust > 0`,
  poids `0.0` exclu : relation inexistante, `_common.py:244-251`) ;
- `trust(r)` : poids de confiance de la relation `r`, `∈ [0, 1]` ;
- cas limite : `|R| = 0` → `0.0` (repli neutre).

**Pourquoi cette équation** :

- Le poids de confiance est la **grandeur élémentaire** du réseau social : sa
  moyenne donne le niveau de confiance « typique » déclaré dans la population.
- Les poids `0.0` sont exclus : ils signifient « relation inexistante » (pas
  « confiance nulle déclarée »), et les inclure diluerait artificiellement la
  moyenne vers 0 dans un réseau clairsemé.

**Usage dans le projet** : `social_complexity.py:131`.

**Interprétation** :

- `AverageTrustLevel` croissante vers 1 : la population déclare des confiances
  élevées entre ses membres.
- `AverageTrustLevel` croissante vers 0 (ou `0.0` non mesuré) : confiances
  faibles ou aucune relation déclarée — distinguer via `measured` et
  `NetworkDensity` (§6.3).
- **Unité : confiance** (`[0,1]`). La moyenne de poids bornés reste dans
  l'intervalle — aucune saturation possible.

### 6.2 TrustVariance

**Nom** : Variance des confiances — `TrustVariance`

**Explication globale** : Variance de population (§3.4) des poids de confiance
déclarés sur le graphe.

**Expression mathématique** :

```math
\mathrm{TrustVariance} = \sigma^2(T) = \frac{1}{|R|} \sum_{r \in R} \bigl(\mathrm{trust}(r) - \bar{T}\bigr)^2
```

avec, en plus des symboles de §6.1 :

- `T̄` : moyenne des poids de confiance (§6.1) ;
- `ddof=0` (variance de population, §3.4) ;
- cas limite : `|R| < 2` → `0.0`.

**Pourquoi cette équation** :

- La variance mesure l'**hétérogénéité des niveaux de confiance** : un réseau
  où toutes les relations ont le même poids a une variance nulle, quels que
  soient le nombre de relations et le niveau moyen.
- Croisée avec `AverageTrustLevel`, elle distingue « confiance uniformément
  élevée » (moyenne haute, variance faible) de « confiance mixte » (moyenne
  moyenne, variance haute) — deux situations sociales radicalement différentes
  que la moyenne seule ne distingue pas.

**Usage dans le projet** : `social_complexity.py:132`.

**Interprétation** :

- `TrustVariance = 0` : toutes les confiances déclarées sont identiques.
- `TrustVariance` croissante : coexistence de relations très confiantes et de
  relations peu confiantes.
- **Unité : `unité² (confiance)`** — carré de l'unité source (§3.4). Ne pas
  comparer à une confiance simple.

### 6.3 NetworkDensity

**Nom** : Densité du réseau — `NetworkDensity`

**Explication globale** : Rapport entre le nombre d'arêtes observées et le
nombre **maximum** d'arêtes possibles dans un graphe non orienté simple à `n`
nœuds.

**Expression mathématique** :

```math
\mathrm{NetworkDensity} = \frac{|E|}{n \cdot (n - 1) / 2}
```

avec :

- `|E|` : nombre d'arêtes de confiance non orientées (paires dédupliquées,
  poids `> 0`, `_common.py:218-230`) ;
- `n` : population vivante (`aliveCount`) ;
- `n(n−1)/2` : nombre de paires unordered possibles (dénominateur corrigé P1) ;
- cas limites : `n < 2` → `0.0` (`safe_ratio`).

**Pourquoi cette équation** :

- La densité est la mesure **canonique** de la connectivité d'un graphe : elle
  répond à « quelle fraction des relations possibles est réellement
  déclarée ? ».
- Le dénominateur `n(n−1)/2` (et non `n(n−1)`) est celui d'un graphe **non
  orienté** simple : une paire `{a, b}` ne compte qu'une fois, quels que soient
  les sens de la déclaration. L'ancienne formule (`n(n−1)`) bornait la densité
  à 0.5 sur un graphe complet, ce qui **conditionnait toute interprétation**
  d'une comparaison entre runs. La correction P1 est documentée dans
  `social_complexity.py:100-104`.
- La densité est **normalisée** : elle est comparable entre populations de
  tailles différentes (contrairement au nombre brut d'arêtes).

**Usage dans le projet** : `social_complexity.py:104`.

**Interprétation** :

- `NetworkDensity = 0` : aucune relation de confiance déclarée (réseau vide).
- `NetworkDensity = 1` : graphe complet — chaque paire d'entités déclare une
  confiance positive.
- `NetworkDensity` intermédiaire : fraction de relations possibles
  effectivement déclarée.
- **Unité : fraction [0, 1].** Comparabilité inter-runs bonne (même
  dénominateur normalisé), sous réserve que `aliveCount` soit publié.

### 6.4 ClusteringCoefficient

**Nom** : Coefficient de clustering — `ClusteringCoefficient`

**Explication globale** : Moyenne (§3.3) des coefficients de clustering locaux.
Le coefficient local d'un nœud mesure la probabilité que deux de ses voisins
soient eux-mêmes connectés (triangles fermés / paires de voisins possibles).

**Expression mathématique** :

```math
\mathrm{ClusteringCoefficient} = \frac{1}{n} \sum_{a \in \mathcal{A}} C_a

C_a =
\begin{cases}
\dfrac{t_a}{d_a \cdot (d_a - 1) / 2} & \text{si } d_a \ge 2 \\[6pt]
0 & \text{sinon}
\end{cases}
```

avec :

- `t_a` : nombre d'arêtes entre les voisins du nœud `a` (triangles fermés
  adjacents à `a`) ;
- `d_a` : degré du nœud `a` (nombre de voisins, graphe non orienté) ;
- `d_a(d_a − 1)/2` : nombre de paires de voisins possibles ;
- `A` : ensemble des entités vivantes ;
- cas limites : `d_a < 2` → `C_a = 0.0` (pas de paires de voisins).

**Pourquoi cette équation** :

- Le coefficient de clustering est la mesure **canonique** de la
  **transitive locale** d'un réseau : « les amis de mes amis sont-ils mes
  amis ? ». C'est un marqueur de structure communautaire (triangles = groupes
  fermés) distinct de la densité globale (§6.3).
- Le graphe est **fermé** avant le comptage (`social_complexity.py:60-74`) :
  une relation déclarée d'un seul côté (`a → b` sans `b → a`) est quand même
  comptée comme une arête non orientée. Sans cette fermeture, une relation
  unidirectionnelle ne créerait jamais de triangle, ce qui sous-estimerait le
  clustering.
- La moyenne locale (et non le coefficient global de Watts-Strogatz) est
  retenue : elle donne une valeur par nœud, agrégée par la moyenne, ce qui est
  plus robuste aux nœuds isolés et plus simple à interpréter.

**Usage dans le projet** : `social_complexity.py:60-90, 134`.
Alimente `ContributionClustering` du `EmergenceScore` (poids 0.15, §11.2).

**Interprétation** :

- `ClusteringCoefficient → 0` : aucun triangle dans le réseau — les voisins ne
  se connaissent pas entre eux (structure en étoile ou en graphe biparti).
- `ClusteringCoefficient → 1` : chaque nœud a son voisinage entièrement
  connecté (réseau composé de cliques).
- **Unité : fraction [0, 1].** Un clustering élevé signale une **structure
  communautaire locale**, pas nécessairement une grande densité globale :
  un réseau de plusieurs cliques disjoints a un clustering élevé et une
  densité faible.

### 6.5 AverageOutDegree

**Nom** : Degré sortant moyen — `AverageOutDegree`

**Explication globale** : Moyenne (§3.3), sur la population, du degré sortant
de chaque entité, normalisé par `n − 1`. Le degré sortant est le nombre de
voisins vers lesquels l'entité déclare une confiance positive.

**Expression mathématique** :

```math
\mathrm{AverageOutDegree} = \frac{1}{n} \sum_{a \in \mathcal{A}} \frac{\deg^+(a)}{n - 1}
```

avec :

- `deg⁺(a)` : nombre de voisins de `a` (relations sortantes avec poids `> 0`) ;
- `n − 1` : nombre maximum de voisins possibles ;
- `n` : population vivante ;
- cas limites : `n < 2` → `0.0` (`safe_ratio`).

**Pourquoi cette équation** :

- La normalisation par `n − 1` transforme un degré brut en une **part de
  voisinage mobilisée**, comparable entre populations de tailles différentes.
- Le nom `AverageOutDegree` (P1) remplace l'ex-`AverageCentrality`, qui
  laissait croire à une centralité intermédiaire (betweenness). La formule
  mesure un degré sortant, pas une centralité.
- Sur un graphe non orienté, cette moyenne est **équivalente à la densité**
  (facteur de normalisation mis à part) : la redondance est documentée dans
  `social_complexity.py:109-112`. Les deux métriques sont publiées pour
  compatibilité de contrat, mais ne doivent pas être lues comme deux
  informations indépendantes.

**Usage dans le projet** : `social_complexity.py:113-115, 135`.

**Interprétation** :

- `AverageOutDegree → 0` : chaque entité déclare très peu de relations
  sortantes (réseau clairsemé).
- `AverageOutDegree → 1` : chaque entité déclare une confiance vers presque
  tous les autres (graphe complet).
- **Unité : fraction [0, 1].** Redondante avec `NetworkDensity` sur un graphe
  non orienté : ne pas traiter comme une information supplémentaire.

### 6.6 NumberOfCommunities

**Nom** : Nombre de communautés — `NumberOfCommunities`

**Explication globale** : Nombre de communautés de taille **≥ 2** détectées
dans le graphe de confiance par propagation d'étiquettes (§6.8).

**Expression mathématique** :

```math
\mathrm{NumberOfCommunities} = \bigl|\{g \in G : |g| \ge 2\}\bigr|
```

avec :

- `G` : ensemble des communautés produites par `communities(agents)`
  (`_common.py:325-337`) ;
- `|g|` : nombre de membres de la communauté `g` ;
- les **singletons** (entités sans relation de confiance) sont exclus.

**Pourquoi cette équation** :

- Le nombre de communautés est le **comptage élémentaire** de structure
  groupale : il répond à « combien de groupes distincts le réseau contient-il
  ? ».
- Exclure les singletons est essentiel : une entité isolée ne constitue pas un
  groupe. Sans cette règle, un monde sans aucune relation aurait été compté
  comme `N` communautés, déclenchant à tort le phénomène `CommunityFormation`
  (`_common.py:283-289`).
- La détection utilise la propagation d'étiquettes (§6.8), un algorithme de
  détection de communautés déterministe.

**Usage dans le projet** : `social_complexity.py:136`.
Déclenche aussi le phénomène `CommunityFormation`
(`NumberOfCommunities > 2`, §11.5).

**Interprétation** :

- `NumberOfCommunities = 0` : aucune communauté de taille ≥ 2 (réseau vide ou
  uniquement des isolats).
- `NumberOfCommunities` croissant : plus de groupes distincts détectés dans le
  graphe de confiance.
- **Unité : count** (entier). C'est un comptage de communautés **inférées**
  (§6.8), pas un comptage de groupes déclarés par SYNE.

### 6.7 CommunitySizeMatch

**Nom** : Concordance des tailles de communautés — `CommunitySizeMatch`

**Explication globale** : Part des tailles de communautés présentes dans
l'historique dont la taille est **aussi présente** dans le partitionnement
courant. La mesure compare des **tailles**, jamais des membres.

**Expression mathématique** :

```math
\mathrm{CommunitySizeMatch} = \frac{\bigl|\{s \in S_{\mathrm{hist}} : s \in S_{\mathrm{courant}}\}\bigr|}{|S_{\mathrm{hist}}|}
```

avec :

- `S_hist` : ensemble des tailles de communautés de la **dernière entrée** de
  `communityHistory` (`history[-1].get("communities")`) ;
- `S_courant` : ensemble des tailles de communautés du tick courant
  (`community_sizes(agents)`, ordre décroissant) ;
- cas limites : `S_courant` vide ou `S_hist` vide → `0.0` ;
  `REQUIRES = _community_history_published` (§2.3).

**Pourquoi cette équation** :

- La mesure évalue la **stabilité des tailles** de communautés entre deux
  instants : « les groupes ont-ils conservé leur taille ? ».
- Comparer des **tailles** (et non des membres) est un choix assumé et
  documenté (`social_complexity.py:51-56`) : deux communautés disjointes de
  même taille sont comptées « stables ». Une stabilité d'**identité**
  (ex. indice de Jaccard entre partitions) exigerait que le contrat de
  `communityHistory` publie les membres — lacune tracée.
- Sans historique, la mesure est **non mesurée** (et non `1.0`) : un repli sur
  1.0 (stabilité parfaite par défaut) aurait produit un faux positif sur un
  monde sans aucune communauté.

**Usage dans le projet** : `social_complexity.py:117-128, 137`.

**Interprétation** :

- `CommunitySizeMatch = 1.0` : toutes les tailles de l'historique sont
  présentes dans le courant (tailles conservées).
- `CommunitySizeMatch = 0` : aucune taille de l'historique n'est présente dans
  le courant (tailles toutes modifiées) — ou `measured = false`.
- **Unité : fraction [0, 1].** Ne pas lire comme une stabilité d'**identité** :
  des communautés aux mêmes tailles mais aux membres différents produisent
  `CommunitySizeMatch = 1.0`.

### 6.8 Propagation d'étiquettes (méthode de détection de communautés)

**Nom** : Propagation d'étiquettes déterministe — `label_propagation`

**Explication globale** : Algorithme itératif de détection de communautés dans
un graphe. Chaque nœud porte une étiquette initiale (son identifiant) ; à
chaque itération, chaque nœud adopte l'étiquette la plus fréquente parmi ses
voisins. L'algorithme s'arrête quand plus aucune étiquette ne change, ou
après 10 itérations.

**Expression mathématique** :

Pas une équation scalaire, mais une **règle de mise à jour** :

```math
\mathrm{labels}^{(t+1)}(a) = \underset{\ell \in L(a)}{\mathrm{argmin}} \bigl(-\mathrm{count}(\ell),\; \ell\bigr)
```

avec, à chaque itération `t` :

- `L(a)` : ensemble des étiquettes portées par les voisins du nœud `a` ;
- `count(ℓ)` : nombre de voisins de `a` portant l'étiquette `ℓ` ;
- `argmin(−count, ℓ)` : l'étiquette la plus fréquente ; en cas d'ex-aequo, la
  **plus petite** lexicographiquement (déterminisme, §2.2) ;
- mise à jour **asynchrone** (en place) dans l'ordre trié des identifiants ;
- nombre maximal d'itérations : **10** (`_common.py:303`) ;
- étiquettes initiales : identifiants des nœuds.

**Pourquoi cet algorithme** :

- La propagation d'étiquettes est un algorithme **canonique** de détection de
  communautés (Raghavan et al., 2007) : simple, scalable, et naturellement
  déterministe quand l'ordre d'itération et la règle d'ex-aequo sont fixés.
- Le déterminisme est **essentiel** pour ECHOS : deux exécutions sur le même
  graphe doivent produire le même partitionnement, sans PRNG ni ordre
  arbitraire (§2.2). La règle d'ex-aequo (plus petite étiquette) et le tri des
  identifiants garantissent cette propriété.
- La fermeture du voisinage (graphe non orienté) est appliquée avant
  l'itération (`_common.py:291-300`) : une relation déclarée d'un seul sens
  est quand même une connexion sociale.
- Les entités **isolées** conservent leur propre étiquette (singletons) : elles
  restent dans la table d'étiquettes (description complète du partitionnement)
  mais sont exclues de `communities()` (§6.6), qui est la vue « groupes »
  réellement mesurée.

**Usage dans le projet** : `echos/analysis/_common.py:276-322`
(`label_propagation`), appelée par `communities()` (`_common.py:325-337`).

Consommée par : `SocialComplexityMetrics` (`NumberOfCommunities`,
`CommunitySizeMatch`) et `GroupDynamicsMetrics` (`InferredCommunities`,
`AverageCommunitySize`, `CommunityCoverage`).

**Interprétation** :

- Le partitionnement produit est une **inférence structurelle** sur le graphe
  de confiance, pas une déclaration de groupes par SYNE. Les communautés
  inférées peuvent coexister avec des groupes natifs (`group_formed`) sans
  coïncider.
- La convergence en ≤ 10 itérations n'est pas garantie sur tous les graphes :
  si l'algorithme s'arrête sur la limite d'itérations, le partitionnement est
  **partiellement stable** (pas de critère de convergence publié). Dans la
  pratique, 10 itérations suffisent pour la plupart des graphes de taille
  modérée.
- `NumberOfCommunities` et les métriques dérivées décrivent la **topologie
  déclarée** au tick observé : une variation entre deux ticks peut provenir d'un
  changement de graphe **ou** d'une non-convergence de l'algorithme — le contrat
  ne publie pas le nombre d'itérations effectuées.

---

## 7. Convergence des objectifs

Moteur : `GoalConvergenceMetrics` (`echos/analysis/goal_convergence.py`).

### 7.1 GlobalGoalAlignment

**Nom** : Alignement global des objectifs — `GlobalGoalAlignment`

**Explication globale** : Part de la population dans la catégorie d'objectif
la plus fréquente. **Même formule** que `GoalConvergence` (§4.6) publiée par
le moteur `CognitiveDiversityMetrics` — deux moteurs, deux publications d'une
même grandeur.

**Expression mathématique** :

```math
\mathrm{GlobalGoalAlignment} = \frac{\max_i c_i}{n}
```

(voir §4.6 pour la définition complète des symboles).

**Pourquoi cette équation** : identique à §4.6. La redondance entre moteurs
est assumée : les deux moteurs publient une vision complète de leur domaine.

**Usage dans le projet** : `goal_convergence.py:60`.
`REQUIRES = _goal_observed` (§2.3).

**Interprétation** : identique à §4.6. Voir aussi `GoalCategoryConcordance`
(§7.2) pour la lecture complémentaire de concordance.

### 7.2 GoalCategoryConcordance

**Nom** : Concordance des catégories d'objectifs — `GoalCategoryConcordance`

**Explication globale** : Probabilité que deux tirages indépendants de la
population tombent sur la **même catégorie** d'objectifs. Voir §3.7 pour
l'équation générale de l'indice de concordance.

**Expression mathématique** :

```math
\mathrm{GoalCategoryConcordance} = \sum_{i} p_i^2
```

où `pᵢ` est la part de la catégorie d'objectifs *i* dans la distribution
observée (`goal_kinds(agents)`).

**Pourquoi cette équation** : identique à §3.7. Le renommage P1
(`CooperationPotential` → `GoalCategoryConcordance`) a corrigé une affirmation
sémantique injustifiée : le calcul ne teste aucune coopération.

**Usage dans le projet** : `goal_convergence.py:67-72`.
`REQUIRES = _goal_observed`. Cas limite : `n < 2` → `0.0`.

**Interprétation** : identique à §3.7. À croiser avec `GoalDiversity` (§4.5)
et `GlobalGoalAlignment` (§7.1) pour une lecture complète : concordance
(Σ p²), dispersion (H), domination (max p).

### 7.3 GoalTypeCounts

**Nom** : Comptages par type d'objectifs — `GoalTypeCounts`

**Explication globale** : Comptage brut des entités par catégorie d'objectifs,
publié comme sortie structurée (dictionnaire `{catégorie → effectif}`).

**Expression mathématique** :

```math
\mathrm{GoalTypeCounts} = \{\, \text{catégorie}_i \mapsto c_i \,\}
```

où `c_i` est l'effectif de la catégorie *i* dans la distribution des objectifs
(`goal_kinds(agents)`), clés triées (déterminisme, §2.2).

**Pourquoi cette équation** :

- La sortie structurée est le **dénominateur brut** des autres métriques du
  moteur : elle permet de vérifier les parts (`pᵢ = cᵢ / n`) sans recalcul.
- Publier les comptages bruts respecte la règle « jamais de ratio sans son
  dénominateur » (`METRICS_DICTIONARY.md` §1) : `GoalConvergence` (§4.6) se
  lit directement sur `GoalTypeCounts`.
- La sortie n'est **pas** persistée comme métrique numérique (dict, pas
  float) : elle est publiée dans les réponses API mais n'entre pas dans les
  séries temporelles.

**Usage dans le projet** : `goal_convergence.py:78`.

**Interprétation** :

- Sortie structurée (`non numérique` au catalogue) : à afficher telle quelle,
  jamais dans une courbe.
- Les comptages se lisent directement : « Feed = 14, Drink = 8, Idle = 3 ».

---

## 8. Répétitions d'action

Moteur : `FeedbackLoopDetector` (`echos/analysis/feedback_loop_detector.py`).

**Portée réelle (P1)** : le moteur compte des paires `(agent, action)`
**répétées** dans une fenêtre glissante de décisions. Il n'observe ni
conséquence, ni relation action → conséquence, ni retour de la conséquence sur
la décision : ce n'est **pas** une boucle causale. Les mots « boucle »,
« amplification », « critique » et « stabilité » ont été retirés de la
nomenclature publique.

Constantes du moteur :

```math
\texttt{WINDOW\_SIZE} = 100 \quad (\text{ticks de la fenêtre glissante})

\texttt{FREQUENCY\_THRESHOLD} = 2 \quad (\text{fréquence minimale pour compter une répétition})

\texttt{AMPLIFICATION\_FACTOR} = 1.5 \quad (\text{facteur d'amplification théorique})
```

### 8.1 RepeatedActionPairs

**Nom** : Paires (agent, action) répétées — `RepeatedActionPairs`

**Explication globale** : Nombre de paires `(agent, action)` dont la fréquence
dans la fenêtre glissante **dépasse** `FREQUENCY_THRESHOLD` (= 2).

**Expression mathématique** :

```math
\mathrm{RepeatedActionPairs} = \bigl|\{(a, \alpha) : \mathrm{freq}(a, \alpha) > 2\}\bigr|
```

avec :

- `freq(a, α)` : nombre de fois où l'entité `a` a décidé l'action `α` dans la
  fenêtre glissante (`WINDOW_SIZE = 100` entrées récentes de `history`) ;
- `FREQUENCY_THRESHOLD = 2` : seuil strict (`> 2`, donc ≥ 3) ;
- cas limite : fenêtre vide → `0.0` (`REQUIRES = "history"`).

**Pourquoi cette équation** :

- La mesure est un **comptage de répétitions** : elle identifie les
  `(agent, action)` qui se répètent au-delà du bruit fondamental (une action
  prise 2 fois sur 100 ticks n'est pas une répétition notable).
- Le seuil `> 2` est une **constante héritée**, non calibrée : il n'a pas fait
  l'objet de campagne de référence.
- La mesure n'implique **aucune causalité** : répéter une action ne dit rien
  de la raison de la répétition ni de ses conséquences. Pour une analyse
  causale réelle, voir `echos.analysis.causal` (§14).

**Usage dans le projet** : `feedback_loop_detector.py:93-97, 134`.
Déclenche aussi le phénomène `FeedbackLoops`
(`RepeatedActionPairs > 5`, §11.5). `REQUIRES = "history"`.

**Interprétation** :

- `RepeatedActionPairs = 0` : aucune paire `(agent, action)` ne se répète au-
  delà du seuil (ou `measured = false`).
- `RepeatedActionPairs` croissant : plus de paires se répètent dans la fenêtre.
- **Unité : count** (entier). Comptage de **répétitions observées**, pas de
  boucles causales.

### 8.2 RepeatedActionShare

**Nom** : Part des répétitions d'action — `RepeatedActionShare`

**Explication globale** : Moyenne (§3.3), sur les paires répétées, de leur
fréquence rapportée à la taille de la fenêtre. C'est la part moyenne de la
fenêtre occupée par ces répétitions.

**Expression mathématique** :

```math
\mathrm{RepeatedActionShare} = \frac{1}{|L|} \sum_{(a,\alpha) \in L} \frac{\mathrm{freq}(a, \alpha)}{W}
```

avec :

- `L` : ensemble des paires `(agent, action)` répétées (`freq > 2`, §8.1) ;
- `freq(a, α)` : fréquence de la paire dans la fenêtre ;
- `W = max(1, |history|)` : taille effective de la fenêtre ;
- cas limite : `L` vide → `0.0`.

**Pourquoi cette équation** :

- La fréquence **brute** croît avec la taille de la fenêtre : rapporter à `W`
  transforme la mesure en une **part** comparable entre runs à fenêtres de
  tailles différentes.
- La moyenne sur `L` (et non la somme) donne la part **moyenne** par paire
  répétée — pas la part totale, qui serait sensible au nombre de paires.
- Le nom `RepeatedActionShare` (P1) remplace l'ex-`LoopStrength`, qui
  affirmait une force de boucle non mesurée.

**Usage dans le projet** : `feedback_loop_detector.py:100-104, 135`.
Alimente `ContributionRepeatedActions` du `EmergenceScore` (poids 0.20,
§11.2). `REQUIRES = "history"`.

**Interprétation** :

- `RepeatedActionShare = 0` : aucune paire répétée au-delà du seuil.
- `RepeatedActionShare` croissante : les paires répétées occupent une part
  croissante de la fenêtre de décisions.
- **Unité : fraction [0, 1].** Mesure de **part de fenêtre**, pas
  d'amplification ni de danger.

### 8.3 ActionDistributionBalance

**Nom** : Équilibre de la distribution des actions — `ActionDistributionBalance`

**Explication globale** : 1 moins la divergence totale (écart absolu total) de
la distribution observée des actions par rapport à la distribution uniforme.
C'est un indice d'**uniformité** de la distribution, borné `[0, 1]`.

**Expression mathématique** :

```math
\mathrm{ActionDistributionBalance} = \mathrm{clamp}\!\left(1 - \sum_{i} \bigl|p_i - \tfrac{1}{k}\bigr|,\; 0,\; 1\right)
```

avec :

- `pᵢ = cᵢ / N` : part observée de l'action *i* (sur l'ensemble des paires
  `(agent, action)` de la fenêtre, pas seulement les répétées) ;
- `k` : nombre d'actions distinctes observées ;
- `1/k` : probabilité de la distribution uniforme ;
- `∑ᵢ |pᵢ − 1/k|` : divergence totale en variation totale (distance de
  total variation, maximale = 2 pour deux distributions disjointes) ;
- cas limite : aucun comptage → `0.0` (repli neutre).

**Pourquoi cette équation** :

- La **distance de variation totale** `∑|pᵢ − qᵢ|` est une distance de
  probabilité standard, bornée `[0, 2]`. `1 − divergence/2` serait la forme
  normalisée exacte ; ici, `clamp(1 − divergence)` est appliqué
  (`feedback_loop_detector.py:116-123`), ce qui borne la valeur `[0, 1]` mais
  sature à 0 dès que la divergence dépasse 1. C'est un choix d'implémentation
  à connaître : la mesure est **bornée par le clamp**, pas par la normalisation
  théorique.
- Le nom `ActionDistributionBalance` (P1) remplace l'ex-`SystemStability`, qui
  affirmait une stabilité temporelle non mesurée : la distribution uniforme
  n'est pas un état d'équilibre établi du système.
- La distribution observée porte sur **toutes** les paires `(agent, action)` de
  la fenêtre, pas seulement les répétées : l'équilibre se mesure sur l'ensemble
  des décisions.

**Usage dans le projet** : `feedback_loop_detector.py:116-125, 136`.
`REQUIRES = "history"`.

**Interprétation** :

- `ActionDistributionBalance → 1` : distribution quasi uniforme des actions
  (aucune action ne domine).
- `ActionDistributionBalance → 0` : distribution très concentrée (une ou
  quelques actions dominent) — ou divergence ≥ 1 (saturée par le `clamp`).
- **Unité : fraction [0, 1].** Ne pas lire comme une « stabilité du système » :
  c'est une mesure d'**uniformité de distribution** à un instant, pas une
  stabilité temporelle.

### 8.4 AmplifiedRepetitions

**Nom** : Répétitions amplifiées — `AmplifiedRepetitions`

**Explication globale** : Nombre de paires répétées dont la fréquence observée
**dépasse de `AMPLIFICATION_FACTOR`** (= 1.5) la fréquence uniforme attendue.

**Expression mathématique** :

```math
\mathrm{AmplifiedRepetitions} = \bigl|\{(a, \alpha) \in L : \mathrm{freq}(a, \alpha) / f_{\mathrm{attendue}} > 1.5\}\bigr|
```

avec :

- `L` : ensemble des paires répétées (`freq > 2`, §8.1) ;
- `f_attendue = W / k` : fréquence uniforme attendue (taille de fenêtre /
  nombre d'actions distinctes) ;
- `AMPLIFICATION_FACTOR = 1.5` (constante héritée, non calibrée) ;
- cas limites : `k = 0` → `f_attendue = W` ; `L` vide → `0.0`.

**Pourquoi cette équation** :

- La mesure compare la fréquence observée à une **référence théorique**
  (distribution uniforme des actions), pas à un risque observé. Un écart de
  facteur 1.5 à la référence uniforme signale une action qui revient
  **significativement plus souvent** que le hasard uniforme ne le suggérerait.
- Le nom `AmplifiedRepetitions` (P1) remplace l'ex-`CriticalLoops`, qui
  affirmait une criticité non mesurée : la mesure compare à une référence
  théorique, elle n'observe aucun danger.
- Le facteur 1.5 est une **constante héritée**, non calibrée.

**Usage dans le projet** : `feedback_loop_detector.py:108-114, 137`.
`REQUIRES = "history"`.

**Interprétation** :

- `AmplifiedRepetitions = 0` : aucune paire répétée ne dépasse la fréquence
  uniforme attendue de facteur 1.5.
- `AmplifiedRepetitions` croissant : plus de paires reviennent
  significativement plus souvent que la référence uniforme.
- **Unité : count** (entier). Écart à une **référence théorique**, pas un
  danger observé.

### 8.5 RepeatedActionCounts

**Nom** : Comptages des actions répétées — `RepeatedActionCounts`

**Explication globale** : Comptage des paires répétées, classées par nom
d'action. Sortie structurée (`{action → effectif}`).

**Expression mathématique** :

```math
\mathrm{RepeatedActionCounts} = \{\, \mathrm{action}_\beta \mapsto \bigl|\{a : \mathrm{freq}(a, \beta) > 2\}\bigr| \,\}
```

Invariant vérifié par test : `Σ valeurs == RepeatedActionPairs` (§8.1).

**Pourquoi cette équation** :

- La sortie décompose `RepeatedActionPairs` par type d'action : elle permet de
  savoir **quelles** actions se répètent, pas seulement combien de paires.
- Les catégories normatives « positive / négative », codées en dur sans mesure
  d'effet, ont été **retirées** (P2) : le comptage est publié tel quel, sans
  qualification.
- La sortie n'est pas persistée comme métrique numérique (dict) : elle est
  publiée dans les réponses API.

**Usage dans le projet** : `feedback_loop_detector.py:129-140`.

**Interprétation** :

- Sortie structurée (`non numérique`) : à afficher telle quelle.
- L'invariant `Σ == RepeatedActionPairs` garantit la cohérence interne.

---

## 9. Durabilité des ressources

Moteur : `ResourceSustainabilityMetrics`
(`echos/analysis/resource_sustainability.py`).

Constantes :

```math
\texttt{\_CRITICAL\_RATIO} = 0.2 \quad (\text{seuil de crise : part de capacité } < 20\,\%)

\texttt{\_RECOVERED\_RATIO} = 0.8 \quad (\text{seuil de récupération : part de capacité } \ge 80\,\%)
```

### 9.1 ResourceFillRatio

**Nom** : Taux de remplissage des réserves — `ResourceFillRatio`

**Explication globale** : Moyenne (§3.3) des parts de capacité restante des
réserves dont la capacité est publiée.

**Expression mathématique** :

```math
\mathrm{ResourceFillRatio} = \frac{1}{|R_c|} \sum_{r \in R_c} \mathrm{clamp}\!\left(\frac{\mathrm{quantity}(r)}{\mathrm{capacity}(r)},\; 0,\; 1\right)
```

avec :

- `R_c` : ensemble des réserves dont `capacity` est publiée et `> 0` ;
- `quantity(r)` : quantité restante de la réserve `r` ;
- `capacity(r)` : capacité publiée de la réserve `r` ;
- `clamp(·, 0, 1)` : borne haute à 1.0 — une réserve au-delà de sa capacité
  publiée est **saturée** (compter un remplissage de 300 % rendrait les
  moyennes inexploitables, `resource_sustainability.py:66-75`) ;
- cas limite : `R_c` vide → `0.0`.

**Pourquoi cette équation** :

- La mesure isole la **part de capacité restante** : c'est la grandeur la plus
  directe de la « santé » d'une réserve. L'ex-`ResourceToConsumptionRatio`
  mélangeait deux dénominateurs incompatibles (`quantity / consumed` et
  `quantity / capacity`) — refonte P1 (`resource_sustainability.py:8-26`).
- Le `clamp` à 1.0 borne les réserves saturées : sans lui, une réserve
  débordant sa capacité publiée ferait grimper la moyenne au-delà de 1,
  rendant la mesure non interprétable.
- `ResourceCoverage` (§9.3) est publiée à côté pour que la moyenne soit
  lisible (« 2 réserves sur 3 avec capacité connue »).

**Usage dans le projet** : `resource_sustainability.py:156-167, 177`.

**Interprétation** :

- `ResourceFillRatio → 0` : les réserves observées sont presque vides.
- `ResourceFillRatio → 1` : les réserves observées sont pleines (ou saturées).
- **Unité : fraction [0, 1].** Ne porter que sur les réserves dont la
  capacité est publiée — croiser avec `ResourceCoverage` pour évaluer la
  représentativité de la moyenne.

### 9.2 CriticalResourceCount

**Nom** : Nombre de ressources critiques — `CriticalResourceCount`

**Explication globale** : Nombre de réserves dont la part de capacité restante
est **inférieure à 20 %** (`_CRITICAL_RATIO`).

**Expression mathématique** :

```math
\mathrm{CriticalResourceCount} = \bigl|\{r \in R_c : \mathrm{quantity}(r) / \mathrm{capacity}(r) < 0.2\}\bigr|
```

(voir §9.1 pour `R_c` et les symboles).

**Pourquoi cette équation** :

- Le seuil de 20 % définit une **crise opérationnelle** : une réserve en-deçà
  de ce seuil est jugée critique pour la viabilité de la population qui en
  dépend.
- Le seuil est une **constante héritée**, non calibrée sur des campagnes de
  référence.
- `CriticalResourceCount` est un **comptage brut** (pas un ratio) : le
  dénominateur (`|R_c|`) est publié via `ResourceCoverage` (§9.3).

**Usage dans le projet** : `resource_sustainability.py:156-168, 178`.

**Interprétation** :

- `CriticalResourceCount = 0` : aucune réserve sous le seuil de 20 %.
- `CriticalResourceCount` croissant : plus de réserves en situation critique.
- **Unité : count** (entier). Croiser avec `ResourceCoverage` pour connaître
  le dénominateur.

### 9.3 ResourceCoverage

**Nom** : Couverture des réserves — `ResourceCoverage`

**Explication globale** : Part de réserves dont la capacité est **connue**
(publiée et `> 0`).

**Expression mathématique** :

```math
\mathrm{ResourceCoverage} = \frac{|R_c|}{|R|}
```

avec :

- `R_c` : réserves dont la capacité est publiée (§9.1) ;
- `R` : ensemble des réserves observées ;
- cas limite : `R` vide → `0.0` (`safe_ratio`).

**Pourquoi cette équation** :

- La mesure expose le **dénominateur de couverture** de `ResourceFillRatio` et
  `CriticalResourceCount` : une moyenne calculée sur 2 réserves sur 10 n'a
  pas la même portée qu'une moyenne sur 10 réserves sur 10.
- C'est un **dénominateur de contrôle** — la règle « jamais de ratio sans son
  dénominateur » (`METRICS_DICTIONARY.md` §1).

**Usage dans le projet** : `resource_sustainability.py:179`.

**Interprétation** :

- `ResourceCoverage = 1.0` : toutes les réserves observées publient leur
  capacité.
- `ResourceCoverage` croissante vers 0 : une part croissante de réserves est
  sans capacité publiée — les ratios associés ne portent que sur un
  sous-ensemble.
- **Unité : fraction [0, 1].** Lire systématiquement à côté de
  `ResourceFillRatio` et `CriticalResourceCount`.

### 9.4 ConsumptionPerTick

**Nom** : Consommation par tick — `ConsumptionPerTick`

**Explication globale** : Volume total consommé de ressources, rapporté à la
durée **réellement observée** de la fenêtre d'événements (en ticks).

**Expression mathématique** :

```math
\mathrm{ConsumptionPerTick} = \frac{\sum_{e \in E_c} \mathrm{amount}(e)}{T_{\mathrm{fen\^etre}}}
```

avec :

- `E_c` : événements `resource_consumed` de la fenêtre ;
- `amount(e)` : volume consommé déclaré dans l'événement `e` (champ
  `value.amount`) ;
- `T_fenêtre` : durée observée de la fenêtre (`eventWindow.ticks`, ou étendue
  des ticks présents dans `events`, minimum 1,
  `resource_sustainability.py:90-99`) ;
- cas limite : `E_c` vide → `0.0` (zéro observé si fenêtre publiée).

**Pourquoi cette équation** :

- Le volume brut croît avec la durée de la fenêtre : rapporter à `T_fenêtre`
  donne un **débit** comparable entre runs à fenêtres de durées différentes.
- La durée utilisée est celle **réellement observée** (`eventWindow.ticks`),
  pas une constante : deux runs comparables donnent des débits comparables.
- `REQUIRES = event_window_published` : sans fenêtre, la mesure est non
  mesurée (§2.3).

**Usage dans le projet** : `resource_sustainability.py:180-182`.
`REQUIRES = event_window_published`.

**Interprétation** :

- `ConsumptionPerTick = 0` : aucun événement de consommation observé (zéro
  observé ou `measured = false`).
- `ConsumptionPerTick` croissante : débit de consommation en hausse.
- **Unité : unité/tick** (débit). Comparabilité bonne entre runs à fenêtres de
  durées différentes, sous réserve que l'unité de `amount` soit la même.

### 9.5 RecoveryTime

**Nom** : Durée moyenne de récupération — `RecoveryTime`

**Explication globale** : Moyenne (§3.3) des durées des cycles **complets**
« crise → récupération » observés dans l'historique des réserves. Une crise
s'ouvre quand la part de capacité moyenne passe sous 20 % ; elle se résout à
un retour ≥ 80 %.

**Expression mathématique** :

```math
\mathrm{RecoveryTime} = \frac{1}{|C|} \sum_{c \in C} \bigl(\mathrm{tick}_{\mathrm{r\acute{e}solution}}(c) - \mathrm{tick}_{\mathrm{ouverture}}(c)\bigr)
```

avec :

- `C` : ensemble des cycles critiques **complets** (ouverture + résolution
  observées dans `history`) ;
- `tick_ouverture(c)` : tick où la part de capacité moyenne passe sous
  `_CRITICAL_RATIO = 0.2` ;
- `tick_résolution(c)` : tick où la part de capacité moyenne atteint ou
  dépasse `_RECOVERED_RATIO = 0.8` ;
- la part de capacité moyenne est calculée sur les réserves dont la capacité
  est publiée (§9.1) ;
- cas limite : aucun cycle complet → `0.0` (censure : les crises encore
  ouvertes ne valent ni durée 0 ni durée observée,
  `resource_sustainability.py:102-148`).

**Pourquoi cette équation** :

- La mesure évalue le **temps de retour à une réserve saine** après une crise :
  c'est un indicateur de résilience du système de ressources.
- Les **crises non résolues** (encore ouvertes à la fin de la fenêtre) sont
  comptées séparément (`UnresolvedCrisisCount`, §9.7) : les inclure dans la
  durée les traiterait comme résolues en 0 tick, ce qui serait faux.
- Les seuils 20 % / 80 % sont des **constantes héritées**, non calibrées.

**Usage dans le projet** : `resource_sustainability.py:102-148, 183`.
`REQUIRES = "history"`.

**Interprétation** :

- `RecoveryTime = 0` : aucun cycle critique complet observé (ou `measured =
  false`) — ne signifie ni « aucune crise » ni « récupération instantanée ».
- `RecoveryTime` croissante : les cycles critiques mettent plus de temps à se
  résorber.
- **Unité : ticks** (durée). Ne porter que sur les cycles **complets** — les
  crises ouvertes sont portées par `UnresolvedCrisisCount`.

### 9.6 RecoveryEpisodes

**Nom** : Épisodes de récupération — `RecoveryEpisodes`

**Explication globale** : Nombre de cycles critiques **complets** observés
(crise ouverte + récupération atteinte) dans l'historique.

**Expression mathématique** :

```math
\mathrm{RecoveryEpisodes} = |C|
```

(voir §9.5 pour `C`).

**Pourquoi cette équation** :

- Le comptage d'épisodes est le **dénominateur** de `RecoveryTime` : une durée
  moyenne calculée sur un seul épisode n'a pas la même portée que sur dix.
- `RecoveryEpisodes = 0` et `UnresolvedCrisisCount > 0` signifient des crises
  ouvertes sans résolution observée — lecture que `RecoveryTime = 0` seule ne
  permet pas.

**Usage dans le projet** : `resource_sustainability.py:184`.
`REQUIRES = "history"`.

**Interprétation** :

- `RecoveryEpisodes = 0` : aucun cycle critique complet observé.
- `RecoveryEpisodes` croissant : plus de cycles critiques complets dans la
  fenêtre.
- **Unité : count** (entier). Croiser avec `RecoveryTime` et
  `UnresolvedCrisisCount`.

### 9.7 UnresolvedCrisisCount

**Nom** : Crises non résolues — `UnresolvedCrisisCount`

**Explication globale** : Nombre de crises **encore ouvertes** à la fin de la
fenêtre d'historique (part de capacité moyenne sous 20 % sans retour observé
≥ 80 %).

**Expression mathématique** :

```math
\mathrm{UnresolvedCrisisCount} =
\begin{cases}
1 & \text{si aucune résolution n'est observée pour la crise ouverte} \\[3pt]
0 & \text{sinon}
\end{cases}
```

(l'algorithme de parcours chronologique ne maintient qu'une crise ouverte à la
fois, `resource_sustainability.py:134-148`).

**Pourquoi cette équation** :

- La mesure expose les **crises censurées** : l'observation s'arrête avant la
  fin de l'épisode. Sans elle, `RecoveryTime = 0` serait ambigu (« aucune
  crise » vs « crise non résolue »).
- C'est un **dénominateur de contrôle** pour `RecoveryEpisodes` et
  `RecoveryTime`.

**Usage dans le projet** : `resource_sustainability.py:146-147, 185`.
`REQUIRES = "history"`.

**Interprétation** :

- `UnresolvedCrisisCount = 0` : aucune crise ouverte en fin de fenêtre.
- `UnresolvedCrisisCount > 0` : au moins une crise est encore ouverte —
  l'observation est **censurée** (état `censored` du catalogue).
- **Unité : count** (entier). Ne pas lire comme « 0 = système sain » : vérifier
  aussi `RecoveryEpisodes` et `ResourceFillRatio`.

---

## 10. Dynamique des groupes

Moteur : `GroupDynamicsMetrics` (`echos/analysis/group_dynamics.py`).

Le moteur distingue deux familles de mesures :

1. les **communautés inférées** — communautés du graphe de confiance
   (propagation d'étiquettes, §6.8). Ce ne sont pas des groupes déclarés par
   SYNE ;
2. les **événements de groupes natifs** — `group_formed` / `group_dissolved`,
   avec leurs dénominateurs bruts publiés à côté des taux.

Constante :

```math
\texttt{\_RATE\_WINDOW\_TICKS} = 1000 \quad (\text{fenêtre de référence des taux normalisés})
```

### 10.1 InferredCommunities et AverageCommunitySize

**Nom** : Communautés inférées / taille moyenne — `InferredCommunities`,
`AverageCommunitySize`

**Explication globale** : `InferredCommunities` compte les communautés de
taille ≥ 2 détectées dans le graphe de confiance (même calcul que
`NumberOfCommunities`, §6.6). `AverageCommunitySize` est la moyenne (§3.3) des
tailles de ces communautés.

**Expression mathématique** :

```math
\mathrm{InferredCommunities} = \bigl|\{g \in G : |g| \ge 2\}\bigr|

\mathrm{AverageCommunitySize} = \frac{1}{|G|} \sum_{g \in G} |g|
```

avec :

- `G` : communautés produites par `communities(agents)` (`_common.py:325-337`) ;
- `|g|` : nombre de membres de la communauté `g` ;
- cas limite : `G` vide → `AverageCommunitySize = 0.0`.

**Pourquoi ces équations** :

- Le nom `InferredCommunities` (P1) remplace l'ex-`ActiveGroups`, qui
  confondait communautés inférées et groupes natifs SYNE. La confusion était
  structurelle : les deux notions coexistent sans coïncider.
- `AverageCommunitySize` complète le comptage : « 5 communautés » ne dit rien
  de leur taille — « 5 communautés de 6 entités en moyenne » est plus
  interprétable.
- Les **singletons** sont exclus (taille ≥ 2) : une entité seule ne constitue
  pas un groupe (§6.6).

**Usage dans le projet** : `group_dynamics.py:148-149, 164-165`.

**Interprétation** :

- `InferredCommunities = 0` : aucune communauté de taille ≥ 2 (réseau vide ou
  uniquement des isolats).
- `AverageCommunitySize` croissante : communautés plus grandes en moyenne.
- **Unités** : `InferredCommunities` en `count` ; `AverageCommunitySize` en
  `entités` (taille moyenne). Ce sont des communautés **inférées** du graphe
  de confiance, pas des groupes déclarés par SYNE.

### 10.2 CommunityCoverage

**Nom** : Couverture communautaire — `CommunityCoverage`

**Explication globale** : Part de la population vivante rattachée à une
communauté de taille ≥ 2.

**Expression mathématique** :

```math
\mathrm{CommunityCoverage} = \frac{\sum_{g \in G} |g|}{n}
```

avec :

- `G` : communautés de taille ≥ 2 (§10.1) ;
- `n` : population vivante (`aliveCount`) ;
- cas limite : `n = 0` → `0.0` (`safe_ratio`).

**Pourquoi cette équation** :

- La mesure expose la **part de population socialisée** : elle répond à «
  quelle fraction des entités vit dans un groupe (de taille ≥ 2) ? ».
- La base est défendue et bornée `[0,1]` pour les indices composites : elle
  remplace l'ancien terme `ActiveGroups / 100` qui n'avait pas de dénominateur
  de population (`group_dynamics.py:166-169`).
- `CommunityCoverage` alimente `ContributionCommunityCoverage` du
  `EmergenceScore` (poids 0.25 — la plus forte contribution, §11.2).

**Usage dans le projet** : `group_dynamics.py:169`.

**Interprétation** :

- `CommunityCoverage = 0` : aucune entité n'est rattachée à une communauté de
  taille ≥ 2.
- `CommunityCoverage → 1` : toute la population vit dans des communautés.
- **Unité : fraction [0, 1].** Les singletons (entités isolées) comptent dans
  le dénominateur mais pas dans le numérateur : `CommunityCoverage = 0.5`
  signifie que la moitié de la population vit en groupe.

### 10.3 AverageGroupLifetime

**Nom** : Durée de vie moyenne des groupes — `AverageGroupLifetime`

**Explication globale** : Moyenne (§3.3) des durées de vie (`lifetime`)
déclarées dans les événements `group_dissolved`.

**Expression mathématique** :

```math
\mathrm{AverageGroupLifetime} = \frac{1}{|D|} \sum_{d \in D} \mathrm{lifetime}(d)
```

avec :

- `D` : événements `group_dissolved` de la fenêtre ;\n- `lifetime(d)` : durée de vie déclarée du groupe dissous (champ
  `value.lifetime`, en ticks) ;
- cas limite : `D` vide → `0.0` (`REQUIRES = "events"`).

**Pourquoi cette équation** :

- La mesure évalue la **durée d'existence des groupes natifs SYNE** : combien
  de temps les groupes déclarés survivent-ils avant dissolution ?
- Le `lifetime` est une **donnée déclarée** par SYNE (événement
  `group_dissolved`), pas recalculée par ECHOS : le moteur se contente de la
  moyennner.
- `REQUIRES = "events"` : sans événements, la mesure est non mesurée.

**Usage dans le projet** : `group_dynamics.py:157, 171`.
`REQUIRES = "events"`.

**Interprétation** :

- `AverageGroupLifetime = 0` : aucune dissolution observée (ou `measured =
  false`).
- `AverageGroupLifetime` croissante : les groupes natifs durent plus
  longtemps avant dissolution.
- **Unité : ticks** (durée). Ne porter que sur les groupes **dissous et
  observés** : un run court peut sous-estimer la durée de vie réelle des
  groupes encore actifs en fin de fenêtre.

### 10.4 GroupFormationRate et GroupDissolutionRate

**Nom** : Taux de formation / dissolution — `GroupFormationRate`,
`GroupDissolutionRate`

**Explication globale** : Nombre d'événements `group_formed` (resp.
`group_dissolved`) normalisé sur 1000 ticks, en divisant par la durée
**réellement observée** de la fenêtre.

**Expression mathématique** :

```math
\mathrm{GroupFormationRate} = |\mathrm{formed}| \cdot \frac{1000}{\max(1,\, T_{\mathrm{fen\^etre}})}

\mathrm{GroupDissolutionRate} = |\mathrm{dissolved}| \cdot \frac{1000}{\max(1,\, T_{\mathrm{fen\^etre}})}
```

avec :

- `|formed|` / `|dissolved|` : compteurs bruts des événements
  `group_formed` / `group_dissolved` ;
- `_RATE_WINDOW_TICKS = 1000` : fenêtre de référence (constante) ;
- `T_fenêtre` : durée observée de la fenêtre (`eventWindow.ticks`, ou étendue
  des ticks présents dans `events`, minimum 1, `group_dynamics.py:111-129`) ;
- cas limites : `T_fenêtre = 0` → ramené à 1 (jamais de division par zéro) ;
  aucun événement → `0.0`.

**Pourquoi ces équations** :

- Normaliser « par 1000 ticks » donne un **taux standard** lisible et
  comparable entre runs à fenêtres de durées différentes.
- Le dénominateur est la durée **réellement observée** (`eventWindow.ticks`),
  jamais la constante 1000 : un unique événement isolé dans une fenêtre de 1
  tick ne vaut pas 1000 — il vaut `1 · (1000/1) = 1000`, ce qui reflète
  l'amplification réelle d'une fenêtre courte. Le dénominateur brut
  (`FormationCount` / `DissolutionCount`) et la fenêtre restent à afficher à
  côté (`group_dynamics.py:88-96`).
- `REQUIRES = event_window_published` : sans fenêtre, les taux sont non
  mesurés.

**Usage dans le projet** : `group_dynamics.py:99-108, 155, 172-173.
`_rate()` est la fonction de calcul. `REQUIRES = event_window_published`.

**Interprétation** :

- `GroupFormationRate = 0` : aucune formation observée dans la fenêtre (zéro
  observé ou `measured = false`).
- `GroupFormationRate` croissante : formations plus fréquentes rapportées à la
  durée observée.
- **Unité : groupes/1000 ticks.** Lire systématiquement avec les compteurs
  bruts (`FormationCount`, `DissolutionCount`) et la durée de fenêtre : un
  taux élevé sur une fenêtre courte est amplifié mécaniquement.

### 10.5 FormationCount et DissolutionCount

**Nom** : Comptages bruts — `FormationCount`, `DissolutionCount`

**Explication globale** : Nombre brut d'événements `group_formed` (resp.
`group_dissolved`) observés dans la fenêtre.

**Expression mathématique** :

```math
\mathrm{FormationCount} = \bigl|\{e \in E : \mathrm{type}(e) = \texttt{group\_formed}\}\bigr|

\mathrm{DissolutionCount} = \bigl|\{e \in E : \mathrm{type}(e) = \texttt{group\_dissolved}\}\bigr|
```

**Pourquoi ces équations** :

- Les comptages bruts sont les **dénominateurs de contrôle** des taux
  normalisés (§10.4) : « 2 formations sur 50 ticks » est plus interprétable
  que « 40 groupes/1000 ticks » seul.
- Publier les deux (taux + comptage brut + fenêtre) respecte la règle « jamais
  de ratio sans son dénominateur » et rend visible l'amplification d'une
  fenêtre courte.
- `REQUIRES = event_window_published` : 0 dans une fenêtre publiée est un
  zéro observé.

**Usage dans le projet** : `group_dynamics.py:174-175.
`REQUIRES = event_window_published`.

**Interprétation** :

- `FormationCount = 0` : aucune formation observée (zéro observé ou `measured
  = false`).
- **Unité : count** (entier). Toujours afficher avec la fenêtre observée.

### 10.6 DissolvedGroupSuccessShare

**Nom** : Part des dissolutions marquées « succès » —
`DissolvedGroupSuccessShare`

**Explication globale** : Moyenne (§3.3) des drapeaux `success` des
événements `group_dissolved` qui publient ce champ.

**Expression mathématique** :

```math
\mathrm{DissolvedGroupSuccessShare} = \frac{1}{|D_s|} \sum_{d \in D_s} \mathrm{success}(d)
```

avec :

- `D_s` : événements `group_dissolved` dont `success` est publié (booléen,
  converti en 0.0/1.0) ;
- `success(d)` : drapeau de succès du groupe dissous `d` ;
- cas limite : `D_s` vide → `0.0` (`REQUIRES = _dissolution_published`).

**Pourquoi cette équation** :

- La mesure évalue le **taux de succès parmi les dissolutions observées** :
  « les groupes qui se dissolvent ont-ils atteint leur objectif ? ».
- Le biais de sélection est **assumé et documenté** (`group_dynamics.py:18-21`) :
  la mesure ne porte que sur les groupes **dissous et observés**, pas sur
  tous les groupes. Un groupe qui a atteint son objectif sans dissolution
  publiée n'apparaît pas dans le dénominateur.
- Le dénominateur (`DissolutionCount`) est publié à côté (§10.5).
- `REQUIRES = _dissolution_published` : sans dissolution observée, la mesure
  est non mesurée (0.0 serait non interprétable sans dénominateur).

**Usage dans le projet** : `group_dynamics.py:158, 178`.
`REQUIRES = _dissolution_published`.

**Interprétation** :

- `DissolvedGroupSuccessShare = 0` : aucune dissolution marquée « succès » —
  ou aucune dissolution publiant `success` (`measured = false`).
- `DissolvedGroupSuccessShare → 1` : toutes les dissolutions observées sont
  marquées « succès ».
- **Unité : fraction [0, 1].** Biais de sélection assumé : ne pas lire comme
  « taux de succès de tous les groupes ».

### 10.7 MemberExitsPerDissolution

**Nom** : Sorties moyennes par dissolution — `MemberExitsPerDissolution`

**Explication globale** : Moyenne (§3.3) du nombre de sorties de membres
(`membersOut`) déclarées dans les événements `group_dissolved`.

**Expression mathématique** :

```math
\mathrm{MemberExitsPerDissolution} = \frac{1}{|D_e|} \sum_{d \in D_e} \mathrm{membersOut}(d)
```

avec :

- `D_e` : événements `group_dissolved` dont `membersOut` est publié (nombre
  entier de sorties) ;
- `membersOut(d)` : nombre de membres sortis du groupe dissous `d` ;
- cas limite : `D_e` vide → `0.0` (`REQUIRES = _dissolution_published`).

**Pourquoi cette équation** :

- La mesure évalue l'**ampleur des sorties** lors des dissolutions : combien
  de membres quittent un groupe qui se dissout ?
- L'ex-`MemberTurnoverRate` appariait `membersOut` et `membersIn` par `zip`
  (listes non garanties alignées), divisait par un dénominateur pouvant être
  nul, puis annualisait sur 1000 ticks. Le contrat ne fournit pas l'effectif
  exposé en membres-temps : on publie donc la moyenne **observée** des sorties
  par dissolution plutôt qu'un taux inventé (`group_dynamics.py:22-27`).
- Le nom `MemberExitsPerDissolution` (P1) dit exactement ce qui est calculé.

**Usage dans le projet** : `group_dynamics.py:159, 180.
`REQUIRES = _dissolution_published`.

Déclenche aussi le phénomène `OrganizationalDynamics`
(`InferredCommunities > 5` et `MemberExitsPerDissolution > 1`, §11.5).

**Interprétation** :

- `MemberExitsPerDissolution = 0` : aucune dissolution publiant `membersOut`
  (ou `measured = false`).
- `MemberExitsPerDissolution` croissante : plus de sorties de membres par
  dissolution observée.
- **Unité : membres** (dénominateur : dissolution). Pas un taux annualisé —
  une moyenne **observée**.

---

## 11. Indices composites

Moteur : `EmergenceIndicators` (`echos/analysis/emergence.py`).
Document détaillé : `EMERGENCE_INDICATORS.md`. Cette section expose les
équations et leur justification scientifique.

### 11.1 EmergenceScore

**Nom** : Score d'émergence composite — `EmergenceScore`

**Explication globale** : Somme pondérée de six composantes déjà bornées
`[0, 1]`. Les poids somment à 1.0, donc le score est déjà dans l'intervalle
`[0, 1]` — le `clamp` final n'est qu'une garde.

**Expression mathématique** :

```math
\mathrm{EmergenceScore} = \mathrm{clamp}\!\left(\sum_{i=1}^{6} w_i \cdot c_i,\; 0,\; 1\right)
```

avec :

- `wᵢ` : poids de la composante *i* (Σᵢ wᵢ = 1.0) ;
- `cᵢ` : composante *i*, déjà normalisée `[0, 1]` (voir §11.2) ;
- `clamp(·, 0, 1)` : garde de bornes (§3.6), sans effet si les composantes
  sont dans l'intervalle.

**Poids** (constants hérités, `emergence.py:53-60`) :

| Composante | Poids `wᵢ` | Source de la composante |
| :-- | --: | :-- |
| `ContributionBeliefDiversity` | 0.15 | `BeliefDiversityNorm` (§4.2) |
| `ContributionGoalDiversity` | 0.15 | `GoalDiversityNorm` (§4.5) |
| `ContributionEmitterCoverage` | 0.10 | `CoverageDelay_Norm` (§11.3) |
| `ContributionClustering` | 0.15 | `ClusteringCoefficient` (§6.4) |
| `ContributionRepeatedActions` | 0.20 | `RepeatedActionShare` (§8.2) |
| `ContributionCommunityCoverage` | 0.25 | `CommunityCoverage` (§10.2) |
| **Total** | **1.00** | |

**Pourquoi cette équation** :

- Une somme pondérée est l'indice composite **le plus simple et le plus
  transparent** : chaque terme est interprétable, le poids est publié, et la
  contribution de chaque composante au score total est calculable sans
  réexécution.
- La refonte P2 a remplacé les entropies **brutes** (bits) par leurs versions
  **normalisées** `[0,1]` : additionner des bits, un clustering `[0,1]` et un
  nombre de groupes produisait une somme qui saturait dans le `clamp` final
  et perdait toute discrimination dans la partie haute. Depuis P2, chaque
  composante est une grandeur `[0,1]` avec une base documentée, et la somme
  pondérée est déjà bornée.
- Les poids sont **publiés** (et non codés en dur dans la formule) pour qu'une
  analyse de sensibilité puisse les examiner sans changer la formule
  silencieusement.
- Les poids sont **[HÉRITÉ] et non calibrés** : aucune campagne de
  référence ni analyse de sensibilité ne les a validés. Le statut du score est
  `exploratory`.

**Usage dans le projet** : `emergence.py:303-318` (`compute_from_metrics`).
Contributions publiées en métriques `Contribution*` avec invariant
`Σ contributions == EmergenceScore` (arrondi 12 décimales,
`emergence.py:311-318`).

**Interprétation** :

- `EmergenceScore → 0` : toutes les composantes sont faibles (diversité,
  clustering, couverture, répétitions, communautés tous faibles).
- `EmergenceScore → 1` : toutes les composantes sont proches de leur maximum.
- **Unité : fraction [0, 1].** Statut `exploratory` : ce score est une
  **heuristique d'observation**, pas une preuve scientifique d'émergence.
  Le champ `Disclaimer` (invariant, ECHOS-032) accompagne toujours le score.
- Un score dont une composante source est **non mesurée** est lui-même non
  mesuré (provenance propagée via `COMPOSITE_DEPENDENCIES`) et ne doit pas
  être rendu comme `0`.

### 11.2 Contributions publiées

**Nom** : Contributions du score — `Contribution*`

**Explication globale** : Chaque terme du score est publié comme métrique
distincte, arrondi à 12 décimales. L'invariant `Σ contributions ==
EmergenceScore` est testé.

**Expression mathématique** :

```math
\mathrm{Contribution}_i = \mathrm{round}(w_i \cdot c_i,\; 12)

\sum_{i=1}^{6} \mathrm{Contribution}_i \;=\; \mathrm{EmergenceScore} \quad (\text{invariant testé})
```

**Pourquoi cette équation** :

- Le score se **décompose** dans n'importe quelle interface, sans
  réexécution ni recalcul local (ADR-003 : le Launcher affiche, il ne
  recalcule pas). Un utilisateur voit immédiatement quelle composante
  domine le score.
- L'invariant testé garantit la cohérence : la somme des contributions
  publiées est exactement le score publié.
- La provenance (`measured`) de chaque contribution est calculée depuis
  `COMPOSITE_DEPENDENCIES` : chaque sortie n'exige que **ses** dépendances
  réelles (`emergence.py:96-117`).

**Usage dans le projet** : `emergence.py:303-314, 323-325`.

**Interprétation** :

- Chaque `Contribution*` se lit comme la **part du score** apportée par la
  composante associée.
- Une contribution à `0.0` avec `measured = false` signale une composante non
  mesurée — le score est alors lui-même non mesuré.

### 11.3 CoverageDelay_Norm

**Nom** : Couverture de diffusion normalisée — `CoverageDelay_Norm`
(composante interne, publiée via `ContributionEmitterCoverage`)

**Explication globale** : Transforme le délai de couverture des émetteurs
(§5.2, en ticks) en une grandeur `[0, 1]` : plus le seuil des 80 % est atteint
vite, plus la contribution est forte.

**Expression mathématique** :

```math
\mathrm{CoverageDelay\_Norm} =
\begin{cases}
\mathrm{clamp}\!\bigl(1 - \mathrm{EmitterCoverageDelay} / 100,\; 0,\; 1\bigr) & \text{si mesuré} \\[4pt]
0.0 & \text{si } \mathrm{EmitterCoverageDelay} \le 0 \text{ ou non mesuré}
\end{cases}
```

avec :

- `EmitterCoverageDelay` : délai de couverture (§5.2), en ticks ;
- `_DIFFUSION_HORIZON = 100` : horizon de diffusion (constante héritée,
  `emergence.py:198-205`) ;
- neutralité V0.1 (ECHOS-006) : délai **non mesuré** (absent ou ≤ 0,
  « jamais diffusé ») → contribution **0.0**, jamais le maximum de la formule
  (une vitesse nulle signifierait une diffusion instantanée, inobservable).

**Pourquoi cette équation** :

- L'horizon de 100 ticks interpole entre « couvert en un tick » (1.0) et «
  non couvert dans l'horizon » (0.0). La normalisation rend la diffusion
  comparable à d'autres composantes `[0,1]` du score.
- La neutralité sur le délai non mesuré est essentielle : sans elle, un
  délai non mesuré (0.0) produirait `1 − 0/100 = 1.0` — le **maximum** —
  ce qui serait l'inverse exact de la convention ECHOS-006 (données
  manquantes → neutres, jamais le max).
- L'horizon 100 est **[HÉRITÉ] et non calibré**.

**Usage dans le projet** : `emergence.py:296-301, 306.
Alimente `ContributionEmitterCoverage` (poids 0.10) et `SystemComplexity`.

**Interprétation** :

- `CoverageDelay_Norm → 1` : le seuil de 80 % est atteint dès le premier tick
  (diffusion immédiate parmi les émetteurs).
- `CoverageDelay_Norm → 0` : le délai dépasse l'horizon de 100 ticks, ou la
  donnée n'est pas mesurée.
- **Unité : fraction [0, 1].** Ne pas lire comme une vitesse de diffusion :
  c'est une normalisation d'un délai, pas une mesure de rapidité intrinsèque.

### 11.4 SystemComplexity

**Nom** : Complexité du système — `SystemComplexity`

**Explication globale** : Moyenne (§3.3) de trois grandeurs normalisées
`[0, 1]` : diversité des croyances, diversité des objectifs et couverture de
diffusion.

**Expression mathématique** :

```math
\mathrm{SystemComplexity} = \mathrm{clamp}\!\left(\frac{\mathrm{BeliefDiversityNorm} + \mathrm{GoalDiversityNorm} + \mathrm{CoverageDelay\_Norm}}{3},\; 0,\; 1\right)
```

avec :

- `BeliefDiversityNorm` : §4.2 ;
- `GoalDiversityNorm` : §4.5 ;
- `CoverageDelay_Norm` : §11.3 ;
- `clamp(·, 0, 1)` : garde de bornes.

**Pourquoi cette équation** :

- Formule canonique **bornée** (décision P2) : trois grandesurs normalisées,
  plus de ticks bruts. L'ancienne formule intégrait
  `InformationDiffusionSpeed` en ticks — un nombre non borné qui faisait
  croître l'indicateur avec la durée du run (10, 100, 1000… selon la longueur
  du run).
- Ce n'est **pas** une approximation de la complexité de Kolmogorov : c'est
  une moyenne de trois dispersions normalisées (statut `exploratory`).
- Le dénominateur 3 est fixe : les trois composantes sont toujours présentes
  dans la formule (même si une est non mesurée → 0.0, ce qui **réduit** la
  complexité apparente — lecture à vérifier via la provenance).

**Usage dans le projet** : `emergence.py:319-321.

Test de non-régression : `EmitterCoverageDelay` au-delà de l'horizon →
contribution de diffusion neutre (0.0), `SystemComplexity` plafonné à 2/3
dans ce cas ; jamais négatif, jamais croissant avec les ticks.

**Interprétation** :

- `SystemComplexity → 1` : les trois dispersions sont proches de leur maximum.
- `SystemComplexity → 0` : les trois dispersions sont faibles.
- **Unité : fraction [0, 1].** Statut `exploratory`. Ne pas lire comme une
  « complexité de Kolmogorov » ni comme une mesure théorique de complexité :
  c'est une moyenne de trois indicateurs normalisés.

### 11.5 DetectedPhenomena

**Nom** : Phénomènes auto-détectés — `DetectedPhenomena`

**Explication globale** : Liste des phénomènes dont **tous** les signaux
déclencheurs sont au-dessus de leur seuil. Chaque phénomène publié porte la
trace des signaux `{metric, value, threshold}`.

**Expression mathématique** :

Pas une équation scalaire, mais un ensemble de **règles à seuils** :

```math
\text{Phénomène déclenché} \iff \forall\, (\mathrm{m\acute{e}trique},\, \mathrm{seuil}) \in \mathrm{signaux} : \mathrm{valeur}(\mathrm{m\acute{e}trique}) > \mathrm{seuil}
```

| Phénomène | Identifiant | Signaux (tous requis) |
| :-- | :-- | :-- |
| Seuil de communautés franchi | `CommunityFormation` | `NumberOfCommunities > 2` |
| Répétitions d'action soutenues | `FeedbackLoops` | `RepeatedActionPairs > 5` |
| Convergence forte des objectifs | `CollectiveCoordination` | `GoalConvergence > 0.7` |
| Concentration des émetteurs | `InformationBottleneck` | `SenderConcentration > 0.3` |
| Communautés nombreuses et sorties de membres | `OrganizationalDynamics` | `InferredCommunities > 5` **et** `MemberExitsPerDissolution > 1` |

**Pourquoi ces équations** :

- Les règles à seuils sont la forme la plus simple de **détection** : chaque
  condition est lisible, testable, et la trace des signaux documente exactement
  pourquoi un phénomène est (ou n'est pas) déclenché.
- Les **identifiants restent stables** (clé de contrat public) ; les
  **libellés et descriptions** décrivent ce qui est réellement observé — un
  comptage, pas une formation ; des répétitions, pas une boucle causale ; une
  concordance de catégories, pas une coordination ; des émissions, jamais des
  réceptions (requalification P0).
- Les **seuils sont [HÉRITÉ] et non calibrés** : ils viennent de la spec
  héritée et n'ont fait l'objet d'aucune campagne de référence.
- **Une absence de détection n'est pas l'absence du phénomène** : les seuils
  non calibrés produisent des faux négatifs possibles.

**Usage dans le projet** : `emergence.py:127-186` (spécification),
`emergence.py:263-284` (`_detected_phenomena`).

**Interprétation** :

- Un phénomène listé : tous ses signaux sont au-dessus du seuil — les valeurs
  et seuils sont publiés dans `signals`.
- Une liste vide : aucun phénomène n'a déclenché — **pas** une preuve que les
  phénomènes sont absents du monde simulé.
- **Sortie structurée** (`non numérique`) : à afficher telle quelle, avec la
  mention « selon la règle X » et les seuils.

---

## 12. Reproductibilité entre runs

Méta-métriques de comparaison de runs (`echos/analysis/reproducibility.py`,
`EXPERIMENT_COMPARISON.md`).

### 12.1 Distribution de croyances

**Nom** : Distribution cognitive — `belief_distribution(agents)`

**Explication globale** : Distribution normalisée (somme = 1) des croyances
de la population, où chaque croyance `(sujet, prédicat, valeur)` compte pour
un, et le total est normalisé à 1.

**Expression mathématique** :

```math
P_b(\mathrm{key}) = \frac{\mathrm{count}(\mathrm{key})}{\sum_{\mathrm{key}} \mathrm{count}(\mathrm{key})}
```

où `key = subject|predicate|value` (triplet sérialisé), clés triées.

**Pourquoi cette équation** :

- La normalisation à 1 rend les distributions **comparables entre populations
  de tailles différentes** : la forme de la distribution compte, pas
  l'effectif brut.
- Chaque croyance compte pour un (pas pour sa confiance) : la distribution
  décrit la **variété des énoncés**, pas leur intensité.
- Dict vide sur population sans croyances (diff nulle).

**Usage dans le projet** : `reproducibility.py:36-58.

### 12.2 Distribution sociale

**Nom** : Distribution sociale — `social_distribution(agents)`

**Explication globale** : Distribution normalisée (somme = 1) des poids de
confiance entre paires d'entités. Le poids d'une paire est la moyenne des
reliances des deux directions (ou de la direction unique) ; seules les arêtes
de confiance positive sont incluses.

**Expression mathématique** :

```math
w(\mathrm{paire}) = \text{moyenne des poids de confiance des deux directions (ou de l'unique)}

P_s(\mathrm{paire}) = \frac{w(\mathrm{paire})}{\sum_{\mathrm{paire}} w(\mathrm{paire})}
```

clés `min|max` triées (réseau non orienté déterministe).

**Pourquoi cette équation** :

- La moyenne des deux directions (si les deux existent) donne un poids de
  paire **non orienté** : `a confie en b` et `b confie en a` fusionnent en une
  seule entrée, ce qui est cohérent avec le traitement non orienté du graphe
  (§6).
- Les arêtes `trust ≤ 0` sont exclues : poids nul = relation inexistante
  (§6.1).
- La normalisation à 1 rend la distribution comparable entre runs de tailles
  différentes.

**Usage dans le projet** : `reproducibility.py:61-86`.

### 12.3 Distance L2 normalisée

**Nom** : Distance L2 normalisée — `l2_normalized(distribution_a, distribution_b)`

**Explication globale** : Distance euclidienne (L2) entre deux distributions,
normalisée pour être bornée `[0, 1]`.

**Expression mathématique** :

```math
d(A, B) = \min\!\left(1.0,\; \sqrt{\frac{\sum_{\mathrm{key}} \bigl(p_A(\mathrm{key}) - p_B(\mathrm{key})\bigr)^2}{2}}\right)
```

avec :

- `key` : union des clés des deux distributions (clés manquantes = 0.0) ;
- `_DIFF_NORMALIZER = 2.0` : deux distributions **opposées** (supports disjoints)
  atteignent `√2` → division par `√2` pour borner à 1.0
  (`reproducibility.py:29, 89-102`) ;
- cas limite : aucune clé commune → `0.0`.

**Pourquoi cette équation** :

- La distance L2 est la distance euclidienne **canonique** entre deux vecteurs
  de probabilité : elle mesure l'écart quadratique cumulé entre les deux
  distributions.
- La normalisation par `√2` (distance L2 maximale entre deux distributions
  disjointes) bornait la distance `[0,1]`, ce qui la rend comparable à un
  score de similarité.
- `min(1.0, ·)` protège contre les arrondis qui pourraient produire une
  valeur légèrement supérieure à 1.

**Usage dans le projet** : `reproducibility.py:89-102.

Utilisée pour : `CognitiveDiff` (distance entre distributions de croyances,
§12.1) et `SocialDiff` (distance entre distributions sociales, §12.2).

**Interprétation** :

- `d = 0` : distributions identiques.
- `d = 1` : distributions maximales (supports disjoints, ou quasi-disjoints).
- **Unité : fraction [0, 1].** Comparabilité directe entre `CognitiveDiff` et
  `SocialDiff` (même normalisation).

### 12.4 Empreinte SHA-256

**Nom** : Empreinte de contenu — `content_fingerprint(store, run_id)`

**Explication globale** : Empreinte SHA-256 (hexadécimal) du contenu canonique
d'un run — séries de métriques, résumés de tick, événements, contextes
(`agents`, `groups`, `phenomena`) et traces de décision. L'identifiant
`run_id` est exclu de la vue canonique.

**Expression mathématique** :

```math
\mathrm{empreinte} = \mathrm{SHA\text{-}256}\bigl(\mathrm{json.dumps}(\mathrm{vue\_canonique},\; \mathrm{sort\_keys=True},\; \mathrm{separators}=(\texttt{","}, \texttt{":"}))\bigr)
```

où `vue_canonique` est le dictionnaire ordonné des données du run, sans
`run_id` et sans le contexte `profiling` (durées de calcul, non
reproductibles, `reproducibility.py:105-114`).

**Pourquoi cette équation** :

- La comparaison **bit-à-bit** exige une empreinte déterministe de contenu :
  deux runs identiques doivent produire la même empreinte, indépendamment de
  leur étiquette `run_id`.
- La sérialisation canonique (`sort_keys=True`, séparateurs compacts) garantit
  que l'empreinte ne dépend pas de l'ordre des clés du dictionnaire Python.
- `profiling` est **exclu** à dessein : les durées de calcul ne sont pas
  reproductibles entre deux exécutions ; comparer les runs sur cette vue
  reviendrait à comparer ce qui est déterministe.

**Usage dans le projet** : `reproducibility.py:105-165.

### 12.5 ReproducibilityScore et IsReproducible

**Nom** : Score de reproductibilité — `ReproducibilityScore`,
`IsReproducible`

**Explication globale** :

- `IsReproducible` : booléen — même **seed** ET même **version** du moteur
  (SYNE) ET contenu observé **bit-à-bit identique** (empreintes SHA-256
  égales).
- `ReproducibilityScore` : `1.0` si reproductible, sinon
  `1.0 − (CognitiveDiff + SocialDiff) / 2`.

**Expression mathématique** :

```math
\mathrm{IsReproducible} = (\mathrm{seed}_A = \mathrm{seed}_B) \;\land\; (\mathrm{version}_A = \mathrm{version}_B) \;\land\; (\mathrm{empreinte}_A = \mathrm{empreinte}_B)

\mathrm{ReproducibilityScore} =
\begin{cases}
1.0 & \text{si } \mathrm{IsReproducible} \\[4pt]
1.0 - \dfrac{\mathrm{CognitiveDiff} + \mathrm{SocialDiff}}{2.0} & \text{sinon}
\end{cases}
```

avec :

- `CognitiveDiff` : distance L2 normalisée entre les distributions de
  croyances (§12.1, §12.3) au dernier contexte `agents` de chaque run ;
- `SocialDiff` : distance L2 normalisée entre les distributions sociales
  (§12.2, §12.3) ;
- `_DIFF_NORMALIZER = 2.0` : diviseur dans le score (deux diffs bornés `[0,1]`
  → moyenne, score borné `[0,1]`) ;
- arrondi à 6 décimales.

**Pourquoi cette équation** :

- `IsReproducible` est un **booléen strict** : la reproductibilité scientifique
  exige l'identité complète (graine, version, contenu). Un score partiel ne
  suffit pas.
- `ReproducibilityScore` est une **mesure de proximité** pour les cas non
  strictement identiques : plus les deux runs se ressemblent cognitivement et
  socialement, plus le score est proche de 1.0. C'est un outil de diagnostic,
  pas un substitut à `IsReproducible`.
- Le mode `summary` (`?light=1`) rapporte `bit_identical` / `is_reproducible`
  à `None` : ils ne peuvent pas être affirmés sans l'empreinte, et un `false`
  incidental prêterait à une conclusion fausse (`reproducibility.py:168-207`).

**Usage dans le projet** : `reproducibility.py:201-206` (summary),
`reproducibility.py:242-246` (compare).

**Interprétation** :

- `IsReproducible = true` : les deux runs sont strictement identiques
  (graine, version, contenu).
- `IsReproducible = false` avec `ReproducibilityScore` proche de 1.0 : les
  runs diffèrent (graine ou version) mais se ressemblent fortement
  cognitivement et socialement.
- `ReproducibilityScore` faible : les runs divergent nettement.
- **Unité : fraction [0, 1].** Le score n'est **jamais** un substitut à
  `IsReproducible` pour une conclusion de reproductibilité : c'est une
  mesure de proximité descriptive.

---

## 13. Méthodes statistiques post-run

Méthodes de synthèse post-exécution (`echos/analysis/calibration.py`).

### 13.1 Statistiques descriptives

**Nom** : Statistiques d'une série — `_stats(values)`

**Explication globale** : Résumé descriptif d'une liste de mesures : effectif,
minimum, moyenne, maximum.

**Expression mathématique** :

```math
\texttt{\_stats}(V) = \bigl\{\, \mathrm{count} : |V|,\;\; \mathrm{min} : \min(V),\;\; \mathrm{mean} : \mathrm{fsum}(V)/|V|,\;\; \mathrm{max} : \max(V) \,\bigr\}
```

avec :

- `fsum` : somme exacte (`math.fsum`) — précision flottante optimale, contrairement
  à `sum` Python qui accumule des erreurs d'arrondi sur les longues séries ;
- arrondi à 8 décimales pour `min`, `mean`, `max` ;
- cas limite : `V` vide → la fonction n'est pas appelée (le bloc entier est
  `None`).

**Pourquoi cette équation** :

- Le résumé descriptif (n, min, moy, max) est le **standard minimal** pour
  décrire une série sans la tronquer. Il ne prétend pas à une inférence
  statistique (pas d'écart-type, pas d'intervalle de confiance) — c'est un
  résumé **observatoire**, pas un test.
- `fsum` (somme de Neumaier) garantit que la moyenne est la plus précise
  possible pour les séries longues (des milliers de ticks).

**Usage dans le projet** : `calibration.py:22-28.

Utilisée pour : séries de besoins (énergie, faim, soif, fatigue), séries de
métriques par moteur, régime des ressources (`food`, `water`).

**Interprétation** :

- `count` : nombre d'observations réellement présentes dans la série.
- `min` / `max` : valeurs extrêmes observées.
- `mean` : valeur centrale (précision maximale via `fsum`).
- **Aucune inférence** : ces statistiques décrivent ce qui est observé, pas ce
  qui pourrait exister hors observation.

### 13.2 Pente par moindres carrés

**Nom** : Pente de régression linéaire — `_least_squares_slope(values)`

**Explication globale** : Pente de la droite de régression par moindres carrés
d'une série ordonnée, exprimée par pas d'index (qui fait office de tick pour
une série échantillonnée à cadence constante).

**Expression mathématique** :

```math
\beta = \frac{\mathrm{Cov}(x, y)}{\mathrm{Var}(x)} = \frac{\sum_{i} (x_i - \bar{x})(y_i - \bar{y})}{\sum_{i} (x_i - \bar{x})^2}
```

avec :

- `xᵢ = i` (index de la série, de 0 à N−1) ;
- `yᵢ` : valeur observée à l'index *i* ;
- `x̄ = (N − 1) / 2` : moyenne des index ;
- `ȳ = fsum(y) / N` : moyenne des valeurs (`fsum`, précision) ;
- `Var(x) = ∑ᵢ (xᵢ − x̄)²` : variance des index (dénominateur de la pente) ;
- cas limites : `N < 2` → `0.0` ; `Var(x) = 0` → `0.0` (série plate).

**Pourquoi cette équation** :

- La pente de régression linéaire est la mesure **canonique de tendance** :
  elle répond à « dans quelle direction et à quelle vitesse la série
  évolue-t-elle ? ». Contrairement à la différence première (`y_N − y_0`), elle
  utilise **toutes** les points et est robuste aux fluctuations ponctuelles.
- La série étant échantillonnée à cadence constante, l'index fait office de
  tick : la pente est donc exprimée **par tick** — unité interprétable
  (variation moyenne par tick).
- La pente est calculée sur les **200 derniers ticks** (`_VIABILITY_WINDOW =
  200`) : c'est la fenêtre des critères d'acceptation de la campagne de
  calibration. Une pente sur toute la série serait dominée par la phase
  initiale de démarrage.
- Le nom `energySlopePerTick` dit exactement ce qui est calculé : la pente de
  l'énergie moyenne par tick sur la fenêtre de viabilité.

**Usage dans le projet** : `calibration.py:31-49.
Utilisée pour : `energySlopePerTick` (viabilité, §13.3).

**Interprétation** :

- `β > 0` : la série **croît** en moyenne (énergie qui remonte).
- `β < 0` : la série **décroît** en moyenne (mort lente — signal de premier
  citoyen, même sans extinction).
- `β = 0` : série plate ou trop courte (`N < 2`).
- **Unité : unité/tick** (variation par tick). La pente est une **tendance
  observée**, pas une prévision : elle ne dit rien de ce qui arrivera après
  la fin de la fenêtre.

### 13.3 Signaux de viabilité

**Nom** : Bloc de viabilité — `_viability_block(ticks, events)`

**Explication globale** : Synthèse déterministe des signaux de viabilité
observés : pente d'énergie, part des actions sous faim saturée, régime des
ressources.

**Expression mathématique** :

Les trois composantes :

```math
1.\;\; \mathrm{energySlopePerTick} = \beta\bigl(\mathrm{\acute{e}nergie\,moyenne},\; 200\;\mathrm{derniers\,ticks}\bigr)

2.\;\; \mathrm{actionSharesWhenHungry}[\alpha] = \frac{\bigl|\{\mathrm{d\acute{e}cisions}\ \alpha\ \mathrm{sur\ les\ ticks\ faim} > 70\}\bigr|}{\bigl|\{\mathrm{toutes\ les\ d\acute{e}cisions\ sur\ ces\ ticks}\}\bigr|}

3.\;\; \mathrm{resourceRegime.food/water} = \texttt{\_stats}(\mathrm{s\acute{e}ries\ food/water})
```

avec :

- `_HUNGRY_THRESHOLD = 70.0` : seuil de faim saturée (constante) ;
- `_VIABILITY_WINDOW = 200` : fenêtre de la pente d'énergie (constante) ;
- `_EAT_OR_DRINK_ACTIONS = {Eat, Drink, SeekFood, SeekWater}` : actions
  pertinentes pour le diagnostic (constante) ;
- les actions sont lues dans les événements `decision_made` sur les ticks où
  `mean_hunger > 70`.

**Pourquoi ces équations** :

- La **mort lente** — une énergie moyenne qui descend sans extinction — est un
  signal de premier citoyen : la pente d'énergie la rend visible même quand la
  population n'est pas encore à zéro. C'est le cœur du bloc de viabilité.
- La part des actions sous faim saturée expose la **sous-priorisation
  Eat/Drink** : quand la faim est à plus de 70, quelles actions les
  entités choisissent-elles ? Une part faible de Eat/Drink signale un biais de
  décision potentiel.
- Le régime des ressources (stats food/water) donne le contexte de
  disponibilité : une pente d'énergie négative dans un régime de ressources
  épuisé a une lecture différente d'une pente négative avec des réserves
  abondantes.
- Le bloc est **observe-only** : il ne change jamais la configuration du
  simulateur. Les choix de paramètres restent explicites et nécessitent une
  décision de calibration revue (`calibration.py:1-12`).

**Usage dans le projet** : `calibration.py:77-117.
Publié dans `GET /api/runs/{id}/viability` (champ `calibration`,
`API_REST.md`).

**Interprétation** :

- `energySlopePerTick` négatif : énergie moyenne en déclin (mort lente
  possible, même sans extinction).
- `energySlopePerTick` positif : énergie moyenne en amélioration.
- `actionSharesWhenHungry` : distribution des décisions sous faim saturée —
  lire les parts par action, en croisant avec `_EAT_OR_DRINK_ACTIONS`.
- `resourceRegime` : stats min/moy/max des réserves — `null` si les colonnes
  ne sont pas présentes dans les résumés (schéma ancien).
- **Toutes ces valeurs sont descriptives** : elles ne prouvent pas une cause,
  elles exposent des faits observés.

---

## 14. Analyse causale

Méthode : reconstruction de chaînes causales hors-ligne
(`echos/analysis/causal.py`, `CAUSAL_ANALYSIS.md`, ADR-002).

### 14.1 Chaîne de reconstruction

**Nom** : Chaîne causale déterministe — `build_chain`

**Explication globale** : Reconstruction de la chaîne
`Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`
d'une entité à un tick, à partir des traces persécutées. Ce n'est pas une
équation : c'est une **méthode de reconstruction déterministe** qui suit une
règle de priorité de sources.

**Expression mathématique** :

Pas d'équation scalaire. La reconstruction suit l'ordre de profondeur :

```math
\mathrm{Action} \leftarrow \texttt{decision\_traces} (\mathrm{action\ du\ tick})

\mathrm{Intention} \leftarrow \text{event } \texttt{decision\_made} \text{ correspondant } (\mathrm{value.intention})

\mathrm{Objectif} \leftarrow \mathrm{contexte\ agents} (\mathrm{dernier} \le \mathrm{tick}) : \mathrm{goals[].kind}

\mathrm{Besoin} \leftarrow \texttt{decision\_traces} (\mathrm{compteurs\ BDI}) + \mathrm{contexte\ agents} (\mathrm{needs})

\mathrm{Croyance} \leftarrow \mathrm{contexte\ agents} (\mathrm{beliefs},\ \mathrm{tri\acute{e}s\ par\ sujet})

\mathrm{M\acute{e}moire} \leftarrow \texttt{decision\_traces} (\mathrm{compteurs\ de\ m\acute{e}moire}) + \mathrm{contexte\ agents}

\mathrm{Perception} \leftarrow \mathrm{events}\ \texttt{message\_received}\ \mathrm{de}\ \texttt{events\_log}\ (\mathrm{tick\ desc},\ \mathrm{id\ desc})
```

Contraintes de déterminisme (ECHOS-041/045) :

- aucun horodatage d'émission, aucun tirage ;
- ordres stables : besoins par (valeur desc, clé), croyances par sujet trié,
  perceptions par (tick desc, événement id desc) ;
- chaque couche contribue **au plus un nœud** ; les multiples (croyances,
  perceptions) sont agrégés dans `detail` ;
- profondeur d'affichage : `DEFAULT_DEPTH = 7`, plafond `MAX_DEPTH = 12` ;
- boucles de rétroaction : récurrence (`cycle=True` + `cycles`) si l'entité a
  déjà choisi la même action aux 16 ticks précédents
  (`RECURRENCE_WINDOW = 16`) ; doublon intra-chaîne → `cutoff` (la chaîne
  s'arrête au seuil du retour).

**Pourquoi cette méthode** :

- La reconstruction suit la **chaîne BDI** (Belief-Desire-Intention) du
  modèle cognitif de SYNE : `Action ← Intention ← Objectif ← Besoin ← Croyance
  ← Mémoire ← Perception`. C'est l'ordre de profondeur du modèle, pas une
  inférence statistique.
- Le mode **hors-ligne** (ADR-002) est essentiel : les traces sont persistées
  (`decision_traces` + `events_log`) et la reconstruction s'exécute après le
  run, sur des données figées. Cela garantit le déterminisme et évite toute
  interférence avec l'exécution en cours.
- La règle « au plus un nœud par couche » borne la profondeur et évite les
  arbres de reconstruction explosifs : les multiples sont agrégés dans
  `detail`, jamais dupliqués comme nœuds séparés.
- Les boucles de rétroaction sont **détectées** (récurrence, cutoff) mais
  **pas étendues** : la profondeur d'affichage reste bornée
  (`CAUSAL_ANALYSIS.md` §4.5.3).

**Usage dans le projet** : `causal.py:39-58` (constantes),
`causal.py:75+` (`_trace_for` et reconstruction).
Cache : `CausalCache` LRU, invalidé sur `AnalyticsStore.ingest_version`
(re-run ⇒ re-analyse, ECHOS-063).

**Interprétation** :

- La chaîne reconstruite montre le **chemin de décision** d'une entité à un
  tick, tel qu'il est **reconstruit depuis les traces** — pas tel qu'il s'est
  réellement produit dans le cerveau de l'entité (qui n'existe pas : SYNE est
  un simulateur).
- `cutoff` signale un doublon intra-chaîne : la reconstruction s'arrête au
  seuil du retour, pour éviter une récursion infinie.
- `cycle = true` avec `cycles` liste les occurrences antérieures de la même
  action : c'est un signal de **récurrence observée**, pas une preuve de
  boucle causale au sens fort.
- La reconstruction est **déterministe** : mêmes traces → mêmes chaînes,
  toujours. Deux analystes sur les mêmes données obtiennent la même chaîne.

---

## 15. Interprétation et limites

### 15.1 Unités et comparabilités

Les unités publiées par le catalogue (`GET /api/metrics/catalog`) font foi.
Rappel des règles de comparabilité (`METRICS_DICTIONARY.md`) :

| Unité | Comparabilité |
| :-- | :-- |
| `fraction [0,1]` | Comparables en ordre de grandeur entre runs, sous réserve que les dénominateurs soient identiques ou publiés |
| `bits` | **Non comparables** entre populations de tailles différentes ni entre espaces de catégories de dimensions différentes |
| `ticks` | Durées comparables si la cadence de simulation est identique |
| `count` | Comptages bruts — toujours lire avec leur dénominateur |
| `unité²` (variances) | Carrés de l'unité source — **jamais** comparables à la grandeur de base |
| `messages/entité/tick`, `groupes/1000 ticks`, `unité/tick` | Débits normalisés — comparables si les fenêtres d'observation sont rapportées |

**Règle de lecture commune** (§3.3 de `METRICS_DICTIONARY.md`) :

1. Chercher la **fiche** (`/api/metrics/catalog`) : unité, domaine, population,
   fenêtre, statut, avertissement.
2. Vérifier la **couverture** : `measured_by_tick`, `missing_ticks`,
   `completeness`, `conservation`.
3. N'afficher que ce que la fiche autorise (`visual`) — jamais de mélange
   d'unités sur un axe, jamais de `false` rendu comme `0`.

### 15.2 Constantes et seuils non calibrés

Les constantes suivantes sont **[HÉRITÉ] et non calibrées** — aucune campagne
de référence ni analyse de sensibilité ne les a validées :

| Constante | Valeur | Localisation | Impact |
| :-- | --: | :-- | :-- |
| Poids `EmergenceScore` | 0.15/0.15/0.10/0.15/0.20/0.25 | `emergence.py:53-60` | Score composite |
| `_DIFFUSION_HORIZON` | 100 ticks | `emergence.py:198` | `CoverageDelay_Norm`, `SystemComplexity` |
| Seuils des phénomènes | 2, 5, 0.7, 0.3, 5+1 | `emergence.py:127-178` | `DetectedPhenomena` |
| `_DECAY_PER_HOP` | 0.9 (10 %/saut) | `information_propagation.py:65` | `TheoreticalHopDecay` |
| `_DIFFUSION_COVERAGE` | 0.8 (80 %) | `information_propagation.py:68` | `EmitterCoverageDelay` |
| `FREQUENCY_THRESHOLD` | 2 | `feedback_loop_detector.py:65` | `RepeatedActionPairs` |
| `AMPLIFICATION_FACTOR` | 1.5 | `feedback_loop_detector.py:66` | `AmplifiedRepetitions` |
| `_CRITICAL_RATIO` | 0.2 (20 %) | `resource_sustainability.py:62` | `CriticalResourceCount`, crises |
| `_RECOVERED_RATIO` | 0.8 (80 %) | `resource_sustainability.py:63` | `RecoveryTime`, récupérations |
| `_RATE_WINDOW_TICKS` | 1000 ticks | `group_dynamics.py:88` | `GroupFormationRate`, `GroupDissolutionRate` |
| `_HUNGRY_THRESHOLD` | 70.0 | `calibration.py:53` | `actionSharesWhenHungry` |
| `_VIABILITY_WINDOW` | 200 ticks | `calibration.py:54` | `energySlopePerTick` |
| `_DIFF_NORMALIZER` | 2.0 | `reproducibility.py:29` | Distance L2, `ReproducibilityScore` |

**Avant de présenter une valeur produite par une de ces constantes comme un
seuil de décision**, une campagne de calibration sur des runs de référence
(survivants, éteints, interrompus) et une analyse de sensibilité sont
nécessaires (`METRICS_SPEC.md` §13 impose cette revue).

### 15.3 Règle d'or et disclaimer

> **ECHOS ne doit jamais transformer une métrique en vérité scientifique.**
> Un score d'émergence, une entropie, une densité ou une pente reste une
> mesure particulière d'un phénomène, jamais une preuve de l'existence d'une
> intelligence, d'une société ou d'un phénomène émergent.

Le champ `Disclaimer` (invariant, ECHOS-032, `emergence.py:76-81`) est
affiché tel quel par le Launcher à côté du score d'émergence :

> « ECHOS ne doit jamais transformer une métrique en vérité scientifique :
> un score d'émergence ou une valeur de centralité reste une mesure
> particulière d'un phénomène, jamais une preuve de l'existence d'une
> intelligence ou d'une société (Monographie §4.10.3, ECHOS-032). »

Les statuts du catalogue complètent ce disclaimer :

- `measured` : la métrique est calculée sur des données réellement observées ;
- `exploratory` : la métrique est calculée mais n'a pas fait l'objet de
  calibration ni de validation sur des campagnes de référence ;
- `suspended` : la métrique ne produit aucun résultat.

**Une absence de détection n'est pas l'absence du phénomène** : les seuils
non calibrés des phénomènes (§11.5) produisent des faux négatifs possibles.
L'interface doit l'écrire à côté de la liste vide.

---

## Points restés ouverts dans ce document

- **Calibration des constantes** : toutes les constantes listées en §15.2
  sont [HÉRITÉ] et non calibrées. Une campagne de référence (survivantes,
  éteintes, interrompues) et une analyse de sensibilité sont nécessaires
  avant de présenter ces valeurs comme des seuils de décision
  (`METRICS_SPEC.md` §13).
- **Poids du score composite** : leur sensibilité reste à étudier ; la valeur
  de référence du golden file (`EmergenceScore = 0.7791446071170001` sur
  `snapshot_analysis.json`) sert de point de comparaison.
- **Horizon de diffusion (100 ticks)** : à calibrer sur des campagnes de
  référence avant d'être présenté comme un horizon de décision.
- **Stabilité d'identité des communautés** : `CommunitySizeMatch` compare des
  tailles, pas des membres. Une stabilité d'identité (Jaccard entre partitions)
  exigerait que le contrat de `communityHistory` publie les membres — lacune
  tracée dans l'inventaire dimension → contrat → vue.
- **Dispersion par métrique** : `AverageGoalAge`, `AverageTrustLevel` et
  `AverageCommunitySize` publient une moyenne sans variance associée. Une
  extension future pourrait publier les écarts-types pour distinguer
  « valeur typique » de « dispersion large ».
- **Alignement Monographie** : la Monographie §4.3 et §4.4 contient des
  noms et comptages antérieurs aux renommages P0/P1 et à la refonte P2 du
  composite. Ce document est aligné sur le **code** et la **documentation
  ECHOS** (catalogue 2.0.0) ; la Monographie n'a pas été utilisée comme
  source pour les formules.
- **Vérification doc ↔ code en CI** : l'alignement de ce document avec le
  code devrait être verrouillé par les golden files (`TESTING.md`) et la
  revue de changement (`METRICS_SPEC.md` §13) : toute modification de formule
  doit être reflétée ici, dans le catalogue et dans le CHANGELOG.
