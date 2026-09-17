# Vision et principes

## 1. Vision

Créer un monde simulé persistant dans lequel des comportements complexes peuvent apparaître à partir de règles locales simples.

Le système ne doit pas être conçu autour d'un scénario narratif prédéfini.

## 2. Principe d'émergence

Un comportement global est considéré comme émergent lorsqu'il résulte des interactions répétées entre agents, environnement et ressources sans être explicitement codé comme comportement global.

Exemple :

```text
Soif
  -> recherche d'eau
  -> plusieurs agents trouvent la même source
  -> concentration locale
  -> interactions
  -> nouvelles décisions
  -> structure collective potentiellement persistante
```

Le système ne contient pas de règle « former un groupe autour de l'eau ».

## 3. Autonomie

Le moteur fournit aux agents :

- un état
- des besoins
- une perception
- une mémoire
- des capacités
- un système de décision
- des actions

Il ne fournit pas une liste de comportements narratifs imposés.

## 4. Déterminisme

La V1 doit autant que possible être reproductible avec une même seed et les mêmes paramètres.

Cela permet de comparer les expériences.

## 5. Découplage

Le Simulation Core ne doit dépendre :

- ni de Godot
- ni de Unity
- ni d'Unreal
- ni d'un renderer particulier.

Le renderer visualise l'état de la simulation.

## 6. Observabilité

La simulation doit produire suffisamment de données pour répondre à :

- Que fait chaque agent ?
- Pourquoi a-t-il choisi cette action ?
- Quelles ressources sont consommées ?
- Quels événements se produisent ?
- Quels comportements collectifs apparaissent ?

## 7. Persistance

Le monde doit pouvoir être sauvegardé et restauré sans perdre son état essentiel.
