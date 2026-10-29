# ADR-008 : Acquisition des modules facultatifs depuis les releases GitHub

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 29 octobre 2026
**Dépend de** : ADR-003, ADR-006, ADR-007, ADR-010
**Source Monographie** : —
**Remplace** : la version du 21 octobre 2026 qui incluait PRISM parmi les actifs téléchargés

---

## Contexte

La spécification de base exige une installation hors ligne et classe
« téléchargement automatique de composants ou dépendances pendant
l'installation » hors périmètre (§1.3, §2.3). Le mainteneur a précisé le
périmètre réel :

- le **socle de base** — Launcher, SYNE, ECHOS, **PRISM** et les éléments de
  support — est embarqué et s'installe **sans réseau** ;
- les **modules facultatifs** — PRISM-LDK, documentation hors ligne,
  SYNE-Mock — sont des archives **téléchargées depuis les releases GitHub**
  par l'installateur, lorsque l'utilisateur les sélectionne.

PRISM est un composant de base : il est donc dans la distribution, coché par
défaut et désactivable (ADR-006). Trois raisons justifient que les modules, eux,
restent séparés :

1. ils ont leurs propres cycles de publication (plugin Unreal, documentation,
   mock) et n'ont pas à bloquer la sortie du socle ;
2. ils ne servent pas à tous les utilisateurs : le LDK exige un projet Unreal,
   le mock est un outil de développement ;
3. leur poids et leurs mises à jour ne doivent pas alourdir chaque distribution
   du socle.

La base admet explicitement l'installation hors ligne « lorsque les fichiers
nécessaires sont intégrés au paquet » (§1.1) : une variante complète reste donc
possible et doit rester supportée.

Il faut une règle qui ne confonde pas **téléchargement automatique** (exclu) et
**acquisition explicite d'un module choisi par l'utilisateur** (autorisée), et
qui conserve les garanties d'intégrité de l'ADR-007.

## Décision

### 1. Deux profils de distribution

| Profil | Contenu | Réseau | Usage |
| :-- | :-- | :-- | :-- |
| **`core`** (défaut) | socle (Launcher, SYNE, ECHOS, PRISM, support) ; les trois modules facultatifs sont décrits avec leur URL de release | requis pour un module facultatif | installation courante |
| **`full`** | socle + les trois archives facultatives de la plateforme | **aucun** | poste isolé, installation hors ligne complète |

Le profil est porté par `distribution.json.profile`. PRISM est présent dans les
deux profils : c'est un composant de base. Un profil `full` n'est publié que si
les trois archives existent pour la plateforme ; une archive manquante fait
échouer le build.

### 2. Le manifeste décrit les actifs, l'installateur ne devine rien

Chaque module facultatif porte sa provenance dans le manifeste signé :

```json
{
  "id": "prism-ldk",
  "required": false,
  "optional": true,
  "version": "0.2.0",
  "acquisition": {
    "kind": "release-asset",
    "asset": "prism-ldk-0.2.0.zip",
    "url": "https://github.com/<owner>/<repo>/releases/download/livex-v0.2.0/prism-ldk-0.2.0.zip",
    "size": 0,
    "sha256": "…",
    "format": "zip",
    "stripComponents": 1
  }
}
```

Pour le profil `full`, `acquisition.kind` vaut `payload` et l'archive est dans
`payload/`. Les composants du socle portent aussi `kind: "payload"` :
`required: true` pour Launcher, SYNE et ECHOS, `required: false` pour **PRISM**,
composant de base coché par défaut mais désactivable.

Aucune autre source n'est acceptée : pas d'API GitHub, pas de résolution de
dernière version, pas de miroir tiers. L'URL est celle du tag exact de la
release ; elle est stable et ne dépend d'aucun quota d'API.

Les actifs recopiés depuis la release d'entrées `prism-v1.0.0` (LDK) pointent
malgré tout vers le tag **`livex-v1.0.0`** courant : une installation ne consulte
qu'une seule release, celle de la distribution qu'elle installe (ADR-010 §3).

Règles :

- un module dont l'actif n'existe pas pour la plateforme est déclaré
  `unavailable` avec une raison, et n'est pas proposé ;
- `size` et `sha256` sont obligatoires pour tout actif distant ;
- l'URL doit pointer vers le dépôt du projet et vers le tag de la release
  courante ; une URL d'un autre hôte est refusée au build ;
- la signature du manifeste (ADR-007) couvre les empreintes des actifs
  distants : un manifeste valide engage donc aussi les archives à télécharger.

### 3. Acquisition déclenchée par l'utilisateur, jamais en arrière-plan

- Aucun téléchargement n'a lieu tant que l'utilisateur n'a pas coché un module et
  validé le récapitulatif.
- Aucune vérification de version, aucune recherche de mise à jour, aucune
  requête réseau au démarrage de l'installateur — y compris pour PRISM, qui est
  embarqué.
- Le récapitulatif affiche, pour chaque module coché : taille à télécharger,
  espace disque requis et estimation de durée **seulement si elle a été mesurée**
  pour cette version ; sinon « durée inconnue ».
- L'utilisateur peut annuler le téléchargement ; les octets déjà reçus restent
  dans le cache reprenable.

### 4. Téléchargement vérifié, reprenable et borné

| Règle | Détail |
| :-- | :-- |
| **Transport** | HTTPS uniquement, certificat validé, redirections limitées à `github.com` et `objects.githubusercontent.com`, proxy système respecté |
| **Reprise** | fichier partiel `<sha256>.part`, reprise par `Range` si le serveur l'accepte, sinon reprise à zéro |
| **Cache** | `<racine de données>/cache/downloads/<sha256>` ; un actif déjà présent et vérifié n'est pas retéléchargé |
| **Vérification** | taille puis SHA-256 après réception complète, **avant** toute extraction ou copie ; signature du manifeste vérifiée avant toute requête |
| **Extraction** | jamais dans un répertoire temporaire non maîtrisé ; extraction en flux vers une destination temporaire du répertoire cible, refus des entrées `..`, absolues, liens et périphériques |
| **Bornes** | délai de connexion 30 s, délai d'inactivité 60 s, 3 tentatives avec temporisation croissante, taille maximale déclarée par le manifeste |
| **Progression** | octets reçus, débit, module concerné, annulation possible ; aucune étape silencieuse de plus de 2 s |
| **Journal** | URL, actif, taille, empreinte attendue, résultat ; jamais de jeton ni d'en-tête d'authentification |

Aucune authentification GitHub n'est utilisée : les releases publiques sont
accessibles anonymement. Si un actif exige une authentification, il est considéré
comme indisponible.

### 5. Échec, absence de réseau et choix de l'utilisateur

| Situation | Comportement |
| :-- | :-- |
| Réseau absent ou coupé pendant le téléchargement | message explicite ; le socle déjà installé reste intact ; reprise possible |
| Actif introuvable (404) ou taille incohérente | module marqué indisponible, cause affichée, aucune installation partielle |
| Empreinte différente | actif supprimé du cache, erreur bloquante pour ce module, alerte de sécurité dans le journal |
| Module facultatif en échec pendant l'installation | choix explicite : continuer sans le module, réessayer, ou annuler l'installation ; le socle n'est pas compromis |
| Installation sans réseau d'un module coché | proposer **« choisir une archive locale »** (vérifiée par la même empreinte) ou continuer sans le module |

La règle de la spécification de base §7.3 est appliquée : une dépendance
facultative ne fait pas échouer les fonctionnalités indépendantes.

### 6. Ajout ultérieur d'un module

Après installation, l'ajout d'un module facultatif utilise :

1. le cache local s'il contient l'actif vérifié ;
2. sinon l'URL du manifeste **conservé dans l'installation**
   (`<install>/.livex-distribution.json`) ;
3. sinon une archive locale choisie par l'utilisateur.

L'installateur n'invente jamais une URL « dernière version ». Une nouvelle
version du module s'obtient par une nouvelle distribution, pas par une mise à
jour silencieuse.

### 7. Rétention des actifs de release

- Les actifs référencés par un manifeste publié ne sont **jamais** supprimés de
  la release, même après une nouvelle version : une installation ancienne doit
  pouvoir ajouter un module ou réparer un composant facultatif.
- Le retrait d'une version (RELEASE_AND_UPDATE §11) supprime l'installateur et
  les distributions, et signale les actifs devenus inutiles ; il ne casse pas
  une installation existante.
- Une release dont un actif manque est refusée par la CI avant publication.

## Conséquences

### Positives

- Le socle, **PRISM compris**, reste strictement hors ligne et testable sans
  réseau.
- L'utilisateur ne paie le poids du LDK, de la documentation ou du mock que s'il
  les choisit.
- Une variante `full` couvre les postes isolés, sans changer le code.
- Les empreintes des actifs distants sont couvertes par la signature du
  manifeste : la chaîne de confiance ne se dégrade pas.

### Négatives

- Un module facultatif dépend de la disponibilité de GitHub au moment de
  l'installation. C'est une dépendance externe nouvelle, explicite.
- Le cache de téléchargement ajoute un espace disque non négligeable ; il doit
  être purgé lorsqu'il dépasse un seuil, sans supprimer un actif référencé par
  l'installation.
- Deux profils à produire et à tester par release.
- Le manifeste doit être conservé dans l'installation, donc un fichier de plus à
  protéger et à nettoyer à la désinstallation.

### Risques

- **GitHub indisponible ou restreint** (réseau d'entreprise) : mitigation par
  l'archive locale, le profil `full` et un message qui explique la cause.
- **Actif remplacé sur la release** : la vérification d'empreinte échoue et le
  build est fautif ; mitigation par la règle de rétention et par un test de
  publication qui rejoue les URL.
- **Croissance du cache** : mitigation par un plafond et une purge des actifs
  non référencés, avec affichage dans la maintenance.
- **Confiance dans l'hôte** : une URL détournée pourrait servir une archive
  malveillante, mais l'empreinte signée la refuse. Une compromission de la clé
  de signature reste le risque résiduel de l'ADR-007.

## Alternatives considérées

- **Tout embarquer dans une distribution unique** : simple, hors ligne par
  construction ; conservé comme profil `full` uniquement, pour ne pas imposer le
  LDK, la documentation et le mock à tous.
- **Télécharger PRISM aussi** : rejeté ; PRISM est un composant de base, il doit
  être disponible sans réseau (ADR-006).
- **Télécharger via l'API GitHub** : permet de résoudre « la dernière version » ;
  rejeté : quota anonyme, dépendance à un format d'API, et surtout risque
  d'installer un actif non décrit par le manifeste signé.
- **Téléchargement automatique en arrière-plan après installation** : rejeté ;
  contredit la spécification et le contrôle utilisateur.
- **Lien vers le site du projet plutôt que vers la release** : rejeté ; l'URL
  doit désigner un actif immuable, vérifiable et versionné.
- **Déduire la disponibilité d'un module par une requête HEAD** : rejeté ; la
  disponibilité est une donnée du manifeste, pas une observation réseau.

## Validation / rejet

- Profil `core` : installation du socle **avec PRISM** sur une machine sans
  réseau ; `--check` retourne `0` ; aucun module facultatif n'est installé.
- Profil `core` avec PRISM-LDK coché et réseau disponible : téléchargement,
  vérification, dépôt de l'archive dans `plugins/prism-ldk/`.
- Réseau coupé pendant le téléchargement : reprise après rétablissement, aucune
  installation partielle, socle intact.
- Empreinte modifiée : refus, message de sécurité, aucune écriture.
- Archive locale fournie pour un module coché : acceptée si l'empreinte
  correspond, refusée sinon.
- Profil `full` : installation complète sans réseau, PRISM et les trois modules.
- Ajout ultérieur d'un module : résolu par le cache ou par le manifeste conservé,
  sans réseau si l'actif est en cache.

Réouverture si GitHub cesse d'héberger les releases du projet, si un miroir
devient nécessaire, ou si un module facultatif rejoint la base.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Arbitrage : socle hors ligne, modules facultatifs acquis depuis les releases GitHub |
| 21 octobre 2026 | Révision | PRISM est un composant de base embarqué ; l'acquisition ne concerne que PRISM-LDK, la documentation et SYNE-Mock |
| 29 octobre 2026 | §2 : URL sur le tag `livex-v` courant, y compris pour les actifs recopiés d'une release d'entrées | Plan V1 (ADR-010) : un installateur ne consulte qu'une seule release, celle de sa distribution |
