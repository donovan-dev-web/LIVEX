# ADR-003 : Dépendances embarquées et auto-suffisance du socle hors ligne

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-001, ADR-002
**Source Monographie** : —

---

## Contexte

La spécification exige une installation **hors ligne** : aucun téléchargement de
composant ni de dépendance pendant l'installation. Elle exige aussi qu'aucun
runtime ne soit considéré comme nécessaire « avant vérification des besoins
réels ». Cette vérification a été faite sur le dépôt :

| Composant | Runtime réel | Distribution actuelle | Conséquence pour un installateur |
| :-- | :-- | :-- | :-- |
| Launcher | .NET 10 (`net10.0`), Avalonia 11 | `dotnet publish` framework-dependent, sans RID ; la CI ne publie pas le Launcher | doit être publié **self-contained** par RID |
| SYNE | .NET 10 (`net10.0`) | CI : `-r win-x64` **sans** `--self-contained true` | doit être publié self-contained |
| ECHOS | **Python ≥ 3.11** (`echos/pyproject.toml`) + fastapi, uvicorn, pydantic, httpx, websockets, pyarrow | `echos-launcher` cherche un venv local, sinon `python` système | un Python système n'est pas une hypothèse acceptable |
| `syne-mock` | **Node 20+**, `src/cli.js` | npm | module facultatif : l'archive publiée doit embarquer son runtime Node (ADR-006, ADR-008) |
| PRISM | **application packagée** par plateforme, construite depuis le projet PRISM | charge utile de la distribution | composant de base coché par défaut et désactivable : aucune dépendance à Unreal Engine sur le poste (ADR-006) |
| PRISM-LDK | plugin Unreal (`PrismLdk`), C++ | archive ZIP | module facultatif : exige Unreal Engine dans le **projet hôte**, jamais sur le poste par l'installateur (ADR-006) |

Deux contraintes supplémentaires sont vérifiables dans le code :

1. Le Launcher déclare `InvariantGlobalization=false`
   (`launcher/Directory.Build.props`). Une publication self-contained .NET sur
   Linux dépend donc de **libicu** ; une machine minimale sans ICU échoue au
   démarrage avec « Couldn't find a valid ICU package ».
2. Le Launcher est une application graphique : sous Linux, il dépend de X11 ou
   Wayland, de fontconfig et des bibliothèques associées. L'installateur, s'il
   est graphique, a les mêmes dépendances.

## Décision

### 1. Binaires .NET : self-contained, par application

Le Launcher, SYNE et l'installateur sont publiés **self-contained** pour chaque
RID supporté (`win-x64`, `linux-x64`). Chaque application embarque son runtime
.NET ; aucun runtime partagé n'est installé sur le poste.

Le partage d'un runtime unique (dossier `runtime/dotnet` + applications
framework-dependent) est écarté en V0.2 : il économise de l'espace mais couple
les cycles de vie des composants, complique la réparation d'un composant isolé
et introduit une variable `DOTNET_ROOT` à maintenir. Il reste une optimisation
possible, à instruire par une mesure.

### 2. ECHOS : CPython embarqué, dépendances pré-installées

ECHOS est livré avec un **interpréteur CPython 3.12** (build autonome pour la
plateforme) et ses dépendances **déjà installées** dans son arborescence
(`components/echos/runtime` et `components/echos/site-packages`).

- Aucun `pip`, aucun `venv`, aucune résolution de dépendance à l'installation :
  le contenu est figé au build et vérifié par empreinte.
- La version livrée est **3.12** ; le minimum `>=3.11` de `pyproject.toml`
  reste la contrainte de développement, pas la version distribuée.
- Le script `echos-launcher` livré pointe vers l'interpréteur embarqué et
  n'utilise jamais un Python système.
- Les wheels sont construites ou récupérées pour chaque plateforme cible au
  moment du build, puis vérifiées par empreinte dans le manifeste de
  distribution.

### 3. Prérequis système Linux : détectés, jamais installés

L'installateur ne peut pas installer de bibliothèque système sans élévation. Il
**détecte** donc les prérequis et échoue avec un message actionnable :

| Prérequis | Usage | Comportement si absent |
| :-- | :-- | :-- |
| `libicu` | globalisation .NET (`InvariantGlobalization=false`) | préflight bloquant, message avec la commande d'installation de la distribution |
| `libssl` | HTTP/TLS du runtime .NET | préflight bloquant |
| X11 ou Wayland, fontconfig | interface graphique (Launcher, installateur) | mode `--unattended` disponible ; sinon message explicite |
| `glibc` ≥ version cible | binaires .NET et CPython | préflight bloquant |

L'embarquement app-local d'ICU (`System.Globalization.AppLocalIcu`) est
documenté comme solution de repli si une distribution supportée s'avère
dépourvue d'ICU ; il n'est pas retenu en V0.2, car il ajoute une bibliothèque
native et sa base de données à chaque paquet.

### 4. Le socle ne télécharge rien ; les modules facultatifs sont acquis

Le profil `core` contient tout le socle : Launcher, SYNE, ECHOS, **PRISM**,
contrats, ressources et build PRISM s'installent **sans aucune requête réseau**.
Aucune dépendance externe n'est téléchargée à l'installation.

Les **trois modules facultatifs** (PRISM-LDK, documentation hors ligne,
SYNE-Mock) suivent une règle distincte, définie par l'ADR-006 et l'ADR-008 : ils
sont acquis depuis les actifs de la release GitHub, uniquement lorsque
l'utilisateur les sélectionne, et vérifiés par l'empreinte signée du manifeste.
Le profil `full` les embarque pour les postes sans réseau.

Cette distinction est volontaire : « téléchargement automatique » (exclu par la
spécification de base) et « acquisition explicite d'un module choisi »
(autorisée) ne sont pas la même opération.

## Conséquences

### Positives

- Promesse hors ligne tenable et testable : le socle s'installe avec le seul paquet, sans réseau.
- Le poids des modules facultatifs n'est payé que par ceux qui les choisissent.
- Aucun runtime .NET ni Python à installer, aucune version système à négocier.
- Cycles de vie indépendants : mettre à jour SYNE ne touche pas au runtime du
  Launcher.
- ECHOS démarre de façon déterministe, avec des versions de bibliothèques
  figées et vérifiées.

### Négatives

- **Taille** : chaque binaire .NET self-contained pèse plusieurs dizaines de
  Mo ; CPython et ses dépendances également. Le paquet final est volumineux
  (ordre de grandeur à mesurer, pas à publier avant mesure). La compression du
  paquet est nécessaire.
- **Mises à jour de sécurité** : un runtime embarqué ne reçoit pas les correctifs
  du système. Chaque release de l'installateur doit reconstruire les runtimes
  avec les versions corrigées, et la politique de support doit l'assumer.
- **Duplication** : trois runtimes .NET (installateur, Launcher, SYNE) au lieu
  d'un partagé.
- **Prérequis Linux** : libicu/libssl restent une dépendance système réelle,
  contrairement à la promesse « aucun prérequis ». Elle est explicite et
  vérifiée, pas silencieuse.

### Risques

- **Écart de build entre plateformes** : un wheel ou un binaire publié pour une
  plateforme et pas pour l'autre. Mitigation : matrice de build obligatoire et
  manifeste de distribution refusé si un fichier manque.
- **Taille de téléchargement** : la build PRISM est désormais dans la charge
  utile de base, donc payée par tous. Mitigation : documenter la taille mesurée
  de chaque actif et de chaque charge utile, sans l'arrondir.

## Alternatives considérées

- **Runtime .NET partagé** : plus compact ; écarté en V0.2 pour l'indépendance
  des composants et la simplicité de réparation.
- **PyInstaller pour ECHOS** : un seul dossier, plus simple à produire ;
  écarté car les imports dynamiques (uvicorn, pyarrow) et les faux positifs
  antivirus rendent le résultat moins prévisible qu'un CPython embarqué dont
  les dépendances sont installées à plat.
- **Python système requis** : zéro embarquement ; écarté, car il contredit
  l'installation hors ligne pour un utilisateur non technique.
- **Embarquer ICU en app-local** : supprime le prérequis Linux ; écarté en
  V0.2 (poids, maintenance d'une bibliothèque native), réévaluable.
- **Télécharger les dépendances du socle à l'installation** : exclu par la
  spécification ; les modules facultatifs suivent l'ADR-008, qui ne couvre
  aucune dépendance tierce.

## Validation / rejet

- Installation du socle sur une machine Windows sans SDK .NET, sans Python et
  **sans réseau**, puis `livex-launcher --check` : le moteur et ECHOS sont
  détectés et démarrent.
- Même vérification sur une Ubuntu 22.04/24.04 vierge, sans Python installé et
  sans réseau.
- Sur une image Linux volontairement dépourvue de `libicu` : le préflight échoue
  avec le message attendu, sans écriture partielle.
- Le manifeste de distribution est refusé si un fichier d'une plateforme
  manque.

Réouverture si la taille du paquet devient un obstacle produit, ou si les
distributions cibles cessent de fournir libicu.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Vérification des besoins réels des composants |
