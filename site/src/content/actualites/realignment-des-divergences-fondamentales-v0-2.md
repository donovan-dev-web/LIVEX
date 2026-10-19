---
title: "Réalignement des divergences fondamentales : LIVEX passe en V0.2"
description: "LIVEX passe en V0.2 : moteur SYNE 0.17.0, contrat d'observabilité 0.5.0, verdicts préenregistrés et documentation alignée sur le code réel."
date: 2026-10-19
tags: ["livex", "v0.2", "syne", "realignement", "contrats"]
draft: false
---

Imaginez trois instruments braqués sur le même monde. Le premier le produit, le second le mesure, le troisième le montre. Pris isolément, chacun dit la vérité. Mis côte à côte, ils racontent des histoires légèrement différentes. C'est précisément ce genre d'écart que LIVEX vient de traquer, puis de refermer. La campagne de réalignement des divergences fondamentales est close : le projet passe en **V0.2**.

Ce chantier n'a rien ajouté de spectaculaire à l'écran. Il n'a produit aucune fonctionnalité dont on puisse faire la démonstration en une capture d'écran. Il a fait quelque chose de moins visible et de plus exigeant : rendre cohérents le moteur, les contrats, l'observation et la documentation. L'objectif tient en une phrase — ce qui est simulé, ce qui est mesuré et ce qui est documenté doivent dire la même chose.

## Pourquoi un réalignement

Une simulation multi-agents crédible repose sur une séparation stricte des responsabilités. SYNE détient l'état du monde, ECHOS l'observe, PRISM le rend visible. Personne n'empiète sur le voisin, et c'est exactement ce qui rend une conclusion possible : s'il n'existe qu'un seul endroit où les lois du monde vivent, alors un phénomène observé vient des lois, pas d'un outil.

Le problème commence quand les couches se mettent à diverger, même de quelques degrés. Une trace qui confond l'absence de mesure avec un zéro. Un ordre d'exécution qui devient, sans le dire, une cause. Une décision collective qui se glisse dans l'arbitrage individuel alors qu'elle prétendait seulement le décrire. Rien de tout cela ne casse une démonstration. Tout cela fausse une expérience.

Le réalignement a donc porté sur quatre axes : la **causalité** et l'ordre des décisions dans le moteur, la **cognition active** et la portée réelle de la décision collective, la **viabilité** du profil localisé face au profil global, et la **validation expérimentale** avec des critères préenregistrés.

## Ce qui a changé dans le moteur

### Refermer la fenêtre causale

Dans un simulateur, l'ordre dans lequel on parcourt les entités ne devrait jamais être une cause. Si l'agent numéro 3 décide avant l'agent numéro 4 — et que cela change le monde — alors l'ordre d'itération est devenu une loi cachée. Dans SYNE, la fenêtre causale est désormais fermée par `TickCommitBuffer` : tous les agents perçoivent et délibèrent sur l'état figé au début du tick, et les effets sont appliqués à sa fermeture. L'invariance à l'ordre d'insertion n'est pas postulée, elle est prouvée par les tests `CausalWindowTests` et `CausalOrderInvarianceTests`.

### Une décision collective exposée, jamais injectée

Un groupe qui « prend une décision » est un fait observable. En faire une force qui pousse l'utilité individuelle serait autre chose : une politique prescrite glissée dans l'arbitrage, sous un nom qui ne l'annonce pas. LIVEX a tranché. La décision collective demeure **exposée** dans les snapshots et l'événement `group_decision`, mais elle n'est **jamais injectée** dans le choix d'un agent. Les mécanismes de propagation d'objectifs collectifs ont été retirés. Et parce que l'observateur doit pouvoir mesurer sans inventer, les distances entre paires liées sont instrumentées : elles valent `null` lorsqu'elles ne sont pas mesurées, jamais zéro.

### Le profil localisé comme modèle cible

Le profil localisé — où les agents cherchent de véritables gisements — est devenu le modèle de référence ; le profil global, où les réserves sont des quantités abstraites, sert désormais de baseline explicitement déclarée. Le gate de viabilité qui autorisait ce basculement a été publié puis franchi, avec des critères fixés avant l'exécution et non ajustés après.

## Des contrats additifs, pas des ruptures

Rendre le moteur cohérent aurait pu signifier casser tout ce qui le lit. Ça n'a pas été le cas. Le contrat d'observabilité passe en **0.5.0** et la description de monde en **1.1**, en **ajout seul** : les consommateurs existants restent compatibles, aucun champ requis n'a disparu.

L'échelle temporelle (`simulatedSecondsPerTick`) et spatiale (`metersPerUnit`) sont désormais explicites, ce qui évite de raconter le temps d'une façon dans le moteur et d'une autre dans le rendu. ECHOS consomme ce flux et calcule ses métriques de viabilité avec une **provenance explicite** — mesuré, absent, ou zéro observé. Cette distinction, qui paraît technique, sépare une observation d'une croyance : confondre l'absence de données avec une valeur nulle, c'est déjà tirer une conclusion que les données ne soutiennent pas.

![Vue des relations observées dans ECHOS](/captures/echos-relations.png)

## L'épreuve du verdict

Un réalignement qui ne se vérifie pas ne vaut pas mieux qu'un discours. La campagne A0-4 a donc appliqué un protocole strict : seuils préenregistrés et lus par le code, au moins cinq graines, contre-factuel obligatoire, provenance en trois états, et interdiction absolue de déplacer un critère après observation.

Les verdicts sont tombés, y compris là où ils dérangent. La **quête spatiale** vers un gisement connu est confirmée sur les 5 graines : le contraste est net, entre 0,87 et 0,91 de décisions ciblant un nœud, contre 0,00 en zéro observé. Le **cycle social** homéostatique, lui, n'est pas confirmé : sa saturation médiane dépasse le seuil préenregistré. Le verdict scientifique général reste **« émergence non établie »**, et il est publié tel quel. Une méthode incapable de dire « non » ne dit rien de solide quand elle dit « oui ».

## Montrer et orchestrer, sans décider

En aval du moteur, deux composants continuent de tenir leur rang. PRISM, le projet Unreal final de LIVEX, intègre le plugin **PRISM-LDK** et consomme les contrats SYNE sans jamais devenir une source de vérité : il rend le monde visible, il ne le calcule pas. Le **Launcher**, lui, orchestre la pile, supervise les composants et archive les campagnes dans un format scellé `.livexp`, tout en restant indépendant de ce qu'il pilote.

![Accueil du Launcher LIVEX](/captures/launcher-home.png)

![Vue PRISM dans le Launcher](/captures/launcher-prism.png)

## La documentation, enfin alignée

Une divergence peut aussi se loger dans les mots. README global, versions, contrats, captures et animations ont été réalignés sur le code réel : le projet est présenté en V0.2, avec des valeurs exactes et une mise en page professionnelle. Une documentation qui promet une version que le moteur n'exécute pas est une divergence comme une autre — simplement plus difficile à mesurer.

![Vue des agents observés dans ECHOS](/captures/echos-agents.png)

## Les chiffres du relevé

| Élément | Valeur |
| :-- | :-- |
| Version globale | V0.2 |
| Moteur SYNE | 0.17.0 |
| Contrat d'observabilité | 0.5.0 |
| Description de monde | 1.1 |
| Tests SYNE | 695/695, 0 échec (604 Core + 91 Console) |
| Tests ECHOS | 383 réussis, 0 échec |
| Tests syne-mock | 51 réussis, 0 échec |
| Tests Launcher | 174 unitaires, 20 intégration, 33 E2E, 0 échec |

## Ce que V0.2 laisse derrière lui

V0.2 marque la clôture d'un travail qui ne se voit pas mais qui commande tout le reste. Le moteur, les contrats, l'observation et la documentation sont alignés : quand SYNE dit une chose, ECHOS la mesure et la documentation la rapporte. Ce n'est pas une promesse d'émergence — le verdict reste « non établie » —, c'est une garantie de cohérence.

La suite porte sur les tâches intermédiaires et la préparation du commit final, sans modification silencieuse des seuils ni des oracles. Pour suivre le détail des décisions, le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/) rassemble les textes de référence, et ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/) publie les relevés au fil de l'eau — également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) si vous préférez que le flux vienne à vous.
