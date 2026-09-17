# ADR-009 : Énergie comme monnaie d'action

**Composant** : SYNE
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.10 (ADR-009)

---

## Contexte

Les actions doivent avoir un coût pour éviter les comportements dégénératifs (actions gratuites et infinies).

## Décision

**Chaque action** (déplacement, communication, combat) **consomme de l'énergie**. L'énergie se **régénère lentement**. Si l'énergie est épuisée, l'entité ne peut plus agir.

## Conséquences

### Positives
- Les entités doivent prioriser leurs actions.
- L'exploration est limitée.
- Les stratégies émergent en réponse à la contrainte énergétique.

### Négatives
- Coûts à calibrer (valeurs [HÉRITÉ] du prototype à réévaluer).

### Risques
- Des coûts mal calibrés peuvent figer le monde (sur-estimation) ou rendre la contrainte décorative (sous-estimation) — supervision par métriques ECHOS.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |