# Documentation LIVEX

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 12 octobre 2026
**Dépend de** : `ARCHITECTURE.md`, `NAMING_CONVENTIONS.md`

---

Cette page distingue les documents de référence actuels des spécifications
historiques conservées pour leur valeur d'archive.

## Structure

```text
docs/
├── presentation/     # Vision, FAQ, glossaire, périmètre éthique
├── governance/       # Feuille de route, état du dépôt, gitflow, plan de refonte
├── adr/              # ADR transverses (filtre : Accepted + implémenté)
├── syne/  echos/  launcher/  prism/   # Documentation par composant
└── installer/        # Spécification seule — aucun code à ce jour
```

Les fichiers techniques transverses (`ARCHITECTURE.md`, `COMMUNICATION.md`,
`INSTALLATION.md`, `CI_CD.md`, `NAMING_CONVENTIONS.md`) vivent à la racine de
`docs/`. Les fichiers de gouvernance de haut niveau (CHANGELOG, VERSIONING,
CONTRIBUTING…) restent à la racine du dépôt.

## Vue générale

- [Architecture globale](ARCHITECTURE.md) — responsabilités des composants,
  plugin PRISM, LDK et projets Unreal.
- [Contrats de communication](COMMUNICATION.md) — transports et cycle de
  contrôle.
- [Installation et démarrage](INSTALLATION.md) — pile SYNE/ECHOS, mock et
  projets Unreal.
- [Vision](presentation/VISION.md) · [Feuille de route](governance/ROADMAP.md) ·
  [Glossaire](presentation/GLOSSARY.md).
- [Convention de nommage](NAMING_CONVENTIONS.md) — identifiants en anglais,
  commentaires en français, règles par langage (C#, Python, JS, C++/Unreal) et
  structure des commentaires dans le code.

## Documentation par composant

| Documentation | Contenu |
|---|---|
| [SYNE](syne/README.md) | Moteur autoritaire, simulation, configuration et contrats de données. |
| [ECHOS](echos/README.md) | Ingestion, analyse, stockage et API headless (interface retirée — ADR-007). |
| [PRISM](prism/README.md) | Projet Unreal final de LIVEX et plugin PRISM-LDK (`PrismLdk`), API Blueprint et intégration. |
| [Launcher](launcher/README.md) | Orchestrateur de la pile : matrice des modes, campagnes, supervision à six niveaux, réseau et Gateway, flux de données et format de paquet `.livexp`. |
| [Installateur](installer/README.md) | **Spécification à venir** — aucun code implémenté ; description de l'assistant d'installation (jalon G6). |
| [syne-mock](../syne-mock/README.md) | Serveur Node.js de développement, ses scénarios, contrats et limites. |

## LIVEX, PRISM et PRISM-LDK

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM** est son projet Unreal final et **PRISM-LDK** (*LIVEX
Development Kit*) est le plugin Unreal intégré à PRISM (nom de module/dossier
actuel : `PrismLdk`). LDK désigne le plugin, pas un projet produit distinct.

Dans le checkout courant, `../prism/LDK/LDK.uproject` est l'hôte Unreal technique
fourni pour développer, compiler et tester le plugin. Son nom de fichier ne
signifie pas que LDK soit le projet complet ou l'application finale LIVEX.

Le code C++ de PRISM-LDK doit rester limité aux besoins d'intégration avec SYNE.
Les types, événements et contrôles de simulation sont exposés à Blueprint afin
de privilégier les systèmes natifs d'Unreal.

## Sources de vérité et limites du mock

- SYNE et ses tests sont la source de vérité du comportement de simulation.
- `syne/API_CONTRACTS.md` décrit le contrat courant échangé avec les
  clients.
- `../syne-mock/` émule le transport et une partie du comportement pour permettre
  l'intégration PRISM sans lancer le moteur complet. Il ne reproduit pas
  fidèlement les décisions, le pathfinding ni tous les systèmes sociaux de
  SYNE. Il ne doit pas servir à produire ou valider des résultats scientifiques.
- En cas de divergence entre un document général et le contrat du composant,
  la documentation détaillée et l'implémentation testée du composant priment.

## Archives

La [Monographie](../LIVEX-Monographie_SnapV0-1.md) décrit des étapes de
conception antérieures. Les références à Godot, aux anciens répertoires ou aux
moteurs graphiques encore « ouverts » dans cette archive ne décrivent pas la
réalisation PRISM actuelle. Les ADR d'origine
restent consultables comme historique ; les schémas courants sont dans les
documents de contrat.
