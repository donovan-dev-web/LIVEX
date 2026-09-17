# ADR-005 : Temporalité — tick = minute

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.6 (ADR-005)

---

## Contexte

Le temps simulé doit avoir un sens pour l'interprétation des besoins, de l'énergie et des ressources.

## Décision

**1 tick = 1 minute de temps simulé** (cycle de 24 heures = 1440 ticks). Réglage **[HÉRITÉ]** du prototype, **paramétrable** (décision n°1 « unité de temps »). L'échelle de temps est contrôlée par la boucle de simulation.

## Conséquences

### Positives
- Les besoins sont mesurés en unités de temps (ex. +1 soif/tick).
- Le renderer peut convertir tick en heures de la journée.
- La vitesse de simulation est un paramètre de débogage (10 t/s par défaut).

### Négatives
- Toute modification d'unité de temps impacte les taux de besoins (paramétriques).

### Risques
- Confusion entre temps réel et temps simulé dans les UIs — rendre l'échelle toujours visible.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |