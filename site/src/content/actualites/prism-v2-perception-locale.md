---
title: "Percevoir sans tout savoir : l'information locale dans PRISM V2"
description: "La perception prévue dans PRISM V2 distingue proximité détaillée, résumés régionaux et mémoire, sans donner aux agents une carte omnisciente."
seoTitle: "Perception locale et mémoire dans PRISM V2"
date: 2026-11-04
tags: ["prism", "v2", "perception", "syne"]
draft: false
---

Pour choisir une action, un agent a besoin d'informations. Mais s'il connaît en permanence la position de chaque ressource et de chaque autre agent, l'exploration perd son sens. La cible PRISM V2 prévoit donc une perception locale, transmise à SYNE sans transformer celui-ci en observateur omniscient.

La spécification distingue trois niveaux. **À proximité**, dans un rayon de départ de 16 blocs, l'agent reçoit des détails sur les objets importants qu'il peut voir : blocs exposés, objets au sol, agents, structures et chantiers. **À moyenne distance**, jusqu'à trois régions, il reçoit des résumés régionaux, comme la présence de certains tags, sans connaître chaque objet. **Au-delà**, PRISM n'envoie aucune observation : l'agent s'appuie sur ce qu'il a mémorisé.

## Des différences plutôt qu'une carte complète

Les observations sont prévues sous forme de changements : apparition, modification ou disparition. À cela s'ajoutent l'état de soi — position, inventaire, outil, maîtrises et statut — et les événements qui expliquent ce qui vient de se produire.

Une règle importante empêche les « rayons X » : les résumés ne comptent que les blocs exposés. Un minerai sous plusieurs couches de roche ne devrait donc pas apparaître comme une ressource connue avant que l'agent puisse l'observer.

## La perception et la mémoire n'ont pas le même rôle

PRISM détermine ce qui est physiquement observable ; SYNE conserve les souvenirs, les croyances et l'incertitude de l'agent. Une information peut ainsi rester utile après que sa source a quitté le champ de perception, tout en devenant moins fiable avec le temps.

Cette frontière change aussi la façon dont les actions échouent. Une macro-action d'approvisionnement doit partir d'un indice connu et ne chercher que parmi les sources perçues. Si la ressource n'est pas là, PRISM renvoie un échec motivé ; SYNE peut alors réviser sa croyance. Le moteur ne révèle pas discrètement l'emplacement d'une ressource cachée pour « aider » l'agent.

Ce modèle est un élément prévu du contrat SYNE↔PRISM 1.0. Il vise à rendre l'exploration et l'information partageable pertinentes, sans garantir qu'elles produiront à elles seules une forme de coopération.
