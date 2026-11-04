# Documentation LIVEX

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 31 octobre 2026
**Dépend de** : `ARCHITECTURE.md`, `NAMING_CONVENTIONS.md`

---

Cette page distingue les documents de référence actuels des spécifications
historiques conservées pour leur valeur d'archive.

## Structure

```text
docs/
├── captures/         # Captures d'écran de présentation
├── codebase/         # Cartographie interactive des fichiers, symboles et relations
├── presentation/     # Vision, FAQ, glossaire, périmètre éthique
├── governance/       # Feuille de route, état du dépôt, gitflow, plan de refonte
├── adr/              # ADR transverses (filtre : Accepted + implémenté)
├── syne/  echos/  launcher/  prism/   # Documentation par composant
├── website/          # Site vitrine : architecture, pages, design, i18n, déploiement
└── installer/        # Installateur V1, code, tests, distributions et documentation
```

Les fichiers techniques transverses (`ARCHITECTURE.md`, `COMMUNICATION.md`,
`INSTALLATION.md`, `CI_CD.md`, `NAMING_CONVENTIONS.md`) vivent à la racine de
`docs/`. Les fichiers de gouvernance de haut niveau (CHANGELOG, VERSIONING,
CONTRIBUTING…) restent à la racine du dépôt.

## Vue générale

- [Cartographie interactive du codebase](codebase/index.html) — exploration interactive
  des composants, fichiers, déclarations, paramètres et relations du dépôt, avec
  une vue en plan 2D et une exploration par voisinage.
- [Parcours interactif d'un run](codebase/flow.html) — machine à états 2D animée
  du lancement à la boucle cognitive répétée, avec les cadences et l'observabilité.
- [Architecture globale](ARCHITECTURE.md) — responsabilités des composants et
  cible d'architecture V2.
- [Contrats de communication](COMMUNICATION.md) — transports et cycle de
  contrôle.
- [Installation et démarrage](INSTALLATION.md) — pile SYNE/ECHOS, mock et
  projets Unreal.
- [Vision](presentation/VISION.md) · [Feuille de route](governance/ROADMAP.md) ·
  [Glossaire](presentation/GLOSSARY.md).
- [Captures](captures/README.md) — captures d'écran de présentation.
- [Convention de nommage](NAMING_CONVENTIONS.md) — identifiants en anglais,
  commentaires en français, règles par langage (C#, Python, JS, C++/Unreal) et
  structure des commentaires dans le code.

## Documentation par composant

| Documentation | Contenu |
|---|---|
| [SYNE](syne/README.md) | Moteur autoritaire, simulation, configuration et contrats de données. |
| [ECHOS](echos/README.md) | Ingestion, analyse, stockage et API headless (interface retirée — ADR-007). |
| [PRISM](prism/README.md) | Refonte V2 : monde voxel autoritaire, noyau .NET headless, contrat SYNE↔PRISM et visualiseur facultatif. |
| [Launcher](launcher/README.md) | Orchestrateur de la pile : matrice des modes, campagnes, supervision à six niveaux, réseau et Gateway, flux de données et format de paquet `.livexp`. |
| [Installateur](installer/README.md) | Installateur LIVEX 1.0.0 : parcours graphique et CLI, installation Windows/Linux, maintenance et désinstallation. |
| [syne-mock](../syne-mock/README.md) | Serveur Node.js de développement, ses scénarios, contrats et limites. |

## Site vitrine

Le site public (https://donovan-dev-web.github.io/LIVEX/) présente la release LIVEX 1.0.0
et la cible V2 en préparation, dont la refonte de PRISM. C'est un site statique
Astro documenté dans [`website/`](website/README.md) : architecture, pages,
design system, composants, multilinguisme FR/EN, données éditoriales, SEO,
accessibilité, déploiement et maintenance.

## LIVEX, PRISM V1 et cible PRISM V2

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM V1** est le projet Unreal livré avec la release 1.0.0 ; il
intègre **PRISM-LDK** (*LIVEX Development Kit*), le plugin Unreal historique.
Cette architecture reste la référence pour la V1 et ses archives.

La **cible PRISM V2** remplace cette architecture par un noyau .NET headless
qui détient la vérité du monde matériel et spatial. SYNE conserve la cognition
des agents ; ECHOS observe les flux. Le visualiseur prévu utilise Silk.NET et
OpenGL, mais reste un client facultatif qui ne commande pas l'avancement du
monde. La V2 échange le contrat SYNE↔PRISM 1.0.0 : commandes, observations,
événements et flux de lecture.

Les documents de [`prism/`](prism/README.md) décrivent la cible V2 complète.
Ils ne déclarent pas que tous ses éléments sont déjà livrés. Les constats de
réalisation seront actualisés avant toute publication de la documentation.

## Sources de vérité et limites du mock

- Pour la cible V2, PRISM et ses spécifications portent l'autorité du monde
  physique ; SYNE porte la cognition et `prism/API_CONTRACTS.md` définit le
  contrat visé entre eux.
- Les contrats V1 de SYNE restent applicables à la release 1.0.0 et aux
  documents explicitement historiques.
- `../syne-mock/` émule le transport et une partie du comportement pour permettre
  l'intégration V1 sans lancer le moteur complet. Il ne reproduit pas
  fidèlement les décisions, le pathfinding ni tous les systèmes sociaux de
  SYNE. Il ne doit pas servir à produire ou valider des résultats scientifiques.
- En cas de divergence entre un document général et le contrat du composant,
  la documentation détaillée et l'implémentation testée du composant priment.

## Archives

La [Monographie](../LIVEX-Monographie_SnapV0-1.md) décrit des étapes de
conception antérieures. Les références à Godot, aux anciens répertoires ou aux
moteurs graphiques encore « ouverts » dans cette archive ne décrivent pas la
réalisation V1. Les ADR d'origine restent consultables comme historique ; les
schémas V2 prévus sont dans les documents de contrat PRISM.
