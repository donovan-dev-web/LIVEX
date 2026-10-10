# ADR-005 : Temporalité — tick = minute

**Composant** : SYNE
**Statut** : [Historique — précisée par ADR-017]
**Dernière mise à jour** : 10 octobre 2026
**Dépend de** : ADR-017 (échelle temporelle configurable)
**Source Monographie** : Annexe F.6 (ADR-005)

---

## Contexte

Le temps simulé doit avoir un sens pour l'interprétation des besoins, de l'énergie et des ressources.

## Décision

**1 tick = 1 minute de temps simulé** (cycle de 24 heures = 1440 ticks). Réglage **[HÉRITÉ]** du prototype. L'échelle de temps est contrôlée par la boucle de simulation.

> **Précision ADR-017 (10 octobre 2026)** : la « paramétrabilité » affirmée ici n'existait **que dans la documentation** — le moteur portait une constante statique (`SimulationTime.TicksPerSimulationMinute = 1`). ADR-017 la rend réelle : `simulation.simulatedSecondsPerTick` (défaut 60 → comportement historique inchangé), horloge instanciée `SimulationClock` et règles de conversion A/B/C. Ce document reste la référence du **profil par défaut** ; l'échelle configurable vit dans ADR-017.

## Conséquences

### Positives
- Les besoins sont mesurés en unités de temps (ex. +1 soif/tick).
- Un client de présentation externe peut convertir le tick en heure du jour (PRISM est aujourd'hui un plugin Unreal, pas un moteur de rendu autonome).
- La vitesse de simulation est un paramètre de débogage (10 t/s par défaut).

### Négatives
- Toute modification d'unité de temps impacte les taux de besoins (paramétriques).

### Risques
- Confusion entre temps réel et temps simulé dans les UIs — rendre l'échelle toujours visible.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 10 octobre 2026 | Précision | La « paramétrabilité » était documentée mais non implémentée ; ADR-017 l'instancie (défaut 60 s/tick, neutre) |
| 17 septembre 2026 | Création | — |