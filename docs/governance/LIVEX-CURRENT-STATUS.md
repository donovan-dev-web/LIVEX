# LIVEX — état transversal actuel

**Composant** : LIVEX (général)
**Statut** : [V1 LIVRÉE ; PRISM V2 EN COURS D'INTÉGRATION — RELEVÉ DU 15 NOVEMBRE 2026, 4 CAS DYNAMIQUES LAUNCHER]
**Date du relevé** : 15 novembre 2026
**Sources** : exécution des suites sur l'arbre de travail (branche `Feat-Refonte-PRISM-V2`) et documentation des composants.

> Cette page décrit l'état du travail V2 au 15 novembre 2026. Elle ne remplace
> pas le relevé détaillé de la release V1 du 31 octobre, conservé dans
> [`LIVEX-STATUS.md`](LIVEX-STATUS.md). Les chiffres ci-dessous sont ceux des
> suites exécutées le 15 novembre 2026 et **additionnés en un total projet**.

## Synthèse

LIVEX 1.0.0 reste la release publique fondée sur SYNE 0.17.0, ECHOS 0.1.0,
Launcher 0.1.0 et PRISM V1/PRISM-LDK sous Unreal. Le travail actuel porte sur
PRISM V2 : une simulation de monde .NET headless, distincte de la présentation
Unreal V1. Le contrat SYNE↔PRISM `1.0.0` est gelé, la boucle couplée est
reproductible et le transfert de l'autorité physique est achevé côté SYNE (plus
aucune classe de monde dans `Simulation.Core`). **G5 est franchie le
14 novembre 2026** (R3 produite et reproductible, empreinte
`855fdc2e1387e8d3`, coût propre à SYNE 30,5 ms/tick) ; le coût de l'hôte
(G1, 431,5 ms à 100 agents au dernier relevé du 15 novembre) reste hors du périmètre de G5. **Le viewer est livré et vérifié à l'écran (P11 livrée le 15 novembre 2026 : PRISM-110 à PRISM-118, Viewer 100/100, chargeurs d'assets câblés ; clôture conditionnée au relevé B8 et à la validation graphique Linux)** ; il pilote désormais la session depuis le Launcher (modes Hôte/Viewer, Play/Pause/Stop/Quit relayés à l'hôte, aucun tick avant Play ; Launcher 181/181 en unitaire, 236/240 toutes suites avec 4 cas dynamiques). Restent, avant la release V2 : la preuve datée de G6 et la recette d'installation sur machine propre (INSTALLER-200), l'empreinte de monde v2 et le rapport daté de G2, et la performance de l'hôte (G1, non bloquante).

## Comptabilité des tests — total projet

Toutes les suites exécutées le **15 novembre 2026** sur l'arbre de travail, en
Release (Windows x64) :

| Composant | Commande | Réussis | Ignorés | Détail |
| :-- | :-- | --: | --: | :-- |
| **PRISM V2** | `dotnet test prism/Prism.sln -c Release` | **525** | 0 | Architecture 8, Contracts 23, Viewer 100, Core 242, Content 51, Bots 23, Host 78 — passe du 15/11 : Viewer 100/100 réexécuté (dont 6 tests de chargement d'assets), Host 78/78 et Architecture 8/8 issus de la passe précédente, autres suites inchangées |
| **SYNE** | `dotnet test syne/Syne.sln -c Release` | **813** | 0 | Core 600, Console 213 |
| **ECHOS** | `.venv\Scripts\python -m pytest` | **419** | 14 | Couverture **91,14 %** |
| **Launcher** | `dotnet test launcher/Launcher.sln -c Release` | **236** | 4 | Unit 181, Integration 20, E2E 35 (+ 4 cas dynamiques `$XunitDynamicSkip$` : `LIVEX_SYNE_PUBLISHED_ROOT` absent, pas des régressions) |
| **Installateur** | `dotnet test installer/Setup.sln -c Release` | **120** | 0 | Unit 73, App 34, Integration 7, E2E 6 |
| **Total produits** | — | **2 113** | **14** | PRISM + SYNE + ECHOS + Launcher + Installateur ; 4 échecs dynamiques Launcher, hors décompte |
| `syne-mock` (outillage) | `npm test` | **51** | 2 | Contrats SYNE historiques, hors produit livré |
| **Total projet** | — | **2 164** | **16** | 2 184 tests collectés (dont 4 cas Launcher dynamiquement ignorés) |

> Les totaux ci-dessus remplacent tous les compteurs antérieurs des documents
> (423, 444, 477 pour PRISM ; 768, 811 pour SYNE ; 383 pour ECHOS). Un compteur
> de release V1 reste valable **pour la V1** et doit être daté comme tel.

## État par composant

| Composant | État connu | Limites et travaux ouverts |
| :-- | :-- | :-- |
| **SYNE** | Moteur `0.18.0`, contrat d'observabilité `0.6.0`. Au relevé du 15 novembre : **813/813 tests** (600 Core, 213 Console). **P9 clôturée le 15 novembre 2026** (étapes 1 à 3c livrées ; résidus transférés : reprise couplée complète → P10, plans de recettes riches → P12) : `PrismWorldAdapter` est la seule implémentation de `IWorld` et **plus aucune classe de monde ne subsiste dans `Simulation.Core`**. Livrés également : mémoire spatiale par chunk (SYNE-221), croyances de recettes/chantiers/structures et désirs instrumentaux (SYNE-222), planificateur à chaînage arrière borné (SYNE-223), savoir par les livres (SYNE-224), point de reprise couplé (SYNE-225), documentation migrée (SYNE-226). Run R2 concluant : 50 agents × 5 000 ticks, `0bf833f4bcfc1e5e` reproduite 3/3 sur trois hôtes, reprise couplée identique au run continu. Write-back BDI → commandes actif (`Aller`, `ObtenirObjet`, `Consommer`, `FabriquerMacro`, `Poser`). **Depuis le 10 novembre 2026, les agents SYNE accomplissent les chaînes sans plan scripté** : l'exécutif de production avance sur les faits de l'hôte (fin de macro distinguée de son acceptation), compte une chaîne quand l'objet final est observé dans l'inventaire, et prospecte un gisement connu quand une récolte est refusée faute de source. Mesures : 3 chaînes (6 agents × 1 500 ticks, 0 abandon), 5 chaînes sur 20 agents × 120 ticks, empreinte `233d76e194c897cf` reproduite deux fois ; **813/813 tests** (Core 600, Console 213) | **G5 franchie le 14 novembre 2026** : R3 pleine échelle (100 agents × 5 000 ticks) produite et reproductible, empreinte `855fdc2e1387e8d3` sur deux passes, 25 chaînes par passe, coût propre à SYNE **30,5 ms/tick** sous le budget de 50 ms. Le profilage du tick couplé (10 novembre 2026) avait nommé les deux calculs dominants — perception et déplacement (A\*) — et les correctifs ont ramené **620 → 84 ms/tick** à 100 agents immobiles et **2 150 → 705 ms** en production (422/422 PRISM, 813/813 SYNE, empreintes de campagne inchangées). Le coût de l'hôte (G1, 431,5 ms à 100 agents au relevé B9 du 15 novembre 2026) reste hors du périmètre de G5. Restent, pour G1 : recherche de chemin bornée, perception incrémentale au déplacement, index spatial des agents, checkpoints hors du chemin critique — [`../prism/PERF-P9-PROFILE.md`](../prism/PERF-P9-PROFILE.md) et [`../prism/G5-R3-REPORT.md`](../prism/G5-R3-REPORT.md). Résidus **transférés, pas perdus** : points de reprise couplés complets (SYNE-225 : saut de séquence, rotation) en durcissement P10 ; enrichissement de SYNE-223 (plans de recettes, cache inter-esprits) et recalibrage de survie (SYNE-227) en P12 ; le format d'écriture des livres (SYNE-224) reste ouvert en P12. La suite V2 ne remplace pas le mode SYNE autonome V1. |
| **PRISM V2** | Contrat `1.0.0` gelé en documentation et `ProtocolVersion.Current`. Au relevé du 15 novembre : **525/525 tests** (Architecture 8, Contracts 23, Viewer 100, Core 242, Content 51, Bots 23, Host 78) et build sans avertissement. **P11 est livrée le 15 novembre 2026** : le viewer est complet (atlas procédural, entités et actions live, chantiers fantômes, suivi et coupe, inspecteur et sept calques, modes `--connect`/embarqué/rejeu/`--record`, interventions journalisées, test d'indépendance du rendu, manifeste `prism/viewer.component.json`, harnais B8). Depuis le 15 novembre 2026, le viewer porte le **plan de contrôle** de la session (`/control/play\|pause\|stop\|quit` relayés à l'hôte, qui reste l'autorité) et le Launcher propose les modes **Hôte**/**Viewer** : la pile reste au tick `-1` jusqu'au **Play** (vérifié en réel : Play 338, pause 343 stable, reprise 499, stop 503). Host headless, monde 2,5D, contenu, actions, persistance, bots déterministes, chaînes de base et harnais R1 implémentés ; campagne réduite 3 graines × 200 ticks × 3 bots épinglée ; campagne complète 3 × 5 000 ticks exécutée, neuf empreintes publiées. Le monde porte la côte (`ShoreBand = 0`) et `WorldGenVersion 2` ; les empreintes dorées restent épinglées en `WorldGenVersion 1`. | **G4 franchie le 9 novembre 2026** : run 5 agents × 2 500 ticks graine 1 → **5/5 chaînes** (outils, agriculture, eau, cuisson, stockage), audit de conservation vert (0 violation), `cropRipeEvents` 4, empreinte `abab272bd13e6ba3` reproduite deux fois, 156 commandes pour 93 `action_already_active` (bénins) et 2 `support_required`. Suites PRISM 423/423 au moment de G4 (525/525 au 15 novembre) ; détail dans [`../prism/G4-R1-REPORT.md`](../prism/G4-R1-REPORT.md). Restent : empreinte de monde v2 et rapport daté (G2), recette G6/INSTALLER-200, performance de l'hôte (G1, non bloquante) et release autonome (G8) ; le viewer (P11) est **livré**, avec validation graphique Windows/Linux et B8 différés. |
| **ECHOS** | API et analyse Python demeurent le service de mesure de la pile V1. Le socle `ECHOS-120` valide et persiste les enveloppes de lecture PRISM avec séquence et `contentHash`, et les expose par API. Relevé du 15 novembre 2026 après le lot P10 : **419 réussis, 14 ignorés, couverture 91,14 %**. | P10 côté ECHOS est **livré** (ECHOS-120…123) : `POST /ingest/prism` est l'appelant de production de l'ingestion (lots bornés, reprise après coupure idempotente, schéma SQLite v9), `POST /api/runs/{id}/prism-analysis` rejoue le flux conservé (`PrismResourceMetrics` : stocks/production/usure ; `PrismCooperationMetrics` : coopération, spécialisation, infrastructures, inégalité de stocks, provenance à trois états) et `GET /api/prism/compare` nomme le premier tick de divergence par `contentHash`. Restent hors ECHOS : LAUNCHER-201/202 et INSTALLER-200. Les tickets V2 sont renumérotés `ECHOS-120…123` pour ne pas collisionner avec les tickets de packaging `ECHOS-100…103`. |
| **Launcher** | Orchestrateur de la release V1, avec paquets `.livexp` et composants publiés. Relevé du 15 novembre après le lot P10-Launcher et la P11 : **236 réussis** (Unit **181**, Integration **20**, E2E **35**), plus 4 cas dynamiques `$XunitDynamicSkip$` (environnement). `--check` rend **5 composants valides** depuis la correction du manifeste de staging. Les 4 tests E2E conditionnels à `LIVEX_SYNE_PUBLISHED_ROOT` restent hors décompte : sans `dotnet publish` de SYNE ils s'auto-ignorent, et l'adaptateur VSTest les rapporte en échec (`$XunitDynamicSkip$`) au lieu de les compter ignorés — condition d'environnement, pas régression. | Le manifeste `prism/component.json` déclare désormais `type: immersion`, `contributesTo: [immersion]` et les capacités `snapshotStream`/`renderCadence` : `PrismRequirements` ne renvoie plus aucune cause, **PRISM V2 n'est plus verrouillé** (LAUNCHER-200 côté manifeste). **LAUNCHER-201/202 sont livrés** : le `.livexp` porte `world.json` (`contentHash`, graine, pack) lu par `ReadWorld()`, un composant déclare au manifeste les points d'accès qu'il consomme, le Launcher transmet l'URL résolue du producteur (`--prism-stream` vers ECHOS), refuse de lancer un consommateur sans producteur et ordonne le démarrage selon `requires`/`consumes` (arrêt inverse, délai de grâce du manifeste). Preuves : 81 tests unitaires Package+Domain et 6 tests ECHOS. Reste INSTALLER-200. |
| **PRISM V1 / PRISM-LDK** | Projet Unreal et plugin historiques livrés dans LIVEX 1.0.0. | Cette architecture n'est pas le runtime de PRISM V2. Les documents V1 restent des références historiques. |
| **`syne-mock`** | Émulateur Node.js des contrats SYNE V1, utile aux clients historiques. Relevé du 15 novembre : **51 réussis, 2 ignorés**. | Ce n'est pas le serveur de campagne PRISM V2 ni un moteur scientifiquement équivalent ; PRISM utilise `Prism.Bots` pour ses campagnes. `MOCK-200` est **retiré** : `syne-mock` reste un outil de test, hors produit livré. |
| **Installateur** | Installateur de la release V1 publié pour Windows et Linux. Relevé du 15 novembre : **120 réussis** (Unit 73, App 34, Integration 7, E2E 6). | **INSTALLER-200 livré côté distribution** : `scripts/release/publish_prism_v2.py` publie `prism-host` (351 fichiers, 114,1 Mo win-x64 / 349, 112,8 Mo linux-x64), `prism-viewer` (210 fichiers, 85,4 / 87,7 Mo) et le pack de contenu (`prism-content`, 9 fichiers, 13,4 Ko), versions vérifiées contre `installer/build/components.json` ; les trois sont déclarés facultatifs (viewer → hôte → pack) et distribués en archives mesurées (taille + SHA-256 dans `distribution.json`). L'ADR-010 est amendé : il ne couvre plus que les entrées V1 sous Unreal. Reste la recette d'installation sur machine propre avec la session G6. |

## Portes V2 encore ouvertes

- **G1 et performance** : **ouverte, non bloquante pour l'implémentation**. Dernière
  mesure complète le 15 novembre 2026 (harnais PRISM-106, `B9-coupled-tick-2026-11-15-v1.json`,
  100 agents × 25 ticks, 2 passes) : tick médian **431,5 ms** (erreur-type 5,15 %),
  **ApplyWrites 404,7 ms** (94 % du tick), perception 15,2 ms, macros 14,4 ms ;
  la perception a une erreur-type de **14,78 %**, donc le harnais rejette ce run
  comme non concluant pour ce poste. À 1 000 agents, la dernière mesure valide
  reste **850 ms** (perception 317 ms). Écarts aux budgets : ×8,6 à 100 agents
  (50 ms) et ×3,4 à 1 000 agents (250 ms). Le levier prioritaire est la
  suppression des recherches de chemin rejouées (PRISM-143), pas la
  parallélisation de la perception (PRISM-140, différée).
- **G2** : quasi couverte — la matrice CI exécute les suites PRISM sous
  Windows **et** Linux avec les empreintes épinglées, et les mondes dorés v1 et
  le viewer v0 sont verts. Il manque **une empreinte de monde `WorldGenVersion 2`**
  (les empreintes dorées restent en v1) et un **rapport `G2-REPORT.md` daté**.
- **G3** : franchie — rapport [`../prism/G3-CONTRACT-REPORT.md`](../prism/G3-CONTRACT-REPORT.md).
  Contrat `1.0.0` gelé, R2 reproductible (`0bf833f4bcfc1e5e` × 3 hôtes), reprise
  couplée bit-identique, write-back BDI → commandes validé, garde-fou de
  déterminisme vert. Le rejeu interplateforme (PRISM-079) reste reporté à G2.
- **G4** : franchie le 9 novembre 2026 — 5/5 chaînes par bots, audit de
  conservation vert, campagne reproductible (`abab272bd13e6ba3`). Détail dans
  [`../prism/G4-R1-REPORT.md`](../prism/G4-R1-REPORT.md).
- **G5** : **franchie le 14 novembre 2026.** R3 pleine échelle (100 agents ×
  5 000 ticks) est produite et reproductible : empreinte `855fdc2e1387e8d3` sur
  deux passes identiques, 25 chaînes terminées par passe, 0 abandon, coût propre
  à SYNE **30,5 ms/tick** sous le budget de 50 ms. Le coût de l'hôte (G1) reste
  hors du périmètre de G5. Détail :
  [`../prism/G5-R3-REPORT.md`](../prism/G5-R3-REPORT.md) et relevé
  `benchmarks/G5-R3-2026-11-14-v1.json`. **P9 est clôturée le 15 novembre 2026** ; les deux résidus (`SYNE-225` reprise couplée complète, `SYNE-223` plans de recettes riches) sont transférés en P10 et P12.
- **G6** : côté ECHOS et côté manifeste, le socle est **livré**. L'ingestion a
  désormais un appelant de production (`POST /ingest/prism`, reprise après
  coupure idempotente), le rejeu analytique écrit stocks/production/usure et les
  quatre indicateurs V2 avec provenance à trois états, la comparaison de runs
  nomme le premier tick de divergence, et `prism/component.json` satisfait
  `PrismRequirements` (`immersion`, `snapshotStream`, `renderCadence`, contrôle)
  — le Launcher ne refuse plus PRISM V2 au manifeste. Restent l'orchestration
  des ports et du cycle de vie, les paquets `.livexp` avec `contentHash` et
  LAUNCHER-201/202 (livrés), puis INSTALLER-200 et l'archivage du flux PRISM par run.
- **G7–G8** : **reportées après l'implémentation complète**, sur décision du
  propriétaire. Le calibrage, les ablations A1–A8, le rapport expérimental et la
  préparation de livraison ne sont pas des préalables au reste du développement.
  Les résultats d'octobre n'établissent pas une émergence générale : la quête
  spatiale est confirmée dans le protocole publié, le cycle social n'est pas
  confirmé.

## Règle de lecture des chiffres

Le total projet du 15 novembre 2026 est **2 164 tests réussis, 16 ignorés,
4 cas dynamiques et 2 184 collectés**, tous composants confondus (PRISM, SYNE, ECHOS, Launcher,
Installateur, `syne-mock`). Les chiffres détaillés de la release V1 restent ceux
du 31 octobre dans [`LIVEX-STATUS.md`](LIVEX-STATUS.md) et ne doivent pas être
additionnés à ce total : ils décrivent un autre périmètre. Toute mise à jour d'un
compteur doit citer la date et la commande du run.
