# DYNAMIC_VIABILITY_INDEX.md

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `ANALYSIS_FOUNDATIONS.md`, `METRICS_SPEC.md`, `EMERGENCE_INDICATORS.md`, `METRICS_DICTIONARY.md`, `REFERENCE_SCENARIOS.md`, `LIMITATIONS.md`
**Source Monographie** : — (document indépendant ; formules alignées sur le code ECHOS et le catalogue 2.0.0)

---

## 1. Positionnement et objet

### 1.1 Ce que ce document spécifie

Ce document spécifie l'**Indice de Viabilité Dynamique** (DVI) : un cadre
mathématique unique pour estimer si une simulation LIVEX se trouve dans un
**régime dynamique viable** — ni figé, ni chaotique — à partir de l'état
observé et de l'histoire récente du monde.

La question à laquelle l'indice répond :

> **À partir de l'état actuel et de l'histoire récente du monde, celui-ci
> possède-t-il les propriétés nécessaires pour rester durablement actif,
> diversifié, organisé, capable de se renouveler et de résorber ses crises ?**

L'indice ne maximise ni la stabilité ni l'activité. Un monde immobile et un
monde chaotique doivent tous deux obtenir un faible score. Le régime recherché
est un **régime dynamique viable** : activité dans une zone optimale,
diversité soutenue, structures organisées, renouvellement observé, crises
résorbées, séries stables sans être figées.

Ce document est le **complément mathématique** d'`ANALYSIS_FOUNDATIONS.md` :
ce dernier documente les équations du cœur analytique implémenté ; celui-ci
spécifie le cadre de viabilité destiné à être implémenté. Les deux partagent
les mêmes principes méthodologiques (§2) et les mêmes outils partagés
(§4, renvoi `ANALYSIS_FOUNDATIONS.md` §3).

### 1.2 Rapport à `EmergenceScore`

`EmergenceScore` (`EMERGENCE_INDICATORS.md`, moteur `EmergenceIndicators`)
reste le **composite implémenté** d'ECHOS. Le DVI lui est **conceptuellement
supérieur** pour la question de la viabilité, sans le remplacer
immédiatement (décision 1A — cohabitation) :

| | `EmergenceScore` | DVI |
| :-- | :-- | :-- |
| Question posée | « Quelles structures cognitives/sociales sont observées à ce tick ? » | « Le monde est-il dans un régime dynamique viable ? » |
| Agrégation | Somme pondérée arithmétique | Moyenne géométrique pondérée |
| Compensation entre composantes | Totale (une composante haute compense une basse) | Interdite (une composante mesurée à 0 annule le noyau) |
| Temporalité | Fenêtre courte, lecture instantanée | Fenêtre courte (moteur) + trajectoire (post-run) |
| Activité | Non modélisée (ni zone optimale, ni pénalisation du figé/chaos) | Composante centrale avec zone optimale |
| Régénération / résilience / persistance | Absentes | Composantes dédiées |
| Poids | [HÉRITÉ] Monographie, non calibrés | [HÉRITÉ] proposés, publiés, calibrables par campagne |

Le DVI **ne consomme pas** `EmergenceScore` : les deux composites lisent des
métriques primitives partagées (croyances, clustering, communautés) mais les
agrègent avec des sémantiques différentes. `EmergenceScore` demeure publié
tel quel jusqu'à implémentation et calibration du DVI ; aucune modification du
code existante n'est exigée par ce document.

### 1.3 Ce que l'indice ne fait pas

- **Pas de modèle d'apprentissage** : aucune prédiction `P(viabilité future)`,
  aucun paramètre θ appris, aucune optimisation automatique des poids
  (contrainte ECHOS : déterminisme strict, `ANALYSIS_FOUNDATIONS.md` §2.1–2.2).
- **Pas de preuve scientifique** : le DVI est une mesure d'observation
  (statut `exploratory` tant que non calibré), jamais la preuve qu'un monde
  est « vivant », « intelligent » ou « social » (règle d'or,
  `VISION.md`, ECHOS-032).
- **Pas de manipulation du monde** : ECHOS observe ; il n'injecte aucune
  perturbation contrôlée. La résilience est donc mesurée sur les **crises
  spontanées** uniquement (§5.5).
- **Pas de score synthétique dans `/api/runs/{id}/viability`** : cet endpoint
  publie les séries sources sans agrégat (décision existante,
  `test_viability_endpoint_exposes_source_series_without_synthetic_score`).
  Le DVI s'ajoute comme **moteur de métriques** et comme **méthode post-run**,
  sans changer ce contrat.

### 1.4 Périmètre : deux couches

Le DVI existe en **deux couches** qui partagent les mêmes composantes
(§5–§6) mais n'ont pas la même profondeur temporelle (décision 2A) :

| Couche | Portée temporelle | Entrées | Sorties | Quand |
| :-- | :-- | :-- | :-- | :-- |
| **Moteur** (`ViabilityIndicators`) | Fenêtre glissante ≤ 100 ticks | Snapshot : `agents`, `history`, `events`, `eventWindow`, `resources`, `aliveCount`, `communityHistory` + résultats des moteurs entrants | Composantes, `ViabilityScore`, `ViabilityCoverage`, `ViabilityRegime` | À chaque tick d'analyse |
| **Post-run** (méthode) | Run complet | `tick_summaries`, `events_log`, `tick_metrics`, `decision_traces` | Séries, pentes, marges aux seuils, `ViabilityTrajectory`, avertissements | Fin de run ou à la demande |

Le moteur fournit un indice **en direct**, pauvre en profondeur mais
déterministe et borné. La méthode post-run fournit la **trajectoire** et les
**diagnostics de crise**, sans être un multiplicateur opaque de l'indice
(décision 3A).

---

## 2. Principes fondateurs

Ces principes prolongent `ANALYSIS_FOUNDATIONS.md` §2. Les ignorer produit des
interprétations fausses, quelles que soient les valeurs affichées.

### 2.1 Neutralité de l'observateur et déterminisme

Aucune formule du DVI ne modifie l'état de SYNE. Aucune ne dépend d'un
générateur aléatoire, d'un horodatage d'émission ni d'un modèle appris :
mêmes entrées → mêmes sorties (ECHOS-027). Les distributions et listes sont
triées avant émission.

### 2.2 Zéro observé ≠ non mesuré

La distinction de `ANALYSIS_FOUNDATIONS.md` §2.3 s'applique intégralement au
DVI :

| Affichage | Signification | Cause |
| :-- | :-- | :-- |
| `0.0`, `measured = true` | **Zéro observé** — la formule a été exécutée, le résultat est nul | Fenêtre publiée, propriété absente du monde |
| `0.0`, `measured = false` | **Non mesuré** — repli neutre, aucune valeur calculable | Donnée absente du snapshot |
| `null` | **Pas d'observation à ce tick** | Tick non couvert |

Le drapeau `measured` est calculé par le mécanisme `REQUIRES` du moteur
(`_common.py:69-105`). Une fenêtre d'événements **vide** est une observation
(« zéro observé ») ; une fenêtre **absente** est « non mesuré »
(`_common.py:108-118`).

### 2.3 Moyenne géométrique : pas de compensation totale

Le noyau du DVI est une **moyenne géométrique pondérée** (§6), contrairement
à la somme arithmétique d'`EmergenceScore`. Conséquence mathématique :

> **Une composante mesurée à 0.0 annule le noyau**, quelles que soient les
> autres composantes.

C'est voulu : un monde qui ne renouvelle aucune structure ou qui n'a aucune
activité n'est pas « partiellement viable » — il manque une propriété
nécessaire. La lecture publie toujours le **profil complet** des composantes
(§9) : le score seul ne suffit jamais.

### 2.4 Couverture du noyau (κ)

Plusieurs composantes peuvent être **non mesurées** à un tick donné (absence
de crise pour la résilience, fenêtre d'événements absente, etc.). Le noyau
n'est alors calculé que sur les composantes mesurées, avec **renormalisation
des poids** ; la fraction de poids effectivement mesurée est publiée comme
`ViabilityCoverage` (κ) :

- `κ = 1.0` : toutes les composantes sont mesurées — score complet ;
- `0 < κ < 1` : score **partiel** — l'interface doit afficher κ à côté du score ;
- `κ = 0` : aucune composante mesurée — `ViabilityScore = null`
  (non mesuré), jamais `0.0`.

Un score partiel ne doit pas être comparé à un score complet sans mentionner κ.

### 2.5 Activité optimale, pas activité maximale

Le DVI ne récompense pas le volume d'activité : il récompense la **proximité
d'une zone optimale** (§5.1). Un monde hyperactif sort de la zone et pénalise
l'indice autant qu'un monde figé. Les bornes de la zone sont `[HÉRITÉ]` et
publiées.

### 2.6 Règle d'or

> **ECHOS ne doit jamais transformer une métrique en vérité scientifique.**
> Un indice de viabilité reste une mesure particulière d'un régime
> observé, jamais une preuve d'existence d'une société, d'une vie ou d'un
> phénomène émergent. (`VISION.md`, `LIMITATIONS.md`, ECHOS-032.)

Le champ `ViabilityDisclaimer` est invariant et doit être affiché tel quel
par toute interface consommant le DVI.

---

## 3. Notation et conventions

### 3.1 Symboles

- `t` : tick courant d'analyse.
- `T` : durée **réellement observée** de la fenêtre (`eventWindow.ticks`,
  minimum 1 — `pipeline.py:255-275`).
- `N` : population vivante (`aliveCount`).
- `M` : ensemble des composantes **mesurées** à ce tick.
- `w_i` : poids de la composante `i`, `∑ᵢ wᵢ = 1` (§12.1).
- `X_i` : valeur de la composante `i`, `X_i ∈ [0, 1]`.
- `κ` : couverture du noyau (§2.4, §6.2).
- `log` : logarithme naturel dans les écritures de mise en œuvre ; les
  entropies partagées restent en `log₂` (bits) comme dans
  `ANALYSIS_FOUNDATIONS.md` §3.1.
- Les valeurs `0.0` publiées distinguent **zéro observé** de **non mesuré**
  via `measured` (§2.2).

### 3.2 Trame d'une section d'équation

Chaque composante suit la trame fixe d'`ANALYSIS_FOUNDATIONS.md` §1.3 :
**Nom · Explication globale · Expression mathématique · Pourquoi cette
équation · Usage dans le projet · Interprétation**.

### 3.3 Sources de données par couche

| Donnée | Moteur (snapshot) | Post-run (store) |
| :-- | :-- | :-- |
| `agents` (croyances, objectifs, actions, confiance) | oui (échantillonné `context_every`) | contexte `agents` (1/20 ticks) |
| `aliveCount` | oui | `tick_summaries.alive_count` |
| `events` + `eventWindow` | oui (≤ 100 ticks, ≤ 2000 événements) | `events_log` (run complet) |
| `history` (actions, ressources, tailles de communautés) | oui (≤ 100 entrées) | reconstruit / Parquet optionnel |
| `communityHistory` | oui (dérivée de `history`) | contexte `groups` |
| `resources` (stocks du tick) | oui | `tick_summaries.mean_food/mean_water` |
| Résultats des moteurs entrants | via registre (`emergence.py:230-241`) | `tick_metrics` |
| Besoins par décision | non | `decision_traces.needs` |
| Issue du run (`extinct`/`surviving`) | non | `population_outcome()` (`sqlite.py:386`) |

---

## 4. Socle de calcul réutilisé

Le DVI **ne redéfinit pas** les outils partagés. Il consomme :

| Outil | Origine | Usage DVI |
| :-- | :-- | :-- |
| `shannon`, `shannon_normalized` | `_common.py:121-150` | via `BeliefDiversityNorm`, `GoalDiversityNorm`, `ActionDiversity` |
| `mean`, `variance` (ddof=0) | `_common.py:153-163` | variance de l'activité sur la fenêtre (§5.7) |
| `clamp`, `safe_ratio` | `_common.py:166-179` | normalisations, aucune division par zéro |
| `event_window_published` | `_common.py:108-118` | `REQUIRES` des composantes fenêtrées |
| `agents_of`, `alive_count`, `activity_of` | `_common.py:187-215` | lecture snapshot |
| `communities`, `community_sizes` | `_common.py:325-342` | via moteurs sociaux/groupe |
| `_least_squares_slope`, `_stats` | `calibration.py:22-49` | pentes post-run (§8.1) |
| `_CRITICAL_RATIO = 0.2` | `resource_sustainability.py:62` | marges aux seuils de crise (§8.2) |
| `population_outcome` | `sqlite.py:386` | issue observée (calibration, §12.2) |
| `belief_distribution`, `l2_normalized` | `reproducibility.py:36-102` | dérive entre contextes (phase 3) |

Les métriques **primitives** consommées par les composantes viennent des
moteurs déjà implémentés (catalogue 2.0.0) — voir la table de correspondance
§10. Le moteur DVI les reçoit comme `EmergenceIndicators` reçoit celles des
moteurs entrants (`emergence.py:287-330`).

---

## 5. Composantes

Sept composantes entrent dans le noyau géométrique (§6). Chacune est bornée
`[0, 1]` et peut être **non mesurée** (`REQUIRES`).

### 5.1 𝓐 — Activité optimale

**Nom** : Activité optimale — `ViabilityActivity`

**Explication globale** : Mesure si le niveau d'activité du monde est proche
d'une **zone optimale** — ni figé, ni chaotique. Ce n'est pas un volume brut :
c'est une **distance à un régime cible**. C'est la composante qui pénalise
explicitement les mondes immobiles et les mondes hyperactifs.

**Expression mathématique** :

```math
a_{\mathrm{act}} = \frac{1}{T} \sum_{t=1}^{T} \frac{\bigl|\{a : \mathrm{act}_t(a) \neq \mathrm{Idle}\}\bigr|}{\max(|\mathcal{A}_t|, 1)}

a_{\mathrm{dec}} = \frac{D}{N \cdot T}

a_{\mathrm{evt}} = \frac{E}{N \cdot T}

\hat{A} = \frac{w_a \cdot \min\!\bigl(1,\, a_{\mathrm{act}} / r^*_{\mathrm{act}}\bigr) \;+\; w_d \cdot \min\!\bigl(1,\, a_{\mathrm{dec}} / r^*_{\mathrm{dec}}\bigr) \;+\; w_e \cdot \min\!\bigl(1,\, a_{\mathrm{evt}} / r^*_{\mathrm{evt}}\bigr)}{w_a + w_d + w_e}

\mathcal{A} = \exp\!\left(-\frac{(\hat{A} - A^*)^2}{2\,\sigma_A^2}\right)
```

avec :

- `a_act` : part d'entités actives (action ≠ Idle), moyenne sur la fenêtre ;
  `act_t(a)` : action de l'entité `a` au tick `t` lue dans `history` (objectif
  courant en repli, `_common.py:198-210`) ; `|·|` : cardinal ;
- `T` : durée observée de la fenêtre (`eventWindow.ticks`, min 1) ;
- `N` : `aliveCount` (repli : longueur de `agents`) ;
- `D` : `DecisionCount`, décisions `decision_made` dans la fenêtre
  (`cognitive_diversity.py`, métrique du catalogue) ;
- `E` : nombre d'événements de la fenêtre (cap 2000, `pipeline.py:70`) ;
- `r*_act`, `r*_dec`, `r*_evt` : repères de normalisation `[HÉRITÉ]` (§12.1) ;
- `w_a, w_d, w_e` : poids des sous-mesures (défaut `1/3` chacun, publiés) ;
- `A*` : activité normalisée optimale `[HÉRITÉ]` ;
- `σ_A` : demi-largeur de la zone optimale `[HÉRITÉ]` ;
- cas limites :
  - `history` absente → `a_act` non mesurée (les sous-mesures restantes sont
    renormalisées par `w_d + w_e`) ;
  - fenêtre d'événements absente → `a_dec` et `a_evt` non mesurés ;
  - si **aucune** sous-mesure n'est mesurée → `𝓐` non mesuré
    (`measured = false`, valeur de repli `0.0` jamais interprétée).

**Pourquoi cette équation** :

- La cloche exponentielle traduit directement la contrainte centrale du DVI :
  le régime viable est une **zone**, pas un maximum. `Â → 0` (figé) et
  `Â` très supérieur à `A*` (débordement) produisent tous deux `𝓐 → 0`.
- La normalisation par repères `[HÉRITÉ]` (et non par le max de la fenêtre)
  évite de masquer les dérives lentes : un monde qui perd progressivement de
  son activité voit `Â` décroître vers 0, pas se re-normaliser à 1.
- Une somme arithmétique d'activité (comme `DecisionCount` seule) ne distingue
  pas « peu d'entités très actives » de « toutes les entités modérément
  actives » : la part d'entités actives (`a_act`) et le taux par entité
  (`a_dec`) couvrent les deux lectures.
- Alternative écartée : `max` de l'activité (récompenserait le chaos) ;
  moyenne mobile brute sans cloche (ne pénaliserait ni le figé ni l'excès).

**Usage dans le projet** :

- **Phase 1 (moteur)** : `history` + `eventWindow` + `aliveCount` du snapshot ;
  `DecisionCount` via le registre (`cognitive_diversity.py`).
- Constantes `A*`, `σ_A`, `r_*`, `w_*` publiées dans le moteur (dictionnaire
  de constantes, modèle `emergence.WEIGHTS`, `emergence.py:53-66`).
- `REQUIRES` : callable — au moins une sous-mesure mesurable.

**Interprétation** :

- `𝓐 → 0` : activité très éloignée de la zone optimale (monde figé **ou**
  monde hors repère). La cause se lit sur `Â` et les sous-mesures.
- `𝓐 → 1` : `Â ≈ A*` — activité dans la zone cible.
- `𝓐 ∈ (0,1)` : activité partielle — comparer à `A*` et aux repères.
- **Unité : fraction [0,1].** Comparabilité entre runs sous réserve que les
  repères `r_*` soient les mêmes (constantes versionnées).
- La pénalisation des mondes statiques vient d'ici (§7.2), **pas** d'un
  multiplicateur externe (pas de double comptage).

### 5.2 𝓓 — Diversité

**Nom** : Diversité — `ViabilityDiversity`

**Explication globale** : La diversité du monde à trois niveaux : croyances
épistémiques, objectifs, actions observées. Un monde qui ne produit qu'une
variété est fragile ; un monde qui en produit sans structure est chaotique.
La diversité est donc **condition nécessaire**, pas condition suffisante —
c'est pourquoi elle entre dans un noyau géométrique et non comme score final.

**Expression mathématique** :

```math
\mathcal{D} = \frac{w_b \cdot \mathcal{D}_{\mathrm{croyances}} \;+\; w_g \cdot \mathcal{D}_{\mathrm{objectifs}} \;+\; w_a \cdot \mathcal{D}_{\mathrm{actions}}}{w_b + w_g + w_a}
```

avec :

- `𝓓_croyances` : `BeliefDiversityNorm` — entropie de Shannon des profils de
  croyances, normalisée par `log₂(|agents|)` ;
  `shannon_normalized` sur les vecteurs binaires de croyances, agent-échantillonné
  (`cognitive_diversity.py`, métrique catalogue) ;
- `𝓓_objectifs` : `GoalDiversityNorm` — entropie normalisée de la distribution
  des objectifs courants ;
- `𝓓_actions` : `ActionDiversity` — entropie normalisée des actions prises
  (fenêtre observée, `history` ou `events` selon disponibilité) ;
- `w_b, w_g, w_a` : poids des sous-mesures `[HÉRITÉ]` (défaut `1/3` chacun,
  publiés) ;
- cas limites :
  - `N = 1` : entropie définie sur un seul agent → `𝓓_croyances = 0`
    (zéro observé) ; une seule croyance n'est pas un manque de mesure ;
  - sous-mesure absente du contexte → non mesurée, renormalisation des poids ;
  - toutes absentes → `𝓓` non mesuré.

**Pourquoi cette équation** :

- Les trois niveaux sont orthogonaux : un monde peut avoir des croyances
  identiques mais des objectifs divers, ou l'inverse. Une moyenne sur les
  trois lit mieux la « diversité d'ensemble » qu'une seule composante.
- La normalisation par `log₂(n)` (et non par le max observé) conserve le lien
  avec l'entropie absolue : `𝓓 = 0` signifie homogénéité réelle, pas
  « plus homogène que ce tick ».
- Alternative écartée : `ShannonEntropy` seule en bits (non comparable entre
  tailles de population différentes) ; `1 − similitude moyenne` (redondant
  avec les entropies déjà publiées).

**Usage dans le projet** :

- **Moteur** : lit `BeliefDiversityNorm` et `GoalDiversityNorm` via le registre
  (`cognitive_diversity.py`), `ActionDiversity` via `history`/`events` du
  snapshot (`reproducibility.py` : `belief_distribution` peut servir de
  repli déterministe si les moteurs n'ont pas tourné).
- **Post-run** : séries des trois sous-mesures depuis `tick_metrics` /
  contexte `agents`.

**Interprétation** :

- `𝓓 → 0` : monde cognitivement/socialement homogène (croyances, objectifs
  ou actions identiques pour tous).
- `𝓓 → 1` : diversité maximale par rapport à la taille de population.
- **Unité : fraction [0,1] normalisée** (0 = homogène, 1 = entropie maximale
  pour `N` agents). Comparaison inter-runs valable si `N` reste comparable ;
  sinon commenter avec la taille de population.

### 5.3 𝓒 — Structure

**Nom** : Structure — `ViabilityStructure`

**Explication globale** : Organisation du réseau d'interactions : densité,
clustering, couverture des communautés. Une structure organisée permet de
diffuser l'information et de stabiliser les interactions ; l'absence de
structure est aussi problématique que la rigidité totale.

**Expression mathématique** :

```math
\mathcal{C} = \frac{w_c \cdot \mathcal{C}_{\mathrm{clustering}} \;+\; w_d \cdot \mathcal{C}_{\mathrm{densit\acute{e}}} \;+\; w_k \cdot \mathcal{C}_{\mathrm{communaut\acute{e}s}}}{w_c + w_d + w_k}
```

avec :

- `𝓒_clustering` : `ClusteringCoefficient` — coefficient de clustering moyen du
  réseau d'interactions observées (`social_complexity.py`, catalogue) ;
- `𝓒_densité` : `NetworkDensity` — densité arête/sommets du même réseau ;
- `𝓒_communautes` : `CommunityCoverage` — fraction de la population couverte
  par une communauté détectée (moteur `group_dynamics.py`) ; défaut de repli
  si `groups[]` est présent mais non traité : couverture calculée à partir des
  groupes du snapshot ;
- `w_c, w_d, w_k` : poids `[HÉRITÉ]` (défaut `1/3` chacun, publiés) ;
- cas limites :
  - `N ≤ 1` ou aucun événement d'interaction : densité et clustering définis
    sur un graphe trivial → `0.0` (zéro observé) ;
  - moteurs sociaux non exécutés : sous-mesures non mesurées, renormalisation.

**Pourquoi cette équation** :

- Le clustering mesure la **localité** des interactions, la densité leur
  **volume relatif**, la couverture communautaire leur **organisation
  mésoscopique**. Les trois se lisent ensemble : un réseau dense mais sans
  clustering est chaotique ; un clustering élevé sur un sous-ensemble minime
  est une structure partielle.
- Alternative écartée : `ClusteringCoefficient` seule (ignore la population
  non connectée) ; distance moyenne des plus courts chemins (instable sur
  graphes fragmentés, coûteux à calculer par tick).

**Usage dans le projet** :

- **Moteur** : registre — résultats de `SocialComplexityIndicators` et
  `GroupDynamicsIndicators` ; repli snapshot `groups[]` si nécessaire.
- **Post-run** : séries `tick_metrics`.

**Interprétation** :

- `𝓒 → 0` : réseau non structuré (aucune arête, aucun groupe).
- `𝓒 → 1` : réseau fortement structuré (clustering, densité et couverture
  proches de leurs repères `[HÉRITÉ]` de saturation).
- **Unité : fraction [0,1].** La densité brute n'est pas un « score » : elle
  est bornée à 1 par définition du graphe et renormalisée vers la zone utile
  par le repère de saturation publié (§12.1).

### 5.4 𝓖 — Renouvellement

**Nom** : Renouvellement — `ViabilityRenewal`

**Explication globale** : Capacité du monde à **produire de la nouveauté** :
créations d'artefacts, inventions, événements de renouvellement. Un monde
immobile n'a pas de renouvellement ; un monde qui crée sans jamais stabiliser
(voir la **balance** diagnostique séparée) reste « vivant » au sens étroit
mais pas viable au sens large — c'est pour cela que la balance n'entre pas
dans le noyau.

**Expression mathématique** :

```math
r_G = \frac{E^*}{N \cdot T}

\mathcal{G} = \min\!\bigl(1,\; r_G / r^*_G\bigr)

\mathrm{balance} = \frac{\nu + 1}{\delta + 1}
```

avec :

- `E*` : nombre d'événements de la fenêtre dont le type ∈ {invention,
  discovery, artifact, event_created} — `fenêtre` = `eventWindow` du moteur,
  ou `events_log` filtré en post-run ;
- `N`, `T` : comme §5.1 ;
- `r*_G` : repère de renouvellement `[HÉRITÉ]` (nombre de nouveautés par
  entité par tick auquel `𝓖 = 1`) ;
- `ν` : nombre d'événements de créations dans la fenêtre ;
- `δ` : nombre d'événements de destructions/dissolutions ;
- le `+1` de la balance est un lissage déterministe (évite l'indétermination
  `0/0`) ;
- cas limites :
  - `history`/`events` absents → `𝓖` non mesuré ;
  - `T = 0` : impossible (`T ≥ 1` garanti par `event_window_published`) ;
  - `r_G = 0` : zéro observé → `𝓖 = 0.0`.

**Pourquoi cette équation** :

- Le ratio par `N · T` rend le taux **comparable entre tailles de population
  et durées de fenêtre** — sans cela, un monde à 10 000 agents produirait
  mécaniquement plus d'événements.
- `min(1, ·)` borne sans pénaliser l'excès : un renouvellement au-delà du
  repère n'est pas un défaut (contrairement à l'activité §5.1, où l'excès
  est une sortie de zone). L'excès de destruction est capté par la balance.
- La balance est séparée du noyau (décision explicite) : elle informe
  l'interprétation (« crée-t-on autant qu'on détruit ? ») sans contaminer le
  noyau géométrique par une grandeur redondante avec 𝓡 et 𝓢.

**Usage dans le projet** :

- **Moteur** : `eventWindow` du snapshot, types d'événements du catalogue
  (`event_types.py` / catalogue 2.0.0).
- **Post-run** : `events_log` complet, séries par fenêtre glissante.
- `ViabilityTurnoverBalance` : sortie de diagnostic séparée (§9).

**Interprétation** :

- `𝓖 → 0` : aucune nouveauté observée dans la fenêtre (monde figé).
- `𝓖 → 1` : taux de renouvellement au moins égal au repère `r_G*`.
- `balance > 1` : plus de créations que de destructions ; `< 1` : l'inverse ;
  `≈ 1` : renouvellement stable.
- **Unité : fraction [0,1]** pour `𝓖` ; `balance` sans unité (ratio de
  comptages), à afficher tel quel.

### 5.5 𝓡 — Résilience

**Nom** : Résilience — `ViabilityResilience`

**Explication globale** : Part des crises détectées qui ont été **résorbées**
par le système — c'est-à-dire retournées à un état viable. Mesurée sur les
crises **spontanées** uniquement : ECHOS n'injecte aucune perturbation
contrôlée (§1.3). Décision 4A : pas de modèle probabiliste 𝓕 ; remplacement
par un diagnostic de trajectoire (§8) en plus de ce taux.

**Expression mathématique** :

```math
\mathcal{R} =
\begin{cases}
\dfrac{R_{\mathrm{ep}}}{R_{\mathrm{ep}} + R_{\mathrm{un}}} & \text{si } R_{\mathrm{ep}} + R_{\mathrm{un}} > 0 \\[8pt]
0.0 & \text{sinon (non mesuré — jamais interprété comme « 0 »)}
\end{cases}
```

avec :

- `R_ep` : nombre de crises de la fenêtre dont `RecoveryEpisodes ≥ 1` ;
- `R_un` : nombre de crises de la fenêtre dont `RecoveryEpisodes = 0`
  (`UnresolvedCrisisCount`) ;
- **Détection d'une crise** (déterministe, alignée `reproducibility.py` et
  `resource_sustainability.py`) : une crise est un événement ou un tick où
  au moins un seuil critique est franchi —
  - `food` ou `water` sous `_CRITICAL_RATIO = 0.2` de son état de référence
    (`resource_sustainability.py:62`) ;
  - ou événement de type crise/mortalité massif du catalogue ;
  - ou sortie de la population sous seuil de viabilité démographique du
    moteur de ressources ;
- `RecoveryEpisodes` : nombre de retours à un état non critique observés
  **après** la crise, dans la fenêtre (métrique du moteur
  `ResourceSustainabilityIndicators`, catalogue 2.0.0) ;
- `R_ep + R_un = 0` : aucune crise détectée dans la fenêtre → **non mesuré**
  (`measured = false`, valeur de repli `0.0`) — jamais interprété comme
  « résilience nulle » ni comme « résilience parfaite » ;
- cas limites :
  - crise détectée sans episode de récupération observable dans la fenêtre
    (fenêtre trop courte) : comptée comme `R_un` — le diagnostic §8 corrige
    l'interprétation en post-run ;
  - moteur de ressources absent : `REQUIRES` non satisfait → non mesuré.

**Pourquoi cette équation** :

- Le taux `R_ep / (R_ep + R_un)` est le **diagnostic de sortie de crise le
  plus direct** disponible sans modèle : sur les crises observées, combien se
  sont soldées par un retour à un état viable ?
- La règle « aucune crise → non mesuré » est mathématiquement honnête : le
  ratio n'est pas définie, et lui attribuer 0.0 ou 1.0 induirait en erreur
  (un monde sans crise observée peut être stable, ou simplement jamais
  éprouvé).
- Alternative écartée : `P(recouvrement)` issue d'un modèle (interdit —
  pas d'apprentissage, §1.3) ; temps moyen de récupération seul (non défini
  si aucune crise, biaisé vers les crises courtes).

**Usage dans le projet** :

- **Moteur** : `eventWindow` + résultats de `ResourceSustainabilityIndicators`
  (`recovery_episodes`, `unresolved_crisis_count`) via registre.
- **Post-run** : crises extraites de `events_log` + `tick_summaries`
  (ressources), aligné sur la détection ci-dessus.
- Le moteur ne modifie pas le monde : comptage uniquement.

**Interprétation** :

- `measured = false` : **aucune crise dans la fenêtre** — le score n'existe
  pas. Afficher « non mesuré — aucune crise observée », pas `0.0`.
- `𝓡 → 0` : crises observées, aucune résorbée (système en dégradation).
- `𝓡 → 1` : toutes les crises observées ont été résorbées.
- `𝓡 ∈ (0,1)` : résilience partielle — combiner avec §8 (pente de la
  population, marge aux seuils) pour juger si la résorption est durable.
- **Unité : fraction [0,1] des crises résorbées.** Non comparable à 1.000
  sans mentionner le nombre de crises observées.

### 5.6 𝓟 — Persistance

**Nom** : Persistance — `ViabilityPersistence`

**Explication globale** : Stabilité des structures au fil du temps —
tailles de communautés alignées, âge moyen des objectifs. Un monde qui
recrée tout en permanence est actif (𝓐) mais pas persistant : rien n'y dure.

**Expression mathématique** :

```math
\mathrm{alignement}_{\mathrm{tailles}} = \exp\!\left(-\frac{\mathrm{Var}(\{s_j\})}{\bar{s}^{\,2}}\right)

\mathrm{persistance}_{\mathrm{obj}} = \min\!\bigl(1,\; G_{\mathrm{\hat{a}ge}} / T\bigr)

\mathcal{P} = \frac{w_m \cdot \mathrm{alignement}_{\mathrm{tailles}} \;+\; w_g \cdot \mathrm{persistance}_{\mathrm{obj}}}{w_m + w_g}
```

avec :

- `s_j` : taille de la communauté *j* à ce tick (`communities`,
  `_common.py:325-342` ; snapshot `groups[]` en repli) ;
- `s̄` : taille moyenne des communautés (`mean({s_j})`) ;
- `Var({s_j})` : variance de population (§3.4 d'`ANALYSIS_FOUNDATIONS.md`)
  des tailles ;
- `G_âge` : `AverageGoalAge` — âge moyen des objectifs courants des agents
  vivants (métrique du moteur `CognitiveDiversityIndicators`, catalogue 2.0.0) ;
- `T` : durée observée (fenêtre) ;
- `w_m, w_g` : poids `[HÉRITÉ]` (défaut `1/2` chacun, publié) ;
- cas limites :
  - aucune communauté → `alignement_tailles = 1.0` (zéro observé : pas de
    dispersion mesurable, pas de dispersion constatée) — sous-mesure
    renormalisée si l'autre est absente ;
  - `G_âge` absente → renormalisation.

**Pourquoi cette équation** :

- La **dispersion relative** des tailles (et non l'écart-type brut) mesure
  l'homogénéité des communautés indépendamment de l'échelle : des groupes de
  5, 5, 5 et 5 persistent aussi bien que des groupes de 500, 500, 500, 500.
- L'exponentielle `exp(−CV²)` transforme le coefficient de variation en score
  borné `[0,1]` : CV=0 (tailles identiques) → 1 ; CV croissant → 0.
- `AverageGoalAge / T` borne l'âge des objectifs à la fenêtre : un objectif
  plus vieux que la fenêtre observée plafonne à 1.0 — on ne peut pas savoir
  plus loin sans données.
- Alternative écartée : entropie des tailles (sensible aux petits groupes) ;
  âge des objectifs seul (ignore la structure sociale).

**Usage dans le projet** :

- **Moteur** : `communities()` sur le snapshot ; `AverageGoalAge` via registre
  (`cognitive_diversity.py`).
- **Post-run** : `communityHistory` reconstruite / `contexte groups`.

**Interprétation** :

- `𝓟 → 0` : structures éphémères ou fortement déséquilibrées, objectifs très
  courts.
- `𝓟 → 1` : communautés de tailles homogènes, objectifs persistants.
- **Unité : fraction [0,1].** L'alignement des tailles est une propriété de
  **forme**, pas de taille absolue : comparer à une population plus nombreuse
  est valide.

### 5.7 𝓢 — Stabilité

**Nom** : Stabilité — `ViabilityStability`

**Explication globale** : Régularité de l'activité au fil de la fenêtre —
variance faible du nombre d'entités actives tick par tick. Un monde
irrégulier (pics et creux) n'est pas dans un régime dynamique viable, même
si son activité moyenne est correcte. Attention : la **stabilité statique**
(monde figé) n'est **pas** récompensée ici — le monde figé est pénalisé par
𝓐 (§5.1), et 𝓢 mesure la **régularité d'une activité qui existe**.

**Expression mathématique** :

```math
a_t = \frac{\bigl|\{a : \mathrm{act}_t(a) \neq \mathrm{Idle}\}\bigr|}{\max(|\mathcal{A}_t|, 1)} \quad \text{pour } t \in \{1, \dots, T\}

\mu_a = \frac{1}{T} \sum_{t=1}^{T} a_t

\sigma_a^2 = \frac{1}{T} \sum_{t=1}^{T} (a_t - \mu_a)^2

\mathcal{S} = \exp\!\left(-\frac{\sigma_a^2}{\sigma_V^2}\right)
```

avec :

- `a_t` : part d'entités actives au tick `t` (mêmes définitions que §5.1) ;
- `μ_a` : moyenne de la série d'activité (§3.3 d'`ANALYSIS_FOUNDATIONS.md`) ;
- `σ²_a` : variance de population (§3.4, `ddof=0`) de la série d'activité ;
- `σ_V` : variance de référence `[HÉRITÉ]` (seuil au-delà duquel la
  variabilité est pénalisée) ;
- cas limites :
  - `history` absente ou fenêtre d'un seul tick → `σ²_a = 0.0` → `𝓢 = 1.0`
    (zéro observé : pas de variabilité mesurable sur une seule observation) ;
  - `history` présente avec `T ≥ 2` mais aucune variation → `σ²_a = 0.0` →
    `𝓢 = 1.0` (activité parfaitement régulière — le figé est pénalisé par 𝓐,
    pas ici).

**Pourquoi cette équation** :

- La variance de l'activité est le signal le plus direct de **régularité
  dynamique** : un régime viable oscille autour d'un niveau sans s'emballer.
- `exp(−V_a/σ_V²)` borne la pénalité et la rend lisible : V_a=0 → 1 ;
  V_a=σ_V² → exp(−1)≈0.37 ; V_a croissante → 0.
- Alternative écartée : coefficient de variation (instable si μ_a→0) ;
  entropie de la distribution des niveaux d'activité (moins interprétable).

**Usage dans le projet** :

- **Moteur** : `history` du snapshot (≤ 100 entrées, bornes déterministes).
- **Post-run** : séries d'activité reconstruites tick par tick (parfait pour
  détecter les pics chaotiques à long terme).

**Interprétation** :

- `𝓢 → 0` : activité très irrégulière (pics/creux amples) — candidat au
  régime CHAOTIC (§7).
- `𝓢 → 1` : activité régulière (y compris activité nulle constante — lire
  avec 𝓐 pour distinguer figé de stable-viable).
- **Unité : fraction [0,1].** σ_V versionné et publié ; ne pas comparer entre
  versions sans recalcul.

---

## 6. Noyau géométrique et couverture

### 6.1 Moyenne géométrique pondérée

**Nom** : Score de viabilité du noyau — `ViabilityScore`

**Explication globale** : Agrège les sept composantes (§5) en un seul indice
par le noyau géométrique pondéré. Contrairement à la somme arithmétique
d'`EmergenceScore`, **aucune compensation entre composantes n'est possible** :
une composante mesurée à 0.0 annule le noyau.

**Expression mathématique** :

```math
M = \{i : \text{composante } i \text{ mesurée à ce tick}\}

W_M = \sum_{i \in M} w_i

\mathrm{ViabilityScore} =
\begin{cases}
\exp\!\left(\dfrac{1}{W_M} \sum_{i \in M} w_i \cdot \ln(X_i)\right) & \text{si } |M| > 0 \text{ et } \forall\, i \in M : X_i > 0 \\[8pt]
0.0 & \text{si } |M| > 0 \text{ et } \exists\, i \in M : X_i = 0 \\[4pt]
\mathrm{null} & \text{si } |M| = 0 \quad \text{(non mesuré)}
\end{cases}
```

avec :

- `w_i` : poids de chaque composante (§12.1), `∑ᵢ wᵢ = 1` sur les **sept**
  composantes ;
- `W_M` : somme des poids des composantes **mesurées** (renormalisation de la
  couverture partielle, §6.2) ;
- `X_i` : valeur `𝓐, 𝓓, 𝓒, 𝓖, 𝓡, 𝓟, 𝓢` ;
- cas limites :
  - `M = ∅` : score non mesuré (`null` + `measured = false`) ;
  - `X_i = 0.0` mesurée : score `0.0` (zéro observé, `measured = true`) —
    conséquence directe de `exp(−∞) = 0` pour la moyenne géométrique ;
  - `X_i` non mesurée : exclue de `M`, poids redistribués via `W_M`.

**Pourquoi cette équation** :

- La moyenne géométrique est **associative, homogène de degré 1 et
  strictement concave** sur l'orthant positif : elle ne peut pas compenser
  une composante nulle par les autres, ce qui traduit fidèlement la sémantique
  « toutes les propriétés sont nécessaires ».
- La forme exponentielle évite les `ln(0)` : un seul `X_i = 0` rend le score
  nul sans indéterminé.
- `W_M` (et non `∑ᵢ wᵢ = 1`) garantit que les composantes **mesurées** se
  partagent la totalité du poids : un score partiel reste sur `[0,1]` et
  lisible, sous réserve de toujours afficher κ (§6.2).
- Alternative écartée : somme arithmétique (compensation totale — cf. tableau
  §1.2) ; moyenne harmonique (trop sensible aux valeurs basses) ; minimum
  dur (sature à la plus mauvaise composante, ignore le profil).

**Usage dans le projet** :

- **Moteur** : calcul direct du moteur `ViabilityIndicators`, publiant
  `ViabilityScore`, `ViabilityCoverage`, `ViabilityRegime` (§7).
- **Post-run** : série `ViabilityScore` par tick dans `tick_metrics` /
  reconstruite ; agrégats de trajectoire en §8.
- Aucune modification de l'endpoint `/api/runs/{id}/viability` (§1.3).

**Interprétation** :

- `0.0` **mesuré** : au moins une composante nécessaire est absente du monde
  (figé, non renouvelant, non diversifié, etc.) — lire le profil (§9).
- `null` : aucune composante mesurée à ce tick — ne jamais afficher `0.0`.
- `0 < ViabilityScore < 1` : viabilité partielle — κ indique la confiance
  dans la lecture.
- **Unité : fraction [0,1].** Comparaison entre runs valide si les constantes
  (§12.1) et κ sont comparables.

### 6.2 Couverture κ

**Nom** : Couverture du noyau — `ViabilityCoverage`

**Explication globale** : Fraction du poids total des composantes qui est
effectivement **mesurée** à ce tick. Indique si le score est complet ou
partiel — condition de lecture obligatoire du score (§2.4).

**Expression mathématique** :

```math
\kappa = \frac{W_M}{\sum_{i=1}^{7} w_i}
```

avec :

- `∑_{i=1}^{7} w_i = 1` sur les sept composantes (§12.1) ;
- `W_M` : somme des poids mesurés (§6.1) ;
- bornes : `κ ∈ [0, 1]`.

**Pourquoi cette équation** :

- κ est la **quantification exacte de la règle de couverture** : elle rend
  explicite la fraction de l'indice qui repose sur des observations réelles.
- Alternative écartée : simple comptage `|M| / 7` (ignore l'inégalité des
  poids — une composante lourde non mesurée compte pour une légère).

**Usage dans le projet** :

- **Moteur** : publié à côté de `ViabilityScore` dans chaque tick d'analyse.
- Interfaces : κ **toujours affiché** avec le score (règle §2.4).

**Interprétation** :

- `κ = 1` : score complet — lecture directe du profil.
- `0 < κ < 1` : score partiel — mentionner κ ; ne pas comparer à un score
  complet sans le dire.
- `κ = 0` : aucun score (`ViabilityScore = null`).

---

## 7. Régimes dynamiques

### 7.1 Régime — `ViabilityRegime`

**Explication globale** : Catégorie discrète du régime observé, dérivée du
score **et** de la stabilité de l'activité. Le régime condense la lecture
pour l'interface sans la remplacer : le profil complet (§9) reste la source.

**Expression mathématique** :

```math
V = \mathrm{ViabilityScore}

\mathrm{ViabilityRegime} =
\begin{cases}
\text{UNKNOWN} & \text{si } V = \mathrm{null} \\[3pt]
\text{CRITICAL} & \text{si } V < s_1 \\[3pt]
\text{WEAK} & \text{si } s_1 \le V < s_2 \\[3pt]
\text{MODERATE} & \text{si } s_2 \le V < s_3 \\[3pt]
\text{STRONG} & \text{si } s_3 \le V < s_4 \\[3pt]
\text{OPTIMAL} & \text{si } V \ge s_4 \\[3pt]
\text{STATIC} & \text{si } \mathcal{A} \text{ mesuré et } \mathcal{A} < a_{\mathrm{static}} \\[3pt]
\text{CHAOTIC} & \text{si } \mathcal{S} \text{ mesuré et } \mathcal{S} < s_{\mathrm{chaotic}}
\end{cases}
```

avec :

- `V` : `ViabilityScore` (§6.1) ;
- `s₁..s₄` : seuils de graduation du régime `[HÉRITÉ]` (§12.1) ;
- `a_static` : seuil d'activité sous lequel le monde est déclaré figé
  `[HÉRITÉ]` ;
- `s_chaotic` : seuil de stabilité sous lequel le monde est déclaré chaotique
  `[HÉRITÉ]` ;
- `UNKNOWN` : score non mesuré (κ = 0) — jamais assimilé à CRITICAL ;
- priorité CHAOTIC > STATIC : un monde irrégulier est plus urgent qu'un monde
  figé ; un monde figé ne peut pas être STRONG car 𝓐 est alors bas.

**Pourquoi cette équation** :

- Les seuils de score donnent la **graduation continue** ; les drapeaux
  STATIC/CHAOTIC donnent les **diagnostics de régime** que le score seul
  ne nomme pas explicitement.
- La priorité CHAOTIC > STATIC reflète l'urgence : le chaos désorganise ;
  le figé est stable mais mort-vivant.
- Alternative écartée : régimes dérivés uniquement de 𝓐 et 𝓢 (ignorerait
  le profil complet) ; régimes par k-means (interdit — apprentissage).

**Usage dans le projet** :

- **Moteur** : publié dans chaque tick d'analyse ; seuils versionnés et
  publiés avec le registre du moteur.

**Interprétation** :

| Régime | Lecture |
| :-- | :-- |
| `UNKNOWN` | κ = 0 — aucune composante mesurée ; ne pas juger le monde. |
| `CRITICAL` | V < s1 — régime non viable au sens strict ; inspecter le profil (§9). |
| `WEAK` | V ∈ [s1, s2) — viability fragile ; une ou plusieurs propriétés manquantes. |
| `MODERATE` | V ∈ [s2, s3) — régime viable partiel ; les propriétés sont présentes mais incomplètes. |
| `STRONG` | V ∈ [s3, s4) — régime dynamique viable ; profil large. |
| `OPTIMAL` | V ≥ s4 — profil complet et activité dans la zone optimale. |
| `STATIC` | Activité sous `a_static` — monde figé (pénalisé par 𝓐, §5.1). |
| `CHAOTIC` | Stabilité sous `s_chaotic` — activité irrégulière ; inspecter les pentes (§8). |

### 7.2 Pénalisation des mondes statiques et chaotiques — règles de non-double-comptage

- La pénalisation du **monde figé** vient **uniquement** de 𝓐 (cloche
  centrée sur `A*`, §5.1). 𝓢 ne pénalise pas le monde figé : sa variance
  d'activité est nulle → `𝓢 = 1.0` (zéro observé de variabilité). Un monde
  figé peut donc avoir 𝓢 = 1.0 **et** 𝓐 ≈ 0 — c'est voulu : deux propriétés
  distinctes (régularité vs vitalité).
- La pénalisation du **monde chaotique** vient **uniquement** de 𝓢 (et du
  drapeau CHAOTIC). 𝓐 ne pénalise pas le chaos : un monde hyperactif peut
  avoir `Â` éloigné de `A*` (pénalisé par 𝓐) **et** 𝓢 bas (pénalisé par 𝓢)
  — les deux signaux sont complémentaires, jamais multipliés deux fois.
- Aucune formule du DVI ne « double-pénalise » : chaque défaut est porté par
  sa composante naturelle, et le noyau géométrique les agrège une seule fois.

---

## 8. Couche post-run : trajectoire et diagnostic de crise

La couche post-run (décision 2A/3A) complète le moteur sans le remplacer.
Elle répond à deux questions que le moteur ne peut pas trancher :

1. **La trajectoire** : le monde s'améliore-t-il, se dégrade-t-il ou est-il
   stable au fil du run ?
2. **Les marges** : à quelle distance des seuils de crise le monde
   se trouve-t-il, et cette distance se creuse-t-elle ?

**Règle de composition (décision 3A)** : la trajectoire conditionne
**l'interprétation** du score ; elle n'est **jamais** un multiplicateur ni un
ajustement du score. Le moteur publie son score tel quel ; le post-run publie
des séries et des diagnostics séparés. Aucune formule opaque ne transforme
`ViabilityScore` en fonction de sa propre dérivée.

### 8.1 Séries et pentes — `ViabilityTrajectory`

**Nom** : Trajectoire de viabilité — `ViabilityTrajectory`

**Explication globale** : Agrège les séries d'activité, de ressources et de
population sur le run complet, et calcule leur **pente par régression des
moindres carrés ordinaires (OLS)** — le même outil que
`calibration.py:22-49` (`_least_squares_slope`, `_stats`). Déterministe, sans
paramètre appris.

**Expression mathématique** :

```math
\mathrm{slope}(S) = \frac{n \sum_{t=1}^{n} t \cdot S(t) \;-\; \Bigl(\sum_{t=1}^{n} t\Bigr) \Bigl(\sum_{t=1}^{n} S(t)\Bigr)}{n \sum_{t=1}^{n} t^2 \;-\; \Bigl(\sum_{t=1}^{n} t\Bigr)^2}

\mathrm{interpr\acute{e}tation}(S) =
\begin{cases}
\text{STABLE} & \text{si } |\mathrm{slope}(S)| \le \varepsilon \\[3pt]
\text{INCREASING} & \text{si } \mathrm{slope}(S) > \varepsilon \\[3pt]
\text{DECREASING} & \text{si } \mathrm{slope}(S) < -\varepsilon \\[3pt]
\text{NOT\_MEASURED} & \text{si la série est absente ou } n < 2
\end{cases}

\Delta_{\mathrm{pop}} = \frac{\overline{S}_{\mathrm{pop}}^{\,\mathrm{fin}}}{\max\!\bigl(\overline{S}_{\mathrm{pop}}^{\,\mathrm{ini}},\, 1\bigr)} - 1

\Delta_{\mathrm{act}} = \frac{\overline{S}_{\mathrm{act}}^{\,\mathrm{fin}}}{\max\!\bigl(\overline{S}_{\mathrm{act}}^{\,\mathrm{ini}},\, 1\bigr)} - 1

\mathrm{ViabilityTrajectory} =
\begin{cases}
\text{IMPROVING} & \text{si } \Delta_{\mathrm{pop}} > +\varepsilon_p \text{ et } \Delta_{\mathrm{act}} > +\varepsilon_a \\[3pt]
\text{DEGRADING} & \text{si } \Delta_{\mathrm{pop}} < -\varepsilon_p \text{ ou } \Delta_{\mathrm{act}} < -\varepsilon_a \\[3pt]
\text{STABLE} & \text{sinon} \\[3pt]
\text{NOT\_MEASURED} & \text{si les séries sont indisponibles}
\end{cases}
```

avec :

- `S(t)` : valeur de la série au tick `t`, `n` : nombre d'observations ;
- `slope(S)` : pente OLS déterministe, repli `None` si `n < 2`
  (`calibration.py:22-49`) ;
- `ε` : bruit de pente `[HÉRITÉ]` (défaut proposé `ε = 0.001` en unités de
  série normalisée — §12.1) ;
- `ε_p`, `ε_a` : seuils de variation relative tiers-à-tiers `[HÉRITÉ]` ;
- cas limites :
  - série absente ou `n < 2` → `NOT_MEASURED` (jamais `STABLE`) ;
  - run d'un seul tick → `NOT_MEASURED` ;
  - `ViabilityScore` non mesuré sur la majorité du run → série de score
    `NOT_MEASURED` (les autres séries restent disponibles).

**Pourquoi cette équation** :

- L'OLS est le diagnostic de tendance le plus sobre : une seule constante
  (la pente), déterministe, réutilisable depuis `calibration.py` — cohérent
  avec la politique d'ECHOS de n'inventer que des outils justifiés
  (`EMERGENCE_INDICATORS.md` §11).
- La trajectoire par **tiers** (et non par corrélation croisée) évite de
  surinterpréter le bruit tick par tick : le run est segmenté en trois
  blocs comparés par moyennes.
- Le **multiplicateur** (ex. `score × (1 + λ·slope)`) est explicitement
  écarté : il masquerait le score mesuré derrière une correction opaque et
  violerait le principe « score = noyau du tick » (§1.4, décision 3A).
- Alternative écartée : lissage exponentiel (paramètre d'oubli arbitraire) ;
  corrélation de Pearson score↔temps (sensible aux extrémités, moins lisible).

**Usage dans le projet** :

- **Post-run** : méthodes de la couche post-run sur `tick_summaries` +
  `tick_metrics` + `events_log` (store sqlite, `sqlite.py:386` et tables
  associées) ; sortie `ViabilityTrajectory` + `slope_*` publiés.
- **Interfaces** : graphiques de séries avec droite de récession OLS ;
  mention « trajectoire diagnostique — ne modifie pas le score ».

**Interprétation** :

| Trajectoire | Lecture |
| :-- | :-- |
| `STABLE` | Séries régulières — le monde tient son régime (à lire avec le régime §7). |
| `IMPROVING` | Population et/ou activité en hausse mesurée — le régime se consolide. |
| `DEGRADING` | Population et/ou activité en baisse mesurée — le régime s'effrite ; croiser avec les marges (§8.2). |
| `NOT_MEASURED` | Données insuffisantes — ne rien conclure. |

- **Unité** : `ViabilityTrajectory` est une **catégorie** ; les `slope_*` sont
  en unités de série par tick (fraction de population, fraction d'activité,
  fraction de stock par tick).

### 8.2 Marges aux seuils de crise

**Nom** : Marges de ressources — marge food / marge water

**Explication globale** : Distance horizontale (en ticks) entre l'état actuel
des stocks et les **seuils critiques** déjà utilisés par ECHOS, avec leur
évolution. C'est le complément quantitatif de la résilience (§5.5) : la
résilience dit **si** les crises passées ont été résorbées ; les marges
disent **à quelle distance** on se trouve d'une nouvelle crise.

**Expression mathématique** :

```math
\rho_{\mathrm{crit}} = 0.2 \quad (\text{constante } \texttt{\_CRITICAL\_RATIO}, \texttt{resource\_sustainability.py:62})

\mathrm{crit}_{\mathrm{food}} = \rho_{\mathrm{crit}} \cdot \mathrm{food}_{\mathrm{r\acute{e}f}}

\mathrm{crit}_{\mathrm{water}} = \rho_{\mathrm{crit}} \cdot \mathrm{water}_{\mathrm{r\acute{e}f}}

\mathrm{marge}_{\mathrm{ticks}}(s) = \frac{\mathrm{stock}(t) - \mathrm{crit}_s}{\max\bigl(-\mathrm{slope}(\mathrm{stock}),\, \varepsilon_{\mathrm{slope}}\bigr)}

\mathrm{marge}_{\mathrm{rel}}(s) = \mathrm{clamp}\!\left(\frac{\mathrm{stock}(t) - \mathrm{crit}_s}{\max(\mathrm{stock}(t),\, \varepsilon)},\; 0,\; 1\right)
```

avec :

- `food_réf`, `water_réf` : états de référence du stock (méthode du moteur
  `ResourceSustainabilityIndicators` — état initial ou moyenne historique
  selon le moteur ; la référence est publiée avec la métrique) ;
- `stock(t)` : niveau observé du stock au tick courant (`mean_food`,
  `mean_water`) ;
- `slope(stock)` : pente OLS de la série du stock (§8.1) ;
- `ε_slope` : garde-fou de division `[HÉRITÉ]` (défaut proposé `1e-6`) ;
- `ε` : petit positif de régularisation `[HÉRITÉ]` ;
- si `slope(stock) > 0` : pas de crise imminente par la tendance —
  `marge_ticks = null` (non applicable), pas `∞` ni `0` ;
- avertissements déterministes (drapeaux, non agrégés) :
  - `WARNING_FOOD_NEAR_CRITICAL`  si `marge_ticks(food) < w_food` ;
  - `WARNING_WATER_NEAR_CRITICAL` si `marge_ticks(water) < w_water` ;
  - `WARNING_POPULATION_DECLINE`  si `slope(S_pop) < −ε` ;
  - `WARNING_ACTIVITY_DECLINE`    si `slope(S_act) < −ε` ;
  - `WARNING_DEGRADING_WITH_THIN_MARGIN`
    si `ViabilityTrajectory = DEGRADING` et `marge_rel < w_rel` ;
- `w_food`, `w_water`, `w_rel` : seuils d'avertissement `[HÉRITÉ]`
  (défauts proposés en ticks de marge et en fraction — §12.1) ;
- cas limites : série absente → avertissement non émis (`NOT_MEASURED`).

**Pourquoi cette équation** :

- Réutilise **exactement** le seuil critique d'ECHOS
  (`_CRITICAL_RATIO = 0.2`, `resource_sustainability.py:62`) : aucune
  définition parallèle de la « crise ».
- La marge **horizontale** (en ticks) est la grandeur opérationnelle la plus
  lisible : « à ce rythme, le stock franchit le seuil dans X ticks ».
- Les avertissements sont des **drapeaux déterministes** : aucune probabilité,
  aucun modèle (§1.3).

**Usage dans le projet** :

- **Post-run** : `tick_summaries.mean_food/mean_water` + `events_log` ;
  aligné sur le moteur de ressources existant.
- Interfaces : affichage des marges et des drapeaux ; jamais comme score.

**Interprétation** :

- Marge relative élevée + pente positive : stock en croissance — confortable.
- Marge horizontale faible + pente négative : crise proche selon la tendance —
  croiser avec `ViabilityResilience` (le système a-t-il déjà résorbé ce type
  de crise ?).
- Aucun avertissement + trajectoire STABLE : régime stable au sens post-run.

---

## 9. Sorties publiées et contrat de lecture

### 9.1 Sorties du moteur (par tick d'analyse)

| Métrique | Type | `measured` | Rôle |
| :-- | :-- | :-- | :-- |
| `ViabilityScore` | fraction [0,1] ou `null` | oui | Noyau géométrique (§6.1) |
| `ViabilityCoverage` | fraction [0,1] | oui (κ toujours défini) | Couverture κ (§6.2) |
| `ViabilityRegime` | catégorie (§7.1) | oui | Régime observé |
| `ViabilityActivity` | fraction [0,1] | si 𝓐 mesuré | Composante 𝓐 |
| `ViabilityDiversity` | fraction [0,1] | si 𝓓 mesuré | Composante 𝓓 |
| `ViabilityStructure` | fraction [0,1] | si 𝓒 mesuré | Composante 𝓒 |
| `ViabilityRenewal` | fraction [0,1] | si 𝓖 mesuré | Composante 𝓖 |
| `ViabilityResilience` | fraction [0,1] ou `0.0` | si crise observée | Composante 𝓡 |
| `ViabilityPersistence` | fraction [0,1] | si 𝓟 mesuré | Composante 𝓟 |
| `ViabilityStability` | fraction [0,1] | si 𝓢 mesuré | Composante 𝓢 |
| `ViabilityTurnoverBalance` | ratio sans unité | si fenêtre mesurable | Balance diagnostique (§5.4) |
| `ViabilityTrajectory` | catégorie (§8.1) | post-run | Trajectoire (méthode) |
| `ViabilityDisclaimer` | texte invariant | toujours | Règle d'or (§2.6) |

### 9.2 Règles de lecture obligatoires

1. **Toujours afficher κ** (`ViabilityCoverage`) à côté de `ViabilityScore`.
2. **Toujours afficher le profil** des sept composantes — le score seul ne
  suffit jamais (§2.3).
3. **Distinguer `0.0` mesuré de `null` / non mesuré** (§2.2) — l'interface
  doit afficher « non mesuré — aucune crise observée » et non `0.0` pour
  `ViabilityResilience` sans crise.
4. **Afficher `ViabilityDisclaimer` tel quel** (§2.6).
5. **Ne jamais** afficher un score sans ses constantes versionnées
  (§12.1) ni sans sa date de génération.
6. **Ne jamais** traiter `ViabilityTrajectory` comme un multiplicateur du
  score (§8, décision 3A).

### 9.3 Contrat d'endpoint

- `/api/runs/{id}/viability` **reste inchangé** : séries sources, sans agrégat
  synthétique (`test_viability_endpoint_exposes_source_series_without_synthetic_score`).
- Le DVI s'ajoute comme **moteur de métriques** (publishant ses sorties par
  tick dans les tables de métriques) et comme **méthode post-run** ; tout
  endpoint futur qui exposerait le DVI devra respecter les règles §9.2.

---

## 10. Table de correspondance — catalogue 2.0.0

Les composantes du DVI consomment des **métriques primitives** déjà définies
dans le catalogue ECHOS 2.0.0 (`echos/echos/analysis/catalog.py`). Ce tableau
est la table de référence pour l'implémentation : aucune nouvelle métrique
primitive n'est inventée par le DVI — seules les métriques de **composante**
et de **score** sont nouvelles.

| Composante DVI | Métriques primitives consommées (catalogue 2.0.0) | Origine |
| :-- | :-- | :-- |
| 𝓐 Activité optimale | `DecisionCount` ; `history` (actions par tick) ; `\|events\|` | `cognitive_diversity.py` ; snapshot |
| 𝓓 Diversité | `BeliefDiversityNorm`, `GoalDiversityNorm`, `ActionDiversity` | `cognitive_diversity.py`, `reproducibility.py` |
| 𝓒 Structure | `ClusteringCoefficient`, `NetworkDensity`, `CommunityCoverage` | `social_complexity.py`, `group_dynamics.py` |
| 𝓖 Renouvellement | événements du catalogue (types création/destruction) | `event_types.py` / catalogue |
| 𝓡 Résilience | `RecoveryEpisodes`, `UnresolvedCrisisCount`, `resource_sustainability._CRITICAL_RATIO` | `resource_sustainability.py` |
| 𝓟 Persistance | `AverageGoalAge` ; tailles de communautés (`communities`) | `cognitive_diversity.py`, `_common.py` |
| 𝓢 Stabilité | variance d'activité (`history`) | snapshot (`_common.py:153-163`) |
| Post-run | `mean_food`, `mean_water`, `alive_count`, `DecisionCount` (séries) | `tick_summaries`, `tick_metrics` |

Les métriques de sortie **nouvelles** du DVI (`Viability*`) sont listées en
§9.1 et doivent être ajoutées au catalogue lors de l'implémentation (phase 1,
§11), avec statut `[HÉRITÉ]` pour les constantes et `exploratory` tant que
non calibré.

---

## 11. Phases d'implémentation

Le DVI est spécifié pour être implémenté **par étapes**, sans casser les
contrats existants. Chaque phase produit une valeur publiable et testable.

### Phase 1 — Moteur de fenêtre courte (immédiat)

**Portée** : `ViabilityIndicators`, registre après `EmergenceIndicators`.

- Implémenter les sept composantes sur le snapshot (fenêtre ≤ 100 ticks),
  en consommant les résultats des moteurs entrants via le registre
  (`emergence.py:230-241` modèle) et les replis snapshot (`_common.py`).
- Publier `ViabilityScore`, `ViabilityCoverage`, `ViabilityRegime`,
  les sept composantes, `ViabilityTurnoverBalance`, `ViabilityDisclaimer`.
- Constantes `[HÉRITÉ]` versionnées et publiées (§12.1) ; `measured` par
  `REQUIRES` (`_common.py:69-105`).
- Tests : golden values par composante (fixtures ECHOS-026/027) ;
  `κ = 0 → null` ; `X_i = 0 → score 0` ; `measured` pour 𝓡 sans crise ;
  STATIC/CHAOTIC ; déterminisme strict (deux exécutions identiques).
- **Ne pas** modifier `/api/runs/{id}/viability` ni `EmergenceIndicators`.

### Phase 2 — Fenêtré et post-run (après phase 1)

**Portée** : méthode post-run + séries longues.

- Séries `ViabilityScore` et composantes tick par tick dans `tick_metrics` /
  reconstruites depuis le store (`tick_summaries`, `events_log`).
- `ViabilityTrajectory` + pentes OLS (`_least_squares_slope`) + marges aux
  seuils (`_CRITICAL_RATIO`) + avertissements déterministes (§8).
- Tests : pentes sur séries synthétiques connues (croissante/décroissante/
  plate) ; `NOT_MEASURED` si série courte ; marges `null` si slope positive ;
  avertissements émis/absus selon les seuils.

### Phase 3 — Contractuel et calibration (après phase 2)

**Portée** : exposition contractuelle + campagne de calibration + sensibilité.

- Exposition des sorties DVI par endpoint publique **conforme aux règles
  §9.2** ; κ et disclaimer systématiques ; aucune synthèse opaque.
- Campagne de calibration (§12.2) : ajuster les constantes sur un jeu de
  scénarios de référence (`REFERENCE_SCENARIOS.md`), publier les résultats et
  les intervalles ; pas d'apprentissage — réglage de paramètres déterministes
  et reproductibles.
- Analyse de sensibilité (§12.3) : impact de chaque poids et constante sur le
  score ; publication des résultats.
- Dériver le DVI d'`EmergenceScore` **si et seulement si** la calibration
  démontre un gain de stabilité/interprétabilité (décision 1A : cohabitation
  jusqu'à preuve contraire) ; sinon `EmergenceScore` reste publié tel quel.
- Tests contractuels : invariants §9.2 ; comparabilité κ ; version des
  constantes exposée.

---

## 12. Calibration sans apprentissage

Le DVI ne contient **aucun modèle d'apprentissage** (§1.3). La calibration
consiste uniquement à **choisir des constantes déterministes** sur un jeu de
scénarios de référence, puis à les **publier et versionner**.

### 12.1 Constantes `[HÉRITÉ]` proposées

| Constante | Symbole | Défaut proposé | Unité | Rôle |
| :-- | :-- | :-- | :-- | :-- |
| Activité cible | `A*` | `0.5` | fraction [0,1] | Centre de la zone optimale 𝓐 (§5.1) |
| Demi-largeur de zone | `σ_A` | `0.25` | fraction | Largeur de la cloche 𝓐 |
| Repère activité | `r_act*` | `0.8` | fraction d'entités actives | Normalisation `a_act` |
| Repère décisions | `r_dec*` | `2.0` | décisions/entité/tick | Normalisation `a_dec` |
| Repère événements | `r_evt*` | `0.5` | événements/entité/tick | Normalisation `a_evt` |
| Repère renouvellement | `r_G*` | `0.1` | nouveautés/entité/tick | Normalisation 𝓖 |
| Variance de référence | `σ_V` | `0.05` | variance d'activité | Pénalité de variabilité 𝓢 |
| Seuil figé | `a_static` | `0.1` | fraction | Drapeau STATIC (§7.1) |
| Seuil chaotique | `s_chaotic` | `0.05` | fraction | Drapeau CHAOTIC (§7.1) |
| Seuils de régime | `s1..s4` | `0.15 / 0.30 / 0.50 / 0.70` | fraction | Graduation ViabilityRegime |
| Seuil régime optimal | (s4) | `0.85` | fraction | OPTIMAL (complète s4) |
| ε de pente | `ε` | `0.001` | unités de série/tick | STABLE vs INCREASING/DECREASING |
| ε de marge | `ε_slope` | `1e-6` | stock/tick | Division marge (§8.2) |
| Seuils d'avertissement | `w_food`, `w_water` | `10` | ticks de marge | Avertissements de crise |
| Seuil marge fine | `w_rel` | `0.2` | fraction | WARNING_DEGRADING_WITH_THIN_MARGIN |

**Poids des composantes** (`∑ᵢ wᵢ = 1`) :

| Composante | Poids `[HÉRITÉ]` | Justification sommaire |
| :-- | :-- | :-- |
| 𝓐 Activité optimale | `0.20` | Propriété fondatrice du régime dynamique (§2.5) |
| 𝓓 Diversité | `0.15` | Condition de robustesse cognitive/sociale |
| 𝓒 Structure | `0.15` | Organisation réseau nécessaire à la diffusion |
| 𝓖 Renouvellement | `0.15` | Capacité de production de nouveauté |
| 𝓡 Résilience | `0.10` | Diagnostic de sortie de crise (souvent non mesuré) |
| 𝓟 Persistance | `0.10` | Durée des structures (secondaire par rapport à la vitalité) |
| 𝓢 Stabilité | `0.15` | Régularité du régime (condition de lecture du score) |

Sous-mesures (défauts `1/3` ou `1/2` égaux, publiés) : 𝓐 `(w_a,w_d,w_e)` ;
𝓓 `(w_b,w_g,w_a)` ; 𝓒 `(w_c,w_d,w_k)` ; 𝓟 `(w_m,w_g)`.

Toute calibration de phase 3 **remplace** ces défauts par des valeurs mesurées,
publiées avec la version du moteur. Les défauts `[HÉRITÉ]` restent utilisables
en cas d'absence de calibration.

### 12.2 Campagne de calibration (phase 3)

- **Jeu de scénarios** : `REFERENCE_SCENARIOS.md` — mondes de référence
  (stable, chaotique, figé, en crise, en récupération, en dérive lente).
- **Signal de vérité observée** : `population_outcome()` (`sqlite.py:386`) +
  issues de run documentées (extinction, survie, dérive) — **pas** un
  étiquetage humain arbitraire, **pas** un modèle.
- **Procédure déterministe** : grid search ou ajustement en deux passes sur
  les constantes de §12.1 ; chaque combinaison est rejouée sur le jeu complet ;
  les résultats sont versionnés avec le code de calibration.
- **Critères** (publiés) : séparation des régimes (figé vs viable vs chaotique
  sur le jeu de référence), stabilité entre exécutions, sensibilité bornée
  (§12.3).
- **Sortie** : tableau des constantes calibrées + justification +
  `LIMITATIONS.md` mis à jour si nécessaire.

### 12.3 Analyse de sensibilité

- Faire varier chaque poids de §12.1 de ±20 % ; mesurer l'impact sur
  `ViabilityScore` et `ViabilityRegime` sur le jeu de référence.
- Publier les résultats (matrice de sensibilité) ; un poids dont la
  variation produit un basculement de régime fréquent est signalé comme
  **critique** et discuté dans `LIMITATIONS.md`.
- Aucune pondération n'est « optimisée par gradient » : la sensibilité est
  un **diagnostic d'interprétabilité**, pas une boucle d'apprentissage.

---

## 13. Limites connues et risques d'interprétation

| # | Limite | Impact | Atténuation |
| :-- | :-- | :-- | :-- |
| L1 | **Constantes `[HÉRITÉ]` non calibrées** (tant que la phase 3 n'est pas faite) | Les seuils et poids proposés ne sont pas validés empiriquement ; les régimes sont indicatifs | Toujours publier κ + profil ; statut `exploratory` ; calibration phase 3 |
| L2 | **Résilience souvent non mesurée** (crises absentes) | 𝓡 retire son poids du noyau fréquemment ; κ varie d'un tick à l'autre | Toujours afficher κ ; croiser avec les marges §8.2 |
| L3 | **Pas de manipulation du monde** (§1.3) | La résilience ne mesure que les crises **spontanées** — un monde jamais éprouvé n'a pas de 𝓡 | Afficher « aucune crise observée » et non un score ; phase 3 : jeux de scénarios de crise |
| L4 | **Fenêtre courte du moteur** (≤ 100 ticks) | Les tendances lentes sont invisibles ; le score peut être stable alors que le run se dégrade | Couche post-run (§8) systématiquement consultée pour les décisions |
| L5 | **Aucune preuve scientifique** (§2.6, ECHOS-032) | Un DVI élevé ne prouve ni vie, ni société, ni émergence | Disclaimer invariant ; vocabulaire `measured`/états de données |
| L6 | **Compensation interdite** (§2.3) | Un score à 0.0 ne dit pas **quelle** propriété manque | Profil obligatoire (§9.2) |
| L7 | **Échantillonnage agent** (`context_every`) | Diversité et activité estimées sur échantillon — bruit de mesure possible | Consistance déterministe (ECHOS-027) ; mentionner l'échantillon dans les interfaces |
| L8 | **Cap d'événements (2000)** | En run très actif, la fenêtre publiée peut tronquer les événements — biais possible sur 𝓖 et 𝓡 | `event_window_published` publie le cap ; le moteur le signale dans ses métadonnées |
| L9 | **Double comptage inter-primitives** | Les mêmes croyances alimentent 𝓓 et le moteur d'émergence | Le DVI ne consomme **pas** `EmergenceScore` (§1.2) ; agrégation distincte, pas de somme des deux scores |
| L10 | **Trajectoire non agrégée** (§8) | Un score élevé avec trajectoire DEGRADING peut surprendre l'utilisateur | Règle §9.2.6 + affichage systématique de la trajectoire à côté du score post-run |

Ces limites sont le complément de `LIMITATIONS.md` (général) et
`EMERGENCE_INDICATORS.md` §14 (moteur d'émergence) ; en cas de contradiction,
le code fait foi.

---

## 14. Statut et règle d'or

### 14.1 Statut

| Aspect | Statut |
| :-- | :-- |
| Document | **[DRAFT]** — spécification mathématique complète, en attente de revue |
| Composantes | Spécifiées (§5) — **non implémentées** |
| Constantes | `[HÉRITÉ]` proposées (§12.1) — **non calibrées** |
| Moteur | `ViabilityIndicators` — **à créer** (phase 1, §11) |
| Post-run | Méthode — **à créer** (phase 2, §11) |
| Calibration | **À faire** (phase 3, §12.2) |
| `EmergenceScore` | Inchangé, toujours le composite implémenté (§1.2) |

### 14.2 Règle d'or

> **Le DVI ne prouve rien. Il mesure un régime observé, dans une fenêtre
> donnée, avec des constantes versionnées.** Un indice élevé n'est ni une
> preuve d'émergence, ni une preuve de vie, ni une preuve de société. Un
> indice nul mesuré signale une propriété manquante — pas un échec
> scientifique. (`VISION.md`, `EMERGENCE_INDICATORS.md` §15, ECHOS-032.)

---

## Points restés ouverts

1. **Revue des défauts `[HÉRITÉ]`** (§12.1) — les valeurs proposées
   (`A* = 0.5`, `σ_A = 0.25`, poids `0.20/0.15/...`) sont des points de
   départ raisonnables, pas des valeurs mesurées. La calibration (phase 3)
   doit les confronter aux scénarios de référence.
2. **Repères `r_act*`, `r_dec*`, `r_evt*`, `r_G*`** — à recalibrer par
   campagne ; les défauts supposent des mondes « typiques » ECHOS.
3. **Seuils d'avertissement de marge** (`w_food`, `w_water` en ticks) — à
   ajuster selon la durée de run médiane des déploiements.
4. **Ordre de priorité STATIC vs CHAOTIC** (§7.1) — hypothèse CHAOTIC >
   STATIC retenue par urgence ; à valider sur les scénarios de référence.
5. **Coexistence avec `EmergenceScore`** — décision 1A : cohabitation ; la
   bascule (si elle a lieu) exige une phase de calibration comparant les deux
   composites sur le même jeu de scénarios, et une mise à jour de
   `EMERGENCE_INDICATORS.md`.
6. **Exposition contractuelle** (phase 3) — à spécifier finement : quels
   endpoints, quelles garanties de compatibilité, quelles invariants de κ et
   disclaimer dans les schémas API.
7. **Interaction avec `ANALYSIS_FOUNDATIONS.md`** — ce document spécifie le
   DVI ; le document des fondements reste la référence pour les équations du
   cœur analytique implémenté. Toute divergence ultérieure doit être résolue
   en faveur du code, puis propagée dans les deux documents.

---

*Dernière mise à jour : 6 octobre 2026 — [DRAFT] — spécification complète des
composantes, du noyau géométrique, des régimes et de la couche post-run ;
implémentation et calibration restent à faire (phases 1–3).*
