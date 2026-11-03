# PRISM — Persistent Reality & Interactive Simulation Module (V2)

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION EN COURS — SOCLES P0–P11 ; P12/P13 À FAIRE ; G1–G8 EN ATTENTE]
**Version cible** : PRISM `2.0.0` — consomme et produit le contrat SYNE↔PRISM `1.0.0` (voir [`API_CONTRACTS.md`](API_CONTRACTS.md))
**Implémentation visée** : .NET (C#), noyau headless `Prism.Core` + hôte `Prism.Host` + visualiseur `Prism.Viewer` (Silk.NET / OpenGL)
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`VISION.md`](VISION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`RESPONSIBILITIES.md`](RESPONSIBILITIES.md)
**Source Monographie** : —

---

> **Lecture obligatoire avant tout.** Ce dossier décrit PRISM **V2**, une refonte de scope. Il remplace la documentation de PRISM V1 (projet Unreal + plugin PRISM-LDK, livré en `1.0.0`), archivée séparément. La simulation V2 est en cours d'implémentation ; consulter la roadmap et le backlog pour l'état réel des phases et les validations différées.

**État d'avancement :** les socles P0–P11 sont amorcés : noyau du monde (P2–P6), hôte headless, contrat et persistance (P7), structures et chantiers (P8), adaptateurs SYNE/ECHOS (P9–P10) et viewer (P11). La matrice contractuelle PRISM-078 couvre les 21 kinds sur les trois liaisons ; les bots, le cycle jour/nuit et le client de lecture du viewer sont en place. Les phases P12–P13 (calibrage, validation, livraison) restent à faire ; la validation Linux et les benchmarks sont reportés et G1–G8 ne sont pas franchies. Le suivi détaillé est dans [`ROADMAP.md`](ROADMAP.md) et [`ISSUES.md`](ISSUES.md).

## Rôle en une phrase

PRISM V2 est **l'autorité du monde** : un monde voxel borné, déterministe, piloté par des données, qui porte la matière, l'espace, le temps physique, la navigation et les structures. SYNE ne conserve que la **cognition des agents** ; ECHOS observe les deux.

## Ce qui change par rapport à V1

| Sujet | PRISM V1 (Unreal, `1.0.0`) | PRISM V2 (voxel, `2.0.0`) |
| :-- | :-- | :-- |
| Rôle | Rendu d'une représentation logique ; jamais co-auteur | Autorité de l'état du monde, de l'espace et de la matière |
| Sens du flux | SYNE → PRISM (lecture) + commandes HTTP de contrôle | SYNE ↔ PRISM bidirectionnel : commandes, observations, événements |
| Technologie | Unreal Engine 5.8, C++ mince + Blueprint | .NET, noyau sans dépendance graphique ; rendu Silk.NET OpenGL optionnel |
| Exécution | Nécessite le moteur de rendu | **Headless par défaut** ; le visualiseur est un client de lecture |
| Monde | Grille logique 2D + obstacles disques (dans SYNE) | Monde de colonnes 2.5D 256 × 256, régions horizontales 16², altitude et couches décrites par des données |
| Ressources | Réserves globales (`Food`, `Water`, `Wood`, `Mineral`) | Objets physiques, inventaires, conteneurs, chaînes de production |
| Échelle SYNE↔moteur | `k`, TPS, cellules World Partition (ADR-003) | Supprimée : 1 bloc = 1 m, aucune conversion |

## Documents

| Document | Rôle |
| :-- | :-- |
| [`VISION.md`](VISION.md) | Objectifs, principes, périmètre et hors-périmètre |
| [`PRISM_SYNTHESIS.md`](PRISM_SYNTHESIS.md) | Synthèse d'une page, pour lecteurs pressés et recruteurs |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Projets .NET, couches, threads, flux, dépendances |
| [`RESPONSIBILITIES.md`](RESPONSIBILITIES.md) | Répartition SYNE / PRISM / ECHOS / Launcher et plan d'extraction du monde de SYNE |
| [`DATA_MODEL.md`](DATA_MODEL.md) | Grille, chunks, blocs, objets, inventaires, agents physiques, structures |
| [`WORLD_GENERATION.md`](WORLD_GENERATION.md) | Génération par seed : bruit entier, couches, zones, filons |
| [`SIMULATION_LOOP.md`](SIMULATION_LOOP.md) | Tick, ordre d'exécution, mises à jour programmées/aléatoires, conflits, calendrier |
| [`CONTENT_SPEC.md`](CONTENT_SPEC.md) | Format des fichiers de données, contenu minimal, paramètres de calibrage |
| [`SYSTEMS_SPEC.md`](SYSTEMS_SPEC.md) | Chaînes de production, structures, chantiers, propriété, coopération |
| [`AGENT_INTERFACE.md`](AGENT_INTERFACE.md) | Impact sur les agents BDI : croyances, planification, macro-actions, savoir des recettes |
| [`API_CONTRACTS.md`](API_CONTRACTS.md) | Contrat d'interface SYNE↔PRISM et flux de lecture |
| [`DETERMINISM.md`](DETERMINISM.md) | Arithmétique entière, flux de hasard, empreintes, vérifications |
| [`PERSISTENCE.md`](PERSISTENCE.md) | Points de reprise, format des chunks, reprise bit-à-bit |
| [`CONFIGURATION.md`](CONFIGURATION.md) | Clés de configuration et profils |
| [`PERFORMANCE.md`](PERFORMANCE.md) | Budgets, cibles, benchmarks à écrire en premier |
| [`RENDERING_SPEC.md`](RENDERING_SPEC.md) | Visualiseur Silk.NET, identité visuelle, interaction |
| [`TESTING.md`](TESTING.md) | Plan de test et de calibrage, ablations, critères de viabilité |
| [`PACKAGING.md`](PACKAGING.md) | Publication, manifestes Launcher, installateur |
| [`ROADMAP.md`](ROADMAP.md) | Feuille de route de bout en bout (phases, jalons, portes de décision, risques) |
| [`ISSUES.md`](ISSUES.md) | Backlog complet prêt pour GitHub/Kanban |
| [`CHANGELOG.md`](CHANGELOG.md) | Historique de cette documentation |
| [`adr/`](adr/README.md) | Décisions d'architecture (ADR-004 à ADR-010 pour la V2) |

## Où lire quoi

- **Comprendre le pourquoi** : `VISION.md`, `PRISM_SYNTHESIS.md`, `adr/ADR-004`.
- **Implémenter le noyau** : `DATA_MODEL.md`, `SIMULATION_LOOP.md`, `DETERMINISM.md`, `CONTENT_SPEC.md`.
- **Implémenter le côté agent (SYNE)** : `API_CONTRACTS.md`, `AGENT_INTERFACE.md`, `RESPONSIBILITIES.md` §4.
- **Planifier** : `ROADMAP.md`, `ISSUES.md`, `PERFORMANCE.md`.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 3 novembre 2026 | Statut et état d'avancement alignés sur les socles P0–P11 | Audit de cohérence avec le code |
| 2 novembre 2026 | Début d'implémentation P0/G0 et ajout du suivi | PRISM V2 |
| 1er novembre 2026 | Création | Refonte de scope V2 (monde voxel autoritaire) |
