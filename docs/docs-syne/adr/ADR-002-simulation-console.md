# ADR-002 : Simulation.Console — un exécutable dédié

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../adr/ADR-001-separation.md`
**Source Monographie** : Annexe F.3 (ADR-002)

---

## Contexte

L'exécution du cœur du moteur doit être possible sans interface graphique, pour le débogage, les tests automatisés, le profiling et les validations de reproductibilité.

## Décision

`Simulation.Console` est un **exécutable .NET** qui :

- Lit un fichier de configuration (`simulation_config.json`) ;
- Initialise un `SimulationEngine` ;
- Exécute le tick suivant en boucle (mode tick-to-tick) ;
- Affiche les statistiques (nombre d'entités actives, durée du tick) ;
- Permet d'arrêter proprement le tick (Ctrl+C) ;
- Supporte les flags CLI (`--headless`, `--world-size`, `--seed`, `--max-ticks`, `--config`).

## Conséquences

### Positives
- Pas de dépendance UI dans le moteur.
- Compatible avec un exécutable Python (simulation_cli.py) pour analyse statistique.
- Testable via `Simulation.Console.Tests`.

### Négatives
- Un projet de plus à maintenir.

### Risques
- La parité avec la bibliothèque Python d'analyse doit rester délibérée (pas de duplication du cœur).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |