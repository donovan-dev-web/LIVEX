# Note — Couche Affective Transitoire (États Émotionnels)

**Statut :** [OUVERT] — piste notée pour mémoire, non détaillée en ADR
**Portée potentielle :** Système de Besoins (§3.12), Système de Décision (§3.14), Système de Mémoire (§3.10)
**Document parent :** LIVEX — Monographie Générale (Partie 3)

---

## Constat

Deux niveaux de « personnalité » existent déjà dans LIVEX :
- **Statique** : les 8 traits de personnalité (§3.7.4), fixes pour la durée de vie d'une entité (héritables, §6.6.3).
- **Dynamique lent** : les 6 besoins (§3.12), qui varient tick par tick selon des règles régulières.

Il manque un niveau intermédiaire : un **état affectif transitoire**, qui module temporairement la perception du risque ou l'urgence à la suite d'un événement marquant récent — sans être ni un trait figé, ni un besoin physiologique régulier. Exemple : une entité qui vient de subir une attaque devrait, pendant une fenêtre de temps limitée, évaluer le monde comme plus dangereux qu'une autre entité au même niveau de besoin de Sécurité (§3.12.1), indépendamment de toute nouvelle menace réelle perçue.

## Pourquoi c'est pertinent

- Peu coûteux architecturalement comparé aux autres pistes (pas de nouveau système, une extension de mécanismes existants).
- Cohérent avec la discipline du déterminisme (§3.25) : un multiplicateur numérique borné dans le temps, pas un système neuronal.
- Lien naturel avec la mémoire : un souvenir à forte charge émotionnelle pourrait décroître plus lentement qu'un souvenir neutre — le mécanisme de décroissance exponentielle existe déjà (§3.10.3, `salience(t) = salience(0) × exp(-decayRate × Δtick)`) ; il suffirait de moduler `decayRate` par un facteur émotionnel au moment de l'enregistrement, sans changer la formule elle-même.

## Esquisse de mécanisme (non détaillée)

```
AffectiveState:
  fear: float [0-1], decroît vers 0 avec le temps (demi-vie configurable)
  triggeredBy: eventId / observationId
  appliedModifiers:
    - Risk *= (1 + fear × poids)         // dans le calcul d'utilité, §3.14.5
    - decayRate(mémoire associée) *= (1 - fear × poids)  // §3.10.3
```

Un seul affect (peur/alerte) suffirait comme première itération — pas besoin d'un catalogue émotionnel complet pour valider l'intérêt du mécanisme.

## Points à trancher (si formalisé plus tard)

1. Quels événements déclenchent un état affectif (probablement liés aux mêmes observations saillantes que l'ADR Politique de Reconsidération) ?
2. Demi-vie de l'état affectif — distincte de la décroissance mémoire, à calibrer séparément.
3. Un seul affect (peur) ou plusieurs dès le départ (peur, confiance accrue, frustration) ?
4. Interaction avec `PersonalityMod` (§3.14.8) — une entité avec un trait de témérité élevé devrait amortir l'effet de la peur, pas l'ignorer.

## Statut

Non prioritaire par rapport aux 6 ADR déjà formalisées — noté ici pour ne pas perdre l'idée, à détailler en ADR complète si le projet en vient à vouloir enrichir la crédibilité comportementale au-delà de la coordination sociale déjà couverte.
