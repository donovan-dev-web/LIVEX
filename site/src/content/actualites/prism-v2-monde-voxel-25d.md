---
title: "Un monde voxel en colonnes : le terrain prévu pour PRISM V2"
description: "Pourquoi la cible PRISM V2 choisit un monde 2.5D borné, découpé en régions et généré à partir d'une graine."
seoTitle: "Le monde voxel 2.5D de PRISM V2"
date: 2026-11-04
tags: ["prism", "v2", "voxel", "monde"]
draft: true
---

Un monde en trois dimensions n'a pas besoin d'être sans limites ni rempli de cavernes pour offrir de vrais choix. Pour PRISM V2, la spécification retient un monde de colonnes : chaque position horizontale possède une surface et des couches de matière en altitude.

Le format prévu est un monde 2.5D borné de **256 × 256 colonnes**, avec une altitude comprise entre 0 et 63. Les colonnes sont regroupées en régions de **16 × 16**. Cette organisation donne un terrain lisible et permet de traiter, enregistrer et transmettre les parties modifiées sans reconstruire tout le monde à chaque changement.

## Une géométrie volontairement lisible

La cible ne comprend ni cavernes ni surplombs. Les agents se déplacent sur la surface, peuvent monter une marche et franchir certains dénivelés ; les couches profondes deviennent accessibles lorsqu'elles sont exposées, par exemple après une extraction. Cette limite réduit la complexité du modèle et rend les règles spatiales plus faciles à interpréter.

La génération doit partir d'une graine. À configuration et contenu identiques, celle-ci permet de retrouver le même terrain. Les matériaux, leur dureté, leurs tags et les objets qu'ils peuvent produire sont définis dans un pack de données. La géométrie pose le cadre ; le contenu donne aux blocs leurs propriétés.

## Ce que le voxel signifie pour un agent

Un bloc n'est pas une ressource abstraite stockée dans une réserve globale. Il occupe une position. Pour le transformer en objet, un agent doit le percevoir, s'en approcher, avoir l'outil requis et attendre la durée d'extraction. L'objet obtenu rejoint ensuite son inventaire ou le sol.

Cette différence ouvre des questions concrètes : où les ressources apparaissent-elles ? Combien de temps faut-il pour les atteindre ? Les agents peuvent-ils transporter assez de matière pour bâtir loin de leur point de départ ? Le modèle ne répond pas à ces questions par un scénario ; il fournit des règles pour les étudier.

La cible PRISM V2 décrit cette géométrie et ses limites. Le [résumé de PRISM V2](https://donovan-dev-web.github.io/LIVEX/technique/prism/) présente la place du terrain dans la refonte complète.
