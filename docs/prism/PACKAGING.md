# PRISM — Packaging pour une release LIVEX

**Composant** : PRISM
**Statut** : [EXÉCUTÉ POUR LIVEX 1.0.0] — PRISM Windows/Linux et PRISM-LDK produits et publiés
**Dernière mise à jour** : 31 octobre 2026
**Dépend de** : [`README.md`](README.md), [`../installer/adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md`](../installer/adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md), [`../installer/RELEASE_AND_UPDATE.md`](../installer/RELEASE_AND_UPDATE.md)

---

## 1. Objet

Ce document décrit la production des **entrées externes** de la release LIVEX :
la build PRISM packagée (Windows et Linux) et l'archive PRISM-LDK. Ces artefacts
ne peuvent pas être produits par la CI : aucun runner n'a Unreal Engine
(ADR-010). Ils sont donc buildés par le mainteneur, archivés de façon
déterministe, épinglés, puis consommés par la CI au tag `livex-v`.

> **Périmètre du dépôt.** Ce checkout contient le plugin (`prism/LDK/`) et son
> hôte technique (`prism/LDK/LDK.uproject`), pas le projet Unreal PRISM complet,
> qui vit hors de ce dépôt. La build PRISM se fait donc dans ce projet externe ;
> seuls les **résultats** arrivent ici, dans des chemins ignorés par git.

## 2. Prérequis

| Élément | Valeur |
| :-- | :-- |
| Unreal Engine | **5.8.0** (`EngineVersion` de `prism/LDK/PrismLdk.uplugin`) |
| Cible Windows | `win-x64`, build packagée |
| Cible Linux | `linux-x64`, build packagée |
| Version PRISM | **1.0.0** (plan V1, D1) |
| Version PRISM-LDK | **1.0.0** (`VersionName` du `.uplugin`, non modifié par le packaging) |

## 3. Chemins

| Sortie | Chemin local | Suivi par git |
| :-- | :-- | :-- |
| Build PRISM Windows | `prism/build/win/` | non |
| Build PRISM Linux | `prism/build/linux/` | non |
| Archive LDK source | `prism/LDK_Package/PrismLdk.zip` | **non** (à désuivre, T1) |
| Archives prêtes à publier | `dist/inputs/` | non |

`prism/build/` et `dist/` sont ignorés par git ; `PrismLdk.zip` ne doit plus
être suivi (le blob déjà présent dans l'historique reste, ce qui est accepté).

## 4. Procédure

1. **Geler la source** : partir du HEAD de `release/1.0.0` (aucune build depuis
   une branche de travail).
2. **Builder PRISM Windows** dans le projet Unreal (Package Project, cible
   Windows, configuration Shipping) et copier le dossier packagé dans
   `prism/build/win/`.
3. **Builder PRISM Linux** de la même façon et copier le résultat dans
   `prism/build/linux/`.
4. **Builder le LDK** : compiler le plugin pour Unreal Engine 5.8 et produire
   `prism/LDK_Package/PrismLdk.zip` (contenu actuel : `Source/`, `Content/`,
   `Resources/`, `PrismLdk.uplugin`, `Binaries/Win64`, `Intermediate/Build`).
   Le `.uplugin` n'est pas modifié.
5. **Archiver de façon déterministe** avec le script de release (T2) :

   ```bash
   python scripts/release/package_prism.py --version 1.0.0
   ```

   Le script produit `dist/inputs/prism-1.0.0-win-x64.zip`,
   `prism-1.0.0-linux-x64.tar.gz` et `prism-ldk-1.0.0.zip`, copie le LDK **bit
   à bit**, calcule taille et SHA-256, et met à jour
   `installer/build/external-inputs.json`.
6. **Contrôler** : `size` et `sha256` de `external-inputs.json` correspondent
   aux fichiers de `dist/inputs/` ; l'archive LDK est identique octet pour octet
   à `prism/LDK_Package/PrismLdk.zip`.
7. **Publication réalisée** : les entrées versionnées sont publiées sur le tag
   `prism-v1.0.0` et consommées par la release assemblée `livex-v1.0.0`.

## 5. Règles

- les archives sont **déterministes** : entrées triées, horodatages
  `SOURCE_DATE_EPOCH`, permissions normalisées ; une même build produit la même
  empreinte ;
- le nom des archives est **définitif** : aucun renommage ni remplacement après
  publication ; un contenu modifié impose une nouvelle version (ADR-008 §7) ;
- l'archive LDK est publiée **telle quelle**, sans ré-archivage ni nettoyage :
  retirer `Intermediate/` est un changement de contenu, donc une nouvelle
  version ; l'étude d'une variante source seule est une issue de suivi (D2) ;
- aucune archive PRISM n'est commitée ; `prism/build/` reste ignoré ;
- la CI échoue si un asset dépasse 1,9 GiB.

## 6. Limites connues

| Limite | Traitement |
| :-- | :-- |
| La build PRISM dépend d'un projet Unreal **hors de ce dépôt** | procédure documentée ici, exécution par le mainteneur |
| L'archive LDK est Windows uniquement (53 118 444 octets, ~250 Mo décompressé) | publiée telle quelle (D2) ; variante source seule à l'étude |
| Aucune reproductibilité bit à bit d'Unreal Engine | archives déterministes, contenu dépendant de la version 5.8.0 consignée |
| Conditions du moteur Epic Games | à vérifier dans `THIRD-PARTY-NOTICES.md` avant publication |

## 7. Références

- [`../installer/adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md`](../installer/adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md)
- [`../installer/RELEASE_AND_UPDATE.md`](../installer/RELEASE_AND_UPDATE.md) §3, §4, §10
- [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md)
- [`BLUEPRINT_ROADMAP.md`](BLUEPRINT_ROADMAP.md) (empaquetage)

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 29 octobre 2026 | Création | Plan d'alignement V1 : production hors CI des entrées PRISM et LDK (ADR-010) |
