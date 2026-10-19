# Validation expérimentale et verdicts — octobre 2026

**Périmètre :** moteur SYNE 0.17.0, `schemaVersion` 5, contrat d’observabilité 0.5.0; comparaison historique avec SYNE 0.15.0 (`707f86c`). Campagnes rapportées les 17 octobre 2026. Les sorties de données brutes, configs de campagne et scripts d’analyse sont des artefacts temporaires; les valeurs, protocoles, limites et verdicts ci-dessous constituent le relevé pérenne.

## 1. Run de référence et viabilité localisée (BL-29, BL-35)

Profil `reference`, monde 500 × 500, 100 agents, 2 500 ticks, seed 12345; ressources localisées, 3 gisements par type, rayon de perception des gisements 200, rayon agent 50. Aucune surcouche `--config` dans le run de référence. Le flux complet distinguait absence (`utility: null`), score reporté (`heldUtility`) et distance non mesurée (`null`).

**Résultat de référence :** population finale 100, aucun décès; checksum `0xdb5979d695a2dc6a`.

Le gate de viabilité localisée a préenregistré quatre critères avant exécution :

| Critère | Résultat |
|---|---|
| V1 — 100 agents vivants à 2 500 ticks sur chacune des seeds 12345, 424242, 999 | Franchi 3/3; aucune mort ni extinction. |
| V2 — sensibilité aux seeds | Franchi; les trois checksums d’état final sont distincts. |
| V3 — quête/conso adossée à un dépôt connu dans le flux seed 12345 | Franchi : 215 411 décisions `SeekFood`/`SeekWater`/`Eat`/`Drink`, toutes avec cible de type `node`; aucune consommation hors dépôt. |
| V4 — compétition observable pour food et water | Franchi : les 3 dépôts de chaque ressource visés par plusieurs agents. `food-1` concentre 58,9 % des décisions food et `water-1` 67,1 % des décisions water. |

La configuration `reference` déclare maintenant explicitement `resources.localized = true`; les dérives social/curiosité sont 0,003/0,002. `ReferenceProfileParityTests` verrouille l’égalité defaults/profil/fichier (3 tests). Ce gate mesure la viabilité et la possibilité d’observer la compétition; il n’est pas un verdict d’émergence ni une validation au-delà de 2 500 ticks.

## 2. Répétitions et sensibilité (BL-30)

Dix seeds (`12345, 424242, 999, 7, 314159, 2026, 2718, 161803, 42, 137`) ont été exécutées sur 2 500 ticks. **100/100 agents vivants** sur chacune; les dix checksums sont distincts. Trois exécutions de seed 12345, dont une avec export de flux, ont le checksum `0xdb5979d695a2dc6a`; l’export ne change pas l’état. Trois surcouches (faim ×2, dérive sociale nulle, réserves globales) ont donné des checksums distincts de la référence. Ces faits établissent viabilité sur l’horizon, déterminisme dans les cas testés et sensibilité aux paramètres; ils n’établissent pas une émergence.

## 3. Remontée de décision (BL-31)

Sur le flux de référence (seed 12345, 2 500 ticks), 250 000 décisions ont été analysées : 24 983 `fresh`, 222 821 `holdover`, 1 746 `interrupted`, 450 `none`. 241 200 portent `evidence[]`; 20 décisions fraîches réparties sur l’horizon ont été remontées du choix à la cible et au fait source.

Aucune violation de `observedTick ≤ tick` ni de `confidence ∈ [0,1]` n’a été relevée. Les 835 décisions fraîches sans preuve comprennent 717 `Idle` et 118 `Rest`, actions sans cible externe; aucune décision à cible externe n’était sans preuve. Les sources observées sont la perception (241 069 occurrences) et les messages (131). Cibles : 236 500 nœuds et 4 700 entités. Limite : la campagne d’échantillonnage porte sur une seule seed.

## 4. Comparaison avant/après (BL-32)

Les critères C1–C5 ont été fixés avant comparaison : viabilité, quête spatiale, exutoires cognitifs, traçabilité causale et absence distincte du zéro. Les versions 0.15.0 (`707f86c`) et 0.17.0 ont été exécutées sur des seeds identiques.

| Critère | Avant 0.15.0 | Après 0.17.0 |
|---|---|---|
| Viabilité, 10 seeds, 2 500 ticks | 100 vivants sur chacune | 100 vivants sur chacune |
| `SeekFood` / `SeekWater`, 500 ticks, seed 12345 | 0 / 0; `Eat`/`Drink` directs = 75,7 % des décisions | 15 523 / 19 784; trajets vers une cible de nœud connue |
| `Explore` / `Socialize`, même fenêtre | 0 / 0 | 4 556 / 592 décisions |
| Preuves, cible, état décisionnel | absents | publiés; 241 200 décisions avec `evidence[]` sur 250 000 |
| Absence de mesure | utilité `0` | `null`, avec `heldUtility` séparé pour le report |

La fenêtre comportementale détaillée porte sur une seed; la viabilité longue sur dix. Il s’agit d’une comparaison de deux versions de code, pas d’une expérience isolant causalement chaque changement.

## 5. Protocole de verdict A0-4 / BL-04

Le protocole fixe cinq exigences : (1) phénomène, métriques, seuils, seeds, population, horizon, bras et logique de verdict préenregistrés avant exécution, les seuils étant lus par le harnais depuis le pré-enregistrement; (2) au moins cinq seeds, avec réussite dans au moins `ceil(0,8 × K)`; (3) contre-factuel baseline et ablation/contraste requis; (4) provenance distincte mesuré, absent (`null`) et zéro observé; (5) aucun déplacement de critère après observation. Verdicts possibles : confirmé, non confirmé, inconclusif (campagne inexécutable).

## 6. Campagne d’ablation/contraste et verdicts (BL-36)

Le pré-enregistrement définissait 3 bras × 5 seeds × 50 agents × 1 000 ticks (15 runs), avec deux phénomènes candidats. Les 15 runs sont restés viables (50/50 agents vivants).

| Phénomène | Verdict | Résultat synthétique |
|---|---|---|
| P1 — cycle homéostatique du besoin social | **Non confirmé** (0/5 seeds satisfont toutes les conditions) | En ablation (`socialRecovery = 0`), aucun cycle chez aucun agent sur 5/5 seeds; cependant la saturation médiane en baseline vaut 1,0, au-dessus du seuil préenregistré `< 0,95`. Le verdict demeure négatif; seuil non déplacé. |
| P2 — quête spatiale vers un gisement connu | **Confirmé par contraste** (5/5 seeds) | Part de décisions fraîches ciblant un nœud : 0,87–0,91 en baseline localisée contre zéro observé sous `resources.localized = false`; part `Seek*` : 0,63–0,73 contre 0–0,01. La distance à une source est absente dans le bras sans nœuds, jamais codée comme zéro. |

Le verdict scientifique général reste **« émergence non établie »**. Un phénomène candidat (quête spatiale) passe le contraste préenregistré; l’autre (cycle social) ne satisfait pas son critère complet. Ce résultat est une sortie légitime du protocole, pas une preuve générale d’émergence.

## 7. Portée et limites

- Gate de viabilité : trois seeds, 100 agents, horizon 2 500 ticks; aucun résultat sur horizon long, raréfaction ou saison hivernale.
- Échantillonnage causal : une seed; il ne remplace pas l’examen de toutes les trajectoires/configurations.
- Comparaison avant/après : critères fixés d’avance, mais versions de code et schémas différents; elle documente les changements ciblés et n’isole pas chaque cause.
- Campagne A0-4 : cinq seeds par bras et deux phénomènes formulés; la confirmation de quête spatiale n’est pas une confirmation d’intelligence, de société ou d’émergence au sens général.
- Les preuves brutes et scripts temporaires ont été retirés; les suites de non-régression et profils de configuration demeurent dans les composants.
