# ADR-001 : Simulation.Core — le cœur du moteur

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.2 (ADR-001)

---

## Contexte

Le moteur de simulation doit être performant et flexible, isolé des détails d'affichage et de contrôle. Le prototype initial mélangeait l'exécution du tick et l'interface console, rendant difficile l'expérimentation et le débogage.

## Décision

Créer un **cœur de moteur** dans `Simulation.Core` qui contient :

- `SimulationEngine` : la boucle principale (1 tick = 1 pas)
- `Runtime` : l'état d'exécution (actifs, en attente, terminés)
- `World` : l'état logique du monde (position, taille, liste d'entités)
- `Agents/` : le comportement d'entité (AI, état, mémoire, cycles)
- `Interaction/` : l'interaction multi-agents (protocole de messages, conflit, relations)
- `Events/` : le bus d'événements et les handlers
- `Spatial/` : la structure spatiale (liste simple → future grille spatiale)
- `ECS/` : le module Entity Component System (architecture de données)
- `Persistence/` : la sérialisation de l'état

## Conséquences

### Positives
- Le cœur est testable indépendamment de l'interface.
- Le débogage est accéléré (pas d'attente pour l'interface).
- Un exécutable d'entrée (`Simulation.Console`) peut être lancé avec des paramètres.
- Le moteur peut être lancé dans une boucle de test automatisé.

### Négatives
- Séparation frontière additionnelle à maintenir.

### Risques
- La grille spatiale (Phase 9) doit remplacer `Spatial/` sans rupture de contrat (retour une interface `ISpatialGrid`).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |