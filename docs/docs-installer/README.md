# INSTALLER — Installateur graphique de la pile LIVEX

**Composant** : LIVEX (Installateur)
**Statut** : [DRAFT]
**Version cible** : 0.1.0
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `../docs-launcher/PACKAGING.md`, `../docs-launcher/COMPONENTS.md`, `../docs-launcher/INTEGRATION_CONTRACT.md`, `../../VERSIONING.md`, `../../CI_CD.md`
**Source Monographie** : —

---

## 1. Rôle

L'installateur LIVEX est l'application qui **dépose, configure, vérifie, met à jour
et retire** la pile LIVEX sur un poste Windows ou Linux (Ubuntu), au moyen d'un
assistant graphique en étapes et d'un mode non interactif équivalent.

Il réalise le jalon **G6 — Livraison** de la feuille de route du Launcher
(`../docs-launcher/ROADMAP.md` §6.7) : installation propre, `--check` correct sur
chaque OS annoncé, mise à jour, conservation des données.

Il ne contient **ni la logique de simulation, ni l'orchestration, ni l'analyse**. Il
dépose des composants dont les manifestes (`component.json`) décrivent le
comportement ; le Launcher les détecte ensuite par le mécanisme existant
(`COMPONENTS.md` §12).

## 2. Périmètre

| Dans le périmètre | Hors périmètre |
| :-- | :-- |
| Assistant graphique Windows x64 et Linux x64 (Ubuntu LTS) | macOS, Windows arm64, Linux arm64 |
| Mode non interactif (`--unattended`) équivalent | Interface web ou installation à distance |
| Installation du Launcher, de SYNE, d'ECHOS, du mock SYNE, du plugin PRISM-LDK, de la documentation | Installation d'Unreal Engine, de Visual Studio, du SDK .NET (hors mode Développeur) |
| Contrôle de la configuration système avant installation | Optimisation ou réparation du système hôte |
| Configuration automatique : chemins, variables d'environnement, manifestes de release | Édition des paramètres de simulation (profils SYNE) |
| Mise à jour par composant, retour arrière, désinstallation | Mise à jour silencieuse en arrière-plan |
| Réparation d'une installation incomplète | Migration de paquets `.livexp` (propriété du Launcher) |

## 3. Principes

1. **Aucune élévation de privilèges par défaut.** Installation par utilisateur
   (`PACKAGING.md` §2). Une installation système est une option explicite, Linux
   seulement, hors V0.1.
2. **Les données de l'utilisateur ne sont jamais touchées.** Le workspace est
   séparé du programme ; mise à jour et désinstallation le conservent
   (`PACKAGING.md` §2.2, §10).
3. **Transactionnel.** Toute installation ou mise à jour est annulable ; une
   interruption laisse l'état précédent utilisable (`PACKAGING.md` §8).
4. **Embarquer plutôt qu'exiger.** Les runtimes nécessaires sont fournis avec les
   composants (ADR-003) ; l'installateur ne modifie pas le système au-delà du
   répertoire choisi, des raccourcis et de variables utilisateur.
5. **Une seule logique, deux interfaces.** L'assistant graphique et le mode
   non interactif exécutent le même plan d'installation (ADR-001).
6. **Honnêteté de l'offre.** Un composant n'est proposé sur un OS que si son
   manifeste le déclare et que le catalogue le marque accepté
   (`V1-CAPABILITY-MATRIX.md`, décision de cadrage n° 8).
7. **Vérifier avec le même code que le Launcher.** La dernière étape exécute
   `livex-launcher --check` ; elle n'a pas de logique de diagnostic parallèle.

## 4. Index des documents

| Document | Contenu | Statut |
| :-- | :-- | :-- |
| [`SPECIFICATION.md`](SPECIFICATION.md) | Exigences, parcours en 10 étapes, options, modes d'exécution, codes de sortie | [DRAFT] |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Projets, plan d'installation, arborescence, dépendances, configuration automatique, plateformes, sécurité | [DRAFT] |
| [`RELEASE_AND_UPDATE.md`](RELEASE_AND_UPDATE.md) | Artefacts de release, catalogue, manifestes de release, pipeline CI, mise à jour, intégrité | [DRAFT] |
| [`TESTING.md`](TESTING.md) | Stratégie de test, machines propres, matrice d'acceptation | [DRAFT] |
| [`ROADMAP.md`](ROADMAP.md) | Jalons I0–I5, prérequis externes, risques | [DRAFT] |
| [`adr/`](adr/README.md) | Quatre décisions d'architecture | [Proposed] |

## 5. État de réalisation

Rien n'est implémenté. Ce dossier est la spécification de départ. À la référence
du 8 octobre 2026 :

- aucun installateur Launcher n'existe (`V1-CAPABILITY-MATRIX.md`, ligne
  « Installation Launcher Windows/Linux ») ;
- les manifestes `component.json` de SYNE, ECHOS et du mock ne déclarent qu'un
  exécutable `linux` ;
- SYNE réel et ECHOS ne sont acceptés que sous Linux ; l'arrêt propre sous
  Windows reste ouvert (`ISSUES.md` O-34, O-39) ;
- PRISM n'a pas de manifeste compatible Launcher (`ADR-006` du Launcher).

La conséquence est tracée dans `ROADMAP.md` : l'installateur Windows peut être
construit avant que les composants y soient acceptés, mais il ne les proposera
qu'une fois le catalogue autorisé.

## 6. Points restés ouverts dans ce document

| # | Question | Impact | Échéance |
| :-- | :-- | :-- | :-- |
| OI-01 | Format de livraison Linux : archive + binaire, `.deb`, ou les deux (l'AppImage exige souvent FUSE 2, absent des Ubuntu récents) | Haute | I3 |
| OI-02 | Emplacement par défaut du workspace : défaut actuel du Launcher (`~/.livex-data`) ou dossier visible (`~/LIVEX`) | Moyenne | I1 |
| OI-03 | Version de Python embarquée : 3.12 (CI de release) ou 3.11 (minimum documenté) | Moyenne | I1 |
| OI-04 | Signature : clé de signature du catalogue, signature Authenticode Windows | Haute | I4 |
| OI-05 | Persistance du workspace choisi si `LIVEX_DATA` est absent du contexte de lancement (fichier de configuration côté Launcher) | Haute | I2 |
| OI-06 | Mécanisme de mise à jour du Launcher lui-même (processus assistant, ou bibliothèque tierce à évaluer) | Haute | I4 |
| OI-07 | Hébergement du catalogue et des artefacts (GitHub Releases ou autre) | Moyenne | I4 |
| OI-08 | Parcours hors-ligne : paquet d'installation complet unique ou téléchargement séparé | Moyenne | I4 |

Ces points sont à reporter dans `../docs-launcher/ISSUES.md` (§2, points ouverts).

## 7. Documents existants à mettre à jour

| Document | Modification |
| :-- | :-- |
| `../docs-launcher/PACKAGING.md` | §2 : « Installation par composant : Non » devient « Oui, par l'installateur » ; §3 : format de livraison ; §8 : mise à jour par composant et catalogue ; supprimer ou résoudre les six points ouverts en fin de document ; ajouter un renvoi vers ce dossier |
| `../docs-launcher/ISSUES.md` | Ajouter OI-01 à OI-08 ; lier O-41 et O-45 à l'ADR-002 de l'installateur |
| `../docs-launcher/ROADMAP.md` | §6.7 : référencer `ROADMAP.md` de ce dossier ; critère G6 |
| `../README.md` | Ajouter la ligne « Installateur » au tableau de la documentation par composant |
| `../../INSTALLATION.md` | Ajouter une section « Installation par l'installateur » et rappeler que la préparation manuelle reste valable pour le développement |
| `../../CI_CD.md` | §4 : ajouter les jobs de release `installer` (matrice Windows/Linux) |
| `../../VERSIONING.md` | Ajouter le préfixe de tag de l'installateur (`setup-v*`) ou le rattacher à `livex-v*` (à décider, ADR-004) |
| `../docs-launcher/adr/README.md` | Ajouter un renvoi vers `adr/README.md` de ce dossier |
