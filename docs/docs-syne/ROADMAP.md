# ROADMAP.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `../ROADMAP.md` (racine)
**Source Monographie** : Annexe J (feuille de route V2), §3.24 (fondations V0.1)

---

## 1. Principes

- Road map **en ordre, sans dates** (décision utilisateur) ; l'ordre reflète les dépendances techniques.
- Alignée sur la feuille de route V2 de la Monographie (Annexe J), prolongée par les fondations V0.1.

## 2. Les phases techniques (ordre)

| # | Intitulé | Contenu | Livrables |
| :-- | :-- | :-- | :-- |
| 0 | Socle | Architecture & documentation | ADR |
| 1 | BDI + Perception | Boucle 10/15 étapes, perception partielle | boucle de simulation, grille spatiale — **livré** (jalon SYNE ph1, issues #7–#12) |
| 2 | Mémoire + Croyances | Mémoire long terme, révision croyances | mémoires + BeliefStore — **livré** (SYNE-013/014) |
| 3 | Décision + Utilité | UtilityEvaluator, objectifs dynamiques | formule d'utilité, DecisionRecord — **livré** (jalon SYNE ph3, issues #16–#19, engineVersion 0.2.0) |
| 4 | Actions | Actions déclaratives, pool d'actions | catalogue d'actions + exécuteur atomique — **livré** (jalon SYNE ph4, issues #20–#23, engineVersion 0.3.0) |
| 5 | Communication | 7 types, protocole, validation | protocole pulsations lumineuses — **livré** (jalon SYNE ph5, issues #24–#28, engineVersion 0.4.0, `CommunicationSystem` : diffusion + interception + relais + coûts + confiance, événements `message_sent`/`message_received`) |
| 6 | Groupes | Cohésion, leadership, décisions collectives | réseau social émergent — **livré** (jalon SYNE ph6, issues #29–#32, engineVersion 0.5.0, `GroupSystem` : cohésion confiance × affinité, LOD 10 ticks, leader par confiance entrante, décisions pondérées, turnover ; `BirthSystem` : fusion consentie, naissances post-boucle ; `SOCIAL_NETWORK.md`) |
| 7 | Ressources + Environnement | Saisons, régénération, obstacles | monde V2 |
| 7b | Fidélités V0.1 (resegmentation) | 4 fidélités monographie (mortalité, naissance consentie fidèle, décision collective → objectifs, cheminement A* déterministe) | **livré** (jalon SYNE ph7b, SYNE-074…077, U6 — audit `RAPPORT_ECART_DOC_IMPLEMENTATION.md`, issues dédiées `docs/docs-syne/ISSUES.md` ph7b, engineVersion 0.6.0) |
| 8 | Observabilité | Tests anti-triche, validations | événements typés complets — **SYNE-080 livré** (émetteur WebSocket `--observe`, snapshot + events camelCase) |
| 9 | Performance & Scalabilité | Benchmarks, optimisations, 500+ entités | grille spatiale, pooling, LOD |
| 10 | Tests & Couverture | 160+ tests, ≥ 80 % | suite complète |
| 11 | Persistance & Reprise | JSON → SQLite, migration | schéma SQLite 11 tables, reprise bit-à-bit |

(Ordre adapté de l'Annexe J.1)

## 3. Jalons de validation (rappel)

- **T0** : 50 ent., 1000 ticks, pas de crash.
- **T1** : 50 ent., 2000 ticks, croyances divergentes.
- **T2** : traits différents → décisions différentes.
- **T3** : information locale.
- **T4** : benchmarks (tableaux d'objectifs Annexe I.3).
- **T5** : 160+ tests, couverture ≥ 80 %.

## 4. Priorités V0.1 spécifiques

1. **Déterminisme** d'abord (PRNG, ordre causal) — fondation de tout le reste.
2. **Contrats de transport** (WorldSnapshot/ExternalEvent) tôt, pour que ECHOS et PRISM se calent dessus.
3. **Persistance & reprise** dès que possible (reprise bit-à-bit).
4. **Paramétrages** (« Entité A/B ») plutôt que classes rigides.

## 5. Liens

- Performance : `PERFORMANCE.md` (budgets, benchmarks).
- Tests : `TESTING.md` (jalons J.2).
- Déterminisme : `DETERMINISM.md`.

## 6. Décisions reportées (spécifications [OUVERT])

Conformément à la checklist §12, les choix ci-dessous restent **consciemment reportés** à la réalisation, sans bloquer l'implémentation :

| Emplacement | Sujet reporté | Où le trancher |
| :-- | :-- | :-- |
| `COMMUNICATION_PROTOCOL.md` | Latence des pulsations (vitesse de signal, §3.16.6) | Phase d'implémentation communication (paramètre configurable optionnel) |
| `SYSTEMS_SPEC.md` | Résolution chiffrée des conflits (ADR-009) | Implémentation `Interaction/` |
| `SYSTEMS_SPEC.md` | Modèle d'héritage / fusion consentie (décision n°17) | Implémentation cycle de vie |
| `SYSTEMS_SPEC.md` | Coûts des livres (décisions n°18/19) | Implémentation savoir tangible |
| `PERFORMANCE.md` | Machine de référence des benchmarks V0.1 | Phase 9 (performance) |
| `CONFIGURATION.md` | Calibration des taux de besoins / seuils | Phase de calibration |
| `CONFIGURATION.md` | « Instruire » (SYNE-042) : ingestion en mémoire épisodique via Eat | Jalon ph7 (sources spatiales, SYNE-070) |
| `DATA_MODEL.md` | Sources spatiales de ressources (décision n°4) | Jalon ph7 (SYNE-070) |

Ces points n'obligent aucune refonte documentaire : ils relèvent de paramétrages et de chiffrages internes.

---

## Points restés ouverts dans ce document
- L'ordre ci-dessus est une proposition issu de la Monographie ; toute inversion sera justifiée explicitement (décision en cours d'arbitrage).
- Aucune date ne sera posée avant consolidation des phases 0-2 du cadrage racine.