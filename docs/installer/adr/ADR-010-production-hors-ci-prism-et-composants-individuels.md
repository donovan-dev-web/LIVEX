# ADR-010 : Production hors CI des entrées PRISM et publication des composants individuels

**Composant** : LIVEX (installateur)
**Statut** : [Accepted]
**Dernière mise à jour** : 29 octobre 2026
**Dépend de** : ADR-006, ADR-007, ADR-008
**Source Monographie** : —

---

## Contexte

La release V1 publie, en plus des installateurs, les composants individuellement :
`syne`, `echos`, `launcher`, `prism`, et les modules `prism-ldk`, `livex-docs`,
`syne-mock`. Or deux de ces artefacts ne peuvent pas être produits par la CI :

- **PRISM packagé** (Windows et Linux, plus de 400 Mo par plateforme) exige
  Unreal Engine ; aucun runner de la CI n'en dispose (`docs/CI_CD.md`).
- **PRISM-LDK** (`prism/LDK_Package/PrismLdk.zip`, 53 118 444 octets) est une
  archive construite localement, suivie par git aujourd'hui, qui contient
  `Binaries/Win64` et `Intermediate/Build` — un artefact Windows non
  reproductible en CI.

Le manifeste `distribution.json` doit porter la taille et le SHA-256 de chaque
fichier **avant** d'être produit, et la CI assemble et publie la distribution.
Il faut donc un ordre de production explicite : les entrées construites hors CI
sont publiées d'abord, épinglées, puis consommées par la CI.

Le plafond GitHub de 100 MiB concerne les fichiers du dépôt git, pas les assets
de release (moins de 2 GiB par fichier, 1000 assets par release) : la taille de
PRISM n'est pas un obstacle, l'ordre de production l'est.

## Décision

### 1. PRISM et PRISM-LDK sont des entrées externes

| Entrée | Producteur | Chemin de build | Publication |
| :-- | :-- | :-- | :-- |
| `prism-win-x64` | mainteneur, hors CI | `prism/build/win/` | asset de la release `prism-v1.0.0` |
| `prism-linux-x64` | mainteneur, hors CI | `prism/build/linux/` | asset de la release `prism-v1.0.0` |
| `prism-ldk` | mainteneur, hors CI | `prism/LDK_Package/PrismLdk.zip` | asset de la release `prism-v1.0.0` |

Ces chemins sont **ignorés par git** ; seul `installer/build/external-inputs.json`
est versionné.

### 2. Empreintes épinglées dans le dépôt

`installer/build/external-inputs.json` contient une entrée par entrée externe
(`id`, `version`, `file`, `size`, `sha256`). La CI refuse un build si un fichier
est absent ou si une empreinte diffère : un assembleur ne peut pas consommer
silencieusement une autre build que celle validée.

### 3. Ordre de publication

1. le mainteneur build PRISM (Windows, Linux) et le LDK sur le HEAD gelé ;
2. le script de packaging produit des archives déterministes dans `dist/inputs/`
   et met à jour `external-inputs.json` ;
3. tag `prism-v1.0.0` : la release d'entrées est créée, les trois archives y sont
   attachées ;
4. tag `livex-v1.0.0` : la CI télécharge les entrées, vérifie les empreintes
   épinglées, assemble les charges utiles, produit les installateurs et les
   archives de composants, calcule `SHA256SUMS`, produit les attestations et
   attache le tout à la release `livex-v1.0.0`.

Les URL des modules facultatifs pointent vers le tag `livex-v` courant
(ADR-008 §2), y compris pour les entrées recopiées depuis `prism-v`.

### 4. Archives individuelles de composants

Chaque composant est publié comme archive autonome, en plus de sa présence dans
les installateurs :

| Composant | Nom | Contenu |
| :-- | :-- | :-- |
| Launcher | `launcher-0.1.0-<rid>.<ext>` | racine installée (`livex-launcher[.exe]` + bibliothèques) |
| SYNE | `syne-0.17.0-<rid>.<ext>` | sous-arbre `components/syne/` |
| ECHOS | `echos-0.1.0-<rid>.<ext>` | sous-arbre `components/echos/` |
| PRISM | `prism-1.0.0-<rid>.<ext>` | sous-arbre `components/prism/` |
| PRISM-LDK | `prism-ldk-1.0.0.zip` | archive du plugin, bit à bit |
| Documentation | `livex-docs-1.0.0.zip` | documents Markdown |
| SYNE-Mock | `syne-mock-0.1.0-<rid>.<ext>` | `components/syne-mock/` avec runtime Node |

Ces archives servent à un usage manuel et avancé ; seuls le LDK, la
documentation et SYNE-Mock sont consommés par l'installateur. Un fichier présent
à la fois dans un installateur et dans une archive de composant a la **même
empreinte** : les archives sont déterministes (entrées triées, horodatages
`SOURCE_DATE_EPOCH`, permissions normalisées).

### 5. Garde-fous

- la CI échoue si un asset dépasse 1,9 GiB (repli non implémenté : découpage en
  parties vérifiables, à ouvrir seulement si nécessaire) ;
- la release est créée en **pré-release**, vérifiée (URL, tailles, SHA-256,
  attestations), puis passée en stable ;
- aucun asset référencé par un manifeste publié n'est supprimé ni remplacé
  (ADR-008 §7) ;
- aucun binaire ni archive n'est commité : `PrismLdk.zip` est désuivi et
  `Intermediate/` reste hors du dépôt.

## Conséquences

### Positives

- La contrainte « pas de runner Unreal » ne bloque plus la release.
- Les empreintes consommées par la CI sont celles validées par le mainteneur,
  pas celles d'une build improvisée.
- Les composants sont publiés individuellement, ce qui rend la release V1
  utilisable hors installateur et conforme à `COMPONENTS.md`.

### Négatives

- Deux tags et deux temps de publication ; une release `livex-v` dépend d'une
  release `prism-v` déjà publiée.
- Le mainteneur doit builder PRISM deux fois, sur deux plateformes, et contrôler
  les empreintes avant de lancer la CI.
- Le zip LDK publié tel quel contient `Binaries/Win64` et `Intermediate/Build`
  (environ 250 Mo décompressé) : archive lourde, Windows uniquement, à traiter
  dans une issue de suivi.

### Risques

- **Empreinte désynchronisée** : la CI refuse le build ; correction par un
  nouveau packaging, jamais par un remplacement d'asset publié.
- **Asset proche de 2 GiB** : garde-fou à 1,9 GiB ; réouverture pour un découpage
  vérifiable.
- **Build local non reproductible** : les archives sont déterministes, mais le
  contenu dépend d'Unreal Engine 5.8 ; la version du moteur est consignée dans
  la note de release.

## Alternatives considérées

- **Runner Unreal auto-hébergé** : reporté ; coût d'exploitation et risque de
  sécurité sur un dépôt public.
- **Hébergement tiers des archives PRISM** : contraire à ADR-008 (une seule
  source, le dépôt du projet).
- **Git LFS pour PRISM** : ne résout pas la production hors CI et alourdit le
  clone.
- **Ajout manuel des assets après la CI** : casserait le manifeste et les
  empreintes, donc la vérification.

## Validation / rejet

- `external-inputs.json` présent et vérifié par la CI ; un fichier modifié fait
  échouer le build.
- `prism-v1.0.0` contient les trois archives ; `livex-v1.0.0` contient les
  installateurs, les archives de composants, `SHA256SUMS` et les attestations.
- Un fichier extrait d'un installateur et de l'archive de son composant a la
  même empreinte.

Réouverture si un asset approche 2 GiB, si un runner Unreal devient disponible,
ou si la release Windows seule est décidée (amendement D5).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 29 octobre 2026 | Création | Plan d'alignement V1 : production hors CI des entrées PRISM, publication des composants individuels (numéroté 010, ADR-009 étant l'association `.livexp`) |
