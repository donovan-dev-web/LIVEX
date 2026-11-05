# Documentation LIVEX

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 4 novembre 2026
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
| [SYNE](syne/README.md) | Moteur autonome V1, cognition couplée V2, simulation, configuration et contrats. |
| [ECHOS](echos/README.md) | Analyse headless, ingestion SYNE et persistance brute partielle du flux PRISM V2. |
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

## LIVEX, PRISM V1 et PRISM V2 en cours

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM V1** est le projet Unreal livré avec la release 1.0.0 ; il
intègre **PRISM-LDK** (*LIVEX Development Kit*), le plugin Unreal historique.
Cette architecture reste la référence pour la V1 et ses archives.

**PRISM V2** remplace l'architecture V1 par un noyau .NET headless qui porte
l'état physique du monde. Son host exécute déjà le tick, les actions et les
chaînes de contenu ; son contrat `1.0.0` échange commandes, observations,
événements et flux de lecture. La boucle couplée avec SYNE applique les
observations aux croyances et a été exercée sur 1 000 ticks. L'extraction
complète de l'ancien monde SYNE, la reprise couplée et le run R2 de 5 000 ticks
restent à faire. Le viewer Silk.NET/OpenGL est un client facultatif ; son
client de flux est implémenté mais pas encore raccordé au lancement live.

Les documents de [`prism/`](prism/README.md) décrivent les fonctions V2
implémentées et les travaux restant à livrer. Pour l'état transversal courant,
consulter [`governance/LIVEX-CURRENT-STATUS.md`](governance/LIVEX-CURRENT-STATUS.md).
Le relevé détaillé [`governance/LIVEX-STATUS.md`](governance/LIVEX-STATUS.md)
reste la photographie de la release V1 au 31 octobre.

## Sources de vérité et limites du mock

- En V2, PRISM porte l'autorité du monde physique ; SYNE porte la cognition,
  tandis que l'extraction des systèmes physiques encore présents dans SYNE
  demeure incomplète. `prism/API_CONTRACTS.md` décrit le contrat `1.0.0` gelé.
- Les contrats V1 de SYNE restent applicables à la release 1.0.0 et aux
  documents explicitement historiques.
- `../syne-mock/` émule le transport et une partie du comportement pour permettre
  l'intégration V1 sans lancer le moteur complet. Il ne reproduit pas
  fidèlement les décisions, le pathfinding ni tous les systèmes sociaux de
  SYNE. Il ne doit pas servir à produire ou valider des résultats scientifiques.
- En cas de divergence entre un document général et le contrat du composant,
  la documentation détaillée et l'implémentation testée du composant priment.

## Archives

La monographie est en cours de réalignement sur PRISM V2. Les chapitres V1
conservés doivent être identifiés explicitement comme historiques ; ils ne
décrivent pas l'architecture actuelle. Les ADR d'origine restent consultables
comme historique.
