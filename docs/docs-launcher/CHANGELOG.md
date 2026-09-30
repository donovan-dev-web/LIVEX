# CHANGELOG — LAUNCHER

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versionnement :
SemVer (`launcher-vX.Y.Z`).

Le Launcher n'a pas encore de version livrée. Le format, les contrats et
l'architecture sont spécifiés ; l'implémentation n'a pas commencé.

## [Unreleased]

### Added
- **Jeu de documentation du composant** — `docs/docs-launcher/` : vision,
  architecture, modèle de composants, contrat d'intégration, format de paquet,
  campagnes, observabilité, réseau, interface, empaquetage, tests, feuille de route,
  points ouverts, journal des modifications et six ADR.
- **Format de paquet `.livexp`** — format unique, vivant pendant la campagne puis
  scellé, avec `manifest.json` comme point de commit, `runs/index.json` comme source
  de reprise, périmètres d'écriture disjoints, versionnage par `schema` et
  déterminisme octet pour octet des paquets scellés.
- **Modèle de composants** — manifeste déclaratif, machine à états explicite,
  registre de session, sélection par profil, détection et adoption.
- **Trois modes d'utilisation** — mode Contrôle comme socle, mode Analyse porté par
  ECHOS, mode Immersion porté par PRISM, de poids égal et sélectionnables.
- **Exigences d'intégration** — `INTEGRATION_CONTRACT.md` énonce ce que chaque
  composant doit exposer pour être orchestrable, sans chemin de contournement.
- **Conception du mode Immersion** — PRISM est conçu au niveau du modèle, avec ses
  exigences, sa vue et ses tests ; son accès reste verrouillé tant qu'il n'est pas
  implémenté.
- **Six ADR** — pile technique .NET et Avalonia, égalité des deux modes, propriété
  de l'analyse par ECHOS, format de paquet, exécution séquentielle et mono-espace,
  verrouillage de PRISM.

### Changed
- **Réécriture complète de la documentation du Launcher** — le monolithe
  le monolithe `LIVEX_Launcher.md` et les six documents annexes de l'ancien
  répertoire de brouillon sont remplacés par le présent jeu de documents.
- **Suppression des documents de comparaison de version** — l'historique de révision
  est désormais porté par ce journal, sans appareil de comparaison dans les
  documents de spécification.
- **Suppression du document d'auto-analyse** — la critique du brouillon est
  absorbée par les sections « Alternatives considérées » des ADR et par `ISSUES.md`.

### Removed
- **Format de paquet précédent** — la notion de paquet, absente du projet, est
  remplacée par le format `.livexp` unique.
- **Modèle de ports à entier global** — le versionnage suit `../../VERSIONING.md`,
  semver par composant, sans numéro de protocole propre au Launcher.
- **Logique de comparaison multi-runs dans le Launcher** — l'analyse appartient à
  ECHOS ; le Launcher ne calcule aucune statistique.

## [0.0.0] — Non publié

- Aucune version publiée.
