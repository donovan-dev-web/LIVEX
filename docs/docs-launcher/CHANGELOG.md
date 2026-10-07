# CHANGELOG — LAUNCHER

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versionnement :
SemVer (`launcher-vX.Y.Z`).

Le Launcher n'a pas encore de version livrée. L'implémentation progresse jalons par
jalons : G1 → G4 livrés, G5 → G7 réalisés côté Launcher contre le banc de stubs, les
portes externes P1 – P5 restant de côté des composants.

## [Unreleased]

### Changed
- **Refonte P3 de la fenêtre d'analyse — la viabilité devient le premier écran
  (USER_INTERFACE.md §9.2, ADR-007)** : réponse au plan `RAPPORT-ANALYSE-ECHOS-LAUNCHER.md`
  (P0 → P3).
  - *Sous-écrans réorganisés autour des questions* : **Viabilité** (défaut : issue,
    populations initiale/finales/minimum, premier tick nul, ticks manquants,
    conservation, pente d'énergie et décisions sous faim > 70 étiquetées « rapport
    post-run », chronologie d'extinction), **Comportements**, **Statistiques exactes**,
    **Comparer**, **Relations et groupes**, **Monde et territoires**, **Entités**.
  - *Courbes en ticks réels* : axe sur les ticks publiés (plus d'axe d'index), trous
    visibles (`null` + `measured_by_tick`), panneaux séparés par unité (population,
    besoins, réserves ne partagent jamais un axe).
  - *Radar et histogramme retirés* de la vue principale (normalisation implicite et
    comparabilité non garanties) ; `RadarChartControl` supprimé. Valeurs exactes en
    cartes numériques et tableau.
  - *Barre de relecture* : curseur de tick, lecture/pause, précédent/suivant, retour
    au direct, vitesse en ticks/seconde (aucun effet sur SYNE) ; la relecture tronque
    les séries publiées sans nouvelle lecture et synchronise les panneaux monde/entités
    sur le tick du curseur (« Suivre le direct » ≠ « Relire ce run »).
  - *Catalogue, viabilité et comparaison consommés* : port `IEchosTelemetrySource`
    élargi (`ReadCatalogAsync`, `ReadViabilityAsync`, `ReadExperimentSummaryAsync`) ;
    libellés, unités, plages, statuts et avertissements viennent d'ECHOS ; comparaison
    refusée explicitement avec moins de 2 runs et plafonnée à 6.
  - Chaque phénomène affiche « signal selon la règle X » (valeur observée + seuil) ;
    les mesures publiées portent unité, fenêtre, statut et provenance, et
    « non mesuré » n'est jamais rendu comme `0`.
  - Tests : 20 tests unitaires de la fenêtre d'analyse (viabilité, trous de données,
    relecture, synchronisation 2D, comparaison, signaux, catalogue) — suite unitaire
    **153/153** ; captures headless des 7 écrans via `tools/UiScreenshot --analysis <section>`.
- **Derniers manques P3 comblés (B1 → B4, `RAPPORT-ANALYSE-ECHOS-LAUNCHER.md`)** —
  quatre ajouts de **rendu** uniquement, aucun calcul scientifique (ADR-003) :
  - *B1 — Marqueurs d'événements* : case à cocher sous la courbe ; une ligne
    verticale par tick portant au moins un événement (`ReadEventsAsync` sur le port
    `IEchosTelemetrySource`, nouvel endpoint ECHOS `/api/runs/{id}/events`), couleur
    par type (mort = rouge, groupe = violet, reste = gris), **bornée à 16 marqueurs**
    et à la fenêtre affichée, groupée par tick. Compte-rendu « N marqueur(s) affiché(s)
    sur M événement(s) publié(s) · types » — une borne de rendu n'est jamais présentée
    comme l'intégralité ; le journal d'un autre run n'est jamais reporté.
  - *B2 — Âge des contextes échantillonnés* : la fiche d'entité affiche la cadence
    publiée (`conservation.sampledDetails.agentContextEvery`, port `AgentContextEvery`)
    et le nombre de ticks derrière le dernier tick observé — métadonnées côte à côte,
    jamais interpolées en donnée mesurée.
  - *B3 — État du run* : ligne « État du run » de l'écran Viabilité décrite depuis
    les faits publiés (`outcome`, `extinction_tick`, `last_tick`, `missing_tick_count`)
    : terminé/extinction, « possiblement en cours », incomplet si ticks manquants —
    avec la note « ECHOS ne distingue pas en cours d'interrompu ».
  - *B4 — Outil de distribution* : sous Statistiques, après le retrait de
    l'histogramme — ComboBox de métrique + distribution en **intervalles explicites**
    (règle de Sturges `⌈log₂ n⌉+1`, bornée [2, 12]) avec effectif publié par intervalle ;
    `null` et `measured = false` exclus et comptés (« N valeur(s) exclue(s) »), zéro
    remplacement par 0, zéro extrapolation. Comptage de classe = transformation de
    rendu bornée (ADR-003).
  - *Correction de relecture* : le curseur **suit le direct** quand il était au bord
    du flux et que de nouveaux ticks sont publiés (au lieu de rester figé sur
    l'ancien dernier tick, ce qui gelait la fenêtre d'affichage).
  - Port : `ReadEventsAsync` → `EchosEventFeed` (record `RunId/Total/Limit/Types/Events`),
    `EchosViability.LastTick`, `EchosRunSummary/EchosRunOption.AgentContextEvery`.
  - Tests : 4 tests unitaires supplémentaires (état du run, âge de contexte,
    distribution, marqueurs bornés/désactivables) — suite unitaire **157/157** ;
    2 tests d'endpoint côté ECHOS (`test_api_routes.py`).

### Added
- **Consoles de logs natives par composant (USER_INTERFACE.md §9, ADR-007)** : le
  gestionnaire de processus publie chaque ligne de `stdout`/`stderr` (séquence globale)
  en plus du fichier de journal — désormais écrit avec `AutoFlush`, donc suivable en direct.
  Un tampon mémoire borné (20 000 lignes par instance) les conserve pour l'affichage.
  Chaque composant démarré ouvre sa **console native** (fenêtre Avalonia par instance) :
  SYNE réel ou émulé selon le moteur choisi, plus ECHOS. Bouton « Console » sur les cartes
  et dans Monitoring pour une ouverture à la demande. Horodatage, canal, texte — sans
  interprétation ; pause, filtres `stdout`/`stderr`, effacement, défilement automatique,
  ouverture du dossier des journaux. Les consoles ne prolongent pas la vie de l'application.
- **Fenêtre d'analyse native du Launcher (USER_INTERFACE.md §9.2, ADR-007)** :
  seconde fenêtre du Launcher, ouverte par le bouton « Ouvrir la fenêtre d'analyse » de
  la vue Analyse, qui sonde l'API REST d'ECHOS (≈1 s, direct activable) et présente
  l'analyse en direct : choix du run et du pas de lecture, courbes et histogrammes
  (LiveCharts2), radar et graphe relationnel **dessinés nativement** (LiveCharts2 2.0.5
  n'offre pas de radar), métriques sélectionnables avec leur dernière valeur, tableau des
  phénomènes émergents et disclaimer méthodologique affiché tel quel. Aucune valeur n'est
  recalculée : le comptage d'histogramme et l'échelle du radar sont des transformations de
  rendu bornées (ADR-003). ECHOS injoignable est affiché, jamais approximé.
- **Menu de sous-écrans de la fenêtre d'analyse (USER_INTERFACE.md §9.2)** : la fenêtre
  propose désormais cinq écrans — vue d'ensemble, **statistiques exactes**, **confiance**,
  **monde 2D**, **fiches d'entités** — un relevé ciblé étant lancé pour l'écran actif.
  - *Statistiques exactes* : métadonnées du run (graine, ticks, issue, extinction) et
    tableau moteur/métrique/valeur **telles que publiées**, provenance comprise
    (« repli neutre » pour un repli, jamais présenté comme une mesure).
  - *Confiance* : graphe natif des entités à leur position publiée, arêtes = relations
    `trust` rendues avec épaisseur/opacité proportionnelles au niveau publié, plus le
    tableau source/cible/confiance (affichage borné à 200 lignes).
  - *Monde 2D* : rendu natif de la description publiée à l'initialisation (cellules de
    terrain, obstacles, ressources initiales, régions) + entités et réserves du tick
    choisi via `GET /api/world`.
  - *Fiches d'entités* : liste des entités observées, puis croyances, relations de
    confiance et 12 dernières décisions publiées pour l'entité sélectionnée — une fiche
    indisponible est affichée sans vider l'écran.
  - Port `IEchosTelemetrySource` élargi (`ReadWorldAsync`, `ReadTrustGraphAsync`,
    `ReadRunDetailAsync`, `ReadAgentProfileAsync`, `ReadDecisionsAsync`) — le Launcher
    ne fait que rendre (ADR-003). 5 tests unitaires supplémentaires, captures headless
    des 5 écrans via `tools/UiScreenshot --analysis`.

### Removed
- **Interface web et shell Electron d'ECHOS retirés (ADR-007)** : `echos-ui/` et
  `echos-desktop/` supprimés, montage statique de FastAPI (`ui_dist`, `SpaStaticFiles`,
  `ECHOS_UI_DIST`) supprimé, job CI `echos-ui` et workflow `echos-desktop.yml` retirés,
  `scripts/dev-stack-electron.sh` supprimé. ECHOS est une **API seule** : le Launcher
  présente, y compris en direct. Voir `adr/ADR-007-consoles-et-fenetre-analyse-natives.md`.

### Added
- **Chaîne batch rejouable SYNE → ECHOS → Launcher (J2B)** : chaque run supervisé est
  lancé avec `--run-id {campagne}-{run}` et `--export-stream`, ce qui produit
  `data/stream.jsonl` à côté de `result.json` ; le flux est archivé dans le `.livexp`.
  ECHOS expose `POST /ingest/run` pour enregistrer un run archivé, et le Launcher l'appelle
  avant `AnalyzeRun` : l'analyse porte donc sur des données réellement produites. L'identité
  `{campagne}-{run}` évite que deux campagnes se chevauchent dans la base analytique ;
  l'ingestion est idempotente par refus et vérifie l'intégrité du flux contre
  `result.json`. La réanalyse faite plus tard depuis le seul paquet produit les mêmes
  octets que celle de la campagne, ce qui a exigé d'exclure le contexte `profiling`
  (durées de calcul) du rapport déterministe côté ECHOS.
- **Progression de run lue depuis le moteur** : `ProcessRunExecutor` sonde
  `GET /api/control/status` sur le port de contrôle toutes les 200 ms et publie
  `IRunExecutor.TickProgress` (`tick`, `maxTicks`, `aliveCount`, `state`). `CampaignRunner`
  relaie l'événement avec le contexte de campagne, et l'interface affiche une barre
  d'avancement. Le contrat documentaire annonçait cette progression depuis le tic de
  simulation, mais rien ne la_branchait : elle était affichée nulle part.

  La sonde est *best-effort* et ne fait jamais échouer un run. Aucun pourcentage n'est
  affiché sans `maxTicks` rapporté : une barre à 0 % pour un moteur muet afficherait une
  progression qui n'existe pas. À l'inverse, si le moteur n'expose pas la route, l'interface
  reste vide plutôt que d'inventer une mesure. La lecture ne modifie jamais l'état du
  moteur : c'est de la supervision, pas une commande.

### Fixed
- **Revue de correction du cœur du Launcher (6 octobre 2026)** — douze correctifs, revérifiés
  par `Launcher.sln` (**0 avertissement / 0 erreur**), **148 tests unitaires, 15 tests
  d'intégration et 28 tests bout en bout** verts, et `livex-launcher --check` en sortie 0 :
  - **Détection hermétique réellement fermée** : `DetectComponents()` appelait `Detect()`, qui
    consulte les sources standard de la machine (application, `LIVEX_HOME`, profil utilisateur,
    registre) même lorsque la composition est construite sur une racine explicite — c'était
    l'échec réel du test `Profils_resolus_et_verrou_immersion`. Construction et redétection
    passent désormais par une même méthode qui ne consulte les sources standard que si la
    composition les autorise ; `Redetection_reste_hermétique_à_la_racine_explicite` verrouille
    la non-régression.
  - **Course démarrage/sortie d'un composant** : l'abonnement à `Exited` avait lieu après
    `StartAsync`, si bien qu'un composant mourant immédiatement laissait une instance fantôme
    et un port verrouillé pour le reste de la session. L'abonnement précède le démarrage et
    l'état de sortie est partagé et traité une seule fois (`EarlyExitEndToEndTests`).
  - **Délai de grâce à l'arrêt** : `ComponentInstallation.ShutdownGrace` lit
    `timeouts.shutdownMs` du manifeste (défaut 15 s) et borne désormais l'arrêt manuel,
    l'annulation d'un run (`ProcessRunExecutor`) et la fermeture de l'application — le
    `Dispose()` de la façade arrête les instances en parallèle, borné au plus long délai
    majoré de 2 s.
  - **Reprise d'une campagne** : `CampaignRunner.RunLoopAsync` recomptait les runs déjà
    terminés à zéro (progression 4/5 affichée 0/5) ; le compteur part désormais de
    `doneRunIds.Count`.
  - **Stratégie de graines `random`** : `ExperimentDefinition.Validate()` refuse
    `SeedStrategy.Random` sans liste `ExplicitSeeds`, et `SeedDeriver.Derive` lit les graines
    enregistrées au lieu de planter en boucle.
  - **Écran bloqué sur « Reprise de la campagne… »** : `MainWindowViewModel.RunCampaignAsync`
    ne traitait ni `CampaignStoppedException` ni les erreurs imprévues ; les deux branches
    existent désormais, l'interface ne peut plus rester figée sur cette mention.
  - **Sécurité des paquets `.livexp`** : `PackageEntryRules` (`IsSafe`/`EnsureSafe`) refuse les
    noms à extension exécutable, à l'écriture (`LivexPackageWriter.WriteEntry` et
    `WriteEntryLocked`) comme à la lecture (`LivexPackageReader`) ; `EchosAnalysisService`
    écarte un tel nom dans `DecodeFiles` et annonce « analyse indisponible ».
  - **Scellement atomique** : `LivexPackageWriter.Seal` écrit dans un fichier temporaire
    `.sealing.tmp` puis déplace, au lieu d'ouvrir le paquet vivant en `FileMode.Create` — une
    interruption pendant le scellement ne détruit plus le paquet. Le champ mort `GlobalLocks`
    a été supprimé.
  - **Surface HTTP du Launcher** : `MiniHttpServer` lit le corps en boucle (plafond 1 Mo →
    413), tolère les paramètres de query répétés, ne partage plus `contentType` entre requêtes
    et enferme ses handlers dans un `try/catch` (500 via `WriteResponseAsync`) — une requête
    malformée ne tue plus le serveur d'observabilité.
  - **Détection des manifestes** : dans `ManifestDetector.DetectFromRoots`, une installation
    invalide ne masque plus une installation valide du même type (avec journal Warn).
  - **`OrchestrationService.Adopt`** : une seule énumération de la session, et parenthèses
    explicites sur la condition mêlant `allowStubs` et l'usage du moteur émulé.
  - **Charge de l'interface** : `OrchestrationFacade.ListExperiences` a un statut par défaut
    unique (« En cours »), et `RecentLogs` cache par signature (chemin + taille) au lieu de
    relire trois fichiers entiers toutes les 800 ms.

  Tests de non-régression ajoutés : `EarlyExitEndToEndTests` (course démarrage/sortie),
  graines `random` tirées/enregistrées et définition `random` sans graines refusée, refus en
  lecture d'un paquet contenant un exécutable (`P8_lecture_refuse_un_paquet_contenant_un_exécutable`),
  progression de reprise comptant les runs déjà terminés, corps de requête POST reçu entier,
  requête au-delà du plafond → 413 (réécrite en `TcpClient` brut), délai de grâce venu du
  manifeste, redétection hermétique à la racine explicite.

- **`config.resolved.json` n'attestait plus la configuration appliquée** : le document
  écrit dans chaque `.livexp` ne contenait que la définition sérialisée de l'expérience,
  dupliquée par `experiment.json`. Rien n'y attestait la graine dérivée, ni la surcouche
  réellement transmise au moteur : deux runs de la même campagne étaient indiscernables,
  alors que la reproductibilité est la raison d'être du fichier.

  Le document porte désormais les trois niveaux qui pourraient diverger — la définition
  demandée (`campaign`), les paramètres résolus du run (`run`, avec la graine dérivée
  effective) et ce que le Launcher a transmis (`engine`, avec la surcouche
  `configOverlay`). La surcouche archivée et celle écrite dans `launcher-config.json`
  sortent désormais du même type (`RunEngineProfile`) construit à partir du seul
  `RunSpec` : le paquet ne peut plus attester une configuration différente de celle qui a
  été appliquée. Les valeurs de transport — port, jeton, corrélation, horodatages,
  chemins — restent volontairement absentes, sans quoi deux paquets d'une même campagne
  cesseraient d'être identiques octet pour octet.

- **Une campagne ne rejouait plus ses runs en temps réel** : le moteur cadence ses ticks
  par `Task.Delay(1s / TicksPerSecond)` (`SimulationController`), et le Launcher ne
  surcouhait que `agents.initialCount` — donc le défaut de 10 ticks/s s'appliquait, soit un
  plancher de `ticks / 10` secondes : la campagne de référence à 1000 ticks durait 100 s,
  pendant lesquelles l'interface n'affichait aucune progression (mode `--headless`) et le
  processus dormait 99 % du temps. Un run batch n'a pas d'observateur : rien ne justifie
  de le rejouer en temps réel. `ProcessRunExecutor` écrit désormais
  `simulation.ticksPerSecond = 1000` dans la surcouche `launcher-config.json`, que
  `ConfigLoader` fusionne sur les défauts intégrés sans toucher au reste du profil. La
  cadence seule change : même campagne, même graine ⇒ `stateChecksum` identique
  (`0x8b3fc2fb7f5b4b95` sur le profil de référence à 1000 ticks) et `stream.jsonl`
  identique à la ligne près, la seule divergence étant la valeur de `ticksPerSecond`
  recopiée dans l'enregistrement de provenance `world_initialized`. Mesuré sur la campagne
  de référence : 100,55 s → 1,96 s. Au-delà de 1000 ticks/s le gain devient marginal, la
  résolution du timer plafonnant le délai aux alentours de 1 ms.
- **Une campagne peut s'exécuter pendant qu'une instance interactive tourne** : le port
  déclaré au manifeste était refusé dès qu'une autre instance de la session le détenait
  (« port déclaré 5181 du manifeste déjà alloué dans cette session »), alors que le
  multi-instance est précisément ce que l'espace d'adressage interne doit absorber
  (`NETWORK.md` §6.2, §9.3). Le repli sur la plage interne, promis par `Resolve` et jamais
  atteint, est appliqué : un port déclaré libre reste prioritaire, un port déjà pris par
  une instance de la session bascule dans la plage, et seul un occupant étranger reste un
  refus explicite — l'opérateur doit pouvoir l'identifier. Résoudre deux fois le même
  point pour la même instance rend désormais la même adresse au lieu d'échouer au
  pré-vol. Toute campagne lancée depuis l'interface, où l'opérateur a laissé SYNE et ECHOS
  démarrés, échouait ainsi à son premier run.
- **Une ingestion d'archive échouée ne condamne plus l'archive** : ECHOS valide et
  commite chaque tick séparément, donc un flux invalidé à mi-parcours laissait les
  segments déjà écrits en base. Le run se retrouvait à la fois tronqué et protégé par la
  garde anti-doublon : la réingestion de l'archive valide était refusée pour une base ne
  contenant que la moitié du run. Une ingestion en échec purge maintenant ce qu'elle a
  écrit, sans toucher un run réellement peuplé — un flux du service direct peut partager
  la même base.
- **Un `tick_summary` répété est refusé** : il trahit un flux réécrit ou fusionné à tort, et
  le résumé était écrasé sans trace. Le contrôle portait sur un ensemble de ticks, qui
  fusionnait silencieusement les doublons.
- **L'identité enregistrée est constatée, pas supposée** : ECHOS fait autorité sur
  l'identité du flux, et le Launcher vérifie désormais que c'est bien celle qu'il va
  analyser. Un dossier de run mal apparié est nommé au lieu de ressortir plus tard comme
  un « run inconnu » opaque. Le stub ECHOS lit lui aussi l'identité dans le flux : il
  renvoyait le `runId` demandé, ce qui aurait masqué précisément ce défaut.
- **Configuration et journaux de run** : sélection explicite de l'installation/version active ;
  ajout d'un dossier via inspection de `component.json`, persistance des racines et des
  choix actifs dans `~/.livex/` ; au démarrage standard, détection des racines déclarées et
  du registre utilisateur. L'écran Logs liste et lit les fichiers de run d'un paquet sans
  extraction, limite l'affichage à 16 Mio et exporte en flux les fichiers jusqu'à 256 Mio
  via un fichier temporaire atomique. Les tests couvrent les chemins autorisés, le refus
  au-delà du plafond d'affichage et l'export borné.
- **Limites rendues visibles** : Configuration indique que les installations ne déclarent
  pas encore de variantes UI/headless sélectionnables ; Monitoring signale que les composants
  actuels n'exposent pas de métrique compatible pour les flux, sans fabriquer de valeurs.
- **Intégration composant Linux** : ECHOS et `syne-mock` livrent un `component.json` et
  adaptent leurs paramètres au port alloué. Les deux déclarent une readiness réelle ; ECHOS
  reste non prêt sans sa base analytique. L'arrêt des deux services exige le jeton de
  session et ferme proprement les serveurs. `syne-mock` ferme ses serveurs aussi sur
  SIGINT/SIGTERM. SYNE dispose maintenant d'un manifeste Linux, d'un service supervisé,
  d'un batch `reference` et d'un export déterministe ; ses tests de processus valident le
  batch, l'arrêt authentifié et le conflit de port. Le défaut du Launcher est maintenant
  aligné sur `reference` et les autres identifiants sont refusés dans le formulaire ;
  l'acceptation depuis une installation publiée et l'intégration de collecte restent
  ouvertes. L'analyse d'expérience transmet à ECHOS un `experiment.json` atomiquement écrit
  qui ne liste que les runs terminés ; l'ingestion analytique elle-même reste à intégrer.
- L'état global du monitoring agrège maintenant les composants réellement requis par le
  profil actif (dont `syne-mock` en Développement), et non SYNE par défaut.
- Bancs verts après ces changements : **86 tests unitaires + 7 d'intégration + 19 bout en
  bout côté Launcher, 310 tests ECHOS (2 ignorés) et 46 tests syne-mock**. La compatibilité
  scientifique SYNE/ECHOS/syne-mock reste une porte distincte
  des tests contre les stubs.
- Suite SYNE complète Release : **514 tests Core + 67 tests Console** ; un test de processus
  confirme qu'un `--export-dir` extérieur au workspace est refusé avant toute écriture.
- **Réalisation G5 → G7 — session complète, diagnostic de livraison, déverrouillage Immersion**
  (composants simulés ; les portes P1 – P5 restent externes) :
  - **G5 — session complète avec ECHOS** : `EchosAnalysisService` réalise `IAnalysisService`
    selon `INTEGRATION_CONTRACT.md` §10.1 — `POST /analysis/run`, `/analysis/experiment`,
    `/analysis/report` sur le port de contrôle (instance en cours → port résolu publié au
    registre, sinon port déclaré au manifeste, sinon 5000), fichiers base64 transportés
    tels quels (`ADR-003` : le Launcher demande, ECHOS produit, rien n'est recalculé).
    `Stub.Echos` implémente les trois chemins avec un contenu déterministe et tient un
    journal de ses demandes (`analysis/requests.log`). Isolation : l'échec de l'analyse
    expérimentale ne fait ni échouer ni rouvrir la campagne — l'absence est consignée.
    Scénario E2E « session complète » : détection → profil satisfiable → ECHOS démarré par
    le cycle de vie réel → deux runs Stub.Syne → rapport archivé **identique octet à octet**
    à la réponse d'ECHOS → affiché tel quel par la vue Analyse ; ECHOS arrêté, la campagne
    suivante se scelle entièrement sans rapport (un mode défaillant ne dégrade pas l'autre).
  - **G6 — `--check` complet (`PACKAGING.md` §6)** : les huit vérifications du tableau dans
    l'ordre — exécution, droits d'écriture, espace disque, version du format (schéma connu
    de tous les paquets présents), composants, moteur, ports (conflit nommé), navigateur
    (information non bloquante) — avec les causes attendues et une sortie textuelle stable
    `[OK  ]/`[ÉCHEC] nom : détail` extraite en `EnvironmentChecker.Format`. Banc hermétique
    par racines explicites. Restent hors code : l'installation Windows/Linux et la cible de
    72 h, conditions physiques du jalon.
  - **G7 — déverrouillage du mode Immersion, condition évaluée** : `PrismRequirements`
    vérifie au manifeste les exigences §11.1 (mode porteur, `snapshotStream`,
    `renderCadence`, point de contrôle) ; la cause exacte et le jalon alimentent la
    résolution de profil, la carte PRISM et la vue Configuration — jamais de constante
    (`ADR-006`). Arguments de démarrage génériques : communs §3.1 pour tout composant,
    arguments de lot SYNE (`--simulation --seed --ticks --autostart`) réservés au seul
    moteur. `Stub.Prism` livré (§13) : endpoints §6, client WebSocket factice (`--ws-url`),
    `args.txt` pour vérifier les arguments reçus. Mode Immersion **sélectionnable** :
    profil choisi dans Configuration avec statut dynamique et boutons Démarrer/Arrêter
    (ordre de résolution, arrêt en inverse strict), verrou réévalué à chaque rafraîchissement.
  - **Supervision et compatibilité avec l'existant** : boucle de sondes câblée dans la
    composition (Démarrage → Prêt, trois sondes manquées = perte de contact, délai de
    démarrage selon `timeouts.startupMs`) — les états transitaient réellement ; `health.path`
    et `health.intervalMs` du manifeste honorés (un ECHOS réel déclarant `/health` est
    sondable) ; les stubs sortent en code 3 sur port occupé (§4) au lieu d'un crash 134 ;
    un run échoué libère systématiquement son port et son entrée de registre. L'audit initial
    de compatibilité stubs ↔ composants réels avait consigné les écarts en `ISSUES.md`
    **O-38 à O-41**. Depuis, J2 a ajouté le manifeste Linux et le cycle de service SYNE ainsi
    que les arguments communs ; le défaut `reference` est aligné et les autres scénarios
    sont refusés dans le formulaire. Restent l'acceptation d'une installation publiée et
    l'intégration des données d'analyse.
  - Bancs verts à cette étape : **63 unitaires + 7 intégration + 10 bout en bout = 80 tests** — dont
    analyse HTTP, isolation de fin de campagne, huit vérifications `--check`, exigences
    PRISM, sélection de profil, session complète E2E et déverrouillage E2E. Les classes de
    composition partagent une collection xUnit : l'allocation de ports de la plage interne
    est sérialisée entre bancs.
- **Réalisation G1 → G4** : première implémentation du Launcher, application .NET 10 /
  Avalonia dans `launcher/` (racine du dépôt), conformément au présent corpus et aux six ADR.
  Format de paquet `.livexp` avec les dix propriétés de `TESTING.md` §5 en tests (dont le
  déterminisme octet pour octet) ; orchestration sans interface (machine à états, registre,
  profils, sondes, processus, confinement) ; campagnes séquentielles avec politiques d'échec,
  reprise exacte et scellement ; interface squelette à trois modes avec verrou Immersion évalué.
  Stubs `Stub.Syne` et `Stub.Echos` conformes à `INTEGRATION_CONTRACT.md` §13 avec pannes
  injectables. Frontières d'assembly vérifiées par analyse statique. Bancs verts : 50 tests
  unitaires, 7 intégration, 3 bout en bout.
- **Interface alignée sur la maquette (GUI.md)** : fenêtre 1536 × 1024 redimensionnable,
  grille 85/48, trois colonnes 222/924/321, sidebar à **sept entrées** (Accueil, Expériences,
  Campagnes, Analyse, Rapports, Configuration, Logs — §5.1), hero débordant de 22 px dans la
  topbar, palette et typographie des jetons §3 (dégradé radial de page, or `#e0a25a`,
  Montserrat interlettré, pastilles à libellé). Cartes COMPOSANTS avec **cycle de vie réel**
  (démarrer/arrêter le composant détecté, jeton par variable d'environnement, arrêt gracieux
  puis forcé), tableau « Dernières expériences » alimenté par les paquets `.livexp` réels,
  colonne droite (état, ressources échantillonnées à 0,8 s, logs du journal de session),
  footer `Mode:Standard`. Réconciliation §2.2 : les **modes** vivent dans le sélecteur de
  type de session de la topbar (Standard/Expérience/Développement), la navigation porte les
  entrées d'écran ; l'Immersion n'est pas une entrée (PRISM apparaît dans la pile, ADR-002/006).
  Vue Analyse inchangée : rapport d'émergence affiché fidèle au fichier ECHOS (ADR-003).
- **Conformité d'audit G1→G4** : `AnalyzeRun` demandé pour tout run réussi (analyses
  individuelles sous `analysis/individual/`), journaux d'échec archivés
  (`runs/<run>/logs/failure.txt`), corrélation propagée en argument **et** variable
  d'environnement, `provenance.json` écrit dès la création du paquet, `run.json` enrichi
  (exitCode, startedAt, endedAt, plateforme) et versions des composants enregistrées dans
  le manifeste (`EXPERIMENTS.md` §12). Lecteur Markdown dans la vue Analyse (ADR-003) et
  ouverture de paquet par `--package`.
- **Surface HTTP d'observabilité du Launcher** (`NETWORK.md` §4.1, `OBSERVABILITY.md` §4) :
  `LauncherHttpSurface` — `GET /health/live|ready|details`, `GET /info`, `GET /metrics`
  (texte Prometheus : `livex_launcher_component_state`, `livex_launcher_disk_free_bytes`)
  et `GET /registry` (document `instances[]` du registre, lecture seule). Port pris dans
  la plage interne 5200–5399 avec pré-vol, démarrage non bloquant (échec journalisé),
  libération à la fermeture de l'application. Contrat vérifié par le banc d'intégration.
- **Jeu de documentation du composant** — `docs/docs-launcher/` : vision,
  architecture, modèle de composants, contrat d'intégration, format de paquet,
  campagnes, observabilité, réseau, interface, empaquetage, tests, feuille de route,
  points ouverts, journal des modifications et six ADR.
- **Format de paquet `.livexp`** — format unique, vivant pendant la campagne puis
  scellé, avec `manifest.json` comme point de commit, `runs/index.json` comme source
  de reprise, périmètres d'écriture disjoints, versionnage par `schema` et
  déterminisme octet pour octet des paquets scellés.
- **Modèle de composants** — manifeste déclaratif, machine à états explicite,
  registre de session, sélection par profil, détection et adoption.
- **Trois modes d'utilisation** — mode Contrôle comme socle, mode Analyse porté par
  ECHOS, mode Immersion porté par PRISM, de poids égal et sélectionnables.
- **Exigences d'intégration** — `INTEGRATION_CONTRACT.md` énonce ce que chaque
  composant doit exposer pour être orchestrable, sans chemin de contournement.
- **Conception du mode Immersion** — PRISM est conçu au niveau du modèle, avec ses
  exigences, sa vue et ses tests ; son accès reste verrouillé tant qu'il n'est pas
  implémenté.
- **Six ADR** — pile technique .NET et Avalonia, égalité des deux modes, propriété
  de l'analyse par ECHOS, format de paquet, exécution séquentielle et mono-espace,
  verrouillage de PRISM.
- **`GUI.md`** — spécification pixel de l'interface, établie par mesure directe de
  la maquette de référence 1536 × 1024 : grille à trois colonnes, bornes de chaque
  zone, palette, typographie, composants, états et interactions, plus l'écart
  mesuré entre la maquette et le prototype HTML.

### Changed
- **`USER_INTERFACE.md` réconcilié avec la maquette** — la maquette montre une
  navigation à sept entrées et non les trois modes du document ; les deux
  découpages sont désormais explicités et la décision de réconciliation est
  signalée comme point ouvert. Le principe « aucun dégradé » est révisé : la
  maquette repose sur des dégradés de fond, qui ne portent jamais d'information.
- **Correction et cartographie des flux** — `NETWORK.md` reçoit une cartographie
  complète des flux en Mermaid (contrôle, données directes, restitution) et une
  cartographie des sous-espaces d'adressage internes orchestrés par le Launcher ;
  `DATA_FLOW.md` reçoit un schéma de bout en bout distinguant lien direct et lien
  transitant par le Launcher. Le faux flux `SYNE → Launcher` de la séquence de run
  est corrigé. La taxonomie des modes de `NETWORK.md` §9 est alignée sur
  `COMPONENTS.md` §8, et « analyse hors ligne » y est requalifiée en absence de
  moteur. La Gateway est marquée « forme de sortie, non planifiée » dans
  `OBSERVABILITY.md`, `INTEGRATION_CONTRACT.md` et `TESTING.md`. Références de
  sections erronées et coquilles corrigées dans l'ensemble du corpus.
- **Réécriture complète de la documentation du Launcher** — le monolithe
  `LIVEX_Launcher.md` et les six documents annexes de l'ancien
  répertoire de brouillon sont remplacés par le présent jeu de documents.
- **Suppression des documents de comparaison de version** — l'historique de révision
  est désormais porté par ce journal, sans appareil de comparaison dans les
  documents de spécification.
- **Suppression du document d'auto-analyse** — la critique du brouillon est
  absorbée par les sections « Alternatives considérées » des ADR et par `ISSUES.md`.

### Removed
- **Format de paquet précédent** — la notion de paquet, absente du projet, est
  remplacée par le format `.livexp` unique.
- **Modèle de ports à entier global** — le versionnage suit `../../VERSIONING.md`,
  semver par composant, sans numéro de protocole propre au Launcher.
- **Logique de comparaison multi-runs dans le Launcher** — l'analyse appartient à
  ECHOS ; le Launcher ne calcule aucune statistique.

## [0.0.0] — Non publié

- Aucune version publiée.
