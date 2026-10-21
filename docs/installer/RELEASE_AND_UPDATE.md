# Production, publication et mise à jour de l'installateur

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`TESTING.md`](TESTING.md), `../../VERSIONING.md`

---

## 1. Objet

Ce document décrit comment une version de l'installateur est construite,
vérifiée, signée, publiée et mise à jour. Il ne fixe pas de date : il fixe des
portes et des artefacts.

## 2. Versionnement

| Élément | Règle |
| :-- | :-- |
| **Version du produit** | SemVer, portée par `distribution.json.version` ; c'est la version annoncée à l'utilisateur |
| **Version de l'installateur** | SemVer propre au binaire `livex-setup`, affichée par `--version` |
| **Tag** | `livex-vX.Y.Z` sur la racine du dépôt, conformément à `VERSIONING.md` |
| **Cohérence** | une release ne publie que des composants dont les versions sont figées dans le manifeste |

Une release de l'installateur est une **release assemblée** : elle embarque des
versions précises de Launcher, SYNE et ECHOS. Elle ne suit pas automatiquement
les releases de composants ; une version de composant n'entre dans une
distribution qu'après validation de la pile.

## 3. Artefacts produits

Pour chaque plateforme (`win-x64`, `linux-x64`) et chaque profil (`core`,
`full`) :

```text
livex-<version>-<rid>-<profil>/
├── livex-setup[.exe]              # binaire d'installation et de maintenance
├── payload/                       # charge utile ; en profil full, les actifs aussi
├── distribution.json              # manifeste canonique
├── distribution.json.sig          # signature Ed25519
├── SHA256SUMS                     # sommes de tous les fichiers
├── SHA256SUMS.asc                 # signature détachée GPG
└── LISEZ-MOI.txt                  # vérification, installation, désinstallation
```

Actifs facultatifs attachés à la même release GitHub, séparément des archives
d'installation :

```text
prism-ldk-<version>.zip
livex-docs-<version>.zip
syne-mock-<version>.zip
```

La build **PRISM** n'est pas un actif séparé : c'est un composant de base, déjà
présent dans `payload/` de chaque archive d'installation. Un actif PRISM publié à
part ne serait utilisé par aucune version de l'installateur.

Règles :

- le nom et l'URL de chaque actif sont ceux déclarés dans `distribution.json` ;
  aucun renommage après publication ;
- un actif `prism-ldk-<version>.zip` est l'archive exacte du plugin, sans
  ré-archivage ;
- un actif dont la taille ou l'empreinte change impose une nouvelle version,
  jamais un remplacement sur place (ADR-008 §7) ;
- l'archive d'installation est un `.zip` (Windows) et un `.tar.gz` (Linux). Le
  contenu décompressé est identique ; aucune installation ne se fait depuis
  l'archive sans l'avoir décompressée, afin que la vérification des chemins
  reste simple.

Aucun autre artefact n'est publié : pas d'installateur « web », pas de paquet
auto-extractible non vérifiable.

## 4. Chaîne de production

```text
1. geler les versions des composants (manifeste de build)
2. publier self-contained : livex-launcher, syne
3. assembler ECHOS : CPython 3.12 + site-packages figés
4. produire les actifs facultatifs :
   4a. archive du plugin PrismLdk
   4b. archive de documentation
   4c. archive SYNE-Mock + runtime Node
5. assembler la charge utile `core` et `full` — build **PRISM packagée** par RID
   dans `payload/` — puis calculer tailles et empreintes
6. écrire distribution.json canonique (profils, acquisition, actifs)
7. signer (Ed25519)
8. vérifier avec Setup.Core (le même code que l'installateur)
9. publier livex-setup self-contained
10. signer Authenticode (Windows) et GPG (toutes plateformes)
11. attacher les actifs à la release, puis vérifier que chaque URL du manifeste
    répond et que l'empreinte servie correspond
12. exécuter la recette de TESTING.md §7
13. publier la release GitHub
```

Les étapes 2 à 7 sont exécutées par le job `installer` de la CI, sur chaque
plateforme. L'étape 10 est exécutée sur machine propre avant publication ; un
échec bloque la release.

## 5. Signature et clés

| Élément | Mécanisme | Portée |
| :-- | :-- | :-- |
| `distribution.json` | Ed25519, clé publique embarquée dans `livex-setup` | intégrité et authenticité de la charge utile **et des actifs facultatifs** (par leurs empreintes) |
| Sommes | GPG, signature détachée | vérification manuelle |
| `livex-setup.exe` | Authenticode SHA-256 | confiance Windows, SmartScreen |
| `livex-launcher.exe`, `Simulation.Console.exe` | Authenticode SHA-256 | exécutables lancés par l'utilisateur |

Règles de gestion :

- la clé privée Ed25519 et la clé GPG ne sont jamais dans le dépôt ;
- elles sont stockées dans le coffre de secrets de la CI et accessibles
  uniquement au job de release ;
- l'empreinte publique est publiée dans `SECURITY.md`, ce document et la note
  de release ;
- une rotation de clé crée une nouvelle version de l'installateur qui embarque
  la nouvelle clé publique ; les anciennes versions ne peuvent pas vérifier les
  nouvelles distributions, ce qui est documenté ;
- une clé compromise impose une release de correction et la publication d'un
  avis.

## 6. Vérification par l'utilisateur

**Windows** : propriétés du fichier → Signatures numériques, ou
`Get-AuthenticodeSignature .\livex-setup.exe`.

**Linux** :

```bash
sha256sum -c SHA256SUMS
gpg --verify SHA256SUMS.asc SHA256SUMS
```

La clé publique et l'empreinte sont publiées dans `SECURITY.md`. Le
`LISEZ-MOI.txt` de chaque distribution reprend ces commandes : un utilisateur ne
doit pas avoir à consulter le dépôt pour vérifier ce qu'il exécute.

## 7. Canaux

| Canal | Usage | Signature | Avertissement |
| :-- | :-- | :-- | :-- |
| `stable` | release publique | complète | aucun |
| `dev` | builds internes, tests | Ed25519 seulement | affiché au démarrage de l'assistant |

Le canal est porté par `distribution.json.channel` et affiché dans `--version`.
Un build `dev` ne doit jamais être présenté comme une release.

## 8. Mise à jour

### 8.1 Politique

- **Aucune mise à jour automatique** : l'installateur ne vérifie pas les
  versions et ne télécharge rien en arrière-plan. Seuls les modules facultatifs
  explicitement cochés sont acquis, depuis l'URL du manifeste signé (ADR-008).
- Une mise à jour est une **exécution manuelle** du paquet de version
  supérieure, qui détecte l'installation et propose la mise à jour en place.
- Les données sont conservées : racine de données, espace de travail, paquets,
  archives, sessions et préférences.
- Un downgrade est refusé par défaut ; l'option de support
  `--allow-downgrade` existe pour un dépannage documenté, jamais proposée dans
  l'interface.

### 8.2 Compatibilité

| Élément | Règle |
| :-- | :-- |
| **Format de l'installation** | `.livex-install.json` (`schema`) ; un schéma supérieur est refusé avec la version requise |
| **Préférences** | `settings.json.schema` ; les clés inconnues sont préservées |
| **Composants** | versions lues dans les manifestes, jamais supposées |
| **Paquets `.livexp`** | indépendants de l'installateur ; une mise à jour ne les modifie pas |
| **Fenêtre de support** | une version N peut être mise à jour directement vers N+1 ; les sauts multiples sont autorisés si le format d'installation le permet, sinon l'assistant l'indique |

### 8.3 Échec d'une mise à jour

Une mise à jour interrompue laisse l'installation précédente utilisable ou un
état signalé avec les résidus et le journal. Elle ne doit jamais laisser un
mélange de fichiers anciens et nouveaux présenté comme valide : le point de
commit est l'écriture de `.livex-install.json` et de l'inventaire.

## 9. Checklist de release

À cocher avant de publier :

- [ ] `dotnet build -warnaserror` et `dotnet test` verts sur Windows et Linux ;
- [ ] recette de bout en bout automatisée verte (`TESTING.md` §7.1) ;
- [ ] recette manuelle sur machine propre Windows et Linux (`TESTING.md` §7.2) ;
- [ ] version du produit et versions des composants cohérentes dans le manifeste ;
- [ ] `CHANGELOG.md` mis à jour (section `[Unreleased]` → version) ;
- [ ] `THIRD-PARTY-NOTICES.md` à jour pour toute dépendance nouvelle ou mise à jour ;
- [ ] en-têtes SPDX présents sur les nouveaux fichiers source ;
- [ ] aucune taille, durée ou performance non mesurée dans la documentation ;
- [ ] signatures vérifiées sur les artefacts publiés ;
- [ ] `SHA256SUMS` et signature publiés avec l'archive ;
- [ ] notes de release : versions, taille mesurée de chaque distribution **et
      de chaque actif facultatif**, limites connues, procédure de vérification ;
- [ ] actifs facultatifs attachés à la release, avec les noms exacts du
      manifeste, et URL vérifiée une par une ;
- [ ] un module sans actif publié est déclaré `unavailable` (il ne doit pas
      apparaître coché) ;
- [ ] profil `full` installable sans réseau sur une machine propre ;
- [ ] lien de téléchargement vérifié depuis la page publique.

Une case non cochée bloque la publication. Une release partielle n'est pas une
release.

## 10. Publication

1. fusion de la PR de release vers `develop` (squash, conformément à
   `docs/governance/GITFLOW.md`) ;
2. tag `livex-vX.Y.Z` sur la racine ;
3. `release.yml` construit, vérifie et attache les artefacts ;
4. publication de la note de release avec les sommes et les empreintes de clé ;
5. mise à jour des liens de téléchargement (dépôt, site) ;
6. vérification post-publication : télécharger l'artefact publié, vérifier les
   signatures, installer sur machine propre.

## 11. Retrait d'une version

Une version défectueuse est **retirée** (assets supprimés ou marqués) et
signalée dans la note de release et `CHANGELOG.md`. Les utilisateurs qui l'ont
installée sont informés par la documentation de correction ; l'installateur ne
met rien à jour à distance.

Les actifs facultatifs d'une version retirée sont conservés tant qu'un
manifeste publié les référence : une installation existante doit pouvoir
réparer ou ajouter un module. Seuls les actifs qu'aucun manifeste publié ne
référence peuvent être supprimés.

Le retrait ne supprime jamais les données des utilisateurs.

## 12. Support et fin de vie

| Élément | Règle |
| :-- | :-- |
| **Correctifs de sécurité** | une release de correction pour la dernière version publiée |
| **Runtimes embarqués** | reconstruits à chaque release avec les correctifs disponibles |
| **Versions supportées** | la dernière version stable et la précédente mineure |
| **Fin de vie** | annoncée dans `CHANGELOG.md` et la note de release ; aucune date inventée |

## 13. Ce que ce document ne couvre pas

- la production de la build PRISM elle-même : elle relève de PRISM, pas de
  l'installateur ; ce document ne couvre que la publication de l'actif et sa
  vérification (ADR-006) ;
- l'installation du LDK dans un projet Unreal, qui reste manuelle ;
- la signature des paquets `.livexp` (sujet distinct) ;
- la distribution par dépôt Linux (`.deb`, Flatpak), hors V0.2 ;
- les mises à jour automatiques, hors périmètre.

Ces sujets sont suivis dans `ROADMAP.md` et dans les issues GitHub.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle base documentaire |
