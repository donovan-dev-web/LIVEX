# ISSUES — Backlog PRISM V2

**Composant** : PRISM
**Statut** : [BACKLOG ACTIF — SOCLES P7/P8/P11 ; G1 EN ATTENTE]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : [`ROADMAP.md`](ROADMAP.md), `../governance/ISSUES.md`, `../governance/KANBAN.md`
**Source Monographie** : Annexe K

---

## 1. Objectif

Backlog complet du composant PRISM V2, **source unique de création des cartes Kanban**. Chaque issue : ID, titre, labels, priorité (P0 bloquant, P1 important, P2 souhaitable), dépendances, critère d'acceptation. Les milestones suivent les phases de `ROADMAP.md`. Les issues des autres composants sont listées en §4 (identifiants réservés).

## 2. Règles

- PRISM ne décide pas du comportement des agents ; aucune issue ne l'y autorise.
- Toute issue qui modifie un monde généré ou une règle met à jour `worldGenVersion`/`engineVersion` et les empreintes dorées dans la même PR.
- Toute issue touchant un contrat suit `API_CONTRACTS.md` §8.

## Suivi d'exécution

| ID | État | Référence |
| :-- | :-- | :-- |
| PRISM-001 | Terminée | Documentation V2 lue ; travaux et décisions ouvertes conservés dans cette roadmap. |
| PRISM-002 | Implémentée ; cross-build `linux-x64` réussi, runner Linux en attente | [#617](https://github.com/donovan-dev-web/LIVEX/issues/617) |
| PRISM-003 | Implémentée ; tests Windows réussis, validation CI native en attente | [#618](https://github.com/donovan-dev-web/LIVEX/issues/618) |
| ECHOS-100 | Socle implémenté | Validation des enveloppes PRISM 1.x, séquences/resynchronisation par snapshot, persistance atomique SQLite v8 et résumé par run ; intégration au cycle d'exécution et traitement analytique restent ouverts. |
| PRISM-010 | Prototype initial mesuré | Génération 2.5D, transitions de pente et parallélisme régional mesurés ; code jetable. Voir [`rapport B6`](benchmarks/B6-generation-25d.md). |
| PRISM-011 | Mesure initiale terminée ; sous l'allocation révisée | 0 échec ; médiane 81,23 ms/1 000 routes en parallèle, sous le poste indicatif révisé à 90 ms. Voir [`rapport B1`](benchmarks/B1-pathfinding-25d.md) ; hiérarchie/cache et tick complet à confirmer. |
| PRISM-012 | Mesure initiale terminée ; sous l'allocation révisée | Calcul médian récent 32,70 ms ; encodage JSON 34,78 ms/1 000 observations, sous les postes de 35/40 ms avec faible marge. Voir [`rapport B2`](benchmarks/B2-perception-25d.md) ; différences, plafond et structures restent non modélisés. |
| PRISM-013 | Mesure initiale terminée ; recommandation documentée | Croissance programmée plus rapide dans le microbenchmark, avec un résultat fonctionnel différent ; la Q-09 reste alignée sur la croissance programmée. Voir [`rapport B3`](benchmarks/B3-growth-25d.md). |
| PRISM-014 | Mesure synthétique complète ; delta contractuel à faire | 100 locksteps loopback, 1 000 commandes/tick : réponse avec snapshots complets + événements, p95 93,67 ms/tick, au-dessus du poste 40 ms. Les différences contractuelles et leurs taux de changement restent à mesurer ; voir [`rapport B4`](benchmarks/B4-transport-25d.md). |
| PRISM-015 | Mesure initiale terminée ; charges synthétiques | Snapshot JSON 4 794 556 o ; delta JSON 111 364 o pour 1 024 colonnes modifiées. Voir [`rapport B5`](benchmarks/B5-region-streaming-25d.md) ; contenu moins compressible et vrai flux à mesurer. |
| PRISM-016–018 | En cours | B6 : génération 3,43 ms séquentielle / 0,94 ms parallèle, tableaux identiques. La fenêtre OpenGL v0 démarre localement sous Windows ; la validation Linux et B8 restent à faire. Transport HTTP/WebSocket JSON décidé ; G1 reste ouverte. |
| PRISM-020 | Implémentée | Registre ordinal canonique clé ↔ identifiant sur 16 bits ; unicité, bornes et round-trip couverts. |
| PRISM-023 | Implémentée (socle) | Grille/régions 2.5D, colonnes SoA uniformes matérialisées à la première modification, surcharges clairsemées triées `(z,y,x)` et dirty flags ; tests de bornes/écriture. |
| PRISM-024 | Implémentée | `Mix64`, dérivation de graines par nom, flux `xoshiro256**` et hasard à compteur ; vecteurs fixes et indépendance testés. |
| PRISM-025 | Implémentée (socle) | Empreintes FNV-1a des régions et du monde avec encodage little-endian et ordre canonique des surcharges ; tests d'ordre et de mutation. |
| PRISM-026 | Implémentée (socle) | Roue temporelle circulaire, événements dus en ordre `(tick, séquence)`, annulation et garde contre le retour en arrière ; tests de rotation et d'égalité de tick. |
| PRISM-027 | Implémentée (socle) | Ledger de création/destruction par objet et règle, audit contre l'inventaire courant, violations ordonnées ; tests d'équilibre et de déséquilibre. |
| PRISM-021 | Implémentée (socle) | Validation structurée des manifestes/version, identifiants, tags hiérarchiques, matériaux, objets, recettes, gabarits, croissance, effets, références, paramètres documentés et drops conditionnels/probabilistes/plage. Dépendances entre packs et extensions post-V2 non prises en charge. |
| PRISM-022 | Implémentée | Pack minimal v0 dans `prism/content/minimal` ; compilation et graphe amorçable à mains nues jusqu'aux pioches de pierre et de fer couverts par tests. |
| PRISM-028 | Implémentée | Tests générés à seed fixe : écriture/récupération de colonnes et surcharges, empreinte invariante à l'ordre d'insertion. |
| PRISM-030 | Implémentée (socle) | Bruit de valeurs 2D et fractal en virgule fixe 16.16, coordonnées négatives, flux nommés, domaine déformé et vecteurs fixes testés. |
| PRISM-031 | Implémentée (socle) | Génération déterministe d'une carte de hauteur bornée avec amplitudes, octaves, déformation de domaine et falaise de bordure ; tests de seed, bornes et configuration. |
| PRISM-032 | Implémentée (socle) | Classification déterministe plaine/forêt/roche/bord d'eau ; profils de strates résolvant surface, terre, pierre et bedrock. |
| PRISM-033 | Implémentée (socle) | Échantillonnage de filons 3D en bruit entier, profondeur relative à la surface, flux ores isolé et placement déterministe. |
| PRISM-034 | Implémentée (socle) | Remplissage statique des colonnes sous le niveau marin sans écraser les blocs déjà générés. |
| PRISM-035 | Implémentée (socle) | Placement déterministe et clairsemé de gabarits d'arbres avec jitter de cellule, densités par zone et vérification de volume libre. |
| PRISM-036 | Implémentée (socle) | Sélection déterministe de sites avec accès aux six catégories de ressources ; huit tentatives de graines dérivées et échec structuré testés. Les planchers globaux de ressources restent à intégrer. |
| PRISM-037 | Empreintes dorées locales ajoutées | Trois graines de référence verrouillées par test. L'exécution et la comparaison interplateformes dans CI restent à faire. |
| PRISM-038 | Implémentée (socle) | Fenêtre Silk.NET/OpenGL 3.3, monde seedable, maillage des faces visibles par région, caméra libre clavier ; tests du maillage réussis. Validation de l'affichage sur runners Windows/Linux, overlays et réglage en direct des paramètres restent à faire. |
| PRISM-039 | Reportée à l'optimisation | Benchmark de génération sur le code de production ; aucun benchmark synthétique supplémentaire avant une phase d'optimisation avec SYNE. |
| PRISM-040 | Implémentée (socle) | Calendrier déterministe jour/heure/saison, transitions jour/saison émises dans l'ordre et garde contre le retour en arrière ; tests de frontières et de débordement. |
| PRISM-041 | Implémentée (socle) | Ordonnancement générique à gigue déterministe sur flux `growthJitter`, délais toujours strictement futurs ; tests de répétabilité et bornes. |
| PRISM-042 | Implémentée (socle) | Tirages uniques par région, calculables en parallèle et application des écritures dans l'ordre canonique ; test identique avec 1 et 8 workers. |
| PRISM-043 | Implémentée (socle) | Le compilateur de contenu lie les règles aux matériaux ; plantation vérifie sol/eau, progression saisonnière gigue comprise, reprise à la saison propice, maturité et flétrissement sont testés. La récolte d'action est implémentée avec PRISM-064 ; la fraîcheur des piles et les commandes restent à intégrer. |
| PRISM-044 | Implémentée (socle) | Fraîcheur entière en permille-ticks calculée à l'accès ou au transfert selon le taux du conteneur ; expiration détruit la pile, retourne un événement de péremption et alimente `MatterLedger` avec `spoil`. |
| PRISM-045 | Implémentée (socle) | Propagation de l'herbe déterministe à partir des mises à jour aléatoires, configurable par `updates.grassSpreadChancePermille`, limitée à la terre exposée adjacente. |
| PRISM-050 | Implémentée (socle) | Corps 1×1×2 validé sur le terrain, profil vitesse/capacité, inventaire à emplacements et dépôt des piles au sol au retrait ; commandes host `Incarner`/`Retirer`, snapshots d'entités et checkpoint du cycle de vie intégrés. `Aller` (chemin A* calculé à la réception, un pas par tick, ordre checkpointé) et la fusion des piles au sol de même taille sont implémentées ; les actions primitives relèvent de P6. |
| PRISM-051 | Implémentée (socle) | Graphe de surface 2.5D, 8 voisins, montée 1/descente 3, corps bloqué par solides et liquides, diagonales sans coupe de coin ; cas limites testés. |
| PRISM-052 | Implémentée (socle) | A* à coûts entiers 1000/1414, ordre de départage déterministe, plafond d'expansion et cache LRU lié au monde, sa révision et au budget ; optimalité, répétabilité, invalidation et budget testés. |
| PRISM-054 | Implémentée (socle) | Perception proche des blocs exposés, objets au sol et agents ; résumés régionaux de tags exposés ; différences apparu/modifié/disparu, état de soi et plafond appliqué aux deltas. Les structures/chantiers restent à modéliser. |
| PRISM-055 | Implémentée (socle) | Arbitrage stateless par flux `conflicts`, tick, agent et cible ; ordre indépendant de l'entrée, doublons refusés et équilibre vérifié sur 10 000 ticks. L'émission des événements perdants sera raccordée à la boucle de commandes. |
| PRISM-056 | Implémentée (socle) | Coût cardinal/diagonal réduit sur les transitions entre blocs de chemin, avec facteur entier configurable ; les tags `walkable_path` et les blocs de gabarits à effet `path_speed` sont compilés. |
| PRISM-060 | Implémentée (socle) | Minage du bloc de surface ou d'une surcharge, proximité/perception, palier d'outil, durée dureté/maîtrise, drops probabilistes déterministes, inventaire ou dépôt au sol ; erreurs motivées et créations au ledger couvertes. `Miner` est dispatchée par le host et résolue à l'échéance de sa durée (`action_started` puis `action_completed`) ; une macro qui l'a demandée reçoit les quantités produites à la résolution. Le minage géologique profond reste à faire. |
| PRISM-061 | Implémentée (socle) | Pose sur case libre dans le monde, lien explicite objet-matériau, proximité/perception, refus si un agent occupe le volume et destruction de l'objet au ledger ; tests d'occupation et de conservation. `places` est validé par le compilateur de contenu ; `Poser` est dispatchée par le host. |
| PRISM-062 | Implémentée (socle) | Conteneur borné par emplacements, propriétaire et droits `take`/`put` sur agents ou groupes opaques ; dépôts/retraits atomiques, portée/perception et `access_denied` testés. Le host crée les conteneurs à la pose d'un objet tagué `container`, dispatch `Prendre`/`Deposer`, interdit de miner un conteneur non vide et checkpoint son contenu, propriétaire et ACL. La capacité est provisoirement fixée à 24 emplacements ; `DefinirAccesConteneur` configure les ACL par commande. Les piles portent désormais leur fraîcheur, les fusions n'agrègent que des états identiques et un conteneur applique son taux de décroissance à la prise (`ApplyDecay`, comptabilité `spoil`, `item_spoiled`). Le balayage des inventaires d'agents est raccordé à la phase de cycle de vie (`AgentInventory.ApplyDecay`, 1 000 ‰/tick) et publie un événement `item_spoiled`. |
| PRISM-063 | Implémentée (socle) | Compilation runtime des recettes avec entrées par item/tag, outils, station et produits ; fabrication vérifie ingrédients, outil, maîtrise, station libre/proche/perçue, durée configurée et échange inventaire/ledger atomique. `Fabriquer` est dispatchée en deux temps : `action_started` au tick de la commande, application de la recette et `action_completed` à l'échéance, avec `Annuler` pour interrompre. `Miner`, `Labourer`, `Planter` et `Recolter` suivent le même cycle différé. |
| PRISM-064 | Implémentée (socle) | Labourage des sols déclarés avec houe, plantation consommant la graine liée au contenu après validation sol/eau/saison, récolte déterministe des drops filtrés par état mûr avec garde capacité ; cycle graine → culture → graines et ledger testés avec le pack minimal. `Labourer`, `Planter` et `Recolter` sont dispatchées et résolues à l'échéance ; croissance et calendrier ont des phases host. |
| PRISM-065 | Implémentée (socle) | Consomme une unité d'objet `edible`, vérifie perception, émet `item_consumed` avec effets ; boire exige une source `drinkable` perçue et adjacente, sans modifier la source. Règles compilées depuis les effets d'items/matériaux ; `Consommer` est dispatchée avec événement dédié et ledger `consume`. |
| PRISM-066 | Implémentée (socle) | `durability` et `usePerAction` sont compilés vers les règles runtime ; chaque outil utilisé s'use au succès, sa durabilité restante suit la pile lors des transferts agent/conteneur et sa casse détruit une unité au ledger sous `wear`. Minage, labourage, outil de recette, échecs sans usure et validation de contenu testés ; la synchronisation différée des cinq actions à durée est en place. |
| PRISM-067 | Implémentée (socle) | Machines d'états par agent pour `ObtenirObjet` (source choisie uniquement dans la perception fournie, filtre type/tag et indice, ordre déterministe, progrès conservé, `source_not_found` si l'indice n'est pas perçu), `Fabriquer` et `Cultiver` (attente de maturité, récolte sur ordre). Leur orchestration host n'avance qu'après la résolution de l'action demandée : le dispatch renvoie le résultat à l'étape en attente (`macro_step_deferred`, `DeferredActionResolutions`) et l'étape est validée avec les quantités produites. |
| PRISM-068 | Implémentée (socle) | Transfert atomique entre agents avec portée configurable (défaut 3 blocs), contrôle de perception, capacité tout-ou-rien et préservation de l'état de durabilité ; erreurs et tests de non-mutation en cas d'échec. `Transferer` est dispatchée par le host ; l'accord SYNE reste hors PRISM. |
| PRISM-069 | Implémentée (socle) | Test déterministe d'un million de commandes aléatoires de transfert/consommation ; audit global périodique des stocks des agents contre le ledger, sans violation. La génération n'exerce pas encore toutes les primitives/actions. |
| PRISM-070 | Implémentée (socle) | Types versionnés de commande, événement, observation et lockstep tick ; sérialisation JSON camelCase, champs inconnus ignorés, validation de version majeure et cohérence des ticks. Schémas JSON publiés pour enveloppes et échanges lockstep ; validation de contrat des payloads par commande implémentée (`CommandPayloadValidator`, `invalid_payload`) et testée. |
| PRISM-071 | Implémentée (socle) | Fenêtre idempotente bornée par capacité (100 000 par défaut), replay renvoie le résultat mémorisé, réutilisation d'un ID avec requête différente refusée, et échec d'exécution non mémorisé. La fenêtre est sérialisée/restaurée dans le checkpoint SQLite du host. |
| PRISM-072 | Implémentée | `IWorldHost.Step` valide et copie la requête, impose des ticks consécutifs et exécute exactement une fois chaque handler dans l'ordre des 12 phases de `SIMULATION_LOOP.md`. Réception/idempotence, dispatch, rejouabilité, échecs motivés, conflits d'ID, panne après exécution partielle et ordre de sortie sont couverts par tests. Le branchement de systèmes métier supplémentaires aux handlers de phase relève des composants/étapes d'intégration ultérieurs ; il ne fait pas partie du critère d'acceptation de l'orchestrateur. |
| PRISM-073 | Implémentée (socle) | Exécutable host HTTP loopback : readiness, info, health, status et cycle prepare/start/pause/resume/stop/shutdown ; routes mutantes protégées par jeton de session. Les arguments et intégrations de cycle de vie du Launcher restent à finaliser. |
| PRISM-074 | Implémentée (socle) | WebSocket textuel `/data/lockstep` avec validation/rejeu de `TickRequest`, et `/data/read` en lecture seule. Tests HTTP et WebSocket ciblés ajoutés, dont une parité de contrat sur les deux liaisons (payload invalide, commande inconnue, version majeure) ; matrice exhaustive par commande à faire. |
| PRISM-075 | Partielle | Avec un pack, le flux borné publie un snapshot initial, des deltas complets des régions modifiées, les snapshots d'entités et les observations différentielles filtrées par agent (état de soi et plafond). Checksums périodiques et resnapshot pour lecteurs lents sont testés. Message `metrics` implémenté (stocks agents/conteneurs/sol, matière par `ruleId`, chantiers, réservations, effets actifs, échecs par raison) et exposé par `/metrics` ; filtrage des chantiers selon les droits et compression restent à faire. |
| PRISM-076 | Partielle | Store SQLite v2 migrable depuis v1 ; persiste/restaure le monde, l'orchestrateur, l'idempotence, les chantiers/gabarits et le cycle de vie (règles de déplacement, agents/inventaires, objets au sol/IDs, historique différentiel de perception), avec vérification du `contentHash` et de l'empreinte. Le checkpoint inclut aussi le ledger, les réservations de stations et la roue d'événements ; la définition de contenu est conservée pour reconstruire les règles d'action. Cadence automatique (500 ticks par défaut), rotation (3), arrêt propre et restauration de checkpoints anciens sans phases optionnelles sont testés ; un test continu/restauré vérifie l'égalité des résultats de tick, de l'empreinte et des entités au milieu d'un déplacement, avec ordres `Aller` checkpointés. Le point couplé SYNE reste à faire. |
| PRISM-080 | Partielle | `prism/component.json` déclare le host et son endpoint WebSocket `/data/read`, sur un port résolu par le Launcher ; le CLI et l'hôte acceptent `--read-port`. Le chemin d'endpoint est maintenant consommé et testé côté Launcher. Validateur de manifeste vert ; script de publication autonome ajouté (`prism/scripts/publish.sh`, hôte et viewer, RID paramétrable) ; exécution et test de lancement sur Windows/Linux restent différés. |
| SYNE-220/221 | Socle d'adaptateur côté transport | `PrismReadStreamClient` consomme le flux `/data/read`, valide versions/séquences et restitue les observations par agent ; la bascule du monde et l'application de ces observations à la cognition SYNE restent ouvertes. |
| PRISM-078 | Partielle | Tests HTTP et WebSocket couvrent les champs inconnus ignorés, le rejet motivé d'une version majeure incompatible et la continuité du tick ; un test vérifie désormais le même code d'échec (payload invalide, commande inconnue) sur les liaisons embarquée, HTTP et WebSocket. La matrice exhaustive par commande reste à faire. |
| PRISM-090 | Implémentée (socle) | Compilation runtime des gabarits du pack et validation déterministe des limites, du terrain plat, des volumes libres et des erreurs motivées. |
| PRISM-091 | Partielle | Chantiers déterministes avec réservation anti-chevauchement, contributions de plusieurs agents par tick, pose progressive canonique, abandon et débit atomique des matériaux du gabarit au prorata du travail. `DemarrerChantier`/`Contribuer` sont raccordées au dispatch du host ; progression, gabarits, matériaux consommés/manquants et blocs sont checkpointés/restaurés. Perception des chantiers implémentée (entrée `ConstructionSite` portant `siteId`, état, travail requis/achevé, suivant le diff apparu/modifié/disparu) et raccordée à la phase de perception du host ; nettoyage des sites abandonnés reste à faire. |
| PRISM-092 | Partielle | Effets compilés avec leur volume local, intégrité des blocs calculée paresseusement et seuls les effets au-dessus du seuil sont énumérés comme actifs ; gabarits/sites sont checkpointés par le host. Un consommateur d'abri (`shelter`) applique les effets aux agents : un agent dans le volume actif reçoit `sheltered`, `shelterSiteId` et le bonus de récupération dans son auto-observation, sous réserve du droit `use` (propriétaire toujours autorisé). Les autres types d'effets et les événements de dommage restent à faire. |
| PRISM-093 | Partielle | Propriétaire et ACL agent/groupe sur les chantiers ; `DefinirAcces` est dispatchée par le host, `build` est contrôlé à la contribution et les droits survivent au checkpoint/restauration. L'usage `use` est désormais appliqué concrètement par la consommation d'effets d'abri (propriétaire ou droit `use` requis), en plus de `build` à la contribution ; les groupes sont résolus par `GroupMembership` (appartenance opaque fournie par le plan de contrôle) ; les autres usages (stockage, production) restent à raccorder. |
| PRISM-110 | Implémentée (socle) | Meshing glouton des surfaces/façades, mise en cache par région et invalidation des régions adjacentes aux frontières ; le viewer traite un nombre borné de régions par image. Atlas, autres couches de rendu et B8 restent à faire. |
| PRISM-111 | Partielle | Occlusion ambiante topologique et matériaux procéduraux amorcés ; atlas et synchronisation jour/nuit manquent encore. |
| PRISM-112 | Partielle | Silhouettes voxel d'agents de démonstration visibles ; actions, chantiers fantômes et données d'entités live restent à raccorder. |
| PRISM-113 | Partielle | Caméras orbite et libre, déplacement/panoramique, rotation et zoom ; API de suivi par ID testée, mais pas de liaison au flux d'entités ni de vue en coupe. |
| PRISM-114 | Partielle | Inspecteur statique des agents de démonstration et calque de zones disponibles ; inspection du monde et les sept calques attendus ne sont pas terminés. |
| PRISM-029, 047–049, 053, 057–059, 077, 079, 081–082, 094–109, 115–119, 120–135 | À faire | Le reste des issues demeure ouvert. G1/G2/G3/G4/G5/G6 ne sont pas franchies. Benchmarks, calibrage et validation Windows/Linux restent différés jusqu'à une phase ultérieure, selon la priorité du propriétaire. |

**Validation locale du 3 novembre 2026 :** PRISM **326/326 tests verts** (Core 172, Viewer 55, Content 35, Host 42, Contracts 14, Architecture 8) et build solution vert sans avertissement. Cette passe termine la résolution différée des cinq actions à durée (`Miner`, `Fabriquer`, `Labourer`, `Planter`, `Recolter`) : le dispatch les résout à l'échéance et renvoie le résultat à l'étape macro en attente (`macro_step_deferred`, canal `DeferredActionResolutions`), qui n'est validée qu'avec les quantités réellement produites. Les actions différées déclenchées par une macro sont désormais inscrites au dispatch (`CommandDispatchPhase.Schedule`) et tout l'état en attente survit au checkpoint (format de dispatch v3, qui relit v1 et v2). Elle conserve les acquis de la passe précédente : ordres `Aller`, perception différentielle des chantiers, consommation d'effets d'abri sous ACL (agents et groupes), fraîcheur portée par les piles (fusion, décroissance en conteneur, balayage d'inventaire avec `item_spoiled`), occupation des agents (`agent_busy`) et checkpoint de la fenêtre d'idempotence. Les suites SYNE, ECHOS et Launcher n'ont pas été réexécutées dans cette passe. Aucun benchmark, calibrage ni comparaison Windows/Linux n'a été exécuté.

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
| PRISM-010 | Prototype jetable : colonnes 2.5D + générateur de surface | perf | P0 | 002 | Carte de surface 256×256 générée ; génération séquentielle et parallèle identiques ; code marqué jetable |
| PRISM-011 | B1 — pathfinding 2.5D, 1 000 agents | perf | P0 | 010 | Rapport initial publié ; zéro échec, transitions mesurées ; sous le poste indicatif de 90 ms ; hiérarchie/cache à évaluer |
| PRISM-012 | B2 — perception trois niveaux, 1 000 agents | perf | P0 | 010 | Rapport initial publié : calcul, taille JSON et positions globales ; écarts et plafond à mesurer |
| PRISM-013 | B3 — croissance : programmée vs aléatoire, monde cultivé | perf | P0 | 010 | Rapport comparatif initial et recommandation Q-09 publiés ; conditions de culture et parallélisme restent à mesurer |
| PRISM-014 | B4 — débit lockstep WebSocket JSON et liaison embarquée | perf, contract | P0 | 010 | Rapport initial avec vrai WebSocket et comparaison embarquée publié ; réponse d'observation complète et allocations à mesurer avant décision transport |
| PRISM-015 | B5 — diffusion de régions, JSON et compression BCL | perf | P1 | 010 | Rapport initial publié : snapshots et deltas JSON/Deflate/Brotli ; charge réaliste et limites documentées |
| PRISM-016 | Spike Silk.NET : fenêtre et région 2.5D maillée, Windows + Linux | viewer | P0 | 002 | Capture des surfaces et façades sur les deux OS |
| PRISM-017 | B8 — meshing et dessin d'un monde complet | perf, viewer | P1 | 016 | Rapport : ms/chunk, images/s |
| PRISM-018 | Rapport de porte G1 et budgets 2.5D/transport | docs | P0 | 011–017, SYNE-200 | Rapport versionné ; ADR-011/012 validés par les mesures |

### Milestone ph2 — Noyau du monde

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| PRISM-020 | Registres d'identifiants stables (blocs, objets, tags, recettes) | core | P0 | 018 | Round-trip clé ↔ identifiant ; stabilité déterministe sur un ensemble de clés identique |
| PRISM-021 | Compilateur et validateur de contenu (schémas JSON, graphe de recettes) | content | P0 | 020 | Erreurs structurées complètes ; pack invalide = démarrage refusé |
| PRISM-022 | Pack de contenu minimal v0 (`CONTENT_SPEC.md` §5) | content | P0 | 021 | Valide ; graphe amorçable à mains nues (test) |
| PRISM-023 | Grille et régions 2.5D (colonnes SoA uniformes, surcharges, *dirty flags*) | core | P0 | 020 | Accès/bornes testés ; région uniforme compacte ; surcharges canoniques ; environ 384 Kio de métadonnées denses à 6 octets/colonne pour 256 × 256 |
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
| PRISM-052 | A* 2.5D entier borné + cache de chemins | core | P0 | 051 | Optimal sur le graphe de surface ; déterministe ; budget de nœuds |
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
| 3 novembre 2026 | Statuts partiels P11 et preuves de validation locale précisés | Revue fonctionnelle P1–P11 ; validations différées conservées |
| 2 novembre 2026 | État d'exécution et références des issues GitHub ajoutés | Démarrage PRISM V2 |
| 1er novembre 2026 | Création | Refonte V2 |
