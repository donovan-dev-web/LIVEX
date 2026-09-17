# Boucle de simulation

## 1. Tick

Un tick représente une unité de temps simulé.

## 2. Ordre V1

```text
1. Advance simulation time
2. Update world
3. Update agent physiology
4. Generate/update perceptions
5. Update memories
6. Evaluate needs
7. Evaluate decisions
8. Execute actions
9. Apply world changes
10. Emit events
11. Validate state
```

## 3. Causalité

L'ordre doit être documenté et stable.

Exemple :

```text
Agent boit
    -> thirst diminue
    -> event DrinkCompleted
    -> état modifié
    -> prochaine décision
```

## 4. Fréquence

La fréquence réelle sera configurable.

Exemple de départ :

```text
10 ticks / seconde
```

Cette valeur est expérimentale.

## 5. Temps accéléré

Le moteur doit pouvoir fonctionner plus vite que le temps réel.

Valeur V1 (cohérente avec la configuration `targetTicksPerSecond = 10` et `simulatedMinutesPerTick = 1`) :

```text
1 seconde réelle = 10 minutes simulées
```

soit `10 ticks / seconde`. Cette valeur est expérimentale et configurable.

## 6. Pause

Le moteur doit supporter :

- pause
- reprise
- step d'un tick
- changement de vitesse

## 7. Reproductibilité

Chaque expérience doit pouvoir être associée à :

- seed
- configuration
- version du moteur
- état initial
