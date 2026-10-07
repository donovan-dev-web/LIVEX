# EMERGENCE_INDICATORS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `METRICS_SPEC.md`, `METRICS_DICTIONARY.md`, `ANALYSIS_FOUNDATIONS.md`, `../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`
**Source Monographie** : §4.4, §4.10.3

---

## 1. Le score d'émergence composite

ECHOS calcule un **score d'émergence** composite (borné sur **[0, 1]**) à partir
de **six composantes déjà normalisées**. Les justifications mathématiques détaillées
(normalisation des entropies, neutralité du délai de diffusion, provenance des
contributions) sont dans `ANALYSIS_FOUNDATIONS.md` §11 ; ce document décrit le
score publié et son interprétation.

```math
\mathrm{EmergenceScore} = \sum_{i} w_i \cdot c_i \qquad (c_i \in [0,\, 1],\ \textstyle\sum_i w_i = 1.0)

\begin{array}{lll}
\mathrm{BeliefDiversityNorm} & \text{entropie des croyances / maximum atteignable} & \times 0.15 \\
\mathrm{GoalDiversityNorm} & \text{entropie des objectifs / maximum atteignable} & \times 0.15 \\
\mathrm{CoverageDelay\_Norm} & \mathrm{clamp}\!\bigl(1 - \mathrm{EmitterCoverageDelay}/100,\ 0,\ 1\bigr) & \times 0.10 \\
\mathrm{ClusteringCoefficient} & \text{coefficient de clustering} & \times 0.15 \\
\mathrm{RepeatedActionShare} & \text{part moyenne des répétitions d'action} & \times 0.20 \\
\mathrm{CommunityCoverage} & \text{part de population en communauté} & \times 0.25
\end{array}
```

- **Composantes bornées [0, 1]** : le score est déjà dans l'intervalle, le
  `clamp` final n'est plus qu'une garde — plus de saturation dans le haut de
  l'échelle (refonte P2, `RAPPORT-ANALYSE-ECHOS-LAUNCHER.md` §9).
- **Normalisation explicite** : les entropies brutes (bits) croissent avec le
  nombre de catégories ; elles ne sont additionnées qu'après division par leur
  maximum (`BeliefDiversityNorm`, `GoalDiversityNorm`).
- **Horizon de diffusion** : `_DIFFUSION_HORIZON = 100` ticks **[HÉRITÉ]** —
  `1 − délai/100` interpole entre « couvert en un tick » (1.0) et « non couvert
  dans l'horizon » (0.0).
- **Neutralité V0.1 (ECHOS-006)** : délai **non mesuré** (absent ou ≤ 0,
  « jamais diffusé ») → contribution **0.0**, jamais le maximum de la formule
  (une vitesse nulle signifierait une diffusion instantanée, inobservable).
- **Poids [HÉRITÉ]** de la Monographie §4.4, **non calibrés** : ils sont
  **publiés** (`emergence.WEIGHTS`) et non codés en dur dans la formule, pour
  qu'une analyse de sensibilité puisse les examiner sans changer la formule
  silencieusement (revue de changement, `METRICS_SPEC.md` §13).

**Implémenté (ECHOS-030 → ECHOS-033)** : moteur `echos/analysis/emergence.py`,
valeurs de référence verrouillées par `tests/golden/analysis_golden.json`
(`EmergenceScore = 0.7791446071170001` sur `snapshot_analysis.json`) et par les
tests de main levée (`test_emergence.py` : composantes à 0,5 → score 0,5).

### 1.1 Contributions publiées

Chaque terme du score est publié comme métrique `Contribution*`, arrondi à
12 décimales :

```math
\mathrm{ContributionBeliefDiversity} + \mathrm{ContributionGoalDiversity} + \mathrm{ContributionEmitterCoverage}
+ \mathrm{ContributionClustering} + \mathrm{ContributionRepeatedActions} + \mathrm{ContributionCommunityCoverage}
\;=\; \mathrm{EmergenceScore}
```

L'égalité est un **invariant testé** (`test_contributions_sum_to_the_score`) :
n'importe quelle interface décompose le score tel quel, sans réexécution ni
recalcul local (ADR-003 : le Launcher affiche, il ne recalcule pas).

### 1.2 Provenance du composite

La provenance (`measured`) d'une sortie composite est calculée depuis
`COMPOSITE_DEPENDENCIES` : chaque sortie n'exige que **ses** dépendances
réelles (`EmergenceScore` → l'union des dépendances de ses six contributions).
Exiger l'intégralité des moteurs entrants marquait « non mesuré » un score entier
à cause d'une métrique incidente qui n'entre pas dans sa formule.

Conséquence d'affichage : un score dont une composante source est **non mesurée**
devient lui-même non mesuré et ne doit pas être rendu comme `0`.

> ⚠️ **Avertissement méthodologique** : ce score est une **heuristique
> d'observation** (statut `exploratory` au catalogue), pas une preuve
> scientifique d'émergence. Le champ `Disclaimer` (invariant, ECHOS-032) est
> affiché tel quel par le Launcher.

## 2. Les phénomènes auto-détectés

| Phénomène | Identifiant (contrat) | Condition (tous les signaux) |
| :-- | :-- | :-- |
| Seuil de communautés franchi | `CommunityFormation` | `NumberOfCommunities > 2` |
| Répétitions d'action soutenues | `FeedbackLoops` | `RepeatedActionPairs > 5` |
| Convergence forte des objectifs | `CollectiveCoordination` | `GoalConvergence > 0.7` |
| Concentration des émetteurs | `InformationBottleneck` | `SenderConcentration > 0.3` |
| Communautés nombreuses et sorties de membres | `OrganizationalDynamics` | `InferredCommunities > 5` **et** `MemberExitsPerDissolution > 1` |

**Implémenté (ECHOS-031)** : sortie `DetectedPhenomena` — liste
`{identifier, label (français), description, signals}` où `signals` est la
**trace des signaux déclencheurs** `[{metric, value, threshold}, …]` (tous les
signaux de la condition, valeur observée et seuil). Ordre d'émission stable
(identifiants du tableau).

**Requalification P0** : les **identifiants restent stables** (clé de contrat
publique, `renamedFrom` sans objet) ; les **libellés et descriptions** décrivent
ce qui est réellement observé — un comptage, pas une formation ; des
répétitions, pas une boucle causale ; une concordance de catégories, pas une
coordination ; des émissions, jamais des réceptions.

**Seuils non calibrés** [HÉRITÉ] : ils viennent de la spec héritée et n'ont fait
l'objet d'aucune campagne de référence. **Une absence de détection n'est pas
l'absence du phénomène** — l'interface doit l'écrire à côté de la liste vide.
Sur la fixture d'analyse, seul `InformationBottleneck` est détecté.

## 3. La complexité du système

```math
\mathrm{SystemComplexity} = \mathrm{clamp}\!\left(\frac{\mathrm{BeliefDiversityNorm} + \mathrm{GoalDiversityNorm} + \mathrm{CoverageDelay\_Norm}}{3},\ 0,\ 1\right)
```

Formule canonique bornée (décision P2) : trois **grandeurs normalisées**, plus
de ticks bruts. L'ancienne formule intégrait `InformationDiffusionSpeed` en
ticks — un nombre non borné qui faisait croître l'indicateur avec la durée du
run (10, 100, 1000… selon la longueur du run).

Test de non-régression : `EmitterCoverageDelay` au-delà de l'horizon →
contribution de diffusion neutre (0,0), `SystemComplexity` plafonné à 2/3 dans
ce cas ; jamais négatif, jamais croissant avec les ticks.

Ce n'est **pas** une approximation de la complexité de Kolmogorov : c'est une
moyenne de trois dispersions normalisées (statut `exploratoire`).

## 4. Décisions d'implémentation (jalon ECHOS ph3 → refonte P2)

| Point | Décision | Justification |
| :-- | :-- | :-- |
| **Formule `EmergenceScore`** | Formule ECHOS (§1), **sans** la division `/5` du prototype Monographie §4.4.1 ; composantes normalisées | Les poids somment à 1.0 → score déjà borné [0,1] ; le `/5` du prototype est contradictoire (max 0.2). Entropies brutes remplacées par `*Norm` : plus de saturation |
| **Contributions** | Six métriques `Contribution*` publiées, Σ == score | Le score se décompose dans n'importe quelle interface, sans réexécution (test invariant) |
| **Provenance du composite** | Propagation depuis `COMPOSITE_DEPENDENCIES` | Une sortie n'exige que ses propres dépendances (§1.2) |
| **`DecisionVariability`** | **`UnpredictabilityIndex` retiré** (décision P0) | Le produit d'une fréquence de répétition et d'un ratio d'actions distinctes ne mesure pas l'imprévisibilité, qui exigerait une baseline prédictive hors échantillon |
| **`SystemComplexity`** | Formule bornée (§3) | L'ancienne formule croissante avec les ticks était illisible comme « complexité » |
| **`CoverageDelay_Norm`** | Neutralité : délai non mesuré → 0.0 | Convention ECHOS-006 (données manquantes → neutres, jamais le max) |
| **Statut** | `exploratoire` pour le score et ses contributions | Aucune campagne de calibration ni analyse de sensibilité n'a validé les poids |

---

## Points restés ouverts dans ce document

- Les poids du score composite (0.15/0.15/0.10/0.15/0.20/0.25) sont [HÉRITÉ] de
  la Monographie — leur sensibilité reste à étudier (`METRICS_SPEC.md` §13
  impose une analyse de sensibilité avant tout changement) ; la valeur de
  référence du golden file sert de point de comparaison.
- L'horizon de diffusion (100 ticks) et les seuils des phénomènes (§2) sont
  [HÉRITÉ] : à calibrer sur des campagnes de référence (survivantes, éteintes,
  interrompues) avant d'être présentés comme des seuils de décision.
- Une projection de survie ou un indice synthétique, s'il est un jour retenu,
  devra être évalué sur des seeds **exclus de la calibration** et exposer
  incertitude et version (`RAPPORT-ANALYSE-ECHOS-LAUNCHER.md` §9, P2).
- **Successeur conceptuel** : `DYNAMIC_VIABILITY_INDEX.md` (**[DRAFT]**)
  spécifie le DVI comme indice de viabilité dynamique — moyenne géométrique
  pondérée (aucune compensation), couverture κ, régimes STATIC/CHAOTIC,
  trajectoire post-run sans ML. Décision 1A : **cohabitation** — ce moteur
  reste le composite implémenté et publié tant que le DVI n'est ni implémenté
  ni calibré ; le DVI ne consomme pas `EmergenceScore`.
