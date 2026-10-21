# Installateur LIVEX — documentation de référence

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT] — documentation de référence, aucun installateur livré
**Dernière mise à jour** : 21 octobre 2026

---

## 1. En bref

L'installateur LIVEX installe, met à jour, répare et désinstalle la pile LIVEX
sur un poste de travail : **Launcher**, **SYNE**, **ECHOS**, leurs contrats et
leurs ressources — le **socle obligatoire**, installé **hors ligne** — ainsi que
**PRISM**, **composant de base** livré dans la même distribution et coché par défaut, puis,
si l'utilisateur les choisit, les **modules facultatifs** : **PRISM-LDK** (plugin
Unreal en ZIP), la **documentation hors ligne** et **SYNE-Mock**. Il fonctionne
**sans privilèges administrateur** et ne touche jamais aux résultats de recherche
sans demande explicite.

> **État réel :** aucun installateur n'est livré à ce jour. Ce dossier est la
> documentation de référence qui précède l'implémentation. Il remplace la
> documentation retirée du dépôt et la spécification de base v0.2, qui n'était
> qu'un document de travail.

## 2. Ce que l'installateur fait

| Fonction | Détail |
| :-- | :-- |
| **Installer** | assistant guidé en français et en anglais, huit étapes, sélection des composants |
| **Acquérir** | modules facultatifs (LDK, documentation, SYNE-Mock) téléchargés depuis la release GitHub, vérifiés par empreinte, ou lus depuis une archive locale |
| **Choisir les chemins** | répertoire logiciel et espace de travail séparés, validés avant écriture |
| **Vérifier** | signature de la distribution, empreinte de chaque fichier, droits, espace disque |
| **Enregistrer** | installation par utilisateur, détection au lancement suivant, entrée système |
| **Mettre à jour** | mise à jour manuelle en place, données conservées, downgrade refusé |
| **Réparer** | vérification de l'inventaire, restauration des fichiers depuis la distribution |
| **Désinstaller** | trois portées (logiciel, configuration, espace de travail), résultats de recherche protégés |
| **Diagnostiquer** | journal UTF-8, mode `--check` sans interface, codes de sortie documentés |

## 3. Ce que l'installateur ne fait pas

- aucune mise à jour automatique, aucune vérification de version, aucun
  téléchargement en arrière-plan ;
- aucune installation machine, aucun service, aucune élévation ;
- aucun téléchargement pour le socle ni pour PRISM : ils s'installent sans réseau ;
- aucun module facultatif installé dans un projet Unreal : l'installateur
  dépose l'archive du LDK, il ne modifie pas le projet hôte ;
- aucune association de fichiers, aucun `PATH`, aucune variable globale ;
- aucune suppression de paquets `.livexp` ou d'archives sans extension
  explicite de la désinstallation ;
- aucun support de macOS ni des architectures non x64.

## 4. Carte documentaire

| Public | Document | Contenu |
| :-- | :-- | :-- |
| **Utilisateur** | [`GUIDE-UTILISATEUR.md`](GUIDE-UTILISATEUR.md) | installer, mettre à jour, réparer, désinstaller, dépannage |
| **Développeur** | [`COMPONENTS.md`](COMPONENTS.md) | rôle, artefact et installabilité de chaque composant |
| **Développeur** | [`ARCHITECTURE.md`](ARCHITECTURE.md) | découpage, flux, formats, sécurité, build |
| **Développeur** | [`SPECIFICATION.md`](SPECIFICATION.md) | exigences normatives et critères d'acceptation |
| **Développeur** | [`TESTING.md`](TESTING.md) | niveaux de test, scénarios, matrice de traçabilité |
| **Mainteneur** | [`RELEASE_AND_UPDATE.md`](RELEASE_AND_UPDATE.md) | production, signature, publication, mise à jour |
| **Mainteneur** | [`ROADMAP.md`](ROADMAP.md) | phases, critères de sortie, risques |
| **Architecte** | [`adr/`](adr/README.md) | décisions, alternatives, conséquences |

Ordre de lecture recommandé : ce README, le guide utilisateur, puis la
spécification, l'architecture et les ADR.

## 5. Périmètre de la V0.2

### Composants installés

| Composant | Statut | Version portée par |
| :-- | :-- | :-- |
| Launcher | obligatoire | `distribution.json`, contrôlée contre `component.json` si présent |
| SYNE | obligatoire | `distribution.json`, contrôlée contre `syne/component.json` |
| ECHOS | obligatoire | `distribution.json`, contrôlée contre `echos/component.json` |
| Contrats | support obligatoire, non décochable | `distribution.json` (`launcher/contracts/`) |
| Ressources | support obligatoire, non décochable | `distribution.json` (`assets/`) |
| PRISM | **composant de base, coché par défaut** | `distribution.json` (`payload/`, `components/prism/`) |
| PRISM-LDK | module facultatif, décoché | actif `prism-ldk-<version>.zip`, `plugins/prism-ldk/` |
| Documentation hors ligne | module facultatif, décoché | actif `livex-docs-<version>.zip`, `docs/` |
| SYNE-Mock | module facultatif, décoché | actif `syne-mock-<version>.zip`, `components/syne-mock/` |

**Seuls Launcher, SYNE et ECHOS sont obligatoires.** PRISM est un composant de
base : il est embarqué, **coché par défaut**, et le décocher affiche
l'avertissement prévu par la base (§3.3). Les **trois modules facultatifs** sont
acquis depuis les actifs de la release GitHub (profil `core`), ou embarqués dans
la variante `full`. Un module dont l'actif n'est pas publié pour la plateforme
est affiché **indisponible**, avec sa raison, jamais comme une case sans effet.

PRISM est une **application packagée** Windows/Linux : Unreal Engine n'est pas
requis sur le poste. PRISM-LDK est le seul élément qui exige Unreal Engine,
dans le projet de l'utilisateur.

### Plateformes

| Plateforme | Support | Prérequis |
| :-- | :-- | :-- |
| Windows 10/11 x64 | supporté | aucun runtime .NET ni Python |
| Linux x64 (glibc) | supporté | `libicu`, `libssl`, affichage, fontconfig |

Les versions Linux minimales sont fixées par la matrice de [`TESTING.md`](TESTING.md).

## 6. Principes directeurs

1. **Simplicité** — un parcours guidé, sans jargon, utilisable au clavier.
2. **Autonomie** — le socle s'installe sans réseau ni runtime ; les modules
   facultatifs sont un choix explicite, jamais une nécessité.
3. **Moindre privilège** — installation par utilisateur, sans élévation.
4. **Séparation** — logiciel, données et espace de travail sont distincts.
5. **Intégrité** — rien n'est écrit avant vérification de la signature et des
   empreintes.
6. **Réversibilité raisonnable** — nettoyage et restauration au mieux, jamais
   de promesse de retour arrière absolu.
7. **Protection des données** — un résultat de recherche ne disparaît pas par
   accident.
8. **Transparence** — les erreurs, les limites et les résidus sont affichés et
   journalisés.
9. **Honnêteté** — aucun composant proposé sans artefact, aucune taille ni
   durée non mesurée dans la documentation.
10. **Cohérence avec le Launcher** — mêmes contrats, mêmes conventions, sans
    couplage de code.

## 7. Décisions structurantes

| Sujet | Décision | Réf. |
| :-- | :-- | :-- |
| Technologie | installateur .NET 10 autonome, assistant Avalonia, cœur sans interface | [ADR-001](adr/ADR-001-technologie-installateur-dotnet.md) |
| Privilèges | installation par utilisateur, sans élévation | [ADR-002](adr/ADR-002-installation-utilisateur-sans-elevation.md) |
| Runtimes | .NET self-contained, CPython embarqué pour ECHOS | [ADR-003](adr/ADR-003-dependances-embarquees.md) |
| Données | racine de données, `settings.json`, espace de travail choisi | [ADR-004](adr/ADR-004-racine-de-donnees-et-espace-de-travail.md) |
| Cycle de vie | enregistrement HKCU/XDG, inventaire, réparation | [ADR-005](adr/ADR-005-enregistrement-et-cycle-de-vie.md) |
| Composants | socle et PRISM embarqués ; LDK, docs et SYNE-Mock facultatifs, actifs de release | [ADR-006](adr/ADR-006-perimetre-composants-v02.md) |
| Confiance | manifeste signé Ed25519, Authenticode, GPG | [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md) |
| Acquisition | profil `core` (téléchargement explicite) ou `full` (hors ligne complet) | [ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md) |

## 8. Vocabulaire

| Terme | Sens |
| :-- | :-- |
| **Répertoire logiciel** | dossier qui contient le Launcher, ses composants et leurs ressources |
| **Racine de données** | `LIVEX_DATA` ou `~/.livex-data` : paquets, archives, sessions, préférences |
| **Espace de travail** | dossier choisi pour les expériences et les rapports |
| **Résultats de recherche** | paquets `.livexp`, archives scellées, sessions, base d'analyse |
| **Distribution** | dossier téléchargé : `livex-setup`, `payload/`, manifeste, signatures |
| **Composant de base** | PRISM : embarqué, coché par défaut, désactivable ; sa désactivation est avertie |
| **Module facultatif** | composant décoché par défaut : PRISM-LDK, documentation, SYNE-Mock |
| **Actif de release** | archive attachée à une release GitHub, vérifiée par taille et SHA-256 |
| **Profil** | `core` (socle seul, modules à la demande) ou `full` (tout embarqué) |
| **Maintenance** | mise à jour, réparation, ajout/retrait de composant, désinstallation |
| **Charge utile** | fichiers réellement copiés dans l'installation |

## 9. Signaler un problème

- défaut de l'installateur : issue GitHub avec le label `component/installer`,
  en joignant le journal (`<racine de données>/logs/installer-*.log`) et la
  version affichée par `livex-setup --version` ;
- défaut d'un composant : issue avec le label du composant concerné ;
- faille de sécurité : voir `SECURITY.md`, jamais une issue publique.

## 10. Contribuer à cette documentation

Une décision se documente par un ADR, pas par une retouche silencieuse. Une
exigence se modifie dans `SPECIFICATION.md`, avec sa traçabilité et son test.
Une affirmation technique doit être vérifiable dans le dépôt ou mesurée ; une
estimation non mesurée est signalée comme telle.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Remplace la documentation retirée |
