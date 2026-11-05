---
title: "Cultiver prend du temps : l'agriculture prévue dans PRISM V2"
description: "Sol labouré, eau proche, croissance saisonnière et récolte : comment l'agriculture doit former une chaîne de plusieurs centaines de ticks."
seoTitle: "Agriculture et saisons dans PRISM V2"
date: 2026-11-04
tags: ["prism", "v2", "agriculture", "saisons"]
draft: false
---

Une récolte n'est pas une réserve que l'on peut simplement appeler. Dans la cible PRISM V2, l'agriculture doit demander une suite d'actions et une attente réelle : trouver une graine, préparer le sol avec une houe, planter près de l'eau, puis laisser la culture croître avant de la récolter.

La culture prévue progresse par stades programmés. Son rythme dépend de la saison : le printemps l'accélère, l'été sert de référence, l'automne le ralentit et l'hiver peut suspendre la croissance. Si l'eau ou le sol ne répond plus aux conditions, la croissance s'arrête. Une culture mûre qui reste trop longtemps sans être récoltée risque de se flétrir.

## Une récolte qui s'inscrit dans le temps

La spécification prend comme repère une croissance de plusieurs centaines de ticks. Ce délai donne un poids aux décisions prises avant la récolte : où planter, qui entretient la parcelle, quand revenir et combien de nourriture conserver.

L'eau relie l'agriculture à une autre chaîne de production. Une source peut être éloignée ; transporter un seau ou construire un puits demande de la capacité et du travail. Un grenier peut ralentir la péremption, mais il faut encore rassembler les matériaux nécessaires à sa construction.

## Ce que l'expérience devra vérifier

Ces règles créent des dépendances possibles entre préparation, entretien, transport et stockage. Elles ne prouvent pas que les agents vont s'organiser collectivement. Les campagnes prévues devront vérifier si le délai agricole et le risque de perdre une récolte modifient la survie ou les échanges, et dans quelles conditions.

Les durées et facteurs saisonniers sont des paramètres de conception à calibrer. Cet article présente l'agriculture telle qu'elle est prévue dans PRISM V2, sans annoncer de résultat expérimental.
