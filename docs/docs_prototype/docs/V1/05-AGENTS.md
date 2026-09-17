# Spécification des agents

## 1. Agent

Un agent est une entité autonome.

```text
Agent
├── Identity
├── State
├── Needs
├── Perception
├── Memory
├── Capabilities
├── DecisionState
└── CurrentAction
```

## 2. Identity

```text
Id
Species
Name
Age
```

## 3. State

```text
Health
Energy
Hunger
Thirst
Position
```

## 4. Traits

```text
Aggression
Sociability
```

Les traits sont des facteurs de décision.

Ils ne contiennent pas de comportement codé.

## 5. Perception

Chaque perception produit des observations.

```text
Observation
├── EntityId
├── EntityType
├── Distance
├── Direction
└── KnownProperties
```

Une observation expire ou devient obsolète selon les règles de la mémoire.

## 6. Memory

La mémoire stocke des informations connues.

Une information mémorisée n'est pas nécessairement vraie au moment présent.

Cela permettra plus tard de simuler l'incertitude et les erreurs.

## 7. Capacités

Une capacité doit indiquer :

- ce que l'agent peut faire
- les préconditions
- les paramètres nécessaires

Exemple :

```text
Eat
Precondition:
    target is edible
    target is reachable
```

## 8. Cycle agent

```text
Observe
  -> Update memory
  -> Evaluate needs
  -> Evaluate actions
  -> Select action
  -> Execute
```
