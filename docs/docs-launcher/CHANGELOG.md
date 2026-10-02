# CHANGELOG — LAUNCHER

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 2 octobre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versionnement :
SemVer (`launcher-vX.Y.Z`).

Le Launcher n'a pas encore de version livrée. L'implémentation progresse jalons par
jalons : G1 → G4 livrés, G5 → G7 réalisés côté Launcher contre le banc de stubs, les
portes externes P1 – P5 restant de côté des composants.

## [Unreleased]

### Added
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
  SIGINT/SIGTERM. SYNE reste volontairement sans manifeste : ses arguments de campagne,
  son cycle de service, ses sorties et son arrêt ne sont pas compatibles sans évolution
  du contrat scientifique.
- L'état global du monitoring agrège maintenant les composants réellement requis par le
  profil actif (dont `syne-mock` en Développement), et non SYNE par défaut.
- Bancs verts après ces changements : **81 tests unitaires + 7 d'intégration + 11 bout en
  bout côté Launcher, 309 tests ECHOS (2 ignorés) et 44 tests syne-mock**. La compatibilité
  scientifique SYNE/ECHOS/syne-mock reste une porte distincte
  des tests contre les stubs.
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
    un run échoué libère systématiquement son port et son entrée de registre. Audit de
    compatibilité stubs ↔ composants réels codés (`syne`, `echos`, `syne-mock`) : écarts
    consignés en `ISSUES.md` **O-38 à O-41** (arguments communs §3.1 rejetés par SYNE,
    endpoints standard §6 absents de SYNE/ECHOS/syne-mock, ECHOS sans argument de ligne de
    commande, aucun `component.json` livré).
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
