---
title: "PRISM : le projet Unreal final et le plugin PRISM-LDK"
description: "PRISM, le projet Unreal final de LIVEX : le plugin PRISM-LDK apporte le rendu perceptif sans jamais décider. SYNE reste la seule autorité du monde simulé."
date: 2026-10-06
tags: ["prism", "unreal", "prism-ldk", "rendu", "immersion"]
draft: false
---

Vous ouvrez une scène Unreal. Un terrain se déploie, des agents circulent, s'arrêtent près d'un point d'eau, repartent. L'œil conclut immédiatement : « tout cela se passe dans Unreal ». C'est faux, et cette erreur est exactement ce que l'architecture de LIVEX a été conçue pour rendre impossible.

Le monde ne se calcule pas à l'écran. Il se calcule dans **SYNE**, le moteur déterministe qui détient la vérité de l'état simulé. Unreal, lui, ne fait que le montrer. Comprendre PRISM, c'est comprendre ce que « montrer » signifie quand on refuse de confondre le rendu avec la décision.

## Une décision écrite : l'ADR-002

Les débuts de LIVEX s'étaient appuyés sur un prototype **Godot**. Puis, à mesure que le projet s'est recentré sur l'immersion et la qualité de la représentation perceptive, un basculement s'est imposé, acté par l'**ADR-002** : le projet Unreal final de LIVEX s'appelle **PRISM** (*Perceptual Rendering & Interactive Simulation Module*) et intègre le plugin Unreal **PRISM-LDK** (*LIVEX Development Kit*).

Cette décision supersède l'ADR-001, conservé à titre historique. Le principe qui l'accompagne est catégorique :

> **SYNE reste le seul moteur décisionnel et l'unique autorité de l'état simulé.**

PRISM est un **hôte de rendu perceptif**, pas un second moteur. Le choix d'écrire noir sur blanc qu'un prototype est un prototype — et non le socle actuel — évite une confusion qui aurait faussé la lecture de l'ensemble du projet.

## Trois responsabilités, un partage net

La répartition n'a rien d'arbitraire : chaque composant a une mission, et une seule.

| Composant | Responsabilité |
| :-- | :-- |
| **SYNE** | Vérité de l'état, boucle de simulation, décisions des agents, déterminisme. |
| **ECHOS** | Observation, analyse, métriques, calibration, comparaison de runs. |
| **PRISM** | Représentation visuelle et perceptive, immersion, interaction utilisateur orientée rendu. Aucun calcul décisionnel. |

Cette séparation garantit que l'observation et la visualisation n'influencent jamais le cours de la simulation. Un rendu qui « aiderait » la décision, même avec de bonnes intentions, briserait la chaîne causale : on ne saurait plus si un comportement vient des lois du monde ou de la manière de le regarder.

## Le plugin PRISM-LDK : le contrat avant l'image

Le **PRISM-LDK** est le module technique qui formalise l'interface entre PRISM et l'écosystème LIVEX. C'est une pièce discrète, mais c'est elle qui rend l'intégration vérifiable. Elle définit quatre choses.

D'abord la **description `world_initialized` (v1.1)** : les paramètres du monde fournis par SYNE, dont `simulatedSecondsPerTick` et `metersPerUnit` à titre informatif, ainsi que les dimensions, la cellule et les TPS. Ensuite le **flux d'observabilité** : PRISM consomme les snapshots et les événements de SYNE dans l'ordre strict « snapshot puis événements », alignés sur le tick courant. Puis le **cycle de contrôle**, qui respecte les phases `prepare`, `ready`, `start`, `pause`, `resume`, `stop` et `reset` selon les contrats. Enfin la **supervision**, avec ses sondes de santé et de disponibilité (`health.path`, `intervalMs`, `startupMs`) et la gestion des états — Démarrage, Prêt, En cours — le tout aligné sur `TRANSPORT_API.md` et `INTEGRATION_CONTRACT.md` pour l'interopérabilité avec SYNE, ECHOS et le Launcher.

Autrement dit : avant d'afficher quoi que ce soit, le plugin sait quel monde il reçoit, à quelle cadence, et comment on lui demande de s'arrêter proprement.

## Le temps et l'espace, traduits sans être trahis

Une question délicate se pose dès qu'on sépare simulation et rendu : comment représenter le temps sans le fausser ? PRISM consomme `simulatedSecondsPerTick`, exposé dans `world_initialized`. Cette valeur définit l'**échelle temporelle simulée** — par exemple 60 secondes par tick pour le profil `reference`, 5 secondes par tick pour `prism` — sans altérer l'ordre causal ni les effets par action côté SYNE. Le temps affiché change ; la mécanique du monde, non.

`metersPerUnit`, lui, harmonise l'échelle spatiale entre la simulation et le rendu perceptif. Le profil `prism` est d'ailleurs conçu pour cet usage : un monde de 2240 × 2240, six TPS cibles et 5 secondes par tick, tout en restant cohérent avec les contrats de SYNE. Adapter la représentation est légitime ; adapter la causalité ne l'est pas.

## Un composant que le Launcher sait piloter

PRISM n'est pas une application isolée qu'on lance à la main. Le **Launcher** évalue le **déverrouillage Immersion** en lisant les exigences de PRISM — mode porteur, `snapshotStream`, `renderCadence`, contrôle — puis sélectionne dynamiquement le **profil** approprié, dont `prism`. Il démarre les composants dans l'ordre de résolution et les arrête dans l'ordre inverse. PRISM est traité comme un composant supervisable à part entière, avec son cycle de vie, ses sondes et sa gestion des arrêts et des incidents, le tout décrit par ses manifestes.

## Montrer, pas décider

Reste la question de fond : à quoi sert un rendu si ce n'est pas lui qui calcule ? La réponse définit la posture de PRISM.

PRISM **montre, il ne décide pas** : l'état visible reflète fidèlement les snapshots de SYNE. Il **respecte le temps simulé** : la cadence de rendu s'aligne sur le flux, sans extrapolation qui romprait la traçabilité. Et il **préserve la traçabilité** : tout élément visible remonte à un tick et à un snapshot identifiables.

Cette discipline se vérifie sur les cas limites. Le plugin a été éprouvé sur un rejeu de ticks troué — les ticks 41 puis 47, avec un silence sur 42 à 46. Résultat : le dernier état reçu demeure l'état courant pendant le silence, et aucun tick manquant n'est fabriqué. À la déconnexion, l'état reçu reste consultable et le flux est simplement déclaré dégradé. Les Automation Specs du plugin se clôturent avec **0 échec** sur Unreal Engine 5.8, y compris sur ces scénarios de rejeu. Un rendu qui invente le tick qu'il n'a pas reçu produirait une image agréable et un résultat faux.

## Un prototype n'est pas un socle

Il reste utile de le redire, car la confusion est fréquente : les références historiques à Godot (ADR-001) décrivent un prototype, jamais l'implémentation actuelle. L'ADR-002 acte formellement le passage à Unreal et au PRISM-LDK comme socle final, et cette clarification est répercutée dans la documentation (VISION, ARCHITECTURE, PRISM). Nommer clairement ce qui est ancien protège la lecture de ce qui est actif.

## Le canal perceptif de LIVEX

PRISM est le **canal perceptif** de LIVEX. En intégrant le plugin PRISM-LDK dans un projet Unreal final, il préserve la règle d'or du projet : **SYNE détient la vérité, ECHOS l'analyse, PRISM la perception.** Cette séparation, apparemment austère, est précisément ce qui permet d'observer une émergence honnête, traçable et reproductible — une émergence qu'on peut montrer sans jamais l'avoir arrangée.

Le détail de l'intégration et des contrats se trouve dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les avancées du projet — y compris les portes encore à franchir — paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
