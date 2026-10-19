---
title: "syne-mock : parité et tests clients"
description: "syne-mock émule les contrats SYNE pour tester un client sans lancer le moteur .NET : transport, description de monde 1.1 et parité mesurée champ par champ."
date: 2026-10-18
tags: ["syne-mock", "parite", "contrats", "tests"]
draft: false
---

Comment tester un écran de rendu quand la chose qu'il doit afficher n'existe pas encore ? Attendre que le moteur soit compilé, publié, lancé et branché, à chaque modification d'une ligne de Blueprint ? Cette dépendance-là tue la vitesse de développement. Elle tue aussi la qualité : on finit par tester rarement, donc mal.

**syne-mock** est né de ce problème très concret. C'est un petit serveur Node.js qui parle le même langage que SYNE — le même transport, les mêmes messages, la même description de monde — sans exécuter la simulation. Un doublure, en somme. À condition de ne jamais oublier ce qu'une doublure ne peut pas faire.

## Un serveur déterministe, pas un moteur de secours

syne-mock est en version 0.1.0, mais il ne vit pas isolé : il émet le contrat d'observabilité **0.5.0** et se déclare aligné sur le moteur **0.17.0**. Ces valeurs ne sont pas recopiées à la main — elles proviennent d'un seul endroit, vérifié par les tests, pour qu'un décalage silencieux soit détecté plutôt que toléré.

Ce qu'il fait, il le fait bien : il émet `world_initialized`, puis, à chaque tick, un `snapshot`, un `tick_summary`, un `decision_made` et un `action_completed` par agent. Le WebSocket texte JSON parle sur `ws://127.0.0.1:5180/`, le contrôle HTTP sur `http://127.0.0.1:5181`. Le cycle de vie est celui du contrat : `prepare`, `ready`, `start`, `pause`, `resume`, `stop`, `reset`.

## Le piège du mauvais monde

Il y a un piège classique, et syne-mock l'illustre bien. Lancer le mock sans configuration fait démarrer un monde de 500 × 500, alors que PRISM attend, dans une session d'immersion, un monde de 2 240 × 2 240. Les deux plateformes se mettent alors à raconter des géométries différentes — et l'on cherche longtemps pourquoi le rendu est faux.

La solution est un fichier de configuration dédié, `config_prism.json`, miroir du profil `prism` du moteur : 2 240 × 2 240, cellule 32, 6 TPS, 5 secondes par tick, 100 agents, cinq obstacles — côte sud, crête centrale, amer — exactement comme dans `SimulationProfiles.Prism()`. Et un durcissement appris à la dure : un chemin de configuration demandé mais introuvable **arrête le démarrage**, plutôt que de retomber silencieusement sur les valeurs par défaut. Le repli silencieux, c'était justement lui, le piège.

## Un harnais de parité, champ par champ

Une doublure ne vaut que si elle ressemble. Pour mesurer la ressemblance, un harnais lance le moteur réel et le mock sur la même géométrie et le même agencement d'obstacles, puis compare leurs descriptions de monde **champ par champ**.

Le résultat est net : **4 900 cases identiques, 11 bloquées, 0 divergence**. La parité ne porte pas seulement sur les nombres, elle porte sur les types et sur le périmètre : le mock ne publie que ce que le moteur sait produire. Cellules `plains`, coût 1 ou 100, obstacles en disques `{id, x, y, radius}`. Ni terrain, ni biome, ni obstacle procédural : ces clés sont **refusées**, pas ignorées. Une clé inconnue doit faire du bruit, pas disparaître.

## Comment s'en servir

Le mode d'emploi tient en trois gestes. Installer et démarrer le serveur, en pointant explicitement une configuration — `npm start -- config_prism.json` — plutôt qu'en laissant les défauts décider. Connecter le client à l'URL du mock au lieu de SYNE, ses ports par défaut coïncidant déjà avec ceux attendus par ECHOS. Puis vérifier l'alignement avec le harnais : `node scripts/parity-mock-syne-world.js`.

Le mock s'intègre aussi au Launcher, via un `component.json` qui déclare son mode de démarrage, sa sonde de statut et un arrêt propre protégé par un jeton de session. Il sait se faire superviser comme les autres — mais il ne prétend pas gérer les campagnes scientifiques de SYNE.

## Ce que la parité garantit, et ce qu'elle ne garantit pas

C'est ici qu'il faut être précis, car c'est ici que les malentendus coûtent cher.

La parité garantit que, pour une même entrée, le mock et le moteur publient **les mêmes champs, avec les mêmes types et les mêmes valeurs bornées**. Un client qui se branche correctement sur le mock se branchera correctement sur SYNE. C'est exactement ce qu'il faut pour tester un rendu, une liaison de transport ou un parseur.

La parité ne garantit **pas** que le mock simule le même monde. Il ne reproduit pas fidèlement les décisions des agents — ni la formule d'utilité, ni la cognition —, pas davantage le pathfinding A*, ni les systèmes sociaux. Son détour local d'obstacle n'est pas l'A* du moteur, et ses trajectoires ne sont pas garanties identiques bit à bit. Plusieurs zones sont volontairement décoratives : les croyances et la confiance sont publiées vides, la fuite reste inatteignable, la mémoire, les livres, la perception et la mort n'ont pas de portée réelle.

## Les vérifications

Les tests du mock passent avec **51 réussis, 0 échec**. Le harnais de parité tourne en intégration continue, et un test dédié couvre le trajet complet ECHOS ↔ mock : contrôle HTTP, flux WebSocket, ingestion, stockage. Un `test_ingestion_states_contract.py` vérifie que les états de données — mesuré, non mesuré, absent, trou — sont correctement distingués sur les deux voies, streaming et batch.

## La doublure, à sa juste place

syne-mock rend un service précis : il débarrasse le développement client de la dépendance au moteur .NET, et il rend cette indépendance **vérifiable**. Sa parité est mesurée, pas supposée. Mais il faut lui garder sa juste place : c'est un outil de développement des contrats, pas un moteur de simulation. Toute conclusion scientifique doit être produite avec SYNE réel, et tout contrat final validé contre lui.

La référence de contrat, les routes et les limites du mock sont détaillées dans son [README](https://github.com/donovan-dev-web/LIVEX/blob/main/syne-mock/README.md) et le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les évolutions de contrat, elles, seront annoncées dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
