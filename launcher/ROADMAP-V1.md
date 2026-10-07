# Roadmap d’achèvement du Launcher V1

**Périmètre :** Launcher LIVEX et intégrations nécessaires avec SYNE, ECHOS,
`syne-mock` et PRISM  
**Référence d’état :** branche `develop`, commit `90f06463` (2 octobre 2026)  
**Ordre :** jalons ordonnés par dépendance, sans dates calendaires  
**Référence fonctionnelle :** `../docs/docs-launcher/` ; cette roadmap décrit
le travail restant observé, pas seulement la cible conceptuelle.

## 1. But et règle de lecture

Cette feuille de route transforme les écarts actuels en travaux réalisables et
en critères de sortie vérifiables. Elle sépare :

- **Transverse** : contrat, orchestration, sécurité, UI, tests et livraison ;
- **SYNE** : moteur réel, unique autorité de simulation ;
- **ECHOS** : ingestion, analyse, API et interface d’observation ;
- **SYNE mock** : émulation de transport pour le développement, pas un second
  moteur scientifique ;
- **PRISM** : rendu Unreal, intégration facultative tant qu’il ne satisfait pas
  son contrat.

Les tâches sont numérotées dans l’ordre d’exécution recommandé. Les tâches
SYNE et ECHOS du jalon 2 peuvent être réalisées en parallèle après le jalon 1.
Un jalon est terminé uniquement lorsque sa porte de sortie est démontrée par
les tests et le scénario utilisateur indiqués, pas lorsque son code est
simplement présent.

**Règle contre les faux positifs :** « le Launcher fonctionne avec un stub »,
« le composant a un processus démarrable » et « le composant passe ses propres
tests » sont trois validations différentes. Seule une acceptation bout en bout
contre le contrat réel clôt les tâches d’intégration correspondantes.

## 2. État déjà présent — ne pas re-développer

- [x] Le format `.livexp`, sa lecture/écriture, le scellement et les propriétés
  essentielles sont implémentés et couverts par des tests.
- [x] Le Launcher détecte les composants par manifeste, gère profils,
  processus, sondes de santé et état d’orchestration.
- [x] Les quatre modes et les neuf écrans principaux sont présents.
- [x] SYNE réel et `syne-mock` sont présentés comme deux variantes exclusives du
  **même rôle SYNE** ; l’interface affiche trois rôles : SYNE, ECHOS et PRISM.
- [x] L’ajout d’installations détectées, le choix de l’installation active et
  les journaux de session et de processus contenus dans un paquet sont
  implémentés.
- [x] Les campagnes séquentielles, la dérivation de graines, l’annulation, le
  scellement et la reprise sont testés contre les composants simulés.
- [x] ECHOS et `syne-mock` ont des adaptations Linux de cycle de vie
  supervisables. Cela ne rend pas leurs capacités disponibles sous Windows ;
  SYNE réel est accepté sous Linux depuis une copie du publish.
- [x] Les suites récentes exécutées sur cette révision comptent 86 tests
  unitaires, 7 d’intégration et 20 end-to-end Launcher, dont un parcours
  campagne contre SYNE publié ; le rapport complet figure dans
  [`../Livex-status.md`](../Livex-status.md).

## 3. Ordre d’exécution et jalons

### Jalon 0 — Figer la cible V1 et les critères d’acceptation

**Pourquoi en premier :** les documents de conception décrivent des capacités
plus larges que celles réellement fournies par les composants. Il faut convenir
des conditions de fin avant de déverrouiller des modes ou d’annoncer une
campagne réelle.

#### Transverse

- [x] Établir une matrice de capacités par rôle, variante, OS et mode :
  lancement de service, UI/headless, campagne, analyse, rendu, readiness,
  contrôle, métriques et arrêt.
- [x] Pour chaque ligne, indiquer si elle est livrée, testée sur stub, acceptée
  contre le composant réel, bloquée par un composant externe ou hors V1.
- [x] Aligner `docs/docs-launcher/ROADMAP.md`, `ISSUES.md`,
  `INTEGRATION_CONTRACT.md`, `COMPONENTS.md`, `PACKAGING.md` et le README du
  Launcher sur cette matrice ; supprimer les affirmations périmées ou
  contradictoires.
- [x] Formaliser les règles V1 déjà retenues : exécution séquentielle,
  un seul rôle SYNE actif, aucun redémarrage automatique, PRISM verrouillé
  tant que non conforme, et absence de parallélisation/multi-espace.
- [x] Définir les profils minimums acceptés par les tests d’acceptation :
  Console, Standard, Développement et Personnaliser, y compris les variantes
  d’installation admissibles — voir la matrice ci-dessus.

La version contractuelle et les schémas seront arrêtés au jalon 1, après
confrontation aux APIs réelles ; le jalon 0 ne fige pas artificiellement un
contrat que SYNE et ECHOS ne satisfont pas encore.

**Porte J0 :** une checklist d’acceptation versionnée et une matrice sans
capacités promises implicitement. Chaque fonction bloquée a un propriétaire :
Launcher ou composant concerné.

**État J0 :** la matrice factuelle est publiée dans
[`V1-CAPABILITY-MATRIX.md`](V1-CAPABILITY-MATRIX.md) ; README Launcher,
ROADMAP Launcher et ISSUES ont été alignés. J0 est terminé pour le cadrage ; le
versionnement exécutable des contrats reste explicitement dans J1.

### Jalon 1 — Rendre les contrats de composants implémentables

**Pourquoi ici :** le Launcher ne peut pas corriger seul l’absence des
opérations de run SYNE ni des opérations d’analyse ECHOS.

#### Transverse — contrat commun

- [x] Fixer une version contractuelle et des schémas versionnés pour les
  manifestes, les arguments communs, les endpoints, les erreurs, les codes de
  sortie et les résultats de run ; partir des contrats observés, sans promettre
  des capacités absentes.
- [x] Confirmer les arguments et leur sémantique : identifiant d’instance,
  ports, bind local, répertoires de travail/journaux, configuration,
  corrélation et mode headless.
- [x] Confirmer la séquence `start → live → ready → shutdown`, ses délais,
  l’authentification des commandes modifiant l’état, les réponses d’erreur et
  les codes de sortie.
- [x] Fixer les règles d’écriture : chaque producteur écrit uniquement dans
  son répertoire assigné ; les artefacts sont fermés et complets avant que le
  Launcher ne scelle le paquet.
- [x] Décider la représentation des variantes UI/headless dans les manifestes
  et comment la compatibilité des capacités est refusée et expliquée.
- [x] Publier des exemples de manifests valides/invalides et des fichiers
  contract-tests réutilisables par les dépôts composants.

**État J1 :** le contrat de contrôle et le manifeste schema 1 sont publiés dans
[`contracts/`](contracts/INTEGRATION-CONTRACT-v1.md), avec exemples positifs et
négatifs, testés par le validateur de schéma exécuté en CI et par les tests
Launcher. Le détecteur refuse désormais les chemins d’exécutable non confinés,
les endpoints invalides et les délais/santés absents ; les ports auxiliaires
peuvent être alloués dynamiquement et transmis via `launchArgument`. Vérifications
locales : schéma et 3 manifestes de composant valides ; Launcher Release avec
84 tests unitaires, 7 d’intégration et 18 end-to-end réussis. J1 est terminé
pour le contrat commun ; les opérations réelles batch SYNE et analyse ECHOS
restent des tâches distinctes de J2A/J2B.

### Jalon 2 — Fermer les blockers propres à SYNE et ECHOS

Les sous-chantiers **SYNE (2A)** et **ECHOS (2B)** sont parallélisables après
J1. Aucun ne remplace l’autre.

#### 2A — SYNE réel

- [x] Ajouter un `component.json` valide pour SYNE avec exécutables et
  capacités réellement supportés par OS/version ; ne pas déclarer une capacité
  absente.
- [x] Implémenter ou adapter un mode batch autonome prenant une simulation,
  une seed, une limite de ticks et un répertoire de sortie ; le processus doit
  commencer sans interaction et terminer de lui-même à l’horizon.
- [x] Prendre en charge les arguments communs retenus au J1, sans ignorer ni
  accepter silencieusement les arguments inconnus.
- [x] Exposer une readiness distincte de la simple vivacité, le statut de run,
  les erreurs explicites et un arrêt propre authentifié ; vider les sorties
  avant de terminer.
- [x] Écrire les résultats, les métadonnées et les journaux dans le répertoire
  de run transmis ; retourner un code de sortie qui distingue fin normale,
  annulation et erreur.
- [x] Ajouter un test de processus réel utilisant la ligne d’arguments du
  `ProcessRunExecutor` : démarrage, readiness, seed/ticks, fin automatique et
  artefact attendu dans le workspace.
- [x] Valider l’arrêt authentifié du service et l’échec explicite sur conflit
  de port dans des tests de processus réels.
- [x] Aligner le scénario SYNE par défaut et le démarrage supervisé sur
  `reference`; refuser dans le formulaire les identifiants batch non supportés.
- [x] Prouver par des tests de processus le confinement des sorties : le batch
  refuse un `--export-dir` extérieur au workspace et le scénario nominal ne
  crée que les emplacements de travail et journaux prévus.
- [x] Tester la reproductibilité exacte pour version, configuration,
  plateforme et seed identiques ; calculer et conserver une empreinte des
  résultats exportés.
- [x] Documenter les différences éventuelles de déterminisme entre systèmes
  d’exploitation sans les présenter comme une égalité exacte non démontrée.
- [x] Exécuter une campagne via `ProcessRunExecutor` sur une copie isolée du
  publish Linux SYNE ; vérifier le résultat réel, les journaux, les versions
  déclarées, l’intégrité et le scellement du `.livexp`.

**État J2A :** un manifeste Linux, le service supervisé et le batch `reference`
sont implémentés ; `result.json` est déterministe et contient un checksum
d’état. Les suites SYNE complètes passent (514 Core, 67 Console), dont des tests
de processus réels pour le batch, l’arrêt authentifié et le conflit de port.
Le démarrage supervisé et le formulaire de campagne utilisent `reference` ;
un identifiant non supporté désactive la création. Le binaire publié au chemin
du manifeste passe un smoke test batch direct.
`PublishedSyneCampaignEndToEndTests` valide aussi le parcours Launcher sur une
copie isolée de l’installation publiée : le Launcher lance le binaire, collecte
`result.json` et les journaux, vérifie l’intégrité du run dans le `.livexp`, puis
scelle le paquet avec la version SYNE. J2A est accepté pour la plateforme
déclarée Linux ; aucun support Windows/macOS n’est revendiqué.

#### 2B — ECHOS headless et analyse

- [x] Fournir des opérations headless stables et versionnées pour l’analyse
  d’un run, l’agrégation d’une expérience/campagne et la génération du rapport.
  Elles peuvent implémenter les chemins actuels `/analysis/run`,
  `/analysis/experiment` et `/analysis/report`, ou faire évoluer ce contrat et
  l’adaptateur Launcher de manière coordonnée.
- [x] Définir les entrées acceptées, les formats de sortie, les erreurs, les
  délais et la compatibilité de schéma ; les réponses doivent référencer des
  fichiers produits de façon déterministe et récupérable.
- [x] Garantir l’analyse avec la seule API/backend en cours d’exécution :
  aucune interface n’est nécessaire — ECHOS n’en expose plus (ADR-007), et les
  fenêtres d’observation du Launcher restent optionnelles.
- [x] Définir et tester la disponibilité réelle : base analytique accessible,
  migrations valides et endpoints d’analyse utilisables avant de signaler
  `ready`.
- [ ] Valider l’installation ECHOS Linux (Python, ressources
  runtime, chemins de données) et spécifier précisément les versions/OS
  supportés.
- [x] Alimenter la base analytique depuis les données batch SYNE : chaque run
  archivé expose son flux d’observabilité, ECHOS l’enregistre par
  `POST /ingest/run` avant toute analyse.
- [x] Ajouter des tests d’acceptation lancés depuis le Launcher sur un run
  réel, une campagne réussie, une entrée absente, une analyse invalide et un
  service arrêté ; une erreur d’analyse doit être signalée sans fabriquer de
  rapport.

**Porte J2B :** le Launcher peut demander les trois opérations contre ECHOS
réel, sans UI, reçoit les artefacts conformes et expose les erreurs sans
résultat de substitution.

**État J2B :** la chaîne batch est complète et vérifiée de bout en bout. SYNE
exécute chaque run supervisé avec `--run-id {campagne}-{run}` et
`--export-stream`, ce qui produit `data/stream.jsonl` à côté de `result.json` ;
le flux est archivé dans le `.livexp` avec le reste du run. Avant toute analyse,
le Launcher ingère ce flux par `POST /ingest/run`, qui réutilise le pipeline
d’ingestion du flux live sans l’interpréter : mêmes métriques, mêmes contextes,
même rapport de calibration. L’ingestion est idempotente **par refus** (409 si le
run est déjà enregistré), l’identité du flux fait autorité et une divergence avec
le `runId` demandé est refusée avant toute écriture. L’intégrité du fichier est
contrôlée à trois niveaux : ticks de snapshot strictement croissants,
exactement un `tick_summary` par snapshot, puis accord avec les ticks annoncés
par `result.json` — seul moyen de détecter une troncature entre deux segments.
Une ingestion qui échoue en cours de parcours **purge ce qu’elle a écrit** : le
pipeline validant chaque tick séparément, un flux invalidé à mi-parcours
laisserait sinon un run à la fois tronqué et protégé par la garde anti-doublon,
et l’archive valide ne pourrait plus jamais être ingérée. La rejouabilité est
prouvée par les octets : le rapport archivé pendant la campagne est identique,
bit pour bit, à celui produit plus tard à partir du seul paquet. Cela a exigé
d’exclure le contexte `profiling` — des durées de calcul non reproductibles —
du rapport déterministe ; ces durées restent disponibles par la voie
d’instrumentation.

Reste ouvert : la validation de l’installation ECHOS Linux (versions et OS
supportés, ressources runtime) et l’acceptation sur un service ECHOS réel
pilotée depuis l’interface du Launcher plutôt que depuis le backend.

#### 2C — `syne-mock`

- [x] Maintenir le mock comme implémentation de développement du rôle SYNE,
  mutuellement exclusive avec SYNE réel ; conserver sa déclaration Linux
  seulement tant qu’aucun runtime Windows n’est livré et testé.
- [x] Compléter sa suite contractuelle de démarrage, readiness, ports,
  arrêt authentifié, signaux et nettoyage des processus/sockets.
- [x] Déclarer explicitement qu’il n’est ni scientifiquement équivalent à SYNE,
  ni compatible avec les campagnes réelles, ni source de résultats à comparer
  au moteur réel.
- [ ] Si l’exécution de campagnes de démonstration avec le mock est retenue,
  ajouter un exécuteur distinct ou une capacité explicite, avec paquets
  étiquetés « simulation émulée » ; ne pas le laisser apparaître comme un run
  SYNE réel. Cette capacité ne bloque pas la V1 scientifique.

**État J2C :** le manifeste reste Linux et n’annonce aucune capacité batch de
campagne ; la documentation qualifie le mock d’émulation de développement
mutuellement exclusive, sans équivalence scientifique. Les tests de service
couvrent readiness, ports attribués, conflit de ports, arrêt authentifié avec
fermeture du WebSocket, SIGINT et SIGTERM. `npm test` passe avec 46 tests.
La sélection exclusive SYNE réel/émulé demeure testée par le Launcher (J0).
J2C est terminé ; la partie technique de J2A et de J2B est désormais couverte,
la validation d'installation ECHOS (versions/OS) reste à faire avant clôture
définitive de J2.

### Jalon 3 — Brancher le Launcher aux contrats réels

**Dépend de :** J1 et J2A ; l’analyse de rapports complets dépend également de
J2B.

#### Launcher — exécution et orchestration

- [ ] Faire résoudre `ProcessRunExecutor` à partir de l’installation et de la
  variante SYNE active, plutôt que d’imposer implicitement un binaire ou une
  ligne de commande historique.
- [ ] Construire la commande à partir du manifeste/capacités validés ; refuser
  avant démarrage un argument ou une option non supportés avec un diagnostic
  exploitable.
- [ ] Vérifier la transition réelle de processus : démarrage, readiness,
  progression, fin à l’horizon, code de sortie, annulation et arrêt forcé après
  le délai de grâce.
- [ ] Raccorder les paramètres de campagne (configuration/simulation, nombre de
  runs, ticks, seed/stratégie, politique d’échec et destination) à la commande
  SYNE ; enregistrer les valeurs effectives dans les métadonnées du run.
- [ ] Collecter les artefacts depuis les seuls chemins autorisés, vérifier
  complétude/empreintes, produire l’index et sceller le paquet seulement après
  fermeture de tous les producteurs.
- [ ] À la reprise, ne pas rejouer un run réussi ; reconnaître et diagnostiquer
  un run partiel ou un processus toujours actif au lieu de le supposer terminé.
- [ ] Contrôler les erreurs composant par composant : échec d’ECHOS ne supprime
  pas les artefacts SYNE ; échec du composant optionnel ne rend pas l’état
  SYNE incohérent.

#### ECHOS — raccordement au paquet

- [x] Envoyer à ECHOS les chemins et identifiants exacts inscrits dans le
  paquet. L’identité analytique `{campagne}-{run}` est calculée en un seul
  endroit (`RunIdentity`), ingérée puis réutilisée pour l’analyse, et inscrite
  telle quelle dans le manifeste d’expérience. Le Launcher constate l’identité
  qu’ECHOS a enregistrée : un dossier mal apparié est nommé au lieu de
  ressortir plus tard comme un « run inconnu » opaque.
- [ ] Archiver les artefacts d’analyse et le rapport retournés dans le
  paquet selon le schéma ; enregistrer les versions d’ECHOS, des métriques et
  du format de rapport.
- [x] Préserver le rapport Markdown source sans le transformer ; afficher et
  exporter le même contenu et signaler clairement son absence ou son erreur
  (session complète G5 : l’archived est comparé octet à octet à ce qu’ECHOS
  renvoie à l’instant, et un ECHOS arrêté produit un paquet sans rapport plutôt
  qu’un rapport approximatif).

**Porte J3 :** un parcours d’acceptation passe contre SYNE et ECHOS réels :
création de campagne → run batch → artefacts → analyse sans interface → rapport
archivé et relu depuis le `.livexp`. Un second parcours arrête la campagne,
la relance et prouve qu’aucun run terminé n’est rejoué.

### Jalon 4 — Terminer les surfaces UI et de configuration

**Dépend de :** J0 ; la validation avec données de campagne réelles dépend de
J3. Le travail de présentation peut commencer avant J3 avec des données de test.

#### Transverse — modes, profils et installation

- [ ] Décrire dans les manifestes les variantes d’interface (service/headless
  ou UI) et les prérequis ; rendre leur choix explicite dans Personnaliser,
  sans proposer de variante absente ou incompatible.
- [ ] Dans les quatre modes, garantir que profils et sélections sont cohérents :
  Console sans PRISM, Standard par défaut, Développement impose l’émulation,
  Personnaliser choisit une installation par rôle ; SYNE réel et mock ne sont
  jamais simultanés.
- [ ] Montrer dans Configuration l’installation/version, les capacités
  disponibles, l’OS supporté, les fichiers manquants et les raisons précises
  d’indisponibilité ; revérifier les manifestes quand une installation change.
- [ ] Vérifier les états vides, erreurs, verrouillages, chargements et parcours
  clavier des neuf écrans sur différentes tailles de fenêtre/zoom.

#### Expériences, Campagnes, Analyse et Rapports

- [ ] Relier l’ouverture des paquets à l’index d’expériences/campagnes, et
  afficher l’état du paquet, les runs, leurs statuts, leurs paramètres et
  leurs erreurs.
- [ ] Compléter les formulaires de création de run/campagne et leur validation
  avant création ; expliquer chaque paramètre et conserver une configuration
  rejouable.
- [ ] Relier progression, annulation et reprise de l’UI à l’état durable de la
  campagne ; éviter qu’une fermeture de vue perde l’opération en cours.
- [ ] Afficher une analyse/rapport uniquement lorsqu’il existe dans le paquet ;
  distinguer un rapport non demandé, en cours, réussi et échoué.
- [ ] Exporter le rapport seul hors paquet avec un choix de destination et
  vérifier qu’il est identique à l’artefact archivé.

#### Monitoring, Logs et Documentation

- [ ] Afficher les trois rôles seulement et regrouper le statut réel/émulé sous
  SYNE ; montrer la variante sélectionnée, le PID et la cause d’indisponibilité.
- [ ] Séparer métriques système (CPU, mémoire, stockage) des métriques de
  composant et des métriques de flux ; afficher « non fourni » si aucun
  endpoint/collecteur ne les expose.
- [ ] Ajouter au Monitoring débit, erreurs et latence des flux uniquement quand
  les composants exposent des compteurs fiables ; documenter source, unité,
  période et indisponibilité plutôt que déduire des valeurs.
- [ ] Vérifier Logs : journal global, journal de session et stdout/stderr par
  run ; filtre/recherche si prévu par le contrat, export, ouverture de dossier
  et limites de lecture/écriture explicites.
- [ ] Valider l’index de documentation embarquée, les liens relatifs, le rendu
  Markdown et le comportement si un document n’est pas distribué.

**Porte J4 :** scénarios UI automatisés et revue visuelle couvrent les neuf
écrans, chaque état vide/erreur et les quatre modes ; aucune option indisponible
n’est présentée comme démarrable.

### Jalon 5 — Durcir sécurité, fiabilité et conformité

**Dépend de :** J1 à J4 ; une partie des tests peut être préparée plus tôt.

#### Transverse — processus et sécurité

- [ ] Tester l’absence de processus orphelin après arrêt normal, annulation,
  crash du Launcher et crash du composant ; implémenter le confinement Windows
  Job Object et Linux groupe de processus lorsque la preuve manque.
- [ ] Vérifier l’authentification de toute commande de contrôle modifiant
  l’état ; ne jamais placer un jeton dans les arguments visibles du processus
  ni dans les journaux.
- [ ] Vérifier ports occupés, adresse de bind loopback, collision entre
  processus et libération des ports sur chaque chemin d’erreur.
- [ ] Valider tout manifeste et fichier de configuration au chargement, refuser
  une version de schéma inconnue et expliquer la version attendue/observée.
- [ ] Repasser les tests de confinement de chemin, liens symboliques, noms
  d’entrées d’archive, taille/ratio de décompression, écriture atomique,
  intégrité des empreintes et refus de modification d’un paquet scellé.
- [ ] Tester espace disque insuffisant, permissions, disque plein à
  l’écriture, processus bloqué, réponse HTTP malformée, timeout et fermeture
  inattendue ; produire une cause et un état final non ambigus.
- [ ] Décider et documenter la politique V1 pour les nouvelles questions
  ouvertes de support : seuils de ressources, cible de performance et
  conservation des journaux. Garder le redémarrage automatique désactivé sauf
  décision et tests explicites.

#### Transverse — tests et CI

- [ ] Ajouter une suite de contract-tests réutilisable pour SYNE, ECHOS et
  `syne-mock`, avec tests d’acceptation séparés des tests de composants.
- [ ] Compléter les scénarios end-to-end réels : session minimale, campagne
  nominale, arrêt/reprise, composant manquant/incompatible, échec d’analyse,
  paquet corrompu et conflit de port.
- [ ] Mesurer les seuils de couverture définis dans `TESTING.md` et vérifier
  les propriétés de paquet/orchestration qui ne sont pas déjà couvertes.
- [ ] Exécuter la CI sur Windows et Linux avec les SDK/runtime et composants
  explicitement épinglés ; rendre les tests intégrés reproductibles ou marquer
  clairement les jobs qui nécessitent une dépendance externe.
- [ ] Exécuter le contrôle de stabilité/performance de 72 h seulement sur une
  machine de référence instrumentée ; enregistrer matériel, versions,
  configuration et résultats.

**Porte J5 :** aucune fuite de processus/secrets, CI verte sur les plateformes
annoncées, scénarios d’échec lisibles, et mesures de performance documentées.

### Jalon 6 — Installer, diagnostiquer et publier

**Dépend de :** J5 pour les plateformes annoncées et de J2 pour déclarer un
composant installable ; la fabrication de paquets peut commencer plus tôt.

#### Transverse — distribution

- [ ] Produire un artefact installable Windows x64 et un paquet Linux x64,
  avec version, licence, empreinte et instructions de désinstallation.
- [ ] Vérifier que les données utilisateur, préférences, journaux, paquets et
  caches résident hors du répertoire applicatif et survivent à une mise à jour.
- [ ] Inclure le contenu Markdown attendu dans la distribution et valider les
  liens relatifs après installation.
- [ ] Terminer `--check` : sortie texte stable, code de sortie documenté,
  tests sur installation propre, composants absents, manifestes invalides,
  répertoires non inscriptibles et conflits de ports.
- [ ] Définir ce qui est distribué avec le Launcher et ce qui est installé
  séparément ; fournir des instructions propres à chaque composant, sans
  présenter un binaire de développement comme un installateur.
- [ ] Vérifier mise à jour, conservation des paquets, échec à mi-installation,
  premier démarrage et lancement d’un profil disponible sur une machine propre.

#### SYNE / ECHOS / `syne-mock`

- [ ] Publier pour chaque composant un manifeste aligné sur les plateformes
  effectivement construites et acceptées.
- [ ] Fournir les prérequis et étapes d’installation (runtime .NET, Python et
  dépendances, Node si requis) ou des distributions autonomes vérifiées.
- [ ] Confirmer qu’une installation sans composant démarre le Launcher et
  expose les absences comme diagnostic, sans faire échouer l’application.

**Porte J6 :** un utilisateur installe le Launcher depuis zéro, diagnostique
l’environnement, ajoute des composants compatibles, lance et arrête un profil,
puis met à jour sans perdre ses données. Tous les artefacts publiés sont
reproductibles et leurs empreintes vérifiées.

## 4. Chantier PRISM — porte d’intégration séparée

PRISM est un chantier de composant et n’est pas un prérequis pour déclarer
terminés les parcours Console, Développement et expériences headless du
Launcher. Il est cependant nécessaire avant de déclarer le mode Immersion ou
l’orchestration PRISM comme livré.

#### PRISM / PRISM-LDK

- [ ] Implémenter l’expérience Unreal de rendu/interaction prévue ; distinguer
  le plugin de transport actuel de l’application visuelle finale.
- [ ] Fournir et valider le manifeste PRISM, les arguments, les capacités
  `snapshotStream` et `renderCadence`, le contrôle, la readiness et l’arrêt.
- [ ] Consommer le contrat de flux SYNE versionné après l’arbitrage de
  compatibilité ; gérer reconnexion, séquence/versions et erreurs sans modifier
  l’état scientifique.
- [ ] Déclarer et mesurer la cadence et les coûts de rendu réellement observés.
- [ ] Prouver par tests que l’arrêt ou l’échec de PRISM ne termine ni ne modifie
  un run SYNE.
- [ ] Valider la détection, l’activation et la désactivation du mode Immersion
  à partir du manifeste ; afficher la cause exacte si une capacité manque.

**Porte PRISM :** test d’acceptation Launcher–SYNE–PRISM, sur un build Unreal
supporté, avec flux en direct, commandes autorisées, métriques déclarées et
indépendance du run lors d’une panne PRISM. Jusqu’à cette porte, le mode reste
verrouillé.

## 5. Ordre synthétique et dépendances

```text
J0 Cible V1 et critères
 └─ J1 Contrats communs
     ├─ J2A SYNE réel ─┐
     ├─ J2B ECHOS réel ├─ J3 Campagne et session réelle de bout en bout
     └─ J2C SYNE mock ┘             │
                                    ├─ J4 UI et parcours réels
                                    └─ J5 Fiabilité, sécurité et CI
                                         └─ J6 Installation et publication

Arbitrage du flux SYNE ──> PRISM ──> Porte d’intégration PRISM
```

J4 (travail de présentation) peut progresser avec des fixtures avant J3, mais
son acceptation finale dépend d’un paquet produit par le parcours réel. Le
chantier PRISM est une branche distincte ; il ne doit ni retarder l’usage
headless ni être utilisé pour déclarer la V1 immersive terminée sans sa porte.

## 6. Définition de « Launcher V1 terminé »

Le Launcher peut être annoncé **V1 opérationnelle pour orchestration et
campagnes réelles** uniquement si :

- [ ] J0 à J6 sont franchis, avec preuves liées dans la CI ou les notes de
  release.
- [ ] Le parcours J3 passe contre SYNE et ECHOS réels, avec l’interface ECHOS
  fermée, rapport dans le paquet, reprise sans rejouer les runs terminés.
- [ ] Les quatre modes respectent la matrice de capacités ; les modes sans
  composant compatible sont explicitement désactivés ou limités.
- [ ] La sécurité, l’arrêt propre et l’absence de processus orphelin sont
  validés sur chaque OS annoncé.
- [ ] Les composants et plateformes non encore compatibles sont identifiés
  comme tels dans Configuration, `--check`, la documentation et les artefacts
  de distribution.

Le **mode Immersion PRISM** ne peut être annoncé comme livré qu’après la porte
PRISM. Les campagnes emulées avec `syne-mock`, si ajoutées, restent des
scénarios de développement et ne remplacent jamais l’acceptation SYNE réel.

## 7. Documents de référence et synchronisation

- Contrats et manifeste : [`../docs/docs-launcher/INTEGRATION_CONTRACT.md`](../docs/docs-launcher/INTEGRATION_CONTRACT.md)
- État des blockers : [`../docs/docs-launcher/ISSUES.md`](../docs/docs-launcher/ISSUES.md)
- Campagnes et reprise : [`../docs/docs-launcher/EXPERIMENTS.md`](../docs/docs-launcher/EXPERIMENTS.md)
- Interface et modes : [`../docs/docs-launcher/USER_INTERFACE.md`](../docs/docs-launcher/USER_INTERFACE.md)
- Livraison : [`../docs/docs-launcher/PACKAGING.md`](../docs/docs-launcher/PACKAGING.md)
- Validation : [`../docs/docs-launcher/TESTING.md`](../docs/docs-launcher/TESTING.md)
- État mesuré : [`../Livex-status.md`](../Livex-status.md)

À chaque tâche terminée, mettre à jour son statut et relier la preuve (PR,
workflow, test ou décision). Si une tâche devient hors V1, déplacer la décision
dans les documents de référence et retirer sa capacité des promesses UI et
packaging ; ne pas simplement supprimer la tâche.
