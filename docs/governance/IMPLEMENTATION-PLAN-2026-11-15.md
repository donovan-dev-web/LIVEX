# LIVEX — plan d'implémentation du 15 novembre 2026

**Composants concernés** : PRISM, SYNE, ECHOS, Launcher, Installateur, Viewer
**Statut** : [PLAN ACTIF — IMPLÉMENTATION PRIORITAIRE ; RAPPORTS, CALIBRAGE ET ABLATIONS REPORTÉS]
**Date** : 15 novembre 2026
**Portée** : ce plan ordonne **ce qui reste à implémenter** pour que la pile V2 soit complète. Il ne planifie ni le calibrage, ni les ablations, ni le rapport expérimental : sur décision du propriétaire, ces travaux viennent **après** l'implémentation de tous les composants.

---

## 1. Principe directeur

L'objectif immédiat est de **terminer le produit** : PRISM, SYNE, ECHOS, Launcher, Installateur et Viewer doivent exister, s'orchestrer et s'observer. Les portes scientifiques G7/G8, le calibrage et le rapport expérimental sont **hors de ce plan** et seront ouverts ensuite, sur des composants complets.

Deux conséquences :

1. Une porte ouverte qui ne bloque pas l'implémentation (G1) ne doit pas arrêter le développement.
2. Une tâche qui ne sert qu'à produire une mesure scientifique (calibrage, ablations, rapport) est **différée**, même si elle est techniquement prête.

## 2. État de départ vérifié — 15 novembre 2026

| Composant | Suites | État fonctionnel |
| :-- | :-- | :-- |
| PRISM V2 | **525/525** | P0–P11 livrées ; hôte, contrat `1.0.0`, persistance, bots, chaînes, viewer complet |
| SYNE | **813/813** | `0.18.0`, contrat d'observabilité `0.6.0`, monde extrait, R3 reproductible |
| ECHOS | **419 + 14 ignorés** | API et analyse V1 ; **ingestion PRISM de production** (`POST /ingest/prism`), analyse et comparaison de runs livrés |
| Launcher | **236 + 4 cas dynamiques** | Orchestration V1 ; manifeste PRISM V2 accepté, modes **Hôte**/**Viewer** et plan de contrôle opérationnels, `--check` 5/5 |
| Installateur | **120** | Release V1 ; artefacts V2 distribués (`prism-host`, `prism-viewer`, pack) |
| `syne-mock` | **51 + 2 ignorés** | Outil de test historique, hors produit livré |
| **Total projet** | **2 164 réussis / 16 ignorés / 2 184 collectés** | 4 cas Launcher dynamiques (`LIVEX_SYNE_PUBLISHED_ROOT` absent) |

Portes : G0, G3, G4, G5 franchies ; G1 ouverte et **non bloquante** ; G2 quasi couverte (hash de monde v2 et rapport daté manquants) ; G6 ouverte ; G7/G8 reportées.

## 3. Lots d'implémentation, dans l'ordre

L'ordre suit les dépendances réelles : l'observabilité avant la présentation, l'orchestration avant le packaging. Les tailles sont **relatives** (S, M, L, XL), jamais calendaires.

### L1 — Alignement documentaire (en cours dans cette passe)

- Corriger les compteurs de tests, les portes annoncées à tort, les identifiants ECHOS en collision et les liens morts.
- **Composants** : documentation transverse.
- **Taille** : S. **Sortie** : un seul total par composant, daté et sourcé.

### L2 — Ingestion de production PRISM → ECHOS (ECHOS-120)

- Brancher `ingest_prism_stream` sur un cycle de run réel : abonnement à `/data/read`, collecte des enveloppes, persistance atomique SQLite avec `runId`, `sequence`, `contentHash`, `seed` et provenance (mesuré / absent / zéro observé).
- Un harnais de run suffit ; **ne pas dépendre du Launcher** pour ce lot.
- **Composants** : ECHOS, script de run. **Taille** : M.
- **Sortie** : un run réel produit une ligne interrogeable par `GET /api/runs/{id}/prism-stream`, et un test d'intégration le rejoue.

### L3 — Moteur 6 et quatre indicateurs (ECHOS-121, ECHOS-122)

- Recalculer stocks, production et usure depuis les snapshots PRISM, sans réutiliser les réserves globales SYNE V1.
- Implémenter les quatre indicateurs : coopération, spécialisation, infrastructures, inégalité de stocks.
- Sources déjà publiées : `item_transferred`, contributions de chantier, effets `use`, `AgentEntitySnapshot.Inventory`, `metrics.structures`. **Aucun changement de contrat n'est nécessaire.**
- **Composants** : ECHOS. **Taille** : L.
- **Sortie** : quatre indicateurs calculés sur un run réel, un test par indicateur, provenance à trois états.

### L4 — Comparaison de runs (ECHOS-123)

- Comparer deux runs par `contentHash`, `seed`, empreinte et **premier tick de divergence**.
- **Composants** : ECHOS. **Taille** : S.
- **Sortie** : deux runs identiques divergent à zéro ; un paramètre changé diverge au tick attendu.

### L5 — Preuve de monde v2 et G2 (PRISM-037)

- Épingler une empreinte de monde **`WorldGenVersion 2`** sur trois graines et produire le rapport daté `G2-REPORT.md`.
- La matrice CI Windows/Linux existe déjà : la comparaison interplateforme est structurelle, il manque le hash v2 et sa trace.
- **Composants** : PRISM, CI. **Taille** : S.
- **Sortie** : G2 franchie, hash v2 cité dans le rapport et dans les tests.

### L6 — Performance de l'hôte et G1 (PRISM-143, puis PRISM-140/141)

- Activer et mesurer les champs de distance BFS par classe (`PRISM_DISTANCE_FIELDS=1`) : ils suppriment les recherches rejouées qui échouent, coût indépendant du nombre d'agents.
- Critère fixé d'avance : `ApplyWrites` ≤ 90 ms à 1 000 agents, sans changer les empreintes de trajectoire.
- PRISM-140 (parallélisme par agent) et le reste de PRISM-141 (pools) restent **après** cette mesure, et seulement si elle ne suffit pas.
- **Composants** : PRISM. **Taille** : M.
- **Sortie** : décision documentée (ADR-015) et mesure avant/après ; G1 franchie ou écart documenté et accepté.

### L7 — Launcher : manifeste et orchestration (LAUNCHER-200/201/202)

- Aligner le manifeste PRISM (`type`, `contributesTo`, `renderCadence`) et `PrismRequirements`, puis les ports, `.livexp` avec `contentHash` et l'arrêt propre de la session SYNE + PRISM + ECHOS.
- **Composants** : Launcher, PRISM. **Taille** : L.
- **Sortie** : une session V2 complète démarre, s'observe et s'arrête proprement depuis le Launcher.

### L8 — Installateur V2 (INSTALLER-200)

- Distribuer `prism-host`, `prism-viewer` et les packs de contenu ; options hôte/viewer/packs ; caducité de l'ADR-010 traitée.
- **Composants** : Installateur, CI. **Taille** : M.
- **Sortie** : installation sur machine propre Windows et Linux, composants V2 lançables.

### L9 — Visualiseur complet (PRISM-110 à PRISM-118)

- Terminer le rendu et l'inspection : atlas, occlusion, entités et actions live, caméras/coupe, inspecteur et sept calques, modes `--connect` / embarqué / rejeu, interventions journalisées désactivées par défaut, test d'indépendance du rendu, manifeste viewer et B8.
- **Composants** : PRISM Viewer. **Taille** : L.
- **Sortie** : P11 close, viewer livrable et testé sur Windows et Linux.
- *(Réalisé le 15/11, PRISM-110 à PRISM-118 : atlas procédural et UV de sommet, entités/actions live et chantiers fantômes, suivi live et coupe, inspecteur et sept calques, modes `--connect`/`--embedded`/`--replay`/`--record`, interventions journalisées via `/control/intervention`, test d'indépendance du rendu, manifeste `prism/viewer.component.json` et harnais B8. Preuves : Viewer **100/100** après câblage des assets et vérification à l'écran, Host **78/78**, Architecture **8/8** ; build solution sans erreur. **Vérification du 15/11 sur fenêtre réelle** : volumes d'arbres/minerais/blocs, blocs minés retirés par `region_delta`, captures PNG (`--screenshot`, `F12`), caméra souris, coupe effective (uniforme `uCutEnabled` corrigé) et arrêt propre du host embarqué ; captures dans `tmp/prism-captures/` (aperçu, coupe `z=18`, embarqué). **Câblage des assets (15/11)** : `ViewerAssets` charge `assets/` ou `--assets <dir>` (atlas, palette terrain, palette agents, police HUD) avec décodeur PNG interne et repli procédural ; `publish_prism_v2.py` copie `prism/assets/` ; un atlas externe de contrôle a été chargé sur fenêtre réelle (`tmp/prism-captures/assets-atlas.png`). **Limites** : la validation graphique locale a produit les captures ci-dessus, mais aucune comparaison graphique Windows/Linux ni aucun relevé B8 publiable n'a été exécuté (validation différée demandée par le propriétaire) ; les calques `Structures` et `Ownership` n'affichent que les données publiées par le contrat `1.0.0`, qui ne porte ni propriétaire ni ACL de site.)*

### L10 — Clôture produit

- Publication autonome, notices tierces, archivage de la documentation V1, mise à jour transverse, puis tags de release.
- **Composants** : tous. **Taille** : M.
- **Sortie** : candidate `2.0.0` installable ; G8 peut être ouverte.

## 4. Travaux explicitement différés

Ces travaux ne sont pas perdus : ils sont **rangés après L10**. Ils ne conditionnent aucune livraison de code.

| Travail | Tickets | Pourquoi il attend |
| :-- | :-- | :-- |
| Calibrage du contenu et du comportement | PRISM-120/121, SYNE-227 | suppose la pile complète et observable |
| Ablations A1–A8 | PRISM-123, ECHOS-130 | suppose le pipeline d'indication et les profils |
| Rapport expérimental pérenne | PRISM-124 | suppose le calibrage et les ablations |
| Gel des empreintes finales | PRISM-125 | suppose le protocole préenregistré |
| Régression de performance et release | PRISM-130 à 135 | suppose L6 à L10 terminés |

## 5. Ce qui n'est plus au programme

| Tâche | Décision | Raison |
| :-- | :-- | :-- |
| PRISM-039, PRISM-046, PRISM-057 | Retirées | benchmarks synthétiques déjà couverts par B6/B9 et les runs réels |
| PRISM-053 | Retirée | pathfinding hiérarchique non câblé, sans gain démontré ; rouvrir sur besoin mesuré |
| PRISM-103 | Close | couverte par `WellDrinkingAcceptanceTests` |
| MOCK-200 | Retirée | `syne-mock` reste un outil de test, sans réorientation justifiée |
| ECHOS-100…103 / 110 (tels que nommés dans PRISM) | Renumérotées | collision avec le packaging bureau livré ; voir `ECHOS-120…123` et `ECHOS-130` |

## 6. Règle de suivi

Un lot n'est clos que si :

1. le code est livré et les suites du composant sont vertes ;
2. les documents du composant citent le même total daté ;
3. les empreintes ou versions modifiées sont justifiées dans le CHANGELOG du composant ;
4. aucune porte n'est annoncée franchie sans sa preuve datée.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 15 novembre 2026 | Création du plan d'implémentation | Résorber les liens morts des documents PRISM et ordonner le reste à faire sans mêler calibrage et rapports |
