---
title: "A0-4 : verdicts d'émergence et protocole préenregistré"
description: "Protocole préenregistré, contre-factuel obligatoire, verdicts publiés : comment LIVEX tranche une revendication d'émergence sans déplacer ses seuils."
date: 2026-10-19
tags: ["emergence", "a0-4", "protocole", "verdict"]
draft: false
---

Il y a un moment désagréable dans toute recherche honnête. C'est celui où l'expérience rend la réponse que l'on n'espérait pas, et où il faut décider quoi en faire. On peut la ranger dans un tiroir, invoquer une graine malheureuse, déplacer discrètement un seuil. Ou on peut l'écrire noir sur blanc et continuer. La campagne **A0-4** de LIVEX a choisi la seconde option, et c'est peut-être sa contribution principale.

Elle a mis deux phénomènes candidats à l'épreuve d'un protocole fixé **avant** l'exécution, et publié ses verdicts tels quels — y compris un verdict négatif.

## Ce qu'exige une revendication d'émergence

Le mot « émergence » a mauvaise presse pour une bonne raison : dans un salon, on peut le brandir à propos de n'importe quoi. « Regardez, un groupe s'est formé ! » Cette phrase ne vaut rien tant qu'elle ne peut pas être contredite.

LIVEX a donc fixé une règle, A0-4, qui énumère ce qu'une revendication doit satisfaire **cumulativement** pour être recevable : l'**absence de programmation explicite** de la structure observée ; une **chaîne causale remontable** jusqu'aux interactions locales ; une **reproduction multi-graines** (K ≥ 5) ; un **contraste ou contre-factuel préenregistré**, comprenant à la fois une baseline et une ablation ; et des **métriques dont la provenance** distingue le mesuré, l'absent et le zéro observé. Une sixième exigence les surplombe toutes : aucun critère ne peut être déplacé après observation. C'est la règle R5, et c'est celle qui donne du prix aux autres.

## Le protocole, écrit avant la première exécution

Le protocole complet est publié, et surtout, il est **lu par le code**. Les seuils proviennent d'un fichier de préenregistrement, jamais recopiés dans un test — de sorte qu'un ajustement discret soit techniquement impossible, pas seulement déconseillé.

La campagne exige au moins cinq graines, avec succès sur au moins ⌈0,8 × K⌉ d'entre elles. Elle impose un contre-factuel : on ne se contente pas de constater un phénomène en baseline, on vérifie qu'il **disparaît** quand on retire la condition supposée le produire. Et elle exige une provenance en trois états, pour que l'absence de mesure ne se déguise jamais en zéro. Le harnais contient quatre vérifications, et aucune n'a été touchée après observation.

## La campagne, en trois bras

L'exécution tient en quelques nombres : **3 bras × 5 graines × 50 agents × 1 000 ticks**, soit quinze runs, bouclés en environ une minute quarante. Le bras de référence n'est pas improvisé : il s'appuie sur le profil localisé du moteur, celui qui vient de franchir son gate de viabilité. Les quinze runs sont restés viables, avec leurs cinquante agents en vie.

## Deux verdicts, dont un qui dérange

### P2 — La quête spatiale : confirmée

Le premier phénomène candidat était la capacité des agents à se diriger vers un gisement **connu**. Le contraste est net : la part de décisions fraîches ciblant un nœud se situe entre **0,87 et 0,91** en baseline localisée, contre **0,00** lorsque les ressources deviennent abstraites. Les décisions de recherche `Seek*` suivent la même pente, entre 0,63 et 0,73 contre 0 à 0,01. Et dans le bras sans nœuds, la distance à une source est **absente** — `null` — jamais codée comme un zéro mesuré. Sur les cinq graines, la quête spatiale passe le contraste préenregistré.

### P1 — Le cycle social : non confirmé

Le second phénomène promettait davantage. Il s'agissait d'un cycle homéostatique du besoin social : une oscillation qui se régule d'elle-même. En ablation, quand on annule la récupération sociale, la disparition est totale — aucun cycle chez aucun agent, sur les cinq graines. Le signal semble donc net.

Mais le protocole ne s'arrête pas au signal qui arrange. En baseline, la saturation médiane vaut **1,0**, au-dessus du seuil préenregistré de 0,95. Autrement dit, le cycle sature au lieu d'osciller. Le plafond de 0,702 observé dans un test antérieur ne valait que pour un agent isolé, jamais pour un arbitrage à cinquante agents. Le verdict est donc **non confirmé**, et le seuil n'a pas bougé d'un pouce.

## Le piège que le harnais a corrigé

L'histoire retient un détail instructif. Une première version du harnais mesurait la « part de décisions ciblant un nœud », mais la traitait avec la même logique que la distance aux sources. Or ce sont deux grandeurs différentes : la part de décisions est un recensement, qui vaut légitimement zéro quand aucun nœud n'existe ; seule la **distance** est réellement absente — `null` — sans nœuds à mesurer. Confondre les deux transformait un zéro observé en absence, et faussait le verdict. Le harnais a été corrigé, puis ré-exécuté **sans toucher aux seuils**. Cette distinction paraît tatillonne ; elle est le cœur de la rigueur revendiquée.

## Le verdict général

Le verdict scientifique général reste **« émergence non établie »** pour les phénomènes étudiés. Une quête spatiale a été confirmée par contraste préenregistré ; le cycle homéostatique social ne satisfait pas ses conditions complètes sous ce protocole.

Ce résultat négatif n'est pas une défaillance de méthode, c'est une information. Il borne précisément ce que LIVEX peut revendiquer, et il évite le glissement le plus courant du domaine : transformer un signe encourageant en preuve générale d'intelligence ou de société.

## Les limites, énoncées à l'avance

Le protocole a aussi fixé ses bornes. Les mesures valent pour la configuration, les graines et les horizons rapportés, rien de plus. Le gate de viabilité localisée, lui, porte sur trois graines et 2 500 ticks. La tenue à long horizon, la raréfaction des ressources et le passage d'un hiver restent à étudier. Confirmer une quête spatiale n'est pas confirmer une émergence au sens large — et la campagne ne prétend pas le contraire.

## La méthode comme contribution

A0-4 montre qu'un projet peut produire des verdicts préenregistrés, y compris négatifs, sans que cela affaiblisse sa crédibilité — au contraire. La méthode est la contribution ; le verdict est le résultat. C'est dans cet esprit que LIVEX poursuit ses investigations, un protocole à la fois.

Les rapports de campagne, les seuils et leurs limites vivent dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les prochains verdicts paraîtront dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) — y compris ceux qui dérangent.
