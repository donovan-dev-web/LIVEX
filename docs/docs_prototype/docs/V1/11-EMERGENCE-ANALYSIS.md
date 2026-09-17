# Analyse des comportements émergents

## 1. Objectif

Ne pas se contenter d'observer visuellement la simulation.

Le projet doit pouvoir mesurer les phénomènes produits.

## 2. Catégories de mesures V1

### Population

- population totale
- naissances futures
- morts
- durée de vie

### Ressources

- consommation
- disponibilité
- concentration
- épuisement

### Spatial

- densité d'agents
- distance moyenne entre agents
- concentration autour des ressources
- déplacements

### Comportement

- fréquence des actions
- durée des actions
- interruptions
- changements de décision

## 3. Émergence

Un comportement n'est pas automatiquement émergent parce qu'il est intéressant.

Il faut pouvoir établir :

1. Il n'est pas explicitement imposé comme objectif global.
2. Il apparaît à partir des règles locales.
3. Il persiste suffisamment longtemps.
4. Il peut être mesuré.
5. Il peut être reproduit ou étudié sur plusieurs runs.

## 4. Expériences

Chaque expérience doit enregistrer :

```text
Seed
Configuration
Population
Durée
Version du moteur
Résultats
```

## 5. Comparaison

Pour étudier un comportement :

```text
Run A
Run B
Run C
...
```

avec un paramètre modifié.

Exemple :

```text
sociability = 0.2
vs
sociability = 0.8
```

On compare ensuite les structures produites.

## 6. Analyzer

L'Analyzer est un service **C#/.NET** (Phase 5, dossier `analyzer/`), dans la
continuité du mono-repo 100 % .NET. Il s'abonne au WebSocket de simulation
(cf. `docs/V1/09-EVENTS-API.md`), calcule les mesures des §2–§3 et les expose via
une API REST consommée par le Web UI (Phase 6).

Périmètre fonctionnel (réalisable en C#/.NET, éventuellement aidé par des
bibliothèques de calcul) :

- statistiques (moyennes, distributions, corrélations)
- analyse de réseaux sociaux (graphe des liens/confiance entre agents)
- clustering (concentrations spatiales, communautés)
- détection de tendances (séries temporelles)
- comparaison de simulations (runs, seeds, configs)

Python reste une alternative envisageable **uniquement** pour les calculs
scientifiques lourds, mais n'est pas retenu par défaut (cohérence de toolchain).
