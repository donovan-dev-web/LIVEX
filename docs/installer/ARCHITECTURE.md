# Architecture de l'installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT]
**Version** : 1.0
**Dernière mise à jour** : 1er novembre 2026 (arborescence V1 et ZIP par composant)
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`adr/`](adr/README.md)
**Vérifié contre** : `main`, 1er novembre 2026

---

## 1. Objet

Ce document décrit **comment** l'installateur est construit pour satisfaire la
[spécification](SPECIFICATION.md). Il ne répète pas les exigences : il explique
le découpage, les flux, les formats et les mécanismes. Les décisions sont
justifiées dans les [ADR](adr/README.md).

## 2. Vue d'ensemble

```text
                 ┌─────────────────────────────────────────────┐
                 │              Distribution                   │
                 │  livex-setup  distribution.json  packages/  │
                 │  assets/  LISEZ-MOI  mentions légales       │
                 └───────────────────┬─────────────────────────┘
                                     │ vérifie, planifie, écrit
                                     │
                 ┌───────────────────┴─────────────────────┐
                 │  Actifs de release (modules facultatifs) │
                 │  GitHub Releases : PRISM, LDK, mock      │
                 └───────────────────┬─────────────────────┘
                                     │ HTTPS, empreinte vérifiée
                                     ▼
   ┌──────────────────────────────────────────────────────────────────┐
   │                          livex-setup                             │
   │  ┌────────────────────┐        ┌──────────────────────────────┐  │
   │  │ Setup.App          │        │ Setup.Core                   │  │
   │  │ assistant, i18n,   │───────▶│ préflight, plan, transaction │  │
   │  │ modes CLI          │        │ enregistrement, préférences  │  │
   │  └────────────────────┘        │ désinstallation, journal     │  │
   │                                └──────────────┬───────────────┘  │
   └───────────────────────────────────────────────┼──────────────────┘
                                                   │
             ┌─────────────────────────────────────┼──────────────────────┐
             ▼                                     ▼                      ▼
   ┌──────────────────┐                ┌────────────────────┐   ┌──────────────────┐
   │ Répertoire       │                │ Racine de données  │   │ Intégration OS   │
   │ logiciel        │                │ settings.json      │   │ HKCU / XDG       │
   │ core/launcher   │                │ workspace/         │   │ raccourcis       │
   │ components/     │                │ packages/ archives/│   │ .desktop / .lnk  │
   └──────────────────┘                └────────────────────┘   └──────────────────┘
```

Trois propriétés structurent la conception :

1. **Aucune dépendance au Launcher.** L'installateur applique les contrats
   publiés (schéma de manifeste, conventions de chemins) mais ne référence aucun
   de ses assemblages. Il peut donc réparer un Launcher défaillant.
2. **Le plan précède l'écriture.** Toute opération est calculée, validée et
   journalisée avant d'être exécutée ; l'interface ne contient aucune règle.
3. **Une seule source pour la charge utile.** Le manifeste de distribution
   pilote l'écran Composants, la copie, l'inventaire et la vérification.

## 3. Découpage logiciel

Le code vit dans un nouveau répertoire racine `installer/`, à côté de
`launcher/`, `syne/`, `echos/` et `prism/` :

```text
installer/
├── Setup.sln
├── Directory.Build.props        # net10.0, warnings = erreurs, conventions Launcher
├── Directory.Packages.props     # versions centralisées
├── Setup.Core/                  # bibliothèque sans interface
├── Setup.App/                   # exécutable livex-setup
├── Setup.Tests.Unit/
├── Setup.Tests.Integration/
├── Setup.Tests.EndToEnd/
├── build/                       # production de la distribution
└── dist/                        # sortie, ignorée par git
```

### 3.1 `Setup.Core`

| Espace de noms | Responsabilité | Dépend de |
| :-- | :-- | :-- |
| `Setup.Core.Distribution` | modèle du manifeste, JSON canonique, vérification de signature | aucune UI |
| `Setup.Core.Planning` | calcul du plan (installer, mettre à jour, réparer, désinstaller) | Distribution, Platform |
| `Setup.Core.Preflight` | contrôles préalables et diagnostic `--check` | Distribution, Platform |
| `Setup.Core.Execution` | transaction, copie, sauvegarde, restauration, empreintes | Platform |
| `Setup.Core.Platform` | abstraction des opérations système | — |
| `Setup.Core.Preferences` | `settings.json`, politique de chemins | Platform |
| `Setup.Core.Lifecycle` | détection, enregistrement, inventaire, versions | Platform |
| `Setup.Core.Journal` | journal structuré UTF-8 | — |
| `Setup.Core.Acquisition` | actifs facultatifs : cache, téléchargement reprenable, extraction confinée | Distribution, Platform |
| `Setup.Core.CommandLine` | analyse des arguments, codes de sortie | — |

Interfaces système (implémentées par plateforme) :

| Interface | Windows | Linux |
| :-- | :-- | :-- |
| `IInstallationStore` | registre `HKCU` | `installation.json` XDG |
| `IIntegration` | raccourcis `.lnk`, icône, entrée « Applications et fonctionnalités » | `.desktop`, icône, lien du bureau |
| `IProcessProbe` | énumération des processus et chemins | `/proc/<pid>/exe` |
| `IPathPolicy` | chemins système Windows, volumes | racines système POSIX, XDG |
| `IPrerequisiteProbe` | aucune (tout est embarqué) | `libicu`, `libssl`, affichage |
| `IFileSync` | `FlushFileBuffers` | `fsync` |
| `IHttpDownloader` | `HttpClient` (proxy système, TLS), reprise `Range` | identique |
| `IArchiveExtractor` | `System.IO.Compression` (zip), `tar` natif pour `.tar.gz` | identique |

`Setup.Core` ne référence ni Avalonia, ni `Microsoft.Win32.Registry` en dehors de
l'implémentation Windows, ni aucun assemblage du Launcher.

### 3.2 `Setup.App`

- Avalonia 11.3.22, même version que le Launcher (`launcher/Directory.Packages.props`).
- Une machine à états d'écrans, des ViewModels sans logique métier, des
  ressources `.resx` FR/EN.
- Un mode `--headless` qui n'initialise jamais Avalonia : `--check`,
  `--unattended`, `--uninstall`, `--repair`, `--version`.
- Publication self-contained par RID, un seul binaire `livex-setup[.exe]`.

### 3.3 Ce que l'installateur ne référence pas

- `Launcher.*` (aucun projet, aucun paquet) ;
- `Launcher.Package` (le format `.livexp` est un paquet de campagne, sans rapport
  avec l'installation) ;
- un runtime .NET partagé, Python système ou Node.

`Setup.Core.Acquisition` ne fait aucune requête hors de la résolution d'un actif
déclaré par le manifeste de distribution : pas de découverte de version, pas d'API GitHub,
pas de télémétrie.

## 4. Manifeste de distribution

### 4.1 Modèle

Voir [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md). Champs
principaux :

| Champ | Type | Rôle |
| :-- | :-- | :-- |
| `schema` | entier | version du format du manifeste |
| `product` | chaîne | `"LIVEX"` |
| `version` | SemVer | version du produit installé |
| `channel` | `stable` \| `dev` | canal de release |
| `profile` | `core` \| `full` | modules facultatifs à télécharger ou embarqués |
| `platform` | `win-x64` \| `linux-x64` | plateforme de la charge utile |
| `releasedAt` | ISO 8601 UTC | date de production |
| `components[]` | objet | id, version, obligatoire, textes FR/EN, licence, `requires`, `installRoot`, fichiers éventuels, `acquisition` |
| `acquisition` | objet | `kind` (`bundled-archive` \| `release-asset` \| `unavailable`), `asset`, `url`, `size`, `sha256`, `format`, `stripComponents`, `reason` |
| `files[]` | objet | chemin relatif, taille, SHA-256 (facultatif pour les composants livrés en ZIP) |
| `signature` | objet | algorithme, clé, valeur (hors document signé) |

### 4.2 Règles de cohérence

- Un composant obligatoire ne peut pas dépendre d'un composant facultatif.
- `requires` ne peut référencer qu'un composant présent dans le même manifeste.
- Un `bundled-archive` porte `asset` (chemin `packages/<id>-<version>-<rid>.zip`),
  `size` et `sha256` ; son contenu n'est pas dupliqué dans `files[]`.
- Un `release-asset` porte obligatoirement `size` et `sha256` ; son URL pointe
  vers le tag de la release courante, sur le dépôt du projet uniquement.
- Un composant `unavailable` porte une `reason` et aucun fichier ; il n'est pas
  proposé à la sélection.
- Le profil `full` embarque PRISM dans `packages/` ; le profil `core` le déclare
  en `release-asset` lorsqu'une entrée externe est disponible.
- Le chemin d'un fichier est **relatif à la racine de la charge utile** et
  **identique** à son chemin d'installation ; aucun tableau de correspondance
  n'est autorisé.
- Un chemin vide, absolu, commençant par `/` ou `\`, contenant `..`, un lecteur
  Windows ou un composant nul est refusé à la génération **et** à la lecture.
- Un fichier déclaré pour une autre plateforme est une erreur de build.
- L'inventaire installé est construit à partir des entrées du ZIP de chaque
  composant, pas à partir de `files[]` : le manifeste public reste léger.

### 4.3 Génération

`scripts/release/build_distribution.py` (Python) lit
`installer/build/components.json` et produit la distribution :

0. lit `installer/build/external-inputs.json` (PRISM Windows/Linux, LDK) et
   marque un composant `unavailable` si l'entrée est absente ou non conforme ;
1. normalise le `component.json` de chaque composant depuis
   `installer/build/components.json` (`manifest`, `executables`) ;
2. compresse **un ZIP par composant** dans `packages/`, en excluant les
   mentions légales du Launcher (placées à la racine) ;
3. calcule taille et SHA-256 de chaque ZIP ; le manifeste ne duplique pas la
   liste des fichiers ;
4. écrit `distribution.json` (schéma 1, JSON UTF-8) ;
5. copie `livex-setup[.exe]`, les mentions légales et `LISEZ-MOI.txt` à la
   racine ;
6. produit le ZIP public unique
   `LIVEX-<version>-<rid>-<profile>.zip`.

Le script est appelé par `scripts/release/build-release.ps1`, qui publie
localement les binaires avant l'assemblage. Aucune compilation n'a lieu dans
GitHub Actions.

## 5. Arborescence installée

```text
<répertoire logiciel>/
├── livex-setup[.exe]             # copie de maintenance autonome (single-file)
├── core/
│   ├── launcher/                 # Launcher et ses dépendances
│   │   ├── livex-launcher[.exe]
│   │   └── *.dll, *.json
│   └── installer/                # métadonnées de maintenance
│       └── distribution.json     # distribution d'origine (réparation / ajout)
├── components/
│   ├── syne/
│   │   ├── component.json
│   │   └── …                     # binaire self-contained + fichiers
│   ├── echos/
│   │   ├── component.json
│   │   ├── echos-launcher[.cmd]
│   │   ├── runtime/              # CPython embarqué
│   │   └── site-packages/
│   ├── syne-mock/                # facultatif
│   └── prism/                    # facultatif, application packagée
├── plugins/
│   └── prism-ldk/                # facultatif, archive ZIP intacte
├── contracts/
│   └── component-manifest-v1.schema.json
├── assets/
│   └── livex.ico / livexp.ico / *.svg
├── LICENSE
├── NOTICE
├── THIRD-PARTY-NOTICES.md
├── .livex-install.json           # identité de l'installation
├── .livex-inventory.json         # fichiers installés + empreintes
└── .livex-distribution.json      # copie de compatibilité du manifeste d'origine
```

Points structurants :

- **La racine ne contient que l'exécutable, les mentions légales et les
  métadonnées cachées** ; le Launcher vit dans `core/launcher/` et lit
  `<installation>/components` grâce à `WorkspaceLayout.InstallationRoot()`.
- Les manifestes `component.json` sont copiés depuis le ZIP du composant ;
  l'installateur ne les réécrit jamais.
- **PRISM n'a pas de `component.json`** : c'est une application, pas un service
  piloté par le Launcher. Son répertoire est donc ignoré par `ManifestDetector`.
- **L'archive du LDK n'est jamais extraite** : elle est déposée telle quelle,
  vérifiable par empreinte, et copiée par l'utilisateur dans son projet Unreal.
- Aucun `livex.json`, aucun fichier de configuration global.
- Les icônes communes (`livex.ico`, `livexp.ico`) viennent du composant
  `resources` et servent au raccourci, à l'association `.livexp` et au Launcher.

## 6. Emplacements

| Élément | Windows | Linux | Configurable |
| :-- | :-- | :-- | :-- |
| Racine commune par défaut | `%LOCALAPPDATA%\LIVEX` | `~/.local/share/livex` | oui, à l'installation |
| Répertoire logiciel | `<racine>/software` | `<racine>/software` | oui (dérivé ou personnalisé) |
| Racine de données | `LIVEX_DATA` sinon `<racine>/data` | `LIVEX_DATA` sinon `<racine>/data` | oui (dérivé ou personnalisé) |
| Espace de travail | `<racine>/workspace` | `<racine>/workspace` | oui (dérivé ou personnalisé) |
| Enregistrement | `HKCU\Software\LIVEX\Installer` | `$XDG_DATA_HOME/livex/installer/installation.json` | non |
| Entrée système | `HKCU\…\Uninstall\LIVEX` | `$XDG_DATA_HOME/applications/livex.desktop` | non |
| Icône | `assets/livex.ico` | `$XDG_DATA_HOME/icons/hicolor/scalable/apps/livex.svg` | non |
| Préférences | `<données>/settings.json` | idem | non |
| Journaux | `<données>/logs/` | idem | non |

L'assistant ne demande **qu'une racine** : les trois emplacements en sont
dérivés. Chaque emplacement peut être personnalisé séparément via une case à
cocher, sans imposer la personnalisation des autres.

## 7. Flux d'installation

```text
[Accueil] → [Licence] → [Composants] → [Chemins]
   → [Récapitulatif] → [Installation] → [Résultat]
```

La langue n'est plus un écran : un sélecteur toujours visible dans le panneau
latéral des étapes applique la culture immédiatement. Le bouton « Lancer LIVEX »
de l'écran de résultat remplace l'ancienne case à cocher de fin d'installation.

Avant l'étape Installation, l'assistant exécute le **préflight** :

1. plateforme et architecture supportées ;
2. empreintes des archives `packages/` et du manifeste ;
3. droits d'écriture réels sur le répertoire logiciel et l'espace de travail ;
4. espace disque (taille des archives × 1,5 + marge) ;
5. politique de chemins (section 6) ;
6. absence de processus LIVEX dans le répertoire cible ;
7. prérequis Linux (`libicu`, `libssl`, affichage) ;
8. cohérence du manifeste (section 4.2).

Puis le plan est calculé :

```text
InstallPlan
├── actions[]      : CreateDirectory | WriteFile | ReplaceFile | DeleteFile
│                    | DownloadAsset | ExtractArchive
│                    | CreateShortcut | WriteRegistration | MergeSettings
├── removedComponents[]
├── bytesToWrite
├── bytesToDownload
├── warnings[]
└── requiresConfirmation
```

Chaque action porte : cible absolue, source dans la charge utile ou URL et
empreinte attendues, caractère destructif, étape de progression. Le
récapitulatif affiche le plan résumé ; l'exécution ne découvre rien.

### 7.1 Acquisition des modules facultatifs

```text
modules cochés
   │
   ├── profil `full` ──────────────▶ ZIP dans packages/  ─────┐
   │                                                          │
   └── profil `core` ──▶ cache ? ──oui──▶ vérifier empreinte ──┤
                            │ non                             │
                            ▼                                 │
                    téléchargement HTTPS (Range, reprise)     │
                            │                                 │
                            ▼                                 │
                    taille + SHA-256 ──échec──▶ refus, cache purgé
                            │ ok                              │
                            └──────────────▶ extraction confinée
                                                              │
                                                              ▼
                                              plan d'écriture + inventaire
```

Règles d'implémentation :

- chaque composant embarqué est un ZIP unique dans `packages/` ; l'installateur
  le vérifie par taille et SHA-256 puis l'extrait vers son `installRoot` ;
- le cache vit dans `<racine de données>/cache/downloads/<sha256>` ; un actif
  vérifié n'est jamais retéléchargé, même après une réinstallation ;
- le fichier partiel est `<sha256>.part` ; la reprise utilise `Range` quand le
  serveur l'accepte, sinon repart de zéro ;
- l'extraction se fait vers un répertoire temporaire **dans le répertoire cible**
  puis renommage, ce qui conserve l'atomicité par fichier de la transaction ;
- les entrées `..`, absolues, liens symboliques et périphériques sont refusées
  avant écriture ; le nombre d'entrées et la taille décompressée sont bornés par
  le manifeste ;
- l'annulation laisse le fichier `.part` en cache et ne modifie pas
  l'installation ;
- l'exécution tourne hors du thread d'interface (`Task.Run`) et la progression
  est limitée à environ 20 mises à jour par seconde (`50 ms`) pour ne pas figer
  l'assistant.

### 7.2 Échecs d'acquisition

| Situation | Effet |
| :-- | :-- |
| pas de réseau | message, choix : archive locale, continuer sans le module, annuler |
| `404` ou actif absent | module indisponible, aucun échec du socle |
| taille ou empreinte incorrecte | actif supprimé du cache, refus, journal `SECURITY` |
| extraction impossible (disque plein, entrée refusée) | module en échec, socle intact, résidus listés |
| module facultatif en échec | poursuite possible sans lui, sur choix explicite |
| annulation utilisateur | aucun fichier installé, cache reprenable |

Après l'écriture : vérification des fichiers installés, écriture de
`.livex-inventory.json`, copie de `.livex-distribution.json`, puis
`.livex-install.json` (point de commit), intégration système, puis exécution
informative de `livex-launcher --check` (sortie journalisée ; un échec est un
avertissement, pas un rollback, car il peut venir d'un conflit de port).

## 8. Transactions et retour arrière

L'installateur n'utilise pas de copie complète en staging (coût disque égal à la
charge utile). Il applique une transaction **fichier par fichier**, journalisée :

1. écrire le nouveau contenu dans un fichier temporaire **à côté de la cible**
   (même volume) : `.<nom>.livex-tmp-<id>` ;
2. si la cible existe, la déplacer vers `.livex-backup-<id>/<chemin>` ;
3. renommer le temporaire vers la cible (opération atomique sur le volume) ;
4. consigner l'action dans le journal de transaction (append, fsync).

En cas d'échec, les actions sont annulées dans l'ordre inverse : supprimer les
fichiers créés, restaurer les sauvegardes, supprimer les répertoires créés et
vides. Une annulation incomplète est signalée avec la liste des résidus ; elle
n'est jamais présentée comme un succès.

Limites assumées : pas de garantie de durabilité après coupure de courant (les
`fsync` sont faits au mieux), pas de retour arrière après redémarrage, pas de
snapshot du système de fichiers.

## 9. Détection et enregistrement

```text
                 ┌──────────────────────────┐
                 │ IInstallationStore.Read  │
                 └────────────┬─────────────┘
                              ▼
              ┌───────────────────────────────┐
              │ enregistrement présent ?      │
              └───────┬───────────────┬───────┘
                 non  │               │ oui
                      ▼               ▼
        ┌─────────────────────┐  ┌──────────────────────────┐
        │ chercher            │  │ valider : répertoire,    │
        │ <défaut>/.livex-    │  │ .livex-install.json,     │
        │ install.json        │  │ core/launcher            │
        └─────────┬───────────┘  └───────┬──────────┬───────┘
                  │                  valide│          │invalide
             non  │                       ▼          ▼
                  ▼                 [maintenance]  [localiser /
             [installation]                        réparer / purger]
```

Un enregistrement **orphelin** (répertoire supprimé, Launcher absent) n'est pas
traité comme une installation valide : une nouvelle installation peut
l'écraser. Le mode `--unattended` ne renvoie donc le code `5` que si
`DetectionResult.IsValid` est vrai.

L'algorithme complet et les clés sont dans
[ADR-005](adr/ADR-005-enregistrement-et-cycle-de-vie.md). Deux invariants :

- **Aucune conclusion « pas installé » sur la seule absence d'enregistrement.**
- **Aucune écriture dans un répertoire contenant une installation valide sans
  passer par le parcours de maintenance.**

## 10. Mise à jour, réparation, maintenance

| Opération | Plan | Particularités |
| :-- | :-- | :-- |
| **Mise à jour** | remplacer les fichiers du produit, conserver données et préférences | refus si version inférieure ; processus arrêtés ; composants facultatifs conservés s'ils existent encore |
| **Réparation** | comparer l'inventaire au disque, restaurer depuis la distribution | échoue proprement si la distribution d'origine est absente et qu'un fichier corrompu doit être restauré |
| **Composants** | ajouter/retirer un facultatif | jamais les obligatoires ; retrait = suppression des fichiers du composant uniquement |
| **Espace de travail** | modifier le chemin mémorisé | validation, écriture de `settings.json` ; l'ancien répertoire n'est ni déplacé ni supprimé |

La comparaison de versions suit SemVer 2.0.0 : une version `pre` est inférieure à
la version finale correspondante, et un changement de `MAJOR` n'autorise pas un
downgrade implicite.

## 11. Désinstallation

Le planificateur de désinstallation produit un plan distinct, à partir de
l'inventaire et de la portée choisie :

| Portée | Actions |
| :-- | :-- |
| **A** | supprimer fichiers inventoriés (y compris `components/prism/`, `components/syne-mock/`, `plugins/`, `.livex-distribution.json`), raccourcis, `.desktop`, enregistrement, entrée système |
| **B** | A + supprimer `settings.json` et `cache/` (dont `cache/downloads/`) |
| **C** | B + supprimer l'espace de travail confirmé |
| **C + extension** | C + supprimer `packages/`, `archives/`, `sessions/`, `echos/` après seconde confirmation |

Ordre d'exécution : processus → intégrations → fichiers → préférences →
espace de travail → répertoires vides → enregistrement. Les journaux de
l'installateur (`<données>/logs/installer-*.log`) ne sont jamais supprimés par
une portée, y compris l'extension. Le cache de téléchargement est supprimé par
la portée B ; l'option A le conserve, pour permettre une réinstallation sans
retélécharger les modules facultatifs.

Cas de l'inventaire absent : l'installateur supprime les répertoires dont il
connaît la propriété (`components/`, `contracts/`, `docs/`, `assets/`), les
fichiers d'identité, et signale que des fichiers du Launcher peuvent subsister.
Il ne supprime le répertoire entier que si l'utilisateur le confirme
explicitement, après affichage de son contenu.

## 12. Préférences et espace de travail

`Setup.Core.Preferences.WorkspaceSettings` :

```text
Read(path)      → objet JSON + état (absent | valide | invalide)
Merge(current, changes) → préserve les clés inconnues
Write(path)     → fichier temporaire + fsync + rename atomique
```

Un fichier invalide est déplacé vers `settings.json.corrupt-<horodatage>` avant
toute écriture, et l'événement est journalisé. Le Launcher doit lire la même
précédence (`--workspace` > `settings.json.workspace` > `<données>/workspace`) ;
cette évolution est portée par une issue distincte côté Launcher.

## 13. Runtimes embarqués

| Élément | Production | Contenu installé |
| :-- | :-- | :-- |
| `livex-setup` | `dotnet publish -r <rid> --self-contained true -p:PublishSingleFile=true` | binaire unique autonome à la racine |
| `livex-launcher` | idem (`PublishSingleFile`) | `core/launcher/` |
| `syne` | idem | `components/syne/` + `component.json` |
| `echos` | CPython 3.12 autonome + `site-packages` figés | `components/echos/runtime/` et `site-packages/` |
| `prism` (**base, coché par défaut**) | build packagée du projet PRISM, par RID | `components/prism/` |
| `prism-ldk` (facultatif) | archive ZIP du plugin `PrismLdk` | `plugins/prism-ldk/PrismLdk-<version>.zip` |
| `syne-mock` (facultatif) | archive ZIP + runtime Node portable | `components/syne-mock/` |
| `resources` | icônes et ressources partagées | `assets/` |

Le build vérifie pour chaque RID que : aucun fichier ne manque, aucun binaire
n'est framework-dependent, et `livex-launcher --check` s'exécute sur un
répertoire de test. Les prérequis Linux sont détectés, pas installés.

## 14. Intégrité et signature

Ordre strict (voir [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md)) :

1. lire `distribution.json` et valider sa forme (`ManifestValidator`) ;
2. vérifier taille et SHA-256 de chaque fichier de la charge utile ;
3. vérifier que chaque actif distant déclare URL, taille et empreinte ;
4. seulement ensuite, construire le plan.

Aucune vérification cryptographique du manifeste n'a lieu en V1 (ADR-007
§3 bis) : la confiance vient des empreintes, du `SHA256SUMS` publié et des
attestations GitHub, vérifiés par l'utilisateur ; Authenticode s'y ajoute sur
Windows quand un certificat est disponible.

Le JSON canonique est produit par un sérialiseur dédié (clés triées, UTF-8 sans
BOM, aucun espace, nombres entiers sans exposant, horodatages UTC). Un test
compare la forme générée à une forme de référence figée.

## 15. Journalisation

Format texte UTF-8, une ligne par événement :

```text
2026-10-21T09:12:04.113Z INFO  step=install op=write result=ok path=components/syne/...
2026-10-21T09:12:05.870Z ERROR step=install op=write result=failed code=IO_DENIED path=...
```

- Niveaux : `DEBUG`, `INFO`, `WARN`, `ERROR`.
- Emplacement : `<racine de données>/logs/installer-AAAAMMJJ-HHMMSS.log`.
- Rotation : 10 fichiers, 5 Mio maximum par fichier.
- Contenu interdit : secrets, jetons, contenu d'expérience, variables
  d'environnement complètes.
- Le chemin du journal est affiché à l'écran de résultat et à l'écran d'échec.

## 16. Interface et i18n

- Machine à états : `Welcome → License → Components → Paths → Summary →
  Progress → Result`, avec branches `Maintenance` et `Uninstall`.
- Chaque écran valide ses entrées avant d'autoriser « Suivant » ; le retour
  conserve l'état.
- Ressources `.resx` (`Strings.fr.resx`, `Strings.en.resx`), résolution par
  `CultureInfo`, repli anglais, clé manquante journalisée.
- Accessibilité : `AutomationProperties.Name` sur les contrôles, ordre de
  tabulation explicite, focus initial par écran, messages d'erreur associés au
  champ fautif, aucune information portée par la couleur seule.

## 17. Sécurité

| Menace | Mesure |
| :-- | :-- |
| Distribution altérée | empreintes SHA-256 par fichier, `SHA256SUMS`, attestations GitHub, Authenticode conditionnel |
| Chemin piégé dans le manifeste | validation stricte, chemin résolu confiné au répertoire cible |
| Lien symbolique dans la cible | refus de suivre un lien lors de l'écriture, vérification des points de reparse Windows |
| Écrasement d'une installation valide | détection obligatoire, parcours de maintenance |
| Suppression de données utilisateur | portées explicites, chemins affichés, confirmations |
| Course entre vérification et écriture | verrou d'installation, revalidation avant remplacement |
| Élévation | aucune, aucune écriture système |
| Fuite d'information dans le journal | liste noire de champs, pas de dump d'environnement |
| Binaire non signé | avertissement explicite en canal `dev` |
| Actif distant remplacé ou servi par un autre hôte | URL déclarée par le manifeste, empreinte SHA-256, redirections limitées à GitHub |
| Empoisonnement du cache | cache adressé par empreinte, revérification à chaque usage, actif non conforme supprimé |
| Archive piégée (zip-slip, lien, bombe) | extraction confinée, refus des entrées absolues/`..`/liens, bornes de taille et d'entrée |
| Fuite d'identité vers GitHub | aucune authentification, aucun identifiant d'installation, aucune télémétrie |

L'installateur n'exécute aucun script contenu dans la charge utile. Il ne lance
le Launcher qu'à la demande explicite de l'utilisateur, après installation.

## 18. Modes non interactifs

Référence complète, avec exemples : [`COMMANDES.md`](COMMANDES.md).

| Commande | Effet | Sortie |
| :-- | :-- | :-- |
| `--check` | préflight seul, aucune écriture | texte stable, `0`/`2` |
| `--unattended --config <f>` | installation avec réponses | `0`/`2`/`3`/`5`/`6` |
| `--uninstall [--scope a\|b\|c] [--purge-research] [--confirm-workspace]` | désinstallation | `0`/`2`/`3`/`4` |
| `--offline-assets <dir>` | actifs facultatifs lus depuis un répertoire local | `0`/`3`/`6` |
| `--no-network` | aucune requête réseau autorisée | `0`/`6` |
| `--repair` | vérification et restauration | `0`/`2`/`3`/`6` |
| `--maintenance` | parcours de maintenance | selon l'action |
| `--version` | versions installateur et distribution | `0` |
| `--lang fr\|en` | langue de l'assistant | — |
| `--verbose` | trace de diagnostic sur la sortie d'erreur | — |

Toute option inconnue, comme tout nom de scénario inconnu, est une erreur
d'usage (`1`) et n'ouvre pas l'assistant : une faute de frappe ne doit jamais
ressembler à un lancement normal. Il en va de même d'un nom de scénario ou d'une
portée de désinstallation invalides.

Le drapeau `--log` figure dans la spécification d'origine mais n'a jamais été
appliqué : il a été retiré de l'aide **et du parseur**, donc refusé comme option
inconnue (`1`). Le journal reste déterminé par la racine de données
(`<racine de données>/logs`).

Le fichier de réponses de `--unattended` reprend les champs de l'assistant :
`installDir`, `workspaceDir`, `components`, `acceptLicense`, `desktopShortcut`,
`offlineAssetsDir`, et facultativement `dataRoot` (racine de données ; sans elle,
`<racine commune>/data` ou `LIVEX_DATA`). `installDir` et `workspaceDir` doivent
être des chemins absolus, `acceptLicense` vaut `true` (personne ne peut accepter
la licence à la place de l'utilisateur) et un identifiant de composant absent du
manifeste est refusé. Toute valeur manquante ou invalide fait échouer la commande
avec `2`, sans valeur par défaut implicite pour les chemins.

Ces modes sont implémentés dans `Setup.Core/Execution/HeadlessRunner.cs`, qui
applique seul la table des codes de sortie et n'attend aucune saisie :
`InstallAsync`, `InstallFromResponsesAsync` (`--unattended --config`),
`UninstallAsync` (portée C refusée sans confirmation explicite de la suppression
de l'espace de travail) et `RepairAsync`.

Une installation réelle dont les archives `packages/` manquent est refusée dès
le préflight (`payload`, code `2`) : le manifeste déclare un `bundled-archive`
introuvable et rien ne peut être extrait. Le refus est explicite plutôt qu'un
échec à la première écriture, qui laisserait des résidus à compenser.

Le même contrôle vaut pour `--check` sur une distribution réelle : `DryRun`
suspend les écritures, il ne rend pas le monde fictif. Le champ `Simulated` de
`InstallRequest` porte cette distinction, et seul le mode démonstration le met
à vrai ; c'est ce qui permet à `--check` de répondre `2` sur une distribution
dégradée (INS-A-37) tout en laissant `--check --demo` à `0`. Le fichier de réponses est lu et validé
par `Setup.Core/CommandLine/UnattendedResponses.cs` ; `Setup.App` ne fait
qu'appeler le lanceur et propager son code de sortie.
Un module facultatif demandé sans réseau ni actif local fait échouer la commande
avec `6`, après écriture d'un journal explicite ; le socle n'est pas installé
partiellement pour autant.

## 19. Build, CI et tests

- **Aucune compilation dans GitHub Actions.** La production est locale :
  `scripts/release/build-release.ps1` publie le Launcher, l'installateur et SYNE,
  assemble ECHOS et SYNE-Mock, vérifie les entrées externes puis appelle
  `scripts/release/build_distribution.py`.
- `release.yml` est un workflow `workflow_dispatch` de **publication seule** : il
  crée la release GitHub sans artefact. L'upload du ZIP public se fait ensuite
  localement (`-Upload` ou `gh release upload`).
- Les scripts produisent un ZIP public unique contenant `livex-setup`, un ZIP
  par composant dans `packages/`, `assets/` et les mentions légales.
- Tests de bout en bout : installation silencieuse dans un répertoire
  temporaire, `--check`, désinstallation, vérification des résidus ; nettoyage
  des clés `HKCU` et fichiers XDG sur le runner.
- Les tests n'exigent jamais d'élévation et ne touchent pas au répertoire
  personnel réel de l'agent.

Détail des scénarios : [`TESTING.md`](TESTING.md).

## 20. Limites et dettes connues

| Réf. | Limite | Traitement |
| :-- | :-- | :-- |
| **INS-L-01** | La distribution de base exige une build PRISM packagée : son absence bloque la release | [ADR-006](adr/ADR-006-perimetre-composants-v02.md), roadmap P2 |
| **INS-L-09** | PRISM n'est pas détecté par le Launcher (pas de contrat de service) | [ADR-006](adr/ADR-006-perimetre-composants-v02.md), point ouvert INS-O-10 |
| **INS-L-10** | Un module facultatif en profil `core` dépend de la disponibilité de GitHub | profil `full`, archive locale, cache ([ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md)) |
| **INS-L-11** | **Aucune signature cryptographique du manifeste en V1** (décision D3) : la confiance repose sur les empreintes SHA-256 des archives `packages/` et sur le SHA-256 du ZIP publié par GitHub. `ManifestValidator` ne contrôle que la forme des champs de signature ; le champ `signature` reste optionnel et vide. Un `distribution.json` forgé, accompagné de fichiers cohérents avec ses empreintes, ne serait donc pas distingué d'un manifeste légitime par l'installateur seul | vérification utilisateur du SHA-256 du ZIP et Authenticode quand un certificat est disponible ; réouverture si une signature centralisée (Sigstore, Azure Trusted Signing) est adoptée |
| **INS-L-12** | **Acquisition non câblée** : `AcquisitionService` (cache `cache/downloads`, actifs locaux, `--no-network`) n'est appelé nulle part, `InstallerEngine` télécharge directement vers la destination finale, et `WindowsHttpDownloader` ne fait pas de reprise `Range` | tâche T5 du plan d'alignement : brancher l'acquisition, ajouter la reprise et les tests serveur local |
| **INS-L-02** | Le Launcher ne lit pas encore `settings.json.workspace` | issue Launcher dédiée, prérequis de recette |
| **INS-L-03** | Réparation et ajout de composant exigent la distribution d'origine | documenté dans le guide |
| **INS-L-04** | Pas de mise à jour différentielle : chaque mise à jour retélécharge la distribution | acceptable tant que le paquet reste sous le seuil produit ; les actifs facultatifs déjà en cache ne sont pas retéléchargés |
| **INS-L-05** | Pas de signature Windows tant que le certificat n'est pas obtenu | release non bloquée : publication non signée avec avertissement SmartScreen documenté |
| **INS-L-06** | Copie de maintenance dans le répertoire logiciel | coût de taille mesuré à chaque release |
| **INS-L-07** | Pas de rollback après redémarrage | hors périmètre V0.2 |
| **INS-L-08** | Pas de mode machine | hors périmètre V0.2 |

## 21. Références

- [`SPECIFICATION.md`](SPECIFICATION.md) — exigences normatives
- [`COMPONENTS.md`](COMPONENTS.md) — rôle et installabilité des composants
- [`adr/README.md`](adr/README.md) — décisions
- [`ROADMAP.md`](ROADMAP.md) — phases et jalons
- [`TESTING.md`](TESTING.md) — stratégie de test
- [`RELEASE_AND_UPDATE.md`](RELEASE_AND_UPDATE.md) — production et publication
- `launcher/contracts/component-manifest-v1.schema.json` — contrat de composant
- `launcher/Launcher.Infrastructure/ManifestDetector.cs` — détection côté Launcher
- `launcher/Launcher.Infrastructure/WorkspaceLayout.cs` — racine de données
- `docs/launcher/PACKAGING.md` — packaging historique du Launcher

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle architecture, vérifiée contre le dépôt |
| 29 octobre 2026 | §18 : fichier de réponses détaillé, implémentation des modes non interactifs, renvoi vers `COMMANDES.md`, `--log` retiré, CLI strict | `HeadlessRunner` et `UnattendedResponses` existent ; le drapeau `--log` n'était pas appliqué |
| 29 octobre 2026 | §4.3 (entrées épinglées, Authenticode avant hachage, archives), §19 (état réel de la CI), §20 (INS-L-11 révisé, INS-L-12, INS-L-05) | Plan d'alignement V1 (D3, ADR-010) et check-up documentaire du 29 octobre 2026 |
| 1er novembre 2026 | §2/§4 manifeste et `packages/`, §5 `core/launcher` et `core/installer`, §6 racine unique, §7 flux sans étape langue, §9 enregistrement orphelin, §13 publication single-file, §19 chaîne locale sans build CI | Corrections V1 de l'installateur et du workflow de release |
