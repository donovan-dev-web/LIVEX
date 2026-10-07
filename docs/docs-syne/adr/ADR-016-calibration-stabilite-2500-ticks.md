# ADR-016 : Calibration B1 — stabilité du scénario par défaut sur 2500 ticks

**Composant** : SYNE (défauts intégrés) / ECHOS (viabilité observée)
**Statut** : Acceptée
**Date** : 7 octobre 2026
**Dépend de** : ADR-014, ADR-015, ADR-009 (énergie = monnaie d'action), DETERMINISM.md §7
**Source** : campagne de runs multi-seeds exécutée le 07/10/2026 (50 et 100 agents × 2500 ticks × seeds 12345 / 424242 / 999) + diagnostic de bilan énergétique par tick (`scripts/calibration-campaign.py`)

---

## Contexte

### Le symptôme signalé

Plusieurs runs à graine variée, **50 agents**, s'éteignent **toujours aux alentours
du tick 300/350**. Reproduit et mesuré : `raw` (défauts intégrés + `agents.initialCount = 50`),
seed 12345 → **extinction t303**, 50 morts `energy_exhaustion`, réserve de nourriture à 0
depuis les premières centaines de ticks.

Cause : les runs du Launcher (et tout run sans surcouche) n'exécutent **pas** le profil de
reference. `RunEngineProfile.ConfigOverlay()` ne pousse que `agents.initialCount` +
`simulation.ticksPerSecond`, et `--simulation reference` ne fait qu'étiqueter l'export —
le moteur construit donc `ConfigLoader.LoadDefaults()`, c'est-à-dire le profil **brut**
(nourriture 100 sans régénération, `moveEnergyCost` 0,5, Eat/Drink sans récupération
d'énergie). Le D1 (ADR-015) n'avait calibré que le **profil** de référence, jamais les
défauts.

### Les trois causes mesurées (bilan énergétique réconcilié tick par tick)

Le bilan est **exact** : `énergie(t) = min(100, énergie(t−1) + delta déclaré de l'action)`
restitue l'énergie observée à 0 près sur l'ensemble des agents-ticks (59 950 observations
sur un run de diagnostic).

1. **La régénération de la nourriture était annulée.** Avec `degradationTick = 100`, la
   réserve perd en fin de période exactement ce que la régénération y a ajouté (apport net
   nul) : la réserve se réduit à sa valeur initiale. 100 agents épuisent les 10 000 de D1
   avant t2000 (mesure : `food` atteint **699** à t1200), Eat devient `Blocked` (réserve
   vide) et l'agent perd le seul gain d'énergie qui le compensait.
2. **Les besoins sociaux monopolisaient la délibération.** `socialDriftRate` 0,001 et
   `curiosityDriftRate` 0,002 n'ont **aucun mécanisme de satisfaction** (aucune action ne
   décrémente `Social`/`Curiosity`, `SetCuriosityElapsed` n'est jamais appelé) : le besoin
   franchit son seuil (0,7 / 0,3) au tick ~700 / ~150 et y reste. Mesure sur un run de
   1200 ticks : part de `Socialize` par fenêtre de 300 ticks = 0 % → 0 % → 51 % → **96 %**,
   `Rest` tombe à 0 %. Le mouvement payé (Explore + Socialize = 75 à 98 % des ticks) n'est
   alors **jamais compensé** : pente d'énergie −0,037/tick en régime établi.
3. **Coûts auxiliaires hors bilan.** L'envoi coûte `0,5 + payload × 0,1` pour ~0,2 envoi et
   ~0,15 réception par agent-tick : ≈ 0,12 énergie/tick, soit **4× le coût de déplacement**
   recalibré — impossible à compenser par Eat/Drink.

### État du profil de référence (D1) à ce stade

La campagne V2 (3 × 1200 ticks, 100 agents) avait déjà noté les critères 2 et 3 de
l'ADR-015 **non atteints** (pente ≈ −0,039/tick, part Eat/Drink 1,9–3,2 %) : le D1 était
une amélioration, pas une stabilisation. Campagne de contrôle reproduite ici (profil de
reference, 50 agents, 2500 ticks) : 0 extinction, mais **23 à 31 morts** et pente
−0,030/−0,036/tick — soit une extinction par épuisement au-delà de ~t3000.

## Décision

**Calibrer les défauts intégrés du moteur** (`SimulationOptions`), pour qu'un run lancé
sans surcouche soit viable à l'horizon 2500 ticks — et rejouer explicitement les mêmes
valeurs dans `SimulationProfiles.Reference()` / `configs/simulation/reference.json`, le
chemin HTTP (`config ?? ReferenceJson()`) ne devant pas dépendre d'un défaut qu'on
oublierait de recaler.

| Levier | Avant (0.14.0) | Après (0.15.0) | Cause mesurée |
| :-- | :-- | :-- | :-- |
| `agents.actions.moveEnergyCost` | 0,5 | **0,03** | mouvement = 75–98 % des ticks : à 0,5 (0,05 en D1) son coût dépasse le revenu amorti de Eat/Drink (≈ 0,057/tick) |
| `agents.needs.socialDriftRate` | 0,001 | **0,0002** | seuil 0,7 franchi au tick ~700 → `Socialize` permanent (96 % des décisions en fin de run) |
| `agents.needs.curiosityDriftRate` | 0,002 | **0,0005** | seuil 0,3 franchi au tick ~150 → `Explore` permanent |
| `resources.food` | 100, régén 0, dégrad. 100 | **20 000, régén 20, dégradation inerte** | dégradation = annulation nette de la régénération ; 100 agents épuisent 10 000 avant t2000 |
| `resources.water` | 1 000, régén 5 | **20 000, régén 10** | marge de même ordre pour l'horizon visé |
| `communication.{send,receive}EnergyCost` + facteurs | 0,5 / 0,1 / 0,2 / 0,05 | **0 / 0 / 0 / 0** | ≈ 0,12 énergie/tick d'envoi, 4× le coût de déplacement recalibré |
| `communication.relayEnabled` + `max{Sends,Receives}PerTick` | true, 5, 3 | **false, 1, 1** | hérité de D1 (« coûts auxiliaires neutres ») |
| `agents.actions.catalog.{eat,drink}.energyRecovery` | 0 / 0 | **2,0 / 1,0** | hérité de D1 |
| `agents.actions.{restEnergyGain,restFatigueRecovery}` | 0,5 / 1 | **1,5 / 2** | hérité de D1 |

Aucune formule ni aucun mécanisme n'est modifié : **configuration uniquement** (règle D2
de l'ADR-015, « configuration changes require a reviewed decision »).

### Corrections associées découvertes pendant la campagne

1. **`configs/simulation/reference.json` n'appliquait pas la calibration D1.** Le fichier
   portait `agents.actions.catalog.eat.energyRecovery` alors que la clé réelle est
   `agents.actions.catalog.entries.eat.energyRecovery` — `System.Text.Json` ignore les
   propriétés non mappées, donc **toute** exécution `--config configs/simulation/reference.json`
   (dont `scripts/batch-analysis.py`) tournait avec `eat/drink.energyRecovery = 0`
   (mesuré : `energyDelta` moyen d'Eat = −0,2 au lieu de +1,8). Corrigé, et le chemin
   `entries` est désormais celui du fichier.
2. **`TickBudgetSnapshot.ComputationShare()` mélangeait des unités** : elle sommait
   `MeanMs(phase)`, temps **par échantillon**, alors que les phases intra-entité sont
   échantillonnées par entité × tick et les phases réseau/monde par tick — la part
   affichée revenait à (communication + événements) / tick, sous-estimant les phases
   intra-entité d'un facteur ≈ population. Corrigé en temps **par tick** ; le seuil
   ≥ 30 % (PERFORMANCE.md §9) reste inchangé et tenu.

## Critères d'acceptation et résultats de la campagne

Protocole : défauts intégrés **sans aucune surcouche** (`agents.initialCount` seul),
2500 ticks, seeds 12345 / 424242 / 999, populations **50 et 100** ; métriques calculées
depuis le flux d'observabilité par tick (`scripts/calibration-campaign.py`) :

1. **zéro extinction** ;
2. **|pente d'énergie moyenne| < 0,005/tick** sur les 200 derniers ticks (mort lente
   détectée même sans extinction) ;
3. **population finale ≥ 90 %** de la population initiale.

Résultats (exécution du 7 octobre 2026, défauts intégrés 0.15.0, `--overlay` vide sauf
`agents.initialCount`) :

| Pop | Seed | Vivants (fin / init) | Extinction | Pente énergie (200 t) | Énergie fin | Morts | Nourriture (min..max) | Eau (min..max) | Verdict |
| --: | --: | --: | :-- | --: | --: | --: | --: | --: | :-- |
| 50 | 12345 | 50 / 50 | — | −0,0014/tick | 97,0 | 0 | 19 730..53 187 | 15 032..25 150 | ✓ |
| 50 | 424242 | 50 / 50 | — | −0,0037/tick | 94,6 | 0 | 19 730..53 633 | 15 028..25 792 | ✓ |
| 50 | 999 | 50 / 50 | — | −0,0027/tick | 96,7 | 0 | 19 730..53 402 | 15 046..25 614 | ✓ |
| 100 | 12345 | 100 / 100 | — | −0,0018/tick | 96,5 | 0 | 10 630..36 524 | 4 080..20 710 | ✓ |
| 100 | 424242 | 100 / 100 | — | −0,0028/tick | 95,2 | 0 | 10 630..37 173 | 4 000..20 710 | ✓ |
| 100 | 999 | 100 / 100 | — | −0,0025/tick | 96,6 | 0 | 10 630..36 848 | 4 217..20 710 | ✓ |

**6/6 runs : 0 extinction, 0 mort, |pente| ≤ 0,0037/tick < 0,005, population finale =
population initiale** — les trois critères sont atteints sur les deux populations.
À titre de comparaison, les défauts 0.14.0 s'éteignaient à t303 (50 agents, 50 morts
`energy_exhaustion`, nourriture à 0). Artefacts : `campaign.json` de chaque run
(reproductible par `scripts/calibration-campaign.py --overlay <vide> --populations 50,100
--seeds 12345,424242,999 --ticks 2500`).

## Conséquences

### Positives

- Le chemin réel des runs (Launcher → overlay `initialCount` + défauts) est viable :
  plus d'extinction « mystérieuse » au tick 300.
- Un seul jeu de valeurs à maintenir : défauts = profil de référence = `reference.json`.
- Diagnostic outillé et rejouable : `scripts/calibration-campaign.py` (campagne
  pop × seeds avec critères) + réconciliation du bilan énergétique.

### Négatives

- **Trajectoire de référence altérée** → `engineVersion` 0.14.0 → **0.15.0** (bump MINOR)
  et **4 checksums dorés re-calés** dans le même commit (procédure DETERMINISM.md §7) :
  perception `0x46769cfb11c8b3a7` → `0xf4aaa2733e491935`, état complet
  `0xe62395429b50b7c1` → `0x954b8e0e70c0af5f`.
- 27 tests unitaires adaptés (valeurs de défauts, réserves explicites là où le test
  visait un mécanisme, formules de décisions n°9/n°13 posées explicitement).
- L'énergie cesse d'être une contrainte réelle : avec `energyRecovery` Eat/Drink > coût
  de déplacement, les agents tiennent le plafond et mangent aussi pour l'énergie
  (ratio d'exécution Eat ≈ 8 % contre 1,7 % attendu à faim seule) — la nourriture devient
  la vraie contrainte, dimensionnée pour l'horizon.
- `configs/simulation/raw.json` reste le jalon historique **non calibré** : il reproduit
  l'ancien comportement et s'éteint à ~t300. C'est volontaire (point de comparaison
  inter-générations), à ne pas présenter comme un scénario viable.

### Risques

- Au-delà de 2500 ticks, la stabilité n'est pas démontrée (la dérive résiduelle
  mesurée reste négative mais < 0,005/tick) — à re-mesurer au prochain allongement
  d'horizon.
- Les besoins sociaux n'ont toujours **pas** de mécanisme de satisfaction : la
  dérive ralentie ne fait que repousser la saturation (tick ~3500 pour `socialDriftRate`).
  Voir « Alternatives considérées ».

## Alternatives considérées

- **Calibrer le seul profil de référence** (défauts inchangés) → refusé : le chemin
  Launcher n'applique jamais ce profil, le symptôme signalé persiste.
- **Faire appliquer `--simulation reference` par le CLI** → non retenu comme substitut
  (utile en complément) : laisser un scénario « reference » exécuter des défauts non
  viables est ce qui a produit le bug ; la correction porte sur les valeurs, pas sur le
  routage.
- **Ajouter un mécanisme de satisfaction des besoins sociaux** (`Socialize` décrémente
  `Social`, `Explore` décrémente `Curiosity`) → **la bonne réponse produit**, mais c'est
  un changement de code/mécanisme hors périmètre « configuration only » de cette
  itération ; à porter en ADR dédié (condition de réouverture ci-dessous).
- **Abaisser `moveEnergyCost` à 0,01 voire neutraliser le mouvement** → refusé : le coût
  d'action doit rester lisible (ADR-009, énergie = monnaie d'action).

## Validation / rejet

- Campagne 50 et 100 agents × 2500 ticks × 3 seeds sur défauts purs : 0 extinction,
  |pente| < 0,005/tick, population ≥ 90 % (protocole ci-dessus, rejouable à tout moment
  par `scripts/calibration-campaign.py`).
- Déterminisme : `FullPipeline_SameSeed_IsBitForBitReproducible` + deux campagnes de
  même seed produisant les mêmes empreintes.
- Suite SYNE complète verte (589 tests) après re-pin des dorés.
- **Réouverture** si : (a) un horizon > 2500 ticks est visé, (b) un mécanisme de
  satisfaction des besoins sociaux est implémenté, (c) la part des décisions Eat/Drink
  contraintes par la faim doit redevenir un critère (le critère 3 de l'ADR-015 reste
  non atteint : la faim moyenne ne dépasse jamais 70 avec ces défauts, il est donc
  vide, pas rempli).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 7 oct. 2026 | Création | Calibration B1 des défauts (stabilité 2500 ticks) + correctifs `reference.json` / `ComputationShare` |
