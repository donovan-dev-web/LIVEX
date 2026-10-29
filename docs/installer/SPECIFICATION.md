# Spécification de l'installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT] — spécification de référence, en revue
**Version** : 1.0
**Dernière mise à jour** : 29 octobre 2026
**Dépend de** : [`adr/`](adr/README.md), [`ARCHITECTURE.md`](ARCHITECTURE.md)
**Remplace** : la spécification de base v0.2 (document de travail), archivée hors
de la documentation de référence

---

## 1. Objet

Ce document définit ce que l'installateur LIVEX doit faire, pour qui, dans
quelles limites, et comment on vérifie qu'il le fait. Il est **normatif** : les
exigences numérotées (`INS-F-*`, `INS-N-*`) sont vérifiables et servent de base
aux tests et aux issues.

Les décisions techniques qui expliquent le *pourquoi* sont dans les
[ADR](adr/README.md) ; la conception détaillée est dans
[`ARCHITECTURE.md`](ARCHITECTURE.md). En cas de contradiction, la présente
spécification prime sur l'architecture, et un ADR prime sur les deux pour le
sujet qu'il tranche.

## 2. Périmètre

### 2.1 Produit installé

L'installateur installe **LIVEX**, un écosystème de simulation :

| Composant | Rôle | Statut dans l'installation |
| :-- | :-- | :-- |
| **Launcher** | point d'entrée, orchestration, fenêtres d'observation | obligatoire |
| **SYNE** | moteur de simulation, autorité de l'état du monde | obligatoire |
| **ECHOS** | ingestion, analyse et API d'observation | obligatoire |
| **Contrats** | schémas et documents de contrat entre composants | obligatoire |
| **Ressources** | icônes, licences, mentions, fichiers de support | obligatoire |
| **PRISM** | application de rendu et d'interaction, **packagée** Windows/Linux | **composant de base, coché**, embarqué |
| **PRISM-LDK** | plugin Unreal distribué en **ZIP**, destiné au projet hôte de l'utilisateur | module facultatif, décoché |
| **Documentation hors ligne** | documentation consultable sans réseau, en archive | module facultatif, décoché |
| **SYNE-Mock** | émulateur du contrat SYNE pour le développement, archive avec son runtime | module facultatif, décoché |

Le **socle de base** — Launcher, SYNE, ECHOS, PRISM et les éléments de support —
est **embarqué** et s'installe sans réseau. **Seuls Launcher, SYNE et ECHOS sont
obligatoires** ; PRISM est un composant de base que l'utilisateur peut
désactiver, avec un avertissement sur les fonctionnalités perdues. Les **trois
modules facultatifs** (PRISM-LDK, documentation, SYNE-Mock) sont des **actifs de
la release GitHub**, acquis à la demande (profil `core`) ou embarqués dans la
variante complète (profil `full`) ; voir
[ADR-006](adr/ADR-006-perimetre-composants-v02.md) et
[ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md).

PRISM est une **application packagée** : Unreal Engine n'est pas requis sur le
poste pour l'utiliser. PRISM-LDK est le seul élément qui exige Unreal Engine,
dans le projet de l'utilisateur — l'installateur ne l'y installe pas lui-même.

### 2.2 Plateformes

| Plateforme | Support | Prérequis système | Notes |
| :-- | :-- | :-- | :-- |
| **Windows 10/11 x64** | supporté | aucun runtime .NET ni Python | installation par utilisateur, sans UAC |
| **Linux x64 (glibc)** | supporté | `libicu`, `libssl`, X11 ou Wayland, fontconfig | installation par utilisateur, sans `sudo` |
| macOS, Windows arm64, Linux arm64 | non supporté | — | hors périmètre |

Les versions minimales exactes de Linux (distribution et glibc) sont fixées par
la matrice de test de [`TESTING.md`](TESTING.md) et publiées dans les notes de
release ; elles ne sont pas inventées ici.

### 2.3 Hors périmètre

- mise à jour automatique ou vérification périodique de version ;
- installation machine, service système, tâche planifiée ;
- téléchargement **automatique** de composants ou de dépendances tierces ;
  l'acquisition **explicite** d'un module facultatif choisi par l'utilisateur
  est dans le périmètre (ADR-008) ;
- installation d'un module facultatif dans un projet Unreal : l'installateur
  dépose l'archive du LDK, il ne modifie pas le projet hôte ;
- déplacement automatique d'un espace de travail existant ;
- migration d'une installation produite par un autre outil (il n'en existe pas) ;
- association de fichiers, modification du `PATH`, variables d'environnement
  globales ;
- macOS et architectures autres que x64 ;
- signature ou chiffrement des paquets de données `.livexp` (sujet distinct).

## 3. Acteurs et définitions

| Terme | Définition |
| :-- | :-- |
| **Utilisateur** | personne qui installe, met à jour ou désinstalle LIVEX sur son compte |
| **Répertoire logiciel** | répertoire qui contient le Launcher, ses composants et leurs ressources |
| **Racine de données** | `LIVEX_DATA` si définie, sinon `~/.livex-data` (Windows : `%USERPROFILE%\.livex-data`) |
| **Espace de travail** | répertoire choisi par l'utilisateur pour ses expériences et rapports |
| **Résultats de recherche** | paquets `.livexp`, archives scellées, journaux de session, base d'analyse ECHOS |
| **Installation valide** | enregistrement présent, répertoire présent, `.livex-install.json` lisible, Launcher présent |
| **Charge utile** (*payload*) | ensemble des fichiers livrés par le paquet de distribution |
| **Distribution** | dossier ou archive contenant `livex-setup`, la charge utile, le manifeste et les signatures |
| **Composant de base** | PRISM : embarqué dans la distribution, **coché par défaut**, désactivable ; sa désactivation est avertie |
| **Module facultatif** | composant décoché par défaut, installable indépendamment : PRISM-LDK, documentation hors ligne, SYNE-Mock |
| **Actif de release** | fichier attaché à une release GitHub (archive d'un module facultatif) ; désigné par une URL, une taille et un SHA-256 |
| **Profil de distribution** | `core` (socle seul, modules acquis en ligne) ou `full` (tout embarqué, installation hors ligne complète) |
| **Mode maintenance** | exécution de l'installateur sur une installation existante (mise à jour, réparation, composants, désinstallation) |

## 4. Exigences fonctionnelles

### 4.1 Installation

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-01** | L'installateur doit installer le **socle** (Launcher, SYNE, ECHOS, contrats, ressources) sans connexion réseau, à partir de la seule distribution fournie. |
| **INS-F-02** | L'installateur doit fonctionner sans privilèges administrateur et sans élévation, sur les plateformes supportées. |
| **INS-F-03** | L'installateur doit refuser un répertoire logiciel non inscriptible par l'utilisateur courant, avec un message indiquant la cause, le chemin et une alternative. |
| **INS-F-04** | L'installateur doit refuser un répertoire logiciel qui est la racine d'un volume, le répertoire personnel, un répertoire système, ou qui chevauche la racine de données ou l'espace de travail. |
| **INS-F-05** | Avant toute écriture, l'installateur doit vérifier l'intégrité de la charge utile, les droits d'écriture, l'espace disque et la compatibilité de la plateforme. |
| **INS-F-06** | Une installation réussie doit produire un Launcher démarrable et une détection de SYNE et ECHOS par `livex-launcher --check`. |
| **INS-F-07** | L'installateur doit proposer de lancer LIVEX à la fin d'une installation réussie, option cochée par défaut. |
| **INS-F-08** | L'installateur doit pouvoir être interrompu par l'utilisateur aux étapes de saisie ; une interruption pendant l'écriture doit déclencher le nettoyage de la section 4.8. |
| **INS-F-09** | Aucune installation partielle ne doit être présentée comme réussie. |

### 4.2 Composants

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-10** | Les trois composants obligatoires — Launcher, SYNE, ECHOS — et leurs éléments de support (contrats, ressources) doivent être installés et ne doivent pas pouvoir être désélectionnés. |
| **INS-F-11** | PRISM, **composant de base**, doit être proposé et **coché par défaut** ; les trois modules facultatifs — PRISM-LDK, documentation hors ligne, SYNE-Mock — doivent être proposés **lorsqu'un actif vérifiable existe pour la plateforme**, indépendamment les uns des autres, et **décochés par défaut** ; sinon ils sont déclarés indisponibles (INS-F-18). |
| **INS-F-12** | L'écran de sélection doit être généré à partir du manifeste de distribution : un module n'est proposé que si son actif existe, embarqué ou publié, et vérifiable. |
| **INS-F-13** | Si PRISM est décoché, l'installateur doit afficher une description visible indiquant que les fonctionnalités de rendu et d'interaction 3D ne seront pas disponibles, sans bloquer la poursuite. |
| **INS-F-14** | Ajouter ou retirer un module facultatif après installation doit passer par le mode maintenance, sans réinstaller les composants obligatoires. |
| **INS-F-15** | Un composant facultatif retiré ne doit pas empêcher le démarrage des composants qui ne dépendent pas de lui. |
| **INS-F-16** | L'installateur doit traiter PRISM comme une **application packagée embarquée** dans la distribution, installée dans `components/prism/` avec son propre raccourci, sans téléchargement et sans exiger Unreal Engine sur le poste. |
| **INS-F-17** | L'installateur doit installer PRISM-LDK sous forme d'**archive ZIP intacte** dans `plugins/prism-ldk/`, et indiquer dans sa description qu'il est destiné à un projet Unreal Engine hôte ; il ne doit ni l'extraire dans LIVEX ni modifier un projet Unreal. |
| **INS-F-18** | Un module facultatif indisponible pour la plateforme doit être signalé comme indisponible avec sa raison, sans case à cocher, et ne doit jamais être installé partiellement. |
| **INS-F-19** | Un module facultatif déclaré `unavailable` ne doit pas empêcher l'installation du socle ni des autres modules sélectionnés. |

### 4.2 bis Acquisition des modules facultatifs (profil `core`)

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-100** | Aucune requête réseau ne doit être émise avant que l'utilisateur n'ait coché un module facultatif et validé le récapitulatif ; l'installateur ne doit jamais vérifier les versions ni télécharger en arrière-plan. |
| **INS-F-101** | Un actif distant doit être téléchargé en HTTPS depuis l'URL du manifeste signé, avec un certificat validé, une reprise par `Range` lorsque le serveur l'accepte, et un cache adressé par empreinte. |
| **INS-F-102** | La taille et le SHA-256 d'un actif téléchargé doivent être vérifiés **avant** toute extraction ou copie ; un actif non conforme doit être supprimé du cache et refusé. |
| **INS-F-103** | L'extraction doit refuser les entrées absolues, `..`, les liens symboliques et les périphériques, et ne jamais écrire hors du répertoire cible du module. |
| **INS-F-104** | L'utilisateur doit pouvoir annuler un téléchargement ; un échec réseau ne doit ni corrompre le socle ni laisser une installation partielle. |
| **INS-F-105** | En l'absence de réseau, l'installateur doit proposer de choisir une archive locale (vérifiée par la même empreinte) ou de poursuivre sans le module, et le profil `full` doit installer les trois modules sans réseau. |
| **INS-F-106** | Le manifeste de distribution utilisé doit être conservé dans l'installation (`.livex-distribution.json`) afin de permettre l'ajout ultérieur d'un module depuis le cache ou depuis l'URL d'origine. |

### 4.3 Chemins et données

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-20** | L'installateur doit demander séparément le répertoire logiciel et l'espace de travail, avec des valeurs par défaut proposées. |
| **INS-F-21** | Le répertoire logiciel par défaut doit être `%LOCALAPPDATA%\Programs\LIVEX` (Windows) et `~/.local/opt/livex` (Linux). |
| **INS-F-22** | L'espace de travail par défaut doit être `<racine de données>/workspace`. |
| **INS-F-23** | L'installateur doit refuser un espace de travail égal à la racine de données, ancêtre de celle-ci, égal au répertoire logiciel, ou contenant celui-ci. |
| **INS-F-24** | L'installateur doit écrire le chemin de l'espace de travail dans `<racine de données>/settings.json`, en préservant les clés inconnues et en écrivant atomiquement. |
| **INS-F-25** | L'installateur ne doit créer aucun fichier `livex.json` ni aucun fichier global jouant le même rôle (chemins d'installation, registre de composants). |
| **INS-F-26** | L'installateur doit créer la racine de données si elle n'existe pas, sans supprimer ni écraser son contenu existant. |
| **INS-F-27** | Un `settings.json` illisible ou invalide doit être conservé sous un nom d'archivage et signalé, jamais écrasé silencieusement. |
| **INS-F-28** | L'installateur doit placer le Launcher à la racine du répertoire logiciel, les composants dans `<répertoire logiciel>/components/<id>/` et l'archive du LDK dans `<répertoire logiciel>/plugins/prism-ldk/`, afin que la détection du Launcher fonctionne sans modification. |
| **INS-F-29** | Si le chemin mémorisé de l'espace de travail est inaccessible au lancement, le Launcher doit le signaler et permettre d'en choisir un autre ; il ne doit ni créer ni supprimer de données silencieusement. |

### 4.4 Parcours de l'assistant

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-30** | Le parcours d'installation doit comporter les étapes : accueil, langue, licence, composants, chemins, récapitulatif, installation, résultat. |
| **INS-F-31** | L'étape licence doit exiger l'acceptation de la licence LIVEX (GPL-3.0-or-later) avant de poursuivre, et rendre accessibles les mentions tierces. |
| **INS-F-32** | L'étape chemins doit valider chaque chemin au moment de la saisie et afficher la valeur effective ainsi que l'espace disque disponible. |
| **INS-F-33** | L'étape récapitulatif doit afficher : répertoire logiciel, espace de travail, composants sélectionnés, taille de la charge utile, taille à télécharger pour les modules facultatifs cochés, et opérations prévues (installation, mise à jour, réparation, téléchargement). |
| **INS-F-34** | L'étape installation doit afficher une progression par étape et par composant, et permettre l'annulation aux points sûrs. |
| **INS-F-35** | L'étape résultat doit distinguer succès, succès avec avertissements et échec ; en cas d'échec, elle doit afficher la cause, le chemin du journal et les résidus éventuels. |
| **INS-F-36** | Le retour en arrière entre étapes doit conserver les choix déjà saisis, sans relancer les opérations déjà effectuées. |
| **INS-F-37** | Si une installation valide est détectée, l'assistant doit ouvrir le parcours de maintenance (mise à jour, réparation, composants, désinstallation) au lieu d'un nouveau parcours d'installation. |

### 4.5 Intégration système

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-40** | L'installateur doit créer un raccourci de menu Démarrer (Windows) ou une entrée `.desktop` (Linux) pointant vers le Launcher. |
| **INS-F-41** | Le raccourci sur le bureau doit être proposé en option et décoché par défaut. |
| **INS-F-42** | Aucun raccourci séparé vers SYNE, ECHOS, SYNE-Mock ou le LDK ne doit être créé. Un raccourci vers PRISM est créé avec PRISM, car c'est une application destinée à l'utilisateur. |
| **INS-F-43** | L'installateur ne doit créer aucune association de fichiers, aucune modification du `PATH` et aucune variable d'environnement globale. |
| **INS-F-44** | L'installateur doit enregistrer l'installation pour l'utilisateur courant : clés `HKCU` sous Windows, fichiers XDG sous Linux (voir [ADR-005](adr/ADR-005-enregistrement-et-cycle-de-vie.md)). |
| **INS-F-45** | L'installateur doit écrire une entrée « Applications et fonctionnalités » sous Windows et un fichier d'installation XDG sous Linux, contenant le chemin et la version. |
| **INS-F-46** | Toutes les intégrations créées doivent être supprimables par la désinstallation, sans toucher aux intégrations préexistantes ou étrangères. |

### 4.6 Détection et cycle de vie

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-50** | L'installateur doit détecter une installation existante, y compris dans un répertoire personnalisé, sans dépendre de `livex.json`. |
| **INS-F-51** | Une installation valide détectée ne doit jamais être écrasée silencieusement ; l'installateur doit proposer la maintenance. |
| **INS-F-52** | Un enregistrement absent, obsolète ou incohérent doit être traité explicitement : localiser, réparer ou supprimer l'entrée, avec confirmation. |
| **INS-F-53** | L'installateur ne doit pas conclure qu'aucune installation n'existe du seul fait qu'un enregistrement est absent. |
| **INS-F-54** | Une mise à jour doit remplacer les fichiers du logiciel en conservant la racine de données, l'espace de travail, les modules facultatifs installés et les résultats de recherche. |
| **INS-F-55** | Une mise à jour vers une version inférieure doit être refusée, sauf option explicite documentée. |
| **INS-F-56** | Une réparation doit vérifier l'inventaire des fichiers installés et signaler les fichiers manquants ou corrompus ; elle ne doit pas modifier les données utilisateur. |
| **INS-F-57** | Une opération de mise à jour, de réparation ou de désinstallation doit refuser de s'exécuter si un processus LIVEX s'exécute depuis le répertoire logiciel, et demander sa fermeture. |
| **INS-F-58** | L'installation simultanée de deux instances enregistrées pour le même utilisateur doit être refusée par défaut. |
| **INS-F-59** | Le mode maintenance doit permettre de modifier l'espace de travail sans réinstaller le logiciel ; le nouveau chemin est validé, écrit dans `settings.json`, et l'ancien répertoire n'est ni déplacé ni supprimé. |

### 4.7 Désinstallation

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-60** | La désinstallation doit être accessible depuis l'assistant, depuis l'entrée système et par `--uninstall`. |
| **INS-F-61** | L'installateur doit proposer trois portées : A (logiciel), B (logiciel et configuration), C (logiciel, configuration et espace de travail). |
| **INS-F-62** | L'option A doit conserver `settings.json`, l'espace de travail, les paquets, les archives, les sessions et les journaux. |
| **INS-F-63** | L'option B doit supprimer `settings.json` et le cache, et conserver l'espace de travail, les paquets, les archives, les sessions et les journaux. |
| **INS-F-64** | L'option C doit afficher le chemin exact de l'espace de travail, un décompte de son contenu et exiger une confirmation explicite avant suppression. |
| **INS-F-65** | L'option C doit conserver les paquets, archives, sessions et journaux, sauf si l'utilisateur coche l'extension « supprimer aussi les résultats de recherche ». |
| **INS-F-66** | L'extension de l'option C doit afficher le nombre de paquets, le nombre d'archives, la taille totale et exiger une seconde confirmation distincte. |
| **INS-F-67** | Un espace de travail introuvable, déplacé ou inaccessible ne doit jamais être supprimé par approximation ; l'assistant doit le signaler et proposer de le localiser. |
| **INS-F-68** | La désinstallation doit supprimer les fichiers qu'elle a installés et les répertoires devenus vides ; elle doit conserver et signaler les fichiers non reconnus présents dans le répertoire logiciel. |
| **INS-F-69** | En cas d'échec, la désinstallation doit signaler les éléments non supprimés, leur emplacement et le chemin du journal, sans déclarer l'opération réussie. |

### 4.8 Erreurs, intégrité et journalisation

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-70** | L'installateur doit vérifier la signature du manifeste et l'empreinte SHA-256 de chaque fichier — embarqué ou téléchargé — avant toute écriture. |
| **INS-F-71** | Une altération, un fichier manquant, un actif non conforme ou un manifeste invalide doit interrompre l'opération avant toute modification du répertoire cible. |
| **INS-F-72** | Une erreur bloquante doit arrêter l'opération, produire un message compréhensible et écrire les informations techniques dans le journal. |
| **INS-F-73** | Après un échec d'installation, l'installateur doit supprimer les fichiers, raccourcis et enregistrements créés par la tentative, et restaurer les fichiers remplacés lorsqu'il en a conservé une copie. |
| **INS-F-74** | L'installateur ne doit pas promettre une restauration absolue de l'état antérieur ; il doit signaler les éléments qu'il n'a pas pu restaurer ou supprimer. |
| **INS-F-75** | Le journal doit contenir : horodatage UTC, étape, opération, résultat, code d'erreur éventuel, chemin concerné. Il ne doit contenir ni secret, ni contenu d'expérience, ni variables d'environnement complètes. |
| **INS-F-76** | Les journaux doivent être écrits dans `<racine de données>/logs/` et conservés par les options A, B et C, y compris l'extension de l'option C. |
| **INS-F-77** | Le journal doit être lisible en UTF-8 et rester exploitable après un échec ou une interruption brutale. |

### 4.9 Langues et accessibilité

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-80** | L'interface de l'installateur doit être disponible en français et en anglais, avec sélection à l'étape 2 et valeur par défaut déduite de la langue du système. |
| **INS-F-81** | La langue choisie doit s'appliquer à tous les écrans, messages, avertissements et récapitulatifs de l'installateur. |
| **INS-F-82** | Une traduction manquante doit retomber sur l'anglais et être signalée dans le journal, jamais afficher une clé brute à l'utilisateur. |
| **INS-F-83** | L'assistant doit être utilisable au clavier, avec un ordre de tabulation cohérent, des libellés d'accessibilité sur les contrôles et un contraste conforme aux règles d'accessibilité du projet. |
| **INS-F-84** | Les textes destinés aux composants eux-mêmes ne relèvent pas de l'installateur et ne doivent pas être dupliqués dans ses ressources. |

### 4.10 Modes non interactifs

| Réf. | Exigence |
| :-- | :-- |
| **INS-F-90** | `livex-setup --check` doit produire un diagnostic textuel stable, sans interface, et un code de sortie documenté. |
| **INS-F-91** | `livex-setup --unattended --config <fichier>` doit installer avec des réponses fournies, sans interaction, et produire le même résultat que l'assistant. |
| **INS-F-92** | `livex-setup --uninstall` doit permettre de choisir la portée par arguments ; sans argument, il applique la portée A (logiciel et intégrations) sans interaction. Le choix des portées se fait dans l'assistant de maintenance. |
| **INS-F-93** | `livex-setup --repair` doit vérifier l'installation et restaurer les fichiers disponibles dans la distribution d'origine. |
| **INS-F-94** | `livex-setup --maintenance` doit ouvrir le parcours de maintenance d'une installation existante. |
| **INS-F-95** | Les modes non interactifs doivent retourner : `0` succès, `1` erreur d'usage, `2` préflight en échec ou opération annulée, `3` échec d'installation, `4` désinstallation partielle, `5` installation déjà présente, `6` actif facultatif indisponible (réseau, 404, empreinte, `--no-network`). |
| **INS-F-96** | Aucun mode non interactif ne doit attendre une saisie ; en cas de décision impossible, il doit échouer avec un message explicite. |
| **INS-F-97** | `livex-setup --version` doit afficher la version de l'installateur, la version du produit et le profil de distribution. |
| **INS-F-98** | `livex-setup --offline-assets <dir>` doit résoudre les actifs des modules facultatifs depuis un répertoire local, en vérifiant leur empreinte, sans aucune requête réseau. |
| **INS-F-99** | `livex-setup --no-network` doit interdire toute requête ; si un module coché exige un actif absent du cache et du répertoire d'actifs, la commande échoue avec un message explicite et le code `6`. |

## 5. Exigences non fonctionnelles

| Réf. | Exigence |
| :-- | :-- |
| **INS-N-01** | **Sécurité** : aucune écriture hors du répertoire logiciel, de la racine de données (cache compris), de l'espace de travail et des emplacements d'intégration documentés. |
| **INS-N-02** | **Sécurité** : aucun secret, jeton ou contenu d'expérience dans les journaux ou les dialogues. |
| **INS-N-03** | **Sécurité** : tout chemin fourni par l'utilisateur est normalisé et validé avant usage ; les liens symboliques et les chemins relatifs piégés sont refusés là où ils permettraient une écriture hors périmètre. |
| **INS-N-04** | **Fiabilité** : le plan d'installation est calculé avant écriture ; une erreur pendant l'exécution laisse un état signalé, jamais un succès mensonger. |
| **INS-N-05** | **Testabilité** : `Setup.Core` ne dépend d'aucune interface et expose des points d'entrée testables sans écran ni registre réel. |
| **INS-N-06** | **Performance** : l'installateur affiche une progression continue, y compris pendant un téléchargement (octets, débit, module) ; aucune étape silencieuse ne dépasse 2 secondes sans retour visuel. La durée totale, la taille des distributions et la taille de chaque actif sont mesurées et publiées à chaque release, jamais estimées dans la documentation utilisateur. |
| **INS-N-07** | **Portabilité** : toute la logique métier est commune aux deux plateformes ; seules les opérations système (registre, XDG, raccourcis, processus) sont spécialisées. |
| **INS-N-08** | **Maintenabilité** : les exigences de ce document sont traçables vers au moins un test ou une issue ; une exigence sans test est un défaut de recette. |
| **INS-N-09** | **Compatibilité** : l'installateur ne référence aucun assemblage du Launcher et applique les contrats publiés ; il peut donc réparer un Launcher défaillant. |
| **INS-N-10** | **Accessibilité** : navigation clavier complète, libellés explicites, contraste conforme, aucune information portée par la couleur seule. |
| **INS-N-11** | **Réseau** : le socle ne fait aucune requête réseau ; un module facultatif n'en fait qu'après sélection explicite, en HTTPS, et l'installateur n'émet ni télémétrie ni requête d'API GitHub. |
| **INS-N-12** | **Souveraineté des données** : un actif téléchargé reste dans le cache local ; aucun contenu d'expérience, aucun identifiant d'installation n'est transmis. |

## 6. Critères d'acceptation

Une version est conforme lorsque les critères suivants sont vérifiés sur les
plateformes supportées, avec les preuves de [`TESTING.md`](TESTING.md).

### 6.1 Installation

- [ ] **INS-A-01** Installation du socle hors ligne sous Windows 11 x64, sans runtime .NET ni Python installé.
- [ ] **INS-A-02** Installation du socle hors ligne sous Linux x64 vierge, sans Python installé.
- [ ] **INS-A-03** Installation dans le répertoire par défaut et dans un répertoire personnalisé (`D:\LIVEX-demo`, `~/Documents/LIVEX`).
- [ ] **INS-A-04** Aucune élévation demandée ; aucune écriture `HKLM` ni hors du répertoire personnel.
- [ ] **INS-A-05** `livex-launcher --check` retourne `0` après installation, avec SYNE et ECHOS détectés.
- [ ] **INS-A-06** Le Launcher démarre depuis le raccourci créé.
- [ ] **INS-A-07** L'option « Lancer LIVEX » est cochée par défaut et fonctionne.

### 6.2 Composants et chemins

- [ ] **INS-A-08** Les composants obligatoires ne peuvent pas être désélectionnés.
- [ ] **INS-A-09** PRISM est coché par défaut, les trois modules facultatifs sont décochés par défaut ; l'absence de documentation hors ligne ne gêne pas le Launcher.
- [ ] **INS-A-10** PRISM décoché affiche la description des fonctionnalités indisponibles, sans bloquer l'installation.
- [ ] **INS-A-11** Le répertoire logiciel et l'espace de travail sont choisis séparément et affichés au récapitulatif.
- [ ] **INS-A-12** `settings.json` contient le chemin de l'espace de travail, les clés inconnues sont préservées, l'écriture est atomique.
- [ ] **INS-A-13** Aucun `livex.json` n'est créé, ni requis.
- [ ] **INS-A-14** Un espace de travail égal à la racine de données est refusé avec un message explicite.

### 6.3 Détection et cycle de vie

- [ ] **INS-A-15** Une installation dans un répertoire personnalisé est détectée au lancement suivant.
- [ ] **INS-A-16** Une seconde installation est refusée ; la maintenance est proposée.
- [ ] **INS-A-17** Un enregistrement supprimé est récupéré depuis `.livex-install.json` après confirmation.
- [ ] **INS-A-18** Un répertoire logiciel supprimé produit un message d'enregistrement orphelin et une proposition de nettoyage.
- [ ] **INS-A-19** Une mise à jour conserve `settings.json`, l'espace de travail et les paquets.
- [ ] **INS-A-20** Une mise à jour vers une version inférieure est refusée.
- [ ] **INS-A-21** Une réparation signale un fichier corrompu et le restaure depuis la distribution d'origine.

### 6.4 Désinstallation

- [ ] **INS-A-22** Option A : logiciel et intégrations supprimés ; `settings.json`, espace de travail et paquets intacts.
- [ ] **INS-A-23** Option B : `settings.json` et cache supprimés ; espace de travail et paquets intacts.
- [ ] **INS-A-24** Option C : espace de travail supprimé après affichage du chemin et confirmation ; paquets et archives intacts.
- [ ] **INS-A-25** Extension de C : paquets, archives et sessions supprimés après seconde confirmation ; journaux de l'installateur conservés.
- [ ] **INS-A-26** Un espace de travail déplacé est signalé et jamais supprimé.
- [ ] **INS-A-27** Aucune clé `HKCU\Software\LIVEX` ni fichier XDG appartenant à LIVEX ne subsiste après désinstallation.
- [ ] **INS-A-28** Des fichiers étrangers présents dans le répertoire logiciel sont conservés et signalés.

### 6.5 Intégrité, erreurs et journalisation

- [ ] **INS-A-29** Un fichier de charge utile modifié fait échouer l'installation avant toute écriture.
- [ ] **INS-A-30** Un fichier dont le SHA-256 diverge du manifeste est refusé avant écriture. Aucune signature cryptographique du manifeste n'est exigée en V1 (ADR-007 §3 bis).
- [ ] **INS-A-31** Une erreur simulée en cours d'installation laisse un état signalé, nettoie les fichiers créés et conserve les fichiers préexistants.
- [ ] **INS-A-32** Le journal contient les étapes, opérations, erreurs et résultats de nettoyage, sans secret ni contenu d'expérience.
- [ ] **INS-A-33** Le journal reste lisible après une interruption brutale du processus.

### 6.6 Interface, langues et modes non interactifs

- [ ] **INS-A-34** L'assistant est complet en français et en anglais.
- [ ] **INS-A-35** Un changement de langue met à jour tous les écrans sans redémarrer.
- [ ] **INS-A-36** L'assistant est utilisable entièrement au clavier.
- [ ] **INS-A-37** `--check` retourne `0` sur une installation conforme et `2` sur une installation dégradée, avec une sortie textuelle stable.
- [ ] **INS-A-38** `--unattended` produit une installation identique à l'assistant et ne demande aucune saisie.
- [ ] **INS-A-39** `--uninstall --scope a|b|c` fonctionne sans interaction.
- [ ] **INS-A-40** Les codes de sortie documentés sont respectés dans tous les modes.

### 6.7 Modules facultatifs et acquisition

- [ ] **INS-A-41** PRISM packagé : installation depuis la distribution **sans réseau** dans `components/prism/`, raccourci fonctionnel, sans Unreal Engine installé.
- [ ] **INS-A-42** PRISM-LDK : l'archive installée dans `plugins/prism-ldk/` est identique bit à bit à l'actif de la release ; aucune extraction dans LIVEX, aucune écriture dans un projet Unreal.
- [ ] **INS-A-43** SYNE-Mock : installé et démarrable sans Node système ; jamais lancé simultanément avec SYNE par l'installateur.
- [ ] **INS-A-44** Documentation hors ligne : archive installée dans `docs/`, lisible sans réseau.
- [ ] **INS-A-45** Un actif dont l'empreinte est modifiée est refusé avant extraction ; le socle et le cache restent cohérents.
- [ ] **INS-A-46** Une coupure réseau pendant le téléchargement permet la reprise ; aucun fichier partiel n'est installé.
- [ ] **INS-A-47** Profil `full` : PRISM et les trois modules s'installent sur une machine sans réseau.
- [ ] **INS-A-48** `--no-network` échoue avec le code `6` lorsqu'un actif manque, et réussit sans requête lorsqu'il est dans le cache.
- [ ] **INS-A-49** Aucune requête réseau n'est émise lors d'une installation `core` sans module facultatif (vérifié par capture réseau).

## 7. Écarts assumés par rapport à la spécification de base v0.2

| Point de la base | Décision retenue | Motif |
| :-- | :-- | :-- |
| Qt Installer Framework « envisagé » | installateur .NET autonome | [ADR-001](adr/ADR-001-technologie-installateur-dotnet.md) |
| PRISM « composant de visualisation et d'intégration à un moteur 3D » | PRISM est une **application packagée** Windows/Linux, installable ; le LDK est un ZIP séparé | [ADR-006](adr/ADR-006-perimetre-composants-v02.md) |
| PRISM, LDK, documentation et mock sans artefact dans le dépôt | PRISM est **embarqué** ; les trois modules sont proposés si un actif est publié, sinon déclarés indisponibles avec leur raison | [ADR-006](adr/ADR-006-perimetre-composants-v02.md) |
| PRISM « facultatif » sans statut de base | PRISM est un **composant de base embarqué** ; seuls Launcher, SYNE et ECHOS sont obligatoires | c'est ce qui donne un sens à l'avertissement §3.3 et garantit le rendu hors ligne |
| PRISM « désactivé par défaut » (§3.2, checklist §14) | PRISM est **coché par défaut** et **désactivable** ; le décocher déclenche l'avertissement §3.3 | arbitrage produit du 21 octobre 2026 : composant de base, son absence est une perte fonctionnelle, pas un choix neutre ; l'avertissement garde son rôle |
| Avertissement si PRISM non coché | **conservé** (base §3.3), affiché seulement si PRISM est disponible | une case réelle existe : l'avertissement a un sens ; il est remplacé par la raison d'indisponibilité sinon |
| Installation strictement hors ligne | socle **et PRISM** hors ligne ; les trois modules facultatifs sont acquis depuis la release, ou embarqués dans le profil `full` | [ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md) ; la base admet l'hors-ligne « lorsque les fichiers sont intégrés au paquet » (§1.1) |
| « Téléchargement automatique » hors périmètre (§1.3) | aucun téléchargement automatique ; l'acquisition d'un module **choisi par l'utilisateur** est autorisée | distinguer automatique et explicite ; rien n'est demandé au réseau avant validation du récapitulatif |
| `Plugins/PrismLDK.zip` | `plugins/prism-ldk/PrismLdk-<version>.zip`, archive intacte | plusieurs versions du plugin peuvent coexister pour plusieurs projets Unreal |
| « Espace de travail » non défini | répertoire d'expériences distinct de la racine de données | [ADR-004](adr/ADR-004-racine-de-donnees-et-espace-de-travail.md) |
| Persistance « adaptée au système » | `settings.json` dans la racine de données | [ADR-004](adr/ADR-004-racine-de-donnees-et-espace-de-travail.md) |
| Trois options de désinstallation | A/B/C + extension explicite pour les résultats de recherche | concilie protection des données et suppression complète |
| Arborescence `LIVEX/Launcher/` + `LIVEX/Components/` | Launcher à la racine, composants dans `components/` | la détection du Launcher lit `BaseDirectory/components` ; l'arborescence de la base casserait la détection sans modification du code |
| « Mise à jour automatique » hors périmètre | mise à jour manuelle en place supportée | une réinstallation guidée est nécessaire pour corriger une version |
| Deux chemins seulement | plus la racine de données (existante dans le code) | le modèle réel du Launcher comporte déjà paquets, archives, sessions |

Ces écarts sont des décisions, pas des oublis. Chacun renvoie à un ADR ou à une
justification vérifiable.

## 8. Points ouverts

| Réf. | Point | Échéance |
| :-- | :-- | :-- |
| **INS-O-01** | Versions Linux minimales supportées (distribution, glibc) | avant P2 |
| **INS-O-02** | Certificat Authenticode : OV ou Azure Trusted Signing, coût, automatisation | avant P5 |
| **INS-O-03** | Signature Linux : sans objet en V1 (sommes + attestations, ADR-007 §3 bis) ; rouverte si une signature centralisée est adoptée | clos en V1 |
| **INS-O-04** | Politique de support des runtimes embarqués (durée, cadence de reconstruction) | avant P2 |
| **INS-O-05** | Format de distribution Linux à long terme (`.deb`, Flatpak, AppImage) | après V0.2 |
| **INS-O-06** | Faisabilité d'une variante de maintenance allégée (sans interface) | après mesure de taille |
| **INS-O-07** | Embarquement app-local d'ICU si une distribution cible en est dépourvue | si un cas réel apparaît |
| **INS-O-08** | Comportement derrière un proxy d'entreprise ou un pare-feu filtrant GitHub | avant P3 |
| **INS-O-09** | Plafond et politique de purge du cache de téléchargement | avant P4 |
| **INS-O-10** | Intégration de PRISM au Launcher (bouton d'ouverture ou détection) | après la première build PRISM publiée |

## 9. Traçabilité

| Exigences | Décision | Implémentation | Vérification |
| :-- | :-- | :-- | :-- |
| INS-F-01 à 09 | ADR-001, ADR-003 | `Setup.Core` (plan, préflight, transaction) | INS-A-01 à 07 |
| INS-F-10 à 19 | ADR-006 | manifeste de distribution, écran Composants | INS-A-08 à 10, 41 à 44 |
| INS-F-20 à 29 | ADR-004, ADR-005 | `WorkspaceSettings`, `PathPolicy`, Launcher | INS-A-11 à 14, 26 |
| INS-F-30 à 37 | ADR-001 | `Setup.App` (assistant) | INS-A-34 à 36 |
| INS-F-40 à 46 | ADR-002, ADR-005 | `IIntegration` (`WindowsIntegration`, `LinuxIntegration`) | INS-A-05, 06, 27 |
| INS-F-50 à 59 | ADR-005 | `IInstallationStore`, `MaintenancePlanner`, `WorkspaceSettings` | INS-A-15 à 21 |
| INS-F-60 à 69 | ADR-004, ADR-005 | `UninstallPlanner` | INS-A-22 à 28 |
| INS-F-70 à 77 | ADR-007 | `InstallerEngine` (empreintes à la copie), `Journal` | INS-A-29 à 33 |
| INS-F-80 à 84 | ADR-001 | ressources FR/EN | INS-A-34, 35 |
| INS-F-90 à 99 | ADR-001 | `CommandLine` | INS-A-37 à 40, 48 |
| INS-F-100 à 106 | ADR-008 | `Acquisition` (téléchargement, cache, extraction) | INS-A-41, 45 à 49 |

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Remplace la spécification de base v0.2 comme référence |
| 29 octobre 2026 | INS-F-92 aligné sur le code, INS-A-30 révisé (D3), traçabilité vers les classes réelles | Check-up documentaire et plan d'alignement V1 |
