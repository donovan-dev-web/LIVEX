---
title: "Campagnes reproductibles et validation multi-seeds"
description: "Six runs multi-seeds sur 2500 ticks, 0 extinction, pente stable : comment LIVEX transforme une intuition de viabilité en preuve reproductible."
date: 2026-10-08
tags: ["campagnes", "seeds", "calibration", "reference", "validation"]
draft: false
---

Un monde qui tient trois cents ticks ne prouve presque rien. À cette échelle, n'importe quel réglage un peu généreux produit l'illusion d'un équilibre. La question intéressante commence bien plus loin : si l'on prolonge jusqu'à 2 500 ticks, sur plusieurs graines, avec des populations différentes, le monde tient-il encore — et tient-il de la même manière ?

LIVEX a choisi de répondre à cette question par une **campagne**, pas par un exemple. Six runs, trois graines, deux tailles de population, des critères écrits avant l'exécution. Le résultat — 6/6 validés — vaut moins par son score que par la méthode qui l'a produit.

## De l'intuition à la preuve

Dire d'un profil qu'il est « viable » n'a de sens que si l'on précise dans quelles conditions, pendant combien de temps, et selon quels critères. Quatre exigences découlent de cette rigueur.

D'abord la **stabilité sur la durée** : survivre à 2 500 ticks, pas seulement à 300, car certaines dérives ne se révèlent qu'avec le temps. Ensuite la **robustesse aux graines** : un comportement qui n'apparaît que sur une graine relève de l'accident, pas de la propriété du monde ; il faut donc observer plusieurs `seed`, dont 12345, 424242 et 999. Puis des **critères quantifiés** : des seuils explicites, jamais un « ça a l'air de tenir ». Enfin la **traçabilité** : chaque run doit pouvoir être rejoué depuis sa graine et sa configuration, sans quoi la preuve n'est pas transmissible.

## Calibrer le profil « reference »

Le profil `reference` a été calibré — valeurs dites **B1** — pour produire une trajectoire stable. Le travail a consisté à ajuster finement des paramètres qui, pris isolément, paraissent anodins, mais dont la combinaison décide de la survie d'une population.

| Famille de paramètres | Valeurs retenues | Intention |
| :-- | :-- | :-- |
| Ressources | `food`/`water` initiales 20000, régénération 20/10, `degradationTick` = `null` | Empêcher l'annulation de la régénération par périodes et garantir un approvisionnement continu. |
| Coûts énergétiques | `moveEnergyCost` 0,03, `restEnergyGain` 1,5, `restFatigueRecovery` 2,0 | Rendre le repos réellement réparateur et le déplacement soutenable. |
| Bénéfices Eat/Drink | `eat.energyRecovery` = 2,0, `drink.energyRecovery` = 1,0 | Rehausser le gain des actions vitales, avec plafonnement et déplafonnement cohérents via `UtilityEvaluator.BenefitOf`. |
| Dérives sociales et curiosité | `socialDriftRate` 0,0002, `curiosityDriftRate` 0,0005 | Limiter les dérives lentes qui finiraient par dominer l'arbitrage. |
| Communication | `maxSendsPerTick`/`maxReceivesPerTick` = 1, `relayEnabled` false, coûts messages à 0 | Borner les interactions sans les faire payer en énergie. |

Un point compte autant que les valeurs : il n'existe **qu'un seul jeu de définitions**, partagé entre `SimulationProfiles.Reference()` et `configs/simulation/reference.json`. Le chemin `entries.*` y est strict, justement pour éviter que des paramètres soient silencieusement ignorés par le format JSON. Parce que ces changements modifient volontairement la trajectoire du monde, ils s'accompagnent d'un incrément `engineVersion` en MINOR ; la calibration B1 a ainsi conduit le moteur jusqu'à la version 0.17.0. La règle reste simple : version inchangée, trajectoire inchangée ; trajectoire changée, version incrémentée.

## Le plan de campagne, et son verdict

Les **critères d'acceptation** ont été fixés avant l'exécution : `alive_count` strictement supérieur à 0 à t=2500 pour tous les runs, aucune mort observée, une pente énergétique stable avec `|energySlopePerTick| < 0,005` sur la fenêtre pertinente, et une population finale proche de la population initiale.

Le plan tient en une ligne arithmétique : **6 runs = (50 agents + 100 agents) × 3 graines** (12345, 424242, 999). À l'arrivée, le verdict est **6/6 validés**. Tous atteignent 2 500 ticks sans extinction, la population finale reste égale à la population initiale, et la pente mesurée tourne autour de 0,0037 par tick — sous le seuil fixé. Le chiffre qui compte n'est pas le 6/6, mais le fait qu'aucun critère n'ait été ajusté après coup pour l'obtenir.

## Rejouer par script

Une campagne qui ne peut pas être rejouée par un tiers n'est pas une campagne, c'est un récit. LIVEX fournit donc des scripts pour la refaire depuis un clone propre. `scripts/calibration_campaign.py` valide le profil `reference` sur plusieurs graines ; `scripts/reset_campaign.py` enchaîne des runs de manière cohérente via `POST /api/control/reset`, avec surcouche de configuration ; `scripts/v3_campaign.py` explore une campagne V3 portant sur la cadence, le backpressure, le lag et la reprise de worker. Tous empruntent le **chemin HTTP réel** (`--serve`), avec un flux WebSocket segmenté par `runId`. Rejouer n'est pas une option documentaire : c'est le mode d'emploi.

## Reset ou prepare : la même pente

Un détail technique a son importance lorsqu'on enchaîne les runs. `POST /api/control/reset` accepte désormais une **surcouche `config` partielle**, exactement comme `start` et `prepare`. Cela permet d'enchaîner des populations différentes sans relancer SYNE — et surtout sans repartir sur des valeurs par défaut non calibrées.

La validation a vérifié ce point de manière concrète : le chemin `prepare` et le chemin `reset`, appliqués avec la même surcouche, produisent **des pentes identiques**. Le profil de référence reste donc appliqué de façon cohérente après un reset. Si ce n'était pas le cas, chaque reprise réintroduirait un biais invisible — le genre d'écart qui, additionné sur des dizaines de runs, finit par invalider toute la série.

## Ce qu'ECHOS consigne

Une campagne ne se résume pas à un tableau de scores. Pendant l'exécution, **ECHOS** consigne `outcome` (`extinct` ou `surviving`), `extinctionTick`, et un bloc `viability` comprenant la pente d'énergie sur les 200 derniers ticks, `actionSharesWhenHungry` et `resourceRegime`. Ces signaux sont **observe-only** : ils décrivent ce qui s'est passé, sans jamais intervenir dans la simulation, et sont exposés dans `/api/runs`. Le Launcher complète l'archive en consignant `provenance.json` et `AnalysisComponentVersion` dans le paquet `.livexp`. Chaque chiffre publié sait d'où il vient.

## Quatre familles de preuves

Les résultats reposent sur quatre appuis complémentaires. Les **oracles dorés** épinglent des checksums sur les parcours critiques — perception `0xf4aaa2733e491935`, état complet `0x954b8e0e70c0af5f` — que toute déviation casse, et qui ont été revalidés après la calibration. Les **tests bout-en-bout** exécutent le parcours J3 réel, avec un SYNE publié et un ECHOS réel, et vérifient la reprise sans rejeu ainsi que l'intégrité octet à octet du paquet. La **détection honnête** journalise les trous de ticks (`gaps_detected`) et marque explicitement la provenance `measured`, plutôt que de combler les manques. Enfin la **campagne V3** mesure la cadence, le backpressure et le lag sous contrainte — avec un gel volontaire du consommateur — et publie ses résultats, jamais masqués. L'ensemble se clôt avec **0 échec** aux relevés.

## Tenir, et pouvoir le refaire

« Tenir 2 500 ticks » n'est pas un artifice. C'est le fruit d'un **ajustement explicite** des défauts intégrés (B1), d'un **ordre causal strict**, d'une **communication bornée** et d'un **suivi utilitaire** cohérent. L'objectif n'a jamais été d'optimiser la survie à tout prix, mais d'obtenir un comportement **stable et traçable** dans un monde aux règles explicites. Un monde qui survivrait par un réglage secret intéresserait peu : le sujet de LIVEX, c'est la preuve, pas la performance.

La philosophie tient en une phrase : **si ça tient sous six runs multi-seeds, on peut rejouer. Si on peut rejouer, on peut discuter honnêtement du phénomène observé.** C'est ce qui transforme une intuition de viabilité en résultat transmissible.

Les protocoles, les seuils et les limites de ces campagnes sont documentés dans le [portail de documentation](https://github.com/donovan-dev-web/LIVEX). Les prochains relevés paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) — et un ★ sur le [dépôt](https://github.com/donovan-dev-web/LIVEX) aide ce travail à être vu.
