# METRICS_DICTIONARY.md

**Composant** : ECHOS (+ lecture Launcher)
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `METRICS_SPEC.md`, `API_REST.md`, `../docs-launcher/USER_INTERFACE.md` §9.2
**Source Monographie** : §4.3, §4.7

---

## 1. Dictionnaire des unités

Unités publiées par le catalogue (`GET /api/metrics/catalog`, champ `unit`) et
conventions d'affichage associées. Une unité ne se déduit **jamais** d'un
graphique : elle vient de la fiche.

| Unité (`unit`) | Signification | Affichage attendu | Exemples |
| :-- | :-- | :-- | :-- |
| `fraction` | rapport **[0, 1]** (part, probabilité, proportion) | nombre décimal tel quel ; **jamais** de conversion implicite en % par le Launcher | `NetworkDensity`, `SenderConcentration`, `ResourceFillRatio`, `EmergenceScore` |
| `bits` | entropie de Shannon brute (≥ 0, croît avec le nombre de catégories) | nombre décimal, axe nommé « bits » ; **non comparable** entre populations de tailles différentes | `BeliefDiversity`, `GoalDiversity` |
| `ticks` | durée mesurée en ticks de simulation | entier ; distinct d'un tick **absolu** | `EmitterCoverageDelay`, `RecoveryTime`, `AverageGoalAge`, `AverageGroupLifetime` |
| `count` | comptage d'occurrences dans la fenêtre publiée | entier ; 0 observé ≠ non mesuré | `DecisionCount`, `CriticalResourceCount`, `FormationCount` |
| `sauts` | nombre maximal de sauts d'un message | entier | `MaxMessageHops` |
| `messages/entité/tick` | volume rapporté à la population vivante et au tick | décimal | `MessageVolume` |
| `groupes/1000 ticks` | événements normalisés sur la fenêtre **réellement observée** | décimal ; le dénominateur est publié avec | `GroupFormationRate`, `GroupDissolutionRate` |
| `entités` | taille moyenne d'une communauté inférée | décimal | `AverageCommunitySize` |
| `membres` | sorties moyennes par dissolution observée | décimal | `MemberExitsPerDissolution` |
| `unité/tick` | volume consommé rapporté à la durée observée | décimal | `ConsumptionPerTick` |
| `confiance` | poids de confiance déclaré [0, 1] | décimal | `AverageTrustLevel` |
| `unité² (confiance)`, `unité² (traits)`, `unité²` | variance : carré de l'unité source | décimal, **jamais** présenté comme une valeur de la grandeur de base | `BeliefConfidenceVariance`, `TrustVariance` |
| `non numérique` | sortie structurée (comptages classés, liste, texte) | affichée telle quelle, jamais dans une courbe | `GoalTypeCounts`, `RepeatedActionCounts`, `DetectedPhenomena`, `Disclaimer` |

**Compteurs de population** (dénominateurs) : `aliveCount` (entités vivantes),
paire d'entités (`n(n−1)/2` pour `NetworkDensity`), émetteurs observés
(`SenderCoverage`), réserves dont la capacité est publiée (`ResourceCoverage`),
dissolutions publiant `success` (`DissolvedGroupSuccessShare`). Chaque fiche
porte son dénominateur dans `population` — le lire avant tout ratio.

## 2. Conventions d'échelle

| Convention | Règle |
| :-- | :-- |
| **Axe temporel** | ticks **réels** publiés (`ticks[]`), jamais un index d'échantillon ; une lacune se voit (point `null`), l'axe n'est pas décalé |
| **Fractions** | valeurs [0, 1] servies brutes ; la conversion en %, si elle existe un jour, est une décision d'affichage explicite et documentée |
| **Entropies** | `bits` brut vs `*Norm` fraction [0,1] : deux unités, deux séries, **jamais** sur le même axe |
| **Variances** | unité au carré : elles ne se lisent ni ne se comparent à la grandeur d'origine |
| **Valeur nulle** | `0` publié = **zéro observé** ; `measured=false` = repli neutre (**non mesuré**) ; `null` = tick sans observation. Trois affichages distincts |
| **Échantillonnage** | `?every=N` (lecture) et `context_every` (contextes détaillés) réduisent la couverture : l'âge réel d'un contexte est publié (`API_REST.md` §3.11, §3.18) |
| **Statut** | `exploratory` s'affiche toujours accompagné de la mention ; `suspended` ne produit aucun résultat |
| **Comparabilité** | deux runs ne se comparent que si `conservation`, `context_every`, `analysis_every`, version et graine sont connus (`/api/experiments/summary`) |
| **Arrondi** | le Launcher affiche `0.###` (3 décimales max, séparateur invariant) sans jamais modifier la valeur publiée |

## 3. Matrice métrique → API → vue

Lecture à trois colonnes : **quelle donnée**, **par quel endpoint**, **où elle
apparaît dans la fenêtre d'analyse** (`USER_INTERFACE.md` §9.2). Toute nouvelle
métrique ou vue doit prolonger ce tableau (revue de changement,
`METRICS_SPEC.md` §13).

### 3.1 Séries et valeurs par moteur

| Moteur | Endpoint(s) | Vue du Launcher | Forme affichée |
| :-- | :-- | :-- | :-- |
| `CognitiveDiversityMetrics` | `/api/runs/{id}/metrics`, `/api/runs/{id}` (dernières valeurs), `/api/experiments/summary` | **Comportements** (courbe), **Statistiques exactes** (tableau), **Comparer** | courbe par tick ; ligne de tableau avec statut et provenance |
| `InformationPropagationMetrics` | idem | idem | idem |
| `SocialComplexityMetrics` | idem + `/api/trust-graph`, `/api/groups` | **Comportements**, **Statistiques exactes**, **Relations et groupes** | courbe ; tableau ; graphe natif (nœuds/arêtes publiés) |
| `GoalConvergenceMetrics` | idem | **Comportements**, **Statistiques exactes**, **Comparer** | courbe ; tableau |
| `FeedbackLoopDetector` | idem | **Statistiques exactes** (section des mesures exploratoires) | tableau étiqueté « exploratoire » |
| `ResourceSustainabilityMetrics` | idem + `/api/runs/{id}/viability` (`resources`, `decisions`) | **Viabilité** (panneau « réserves »), **Comportements** | panneau par unité ; courbe |
| `GroupDynamicsMetrics` | idem + `/api/groups` | **Comportements**, **Relations et groupes**, **Viabilité** | courbe ; graphe ; lignes de résumé |
| `EmergenceIndicators` | `/api/runs/{id}/metrics`, `/api/runs/{id}` (`phenomena`), `/api/emergent-phenomena` | **Comportements** (score + signaux), **Statistiques exactes** | courbe du score ; signaux « selon la règle X » avec valeur et seuil |

### 3.2 Résumé, viabilité et comparaison (contrats dédiés)

| Donnée | Endpoint | Vue du Launcher |
| :-- | :-- | :-- |
| Issue, populations initiale/finales/minimum, premier tick nul, ticks manquants | `/api/runs/{id}/viability` (`population`, `completeness`) | **Viabilité** (en-tête et lignes de faits) |
| Séries de besoins (énergie, faim, soif, fatigue) | `/api/runs/{id}/viability` (`needs`) | **Viabilité** — panneau « besoins » (même unité) |
| Réserves (nourriture, eau) | `/api/runs/{id}/viability` (`resources`) | **Viabilité** — panneau « réserves » |
| Rapport post-run : pente d'énergie, décisions sous faim > 70, régime des ressources | `/api/runs/{id}/viability` (`calibration`, `viability`) | **Viabilité** — bloc « rapport post-run » (étiqueté comme tel) |
| Chronologie d'extinction | `/api/runs/{id}/viability` (`extinctionChronology`) | **Viabilité** — chronologie descriptive |
| Contexte de contrôle (version, graine, issue, conservation) + dispersion | `/api/experiments/summary` | **Comparer** (tableau de contexte + tableau de dispersion) |
| Reproductibilité (empreinte, `bit_identical`) | `/api/compare` | hors fenêtre d'analyse (outil de campagne) |
| Fiches (unité, domaine, dénominateur, fenêtre, statut, avertissement, `renamedFrom`) | `/api/metrics/catalog` | info-bulles et colonnes de statut, **toutes sections** |
| Provenance par tick | `/api/runs/{id}/metrics` (`measured_by_tick`) | masque de trou dans les courbes, colonne « statut » du tableau |
| État du run (terminé / possiblement en cours / incomplet) | `/api/runs/{id}/viability` (`outcome`, `extinction_tick`, `last_tick`, `completeness.missingTickCount`) | **Viabilité** — ligne « État du run » (description des faits, aucun état déduit) |
| Journal d'événements (type, tick, entité, action, cause) | `/api/runs/{id}/events` (`total`, `limit`, `types`, `events`) | **Comportements** — marqueurs verticaux sur la courbe (16 max, fenêtre affichée) ; case « Marqueurs d'événements » |
| Cadence et âge d'un contexte échantillonné | run → `conservation.sampledDetails.agentContextEvery` + `last_tick` | **Entités** — en-tête de fiche « cadence N tick(s) (âge ≤ N-1) · X tick(s) derrière le dernier » |
| Distribution d'une métrique (intervalles + effectifs) | `/api/runs/{id}/metrics` (valeurs + `measured_by_tick`) | **Statistiques exactes** — outil de distribution (comptage de classe rendu, exclusions comptées, aucune extrapolation) |
| Description du monde, entités, réserves | `/api/world` | **Monde et territoires** |
| Confiance, croyances, décisions d'une entité | `/api/trust-graph`, `/api/beliefs/{id}`, `/api/relationships/{id}`, `/api/runs/{id}/decisions` | **Relations et groupes**, **Entités** |

### 3.3 Règle de lecture commune

1. Chercher la **fiche** (`/api/metrics/catalog`) : unité, domaine, population,
   fenêtre, statut, avertissement.
2. Vérifier la **couverture** : `measured_by_tick`, `missing_ticks`,
   `completeness`, `conservation`.
3. N'afficher que ce que la fiche autorise (`visual`) — jamais de mélange
   d'unités sur un axe, jamais de `false` rendu comme `0`.

## Points restés ouverts dans ce document

- Le format d'affichage des fractions (décimal vs pourcentage) n'est pas tranché :
  toute décision devra être documentée ici et testée côté Launcher.
- Les unités des séries SYNE non publiées comme métriques (saisons, débits) ne
  sont pas encore dans ce dictionnaire — elles n'ont pas d'endpoint dédié.
- La matrice §3 doit être relue à chaque ajout de sous-écran (contrôle de revue
  imposé par `METRICS_SPEC.md` §13).
