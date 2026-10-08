# SPECIFICATION.md — Installateur LIVEX

**Composant** : LIVEX (Installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `README.md`, `../docs-launcher/PACKAGING.md`, `../docs-launcher/COMPONENTS.md`, `../docs-launcher/NETWORK.md`
**Source Monographie** : —

---

## 1. Objet

Ce document spécifie **ce que fait** l'installateur : exigences, parcours,
options, modes d'exécution et codes de sortie. Le **comment** est dans
`ARCHITECTURE.md` ; la livraison et la mise à jour dans `RELEASE_AND_UPDATE.md`.

## 2. Exigences

Chaque exigence est vérifiable ; `TESTING.md` §6 la relie à un test.

### 2.1 Fonctionnelles

| ID | Exigence |
| :-- | :-- |
| INS-F01 | L'assistant guide l'utilisateur de la présentation à la fin d'installation en étapes ordonnées (§3), avec retour arrière possible tant que l'installation n'a pas commencé |
| INS-F02 | La licence est affichée en entier ; l'installation ne peut pas commencer sans acceptation explicite |
| INS-F03 | Le contrôle système est exécuté avant tout changement ; chaque résultat est classé **bloquant**, **avertissement** ou **information**, avec sa cause en clair |
| INS-F04 | L'utilisateur choisit un répertoire du programme et un répertoire du workspace, validés (existence, droit d'écriture, espace libre, chemin non ambigu) avant de continuer |
| INS-F05 | L'utilisateur choisit les composants à installer ; chaque option affiche sa taille de téléchargement et sa taille installée, et le total est comparé à l'espace libre |
| INS-F06 | Le Launcher, SYNE, ECHOS, le mock SYNE, PRISM-LDK et la documentation sont des options distinctes ; les dépendances entre options sont appliquées automatiquement et expliquées |
| INS-F07 | Les runtimes requis par un composant choisi sont installés avec lui (ADR-003) ; aucune installation système n'est faite sans l'accord de l'utilisateur |
| INS-F08 | La configuration automatique écrit les variables d'environnement utilisateur, les manifestes de release des composants installés, le registre d'installations et les raccourcis (§3, étape 8) |
| INS-F09 | L'installation se termine par l'exécution de `livex-launcher --check` ; son résultat est affiché et journalisé |
| INS-F10 | Une installation interrompue ou en échec est annulée ; l'état précédent est restauré |
| INS-F11 | L'installateur détecte une installation existante et propose : mettre à jour, réparer, modifier les composants, désinstaller |
| INS-F12 | La désinstallation retire le programme, les raccourcis et les variables ; elle ne supprime jamais le workspace, sauf demande explicite et confirmée |
| INS-F13 | Un mode non interactif réalise exactement les mêmes opérations que l'assistant (§5) |
| INS-F14 | Une mise à jour remplace un composant de façon atomique, conserve la version précédente jusqu'à validation, et refuse de s'exécuter pendant qu'un run est actif |
| INS-F15 | L'interface est en français ; les fichiers écrits sont en UTF-8, fins de ligne LF (`PACKAGING.md` §11) |

### 2.2 Non fonctionnelles

| ID | Exigence |
| :-- | :-- |
| INS-N01 | Aucune élévation de privilèges en installation par utilisateur |
| INS-N02 | Aucun artefact téléchargé n'est utilisé avant vérification de son empreinte SHA-256 |
| INS-N03 | Le binaire de l'installateur démarre sans runtime préinstallé (self-contained) |
| INS-N04 | Aucune écriture hors des répertoires choisis, du profil utilisateur (`~/.livex/`), des raccourcis et des variables utilisateur déclarées |
| INS-N05 | Journal d'installation lisible et stable, sans donnée personnelle hors chemins (§6) |
| INS-N06 | Annulation possible à tout moment pendant l'installation, avec retour à l'état initial |
| INS-N07 | Fonctionnement au clavier seul ; contraste conforme à la charte du Launcher |
| INS-N08 | Réseau : TLS obligatoire pour le catalogue et les artefacts ; aucun envoi de télémétrie |

## 3. Parcours de l'assistant

Dix étapes. Les étapes 1 à 6 ne modifient **rien** sur la machine.

| # | Étape | Contenu | Sortie / bloquant |
| :-- | :-- | :-- | :-- |
| 1 | **Présentation** | Nom, version, composants de la pile, ce qui sera installé, ce qui ne le sera pas (Unreal, SDK .NET) | Continuer |
| 2 | **Licence** | Texte du `LICENSE` embarqué et `THIRD-PARTY-NOTICES.md` ; case d'acceptation | Acceptation requise |
| 3 | **Contrôle système** | Voir §3.1 | Bloquants à résoudre ; avertissements acquittables |
| 4 | **Répertoires** | Programme et workspace (§3.2) | Chemins valides |
| 5 | **Composants et options** | Voir §4 ; tailles ; contrôle d'espace | Sélection cohérente et espace suffisant |
| 6 | **Récapitulatif** | Liste des actions, chemins, variables et raccourcis qui seront créés ; téléchargement total | Confirmation « Installer » |
| 7 | **Installation** | Progression globale et par étape ; ordre : runtimes, Launcher, SYNE, ECHOS, mock, PRISM-LDK, documentation ; annulation possible | Installation terminée ou annulée |
| 8 | **Configuration automatique** | Variables, manifestes de release, registre, raccourcis (§3.3) | Configuration écrite |
| 9 | **Vérification** | Exécution de `livex-launcher --check`, résultat par ligne | Rapport ; échec bloquant proposé en réparation |
| 10 | **Fin** | Résumé, chemin du journal, options « Lancer le Launcher » et « Ouvrir la documentation » | — |

L'installation des dépendances (« vérification des dépendances et installation »)
est répartie : **la détection** a lieu à l'étape 3, **la mise en place** au début de
l'étape 7, avant le composant qui en a besoin.

### 3.1 Contrôle système (étape 3)

| Contrôle | Critère | Classe |
| :-- | :-- | :-- |
| Système d'exploitation | Windows 10 22H2 ou 11 x64 ; Ubuntu LTS x64 (versions listées dans le catalogue) | Bloquant |
| Architecture | x64 | Bloquant |
| Espace disque | Libre sur le volume du programme et du workspace ≥ somme des tailles installées + marge de travail (seuil du workspace : OI à fixer, `PACKAGING.md` §12) | Bloquant |
| Droits d'écriture | Sur les deux répertoires et sur le profil utilisateur | Bloquant |
| Mémoire vive | Seuil minimal déclaré par le catalogue | Avertissement |
| Bibliothèques Linux | Présence des bibliothèques graphiques et d'internationalisation nécessaires à l'interface Avalonia (ex. X11, fontconfig, ICU) | Bloquant pour le Launcher |
| Session graphique | Linux : présence d'un affichage ; sinon proposer le mode non interactif | Avertissement |
| Réseau | Accès au catalogue ; sinon proposer le mode hors-ligne | Avertissement |
| Ports | Ports 5000, 5180, 5181 libres ; plage interne du Launcher 5200–5399 | Avertissement (le conflit est repris par `--check`) |
| Installation existante | Version détectée, manifeste de reçu valide | Information |
| Unreal Engine 5.8 | Détecté seulement si PRISM-LDK est sélectionné | Information |
| Antivirus / SmartScreen | Non détectable de façon fiable ; une aide textuelle est fournie en cas de blocage | Information |

Le contrôle système **réutilise** le code du Launcher pour les vérifications
communes (droits, espace, ports) afin que le résultat de l'étape 3 et celui de
l'étape 9 ne divergent pas.

### 3.2 Répertoires (étape 4)

| Répertoire | Windows (défaut) | Linux (défaut) | Variable |
| :-- | :-- | :-- | :-- |
| Programme | `%LOCALAPPDATA%\Programs\LIVEX` | `~/.local/share/livex` | — |
| Workspace | Défaut du Launcher (`%USERPROFILE%\.livex-data`) ou dossier visible (OI-02) | Défaut du Launcher (`~/.livex-data`) ou `~/LIVEX` (OI-02) | `LIVEX_DATA` |

Règles :

- les deux répertoires sont **distincts et non imbriqués** ;
- le programme ne peut pas être dans le workspace, ni l'inverse ;
- un workspace existant est réutilisé tel quel, jamais vidé ;
- un chemin dans un dossier synchronisé (OneDrive, Dropbox) déclenche un
  **avertissement** : les paquets `.livexp` peuvent être volumineux
  (`PACKAGING.md` §2.2, volumétrie élevée) ;
- les chemins contenant des caractères non portables sont signalés en
  avertissement.

### 3.3 Configuration automatique (étape 8)

| Élément | Action | Détail |
| :-- | :-- | :-- |
| `LIVEX_DATA` | Définie | Si le workspace diffère du défaut du Launcher |
| `LIVEX_HOME` | Définie | Seulement si la racine des composants n'est pas `components/` à côté de l'exécutable |
| Registre d'installations | Mis à jour | Ajout de la racine des composants dans `~/.livex/components.registry` (détection par priorité 4) |
| Manifestes de release | Écrits | `component.json` par composant, générés pour l'OS (`RELEASE_AND_UPDATE.md` §4) |
| Reçu d'installation | Écrit | `install-receipt.json` : versions, empreintes, chemins, date ; sert à la mise à jour, à la réparation et à la désinstallation |
| Raccourcis | Créés | Windows : menu Démarrer ; Linux : entrée `.desktop` et icône |
| Désinstallation | Déclarée | Windows : entrée « Applications installées » (HKCU) ; Linux : commande `livex-setup --uninstall` |
| Ports | Écrits au manifeste | Valeurs par défaut des manifestes, ou valeurs choisies si un conflit a été résolu |

Toute variable ou fichier écrit est listé à l'étape 6 et consigné dans le reçu,
ce qui permet de tout retirer à la désinstallation.

## 4. Composants et options

| Id | Contenu | Défaut | Dépend de | OS | Exclusion | Remarques |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| `launcher` | Application, assets, modèles (`templates/`) | Coché, obligatoire | — | Win, Linux | — | Distribué self-contained |
| `syne` | Moteur de simulation publié | Coché | `launcher` | Linux accepté ; Windows selon catalogue | Ports avec `syne-mock` | Exécutable publié, pas de SDK |
| `echos` | API d'analyse headless + environnement Python | Coché | `launcher` | Linux accepté ; Windows selon catalogue | — | Interface d'analyse portée par le Launcher (ADR-007 du Launcher) |
| `syne-mock` | Serveur Node.js de développement | Décoché | `launcher` | Win, Linux selon catalogue | Ports avec `syne` : un seul actif à la fois | Outil d'intégration, non scientifique (`INSTALLATION.md`) |
| `prism-ldk` | Plugin Unreal PRISM-LDK | Décoché ; masqué si Unreal 5.8 non détecté | `launcher` | Selon catalogue | — | Voir ADR-006 du Launcher : verrouillé tant qu'aucun manifeste PRISM accepté n'existe |
| `docs` | Documentation embarquée | Coché | — | Win, Linux | — | Lue par le lecteur Markdown du Launcher |
| `dev-tools` | Scripts de développement (`dev-stack.sh` et équivalents) | Décoché | `syne`, `echos` | Linux d'abord | — | Hors parcours utilisateur ; avec SDK exigé, jamais installé |

Un composant dont le catalogue ne déclare pas l'OS courant, ou qui n'est pas
marqué accepté, est **affiché grisé avec sa cause** ; il n'est pas installable.

Si `syne` et `syne-mock` sont tous deux sélectionnés, l'assistant l'affiche comme
**avertissement** (même ports 5180/5181) et propose deux issues : conserver les
deux avec des ports distincts pour le mock, ou n'en garder qu'un.

Les tailles affichées proviennent du catalogue (octets téléchargés et octets
installés, mesurés par la CI). Aucune taille n'est codée en dur dans
l'installateur.

## 5. Modes d'exécution

| Mode | Invocation | Usage |
| :-- | :-- | :-- |
| Assistant | `livex-setup` | Parcours des 10 étapes |
| Non interactif | `livex-setup --unattended` | Postes sans écran, CI, tests sur machines propres |
| Simulation | `livex-setup --dry-run` | Affiche le plan, ne modifie rien |
| Mise à jour | `livex-setup --update [--component <id>]` | Application des versions du catalogue |
| Réparation | `livex-setup --repair` | Re-vérifie les empreintes, restaure les fichiers manquants, regénère la configuration |
| Désinstallation | `livex-setup --uninstall [--purge-workspace]` | Retire le programme ; le workspace n'est retiré qu'avec l'option, après confirmation |

Options communes du mode non interactif :

| Option | Rôle |
| :-- | :-- |
| `--prefix <chemin>` | Répertoire du programme |
| `--workspace <chemin>` | Répertoire du workspace |
| `--components <liste>` | Ex. `launcher,syne,echos` |
| `--accept-license` | Acceptation explicite de la licence |
| `--catalog <url\|fichier>` | Catalogue alternatif |
| `--offline <répertoire>` | Source locale d'artefacts (OI-08) |
| `--no-shortcuts`, `--no-env` | Désactive raccourcis ou variables |
| `--log <fichier>` | Journal |
| `--json` | Sortie structurée pour la CI |

### 5.1 Codes de sortie

| Code | Signification |
| :-- | :-- |
| 0 | Succès |
| 1 | Erreur non classée |
| 2 | Arguments invalides |
| 3 | Prérequis bloquant non satisfait |
| 4 | Espace disque insuffisant |
| 5 | Échec d'intégrité (empreinte ou signature) |
| 6 | Conflit bloquant (port, installation incompatible) |
| 7 | Annulé par l'utilisateur ; état initial restauré |
| 8 | Installation terminée mais vérification finale en échec |
| 9 | Run actif : mise à jour refusée |

Les codes sont stables ; tout ajout est une évolution mineure documentée dans
`CHANGELOG.md`.

## 6. Journal d'installation

- Fichier `install-<horodatage UTC>.log` dans `<programme>/install/logs/`, en JSON
  ligne par ligne : étape, résultat, durée, cause.
- Horodatages en UTC (`PACKAGING.md` §11).
- Aucun contenu du workspace n'y figure.
- Le chemin du journal est affiché à l'étape 10 et en cas d'échec.

## 7. Cas d'erreur de référence

| Situation | Comportement attendu |
| :-- | :-- |
| Espace insuffisant en cours d'installation | Annulation, restauration, code 4 |
| Perte réseau pendant un téléchargement | Reprise de l'artefact, puis échec explicite après les nouvelles tentatives ; rien n'est installé à moitié |
| Empreinte incorrecte | Artefact rejeté et supprimé, code 5 |
| Port déjà utilisé | Avertissement à l'étape 3 ; l'installation reste possible ; `--check` le reporte à l'étape 9 |
| Fermeture de l'assistant pendant l'étape 7 | Confirmation, puis annulation et restauration |
| Installation existante plus récente | Refus de rétrograder, sauf option explicite |
| Composant en cours d'utilisation | Mise à jour refusée avec indication du processus (code 9) |
| Droits insuffisants sur un répertoire choisi | Blocage à l'étape 4 avec cause |

---

## Points restés ouverts dans ce document

- **Seuil d'espace du workspace** (INS-F03, §3.1) : dépend de la volumétrie d'une
  campagne (`PACKAGING.md` §12).
- **Versions Ubuntu et Windows supportées** : à fixer avec les premières
  validations sur machines propres (`TESTING.md` §4).
- **Mode Développeur** : périmètre de `dev-tools` et vérification du SDK, hors
  parcours utilisateur.
