# ISSUES — Backlog PRISM V2

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`ROADMAP.md`](ROADMAP.md), `../governance/ISSUES.md`, `../governance/KANBAN.md`
**Source Monographie** : Annexe K

---

## 1. Objectif

Backlog complet du composant PRISM V2, **source unique de création des cartes Kanban**. Chaque issue : ID, titre, labels, priorité (P0 bloquant, P1 important, P2 souhaitable), dépendances, critère d'acceptation. Les milestones suivent les phases de `ROADMAP.md`. Les issues des autres composants sont listées en §4 (identifiants réservés).

## 2. Règles

- PRISM ne décide pas du comportement des agents ; aucune issue ne l'y autorise.
- Toute issue qui modifie un monde généré ou une règle met à jour `worldGenVersion`/`engineVersion` et les empreintes dorées dans la même PR.
- Toute issue touchant un contrat suit `API_CONTRACTS.md` §8.

## 3. Backlog PRISM

### Milestone ph0 — Cadrage (G0)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-001 | Relire et valider la documentation V2 | docs | P0 | — | Revue croisée ; incohérences tracées ; ADR-004…010 `Proposed` |
| PRISM-002 | Créer la solution `prism/` (projets vides, `Directory.Build.props`, `.editorconfig`) | core, ci | P0 | 001 | Build vert Windows et Linux |
| PRISM-003 | CI `prism-build` et `prism-test` + analyseur d'architecture | ci | P0 | 002 | La CI échoue si `Core` référence GL, `float`, `double`, `DateTime.Now`, `Random` |
| PRISM-004 | Stratégie de branches V2 (`feature/prism-v2`, `release/2.0.0`) | docs, ci | P1 | 001 | Documentée dans `GITFLOW.md` |
| PRISM-005 | Glossaire : chunk, bloc, tag, pack, macro-action, tick, chantier | docs | P2 | 001 | Entrées ajoutées à `presentation/GLOSSARY.md` |

### Milestone ph1 — Spikes et benchmarks (G1)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-010 | Prototype jetable : grille + générateur minimal | perf | P0 | 002 | Monde 256×256×64 généré ; code marqué jetable |
| PRISM-011 | B1 — pathfinding 1 000 agents | perf | P0 | 010 | Rapport : ms/tick, taux d'échec, mémoire |
| PRISM-012 | B2 — perception trois niveaux, 1 000 agents | perf | P0 | 010 | Rapport : ms/tick, octets/observation |
| PRISM-013 | B3 — croissance : programmée vs aléatoire, monde cultivé | perf | P0 | 010 | Rapport comparatif ; recommandation Q-09 |
| PRISM-014 | B4 — débit de commandes, JSON vs binaire, pas de verrouillage | perf, contract | P0 | 010 | Rapport ; recommandation Q-01 |
| PRISM-015 | B5 — diffusion de chunks, Deflate vs Brotli | perf | P1 | 010 | Rapport : octets, ms |
| PRISM-016 | Spike Silk.NET : fenêtre, un chunk maillé, Windows + Linux | viewer | P0 | 002 | Capture sur les deux OS |
| PRISM-017 | B8 — meshing et dessin d'un monde complet | perf, viewer | P1 | 016 | Rapport : ms/chunk, images/s |
| PRISM-018 | Rapport de porte G1 et décisions (3D vs 2.5D, Q-01) | docs | P0 | 011–017, SYNE-200 | Rapport versionné ; ADR-005/008 mis à jour |

### Milestone ph2 — Noyau du monde

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-020 | Registres d'identifiants stables (blocs, objets, tags, recettes) | core | P0 | 018 | Round-trip clé ↔ identifiant ; stabilité intra-run |
| PRISM-021 | Compilateur et validateur de contenu (schémas JSON, graphe de recettes) | content | P0 | 020 | Erreurs structurées complètes ; pack invalide = démarrage refusé |
| PRISM-022 | Pack de contenu minimal v0 (`CONTENT_SPEC.md` §5) | content | P0 | 021 | Valide ; graphe amorçable à mains nues (test) |
| PRISM-023 | Grille et chunks (tableaux SoA, chunks uniformes, *dirty flags*) | core | P0 | 020 | Accès, bornes, mémoire ≈ 12,6 Mo dense |
| PRISM-024 | `Mix64`, flux nommés, `xoshiro256**`, hasard à compteur | core | P0 | 002 | Vecteurs de test figés ; indépendance des flux |
| PRISM-025 | Hachage FNV-1a chunk/monde, empreintes canoniques | core | P0 | 023 | Empreinte stable ; ordre canonique |
| PRISM-026 | Roue temporelle (événements programmés) | core | P0 | 023 | Ordre d'exécution déterministe, millions d'événements |
| PRISM-027 | `MatterLedger` et audit de conservation | core | P0 | 020 | Audit configurable ; échec du test si déséquilibre |
| PRISM-028 | Harnais de tests par propriétés (idempotence, ordre) | ci | P1 | 023 | Générateurs de commandes ; exécuté en CI |

### Milestone ph3 — Génération et visualiseur de debug (G2)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-030 | Bruit entier 16.16 (treillis, octaves, déformation de domaine) | core | P0 | 024 | Valeurs identiques Windows/Linux |
| PRISM-031 | Relief et bordure en falaise | core | P0 | 030 | Hauteur dans les bornes ; bordure continue |
| PRISM-032 | Zones (plaine, forêt, roche, bord d'eau) et strates | core | P0 | 031 | Quatre zones présentes ; strates cohérentes |
| PRISM-033 | Filons de charbon et de fer | core | P0 | 032 | Grappes ; fer accessible par falaises |
| PRISM-034 | Eau : nappes et lacs (blocs sources statiques) | core | P0 | 032 | Pas d'eau flottante ; rives marchables |
| PRISM-035 | Forêts par gabarits d'arbres | core | P1 | 032 | Densité par zone ; arbres valides |
| PRISM-036 | Sites d'apparition, contrôles de jouabilité, tentatives dérivées | core | P0 | 033–035 | Accessibilité des 6 catégories ; échec > 8 tentatives = erreur structurée |
| PRISM-037 | Mondes dorés (3 graines) et empreintes inter-plateformes | ci | P0 | 036 | Job CI Windows = Linux |
| PRISM-038 | Visualiseur de debug v0 (fenêtre, meshing naïf, caméra libre) | viewer | P1 | 016, 023 | Affiche un monde généré ; réglage des paramètres |
| PRISM-039 | B6 — génération sur code de production | perf | P1 | 036 | Rapport ; parallélisme par chunk |

### Milestone ph4 — Temps et mises à jour

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-040 | Calendrier (jour, heure, saison) et événements | core | P0 | 026 | `day_changed`, `season_changed` |
| PRISM-041 | Mises à jour programmées avec gigue | core | P0 | 026, 024 | Résultat indépendant du parallélisme |
| PRISM-042 | Mises à jour aléatoires à compteur, parallèles par chunk | core | P0 | 024 | 1, 2, 8 threads → mêmes empreintes |
| PRISM-043 | Croissance du blé, conditions, arrêt, flétrissement | content | P0 | 041 | Stades 0→3 ; saisons ; flétrissement |
| PRISM-044 | Péremption paresseuse et expiration | core | P0 | 026 | Fraîcheur par conteneur ; événement de perte |
| PRISM-045 | Propagation d'herbe (aléatoire) | content | P2 | 042 | Paramétrée ; déterministe |
| PRISM-046 | B3 sur code de production | perf | P1 | 043 | Budget respecté sur monde cultivé |

### Milestone ph5 — Corps, navigation, perception

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-050 | Agent physique : corps 1×1×2, inventaire, `Incarner`/`Retirer` | core | P0 | 023 | Inventaire déposé au sol au retrait |
| PRISM-051 | Graphe de mouvement (montée 1, descente ≤ 3, liquides infranchissables) | core | P0 | 050 | Tests de cas limites |
| PRISM-052 | A* 3D entier borné + cache de chemins | core | P0 | 051 | Optimal ; déterministe ; budget de nœuds |
| PRISM-053 | Pathfinding hiérarchique (portails de chunks) | perf | P1 | 052 | B1 atteint la cible |
| PRISM-054 | Perception à trois niveaux, blocs exposés, différences | core | P0 | 023, 050 | Pas de « rayons X » ; plafond par agent |
| PRISM-055 | Arbitrage des conflits (clés `Mix64`) | core | P0 | 024 | Équité sur grand nombre de ticks |
| PRISM-056 | Chemins aménagés (accélération) | content | P2 | 052 | Coût divisé par `pathSpeedupFactor` |
| PRISM-057 | B1 et B2 sur code de production | perf | P1 | 053, 054 | Cibles de `PERFORMANCE.md` |

### Milestone ph6 — Actions, macro-actions, conservation

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-060 | `Miner` (dureté, palier, maîtrise, drops probabilistes) | core | P0 | 050, 027 | Échecs motivés ; ledger équilibré |
| PRISM-061 | `Poser` | core | P0 | 060 | Refus si agent dans la case |
| PRISM-062 | Conteneurs, `Prendre`/`Deposer`, listes d'accès | core | P0 | 050 | `access_denied` observable |
| PRISM-063 | `Fabriquer` (stations, entrées par tags, durée, maîtrise) | core | P0 | 062 | Station occupée = échec motivé |
| PRISM-064 | `Planter`, `Recolter`, labourage | core | P0 | 043, 060 | Cycle graine → culture → graines |
| PRISM-065 | `Consommer`, boire à la source, événement `item_consumed` avec effets | core | P0 | 062 | Effets transmis à SYNE |
| PRISM-066 | Durabilité des outils | core | P1 | 060 | Outil détruit à zéro (`wear`) |
| PRISM-067 | Macro-actions (`ObtenirObjet`, `Fabriquer`, `Cultiver`) avec indices de croyance | core | P0 | 060–065 | `source_not_found` si rien de perçu ; pas de recherche globale |
| PRISM-068 | `Transferer` atomique | core | P1 | 062 | Tout ou rien |
| PRISM-069 | Fuzz de commandes et audit de conservation global | ci | P0 | 067 | 1 M de commandes aléatoires sans violation |

### Milestone ph7 — Contrat, hôte, persistance (G3)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-070 | `Prism.Contracts` : types, schémas JSON, versionnement | contract | P0 | 067 | Schémas publiés dans `schemas/` |
| PRISM-071 | Idempotence et fenêtre de `commandId` | contract | P0 | 070 | Rejeu = même résultat, sans doublon |
| PRISM-072 | Boucle de tick à ordre fixe + `IWorldHost.Step` | core | P0 | 070 | Ordre de `SIMULATION_LOOP.md` §2 vérifié |
| PRISM-073 | Hôte headless et plan de contrôle HTTP, `/health/ready` | host | P0 | 072 | Cycle de vie de `ARCHITECTURE.md` §5 |
| PRISM-074 | Liaison WebSocket en pas de verrouillage | contract | P0 | 072 | Mêmes tests de contrat que la liaison embarquée |
| PRISM-075 | Flux de lecture (snapshot, delta, entités, métriques, empreinte) | contract | P0 | 072 | Reconstruction exacte du monde côté client |
| PRISM-076 | Persistance : points de reprise et reprise bit-à-bit | host | P0 | 072 | Restauré = continu (empreintes) |
| PRISM-077 | `Prism.Bots` (aléatoire, glouton, planificateur) et runs dorés | ci | P0 | 072 | 3 graines × 5 000 ticks, empreintes figées |
| PRISM-078 | Tests de contrat sur les deux liaisons | ci | P0 | 074 | Versions inconnues, champs inconnus, échecs motivés |
| PRISM-079 | Preuve de déterminisme : rejeu, parallélisme, Windows/Linux | ci | P0 | 077 | Job CI vert |
| PRISM-080 | Manifeste Launcher (`component.json`) et validation | host | P0 | 073 | `validate_manifests.py` vert |
| PRISM-081 | Gel du contrat `1.0.0` et rapport de porte G3 | docs | P0 | 078–082, SYNE-210 | Rapport ; contrat étiqueté |
| PRISM-082 | Run R2 : cognition SYNE actuelle contre PRISM | ci | P0 | SYNE-210 | Reproductible ; aucun crash sur 5 000 ticks |

### Milestone ph8 — Contenu, structures, chantiers (G4)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-090 | Gabarits et validation de placement | content | P0 | 081 | Terrain inadapté = échec motivé |
| PRISM-091 | Chantiers : états, perception, pose progressive, abandon | core | P0 | 090 | Plusieurs contributeurs sans messages dirigés |
| PRISM-092 | Structures : effets, intégrité paresseuse | core | P0 | 091 | Effet cesse sous le seuil |
| PRISM-093 | Accès et propriété, `DefinirAcces` | core | P0 | 062, 092 | Refus et vols observables |
| PRISM-094 | Chaîne Outils complète (paliers 0→2) | content | P0 | 063 | Bot : mains nues → pioche de fer |
| PRISM-095 | Chaîne Agriculture complète | content | P0 | 064 | Bot : graine → récolte → nouvelles graines |
| PRISM-096 | Chaîne Eau (seau, transport, puits) | content | P0 | 092 | Bot : puits fonctionnel |
| PRISM-097 | Chaîne Transformation (cuisson) | content | P1 | 063 | Gain de valeur et de conservation mesuré |
| PRISM-098 | Chaîne Stockage (coffre, grenier) | content | P1 | 092 | Péremption ralentie |
| PRISM-099 | Maîtrise par catégorie | core | P1 | 063 | Durée réduite, plafonnée |
| PRISM-100 | Usure des structures (optionnelle, désactivée par défaut) | core | P2 | 092 | Activable sans autre effet |
| PRISM-101 | Leviers d'ablation `mechanisms.*` | core | P0 | 094–099 | Chaque levier testé isolément |
| PRISM-102 | Run R1 (bots planificateurs, 50 agents) et rapport G4 | ci | P0 | 094–101 | 5 chaînes jouées ; audit vert |

### Milestone ph11 — Visualiseur complet

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-110 | Meshing glouton incrémental | viewer | P0 | 038, 075 | Remaillage des chunks *dirty* ; budget par image |
| PRISM-111 | Atlas procédural, occlusion ambiante, jour/nuit | viewer | P1 | 110 | Cycle synchronisé au calendrier |
| PRISM-112 | Agents, actions, chantiers fantômes | viewer | P1 | 110 | Plan visible ; interpolation visuelle seule |
| PRISM-113 | Caméras (orbite, libre, suivi, coupe) | viewer | P1 | 110 | — |
| PRISM-114 | Inspecteur et calques | viewer | P1 | 112 | Les 7 calques de `RENDERING_SPEC.md` §5 |
| PRISM-115 | Modes client / embarqué / rejeu | viewer | P1 | 075 | Saut de deltas sans incohérence |
| PRISM-116 | Interventions journalisées (désactivées par défaut) | viewer, contract | P2 | 115 | Rejouables ; run marqué |
| PRISM-117 | Test d'indépendance du rendu | ci | P0 | 115 | Empreintes identiques avec/sans viewer |
| PRISM-118 | Manifeste du viewer et B8 final | viewer | P1 | 110 | 60 images/s sur GPU intégré récent |

### Milestone ph12 — Calibrage et validation (G7)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-120 | Calibrage : extractions, transport, croissance, eau | calibration | P0 | G6 | ADR de calibrage ; V1/V2 atteints |
| PRISM-121 | Calibrage : péremption, coûts de structures, maîtrise | calibration | P0 | 120 | ADR ; V3/V4 atteints |
| PRISM-122 | Runs R3 et R4 (100 et 1 000 agents) | perf | P0 | 121 | Budgets de `PERFORMANCE.md` §2 |
| PRISM-123 | Ablations A1…A8 (≥ 5 graines, 10 000 ticks) | experiment | P0 | 121, ECHOS-110 | Données et analyses archivées |
| PRISM-124 | Rapport expérimental pérenne | docs | P0 | 123 | Publié, résultat positif ou négatif |
| PRISM-125 | Figer les empreintes dorées | ci | P0 | 124 | Mises à jour dans la même PR |

### Milestone ph13 — Candidate de livraison (G8)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-130 | Régression de performance et couverture ≥ 80 % | ci | P0 | 125 | Seuils tenus |
| PRISM-131 | Notices tierces (y compris transitives) | docs | P0 | 130 | `THIRD-PARTY-NOTICES.md` complet |
| PRISM-132 | Archiver la documentation V1 en `legacy-unreal/` ; ADR-002/003 `Superseded` ; ADR-004…010 `Accepted` | docs | P0 | 124 | Liens corrigés |
| PRISM-133 | Mise à jour `README`, `VERSIONING`, `governance/*`, FAQ, site, monographie | docs | P1 | 132 | Cohérence croisée |
| PRISM-134 | Publication autonome Windows/Linux (`prism-host`, `prism-viewer`, `prism-content`) | ci | P0 | 130 | Installation sur machine propre |
| PRISM-135 | Tags `prism-v2.0.0`, `livex-v2.0.0` et notes de release | docs | P0 | 134 | Release publiée |

## 4. Issues transverses (identifiants réservés)

| ID | Composant | Sujet | Phase |
| :-- | :-- | :-- | :-- |
| SYNE-200 | SYNE | Spike planificateur GOAP/HTN (B7) | P1 |
| SYNE-210 | SYNE | Adaptateur `PrismClient` minimal (marcher, manger, boire) | P7 |
| SYNE-220 | SYNE | Extraction du monde en 3 temps (parallèle → bascule → suppression) | P9 |
| SYNE-221 | SYNE | Mémoire spatiale par chunk (masque de tags, confiance) | P9 |
| SYNE-222 | SYNE | Croyances recettes/structures/chantiers ; désirs instrumentaux ; sécurité alimentaire | P9 |
| SYNE-223 | SYNE | Planificateur intégré, cache, replan sur échec | P9 |
| SYNE-224 | SYNE | Savoir des recettes : simple puis riche (livres) | P9 |
| SYNE-225 | SYNE | Persistance : tables du monde retirées, jeu de points de reprise couplé | P9 |
| SYNE-226 | SYNE | Documentation SYNE (DATA_MODEL, SYSTEMS_SPEC, API_CONTRACTS, DETERMINISM, PERSISTENCE, CONFIGURATION) | P9 |
| SYNE-227 | SYNE | Recalibrage de survie (remplace ADR-014/015/016) | P12 |
| SYNE-228 | SYNE | Version `0.18.0`, contrat d'observabilité `0.6.0` | P9 |
| ECHOS-100 | ECHOS | Ingestion du flux de lecture PRISM | P10 |
| ECHOS-101 | ECHOS | Refonte du moteur 6 (stocks, production, usure) | P10 |
| ECHOS-102 | ECHOS | Indicateurs : coopération, spécialisation, infrastructures, inégalité de stocks | P10 |
| ECHOS-103 | ECHOS | Comparaison de runs avec `contentHash` et premier tick de divergence | P10 |
| ECHOS-110 | ECHOS | Pipeline d'ablation (profils, ≥ 5 graines, statistiques) | P12 |
| LAUNCHER-200 | Launcher | PRISM requis par session ; exigences §11 réécrites ; ADR-006 révisé | P10 |
| LAUNCHER-201 | Launcher | `.livexp` : configuration PRISM et `contentHash` | P10 |
| LAUNCHER-202 | Launcher | Ports et orchestration SYNE + PRISM + ECHOS ; arrêt propre | P10 |
| INSTALLER-200 | Installateur | Paquets .NET autonomes ; options hôte/viewer/packs ; caducité de l'ADR-010 | P10 |
| GOV-200 | Gouvernance | Phase 6 de `governance/ROADMAP.md`, `VERSIONING`, `LIVEX-STATUS` | P0 / P13 |
| MOCK-200 | syne-mock | Retrait ou réorientation | P10 |

## 5. Cartographie portes ↔ issues

G0 : 001–005 · G1 : 010–018 · G2 : 020–039 · G3 : 040–082 · G4 : 090–102 · G5 : SYNE-220…228 · G6 : ECHOS-100…103, LAUNCHER-200…202, INSTALLER-200 · G7 : 120–125 · G8 : 130–135.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
