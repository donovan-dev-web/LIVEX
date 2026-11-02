# Installateur LIVEX — documentation de référence

**Composant** : LIVEX (installateur)
**Statut** : [LIVRÉ ET VALIDÉ] — installateur 1.0.0 inclus dans la release publique Windows/Linux `livex-v1.0.0`; suites automatisées exécutées
**Dernière mise à jour** : 31 octobre 2026

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

> **État réel :** l'installateur est implémenté dans `installer/` et livré avec LIVEX 1.0.0 pour Windows et Linux. Son **mode démonstration**
> permettant de parcourir tout l'assistant sans rien installer (voir §5 bis).
> Ce dossier reste la documentation de référence : il remplace la documentation
> retirée du dépôt et la spécification de base v0.2, qui n'était qu'un document
> de travail.

## 2. Ce que l'installateur fait

| Fonction | Détail |
| :-- | :-- |
| **Installer** | assistant guidé en français et en anglais, huit étapes, sélection des composants |
| **Acquérir** | modules facultatifs (LDK, documentation, SYNE-Mock) téléchargés depuis la release GitHub et vérifiés par empreinte, ou embarqués dans la variante `full` |
| **Choisir les chemins** | répertoire logiciel et espace de travail séparés, validés avant écriture |
| **Vérifier** | empreinte SHA-256 de chaque fichier, forme du manifeste, droits, espace disque ; la confiance V1 repose sur ces empreintes, le `SHA256SUMS` publié et les attestations GitHub, pas sur une signature du manifeste (ADR-007 §3 bis) |
| **Enregistrer** | installation par utilisateur, détection au lancement suivant, entrée système |
| **Mettre à jour** | mise à jour manuelle en place, données conservées, downgrade refusé |
| **Réparer** | vérification de l'inventaire, restauration des fichiers depuis la distribution |
| **Désinstaller** | trois portées (logiciel, configuration, espace de travail), résultats de recherche protégés |
| **Diagnostiquer** | journal UTF-8, mode `--check` sans interface, codes de sortie documentés |
| **Automatiser** | modes non interactifs `--check`, `--unattended --config`, `--uninstall`, `--repair`, codes de sortie ([`COMMANDES.md`](COMMANDES.md)) |

## 3. Ce que l'installateur ne fait pas

- aucune mise à jour automatique, aucune vérification de version, aucun
  téléchargement en arrière-plan ;
- aucune installation machine, aucun service, aucune élévation ;
- aucun téléchargement pour le socle ni pour PRISM : ils s'installent sans réseau ;
- aucun module facultatif installé dans un projet Unreal : l'installateur
  dépose l'archive du LDK, il ne modifie pas le projet hôte ;
- aucune association de fichiers **autre que `.livexp`** (voir
  [ADR-009](adr/ADR-009-association-fichiers-livexp.md)), aucun `PATH`, aucune
  variable globale ;
- aucune suppression de paquets `.livexp` ou d'archives sans extension
  explicite de la désinstallation ;
- aucun support de macOS ni des architectures non x64.

## 4. Carte documentaire

| Public | Document | Contenu |
| :-- | :-- | :-- |
| **Utilisateur** | [`GUIDE-UTILISATEUR.md`](GUIDE-UTILISATEUR.md) | installer, mettre à jour, réparer, désinstaller, dépannage |
| **Utilisateur / CI** | [`COMMANDES.md`](COMMANDES.md) | toutes les options, codes de sortie, fichier de réponses, journal |
| **Développeur** | [`COMPONENTS.md`](COMPONENTS.md) | rôle, artefact et installabilité de chaque composant |
| **Développeur** | [`ARCHITECTURE.md`](ARCHITECTURE.md) | découpage, flux, formats, sécurité, build |
| **Développeur** | [`SPECIFICATION.md`](SPECIFICATION.md) | exigences normatives et critères d'acceptation |
| **Développeur** | [`TESTING.md`](TESTING.md) | niveaux de test, scénarios, matrice de traçabilité |
| **Mainteneur** | [`RELEASE_AND_UPDATE.md`](RELEASE_AND_UPDATE.md) | production, signature, publication, mise à jour |
| **Mainteneur** | [`ROADMAP.md`](ROADMAP.md) | phases, critères de sortie, risques |
| **Architecte** | [`adr/`](adr/README.md) | décisions, alternatives, conséquences |

Ordre de lecture recommandé : ce README, le guide utilisateur, puis la
spécification, l'architecture et les ADR.

## 5. Périmètre livré en V1.0.0

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

## 5 bis. Mode démonstration (monde fictif)

Pour vérifier les écrans, les flux et la mise en page **sans rien installer**,
l'installateur embarque un **monde fictif** :

| Élément | Comportement |
| :-- | :-- |
| Activation | `livex-setup --demo`, ou bascule « Mode démonstration » sur l'écran d'accueil ; activé automatiquement si aucune `distribution.json` n'accompagne l'exécutable |
| Scénarios | `--demo-scenario fresh` (défaut), `existing`, `prism` (PRISM indisponible), `failure` (échec en cours d'écriture) |
| Garantie | `DryRun` strict : aucune écriture disque, aucun téléchargement, aucun raccourci, aucune clé de registre, aucun fichier de journal |
| Contenu | distribution imaginaire complète (socle, PRISM coché par défaut, LDK / documentation / SYNE-Mock en actifs, tailles et empreintes fictives mais déterministes), inventaire et installation existante de démonstration |
| Plan | le plan réel est calculé puis rejoué action par action ; récapitulatif et progression sont ceux du produit |
| Périmètre | installation, maintenance (mise à jour, réparation, gestion des composants, espace de travail) et désinstallation (portées logiciel / configuration / espace de travail) |
| Commandes | `--demo`, `--demo-scenario`, `--check --demo` ; le reste des options est décrit dans [`COMMANDES.md`](COMMANDES.md) |

### Lancer depuis les sources

Prérequis : SDK .NET 10 (version épinglée dans `installer/global.json`).

```bash
# Lancement normal de l'assistant, depuis installer/
cd installer
dotnet run --project Setup.App

# Lancement en mode fictif (aucune installation)
dotnet run --project Setup.App -- --demo

# Variantes de scénario
dotnet run --project Setup.App -- --demo --demo-scenario=existing
dotnet run --project Setup.App -- --demo --demo-scenario=prism
dotnet run --project Setup.App -- --demo --demo-scenario=failure
```

Les mêmes commandes depuis la racine du dépôt :

```bash
dotnet run --project installer/Setup.App
dotnet run --project installer/Setup.App -- --demo --demo-scenario=failure
```

Le `--` sépare les arguments de `dotnet run` de ceux de l'installateur : c'est
la forme à utiliser, la seule dont le comportement est stable d'une version de
SDK à l'autre. Le mode fictif est aussi accessible depuis l'écran d'accueil
lorsque l'exécutable tourne sans `distribution.json` à côté de lui.

Le monde fictif vit dans `Setup.Core.Simulation` (`DemoWorld`, `DemoScenario`,
`SimulationExecutor`) et la plateforme est simulée par `SimulatedIntegration` et
`InMemoryInstallationStore`. Il est couvert par les quatre projets de tests de
`installer/` (Unit, App, Integration, EndToEnd), dont un test qui vérifie qu'aucun
répertoire n'est créé pendant le parcours complet.

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
| Confiance | empreintes SHA-256, `SHA256SUMS`, attestations GitHub, Authenticode conditionnel | [ADR-007](adr/ADR-007-manifeste-de-distribution-et-confiance.md) |
| Acquisition | profil `core` (téléchargement explicite) ou `full` (hors ligne complet) | [ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md) |
| Association `.livexp` | ouverture des paquets de campagne par le Launcher, en HKCU/XDG | [ADR-009](adr/ADR-009-association-fichiers-livexp.md) |
| Release V1 | entrées PRISM/LDK produites hors CI, épinglées, composants publiés individuellement | [ADR-010](adr/ADR-010-production-hors-ci-prism-et-composants-individuels.md) |

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
| 29 octobre 2026 | État d'implémentation, §5 bis (mode démonstration), ADR-009 | L'installateur existe en code ; l'association `.livexp` est décidée |
| 29 octobre 2026 | §2 : la signature n'est pas vérifiée cryptographiquement | Constat de revue, tracé en INS-L-11 |
| 29 octobre 2026 | Référence [`COMMANDES.md`](COMMANDES.md), statut d'implémentation, §5 bis complété, lancement `dotnet run` documenté | Les commandes sont implémentées et vérifiées |
| 29 octobre 2026 | Confiance V1 (empreintes + sommes + attestations), ADR-010, état de release | Plan d'alignement V1 : D3, ADR-010 |
