# LIVEX — état transversal actuel

**Composant** : LIVEX (général)
**Statut** : [V1 LIVRÉE ; PRISM V2 EN COURS D'INTÉGRATION — TOUTES SUITES VERTES AU 15 NOVEMBRE 2026]
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
(G1, 431,5 ms à 100 agents au dernier relevé du 15 novembre) reste hors du périmètre de G5. Restent, avant la
release V2 : l'implémentation complète du viewer, l'ingestion ECHOS en
production, le raccordement du Launcher et de l'installateur aux artefacts V2,
l'empreinte de monde v2 et le rapport daté de G2, et la performance de l'hôte
(G1, non bloquante).

## Comptabilité des tests — total projet

Toutes les suites exécutées le **15 novembre 2026** sur l'arbre de travail, en
Release (Windows x64) :

| Composant | Commande | Réussis | Ignorés | Détail |
| :-- | :-- | --: | --: | :-- |
| **PRISM V2** | `dotnet test prism/Prism.sln -c Release` | **487** | 0 | Architecture 8, Contracts 23, Viewer 62, Core 242, Content 51, Bots 23, Host 78 |
| **SYNE** | `dotnet test syne/Syne.sln -c Release` | **813** | 0 | Core 600, Console 213 |
| **ECHOS** | `.venv\Scripts\python -m pytest` | **393** | 14 | Couverture **91,24 %** |
| **Launcher** | `dotnet test launcher/Launcher.sln -c Release` | **229** | 4 | Unit 174, Integration 20, E2E 35 ; les 4 ignorés sont dynamiques (`LIVEX_SYNE_PUBLISHED_ROOT` absent) |
| **Installateur** | `dotnet test installer/Setup.sln -c Release` | **120** | 0 | Unit 73, App 34, Integration 7, E2E 6 |
| **Total produits** | — | **2 042** | **18** | PRISM + SYNE + ECHOS + Launcher + Installateur |
| `syne-mock` (outillage) | `npm test` | **51** | 2 | Contrats SYNE historiques, hors produit livré |
| **Total projet** | — | **2 093** | **20** | 2 113 tests collectés |

> Les totaux ci-dessus remplacent tous les compteurs antérieurs des documents
> (423, 444, 477 pour PRISM ; 768, 811 pour SYNE ; 383 pour ECHOS). Un compteur
> de release V1 reste valable **pour la V1** et doit être daté comme tel.

## État par composant

| Composant | État connu | Limites et travaux ouverts |
| :-- | :-- | :-- |
| **SYNE** | Moteur `0.18.0`, contrat d'observabilité `0.6.0`. Au relevé du 15 novembre : **813/813 tests** (600 Core, 213 Console). **P9 clôturée le 15 novembre 2026** (étapes 1 à 3c livrées ; résidus transférés : reprise couplée complète → P10, plans de recettes riches → P12) : `PrismWorldAdapter` est la seule implémentation de `IWorld` et **plus aucune classe de monde ne subsiste dans `Simulation.Core`**. Livrés également : mémoire spatiale par chunk (SYNE-221), croyances de recettes/chantiers/structures et désirs instrumentaux (SYNE-222), planificateur à chaînage arrière borné (SYNE-223), savoir par les livres (SYNE-224), point de reprise couplé (SYNE-225), documentation migrée (SYNE-226). Run R2 concluant : 50 agents × 5 000 ticks, `0bf833f4bcfc1e5e` reproduite 3/3 sur trois hôtes, reprise couplée identique au run continu. Write-back BDI → commandes actif (`Aller`, `ObtenirObjet`, `Consommer`, `FabriquerMacro`, `Poser`). **Depuis le 10 novembre 2026, les agents SYNE accomplissent les chaînes sans plan scripté** : l'exécutif de production avance sur les faits de l'hôte (fin de macro distinguée de son acceptation), compte une chaîne quand l'objet final est observé dans l'inventaire, et prospecte un gisement connu quand une récolte est refusée faute de source. Mesures : 3 chaînes (6 agents × 1 500 ticks, 0 abandon), 5 chaînes sur 20 agents × 120 ticks, empreinte `233d76e194c897cf` reproduite deux fois ; **813/813 tests** (Core 600, Console 213) | **G5 franchie le 14 novembre 2026** : R3 pleine échelle (100 agents × 5 000 ticks) produite et reproductible, empreinte `855fdc2e1387e8d3` sur deux passes, 25 chaînes par passe, coût propre à SYNE **30,5 ms/tick** sous le budget de 50 ms. Le profilage du tick couplé (10 novembre 2026) avait nommé les deux calculs dominants — perception et déplacement (A\*) — et les correctifs ont ramené **620 → 84 ms/tick** à 100 agents immobiles et **2 150 → 705 ms** en production (422/422 PRISM, 813/813 SYNE, empreintes de campagne inchangées). Le coût de l'hôte (G1, 431,5 ms à 100 agents au relevé B9 du 15 novembre 2026) reste hors du périmètre de G5. Restent, pour G1 : recherche de chemin bornée, perception incrémentale au déplacement, index spatial des agents, checkpoints hors du chemin critique — [`../prism/PERF-P9-PROFILE.md`](../prism/PERF-P9-PROFILE.md) et [`../prism/G5-R3-REPORT.md`](../prism/G5-R3-REPORT.md). Résidus **transférés, pas perdus** : points de reprise couplés complets (SYNE-225 : saut de séquence, rotation) en durcissement P10 ; enrichissement de SYNE-223 (plans de recettes, cache inter-esprits) et recalibrage de survie (SYNE-227) en P12 ; le format d'écriture des livres (SYNE-224) reste ouvert en P12. La suite V2 ne remplace pas le mode SYNE autonome V1. |
| **PRISM V2** | Contrat `1.0.0` gelé en documentation et `ProtocolVersion.Current`. Au relevé du 15 novembre : **487/487 tests** (Architecture 8, Contracts 23, Viewer 62, Core 242, Content 51, Bots 23, Host 78) et build sans avertissement. Host headless, monde 2,5D, contenu, actions, persistance, bots déterministes, chaînes de base et harnais R1 implémentés ; campagne réduite 3 graines × 200 ticks × 3 bots épinglée ; campagne complète 3 × 5 000 ticks exécutée, neuf empreintes publiées. Le monde porte la côte (`ShoreBand = 0`) et `WorldGenVersion 2` ; les empreintes dorées restent épinglées en `WorldGenVersion 1`. | **G4 franchie le 9 novembre 2026** : run 5 agents × 2 500 ticks graine 1 → **5/5 chaînes** (outils, agriculture, eau, cuisson, stockage), audit de conservation vert (0 violation), `cropRipeEvents` 4, empreinte `abab272bd13e6ba3` reproduite deux fois, 156 commandes pour 93 `action_already_active` (bénins) et 2 `support_required`. Suites PRISM 423/423 au moment de G4 (487/487 au 15 novembre) ; détail dans [`../prism/G4-R1-REPORT.md`](../prism/G4-R1-REPORT.md). Restent : viewer complet (P11), empreinte de monde v2 et rapport daté (G2), ingestion ECHOS en production, performance de l'hôte (G1, non bloquante) et release autonome (G8). |
| **ECHOS** | API et analyse Python demeurent le service de mesure de la pile V1. Le socle `ECHOS-120` valide et persiste les enveloppes de lecture PRISM avec séquence et `contentHash`, et les expose par API. Relevé du 15 novembre 2026 : **393 réussis, 14 ignorés, couverture 91,24 %**. | L'ingestion PRISM (`ingest_prism_stream`) n'a **aucun appelant de production** : elle n'est pas raccordée au cycle de run. Aucun module d'analyse ECHOS ne référence PRISM, donc les quatre indicateurs (coopération, spécialisation, infrastructures, inégalité de stocks) et la comparaison de runs V2 restent à écrire. Les tickets V2 sont renumérotés `ECHOS-120…123` pour ne pas collisionner avec les tickets de packaging `ECHOS-100…103`. |
| **Launcher** | Orchestrateur de la release V1, avec paquets `.livexp` et composants publiés. Relevé du 15 novembre : **229 réussis, 4 ignorés** (Unit 174, Integration 20, E2E 35) ; les 4 ignorés sont dynamiques faute de `LIVEX_SYNE_PUBLISHED_ROOT`. | Le manifeste `prism/component.json` déclare les capacités `headless` et `snapshotStream`, alors que `PrismRequirements` exige `immersion` et `renderCadence` : **PRISM V2 est verrouillé** tant que le manifeste et les exigences ne sont pas alignés (LAUNCHER-200). Restent l'orchestration des ports et du cycle de vie, `.livexp` avec `contentHash` et l'arrêt propre (LAUNCHER-201/202). |
| **PRISM V1 / PRISM-LDK** | Projet Unreal et plugin historiques livrés dans LIVEX 1.0.0. | Cette architecture n'est pas le runtime de PRISM V2. Les documents V1 restent des références historiques. |
| **`syne-mock`** | Émulateur Node.js des contrats SYNE V1, utile aux clients historiques. Relevé du 15 novembre : **51 réussis, 2 ignorés**. | Ce n'est pas le serveur de campagne PRISM V2 ni un moteur scientifiquement équivalent ; PRISM utilise `Prism.Bots` pour ses campagnes. `MOCK-200` est **retiré** : `syne-mock` reste un outil de test, hors produit livré. |
| **Installateur** | Installateur de la release V1 publié pour Windows et Linux. Relevé du 15 novembre : **120 réussis** (Unit 73, App 34, Integration 7, E2E 6). | La distribution des composants V2 et leur acquisition par l'installateur attendent la lane INSTALLER-200 et les artefacts définitifs (`prism-host`, `prism-viewer`, packs de contenu). |

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
- **G6** : socle ECHOS-120 en place (validation et persistance des enveloppes
  PRISM), mais l'**ingestion n'a aucun appelant de production** et les quatre
  indicateurs V2 ne sont pas écrits. Le manifeste PRISM (`headless`,
  `snapshotStream`) ne satisfait pas `PrismRequirements` (`immersion`,
  `renderCadence`) : le Launcher ne peut pas lancer PRISM V2. Les lanes
  LAUNCHER-200/201/202 et INSTALLER-200 restent ouvertes.
- **G7–G8** : **reportées après l'implémentation complète**, sur décision du
  propriétaire. Le calibrage, les ablations A1–A8, le rapport expérimental et la
  préparation de livraison ne sont pas des préalables au reste du développement.
  Les résultats d'octobre n'établissent pas une émergence générale : la quête
  spatiale est confirmée dans le protocole publié, le cycle social n'est pas
  confirmé.

## Règle de lecture des chiffres

Le total projet du 15 novembre 2026 est **2 093 tests réussis, 20 ignorés,
2 113 collectés**, tous composants confondus (PRISM, SYNE, ECHOS, Launcher,
Installateur, `syne-mock`). Les chiffres détaillés de la release V1 restent ceux
du 31 octobre dans [`LIVEX-STATUS.md`](LIVEX-STATUS.md) et ne doivent pas être
additionnés à ce total : ils décrivent un autre périmètre. Toute mise à jour d'un
compteur doit citer la date et la commande du run.
