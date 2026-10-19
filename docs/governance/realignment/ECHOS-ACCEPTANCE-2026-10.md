# ECHOS — acceptation de la phase 5 (octobre 2026)

**Date du relevé :** 17 octobre 2026 · **Volet :** BL-24 · **Portée :** contrat d’ingestion batch et streaming, états des données, provenance.

## Objectif et résultat

La vérification ciblée a porté sur la distinction de quatre états : zéro mesuré, valeur non mesurée, mesure absente selon la cadence, et tick perdu (trou). La logique d’ingestion existante tenait le contrat; la lacune était la couverture conjointe des voies batch et streaming. Les tests contractuels ont été ajoutés sans modification de production ECHOS.

## Contrat vérifié

Le même flux de ticks observés 1, 2 et 4 (tick 3 manquant) a été rejoué via WebSocket et fichier `stream.jsonl`/batch :

| État | Interprétation | Représentation vérifiée |
|---|---|---|
| Zéro mesuré | Observation réelle d’une valeur nulle | `BeliefDiversity = 0.0`, `measured = true` |
| Non mesuré | Valeur neutre, pas une mesure | `BeliefDiversityNorm = 0.0`, `measured = false` |
| Absent | Tick ingéré, métrique non planifiée à cette cadence | Valeur absente de l’axe de mesure; cadence publiée (`analysisEvery`) |
| Trou | Tick non reçu | `missing_ticks = [3]`, `gaps_detected = 1`; la complétude indique les ticks effectivement perdus |

La provenance par tick accompagne les séries. Les deux voies produisent les mêmes données canoniques; le point d’entrée public batch est également couvert. L’ingestion ne contient aucun canal de contrôle vers SYNE; le pilotage reste sur l’API de contrôle distincte.

## Résultats et limites

Le relevé du 17 octobre indique 11 tests contractuels dédiés; suite ECHOS **372 réussis, 14 ignorés**, couverture **91,09 %**, exit 0. Les portes réelles alors lancées ont donné : SYNE réel **385 réussis, 1 ignoré**; mock SYNE **373 réussis, 13 ignorés**, exit 0.

Ces nombres datent de cette campagne; ils ne sont pas une promesse des résultats d’une exécution future. ECHOS peut signaler les trous, mais ne reconstitue pas les trames perdues ni leur contenu. Les tests permanents et contrats courants sont documentés dans `docs/echos/TESTING.md`, `docs/echos/API_REST.md` et les tests d’ingestion du composant.
