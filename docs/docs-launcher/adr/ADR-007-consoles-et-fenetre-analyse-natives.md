# ADR-007 : Consoles natives et fenêtre d'analyse dans le Launcher, ECHOS sans interface

**Composant** : LIVEX (Launcher)
**Statut** : [Accepted]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `ADR-001-stack-dotnet-avalonia.md`, `ADR-003-analyse-propriete-de-echos.md`
**Annule et remplace** : `../../docs-echos/adr/ADR-003-shell-electron-desktop.md` ([Superseded])
**Source Monographie** : —
**Date de décision** : 5 octobre 2026 (utilisateur)

---

## Contexte

Trois constats convergents, relevés dans le code et non dans la seule intention.

**1. Les logs des composants existent mais ne sortent jamais.** Le gestionnaire
de processus redirige déjà `stdout` et `stderr` de chaque composant, mais
uniquement vers des fichiers, sans diffusion en direct. L'écran Logs du Launcher
relit des fichiers. Le donnée est donc capturée à la source et perdue pour
l'affichage : il manque un bus d'événements, pas une capture.

**2. ECHOS porte trois responsabilités au lieu d'une.** Le composant est un
moteur d'analyse Python (ingestion, stockage, sept moteurs de métriques, API
REST), mais il héberge aussi une interface React/TypeScript (41 fichiers sources,
62 tests) **et** un shell Electron avec sa chaîne PyInstaller + electron-builder.
Le serveur FastAPI ne sert plus seulement une API : il monte un build statique
avec repli SPA. Conséquences : une instance Chromium pour afficher des séries
JSON, une chaîne de build Node de plus, un workflow CI dédié, deux langages de
présentation pour le même contenu, et un coût mémoire permanent chez l'utilisateur.

**3. La documentation tranchait déjà dans l'autre sens.** L'ADR-003 du Launcher
déclare : *« La frontière porte sur le calcul, pas sur l'affichage. ECHOS calcule,
le Launcher présente »*, et qualifie l'interface web d'ECHOS de *« télémétrie
optionnelle »*. Le projet maintenait donc deux décisions contradictoires :
l'ADR-001 du Launcher interdit « toute page web, tout conteneur web », tandis
que l'ADR-003 d'ECHOS acceptait un shell Electron.

Le coût de la contradiction n'est pas théorique : il impose à quiconque veut
regarder une courbe d'ouvrir Chromium, tout en maintenant une interface de
rapport séparée dans le Launcher.

## Décision

**ECHOS est un moteur sans interface. Le Launcher présente, y compris en direct,
dans des fenêtres natives.**

### 1. Consoles de logs par composant

- Le gestionnaire de processus **publie chaque ligne** de `stdout`/`stderr`
  (séquence globale croissante) en plus de l'écriture du fichier de journal,
  qui reste la source durable (`AutoFlush` activé, `tail -f` devient possible).
- Un **tampon mémoire borné** (20 000 lignes par instance) conserve le flux pour
  la lecture ; au débordement, c'est la fin du flux qui est retenue.
- Chaque instance supervisée dispose d'une **fenêtre console native** (Avalonia) :
  elle s'ouvre **automatiquement au démarrage** du composant et à la demande
  depuis les cartes et le Monitoring. Le choix réel ou émulé de SYNE se lit dans
  le titre : une console « SYNE » ou « SYNE (émulé) », selon le moteur démarré.
- La console **n'interprète rien** : horodatage, canal, texte. `stderr` est
  coloré, les canaux sont filtrables, la lecture est mise en pause sans perte.

### 2. ECHOS sans interface ni shell de bureau

- Suppression de `echos/echos-ui/` (interface React + Vite) et de
  `echos/echos-desktop/` (shell Electron, empaquetage PyInstaller, electron-builder).
- Suppression du montage statique dans `echos/api/app.py` (`SpaStaticFiles`,
  `ui_dist`) et de `ECHOS_UI_DIST` dans `echos/server.py` : `/` publie la liste
  des endpoints, aucune page n'est servie.
- Suppression des workflows CI associés (`echos-ui` dans `ci.yml`,
  `echos-desktop.yml`) et du script `dev-stack-electron.sh`.
- L'API REST reste **le seul contrat de sortie** : runs, séries de métriques,
  calibration, décisions, croyances, relations, groupes, phénomènes émergents,
  comparaison, opérations d'analyse et rapport.

### 3. Fenêtre d'analyse du Launcher

- Un bouton ouvre une **seconde fenêtre** du Launcher (multi-fenêtre), qui
  consomme l'API REST d'ECHOS par sondage périodique (1 s) et affiche l'analyse.
- **Aucune valeur scientifique n'est recalculée** : le Launcher rend les séries
  et les agrégats tels qu'ECHOS les produit (règle de l'ADR-003). Le calcul reste
  la propriété d'ECHOS ; seule la présentation change de propriétaire.
- Le rendu des graphiques utilise **LiveCharts2** (`LiveChartsCore.SkiaSharpView.Avalonia`),
  ajouté à la liste centrale des paquets. Aucun composant de la pile ne devient
  une dépendance du Launcher : c'est une bibliothèque de présentation, pas un
  composant LIVEX. Le **graphe de confiance** et le **monde 2D** restent dessinés
  nativement (contrôles Avalonia, `DrawingContext`) — sans ajouter de dépendance.
- Le modèle de vue vit dans `Launcher.Presentation`, la collecte HTTP dans
  `Launcher.App` (composition), conformément au découpage de l'ADR-001.
- La fenêtre porte un **menu de sous-écrans** organisés autour des questions
  (`USER_INTERFACE.md` §9.2) : **Viabilité** (écran par défaut : issue,
  population, besoins, ressources, complétude), **Comportements**, **Statistiques
  exactes**, **Comparer**, **Relations et groupes**, **Monde et territoires**,
  **Entités**. Un seul relevé est lancé à la fois, pour l'écran actif.
- **Formes visuelles (refonte P3, RAPPORT §6.2–6.4)** : courbes en **ticks
  réels** avec trous visibles et provenance par tick (`measured_by_tick`),
  **panneaux par unité** (population / besoins / réserves ne partagent jamais un
  axe), cartes numériques et tableaux pour les valeurs exactes. Le **radar
  min-max** et l'**histogramme de ticks** ont été **retirés** de la vue principale
  : ils suggéraient une normalisation et une comparabilité que le contrat ne
  garantit pas. Un radar ne sera réintroduit que pour un ensemble normalisé
  commun et documenté.
- **Barre de relecture** (bas de fenêtre) : curseur de tick, lecture/pause,
  précédent/suivant, retour au direct et vitesse en ticks/seconde (aucun effet
  sur SYNE). La relecture ne fait que **tronquer les séries déjà publiées** :
  aucune donnée n'est lue, écrite ni interpolée ; « Suivre le direct » et
  « Relire ce run » sont deux états distincts. Les panneaux monde/entités suivent
  le tick du curseur.
- **Catalogue, viabilité et comparaison** : libellés, unités, plages,
  dénominateurs, statuts et avertissements proviennent de
  `GET /api/metrics/catalog` (registre versionné d'ECHOS) ; les faits de viabilité
  de `GET /api/runs/{id}/viability` (y compris le bloc « rapport post-run »
  étiqueté comme tel et la chronologie d'extinction, observations sans cause
  racine) ; la comparaison de `GET /api/experiments/summary` (contexte de
  contrôle + dispersion publiée, 2 à 6 runs côté interface). Aucun seuil n'est
  affiché sans sa valeur observée, et une détection est rendue « signal selon la
  règle X ».
- **Quatre ajouts P3 (RAPPORT, manques B1 → B4)**, tous de simples rendus de
  données publiées :
  - *Marqueurs d'événements* (B1) : case à cocher sous la courbe, une ligne
    verticale par tick portant au moins un événement de `GET /api/runs/{id}/events`
    (nouvel endpoint **lecture seule**, borné à 500 lignes par défaut / 2000 au
    plus), couleur par type, plafond de 16 marqueurs limités à la fenêtre
    affichée. Le marqueur signale *qu'un* événement existe, jamais sa signification
    ; le compte-rendu « N affiché(s) sur M publié(s) » empêche une borne de
    rendu de passer pour l'intégralité.
  - *Âge des contextes échantillonnés* (B2) : la fiche d'entité affiche la
    cadence publiée (`conservation.sampledDetails.agentContextEvery`) et l'écart
    au dernier tick observé — métadonnées côte à côte, jamais interpolées.
  - *État du run* (B3) : ligne « État du run » de l'écran Viabilité décrite
    depuis `outcome`, `extinction_tick`, `last_tick` et `missing_tick_count`
    publiés. ECHOS ne distinguant pas « en cours » d'« interrompu », la mention
    l'annonce ; aucun état n'est déduit sans son fait source.
  - *Outil de distribution* (B4) : outil explicite sous Statistiques après le
    retrait de l'histogramme — intervalles (règle de Sturges, bornée [2, 12])
    et effectif publié par intervalle, `null` et `measured = false` exclus et
    comptés, aucune extrapolation. Comptage de classe = transformation de rendu
    bornée (ADR-003), jamais une densité ni une probabilité.
- Le monde 2D, le graphe de confiance et les fiches exigent des données que
  l'API ne publiait pas : **ECHOS étend son API en lecture seule** —
  `GET /api/world` (description de monde persistée depuis `world_initialized`,
  entités, réserves) et `GET /api/trust-graph` (nœuds et arêtes publiés). La
  persistance de la description de monde est faite à l'ingestion, jamais
  reconstituée a posteriori ; le Launcher ne fait que les rendre.

## Conséquences

### Positives
- Une seule chaîne de présentation dans le projet : plus de divergence entre ce
  que l'utilisateur lit dans ECHOS et ce qu'il lit dans le Launcher.
- Zéro instance Chromium pour observer une simulation : le coût mémoire de
  l'observation passe de plusieurs centaines de mégaoctets à quelques mégaoctets.
- ECHOS redevient un composant d'analyse : une API Python, une base, des tests.
  Sa chaîne de build se limite à `pip` + `pytest` + `flake8`.
- Les logs deviennent enfin lisibles pendant l'exécution, ce qui réduit le temps
  de diagnostic d'un composant défaillant.
- La frontière ADR-003 devient vérifiable mécaniquement : une vue du Launcher
  qui *affiche* est conforme ; une vue qui *calcule* ne l'est pas.

### Négatives
- Le Launcher porte désormais deux fenêtres de plus et une bibliothèque de
  graphiques : son périmètre de présentation s'élargit, ce qui alourdit la revue
  de conception exigée par l'ADR-003.
- Le sondage REST (1 s) est moins fin qu'un push : une métrique écrite entre
  deux relevés n'apparaît qu'au relevé suivant. Accepté pour l'instant, car le
  contrat publié d'ECHOS est déjà en lecture par ressource.
- L'interface React disparaît : les 62 tests UI et les écrans existants sont
  perdus et doivent être refaits en Avalonia, écran par écran.
- L'empaquetage `.exe`/`.deb` autonome d'ECHOS promis par l'ancien ADR-003
  n'existe plus : ECHOS s'installe avec Python, comme aujourd'hui.

### Risques
- **Fuite de calcul dans la fenêtre d'analyse.** Une moyenne « calculée pour
  l'affichage » romprait l'unicité de la sémantique. Mitigation : les valeurs
  affichées proviennent des champs de l'API ECHOS ; tout calcul local doit être
  refusé en revue et par le test de l'ADR-003.
- **Volume de la fenêtre console.** Un moteur bavard peut produire des dizaines
  de milliers de lignes par minute. Mitigation : tampon borné, relevé plafonné
  à 2 000 lignes par coup de sonde, collection d'affichage bornée à 5 000.
- **Fenêtres orphelines.** Des consoles ouvertes prolongeraient la vie de
  l'application. Mitigation : `ShutdownMode.OnMainWindowClose` et libération des
  fenêtres à la fermeture.
- **Dette documentaire.** D'autres documents décrivaient encore l'interface web
  (`FRONTEND_VISION.md`, `USER_STORIES.md`, `UI_DESIGN.md`, ROADMAP, ISSUES,
  monographie). Ils sont marqués **historiques** plutôt que laissés comme
  prescriptions ; le reste se traite dans les revues de documentation (P4).

## Alternatives considérées

- **Conserver l'interface web et l'ouvrir depuis le Launcher** : refusé. C'est
  l'état actuel : une dépendance Chromium, deux chaînes de build et deux
  présentations du même résultat.
- **Terminal système externe** (`gnome-terminal`, `xterm`, `cmd`) pour les logs :
  refusé. Dépendance à l'environnement de bureau, non testable, impossible à
  fermer proprement, absent sur certains postes. La fenêtre native couvre le
  besoin avec un coût maîtrisé.
- **Libre de dessin des graphiques sans dépendance** : écarté en faveur de
  LiveCharts2, retenu par l'utilisateur pour des graphes riches (axes, zoom,
  infobulles) sans réinventer le rendu. Le risque de dépendance est circonscrit à
  la couche de présentation.
- **Push temps réel depuis ECHOS (WebSocket/SSE dédié)** : reporté. Le contrat
  REST publié suffit au premier palier et évite toute modification du moteur ;
  à réexaminer si le sondage de 1 s devient un goulot.
- **Supprimer seulement le shell Electron en gardant `echos-ui`** : refusé. Il
  resterait un build Node, un job CI et une interface non utilisée par le
  présentatif choisi.

## Validation / rejet

- **Test de non-calcul** (ADR-003) : aucune vue de la fenêtre d'analyse ne
  produit une valeur scientifique ; les séries affichées sont celles de la
  réponse REST.
- **Test d'absence d'interface** : `test_no_interface_is_served` vérifie qu'une
  route de navigateur répond 404 JSON — toute réintroduction d'un montage
  statique échoue.
- **Test de tampon** : débordement, ordre de séquence, désabonnement couverts.
- **Test de console** : relevé, pause, filtres, effacement, plafonds couverts.
- **Test de la fenêtre d'analyse** : relevé complet depuis le port,
  indisponibilité affichée, sélection de métriques reconstruisant la courbe sans
  nouvelle lecture, un seul relevé à la fois, pas de lecture transmis au port sans
  interprétation.
- **Test des formes et des lacunes** : premier écran = profil de viabilité,
  trous de données rendus en **coordonnées de tick réelles**, métrique non
  mesurée incapable de passer pour une observation, relecture tronquant la courbe
  sans nouvelle lecture et synchronisant la vue 2D sur le tick du curseur.
- **Test des sous-écrans** : statistiques exactes rendues telles que publiées
  (provenance « repli neutre » comprise), comparaison affichant contexte et
  dispersion publiés (refus explicite sans valeur inventée), signaux des
  phénomènes affichés avec leurs seuils, graphe de confiance sans agrégat local,
  monde 2D lu au tick demandé, fiche d'entité affichant croyances/relations/
  décisions et signalant son indisponibilité sans vider l'écran.
- **Test ECHOS** : `world_initialized` persisté en contexte `world` au tick 0,
  contexte `resources` suivant la cadence d'`agents`, endpoints `/api/world` et
  `/api/trust-graph` déterministes et honnêtes en l'absence d'observation.
- **Porte de build** : `dotnet build --warnaserror` et `flake8` doivent rester
  sans avertissement.
- **Réouverture** : si ECHOS doit redevenir distribuable seul (paquet autonome
  sans Python), ou si le sondage de 1 s devient insuffisant en campagne dense.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 6 octobre 2026 | Manques P3 restants comblés (B1 → B4) : marqueurs d'événements (nouvel endpoint `/api/runs/{id}/events`), âge des contextes échantillonnés, état du run, outil de distribution (intervalles + effectifs) ; le curseur de relecture suit désormais le direct quand de nouveaux ticks sont publiés | Finir le plan P0 → P4 du RAPPORT d'analyse : les quatre derniers manques d'observation identifiés par revue de code |
| 6 octobre 2026 | Refonte P3 de la fenêtre d'analyse : parcours Viabilité → Comportements → Statistiques → Comparer → Relations → Monde → Entités, écran de viabilité par défaut, courbes en ticks réels avec trous et provenance, panneaux par unité, radar et histogramme retirés, barre de relecture, catalogue/viabilité/synthèse multi-runs consommés | Répondre aux P0 (faux chiffres, comparabilité invalide), P1 (registre versionné, statuts de données) et P3 (observation utile) du RAPPORT d'analyse |
| 5 octobre 2026 | Création : consoles natives, suppression de l'interface ECHOS, fenêtre d'analyse dans le Launcher | Finir la contradiction entre l'ADR-001/ADR-003 du Launcher et l'ADR-003 d'ECHOS, et rendre les logs lisibles en direct |
| 5 octobre 2026 | Élargissement : menu de sous-écrans (statistiques, confiance, monde 2D, fiche d'entité) et endpoints ECHOS `/api/world`, `/api/trust-graph` | L'interface retirée d'ECHOS portait ces vues ; les pertes d'information doivent être comblées par le même moteur, pas par un calcul local du Launcher |
