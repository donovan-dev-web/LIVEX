# ROADMAP.md

**Composant** : LIVEX (général) 
**Statut** : [ROADMAP TRANSVERSE — SOCLE V1 LIVRÉ ; INTÉGRATION PRISM V2 EN COURS]
**Dernière mise à jour** : 4 novembre 2026
**Dépend de** : `../presentation/VISION.md`, `../../VERSIONING.md` 
**Source Monographie** : Annexe J (feuille de route V2 détaillée), Partie 9 (Conclusions) 

---

## 1. Principe

La road map LIVEX est exprimée **en ordre, sans dates** : les phases s'enchaînent de manière obligatoire, mais leur calendrier dépend de la disponibilité et des validations intermédiaires. C'est la méthode retenue (décision utilisateur) pour préserver la rigueur scientifique du projet sans s'enfermer dans un planning irréaliste.

## 2. Phases cadres du projet

| Phase | Intitulé | Objectif | Sortie |
| :-- | :-- | :-- | :-- |
| **0** | Socle & gouvernance | Réseau, dépôt, conventions, pipeline | `LICENSE`, `../../VERSIONING.md`, `GITFLOW.md`, `../CI_CD.md`, gouvernance, templates |
| **1** | Cadrage général | Vision, architecture, contrats, glossaire | Documents racine (`../ARCHITECTURE.md`, `../COMMUNICATION.md`, ...) |
| **2** | Moteur (SYNE) | Cœur du monde simulé | `../syne/*` |
| **3** | Observation (ECHOS) | Analyse et pilotage | `../echos/*` |
| **4** | Présentation PRISM V1 | Projet Unreal et plugin PRISM-LDK (`PrismLdk`), livrés avec LIVEX 1.0.0 | Historique V1 ; ne décrit pas PRISM V2 |
| **5** | Consolidation | Cohérence des contrats, FAQ, contribution | Relecture croisée, `../presentation/FAQ.md`, `../../CONTRIBUTING.md`, checklist finale |
| **6** | Refonte et intégration PRISM V2 | Monde 2,5D autoritaire, host .NET, chaînes de contenu, contrat SYNE↔PRISM et ingestion ECHOS | `../prism/ROADMAP.md` ; validation et release restent ouvertes |

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
5. **T4** : benchmarks — objectifs de ticks/s (cf. `../syne/PERFORMANCE.md`).
6. **T5** : 160+ tests, couverture ≥ 80%.
7. ~~**T6** : `docker compose up` démarre en < 30 s.~~ **Retiré du périmètre LIVEX (08/10/2026)** : plus de Docker dans le projet — l'installation et l'orchestration sont portées par le Launcher (artefacts natifs, `--check`), arbitrage A1.

## 5. Long terme (vision)

Travaux de V2 encore ouverts :
- Extraire complètement l'autorité du monde physique de l'ancien moteur SYNE.
- Réaliser la reprise couplée SYNE/PRISM et le run R2 de 5 000 ticks reproductible.
- Raccorder l'ingestion du flux PRISM au cycle de run ECHOS et aux métriques prévues.
- Achever le mode viewer live, les validations interplateformes, les calibrages et le packaging V2.
- Mode joueur-habitant (incarner une entité).

Ces pistes sont documentées dans les ROADMAP de chaque composant ; elles ne sont pas datées.

## 6. Backlog unifié SYNE + ECHOS (jalons croisés)

Section de **backlog consolidé** qui référence les issues des deux composants déjà réalisés et aligne la validation croisée (`../syne/ISSUES.md`, `../echos/ISSUES.md`). Elle sert de **base unique** pour la création des issues GitHub et du Kanban LIVEX (cf. `ISSUES.md`, `KANBAN.md`).

| Jalon unifié | Couvre (SYNE) | Couvre (ECHOS) | Issues de référence | Critère de validation croisée |
| :-- | :-- | :-- | :-- | :-- |
| **U0** — Socle & gouvernance | ph0 (`SYNE-001…006`) | ph0 (`ECHOS-001…005`) | `../syne/ISSUES.md` ph0, `../echos/ISSUES.md` ph0 | Architecture + ADR (SYNE ADR-001/002, ECHOS ADR-001/002) ; pipeline CI vert |
| **U1** — Boucle & perception | ph1 (`SYNE-010…015`) | ph1 (`ECHOS-010…016`) | ph1 SYNE, ph1 ECHOS | Boucle 10/15 étapes SYNE ; ingestion WebSocket ECHOS ; golden : T0/T1 |
| **U2** — Mémoire & croyances | ph2 (`SYNE-020…022`) | ph2 (`ECHOS-020…027`) | ph2 SYNE, ph2 ECHOS | Révision des croyances ; 7 moteurs de métriques ECHOS ; golden files |
| **U3** — Décision & utilité | ph3 (`SYNE-030…033`) | ph3 (`ECHOS-030…033`) | ph3 SYNE, ph3 ECHOS | Délibération BDI ; score d'émergence [0,1] ; T2 trait→décision |
| **U4** — Actions & communication | ph4–5 (`SYNE-040…054`) | ph4 (`ECHOS-040…045`) | ph4±ph5 SYNE, ph4 ECHOS | Périmètre des actions ; API REST ECHOS ≥ 80 % ; T3 information locale |
| **U5** — Groupes | ph6 (`SYNE-060…063`) | ph5 (`ECHOS-050…054`) | ph6 SYNE, ph5 ECHOS | Groupes BDI ; logging & instrumentation ; jalons J2/J3 |
| **U6** — Observabilité & fidélités | ph8 (`SYNE-080…082`) + ph7b (`SYNE-074…077`) | ph6 (`ECHOS-060…063`) | ph8+ph7b SYNE, ph6 ECHOS | Événements typés ; analyse causale ; 4 fidélités monographie (mortalité, naissance consentie fidèle, décision collective → objectifs, cheminement A* déterministe) |
| **U7** — Performance & comparaison | ph9–10 (`SYNE-090…102`) | ph7 (`ECHOS-070…072`) | **Implémentation avancée — validation produit partielle** — budgets et benchmarks du moteur présents ; ECHOS `/api/compare`, reproductibilité et exports présents. La cadence contrôlée, le backpressure, le lag et le parcours UI ne sont pas encore des preuves de release. | Budgets tick ; comparaison de runs (seed 12345) ; T4 benchmarks ; mesure pipeline à ajouter |
| **U8** — Tests & persistance (V0.1 → V1) | ph7 (`SYNE-070…073`) + ph11 (`SYNE-110…113`) + `SYNE-120…122` + `SYNE-130…131` | ph9 (`ECHOS-090…092`) — ph8 (Écrans web) retiré par ADR-007 | **Implémentation avancée — non accepté comme jalon transverse** — persistance, contrôle :5181 et intégration réelle multi-ticks présents ; la stabilité long-run, la reprise worker et le parcours UI complet restent à prouver. | Persistance bit-à-bit, contrôle, intégration SYNE→ECHOS, métriques REST ; test long-run et preuve de fraîcheur à ajouter |

### Intégration PRISM V2 (état au 4 novembre 2026)

- PRISM V1/PRISM-LDK est la présentation Unreal historique de LIVEX 1.0.0. PRISM V2 est une solution .NET qui porte l'état du monde physique. Le host, le contrat `1.0.0`, les chaînes d'actions et les bots sont partiellement implémentés ; le relevé du 4 novembre compte 403 tests PRISM verts.
- SYNE et PRISM échangent des commandes et observations dans une boucle couplée ; l'adaptateur et l'application des observations aux croyances sont livrés et le transport a été exercé sur 1 000 ticks. Le run R2 de 5 000 ticks, ses répétitions identiques et la reprise partagée restent ouverts.
- ECHOS valide et persiste les enveloppes du flux de lecture PRISM ; l'ingestion au cycle de run et les métriques associées ne sont pas encore raccordées. Le viewer possède un client de lecture testé mais son lancement live reste à câbler.
- Le mock Node.js demeure un outil de compatibilité V1 ; `Prism.Bots` sert aux campagnes V2 et ne remplace pas la validation couplée contre SYNE.

## 7. Dette de conformité de distribution

| Tâche | Portée | Critère de clôture |
| :-- | :-- | :-- |
| **Notices des dépendances transitives** | Installeur Launcher, paquets NuGet / PyPI / npm | Générer les notices de licence de l'ensemble des dépendances transitives (et non des seules dépendances directes listées dans `THIRD-PARTY-NOTICES.md`), les joindre à chaque artefact distribué et vérifier leur présence dans l'installeur avant toute publication binaire. |

---

## Points restés ouverts dans ce document

- Pas de dates : aucune jalon daté ne sera fixé avant consolidation des phase 2-4.
- L'ordre des phases Monographie V2 est conservé tel quel ; si une priorité émerge pendant la documentation (ex. un sous-système à détailler en premier), la route sera mise à jour explicitement.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 19 octobre 2026 | Création | Documentation de la roadmap projet LIVEX |
| 21 octobre 2026 | Mise à jour | Alignment with new specification base |
