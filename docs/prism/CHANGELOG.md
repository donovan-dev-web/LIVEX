# CHANGELOG — PRISM (documentation V2)

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 15 novembre 2026

Format : Keep a Changelog. Ce fichier retrace la **documentation** de la V2 ; l'historique de PRISM V1 (`1.0.0`) est archivé dans `legacy-unreal/CHANGELOG.md`.

## [Unreleased] — Documentation V2

### Added
- Spécification complète de PRISM V2 (monde voxel autoritaire) : vision, architecture, répartition des responsabilités, modèle de données, génération, boucle de simulation, contenu, systèmes, interface agent, contrat SYNE↔PRISM, déterminisme, persistance, configuration, performance, rendu, tests, packaging.
- ADR-004 à ADR-010 ; registre de questions ouvertes (Q-01 à Q-12).
- Feuille de route de bout en bout et backlog (`ROADMAP.md`, `ISSUES.md`).

### Changed
- **P10 — ECHOS branché sur PRISM (15 novembre 2026)** : le manifeste
  `prism/component.json` déclare `type: immersion`, `contributesTo: ["immersion"]`
  et la capacité `renderCadence` ; `PrismRequirements` ne renvoie plus aucune
  cause et le mode Immersion n'est plus verrouillé côté manifeste
  (LAUNCHER-200). Côté ECHOS, `POST /ingest/prism` est l'appelant de production
  de l'ingestion (reprise après coupure idempotente, schéma SQLite v9),
  `PrismResourceMetrics` et `PrismCooperationMetrics` projettent
  stocks/production/usure et les quatre indicateurs avec provenance à trois
  états, et `GET /api/prism/compare` nomme le premier tick de divergence par
  `contentHash` et `seed`. Suites ECHOS : **419 réussis, 14 ignorés**
  (couverture 91,14 %).
- **Relevé du 15 novembre 2026** : suite PRISM **487/487** (Architecture 8, Contracts 23, Viewer 62, Core 242, Content 51, Bots 23, Host 78) ; total projet **2 119 réussis, 20 ignorés** (PRISM, SYNE, ECHOS, Launcher, Installateur, `syne-mock`). Les compteurs 423/444/477 des documents antérieurs sont remplacés par ce relevé daté.
- **Passe de cohérence documentaire du 15 novembre 2026** : R3 est décrite comme produite et reproductible ; la mesure B9 est relue en erreur-type (total 5,15 %, ApplyWrites 5,49 %, perception 14,78 % non concluante) ; les identifiants ECHOS V2 sont cités sous `ECHOS-120…123` / `ECHOS-130` ; les liens vers [`../governance/IMPLEMENTATION-PLAN-2026-11-15.md`](../governance/IMPLEMENTATION-PLAN-2026-11-15.md) sont désormais valides.
- **Monde généré** : la côte est nivelée (`ShoreBand = 0`) et le générateur passe à `WorldGenVersion 2`. Les empreintes dorées de campagne restent épinglées en `WorldGenVersion 1` ; aucun hash de monde v2 n'est encore publié (G2).
- **Viewer** : état réel documenté — meshing glouton, client de lecture `/data/read`, cycle jour/nuit et silhouettes de démonstration en place ; atlas, entités live, suivi/coupe, inspecteur à sept calques, `--connect`/embarqué/rejeu, interventions journalisées, test d'indépendance du rendu et manifeste B8 restent ouverts.
- **Backlog** : les tickets de benchmark synthétique `PRISM-039/046/057`, le pathfinding hiérarchique non câblé `PRISM-053` et `MOCK-200` sont retirés ou réorientés ; `PRISM-103` est couverte par l'acceptation de boisson (`WellDrinkingAcceptanceTests`) et fermée ; `PRISM-140/141/143` et le viewer `PRISM-110–116/118` sont renvoyés à leurs lots du plan d'implémentation (L6 pour la performance, L9 pour le viewer) plutôt que traités dans la passe documentaire. Les tickets d'intégration ECHOS V2 passent de `ECHOS-100…103` à `ECHOS-120…123` pour ne pas collisionner avec les tickets de packaging déjà livrés.
- Alignement de la documentation P0–P11 sur le code : matrice contractuelle PRISM-078, quatre bots dont `idle`, cycle jour/nuit et client de lecture du viewer, support Linux, manifeste Launcher `prism-host`.
- Porte G5 franchie le 14 novembre 2026 : R3 (100 agents × 5 000 ticks) produite et reproductible (empreinte `855fdc2e1387e8d3`, deux passes identiques), coût propre à SYNE 30,5 ms/tick sous le budget de 50 ms. Rapport de porte [`G5-R3-REPORT.md`](G5-R3-REPORT.md) mis à jour en conséquence ; statuts des portes dans [`ROADMAP.md`](ROADMAP.md) et [`ISSUES.md`](ISSUES.md) mis à jour (G5 franchie, G1/G2/G6 restent ouvertes).
- ADR-002 et ADR-003 passent à `[Superseded]` (au moment de la fusion de la V2).
- Le contrat PRISM cesse d'être une consommation de `syne/API_CONTRACTS.md` 0.5.0.

### Removed
- Rien tant que la V2 n'est pas fusionnée ; l'archivage de la documentation V1 est un jalon (ISSUES `PRISM-0xx`, phase P13).
