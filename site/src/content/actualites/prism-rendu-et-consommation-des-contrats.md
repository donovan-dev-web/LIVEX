---
title: "PRISM : rendu et consommation des contrats"
description: "PRISM rend visible le monde simulé de LIVEX sans devenir source de vérité : lecture tolérante des contrats SYNE, null préservé, aucun tick inventé."
date: 2026-10-18
tags: ["prism", "unreal", "contrats", "rendu"]
draft: false
---

Une trame arrive. Elle contient un champ que votre code n'a jamais vu. Que se passe-t-il ? Deux réactions sont possibles. La mauvaise : le parseur lève une exception, la scène se fige, le rendu s'arrête — parce qu'une version du moteur a évolué plus vite que le client. La bonne : le champ inconnu est ignoré, et l'image continue. Ce détail minuscule sépare un client de rendu robuste d'un client fragile.

**PRISM**, le composant de représentation 3D de LIVEX, est bâti sur la seconde réaction. Il consomme des contrats versionnés, et il est conçu pour qu'un champ nouveau ne le fasse jamais tomber.

## Un rendu, pas un simulateur

La règle qui gouverne PRISM tient en une ligne : il montre, il ne décide pas. PRISM est un projet Unreal Engine intégrant le plugin **PRISM-LDK**, qui expose les types, fonctions et événements nécessaires pour se connecter à SYNE. Mais tout ce que l'écran affiche reflète l'état publié par le moteur ; rien de ce qui est visible ne revient influencer une décision.

Cette séparation n'est pas seulement une élégance d'architecture. Si le rendu pouvait modifier le monde, on ne saurait plus si un comportement observé vient des lois de la simulation ou de la manière de la regarder. La chaîne causale serait rompue, et toute conclusion deviendrait suspecte.

## Consommer un contrat, ce n'est pas en dépendre

PRISM se branche sur le **contrat d'observabilité 0.5.0** et la **description de monde 1.1** — les mêmes trames, qu'elles proviennent de SYNE ou de syne-mock. C'est justement là que se joue la robustesse : le plugin ne doit pas s'effondrer si la source ajoute un champ, ni inventer une valeur si un champ manque.

### La discipline de tolérance

Un client de rendu correct suit trois règles simples. Ignorer les champs inconnus, pour que la compatibilité fonctionne dans les deux sens du temps : un vieux client doit continuer de fonctionner face à un moteur plus récent. Traiter les champs optionnels comme absents s'ils ne sont pas là, plutôt que de leur substituer une valeur par défaut trompeuse. Et ne **jamais** écrire dans le monde simulé.

Ces règles paraissent évidentes. Elles ne le sont pas quand elles sont mises à l'épreuve des cas limites.

## null n'est pas égal à dix

Parmi les vérifications du plugin, une spec mérite d'être racontée. Elle soumet au parseur réel des trames de SYNE et de syne-mock, et vérifie qu'un `null` n'est **jamais** converti en `10`. C'est un piège classique en sérialisation : une valeur absente qui glisse vers un défaut arbitraire, et un rendu qui affiche soudain une position plausible et fausse.

La même spec vérifie les valeurs facultatives. `simulatedTimeSeconds` peut manquer : le plugin calcule alors le temps à partir du tick et de `simulatedSecondsPerTick`. `leaderId` peut être absent. Et les mesures d'analyse des liens (`maxBondDistance`, `meanBondDistance`) sont présentes dans la trame, mais **délibérément ignorées** : PRISM est un rendu 3D, c'est l'observatoire qui lit ces métriques. Un rendu n'a pas à consommer tout ce qu'il reçoit.

## Ce qu'il fait d'un trou

Le cas le plus révélateur d'un rendu honnête n'est pas la trame complète : c'est la trame manquante. Le plugin a été éprouvé sur un rejeu troué — les ticks 41 puis 47, avec un silence sur 42 à 46.

Résultat : le dernier état reçu demeure l'état courant pendant le silence, et **aucun tick manquant n'est fabriqué**. À la déconnexion, l'état reçu reste consultable et le flux est simplement déclaré dégradé. Aucune interpolation ni extrapolation ne s'insère dans le chemin d'état autoritaire du plugin : les écritures vers SYNE, elles, sont des commandes de contrôle explicites et protégées, nettement distinctes du rendu.

L'interpolation éventuelle à l'écran, entre deux snapshots, est un autre sujet — elle reste visuelle et n'est jamais réinjectée dans le monde. Inventer un tick pour obtenir une image fluide produirait une belle scène et un résultat faux.

![Vue PRISM dans le Launcher](../../assets/captures/launcher-prism.png)

## Où en est le travail

Il faut être clair sur le périmètre. PRISM est en **version de travail** : l'expérience de rendu interactive complète est **hors périmètre V0.2**. Le plugin, lui, est vérifié avec sérieux : au relevé sur Unreal Engine 5.8, ses Automation Specs se clôturent avec **69 specifications documentées et 0 échec**, y compris les deux specs de rejeu de flux troué. Ces vérifications parsent de véritables trames.

Côté intégration, PRISM est ouvert par le Launcher lorsqu'une session de rendu est disponible. Le plugin lit la même identité des deux côtés : contrat 0.5.0, moteur 0.17.0, description 1.1.

## Une réserve de fraîcheur, énoncée

Une limite mérite d'être signalée plutôt que dissimulée. La détection d'un flux **déconnecté** est couverte. En revanche, un WebSocket connecté mais silencieux n'est pas distingué d'un flux actif : aucune garde de monotonie de tick ni champ d'ancienneté n'est publiée à ce jour. Ce n'était pas prescrit par le contrat de transport examiné lors de l'acceptation ; la capacité n'a donc pas été ajoutée.

Si un consommateur a besoin de détecter un flux figé, il faudra définir le contrat et ses tests **avant** d'ajouter la fonctionnalité. C'est la règle maison : pas de capacité non spécifiée, pas de comportement implicite.

## Montrer fidèlement, voilà la valeur

PRISM ne cherche pas à être un second moteur, et il ne le sera pas. Sa contribution est ailleurs : montrer fidèlement un monde qu'il n'a pas calculé, tolérer les évolutions du contrat sans casser, préserver l'absence au lieu de la combler, et ne jamais laisser le rendu s'inviter dans la décision. Un champ inconnu arrive ? L'image continue. Un tick manque ? La scène ne l'invente pas. C'est exactement ce qu'on demande à un canal perceptif : représenter sans trahir.

Le détail de l'intégration et des contrats vit dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les avancées du projet — y compris les portes encore à franchir — paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
