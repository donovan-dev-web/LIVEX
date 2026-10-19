---
title: "ECHOS : observation, analyse et calibration rigoureuses"
description: "ECHOS observe SYNE sans jamais le modifier : métriques avec provenance, trous de ticks signalés, rapports de calibration versionnés."
date: 2026-10-05
tags: ["echos", "analyse", "metriques", "calibration", "observabilite"]
draft: false
---

Une courbe retombe à zéro. Imaginez le rapport de fin de campagne : la population décroît, tick après tick, puis la ligne touche l'axe. Deux lectures s'offrent à vous. Le monde est mort — ou votre instrument a cessé de voir au milieu du trajet.

Un observatoire ordinaire publierait le zéro, et vous décideriez ensuite. **ECHOS** publie le zéro et sa provenance : mesure réelle, ou fenêtre manquante. C'est peu, dit comme ça. C'est tout, si l'on veut en tirer une conclusion.

ECHOS (*Emergent Cognition & Holistic Observation System*) est l'observateur et l'analyste de LIVEX, en version 0.1.0. Sa fonction tient en une phrase : transformer l'observabilité brute de SYNE en **faits traçables, comparables et reproductibles** — et ne jamais produire autre chose.

## Un observatoire séparé, par principe

On pourrait croire qu'un observatoire est un accessoire : quelques compteurs dans le moteur suffiraient. Le projet en a fait un composant à part entière, et la raison est de fond. Si la mesure vivait dans le moteur, elle pourrait influencer le monde : un compteur qui consomme du hasard, un calcul qui retarde un tick, une agrégation qui lit un état à moitié mis à jour.

En séparant l'observatoire, LIVEX garantit que la mesure est **en aval** : ECHOS lit un flux, il ne le modifie pas. D'où une répartition nette des autorités. SYNE détient la vérité du monde ; ECHOS détient la vérité des **observations et des analyses**. L'observateur ne renvoie jamais de commande capable d'altérer l'état décisionnel, ne lisse pas les discontinuités, ne comble pas les trous : il les signale. Et lorsqu'il ne voit pas quelque chose, il le dit plutôt que de le deviner.

## Le rythme du flux

ECHOS ingère le WebSocket de SYNE en respectant strictement un ordre à chaque tick : d'abord le **snapshot**, état complet à ce tick ; puis les **événements**, conséquences appliquées ; enfin le **tick_summary**, agrégats consolidés. Cet alignement est vérifié, pas supposé. Un snapshot lu après coup, mélangé à des événements du tick suivant, produirait des mesures plausibles et fausses — le genre d'erreur qui ne se voit qu'au moment où l'on essaie de reproduire.

Un changement de `run_id` déclenche une chorégraphie précise : le nouveau run est enregistré **avant** son premier tick, les fenêtres glissantes sont réinitialisées, la série Parquet est vidée, un rapport propre est préparé. Et si un tick manque — un écart entre le dernier tick vu plus un et le tick reçu —, ECHOS le détecte, le journalise structurellement et incrémente `gaps_detected`. Si le tick 812 manque entre 811 et 813, le rapport le dira. Le rattrapage éventuel du moteur est signalé comme hors périmètre V0.2 : jamais approximé, jamais comblé en silence.

## Mesuré, ou seulement affiché

C'est ici que l'observateur devient un instrument plutôt qu'un tableau de bord. Chaque métrique porte un booléen `measured`, par couple moteur/métrique. `measured = true` signifie que la valeur a été réellement calculée depuis le flux pour la fenêtre considérée. `measured = false` signifie qu'un repli neutre a dû s'appliquer faute de fenêtre exploitable — et ce repli n'est jamais présenté comme une mesure.

La distinction est exposée par l'API, sur `/api/runs/{id}/metrics` et `/api/runs/{id}`. L'objectif est simple à formuler : **aucune métrique à zéro systématique ne peut passer inaperçue**. Un zéro peut vouloir dire « le monde est vide » ; il peut vouloir dire « je n'ai rien vu ». L'API empêche de confondre les deux.

Cette nuance paraît technique. Elle est en réalité la ligne de partage entre une observation et une croyance. Un chercheur qui lit `measured = false` sait qu'il ne dispose pas d'un fait, mais d'une absence de fait. Il peut alors relancer l'ingestion, vérifier le flux, corriger la fenêtre — au lieu de conclure hâtivement à une extinction.

## Ce que l'observatoire retient

Les faits vont quelque part : une base SQLite versionnée, organisée par usage. Les `tick_summaries` gardent les agrégats globaux par tick — population, moyennes, ressources. Les `tick_metrics` stockent les métriques normalisées, avec leur colonne `measured` et des valeurs bornées. Les `contexts` conservent le contexte `agents`, `groups`, `phenomena` et `profiling` à cadence contrôlée (`context_every`, 20 ticks par défaut), **plus** le dernier tick systématiquement conservé — de sorte que la fin d'un run n'est jamais perdue. Les `events` forment le journal observé, déterministe dans sa forme. Les `runs` portent les métadonnées : `run_id`, `seed`, `engineVersion`, `outcome`, `extinctionTick`, `viability`.

La migration est additive, pour préserver la compatibilité ascendante avec les bases existantes. Une campagne ne doit pas devenir illisible parce que l'outil a changé de version entre deux relevés.

## Le rapport de calibration

À la fin d'un run, ou sur demande, ECHOS produit un rapport `schemaVersion 2` qui se lit comme un verdict daté et signé. L'en-tête porte `run_id`, `seed`, `engineVersion`, horodatage et provenance. Le résultat populationnel dit si le monde a survécu — `outcome` (`extinct` ou `surviving`) — et, le cas échéant, `extinctionTick`, premier tick où `alive_count` vaut zéro.

Vient le bloc de viabilité : la pente d'énergie par tick sur les 200 derniers ticks (`energySlopePerTick`), la part des actions prises quand la faim est haute (`actionSharesWhenHungry`, déclenchée au-dessus d'une faim moyenne de 70), et le régime de ressources (`resourceRegime`) sur la fenêtre pertinente. Le `population_outcome` est recalculé en SQL, pour que l'API et la persistance disent la même chose.

Ce rapport est `observe-only` : il décrit, il n'altère jamais SYNE. Il ne sert pas à piloter le monde en cours de route, ni à corriger une trajectoire jugée décevante. Il sert à documenter ce qui a eu lieu, avec assez de précision pour qu'un autre puisse le contester.

## Comparer sans tricher

Comparer deux runs est le geste le plus utile de l'observatoire — et le plus tentant à truquer. L'API `/api/compare` sépare donc deux régimes. Par défaut, elle renvoie un résumé ; avec `light=1`, uniquement le résumé, sans séries ni empreinte. Seule la comparaison **complète**, fondée sur l'empreinte bit-à-bit, peut statuer sur « bit-identique » ou « reproductible ». Un verdict fort ne peut donc pas naître d'un aperçu léger.

C'est une règle de vocabulaire autant que de calcul. Dire « les deux runs sont identiques » après avoir comparé deux moyennes serait une faute. L'outil rend la faute difficile : il faut demander la comparaison complète pour obtenir la réponse forte.

## De la donnée à l'écran

L'interface `echos-ui` consomme cette API sans la déformer : sélection explicite du `run_id` — plus de « run le plus récent » implicite pour les vues contextuelles —, export CSV propre plutôt qu'une enveloppe JSON illisible, graphiques dont la clé de cache intègre la cadence, et vues ciblées (`beliefs`, `relationships`, `phenomena`) qui exigent un `run_id` déclaré.

Le Launcher, de son côté, délègue l'analyse par HTTP : appels standards courts, mais `ingest`, `analysis` et `report` bénéficient de délais étendus, jusqu'à 30 minutes, pour les ingests lourdes. La version d'ECHOS qui a produit un rapport est consignée dans le manifeste du paquet `.livexp` (`AnalysisComponentVersion`) : la traçabilité descend jusqu'à l'outil de mesure.

Les preuves suivent : la suite ECHOS passe **383 tests réussis, 0 échec**, avec une couverture mesurée de 91,32 %. Ce chiffre ne dit pas que les conclusions scientifiques sont vraies ; il dit que l'instrument fait ce qu'il annonce.

## L'observateur honnête

ECHOS ne cherche pas à lisser les discontinuités ni à combler les trous : il les signale. Il ne prétend pas mesurer ce qu'il ne voit pas : il l'indique, champ à l'appui. Cette discipline est ce qui permet à LIVEX d'affirmer des phénomènes sur des **bases factuelles et traçables**, plutôt que sur des impressions — et d'accepter aussi qu'un phénomène ne soit pas confirmé.

C'est elle, par exemple, qui rend possible de publier un verdict « non confirmé » sans le maquiller. Un observateur complaisant aurait trouvé une fenêtre, déplacé un seuil, appelé « cycle » une saturation. ECHOS n'a pas ce pouvoir — et c'est précisément ce qu'on lui demande.

Pour aller plus loin, la page [ECHOS du site](https://donovan-dev-web.github.io/LIVEX/technique/echos/) résume l'architecture et les contrats, le détail vivant dans le [dépôt](https://github.com/donovan-dev-web/LIVEX). Les prochains rapports de campagne, eux, sont annoncés dans le [journal](https://donovan-dev-web.github.io/LIVEX/actualites/) — suivez-le en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) pour les recevoir dès leur parution.
