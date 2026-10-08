# ARCHITECTURE.md — Installateur LIVEX

**Composant** : LIVEX (Installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `SPECIFICATION.md`, `adr/ADR-001`, `adr/ADR-002`, `adr/ADR-003`, `../docs-launcher/COMPONENTS.md`, `../docs-launcher/PACKAGING.md`
**Source Monographie** : —

---

## 1. Principes d'architecture

1. **Cœur sans interface.** Toute la logique vit dans `Setup.Core`, testable sans
   fenêtre. L'assistant et le mode non interactif sont deux façades du même plan
   (ADR-001).
2. **Plan déclaratif.** L'installation est une liste d'étapes calculée avant toute
   écriture, affichable (`--dry-run`) et annulable.
3. **Journal de transaction.** Chaque écriture est enregistrée avant d'être faite ;
   l'annulation rejoue le journal à l'envers.
4. **Frontières d'assembly vérifiées par test**, comme pour le Launcher
   (`AssemblyBoundaryTests`, ADR-001 du Launcher).
5. **Aucune référence de projet** vers SYNE, ECHOS ou PRISM. L'installateur ne
   connaît les composants que par leur catalogue et leurs manifestes.

## 2. Découpage en projets

```text
installer/
├── Setup.Core/            Plan, étapes, journal, catalogue, intégrité, système de fichiers abstrait
├── Setup.Platform.Windows/  Variables HKCU, raccourcis, entrée de désinstallation
├── Setup.Platform.Linux/    XDG, .desktop, environment.d, contrôle des bibliothèques
├── Setup.App/             Assistant Avalonia (MVVM) et CLI (--unattended, --update, …)
├── Setup.Tests.Unit/      Plan, journal, annulation — système de fichiers simulé
├── Setup.Tests.Integration/  Installation réelle dans un répertoire temporaire
└── Setup.Tests.Clean/     Scénarios sur machines propres (hors CI standard)
```

| Assembly | Peut référencer | Ne peut pas référencer |
| :-- | :-- | :-- |
| `Setup.Core` | BCL uniquement (+ bibliothèque de hachage et de lecture ZIP standard) | Avalonia, projets de plateforme |
| `Setup.Platform.*` | `Setup.Core` | Avalonia |
| `Setup.App` | `Setup.Core`, `Setup.Platform.*`, Avalonia | Tout composant LIVEX |

La réutilisation de code du Launcher (détection, contrôles `--check`) se fait en
**référençant les assemblies existants** `Launcher.Infrastructure` pour les
contrôles et le détecteur de manifestes, à l'exclusion de `Launcher.Presentation`.
Si la dépendance se révèle couplante, les contrôles communs sont extraits dans un
assembly partagé `Launcher.Environment` (point ouvert, §10).

## 3. Plan d'installation

### 3.1 Modèle

```text
InstallPlan
 ├─ Context        (OS, prefix, workspace, sélection, catalogue résolu)
 └─ Steps[]        (ordonnées, avec dépendances)

Step
 ├─ Id, Libellé, Poids (pour la progression)
 ├─ Preconditions()   → Résultat
 ├─ Do(ctx, journal)  → Résultat
 ├─ Undo(ctx, journal)
 └─ Verify(ctx)       → Résultat
```

Propriétés exigées :

| Propriété | Règle |
| :-- | :-- |
| Idempotence | Rejouer `Do` sur un état déjà conforme ne change rien (réparation, reprise) |
| Annulation | `Undo` ramène à l'état avant `Do`, y compris les variables et raccourcis |
| Déterminisme du plan | Même contexte et même catalogue donnent le même plan |
| Pureté du calcul | Le calcul du plan n'écrit rien |

### 3.2 Étapes types (ordre de V0.1)

| # | Étape | Remarque |
| :-- | :-- | :-- |
| 1 | `PrepareDirectories` | Crée le programme et le workspace s'ils n'existent pas |
| 2 | `FetchArtifacts` | Télécharge, vérifie l'empreinte, extrait dans un répertoire de travail du programme |
| 3 | `InstallRuntimes` | Runtimes embarqués : `uv`, Node (si mock) — §5 |
| 4 | `InstallLauncher` | Extraction dans `launcher/` |
| 5 | `InstallComponent(syne)` | Extraction dans `launcher/components/syne/` |
| 6 | `InstallComponent(echos)` | Extraction, puis construction de l'environnement Python — §5.2 |
| 7 | `InstallComponent(syne-mock)` | Extraction, puis installation des modules Node si nécessaire |
| 8 | `InstallComponent(prism-ldk)` | Copie du plugin dans le projet Unreal désigné — §5.4 |
| 9 | `InstallDocs` | Documentation embarquée |
| 10 | `WriteConfiguration` | Variables, registre, manifestes de release, reçu — §6 |
| 11 | `CreateShortcuts` | Spécifique à la plateforme |
| 12 | `RegisterUninstall` | Spécifique à la plateforme |
| 13 | `VerifyInstallation` | Exécute `livex-launcher --check` |

### 3.3 Journal de transaction

- Fichier `install/transaction.jsonl` dans le répertoire du programme, écrit en
  ajout avant chaque action : type, cible, état précédent (existence, empreinte).
- En cas d'échec ou d'annulation, rejoué en ordre inverse.
- En cas d'arrêt brutal (coupure, processus tué), au relancement, l'installateur
  détecte un journal non terminé et propose de **restaurer** ou de **reprendre**.
- Le journal est supprimé à la fin d'une installation réussie ; le reçu
  (`install-receipt.json`) est le seul état durable.

### 3.4 Remplacement atomique

Un composant est installé dans `components/<id>.new/`, validé, puis substitué par
renommage :

```text
components/syne/        ← version en service
components/syne.new/    ← version préparée et validée
        ↓ renommages successifs
components/syne/        ← nouvelle version
components/syne.prev/   ← version précédente conservée jusqu'à validation finale
```

Le renommage est la seule opération non interruptible ; elle est précédée et
suivie d'une écriture du journal pour que la reprise soit déterministe.

## 4. Arborescence installée

Elle suit `PACKAGING.md` §2.1 : composants dans `components/` à côté de
l'exécutable (priorité 1 du détecteur).

```text
<programme>/
├── launcher/
│   ├── livex-launcher[.exe]
│   ├── *.dll, assets, docs/
│   └── components/
│       ├── syne/         component.json, binaires publiés
│       ├── echos/        component.json, sources, environnement Python
│       ├── syne-mock/    component.json, sources, modules Node
│       └── prism-ldk/    présent seulement si PRISM est livré
├── runtimes/
│   ├── uv/               binaire uv, installations Python gérées par uv
│   └── node/             runtime Node, si le mock est installé
├── templates/            modèles de paquet, de campagne, de profil
├── install/
│   ├── install-receipt.json
│   ├── logs/
│   └── (transaction.jsonl pendant une installation)
├── LICENSE
├── THIRD-PARTY-NOTICES.md
└── VERSION

<workspace>/             (LIVEX_DATA)
├── preferences/  sessions/  packages/  archives/  cache/    — propriété du Launcher
```

Le reçu `install-receipt.json` contient : version de l'installateur, version du
catalogue, composants (id, version, empreinte de l'artefact, chemin), variables
et fichiers écrits hors du programme, date UTC. Il est la source de la
désinstallation et de la réparation.

## 5. Dépendances

### 5.1 Stratégie générale

| Dépendance | Besoin | Stratégie | Contrôle à l'étape 3 |
| :-- | :-- | :-- | :-- |
| .NET (Launcher, SYNE) | Exécution | **Publication self-contained** par OS ; le SDK n'est pas requis | Aucun |
| Python (ECHOS) | API FastAPI, ingestion, analyse | **`uv` embarqué** : installation d'un interpréteur géré, puis synchronisation depuis `uv.lock` | Accès réseau, ou artefacts hors-ligne |
| Node (mock) | Serveur de développement | **Runtime Node embarqué** dans le paquet du mock | Aucun |
| Bibliothèques système Linux | Interface Avalonia, internationalisation | **Détectées, jamais installées** ; message avec la commande de paquet à lancer | Bloquant pour le Launcher |
| Unreal Engine 5.8 | Plugin PRISM-LDK | **Détecté**, jamais installé | Information |
| Navigateur par défaut | Ouverture de la documentation externe | Information (comme `--check`) | Information |

Raison d'ensemble : l'installateur ne doit pas modifier la configuration système
de l'utilisateur pour fonctionner (ADR-003). Le SDK .NET, Visual Studio et Unreal
restent des prérequis de **développement**, décrits dans `INSTALLATION.md`.

### 5.2 ECHOS et l'environnement Python

L'étape `InstallComponent(echos)` :

1. extrait les sources d'ECHOS ;
2. demande à `uv` d'installer l'interpréteur de la version cible (OI-03) dans
   `runtimes/uv/` ;
3. crée `components/echos/.venv` et y installe les dépendances figées d'après
   `uv.lock` (`--frozen`) ;
4. vérifie l'import des dépendances natives (par exemple `pyarrow`) ;
5. génère le lanceur d'ECHOS pour l'OS (voir §6.3).

Si le réseau est absent, l'étape utilise les roues (*wheels*) fournies par le
paquet hors-ligne (`RELEASE_AND_UPDATE.md` §7). Si ni l'un ni l'autre n'est
disponible, ECHOS est désélectionné à l'étape 5 avec la cause.

### 5.3 Linux : bibliothèques

L'étape 3 interroge le système (`ldconfig -p`, présence de fichiers connus) pour la
liste déclarée par le catalogue. En cas d'absence, l'assistant affiche la liste
manquante et la commande correspondante pour Ubuntu (`apt install …`), et **ne
l'exécute pas** : l'élévation est refusée par principe (INS-N01).

### 5.4 PRISM-LDK

PRISM-LDK est un plugin d'Unreal Engine 5.8. L'installateur :

- détecte les installations d'Unreal (registre Epic sous Windows, emplacements
  usuels sous Linux) ;
- demande le **projet hôte** auquel ajouter le plugin ;
- copie `Plugins/PrismLdk/` dans ce projet, sans le compiler ;
- affiche la procédure de compilation (`docs/docs-prism/`).

Tant qu'aucun manifeste PRISM accepté n'existe (ADR-006 du Launcher), l'option est
affichée **verrouillée** : l'installateur ne prétend pas rendre PRISM utilisable
depuis le Launcher.

## 6. Configuration automatique

### 6.1 Variables et registre

| Mécanisme | Windows | Linux | Quand |
| :-- | :-- | :-- | :-- |
| `LIVEX_DATA` | Variable utilisateur (HKCU\Environment) + diffusion du changement | `~/.config/environment.d/50-livex.conf` + entrée `.desktop` (`Exec=env LIVEX_DATA=… livex-launcher`) | Si le workspace diffère du défaut du Launcher |
| `LIVEX_HOME` | Idem | Idem | Si les composants ne sont pas dans `components/` à côté de l'exécutable |
| Registre d'installations | Ligne ajoutée à `%USERPROFILE%\.livex\components.registry` | Ligne ajoutée à `~/.livex/components.registry` | Toujours : mécanisme sans variable d'environnement |

Le **registre d'installations** est le mécanisme principal, car il ne dépend
d'aucun contexte de lancement (raccourci, terminal, session). Les variables ne
servent qu'à communiquer l'emplacement du workspace. Le risque d'un workspace
« perdu » quand `LIVEX_DATA` est absent d'un contexte de lancement est le point
OI-05 : il faut un mécanisme de persistance côté Launcher (par exemple un fichier
de configuration utilisateur lu avant la variable). Ce changement est un
**prérequis externe** de l'installateur (`ROADMAP.md` §3).

### 6.2 Manifestes de release

Les `component.json` du dépôt déclarent des chemins de développement (par exemple
`Simulation.Console/bin/Release/net10.0/publish/Simulation.Console`) et un seul
OS. L'installateur ne les copie pas tels quels : il écrit, pour chaque composant,
le manifeste **de release** produit par la CI (`RELEASE_AND_UPDATE.md` §4), avec :

- l'exécutable propre à l'OS d'installation ;
- les ports (valeurs par défaut ou choisies) ;
- la version réelle du composant ;
- les clés `capabilities`, `health`, `timeouts` du manifeste d'origine.

Chaque manifeste écrit est validé par le schéma versionné
(`launcher/contracts/component-manifest-v1.schema.json`) avant d'être activé ;
un manifeste invalide fait échouer l'étape et déclenche l'annulation.

### 6.3 Lanceurs des composants scripts

| Composant | Linux | Windows |
| :-- | :-- | :-- |
| ECHOS | Script d'amorçage qui choisit l'interpréteur du `.venv` | Lanceur équivalent (fichier de commandes ou exécutable minimal) appelant l'interpréteur du `.venv` |
| syne-mock | Appel du runtime Node embarqué sur `src/cli.js` | Idem |

Ces lanceurs sont des **artefacts de release**, versionnés avec le composant, non
générés au vol par l'installateur, afin que leur comportement soit testé en CI.

### 6.4 Ports

| Port | Composant | Remarque |
| :-- | :-- | :-- |
| 5000 | ECHOS (API) | |
| 5181 | SYNE ou mock (contrôle HTTP) | Exclusif entre SYNE et mock |
| 5180 | SYNE ou mock (WebSocket) | Exclusif entre SYNE et mock |
| 5200–5399 | Launcher (plage interne) | Hors manifestes |

Si l'utilisateur garde SYNE et le mock, l'installateur propose de décaler le mock
(`endpoints.*.port` du manifeste de release) et rappelle qu'un seul est actif à la
fois par défaut. Le choix est inscrit au reçu.

## 7. Plateformes

### 7.1 Windows

| Sujet | Choix |
| :-- | :-- |
| Portée | Utilisateur (HKCU), sans élévation |
| Installation par défaut | `%LOCALAPPDATA%\Programs\LIVEX` |
| Désinstallation | Clé `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\LIVEX` |
| Raccourcis | Dossier du menu Démarrer de l'utilisateur |
| Variables | `HKCU\Environment` + notification de changement de l'environnement |
| Arrêt propre d'un composant sans console | Dépend d'O-34 (`INTEGRATION_CONTRACT.md` §5.1) |
| Signature | Authenticode, selon OI-04 ; sans signature, SmartScreen avertit au premier lancement |

### 7.2 Linux (Ubuntu)

| Sujet | Choix |
| :-- | :-- |
| Portée | Utilisateur ; installation système hors V0.1 |
| Installation par défaut | `~/.local/share/livex` (conforme XDG) |
| Raccourcis | `~/.local/share/applications/livex.desktop`, icône dans le thème utilisateur |
| Commande | Lien symbolique `~/.local/bin/livex-launcher` ; avertissement si ce dossier n'est pas dans le `PATH` |
| Variables | `environment.d` pour la session graphique ; pas de modification de `.bashrc` |
| Désinstallation | `livex-setup --uninstall` (le binaire est copié dans `install/`) |
| Distribution | Archive avec binaire, puis `.deb` (OI-01) ; l'AppImage n'est pas retenue seule |

## 8. Sécurité

| Menace | Mesure |
| :-- | :-- |
| Artefact altéré | Empreinte SHA-256 vérifiée avant extraction ; catalogue signé (OI-04) |
| Archive piégée (chemins `..`, liens) | Règles de nom d'entrée appliquées à l'extraction, sur le modèle de `PackageEntryRules` du Launcher |
| Rétrogradation malveillante | Version du catalogue comparée au reçu ; rétrogradation refusée sans option explicite |
| Injection par chemin ou argument | Aucune commande n'est construite par concaténation ; arguments passés en liste |
| Élévation involontaire | Aucun appel élevé ; refus explicite si lancé en administrateur ou en `root` sans l'option correspondante |
| Écoute réseau | Aucun service lancé par l'installateur ; les manifestes restent en boucle locale IPv4 (`V1-CAPABILITY-MATRIX.md` §5) |
| Fuite de données | Aucune télémétrie ; journal limité aux chemins et résultats |

## 9. Journalisation et diagnostic

- Journal JSON ligne par ligne (`SPECIFICATION.md` §6).
- `livex-setup --check` : relit le reçu, vérifie les empreintes des fichiers
  installés et appelle `livex-launcher --check`.
- Un échec d'étape affiche : étape, cause, action de restauration, chemin du
  journal.

## 10. Points restés ouverts dans ce document

- **Réutilisation du code du Launcher** : référence directe à
  `Launcher.Infrastructure` ou extraction d'un assembly commun.
- **Persistance du workspace** (OI-05) : mécanisme côté Launcher.
- **Registre d'installations** : format actuel (une ligne par répertoire) suffisant
  pour porter la version et l'OS, ou à enrichir.
- **Lanceur ECHOS sous Windows** : forme exacte (fichier de commandes, exécutable
  minimal) à décider avec l'acceptation d'ECHOS sous Windows.
- **Mise à jour du Launcher** (OI-06) : un exécutable en cours d'exécution ne peut
  pas se remplacer ; processus assistant ou bibliothèque tierce à évaluer
  (`adr/ADR-004`).
