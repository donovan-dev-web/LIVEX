---
title: "Du minerai à l'outil : fabriquer et stocker dans PRISM V2"
description: "La cible PRISM V2 remplace les réserves globales par des objets, des recettes, des stations et des inventaires soumis à des coûts."
seoTitle: "Fabrication, outils et stocks dans PRISM V2"
date: 2026-11-04
tags: ["prism", "v2", "fabrication", "ressources"]
draft: false
---

Dans la cible PRISM V2, obtenir une ressource n'est que le début d'une chaîne. Un agent pourrait extraire de la pierre, fabriquer une tête et un manche, puis assembler une pioche à un établi. Pour atteindre le fer, il lui faudrait ensuite le bon outil, du minerai, du charbon et un four.

Les recettes sont prévues comme des données : elles indiquent les entrées, les sorties, la station nécessaire et la durée. Elles peuvent utiliser des tags — « combustible », « outil », « minerai » — plutôt qu'une liste figée de chaque objet. Cela doit permettre d'étendre le contenu sans réécrire les règles du moteur.

## Transporter a un coût

Les inventaires ont une capacité limitée. Les objets peuvent être déposés dans des conteneurs, déplacés entre agents ou laissés au sol. Les outils s'usent à l'usage et doivent être remplacés. Certains aliments périssent ; un coffre ou un grenier ralentit leur perte de fraîcheur.

La matière suit des règles explicites : une recette consomme ses ingrédients et produit ses sorties, un outil cassé est retiré, un aliment périmé est comptabilisé comme détruit. Le registre prévu doit permettre de suivre ces entrées et sorties, plutôt que de faire apparaître ou disparaître des objets sans explication.

## Une chaîne de production, plusieurs décisions

Fabriquer implique de décider quoi transporter, quelle station utiliser et quelle quantité garder en réserve. Les chaînes de production créent ainsi des coûts fixes, des délais et des besoins logistiques. Elles pourraient donner un intérêt au partage d'un four ou à l'échange d'outils, mais ces effets restent une hypothèse à tester.

Le monde ne récompensera pas automatiquement les groupes. Les expériences prévues devront comparer les situations avec et sans certains mécanismes, puis mesurer ce qui change. Un résultat où les agents ne coopèrent pas serait lui aussi informatif.

Les recettes, outils, stocks et règles de conservation décrits ici appartiennent au périmètre prévu de PRISM V2 ; ils ne signifient pas que la chaîne complète est déjà validée.
