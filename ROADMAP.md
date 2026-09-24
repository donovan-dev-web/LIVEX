# ROADMAP.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `VISION.md`, `VERSIONING.md`
**Source Monographie** : Annexe J (feuille de route V2 détaillée), Partie 9 (Conclusions)

---

## 1. Principe

La road map LIVEX est exprimée **en ordre, sans dates** : les phases s'enchaînent de manière obligatoire, mais leur calendrier dépend de la disponibilité et des validations intermédiaires. C'est la méthode retenue (décision utilisateur) pour préserver la rigueur scientifique du projet sans s'enfermer dans un planning irréaliste.

## 2. Les 6 phases cadres

| Phase | Intitulé | Objectif | Sortie |
| :-- | :-- | :-- | :-- |
| **0** | Socle & gouvernance | Réseau, dépôt, conventions, pipeline | `LICENSE`, `VERSIONING.md`, `GITFLOW.md`, `CI_CD.md`, gouvernance, templates |
| **1** | Cadrage général | Vision, architecture, contrats, glossaire | Documents racine (`ARCHITECTURE.md`, `COMMUNICATION.md`, ...) |
| **2** | Moteur (SYNE) | Cœur du monde simulé | `docs/docs-syne/*` |
| **3** | Observation (ECHOS) | Analyse et pilotage | `docs/docs-echos/*` |
| **4** | Représentation (PRISM) | Rendu et interaction | `docs/docs-prism/*` |
| **5** | Consolidation | Cohérence des contrats, FAQ, contribution | Relecture croisée, `FAQ.md`, `CONTRIBUTING.md`, checklist finale |

## 3. Alignement sur la feuille de route V2 (Annexe J)

La Monographie fournit une feuille de route V2 en 24 semaines (13 phases). Elle sert de **référence technique** pour le contenu des jalons, mais sa temporalité est indicative. Équivalence indicative :

| Phase Monographie (J.1) | Contenu | État LIVEX |
| :-- | :-- | :-- |
| 0 | Architecture & documentation | Contenu couvert par les **phases 0-1** |
| 1 | BDI + Perception | **Phase 2** (SYNE) — boucle 10/15 étapes |
| 2 | Mémoire + Croyances | Phase 2 |
| 3 | Décision + Utilité | Phase 2 |
| 4 | Actions | Phase 2 |
| 5 | Communication | Phase 2 |
| 6 | Groupes | Phase 2 |
| 7 | Ressources + Environnement | Phase 2 |
| 8 | Observabilité | Phase 2 (anti-triche, validations) |
| 9 | Performance & Scalabilité | Phase 2 (PERFORMANCE.md) |
| 10 | Tests & Couverture | Phase 2 (TESTING.md) |
| 11 | Analyzer V2 (7 moteurs) | **Phase 3** (ECHOS) |
| 12 | CI/CD & Déploiement | **Phase 0** (pipeline) + **Phase 5** (consolidation) |

## 4. Jalons de validation (Monographie J.2, adapté)

1. **T0 — Y3** : 50 entités, 1000 ticks, pas de crash.
2. **T1** : 50 entités, 2000 ticks, croyances divergentes (deux entités avec expériences différentes → croyances différentes).
3. **T2** : traits différents → décisions différentes (courage=0 vs courage=2 sur situation identique).
4. **T3** : information locale (message n'est reçu que dans le rayon).
5. **T4** : benchmarks — objectifs de ticks/s (cf. `docs/docs-syne/PERFORMANCE.md`).
6. **T5** : 160+ tests, couverture ≥ 80%.
7. **T6** : `docker compose up` démarre en < 30 s.

## 5. Long terme (vision)

Pistes futures (Monographie §5.15, Partie 9) :
- PRISM sur moteur graphique définitif (Unreal/Unity/autre).
- Mode joueur-habitant (incarner une entité).
- Représentation des constructions/territoires, saisons, météo.
- Intégration ECHOS dans la scène.

Ces pistes sont documentées dans les ROADMAP de chaque composant ; elles ne sont pas datées.

---

## 6. Backlog unifié SYNE + ECHOS (jalons croisés)

Section de **backlog consolidé** qui référence les issues des deux composants déjà réalisés et aligne la validation croisée (`docs/docs-syne/ISSUES.md`, `docs/docs-echos/ISSUES.md`). Elle sert de **base unique** pour la création des issues GitHub et du Kanban LIVEX (cf. `ISSUES.md` racine §5.1, `KANBAN.md` racine).

| Jalon unifié | Couvre (SYNE) | Couvre (ECHOS) | Issues de référence | Critère de validation croisée |
| :-- | :-- | :-- | :-- | :-- |
| **U0** — Socle & gouvernance | ph0 (`SYNE-001…006`) | ph0 (`ECHOS-001…005`) | `docs-syne/ISSUES.md` ph0, `docs-echos/ISSUES.md` ph0 | Architecture + ADR (SYNE ADR-001/002, ECHOS ADR-001/002) ; pipeline CI vert |
| **U1** — Boucle & perception | ph1 (`SYNE-010…015`) | ph1 (`ECHOS-010…016`) | ph1 SYNE, ph1 ECHOS | Boucle 10/15 étapes SYNE ; ingestion WebSocket ECHOS ; golden : T0/T1 |
| **U2** — Mémoire & croyances | ph2 (`SYNE-020…022`) | ph2 (`ECHOS-020…027`) | ph2 SYNE, ph2 ECHOS | Révision des croyances ; 7 moteurs de métriques ECHOS ; golden files |
| **U3** — Décision & utilité | ph3 (`SYNE-030…033`) | ph3 (`ECHOS-030…033`) | ph3 SYNE, ph3 ECHOS | Délibération BDI ; score d'émergence [0,1] ; T2 trait→décision |
| **U4** — Actions & communication | ph4–5 (`SYNE-040…054`) | ph4 (`ECHOS-040…045`) | ph4±ph5 SYNE, ph4 ECHOS | Périmètre des actions ; API REST ECHOS ≥ 80 % ; T3 information locale |
| **U5** — Groupes | ph6 (`SYNE-060…063`) | ph5 (`ECHOS-050…054`) | ph6 SYNE, ph5 ECHOS | Groupes BDI ; logging & instrumentation ; jalons J2/J3 |
| **U6** — Observabilité & fidélités | ph8 (`SYNE-080…082`) + ph7b (`SYNE-074…077`) | ph6 (`ECHOS-060…063`) | ph8+ph7b SYNE, ph6 ECHOS | Événements typés ; analyse causale ; 4 fidélités monographie (mortalité, naissance consentie fidèle, décision collective → objectifs, cheminement A* déterministe) |
| **U7** — Performance & comparaison | ph9–10 (`SYNE-090…102`) | ph7 (`ECHOS-070…072`) | **Jalon U7 complet — livré** — SYNE ph9–10 + ECHOS ph7 : budgets ≥ 30 %, cibles 50/500/1000 dépassées ~30–60×, 324 tests / couverture 96,19 %, baseline d'état SYNE `0x072a488aa18c05eb` ; ECHOS `/api/compare` + méta-métriques de reproductibilité `ReproducibilityScore = 1 − (CognitiveDiff+SocialDiff)/2`, exports CSV/JSON, couverture ECHOS 97,97 % | Budgets tick ; comparaison de runs (seed 12345) ; T4 benchmarks |
| **U8** — Tests & persistance (V0.1 → V1) | ph7 (`SYNE-070…073`) + ph11 (`SYNE-110…113`) + `SYNE-120…122` + `SYNE-130…131` | ph8 (`ECHOS-080…085`) + ph9 (`ECHOS-090…092`) | Jalon U8 — en cours (SYNE-110→113 + 070 + 071 livrés) | Environnement (ressources/obstacles/territoire, 070-073) : **070 livré** (minéraux + cycle régénération/dégradation, engineVersion 0.7.0) ; **071 livré** (constructions = obstacles statiques configurables + tracés, engineVersion 0.8.0) — 072/073 restants ; SQLite bit-à-bit + reprise (110-112) + contrôle HTTP :5181 (113) — **livrés** (3 PR, 342 tests, interop `ControlClient` ECHOS OK) ; calibration/tests/cross-cutting (120-122, 130-131) ; ECHOS : 080-085 livrés (UI, U7), 085 finalisé contre le réel :5181, suite de tests 090-092 + intégration SYNE↔ECHOS↔PRISM |

### Condition PRISM

- PRISM (feuille de route PRISM, `docs/docs-prism/ROADMAP.md`) n'est **créé qu'une fois SYNE + ECHOS réalisés** : son démarrage exige l'achèvement de **U0 → U8** (backlog unifié ci-dessus), sans lequel il n'aurait pas de contrat stable à rendre. Cette condition est reprise dans `docs/docs-prism/ROADMAP.md` §3 et `VERSIONING.md`.

---

## Points restés ouverts dans ce document
- Pas de dates : aucune jalon daté ne sera fixé avant consolidation des phase 2-4.
- L'ordre des phases Monographie V2 est conservé tel quel ; si une priorité émerge pendant la documentation (ex. un sous-système à détailler en premier), la route sera mise à jour explicitement.