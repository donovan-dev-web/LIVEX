---
title: "LIVEX : naître d'une question"
description: "Peut-on faire émerger des structures sociales non programmées dans un monde simulé ? Retour sur la question qui a donné naissance à LIVEX."
date: 2026-10-01
tags: ["vision", "fondements", "emergence"]
draft: false
---

Entrez dans le village. La place est tiède, une femme balaie devant sa porte, un homme remonte du puits avec deux seaux, un garde longe le mur d'enceinte. Vous partez deux heures en quête. Quand vous revenez, la femme balaie encore, l'homme remonte les mêmes deux seaux, le garde est au même angle du mur. Rien n'a bougé.

Ce n'est pas un oubli des développeurs. C'est une architecture. Chaque habitant suit un arbre de comportements ou une machine à états : « si le joueur approche, saluer ; si la nuit tombe, dormir ; si la réserve est vide, aller au puits ». La richesse apparente vient du nombre de cas prévus, tous écrits à l'avance. Le village n'est pas habité : il est joué.

Et si l'on construisait un monde où la partition n'existe pas ?

## Une question, avant toute fonctionnalité

> Peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?

Cette question a été posée avant la première ligne de code. Elle commande encore aujourd'hui les choix d'architecture de LIVEX, comme ses renoncements. Elle vient d'une curiosité pour les mondes de science-fiction et les LitRPG, ces univers qui paraissent réellement vivants parce que leurs habitants ont des besoins, des perceptions et une autonomie propre. L'objectif n'est donc pas d'écrire des comportements plus riches. C'est de construire un environnement et des lois assez solides pour laisser ces comportements **émerger**.

Des lois solides, dit-on. Il en manque une : vérifiable.

## De l'intuition à l'expérience

Il est facile d'évoquer l'émergence dans un salon. « Regardez, un groupe s'est formé ! » Une telle phrase ne vaut rien tant qu'elle ne peut pas être contredite. Pour répondre honnêtement, il faut passer de l'intuition à un **dispositif testable** : un monde dont les règles sont lisibles, dont les comportements n'ont pas été écrits, et dont tout ce qui s'y produit peut être observé, mesuré, analysé et **rejoué**.

### Observer, puis interpréter

LIVEX distingue avec soin deux choses que l'on confond souvent.

Ce qu'on observe, d'abord : des propriétés au niveau du système — regroupements, échanges, spécialisations — qui n'ont pas été écrites comme objectifs. Au bout de quelques centaines de ticks, par exemple, des entités se retrouvent au même endroit. C'est un fait : il est dans le flux, il se compte, il se reproduit à graine identique.

Ce qu'on interprète, ensuite : les hypothèses que l'on formule pour expliquer ce fait. « Elles s'y retrouvent parce que la soif les y pousse, et que certaines ont appris que d'autres y viennent. » Une hypothèse n'a de valeur que si elle peut être infirmée — par un rejeu, par un contre-factuel, par une mesure dont on connaît la provenance.

Dans ce cadre, l'émergence n'a rien de magique. Elle désigne des propriétés systémiques **traçables** jusqu'aux interactions locales qui les ont produites, et reproductibles d'une exécution à l'autre à graines identiques. Le mot perd son prestige ; il gagne un sens.

## Ni marionnettes, ni boîte noire

Deux grandes familles occupent le terrain, et LIVEX n'appartient à aucune des deux.

La première est **scriptée**. Comportements prédéfinis, précis, lisibles, économiques. Sa limite est structurelle : un arbre de comportements encode des cas prévus. Il peut en combiner beaucoup, il n'en invente aucun. Une conduite dont aucun cas n'a été écrit lui reste hors de portée. L'illusion orchestrée est précieuse pour le spectacle ; elle est inutile pour la question posée ici.

La seconde délègue la cognition à l'**apprentissage profond**. Des modèles capables de productions saisissantes, mais probabilistes. Deux exécutions avec la même entrée peuvent diverger ; remonter la chaîne causale d'une décision devient un problème ouvert ; tenir une cohérence sur des milliers de ticks reste fragile. Ce n'est pas un jugement de valeur : c'est une question de compatibilité avec ce que la recherche exige.

LIVEX choisit alors une troisième voie : un **moteur à règles explicites** où des entités autonomes, dotées de besoins, de mémoire, de croyances et de capacités d'action, interagissent dans un environnement persistant et partiellement observable. Un exemple suffit à faire sentir le principe. Un agent a faim ; il aperçoit un voisin ; il arbitre entre manger, parler ou se reposer. Personne n'a écrit cette scène. Elle résulte d'une formule d'utilité, de seuils qui dérivent, d'une perception incomplète et d'un tempérament. Le résultat visé n'est pas une démonstration spectaculaire : c'est une **expérimentation contrôlée**.

## Le prix de l'honnêteté

Une telle démarche impose des contraintes que l'on ne voit pas depuis la salle de projection. Il faut un déterminisme vérifiable, une instrumentation qui ne modifie pas le monde observé, des versions qui ne bougent pas en silence. Et il faut accepter un risque inconfortable : que l'expérience rende un verdict négatif.

Ce risque a été couru. En octobre 2026, LIVEX a mis deux phénomènes candidats à l'épreuve d'un protocole préenregistré : trois bras, cinq graines, cinquante agents, mille ticks par run. La quête spatiale vers un gisement connu a été **confirmée par contraste** sur les cinq graines. Le cycle homéostatique du besoin social, lui, n'a pas satisfait ses conditions : verdict **non confirmé**. Et le verdict scientifique général est resté celui qu'imposaient les données : « émergence non établie ».

Un projet qui chercherait seulement à convaincre aurait mis le second résultat sous le tapis. LIVEX l'a publié, parce que ce n'est pas un échec de communication : c'est le résultat. Une méthode qui ne peut pas dire « non » ne dit rien de solide quand elle dit « oui ».

## La question reste ouverte — et c'est ainsi qu'elle doit l'être

C'est dans cet esprit que naît LIVEX : non pour produire une réponse définitive, mais pour construire un outil permettant de poser la question dans des **conditions contrôlées, honnêtes et testables**. Une question qui se rejoue, se mesure et se discute — plutôt qu'une question qui se raconte.

La suite s'écrit lentement, au rythme des campagnes et des relevés. Si vous voulez suivre l'enquête, deux portes sont ouvertes : le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/), où vivent les textes de référence, et ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml). Les résultats y paraissent au fil de l'eau — y compris ceux qui dérangent.
