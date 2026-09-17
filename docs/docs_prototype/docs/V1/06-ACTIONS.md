# Système d'actions

## 1. Principe

Les actions sont des capacités exécutables.

Le système de décision choisit une action ; le système d'action l'exécute.

## 2. États

Une action peut avoir :

```text
Pending
Executing
Completed
Cancelled
Failed
```

## 3. Actions V1

### MoveTo

Paramètres :

- position cible
- entité cible optionnelle

Résultat :

- déplacement de l'agent

### Eat

Paramètres :

- source de nourriture

Effets :

- quantité de nourriture diminuée
- faim diminuée

### Drink

Paramètres :

- source d'eau

Effets :

- soif diminuée

### Rest

Effets :

- energy augmentée
- durée simulée

### Explore

Effets :

- choix d'une destination
- déplacement
- découverte potentielle de nouvelles entités

### Gather

Collecte une ressource pour la stocker (inventaire minimal V1).

### Talk

Crée une interaction sociale avec un agent perçu à portée ; influence la mémoire sociale et les scores futurs via le trait `Sociability`.

### Attack

Modifie l'état d'un autre agent selon les règles de combat minimales V1 (réduit la santé/énergie de la cible proportionnellement au trait `Aggression`).

### Flee

Cherche une position plus sûre en s'éloignant d'un danger perçu ; favorisée par un `SafetyNeed` élevé.

Les neuf actions (MoveTo, Eat, Drink, Rest, Explore, Gather, Talk, Attack, Flee) sont disponibles en V1. Voir `docs/V1/03-V1-SPECIFICATION.md` §10 pour les préconditions et effets détaillés.

## 5. Interruption

Toute action longue doit pouvoir être annulée.

Causes possibles :

- danger
- cible disparue
- ressource épuisée
- précondition devenue fausse
- nouvelle priorité critique

## 6. Action et représentation

Une action ne contient pas d'animation.

Elle expose son état.

Exemple :

```text
Action = MoveTo
Target = Food_12
Progress = 0.65
```

Le renderer décide comment représenter cela.
