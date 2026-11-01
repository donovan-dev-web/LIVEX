# Production, publication et mise à jour de l'installateur

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT] — chaîne locale V1
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`TESTING.md`](TESTING.md), [`COMPONENTS.md`](COMPONENTS.md), [ADR-010](adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md), `../../VERSIONING.md`

---

## 1. Objet

Ce document décrit comment une version de l'installateur est construite,
vérifiée, publiée et mise à jour. Il ne fixe pas de date : il fixe des portes et
des artefacts.

> **État au 1er novembre 2026.** La chaîne est **locale** : `build-release.ps1`
> publie les binaires, assemble la distribution et produit le ZIP public.
> GitHub Actions ne compile ni ne signe rien ; le workflow `release.yml` se
> limite à créer la release qui recevra l'upload local.

## 2. Versionnement

| Élément | Version V1 | Portée par |
| :-- | :-- | :-- |
| **Global LIVEX** | `1.0.0` | `distribution.json.version`, tag `livex-v1.0.0`, `CITATION.cff` |
| **Installateur** | `1.0.0` | `installer/Directory.Build.props`, affichée par `--version` |
| **PRISM** | `1.0.0` | archives d'entrée, `external-inputs.json` |
| **PRISM-LDK** | `1.0.0` | `VersionName` `1.0` du `.uplugin`, archive d'entrée |
| **SYNE** | `0.17.0` | `syne/component.json` |
| **ECHOS** | `0.1.0` | `echos/component.json` |
| **Launcher** | `0.1.0` | `launcher/Directory.Build.props` |
| **SYNE-Mock** | `0.1.0` | `syne-mock/component.json` |

Le passage à `1.0.0` est un jalon **transverse** : les composants gardent leur
SemVer propre. Une release ne publie que des composants dont les versions sont
figées dans le manifeste, et `scripts/release/check_versions.py` vérifie la
cohérence de toutes les sources (manifestes, `Directory.Build.props`,
`package.json`, `.uplugin`, `CITATION.cff`, `CHANGELOG.md`).

Deux tags coexistent :

| Tag | Porte | Rôle |
| :-- | :-- | :-- |
| `prism-v1.0.0` | `release/1.0.0` | release d'**entrées** : PRISM Windows, PRISM Linux, PRISM-LDK |
| `livex-v1.0.0` | `main`, après fusion | release **assemblée** : installateurs, archives de composants, sommes, attestations |

Les tags `syne-v*` et `echos-v*` ne sont pas posés pour la V1 ; les versions des
composants sont consignées dans les notes de release.

## 3. Artefacts produits

### 3.1 Release assemblée `livex-v1.0.0`

| Catégorie | Nom | Origine |
| :-- | :-- | :-- |
| ZIP public | `LIVEX-1.0.0-win-x64-core.zip` (et `full`, `linux-x64`) | build local |
| Composants | `packages/<id>-<version>-<rid>.zip` **à l'intérieur** du ZIP | build local |
| PRISM | `prism-1.0.0-win-x64.zip`, `prism-1.0.0-linux-x64.tar.gz` | entrée externe (profil `core`) |
| Modules | `prism-ldk-1.0.0.zip` (actif externe) ; `syne-mock` embarqué | LDK externe ; mock local |

Contenu du ZIP public :

```text
LIVEX-1.0.0-win-x64-core.zip
└── LIVEX-1.0.0-win-x64-core/
    ├── livex-setup.exe            # binaire d'installation et de maintenance
    ├── distribution.json          # manifeste (acquisition bundled-archive)
    ├── packages/                  # un ZIP par composant embarqué
    │   ├── launcher-0.1.0-win-x64.zip
    │   ├── syne-0.17.0-win-x64.zip
    │   ├── echos-0.1.0-win-x64.zip
    │   ├── syne-mock-0.1.0-win-x64.zip
    │   └── resources-1.0.0-win-x64.zip
    ├── assets/                    # icônes et ressources racine
    ├── LICENSE
    ├── NOTICE
    ├── THIRD-PARTY-NOTICES.md
    └── LISEZ-MOI.txt
```

Chaque ZIP de composant contient exactement le sous-arbre installé
(`core/launcher/`, `components/syne/`, `assets/`, etc.) et le `component.json`
normalisé lorsqu'il existe. L'installateur extrait ces archives ; le manifeste
public ne duplique pas la liste des fichiers.

### 3.2 Release d'entrées `prism-v1.0.0`

```text
prism-1.0.0-win-x64.zip
prism-1.0.0-linux-x64.tar.gz
prism-ldk-1.0.0.zip        # copie bit à bit de prism/LDK_Package/PrismLdk.zip
```

Ces actifs restent nécessaires au profil `core` ; le profil `full` les embarque
dans `packages/`.

### 3.3 Règles

- le nom et l'URL de chaque actif sont ceux déclarés dans `distribution.json` ;
  aucun renommage ni remplacement après publication ;
- les URL des actifs externes pointent vers le tag **`livex-v1.0.0`** courant,
  y compris pour le LDK recopié depuis `prism-v1.0.0` (ADR-008 §2) ;
- les ZIP sont **déterministes** : entrées triées, horodatages
  `SOURCE_DATE_EPOCH`, permissions normalisées ;
- le ZIP public final est l'unique fichier à télécharger ; les archives de
  composants ne sont pas publiées séparément ;
- aucune compilation ni signature n'a lieu dans GitHub Actions ;
- aucun autre artefact n'est publié : pas d'installateur « web », pas de paquet
  auto-extractible non vérifiable.

## 4. Chaîne de production

Entièrement locale, sur le poste du mainteneur :

```text
0. entrées externes : PRISM Windows/Linux et LDK buildés hors CI, archivés de
   façon déterministe dans dist/inputs/, empreintes épinglées dans
   installer/build/external-inputs.json
1. pwsh scripts/release/build-release.ps1 -Version 1.0.0 -Tag livex-v1.0.0
2. publication self-contained : livex-launcher (core/launcher), livex-setup
   (single-file) et syne ; copie de syne/component.json
3. assemblage ECHOS : CPython 3.12 + site-packages figés
4. assemblage SYNE-Mock
5. vérification des entrées externes (check_assets.py)
6. build_distribution.py :
   - normalise les component.json,
   - compresse un ZIP par composant dans packages/,
   - calcule taille et SHA-256,
   - écrit distribution.json,
   - copie livex-setup, mentions légales et LISEZ-MOI.txt,
   - produit LIVEX-<version>-<rid>-<profile>.zip
7. tests : dotnet test installer/Setup.sln, dotnet build launcher/Launcher.sln
8. recette de TESTING.md §7 sur une machine propre
9. création manuelle de la release GitHub (workflow release.yml), puis :
   pwsh scripts/release/build-release.ps1 ... -Upload
10. vérification du ZIP téléchargé, passage en stable
```

L'ordre est contraignant : les entrées externes sont vérifiées **avant**
l'assemblage, et le ZIP public est testé **avant** l'upload final.

## 5. Confiance, signature et clés

Décision V1 (plan d'alignement, D3 ; [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md) §3 bis) :

| Élément | Mécanisme | Portée |
| :-- | :-- | :-- |
| `distribution.json` | SHA-256 de chaque archive `packages/` | intégrité de la charge utile et des actifs facultatifs |
| ZIP public | empreinte SHA-256 publiée par GitHub pour l'asset téléchargé | vérification manuelle |
| Provenance | aucun build CI en V1 | la confiance repose sur le poste de build |
| Windows | Authenticode SHA-256, si un certificat est disponible | SmartScreen ; appliqué avant le calcul des empreintes |
| Linux | aucune signature détachée en V1 | sommes manuelles |

Règles :

- **la signature Ed25519 du manifeste et la signature GPG sont retirées** : la
  clé publique voyagerait dans le même téléchargement que le manifeste, et
  l'installateur ne peut pas garantir sa propre authenticité ;
- le champ `signature` du manifeste reste **optionnel** dans le schéma, vide en
  V1, pour un usage futur ;
- l'Authenticode est appliqué **localement avant** le calcul des empreintes du
  manifeste ;
- aucun secret, certificat `.pfx` ni clé n'est dans le dépôt ;
- un asset publié n'est jamais remplacé : un contenu modifié impose une nouvelle
  version (ADR-008 §7).

## 6. Vérification par l'utilisateur

**Windows** : propriétés du fichier → Signatures numériques, ou
`Get-AuthenticodeSignature .\livex-setup.exe`. Une release non signée l'indique
dans ses notes : SmartScreen peut afficher un avertissement.

**Toutes plateformes** : comparer le SHA-256 du ZIP téléchargé à celui affiché
par la release GitHub, puis décompresser le ZIP et lancer `livex-setup`.

```powershell
Get-FileHash .\LIVEX-1.0.0-win-x64-core.zip -Algorithm SHA256
```

```bash
sha256sum LIVEX-1.0.0-linux-x64-core.zip
```

Le `LISEZ-MOI.txt` de chaque distribution reprend ces étapes : un utilisateur
ne doit pas avoir à consulter le dépôt pour vérifier ce qu'il exécute.

## 7. Canaux

| Canal | Usage | Confiance | Avertissement |
| :-- | :-- | :-- | :-- |
| `stable` | release publique | sommes + attestations (+ Authenticode si disponible) | aucun, sauf SmartScreen si non signé |
| `dev` | builds internes, tests | sommes uniquement | affiché au démarrage de l'assistant |

Le canal est porté par `distribution.json.channel` et affiché dans `--version`.
Un build `dev` ne doit jamais être présenté comme une release.

## 8. Mise à jour

### 8.1 Politique

- **Aucune mise à jour automatique** : l'installateur ne vérifie pas les
  versions et ne télécharge rien en arrière-plan. Seuls les modules facultatifs
  explicitement cochés sont acquis, depuis l'URL du manifeste (ADR-008).
- Une mise à jour est une **exécution manuelle** du paquet de version
  supérieure, qui détecte l'installation et propose la mise à jour en place.
- Les données sont conservées : racine de données, espace de travail, paquets,
  archives, sessions et préférences.
- Un downgrade est refusé par défaut, sans option de contournement en V1 ; un
  retour arrière passe par une réinstallation de la version précédente,
  documentée comme non prise en charge.

### 8.2 Compatibilité

| Élément | Règle |
| :-- | :-- |
| **Format de l'installation** | `.livex-install.json` (`schema`) ; un schéma supérieur est refusé avec la version requise |
| **Préférences** | `settings.json` ; les clés inconnues sont préservées |
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
- [ ] `check_versions.py` vert : manifeste, `component.json`, `Directory.Build.props`, `package.json`, `.uplugin`, `CITATION.cff`, `CHANGELOG.md` ;
- [ ] entrées externes présentes et conformes à `installer/build/external-inputs.json` ;
- [ ] version du produit et versions des composants cohérentes dans le manifeste ;
- [ ] `CHANGELOG.md` mis à jour (section `[Unreleased]` → `[1.0.0]`) ;
- [ ] `THIRD-PARTY-NOTICES.md` à jour, relevé sur les artefacts réels ;
- [ ] en-têtes SPDX présents sur les nouveaux fichiers source ;
- [ ] aucune taille, durée ou performance non mesurée dans la documentation ;
- [ ] ZIP public unique produit localement et SHA-256 relevé ;
- [ ] `packages/` contient un ZIP par composant et aucun `payload/` résiduel ;
- [ ] `distribution.json` ne référence que des `bundled-archive` présentes ou
      des `release-asset` réellement publiés ;
- [ ] notes de release : versions, **taille mesurée** du ZIP, limites connues,
      procédure de vérification ;
- [ ] actifs externes attachés à la release, avec les noms exacts du manifeste,
      et URL vérifiée une par une ;
- [ ] un module sans actif publié est déclaré `unavailable` (il ne doit pas
      apparaître coché) ;
- [ ] profil `full` installable sans réseau sur une machine propre ;
- [ ] lien de téléchargement vérifié depuis la page publique ;
- [ ] release créée en pré-release, vérifiée, puis passée en stable.

Une case non cochée bloque la publication. Une release partielle n'est pas une
release.

## 10. Publication

Flux conforme à `docs/governance/GITFLOW.md` :

1. `release/1.0.0` depuis `develop`, gel des fonctionnalités ;
2. bump des versions (D1), `CHANGELOG.md`, `CITATION.cff`, `LIVEX-STATUS.md` ;
3. build des entrées externes sur le HEAD gelé, puis tag **`prism-v1.0.0`** sur
   `release/1.0.0` et upload des trois archives d'entrée ;
4. PR `release/1.0.0` → `main` (CI verte, aucune fonctionnalité nouvelle) ;
5. création de la release GitHub `livex-v1.0.0` via le workflow
   `release.yml` (aucun build) ;
6. build local : `build-release.ps1 -Version 1.0.0 -Tag livex-v1.0.0` ;
7. recette locale du ZIP produit, puis upload :
   `build-release.ps1 ... -Upload` (ou `gh release upload`) ;
8. vérification des URL, tailles et SHA-256, puis passage en stable si la
   release a été créée en pré-release ;
9. retour `main` → `develop`, suppression de `release/1.0.0` ;
10. PR de publication (README, `docs/INSTALLATION.md`, site FR/EN) fusionnée
    **seulement après** vérification de la release publique.

Un échec en cours de chaîne se corrige par une nouvelle PR ; un tag publié n'est
jamais déplacé ni réécrit.

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

- la production de la build PRISM elle-même : elle est décrite dans
  [`../prism/PACKAGING.md`](../prism/PACKAGING.md) ; ce document ne couvre que
  l'épinglage, la publication et la vérification des entrées ;
- l'installation du LDK dans un projet Unreal, qui reste manuelle ;
- la signature des paquets `.livexp` (sujet distinct) ;
- la distribution par dépôt Linux (`.deb`, Flatpak), hors V1 ;
- les mises à jour automatiques, hors périmètre.

Ces sujets sont suivis dans [`ROADMAP.md`](ROADMAP.md) et dans les issues
GitHub.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle base documentaire |
| 29 octobre 2026 | §2 versions V1, §3 artefacts (4 installateurs, archives de composants, entrées), §4 chaîne (entrées épinglées, Authenticode avant hachage, attestations, garde-fou 1,9 GiB, pré-release), §5/§6/§7 confiance D3, §9 checklist, §10 Gitflow et deux tags, §13 renvoi PACKAGING | Plan d'alignement V1 : corrections C6a/C6b/C6c, décisions D1 à D6, ADR-010 |
| 1er novembre 2026 | §1 chaîne locale, §3 ZIP unique et `packages/`, §4 build-release local, §5/§6 vérification sans build CI, §9 checklist, §10 publication manuelle | Corrections V1 de l'installateur et du workflow de release |
