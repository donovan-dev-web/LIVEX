# ADR-004 : Refonte de PRISM — monde voxel autoritaire, SYNE allégé

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-002 (Superseded), ADR-003 (Superseded), `../../syne/adr/ADR-001-separation.md`, `../../launcher/adr/ADR-002-modes-analyse-et-immersion-de-poids-egal.md`
**Source Monographie** : —

---

## Contexte

PRISM V1 est un projet Unreal qui rend une représentation logique 2D portée par SYNE (ADR-002). SYNE détient donc le monde : grille, obstacles disques, réserves globales, saisons, territoires, navigation A*, ligne de vue, perception géométrique (46 fichiers du moteur touchent au monde, d'après un relevé grossier de `Simulation.Core`). Trois constats motivent la refonte :

1. Le monde logique est **pauvre** : deux besoins matériels satisfaits par des réserves globales, aucune matière à transformer ; la coopération n'a pas de raison structurelle.
2. SYNE cumule **cognition et monde**, alors que la cognition est son objet scientifique.
3. L'échelle et la cadence SYNE↔Unreal (ADR-003 : `k`, TPS 6, cellules World Partition) ont créé une dette de calibrage qui n'existe que parce que le monde vit dans un moteur de jeu qu'il ne contrôle pas.

## Décision

**PRISM devient un composant autonome qui porte l'état du monde, l'espace, la matière et le temps physique, sous forme d'un monde voxel borné décrit par des données. SYNE ne gère plus que les agents (cognition, besoins, mémoire, relations).**

Éléments structurants :

- Les agents expriment des **intentions** (commandes, macro-actions) ; PRISM les exécute sur plusieurs ticks et renvoie **observations** et **événements** (ADR-008).
- PRISM est **headless par défaut** ; le rendu est un client de lecture (ADR-005).
- Le monde est une grille 256 × 256 × 64, chunks 16³, bloc de 1 m (ADR-006).
- PRISM garantit le déterminisme du monde par arithmétique entière et hasard à compteur (ADR-007).
- Les contenus (matériaux, objets, recettes, gabarits) sont des données (ADR-006, ADR-010).
- Le projet Unreal PRISM et le plugin PRISM-LDK sont **archivés** ; ils restent la ligne `1.x`.

## Conséquences

### Positives
- SYNE se recentre ; ses budgets de tick ne portent plus la géométrie.
- Plus d'échelle moteur : un bloc = un mètre, un tick = un tick ; suppression de ADR-003.
- Un monde riche rend la coopération testable par ablation.

### Négatives
- Rupture de contrat SYNE↔PRISM : SYNE `0.18.0`, contrat `0.6.0` ; ECHOS et Launcher à adapter.
- Les calibrages ADR-014/015/016 (survie, stabilité 2500 ticks) sont invalidés et à refaire.
- Un composant de plus à exécuter à chaque run (PRISM headless est requis, plus seulement en mode Immersion).
- PRISM-LDK, 63 Automation Specs et le travail Blueprint ne sont plus réutilisables que comme référence de contrat.

### Risques
- Coût du pathfinding 3D et du volume d'observations à 1 000 agents → repli 2.5D (ROADMAP §8, porte G1).
- La coopération peut ne **pas** émerger : le résultat négatif est publiable (VISION §5).

## Alternatives considérées

- **Garder Unreal et enrichir la représentation logique** : refusé, ne résout ni la pauvreté du monde ni la séparation des responsabilités.
- **Monde riche mais toujours dans SYNE** : refusé, aggrave la charge de SYNE et sa dette de calibrage.
- **Monde 2D enrichi sans voxel** : écarté comme choix principal, retenu comme **plan de repli** (grille 2.5D) car le modèle de contenu est indépendant de la géométrie.

## Validation / rejet

Validé au jalon G5 (run complet SYNE + PRISM sans classe de monde dans `Simulation.Core`). Réouvert si G1 échoue (budget de performance) ou si G7 réfute l'intérêt du scope.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
