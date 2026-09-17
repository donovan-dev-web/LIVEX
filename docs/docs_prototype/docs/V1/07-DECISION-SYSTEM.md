# Système de décision

## 1. Objectif

Choisir l'action la plus pertinente à partir de la situation courante.

## 2. V1 : Utility AI

Chaque action candidate reçoit un score.

```text
score(action, agent, context)
```

Le score dépend de facteurs tels que :

- intensité du besoin
- distance
- danger (DangerUtility : favorise Flee quand un danger est perçu, cf. SafetyNeed)
- traits
- mémoire
- disponibilité de la cible
- coût de l'action

Les besoins normalisés incluent `SafetyNeed`, dérivé de la perception d'un agent menaçant à proximité ou d'un état critique. `Flee` devient dominante quand `SafetyNeed` est élevé ; `Attack` est favorisée par un trait `Aggression` élevé en contexte de compétition. Voir `docs/V1/03-V1-SPECIFICATION.md` §7 et §12.

## 3. Exemple

```text
Hunger = 90

Food à 5m
Water à 30m
Energy = 70
```

Scores possibles :

```text
Eat       0.90
Drink     0.25
Rest      0.20
Explore   0.10
```

Le système choisit Eat ou une action permettant d'atteindre la nourriture.

### 3.1 Exemple avec danger

```text
Hunger = 40
Agent menaçant (aggression élevée) à 8m
SafetyNeed = 0.85
```

Scores possibles :

```text
Flee      0.88
Eat       0.30
Drink     0.10
Rest      0.05
Explore   0.02
```

Le système choisit Flee malgré un besoin de nourriture modéré.

## 4. Important

La décision ne doit pas contenir :

```text
if hunger > 80 then Eat
```

comme unique mécanisme.

La décision doit permettre à plusieurs actions d'être évaluées simultanément.

## 5. Variabilité

Deux agents peuvent obtenir des scores différents avec le même contexte grâce à leurs traits, mémoire et état.

Une petite part de bruit contrôlé peut être introduite plus tard pour éviter un comportement parfaitement déterministe.

## 6. Hiérarchie

V1 :

```text
Needs
  -> Action Utility
  -> Best Action
```

Plus tard :

```text
Needs
  -> Goals
  -> Plans
  -> Actions
```

Les objectifs complexes sont hors périmètre V1.
