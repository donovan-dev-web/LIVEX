# ADR-003 : Runtimes embarqués plutôt que dépendances système

**Composant** : LIVEX (Installateur)
**Statut** : [Proposed]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `ADR-002-installation-par-composant-sans-elevation.md`
**Source Monographie** : —

---

## Contexte

La pile LIVEX s'appuie sur plusieurs technologies : .NET 10 (Launcher, SYNE),
Python 3.11+ avec FastAPI et `pyarrow` (ECHOS, `uv.lock` présent), Node.js 20+
(mock), Unreal Engine 5.8 (PRISM-LDK). `INSTALLATION.md` les liste comme
prérequis de développement. Pour un utilisateur final, exiger leur installation
préalable contredit deux principes : aucune élévation de privilèges et
installation en un parcours guidé. Le Launcher lui-même est décrit comme
n'ayant « aucune dépendance système » au-delà de la couche .NET
(`PACKAGING.md` §3).

L'installation de runtimes système modifie la machine (variables `PATH`,
versions concurrentes) et suppose souvent une élévation (gestionnaires de
paquets, installateurs officiels).

## Décision

| Dépendance | Décision |
| :-- | :-- |
| .NET | **Self-contained** pour le Launcher, SYNE et l'installateur ; le SDK n'est jamais installé par l'installateur |
| Python | **`uv` embarqué** (un binaire) installe un interpréteur géré dans `runtimes/` et synchronise l'environnement d'ECHOS depuis `uv.lock` ; version fixée avec la CI (OI-03) ; roues fournies dans le paquet hors-ligne |
| Node | **Runtime embarqué** dans le paquet du mock ; absent si le mock n'est pas choisi |
| Bibliothèques système Linux | **Détectées, jamais installées** ; liste et commande affichées |
| Unreal Engine | **Détecté, jamais installé** ; PRISM-LDK copié dans le projet désigné |

Aucun runtime n'est ajouté au `PATH` global ni partagé avec d'autres
applications.

## Conséquences

### Positives
- Installation reproductible, indépendante du système hôte.
- Aucune élévation requise ; désinstallation complète en retirant le répertoire.
- Les versions de runtime sont maîtrisées et testées en CI.
- ECHOS bénéficie de l'environnement figé d'après `uv.lock`.

### Négatives
- Taille d'installation plus élevée (runtimes embarqués) ; mitigée par les options.
- La CI doit produire et mettre à jour les runtimes embarqués.
- L'installation d'ECHOS suppose du réseau ou le paquet hors-ligne.
- Les mises à jour de sécurité des runtimes deviennent notre responsabilité.

### Risques
- Roue native (`pyarrow`) indisponible pour une combinaison OS/Python : ECHOS non
  installable (couvert par la version Python choisie et le paquet hors-ligne).
- Divergence entre l'environnement de développement (Python système, `dev-stack.sh`)
  et celui installé : à tester en CI.

## Alternatives considérées

- **Exiger les runtimes système** : plus léger, mais fragile (versions, `PATH`) et
  incompatible avec l'absence d'élévation → refusé pour l'utilisateur final, conservé
  pour le développement (`INSTALLATION.md`).
- **Installer les runtimes via un gestionnaire de paquets** (apt, winget) :
  élévation, comportement variable → refusé.
- **Empaqueter ECHOS en exécutable unique** (PyInstaller ou équivalent) : supprime
  Python mais complique les dépendances natives et le débogage → non retenu pour
  V0.1, à réévaluer.
- **Conteneurs** : étrangers à un poste de travail Windows sans configuration →
  refusé.

## Validation / rejet

- Validation : TS-01 sur machine propre sans .NET, Python ni Node ; ECHOS démarre
  avec l'interpréteur embarqué ; `--check` passe.
- Réouverture : si la taille cumulée devient dissuasive (mesurée par la CI), ou si
  une dépendance native impose un compilateur sur la machine cible.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création | Spécification de l'installateur |
