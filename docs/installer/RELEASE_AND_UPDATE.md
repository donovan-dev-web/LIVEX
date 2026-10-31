# Production, publication et mise à jour de l'installateur

**Composant** : LIVEX (installateur)
**Statut** : [PROCÉDURE DE RELEASE — V1.0.0 PUBLIÉE]
**Dernière mise à jour** : 31 octobre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`TESTING.md`](TESTING.md), [`COMPONENTS.md`](COMPONENTS.md), [ADR-010](adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md), `../../VERSIONING.md`

---

## 1. Objet

Ce document décrit comment une version de l'installateur est construite,
vérifiée, publiée et mise à jour. Il ne fixe pas de date : il fixe des portes et
des artefacts.

> **État au 31 octobre 2026.** La release `livex-v1.0.0` est publiée avec les
> installateurs Windows/Linux, les actifs PRISM et les archives listés ci-dessous.
> Cette procédure décrit les artefacts V1 et sert de référence pour les releases
> suivantes ; elle ne remplace pas les preuves de recette sur machines propres.

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
| Installateurs | `livex-1.0.0-win-x64-core.zip`, `livex-1.0.0-win-x64-full.zip`, `livex-1.0.0-linux-x64-core.tar.gz`, `livex-1.0.0-linux-x64-full.tar.gz` | CI |
| Composants | `launcher-0.1.0-<rid>.<ext>`, `syne-0.17.0-<rid>.<ext>`, `echos-0.1.0-<rid>.<ext>` | CI |
| PRISM | `prism-1.0.0-win-x64.zip`, `prism-1.0.0-linux-x64.tar.gz` | entrée externe, recopiée |
| Modules | `prism-ldk-1.0.0.zip`, `livex-docs-1.0.0.zip`, `syne-mock-0.1.0-<rid>.<ext>` | LDK : entrée externe ; docs et mock : CI |
| Intégrité | `SHA256SUMS` couvrant tous les assets ; attestations GitHub de provenance | CI |

Contenu d'un installateur (identique pour les deux profils, à la charge utile
près) :

```text
livex-1.0.0-<rid>-<profil>/
├── livex-setup[.exe]              # binaire d'installation et de maintenance
├── payload/                       # charge utile ; en profil full, les actifs aussi
├── distribution.json              # manifeste canonique
├── SHA256SUMS                     # sommes de tous les fichiers de l'archive
└── LISEZ-MOI.txt                  # vérification, installation, désinstallation
```

Contenu d'une archive de composant : exactement le sous-arbre installé
(`components/syne/…`, `components/echos/…`, `components/prism/…`, racine pour le
Launcher), décompressable à la racine d'une installation. Ces archives servent à
un usage manuel et avancé ; seuls LDK, docs et mock sont consommés par
l'installateur.

### 3.2 Release d'entrées `prism-v1.0.0`

```text
prism-1.0.0-win-x64.zip
prism-1.0.0-linux-x64.tar.gz
prism-ldk-1.0.0.zip        # copie bit à bit de prism/LDK_Package/PrismLdk.zip
```

### 3.3 Règles

- le nom et l'URL de chaque actif sont ceux déclarés dans `distribution.json` ;
  aucun renommage ni remplacement après publication ;
- les URL des modules facultatifs pointent vers le tag **`livex-v1.0.0`** courant,
  y compris pour le LDK recopié depuis `prism-v1.0.0` (ADR-008 §2) ;
- les archives sont **déterministes** : entrées triées, horodatages
  `SOURCE_DATE_EPOCH`, permissions normalisées ; un fichier présent dans un
  installateur et dans une archive de composant a la même empreinte ;
- une archive embarquée dans le profil `full` est identique, octet pour octet, à
  l'asset publié ;
- la CI échoue si un asset dépasse **1,9 GiB** ; le découpage en parties
  vérifiables n'est pas implémenté tant qu'il n'est pas nécessaire ;
- l'archive d'installation est un `.zip` (Windows) et un `.tar.gz` (Linux) ; le
  contenu décompressé est identique ; aucune installation ne se fait depuis
  l'archive sans l'avoir décompressée ;
- aucun autre artefact n'est publié : pas d'installateur « web », pas de paquet
  auto-extractible non vérifiable.

## 4. Chaîne de production

```text
0. entrées externes : PRISM Windows/Linux et LDK buildés hors CI, archivés de
   façon déterministe dans dist/inputs/, empreintes épinglées dans
   installer/build/external-inputs.json, publiés sur prism-v1.0.0
1. geler les versions des composants (manifeste de build)
2. publier self-contained : livex-launcher, syne
3. assembler ECHOS : CPython 3.12 + site-packages figés
4. produire les actifs facultatifs : archive de documentation, archive SYNE-Mock
   + runtime Node
5. télécharger les entrées de prism-v1.0.0 et vérifier external-inputs.json
6. assembler la charge utile core et full (PRISM depuis l'entrée vérifiée) puis
   calculer tailles et empreintes
7. signer Authenticode les exécutables Windows (si SIGNING_ENABLED), PUIS
   calculer les empreintes : signer après hachage invaliderait files[].sha256
8. écrire distribution.json canonique (profils, acquisition, actifs)
9. vérifier le manifeste avec Setup.Core (le même code que l'installateur)
10. produire les archives d'installateurs et de composants
11. calculer SHA256SUMS et les attestations de provenance
12. créer la release livex-v1.0.0 en PRÉ-RELEASE, attacher tous les assets
13. vérifier chaque URL, taille et empreinte du manifeste publié
14. exécuter la recette de TESTING.md §7 ; passer la release en stable
```

L'ordre est contraignant : **signer avant de hacher**, publier les entrées
**avant** la distribution assemblée, vérifier **avant** de passer en stable.

## 5. Confiance, signature et clés

Décision V1 (plan d'alignement, D3 ; [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md) §3 bis) :

| Élément | Mécanisme | Portée |
| :-- | :-- | :-- |
| `distribution.json` | SHA-256 de chaque fichier, obligatoire | intégrité de la charge utile et des actifs facultatifs |
| Sommes | `SHA256SUMS` | vérification manuelle (`sha256sum -c`) |
| Provenance | attestations GitHub (`actions/attest-build-provenance`) | vérification par `gh attestation verify` |
| Windows | Authenticode SHA-256, si un certificat est disponible | SmartScreen ; appliqué avant hachage |
| Linux | aucune signature détachée en V1 | sommes + attestations |

Règles :

- **la signature Ed25519 du manifeste et la signature GPG sont retirées** : la
  clé publique voyagerait dans le même téléchargement que le manifeste, et
  l'installateur ne peut pas garantir sa propre authenticité ;
- le champ `signature` du manifeste reste **optionnel** dans le schéma, vide en
  V1, pour un usage futur ;
- `SIGNING_ENABLED` (variable de dépôt) active l'Authenticode ; désactivé par
  défaut, la release est publiée non signée avec un avertissement SmartScreen
  documenté ;
- aucun secret, certificat `.pfx` ni clé n'est dans le dépôt ; ils restent dans
  les secrets de la CI, accessibles au seul job de publication ;
- un asset publié n'est jamais remplacé : un contenu modifié impose une nouvelle
  version (ADR-008 §7).

## 6. Vérification par l'utilisateur

**Windows** : propriétés du fichier → Signatures numériques, ou
`Get-AuthenticodeSignature .\livex-setup.exe`. Une release non signée l'indique
dans ses notes : SmartScreen peut afficher un avertissement.

**Linux** :

```bash
sha256sum -c SHA256SUMS
gh attestation verify livex-1.0.0-linux-x64-core.tar.gz -R donovan-dev-web/LIVEX
```

Le `LISEZ-MOI.txt` de chaque distribution reprend ces commandes : un utilisateur
ne doit pas avoir à consulter le dépôt pour vérifier ce qu'il exécute. La
procédure et les empreintes éventuelles sont publiées dans `SECURITY.md`.

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
- [ ] `SHA256SUMS` et attestations produits et publiés ;
- [ ] aucun asset au-dessus de 1,9 GiB ; `check_assets.py` vert ;
- [ ] notes de release : versions, **taille mesurée** de chaque distribution
      **et de chaque actif facultatif**, limites connues, procédure de vérification ;
- [ ] actifs facultatifs attachés à la release, avec les noms exacts du
      manifeste, et URL vérifiée une par une ;
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
5. tag **`livex-v1.0.0`** sur `main` : `release.yml` exécute `verify`,
   `inputs`, `installer` (matrice Windows/Linux) et `publish` ;
6. release créée en **pré-release** ; vérification des URL, tailles, SHA-256 et
   attestations ;
7. recette de `TESTING.md` §7.2 sur machine propre ;
8. passage en stable (`--prerelease=false --latest`) après confirmation du
   mainteneur ;
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
