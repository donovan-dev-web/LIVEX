# ADR-010 : Abandon de la morphologie physique

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.11 (ADR-010)

---

## Contexte

La simulation physique de la morphologie (taille, forme, vitesse) ajoute une complexité massive sans bénéfice pour l'émergence de haut niveau.

## Décision

Les entités sont des **entités logiques**. Leur « corps » est un ensemble de propriétés (position, vitesse, santé) **sans représentation physique détaillée**.

## Conséquences

### Positives
- Complexité réduite (pas de collisions physiques).
- La mobilité est un paramètre numérique (coût d'action).

### Négatives
- **Pas de collisions physiques** : les entités peuvent se chevaucher.
- Le renderer doit utiliser un modèle standard pour toutes les entités (PRISM).

### Risques
- Le chevauchement peut sembler irréaliste dans le rendu — compensé par une présentation stylisée.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |
| 24 septembre 2026 | Guide de migration V0.1 publié dans `../MIGRATION_MORPHOLOGY.md` (SYNE-130) | Formaliser les champs conservés et retirés |