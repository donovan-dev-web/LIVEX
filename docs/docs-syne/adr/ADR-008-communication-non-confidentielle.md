# ADR-008 : Communication non confidentielle

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.9 (ADR-008), §3.16

---

## Contexte

Pour simplifier la communication, toutes les entités peuvent théoriquement percevoir les messages. Mais cela crée un « espace public » déréglé s'il n'est pas borné spatialement.

## Décision

La communication est **locale** (rayon limité, pulsations lumineuses) mais **non confidentielle**. Toutes les entités dans le rayon peuvent écouter. Le **secret est écarté** pour favoriser l'observation, les alliances (tacites/explicites) et la tromperie.

## Conséquences

### Positives
- La tromperie est possible (diffuser de faux messages).
- La surveillance est facile (un tiers peut écouter).
- Pas de canaux privés → modèle simple et observable.

### Négatives
- Les entités ne peuvent pas communiquer en privé (limitation assumée).

### Risques
- **Interception** (décision n°8) : possible par principe, mais la documentation et le code ne doivent pas traiter l'interception comme une règle tant que la décision n'est pas tranchée.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |