---
title: "SYNE : un moteur décisionnel déterministe pour l'émergence"
description: "SYNE, le moteur de LIVEX : un monde à ticks, des agents autonomes, un déterminisme contrôlé et une reprise exacte. Plongée dans la mécanique."
date: 2026-10-04
tags: ["syne", "moteur", "agents", "determinisme", "persistance"]
draft: false
---

Retirez l'écran. Il reste un monde qui tourne : une grille de cellules, des réserves qui se renouvellent, des dizaines d'entités qui ont faim, se fatiguent, se souviennent et choisissent. Personne ne regarde, et pourtant tout se décide. C'est **SYNE** (*Systems & Emergent Network Engine*), le moteur de simulation multi-agents de LIVEX, en version 0.17.0. Sa responsabilité tient en une phrase : détenir la **vérité de l'état du monde**.

Tout le reste du projet est bâti autour de ce fait. ECHOS mesure ce que SYNE produit, PRISM montre ce que SYNE décide, le Launcher archive ce que SYNE a livré. Aucun de ces composants ne réécrit l'état simulé, et la logique décisionnelle s'exécute indépendamment du rendu. Cette séparation n'est pas décorative : elle est ce qui rend la question de LIVEX testable, puisqu'il n'existe qu'un seul endroit où les lois du monde vivent.

## Un seul endroit où les lois vivent

On pourrait imaginer un moteur qui décide, un rendu qui influence les décisions et un tableau de bord qui recalcule l'état à sa façon. Ce serait plus simple à brancher, et beaucoup plus difficile à croire. Si trois composants peuvent modifier le monde, une observation ne prouve plus rien : on ne sait plus si le phénomène vient des lois ou de l'un des outils.

SYNE ferme cette porte. Il expose son état, il ne le délègue pas. Les autres composants ont des rôles précis et bornés : ECHOS observe sans influer, PRISM visualise sans décider, le Launcher orchestre sans réinterpréter. Cette autorité unique est ce qui permet de poser la question « qu'émerge-t-il de ces règles seules ? » — et d'y répondre par un rejeu, pas par une impression.

## Cinq principes, un espace étroit

Le moteur tient sa promesse par cinq principes qui reviennent partout.

Le **déterminisme**, d'abord : à graine et configuration identiques, l'exécution produit un déroulement reproductible. L'**autonomie locale**, ensuite : les agents décident depuis ce qu'ils perçoivent — une part seulement du monde — avec leur mémoire, leurs croyances, leurs besoins et leurs capacités d'action. L'**émergence traçable** : les structures collectives ne sont pas programmées ; elles résultent d'interactions dont on peut retracer les causes. L'**observation non intrusive** : SYNE diffuse son état sans attendre de retour capable d'influencer ses décisions. Et la **reprise exacte** : une persistance bit-à-bit qui permet de restaurer le monde à un tick donné.

Cinq principes qui, ensemble, dessinent un espace étroit : presque rien n'est laissé au hasard non contrôlé, presque rien n'est laissé à la discrétion du rendu. C'est exactement l'intention.

## Un tick, six temps

SYNE opère en temps discret : le monde avance par ticks. Et un tick se lit presque comme une phrase.

### Percevoir

Chaque agent regarde autour de lui — à portée bornée, de travers, avec ce que sa mémoire garde. Le monde est partiellement observable, et c'est voulu. L'information imparfaite n'est pas une approximation regrettable : c'est un moteur de diversité comportementale. Deux agents placés au même endroit ne voient pas la même chose, parce qu'ils n'ont pas la même histoire.

### Évaluer et choisir

Vient ensuite l'évaluation. Besoins, fatigue, énergie, traits et tempéraments, croyances et contexte social sont pondérés dans une **formule d'utilité**, qui rend l'arbitrage lisible chiffre par chiffre. Puis l'action la plus apte l'emporte, compte tenu des contraintes locales.

L'agent ne cherche pas l'optimum mondial : il ne peut pas, il ne voit pas le monde. Il cherche un **compromis local plausible**. C'est précisément là que l'intérêt commence. Des compromis locaux, répétés par des entités imparfaites, sont le seul matériau dont disposent les structures collectives.

### Appliquer, mettre à jour, refermer

L'action s'applique — déplacement, interaction, consommation, communication. Les mises à jour en tirent les conséquences : besoins, énergie, fatigue, mémoire, croyances, réserves, événements. Enfin, le tick se referme : un snapshot, puis les événements, partent vers les consommateurs.

Cet ordre est invariant, et aucun tirage ne dépend de l'horloge système pour trancher une décision. Cette phrase est le cœur du moteur : tant que l'ordre tient, la trajectoire tient.

## L'agent, de l'intérieur

Un agent SYNE n'est pas un script mobile. Il porte des **besoins** — faim, soif, sécurité, social — avec leurs seuils et leur dérive ; une **énergie et une fatigue** qui coûtent et se réparent ; une **mémoire et des croyances** mises à jour graduellement par les interactions ; une **perception partielle** qui l'empêche de tout savoir ; des **traits** qui biaisent durablement ses évaluations ; un ensemble **borné d'actions** selon le contexte ; et une **communication locale** elle aussi bornée, en envois et réceptions par tick, pour éviter des effets globaux non maîtrisés.

Prenez une scène ordinaire. Un agent a faim. Il aperçoit un voisin, et doit arbitrer entre manger, s'approcher ou se reposer. Il ne dispose pas d'une carte du monde ni d'un plan général : il a une croyance sur un gisement, une mémoire de la dernière interaction, une jauge d'énergie qui baisse. Sa décision ne sera peut-être pas la meilleure. Elle sera la sienne.

C'est cette modestie qui rend le système intéressant. Un agent qui optimiserait globalement produirait une trajectoire unique, prévisible, sans histoire. Un agent qui compose avec ce qu'il perçoit produit des trajectoires différentes — et parfois, sans que personne l'ait prévu, des régularités.

## Le monde autour

Le monde est une grille à **cellules discrètes**. Il gère des **ressources** — nourriture, eau, minéraux — qui se régénèrent ou se dégradent périodiquement selon la configuration ; des **obstacles statiques**, tracés dans le monde, dont la révision rafraîchit le cache de chemin (A*) quand l'environnement change ; un rayon d'interaction borné et une perception locale.

L'échelle temporelle se règle par `simulatedSecondsPerTick` (60 par défaut, soit une minute simulée par tick), `metersPerUnit` restant informatif. L'horloge est instanciée au démarrage : les conversions s'appliquent une fois, et les effets par action demeurent inchangés. Changer la représentation du temps ne change pas ce que le monde fait ; cela change seulement la façon dont on le raconte.

## Sauver, piloter, diffuser

Trois services tiennent le monde à jour et l'ouvrent au reste du système.

La **persistance SQLite** — schéma versionné, onze tables typiques, transactions atomiques, sauvegarde automatique périodique, rotation de backups bornée — permet la reprise exacte au dernier `tick_states`. Le **serveur de contrôle HTTP** (`--serve`, port 5181) expose `start`, `pause`, `resume`, `stop`, `reset` et `status`. `reset` accepte une surcouche `config`, avec la même logique que `start` et `prepare`, ce qui permet d'enchaîner des runs cohérents sans y penser à chaque fois. Le **flux d'observabilité**, en WebSocket, diffuse **snapshot puis événements** à chaque tick, avec `simulatedTimeSeconds`, `seed` et `runId` ; l'ordre de diffusion y est garanti.

Le tout se réfère à des contrats écrits : `API_CONTRACTS.md`, `TICK_QUANTITIES.md` — l'inventaire des quantités échangées à chaque tick — et `OBSERVABILITY.md`. Chaque exécution porte son `run_id` canonique, `run-<seed>-<12hex>`, lisible de SYNE à ECHOS comme dans les artefacts archivés.

## Deux profils, deux intentions

Le profil **`reference`** est calibré pour la stabilité sur 2 500 ticks. Sur six runs — trois graines, deux tailles de population —, l'objectif explicite est de tenir sans extinction : 0 extinction, pente d'énergie sous le seuil, population finale proche de la population initiale. Ce n'est pas une promesse d'immortalité : c'est un cadre de mesure, avec des seuils fixés avant l'exécution.

Le profil **`prism`**, lui, est orienté rendu perceptif : monde plus grand, cellules adaptées, `simulatedSecondsPerTick` à 5, `metersPerUnit` à 1. Il est strictement additif : il change la représentation du monde, jamais son ordre causal. On peut donc brancher un rendu sans modifier la science — et c'est exactement ce que PRISM, encore en développement, viendra chercher.

## Les preuves, et ce qu'elles ne disent pas

SYNE maintient des **oracles dorés** — des checksums épinglés dont toute variation signale une trajectoire qui a bougé. Un changement volontaire se justifie par un bump `engineVersion` en MINOR, dans le même commit que le recalcul des empreintes : un changement annoncé est légitime, un changement silencieux ne l'est pas. Les campagnes multi-seeds, les tests de reprise bit-à-bit, les golden tests et les parcours bout-en-bout contre ECHOS réel valident continûment ces garanties. Les suites du moteur affichent **695/695, 0 échec**.

Il faut pourtant dire ce que ces chiffres ne prouvent pas. La viabilité n'est établie que pour les graines, configurations et horizons explicitement rapportés. Rien ici ne démontre une viabilité universelle, ni une émergence générale. Les tests confirment leurs assertions ; ils ne certifient pas une conformité totale à la monographie.

Et c'est très bien ainsi. Un moteur qui promettrait l'émergence serait suspect. Un moteur qui promet la **rejouabilité** de ce qui se produit est vérifiable.

## L'autorité, pas la promesse

Au fond, SYNE est l'autorité : ECHOS observe sans influer, PRISM visualise sans décider. C'est cette séparation qui permet à LIVEX de poser la question « qu'émerge-t-il de ces règles seules ? » — et d'y répondre par un **rejeu reproductible**, pas par une interprétation. Le moteur ne promet pas que quelque chose d'intéressant se produira ; il promet que, si quelque chose se produit, vous pourrez le revoir, le mesurer et le discuter.

Pour descendre dans le détail, le [guide SYNE](https://github.com/donovan-dev-web/LIVEX/blob/main/syne/README.md) décrit le monde, la cognition, la boucle et la persistance ; la page [SYNE du site](https://donovan-dev-web.github.io/LIVEX/technique/syne/) en donne la vue courte. Et pour ne manquer ni la prochaine campagne ni ses résultats, le [journal](https://donovan-dev-web.github.io/LIVEX/actualites/) reste le fil — en [RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) si vous préférez que ce soit le flux qui vienne à vous.
