# ADR-014 : Baselines de calibration V0.1

**Composant** : SYNE / ECHOS
**Statut** : Accepted
**Date** : 24 septembre 2026
**Dépend de** : ADR-009, ADR-010, SYNE-120, SYNE-131

## Contexte

Les valeurs historiques du prototype sont configurables et ne doivent pas être présentées comme des constantes scientifiques. SYNE-120 doit rendre ces valeurs auditables à partir de runs valides, tandis que SYNE-131 établit une boucle de rapport reproductible sans altérer les simulations.

## Décision

Les valeurs effectives ci-dessous constituent les baselines V0.1 héritées, provisoires et modifiables via configuration. Elles restent distinctes des valeurs optimales, qui ne sont pas établies par les seuls tests logiciels.

| Paramètre | Baseline V0.1 | Source |
|---|---:|---|
| Besoins initiaux faim / soif / fatigue / énergie | 0 / 0 / 0 / 100 | `BodyNeeds` |
| Dérive faim / soif / fatigue par tick | +0,5 / +0,7 / +0,3 | décision 3 |
| Déclencheurs faim / soif / fatigue | 50 / 50 / 70 | `needs.*TriggerThreshold` |
| Réserves initiales nourriture / eau / bois / minéraux | 100 / 1000 / 50 / 0 | `SimulationOptions` |
| Régénération initiale nourriture / eau / bois / minéraux | 0 / 5 / 0,1 / 0 par tick | `SimulationOptions` |
| Écriture d'un livre / bénéfice de lecture | 20 énergie / 1 unité | `world.books.*`, livres désactivés par défaut |

Le rapport ECHOS post-run (`GET /api/runs/{id}/calibration`) agrège de façon déterministe les résumés de ticks, événements et métriques par moteur. Il est en lecture seule, horodatage exclu, et ne modifie ni configuration ni run. La revue humaine compare les rapports entre seeds/configurations avant toute décision de calibration.

Les coûts, matériaux et durées d'une construction agentique restent non implémentés et ouverts selon la décision 20 ; aucune baseline n'est inventée ici. L'effet cognitif des livres dépend du futur moteur mémoire et reste hors de ce jalon.

## Conséquences

- Les valeurs héritées sont explicites et testables, mais ne sont pas déclarées optimales.
- Une calibration fondée scientifiquement exige des jeux de runs documentés et une validation humaine.
- Le rapport ne lance pas automatiquement de nouvelle simulation et n'applique aucun réglage.
