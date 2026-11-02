# ADR-010 : Eau statique, gabarits de construction, conservation de la matière

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004, ADR-006
**Source Monographie** : —

---

## Contexte

Trois décisions bornent la complexité du monde pour que la V2 soit finissable et vérifiable.

## Décision

1. **Eau : bloc source statique**, sans écoulement. L'écoulement ajoute une règle coûteuse et difficile à rendre déterministe ; il reste une extension ultérieure. Une source est **inépuisable par déclaration** (propriété de matériau `infinite`), seule exception documentée à la conservation avec les rendements de récolte.
2. **Construction par gabarits** (plans prédéfinis en données : abri 3 × 3, puits, chemin, grenier, pont, escalier). Les agents décident *quel* gabarit, *où* et *quand* ; pas de dessin bloc par bloc.
3. **Conservation de la matière** : aucun objet n'est créé sans règle déclarée (recette, minage, récolte, générateur), aucun n'est détruit sans règle (consommation, usure, péremption). Un **registre d'audit** par `ruleId` vérifie l'invariant à intervalle régulier en build de test.

## Conséquences

### Positives
- Espace d'actions borné ; planification traitable.
- Invariant testable qui détecte les bugs de duplication tôt.

### Négatives
- Pas d'irrigation par canaux ni d'inondation ; la proximité d'eau remplace l'écoulement.
- Catalogue de gabarits à enrichir pour de la variété.

## Alternatives considérées

- **Automate cellulaire pour l'eau** : repoussé (coût, déterminisme).
- **Construction libre** : écartée (explosion de l'espace d'actions).

## Validation / rejet

Validé au jalon G4 (audit de conservation vert sur toutes les chaînes). L'eau dynamique pourra être rouverte après G7.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
