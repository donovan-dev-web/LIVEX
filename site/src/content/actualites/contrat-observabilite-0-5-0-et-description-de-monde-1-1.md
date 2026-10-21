---
title: "Contrat d'observabilité 0.5.0 et description de monde 1.1"
seoTitle: "Contrat d'observabilité 0.5.0 et monde 1.1"
description: "Le contrat d'observabilité passe en 0.5.0 et la description de monde en 1.1 : champs additifs, convention du null et parité syne-mock mesurée."
date: 2026-10-18
tags: ["contrat", "observabilite", "syne", "parite"]
draft: false
---

Un champ vaut `null`. Un autre vaut `0`. À l'écran, dans un tableau de bord, ces deux valeurs se ressemblent tellement qu'on les confond sans y penser. Et pourtant, tout le sens d'une mesure tient dans l'écart entre les deux : d'un côté, l'agent n'a été observé nulle part ; de l'autre, il n'a pas bougé. Publier la première comme si c'était la seconde, c'est inventer un fait.

C'est cette frontière que le nouveau **contrat d'observabilité 0.5.0** rend explicite dans l'interface que SYNE expose à ses consommateurs. Et c'est pourquoi une mise à jour technique mérite qu'on s'y arrête.

## Qui parle à qui

Le contrat d'observabilité est l'interface par laquelle le moteur expose son état. Quatre consommateurs s'y branchent, chacun avec son métier : ECHOS mesure, PRISM affiche, syne-mock émule, le Launcher orchestre. Quand ce contrat bouge, tout le monde bouge — ou personne ne reste compatible.

La bonne nouvelle, c'est que cette version 0.5.0, comme la description de monde 1.1 qui l'accompagne, avance par **ajout seul**. Aucun champ requis n'a été supprimé, aucun type n'a changé sous les pieds des consommateurs existants. Une évolution de contrat qui casse ses clients n'est pas une évolution, c'est une rupture déguisée ; ici, la seule rupture est celle du silence — on parle désormais davantage.

## Ce qui apparaît en 0.5.0

Trois ajouts méritent d'être nommés, tous optionnels et documentés dans `API_CONTRACTS.md`.

Le premier sert l'instrumentation de la cognition : le snapshot porte désormais la distance **maximale** et **moyenne** des paires liées (`maxBondDistance`, `meanBondDistance`). Ce sont des mesures de terrain, et elles obéissent à la convention maison : une valeur non mesurée vaut `null`, jamais zéro.

Le deuxième est une horloge. Le snapshot expose `simulatedTimeSeconds`, le temps simulé cumulé. Les minutes entières sont conservées pour ne rien casser, mais un consommateur peut désormais raisonner en secondes réelles du monde plutôt qu'en ticks.

Le troisième relie les deux : le point de contrôle `/api/control/status` expose `simulatedSecondsPerTick`, pour les clients qui pilotent le moteur et ont besoin de savoir à quelle vitesse le temps passe.

### La convention du null

Cette convention traverse tout le contrat, et elle est simple à énoncer : une métrique **absente** — pas de paires liées, par exemple — vaut `null`. Un **zéro observé** — une distance réellement mesurée et nulle — vaut `0`. Confondre les deux fausse l'analyse. C'est exactement le piège qui avait été corrigé dans le harnais de la campagne A0-4, où un recensement légitimement nul avait été pris pour une mesure manquante.

## La description de monde passe en 1.1

La description de monde, émise une seule fois par exécution, répond à une question que le rendu pose avant toute autre : **de quel monde parle-t-on ?** En 1.1, elle porte désormais l'échelle temporelle et spatiale, ce qui évite au rendu de réinventer cette traduction.

| Champ | Type | Signification |
| :-- | :-- | :-- |
| `simulatedSecondsPerTick` | nombre | Secondes simulées par tick (défaut 60) |
| `metersPerUnit` | nombre | Mètres par unité spatiale (informatif) |
| `worldSize` | objet | Dimensions de la grille |
| `seed` | nombre | Graine du run |

Deux sources publient cette géométrie : le moteur SYNE et l'émulateur syne-mock. Pour qu'on ne se contente pas de l'espérer, un harnais les compare champ par champ : **4 900 cases identiques, 11 bloquées, 0 divergence**. La parité est mesurée, pas supposée.

## La parité syne-mock

syne-mock existe pour une raison précise : permettre de développer et de tester un client sans lancer le moteur .NET. Il émet le même transport, la même forme de messages, la même description de monde. Il n'émet pas la même simulation — on ne lui demande pas de reproduire fidèlement les décisions, le pathfinding ni les systèmes sociaux de SYNE. Sa fidélité porte sur les **contrats**, pas sur le contenu sémantique profond.

C'est une distinction qu'il faut garder en tête : syne-mock est fiable pour vérifier qu'un client parle correctement, et inutilisable pour établir un résultat scientifique.

## Les preuves, une par une

Le contrat ne se déclare pas, il se vérifie. La version est portée par le code : `ObservabilityContract.Version = "0.5.0"` et `EngineVersion = "0.17.0"`. syne-mock recopie ces valeurs depuis sa propre configuration, et un test vérifie qu'il ne dérive pas. Le harnais de parité confirme les 4 900 cases identiques et 11 bloquées, sans divergence. Côté rendu, les specs PRISM-LDK parsent de véritables trames.

## Migrer sans se blesser

Pour un consommateur déjà branché, la marche à suivre se résume à trois gestes. Lire `simulatedTimeSeconds` s'il est présent, et sinon calculer le temps depuis le tick et `simulatedSecondsPerTick`. Traiter `maxBondDistance` et `meanBondDistance` comme des nombres possiblement nuls. Et surtout, ne jamais casser si un champ nouveau est absent : la compatibilité ascendante vaut dans les deux sens.

## Un contrat qui dit ce qu'il ne sait pas

La vraie valeur de la version 0.5.0 tient moins à ses champs qu'à sa discipline. Elle rend explicites l'échelle du temps et celle de l'espace, et elle nomme l'absence au lieu de la déguiser en zéro. Un contrat qui distingue « je ne sais pas » de « j'ai mesuré zéro » est un contrat auquel on peut confier une conclusion scientifique.

Le détail des champs, des versions et des règles de migration est consigné dans `docs/syne/API_CONTRACTS.md` et dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les évolutions de contrat, elles, seront annoncées ici, dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/) — suivez-le en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) pour ne manquer aucune version.
