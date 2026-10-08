# ADR-015 : Calibration de survie du scénario de référence (décision D1)

**Composant** : SYNE (profil de référence) / ECHOS (viabilité observée)
**Statut** : Acceptée
**Date** : 29 septembre 2026
**Dépend de** : ADR-014, ADR-009, DETERMINISM.md §7, `RAPPORT-ELEMENTS-OUVERTS.md` §5.1 (campagne J-C1 → J-C4, décisions D1/D2/D3)
**Source** : Rapport de campagne de runs (3 runs pilotés 1200 ticks — seeds 12345 / 424242 / 999 — + benchmark CLI + 4 runs batch 2000 ticks)

## Contexte

La campagne de runs établit deux faits structurants :

1. **Le scénario de référence n'est pas viable à long terme** : 2 extinctions sur 3 ;
   la population survivante est en **mort lente** (énergie moyenne 69 → 49 entre t800
   et t1200), sous le seuil de détection des tests fonctionnels.
2. **L'arbitrage utilitaire n'impose pas de se nourrir faim saturée** : utilité
   moyenne Eat 13,9 vs Socialize 83,7 ; part des décisions Eat/Drink de 9,7 % sur
   les ticks où la faim moyenne dépasse 70.

Causes mesurées dans le code (vérifiées, non inférées) :

- `UtilityEvaluator.BenefitOf` : Eat/Drink plafonnés à `min(need, 30)` — à faim
  saturée, l'urgence (sigmoïde ×20, +10 critique) ne compense jamais un bénéfice
  capé face à Socialize (`Social × 60`) ; le plafond rend aussi SeekFood ≡ Eat,
  l'agent bouclant sur le déplacement au lieu de l'action qui résout le besoin.
- Bilan énergétique : seul `Rest` rapporte de l'énergie (`restEnergyGain` 1,5) ;
  le coût des déplacements (Explore 48 %, Socialize 34 % des décisions) n'est
  jamais compensé → pente d'énergie négative continue jusqu'à `DeathEnergyThreshold` 0.

## Décision

**Calibration rééquilibrée, portée exclusivement par la configuration du profil de
référence** — jamais en dur dans la boucle de décision (règle d'or D2 : ECHOS reste
observe-only ; la calibration reste « configuration changes require a reviewed
decision », SYNE-131) :

| Levier | Avant | Après | Justification |
| :-- | :-- | :-- | :-- |
| `BenefitOf` Eat/Drink (formule, paramétrable par seuils en V0.2) | `min(need, 30)` | `min(need, 100) × 0.6` | plafond 60 comparable à Socialize (60), **monotone** avec le besoin : l'urgence relative peut basculer l'arbitrage |
| Catalogue `eat.EnergyRecovery` | 0 | **2.0** | manger compense le coût métabolique du déplacement vers la nourriture |
| Catalogue `drink.EnergyRecovery` | 0 | **1.0** | idem, asymétrie eau/nourriture assumée |
| `moveEnergyCost`, `HungerRate`/`ThirstRate`, réserves | inchangés | inchangés | un seul axe d'arbitrage à la fois (isolation des causes) |

Fichiers porteurs : `SimulationProfiles.Reference()` et `configs/simulation/reference.json`
(`agents.actions.catalog.{eat,drink}.energyRecovery`), `UtilityEvaluator.BenefitOf`.

**Traçabilité** : `engineVersion` 0.12.0 → **0.13.0** (bump MINOR, DETERMINISM.md §7 —
altération volontaire de trajectoire). Les checksums dorés (perception 0x46769cfb11c8b3a7,
état complet 0xe62395429b50b7c1) sont **re-calés et assumés dans le même commit** que
cet ADR, conformément à la procédure §8 du plan.

## Critères d'acceptation de la re-campagne (3 × 1200 ticks, seeds 12345 / 424242 / 999)

1. zéro extinction ; population finale = population initiale (100/100) sur les 3 seeds ;
2. énergie moyenne **stable** sur les 200 derniers ticks (|pente| < 0,005/tick) ;
3. part des décisions Eat/Drink ≥ 15 % sur les ticks où la faim moyenne > 70 ;
4. déterminisme re-vérifié : deux runs de même seed → empreintes bit-à-bit identiques.

Si le critère 3 est atteint mais pas le 2 : itérer sur le seul levier
`EnergyRecovery` (levier isolé, cf. garde-fous du plan §10).

## Signaux de viabilité observés côté ECHOS (D2, observe-only)

Le rapport de calibration (`GET /api/runs/{id}/calibration`, schemaVersion 2) gagne :

- `outcome` / `extinctionTick` : extinction détectée au niveau ECHOS (premier tick
  où `alive_count = 0`), sans rétroaction sur le moteur ;
- bloc `viability` : `energySlopePerTick` (pente des 200 derniers ticks — détecte la
  mort lente même sans extinction), `actionSharesWhenHungry` (distribution des
  actions quand `mean_hunger > 70`), `resourceRegime` (min/moy/max des réserves).

Ces signaux **informent** la prochaine décision de calibration ; ils ne la déclenchent pas.

## Conséquences

- Les runs antérieurs restent analysables ; la comparaison inter-générations
  (0.12 vs 0.13) est de facto `is_reproducible: false` via `same_version` — attendu.
- Les budgets de performance de CI ne bougent pas (≥ 20/10 t/s, marge ~40×).
- Toute itération ultérieure de calibration exige un nouvel ADR et re-calage des
  goldens dans le même commit.
