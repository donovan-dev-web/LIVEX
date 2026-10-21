# ADR-006 : Périmètre, nature et provenance des composants

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : ADR-003, ADR-008
**Source Monographie** : —
**Remplace** : les versions du 21 octobre 2026 qui excluaient PRISM du périmètre, puis qui le traitaient comme un module téléchargé

---

## Contexte

La spécification de base (v0.2) distingue deux niveaux :

- **composants obligatoires** : Launcher, SYNE, ECHOS, contrats, ressources ;
- **composants facultatifs, décochés par défaut** : PRISM, PrismLDK,
  documentation hors ligne, SYNE-Mock — c'est la nomenclature de la base ; la
décision ci-dessous en distingue trois niveaux.

Elle exige aussi (§3.3) qu'une description visible prévienne l'utilisateur
lorsque **PRISM n'est pas sélectionné**, parce que des fonctionnalités du
produit disparaissent. Un avertissement de perte fonctionnelle n'a de sens que
si le composant concerné fait partie de l'installation de référence : c'est un
**composant de base**, que l'on peut désactiver, pas une extension lointaine.

Le mainteneur a confirmé cette lecture :

- **seuls Launcher, SYNE et ECHOS sont obligatoires** ;
- **PRISM est un composant de base**, **coché par défaut** et désactivable, dont
  la désactivation déclenche l'avertissement ;
- **PRISM-LDK**, la **documentation hors ligne** et **SYNE-Mock** sont les
  modules facultatifs, acquis depuis les releases GitHub.

Deux erreurs de la rédaction précédente sont donc corrigées :

1. PRISM n'est pas un projet à compiler par l'utilisateur : c'est une
   **application packagée** pour Windows et Linux, livrée avec la distribution.
2. PRISM n'est pas un module téléchargé : il est **embarqué**, car c'est le
   composant de base qui porte le rendu ; seuls le LDK, la documentation et le
   mock sont acquis à la demande.

## Décision

### 1. Trois niveaux, pas deux

| Niveau | Composants | Sélection | Provenance | Réseau |
| :-- | :-- | :-- | :-- | :-- |
| **Obligatoire** | Launcher, SYNE, ECHOS | verrouillé | charge utile de la distribution | aucun |
| **Support obligatoire** | contrats, ressources | verrouillé | charge utile | aucun |
| **Base** | **PRISM** | **coché par défaut**, désactivable ; désactivation avertie | charge utile de la distribution | aucun |
| **Module facultatif** | PRISM-LDK, documentation hors ligne, SYNE-Mock | décoché par défaut | actif de la release, ou charge utile du profil `full` | requis, sauf profil `full` |

Les contrats et les ressources ne sont pas des composants au sens produit : ce
sont des éléments de support, installés avec les trois obligatoires et non
décochables. La formulation « seuls Launcher, SYNE et ECHOS sont obligatoires »
reste donc exacte.

### 2. PRISM : application packagée, embarquée, désactivable

| Élément | Décision |
| :-- | :-- |
| **Nature** | application **packagée** par plateforme (`win-x64`, `linux-x64`), build autonome |
| **Runtime requis** | aucun : Unreal Engine n'est ni installé ni nécessaire sur le poste |
| **Provenance** | `payload/` de la distribution, `acquisition.kind = "payload"` |
| **Installation** | `components/prism/` |
| **Sélection** | **cochée par défaut** ; l'utilisateur peut la décocher. Écart assumé par rapport à la base §3.2 et à sa checklist §14, qui la veulent décochée : PRISM étant un composant de base, son absence est une perte fonctionnelle, pas un choix neutre |
| **Désactivation** | message visible : les fonctionnalités de rendu et d'interaction 3D ne seront pas disponibles ; poursuite possible |
| **Intégration** | raccourci de menu propre à PRISM, car c'est une application destinée à l'utilisateur |
| **Contrat de composant** | aucun `component.json` : PRISM n'expose pas de service de contrôle |

PRISM n'est donc pas « un projet Unreal » dans cette documentation : ce que
l'installateur manipule est une build packagée. La chaîne qui la produit relève
de PRISM, pas de l'installateur.

### 3. PRISM n'est pas un service du Launcher

Le contrat `component-manifest-v1` exige un point d'accès `control` HTTP : il
décrit des services que le Launcher démarre et pilote. PRISM est une application
que l'utilisateur lance ; il consomme SYNE et n'expose pas de service.

Conséquences :

- PRISM n'a pas de `component.json` et n'est pas détecté par `ManifestDetector`
  (un répertoire sans manifeste est ignoré silencieusement) ;
- il reçoit son propre raccourci ; la règle « aucun raccourci vers les
  composants internes » vise SYNE, ECHOS et les outils, pas l'application de
  rendu ;
- si PRISM expose un jour un contrat de service, il pourra être détecté sans
  changer l'installateur ; ce n'est pas une condition de la V0.2.

### 4. Les trois modules facultatifs

| Module | Nature | Installation | Runtime requis sur le poste |
| :-- | :-- | :-- | :-- |
| **PRISM-LDK** | archive **ZIP** du plugin `PrismLdk` | `plugins/prism-ldk/PrismLdk-<version>.zip`, conservée intacte | Unreal Engine, dans le **projet hôte** de l'utilisateur |
| **Documentation hors ligne** | archive ZIP de documents Markdown | `docs/` | aucun |
| **SYNE-Mock** | archive ZIP du mock **et de son runtime Node** | `components/syne-mock/` | aucun |

Règles :

- **PRISM-LDK** n'est jamais installé « dans » un projet Unreal par
  l'installateur : il dépose l'archive, l'utilisateur choisit le projet hôte.
  L'archive doit être **bit à bit** celle de la release.
- **SYNE-Mock** possède un `component.json` (`runtime: script`, contrôle HTTP
  5181, WebSocket 5180) : il est détectable et entre en conflit de ports avec
  SYNE. Les deux ne s'exécutent jamais ensemble ; l'installateur ne démarre ni
  l'un ni l'autre. Une archive sans runtime Node ne doit pas être publiée.
- **La documentation** ne doit plus être copiée dans la publication du Launcher
  (`Launcher.App.csproj`) : sinon le module facultatif serait toujours présent.
  Le Launcher doit fonctionner sans `docs/` (issue dédiée).

### 5. Aucune case sans artefact, aucun module fantôme

L'écran Composants est généré depuis le manifeste. Un module facultatif dont
l'actif n'est pas publié pour la plateforme est affiché **indisponible**, avec sa
raison, **sans case à cocher**. PRISM, embarqué, est toujours disponible avec la
distribution ; il n'a pas de cas « indisponible ».

### 6. Arborescence installée

```text
<répertoire logiciel>/
├── livex-launcher[.exe]              # obligatoire, à la racine
├── livex-setup[.exe]
├── components/
│   ├── syne/                         # obligatoire
│   ├── echos/                        # obligatoire
│   ├── prism/                        # base, coché par défaut
│   └── syne-mock/                    # module facultatif
├── plugins/
│   └── prism-ldk/
│       └── PrismLdk-<version>.zip    # module facultatif
├── contracts/
├── docs/                             # module facultatif
├── assets/
└── …
```

Écarts assumés avec l'arborescence fonctionnelle de la base (§4.1) :

| Base | Retenu | Motif |
| :-- | :-- | :-- |
| `LIVEX/Launcher/` | Launcher à la racine | `ManifestDetector` lit `BaseDirectory/components` ; un sous-répertoire casserait la détection |
| `Components/` | `components/` | casse identique à celle du code et des manifestes ; Linux est sensible à la casse |
| `Plugins/PrismLDK.zip` | `plugins/prism-ldk/PrismLdk-<version>.zip` | plusieurs versions du plugin peuvent coexister pour plusieurs projets Unreal ; l'archive reste intacte |
| `Docs/`, `Assets/` | `docs/`, `assets/` | même convention que le reste du dépôt |

## Conséquences

### Positives

- La hiérarchie correspond au produit et à l'avertissement de la base : PRISM
  fait partie de l'installation de référence, sans être imposé.
- Le socle et PRISM s'installent **sans réseau** ; seuls les trois modules
  facultatifs demandent une connexion, et le profil `full` la supprime.
- Aucune promesse d'interface sans artefact : la disponibilité d'un module est
  une donnée du manifeste, vérifiée par un test.
- PRISM-LDK reste indépendant de PRISM : cocher l'un ne coche pas l'autre.

### Négatives

- **Taille du socle** : la distribution embarque désormais la build PRISM. Le
  poids augmente pour tous les utilisateurs, y compris ceux qui le décochont.
  C'est le prix d'un composant de base ; la taille est mesurée et publiée à
  chaque release, jamais estimée dans la documentation utilisateur.
- **Trois modules à tester** (présent, absent, corrompu, hors ligne) en plus de
  PRISM.
- **PRISM hors du modèle de composant** : pas d'état « PRISM installé » dans
  l'interface du Launcher sans évolution de son côté.

### Risques

- **Build PRISM indisponible au moment de la release** : la distribution de base
  ne peut pas être publiée, puisque PRISM en fait partie. Mitigation : la build
  packagée est une condition de sortie de la phase P2, pas un bonus.
- **Confusion PRISM / PRISM-LDK** : descriptions distinctes, dépendance Unreal
  explicitement écrite dans la fiche du LDK, aucun couplage de sélection.
- **Désactivation massive de PRISM** : l'utilisateur choisit un produit sans
  rendu. Mitigation : avertissement explicite et récapitulatif qui rappelle la
  perte fonctionnelle.

## Alternatives considérées

- **Exclure PRISM du périmètre** (première version de cet ADR) : rejeté ; PRISM
  est le composant de base qui porte le rendu.
- **Traiter PRISM comme un module téléchargé** (deuxième version) : rejeté ; un
  composant de base doit être disponible hors ligne, sans dépendre de GitHub.
- **Rendre PRISM obligatoire** : rejeté ; la base le veut facultatif, avec
  avertissement, et une installation sans rendu doit rester possible.
- **Installer PRISM-LDK directement dans un projet Unreal choisi par
  l'utilisateur** : rejeté ; l'installateur ne connaît pas les projets de
  l'utilisateur et ne doit pas écrire dans un projet qu'il n'a pas créé.
- **Extraire le ZIP du LDK dans `plugins/prism-ldk/`** : rejeté ; le fichier
  installé doit être exactement l'archive publiée, vérifiable par empreinte.
- **Donner un `component.json` à PRISM** : rejeté en V0.2 ; cela exigerait un
  point d'accès de contrôle que PRISM n'expose pas.

## Validation / rejet

- Installation standard avec PRISM coché : `components/prism/` présent, raccourci
  fonctionnel, aucune requête réseau, aucun Unreal Engine requis.
- Installation avec PRISM décoché : message de perte fonctionnelle sur l'écran
  Composants et au récapitulatif ; installation réussie sans `components/prism/`.
- PRISM-LDK coché seul n'installe pas PRISM désactivé ; PRISM coché seul
  n'installe pas le LDK ; l'archive du LDK est identique bit à bit à l'actif.
- SYNE-Mock installé possède son runtime et démarre sans Node système.
- Une release sans actif LDK affiche le module indisponible, sans case, avec la
  raison, et n'empêche pas l'installation.

Réouverture si PRISM devient obligatoire, si le LDK devient un composant
autonome, ou si un module facultatif rejoint la base.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Vérification de la faisabilité des composants facultatifs |
| 21 octobre 2026 | Révision | PRISM est une application packagée, PRISM-LDK un ZIP de plugin, SYNE-Mock et la documentation des archives |
| 21 octobre 2026 | Révision | PRISM est un **composant de base embarqué** ; seuls Launcher, SYNE et ECHOS sont obligatoires ; le LDK, la documentation et SYNE-Mock sont les modules téléchargés |
| 21 octobre 2026 | Révision | PRISM est **coché par défaut** et désactivable ; le décocher affiche l'avertissement §3.3. Écart assumé par rapport à la case « désactivé par défaut » de la base |
