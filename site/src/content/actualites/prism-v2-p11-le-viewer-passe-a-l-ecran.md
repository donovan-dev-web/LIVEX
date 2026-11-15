---
title: "P11 : le viewer PRISM passe à l'écran"
description: "Le viewer PRISM V2 est livré : rendu du monde, inspecteur, sept calques, modes client, embarqué et rejeu, et plan de contrôle relayé à l'hôte. Ce qui est vérifié, et ce qui reste à prouver."
seoTitle: "Viewer PRISM V2 : P11 livrée"
date: 2026-11-15
tags: ["prism", "v2", "viewer", "launcher", "livex"]
draft: false
---

Un monde headless est vérifiable ; il n'est pas regardable. Pendant longtemps, PRISM V2 savait donc produire des ticks, des empreintes et des chaînes de production sans qu'on puisse voir ce qu'ils fabriquaient. La phase P11 répond à ce manque : un viewer lit le flux du monde, l'affiche, et permet de l'inspecter sans jamais en devenir l'autorité.

## Ce qui est à l'écran

Le rendu couvre désormais le monde réel, pas une maquette : terrain maille par regions avec budget par image et culling de frustum, atlas procédural généré au démarrage, cycle jour/nuit déterministe, entités interpolées depuis le magasin, chantiers en fantômes remplis au prorata du travail accompli, objets au sol colorés par leur fraîcheur.

L'inspection suit la même règle : le viewer montre ce que le contrat publie. La caméra orbite ou se libère, le suivi d'un agent se fait par identifiant, la vue en coupe laisse voir les blocs enterrés, l'inspecteur lit le magasin live et sept calques superposent zones, ressources, fraîcheur, chemins, structures, propriété et construction. Ce qui n'est pas publié par le contrat — maîtrises, croyances, plans cognitifs — n'est pas inventé à l'écran.

Trois modes coexistent : client (`--connect`), embarqué et rejeu. Le mode client se branche sur un hôte séparé ; le mode embarqué héberge un hôte pour une démonstration ; le rejeu relit un flux enregistré (`--record`) à cadence contrôlée. Le module de capture PNG (`--screenshot`, `F12`) complète l'outillage.

## Le contrôle, sans usurper l'autorité

Un viewer qui peut mettre en pause n'est plus tout à fait un simple lecteur. La réponse retenue est explicite : le viewer expose `play`, `pause`, `stop` et `quit`, mais chaque commande est **relayée à l'hôte**, qui reste l'autorité du monde. Le Launcher propose deux modes de session, **Hôte** et **Viewer** ; la pile reste au tick `-1` jusqu'au **Play**.

La chaîne a été vérifiée en conditions réelles : préparation à `-1`, Play au tick 338, pause stable à 343, reprise à 499, arrêt à 503, puis `quit` en HTTP 202 avec code de sortie 0. Aucun tick n'est exécuté avant le Play, et le viewer ne modifie jamais l'état du monde.

## Ce que P11 ne prouve pas encore

Le code, le manifeste `prism/viewer.component.json`, les tests et la chaîne gatée sont livrés : la suite PRISM compte **525/525 tests**, dont la totalité des 100 tests du viewer. Cela ne clôt pas encore la phase.

Deux preuves restent différées : un **relevé B8 publiable** des performances de rendu sous Windows, et une **validation graphique sous Linux** avec une fenêtre réelle. Le viewer a été vérifié à l'écran sous Windows ; aucune capture du viewer V2 n'est publiée sur ce site, précisément parce qu'une image ne remplace pas ces deux mesures.

P11 est donc livrée, et non close. La distinction est la même que partout dans LIVEX : ce qui est montré doit pouvoir être rejoué, et ce qui reste à prouver est nommé.
