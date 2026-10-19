---
title: "Reproductibilité et déterminisme au cœur de LIVEX"
description: "À graine identique, même trajectoire : pourquoi LIVEX traite la reproductibilité et le déterminisme comme des exigences scientifiques, non comme des options."
date: 2026-10-03
tags: ["reproductibilite", "determinisme", "methode", "contrats"]
draft: false
---

Vous lancez le monde à 20 h 14. Pendant quelques heures simulées, un groupe se forme près d'un point d'eau, se stabilise, se spécialise. Vous notez l'observation, vous la racontez — puis vous relancez. Cette fois, rien ne se forme. Les agents passent, repartent, se croisent sans jamais s'installer. Lequel des deux runs a raison ?

Sans moyen de rejouer, aucun des deux. Vous ne détenez pas un résultat : vous détenez une anecdote. C'est exactement ce que LIVEX refuse. Si l'on veut pouvoir dire « voici la chaîne de causes qui a produit ce groupe », il faut pouvoir **rejouer** le même monde. La reproductibilité et le déterminisme ne sont donc pas des options de confort : ce sont les conditions de possibilité d'une affirmation scientifique.

## Une anecdote n'est pas un résultat

L'émergence est une notion facile à invoquer et difficile à établir. Elle se prête aux récits : une configuration frappante, une capture d'écran, une courbe qui monte. Mais un récit ne survit pas à la question suivante : « est-ce que cela se reproduit ? »

Reproduire ne signifie pas obtenir « quelque chose de ressemblant ». Cela signifie obtenir la même trajectoire, ou pouvoir expliquer précisément pourquoi elle diffère. Sans cette exigence, l'interprétation l'emporte sur le fait. On finit par décrire du bruit avec de belles courbes, et par appeler « émergence » ce qui n'était qu'une fluctuation.

## Trois exigences, un même dispositif

La question directrice de LIVEX exige une réponse instrumentée, pas argumentative. Trois exigences en découlent.

La première est la **réponse factuelle** : « à cette graine, sous cette configuration cohérente, ce phénomène apparaît au tick N ». Non « cela semble arriver parfois ». La deuxième est la **validation par rejeu** : un observateur indépendant doit obtenir le même résultat en rejouant ; sinon, l'affirmation reste une opinion. La troisième est la **traçabilité** : toute affirmation doit remonter à des interactions locales, jusqu'à la décision et à l'événement qui l'a produite.

Ces trois exigences ne se satisfont pas avec un seul outil. Elles demandent une chaîne complète, du moteur qui décide à l'observatoire qui mesure, en passant par l'orchestrateur qui archive.

## SYNE, ou le déterminisme par construction

SYNE, le moteur de simulation, est conçu pour un déroulement déterministe à partir d'entrées contrôlées. Trois disciplines en tiennent l'essai.

### Une seule graine, un seul générateur

Le générateur pseudo-aléatoire est initialisé une seule fois, à partir de `seed`, et tous les tirages passent par lui. Ce choix a une conséquence que l'on sous-estime souvent : l'état du générateur doit être **sérialisable**. C'est ce qui permet de sauvegarder un monde en pleine course et de le reprendre exactement, plutôt que de le rejouer depuis le début.

De là vient une règle qui surprend : l'usage du générateur standard du cadre d'exécution est interdit. Son état n'est pas portable d'une plateforme à l'autre, et son algorithme peut changer d'une version du cadre à l'autre. Deux runs « identiques » exécutés ailleurs divergeraient alors en silence. Interdire n'est pas un caprice : c'est fermer une porte à une divergence invisible.

### Un ordre causal qui ne bouge pas

L'avancement est discret, en ticks, sans horloge système non maîtrisée dans le cœur décisionnel. À chaque tick, l'ordre est invariant : **perception → évaluation → sélection d'action → application des effets → mises à jour → fin de tick**.

Aucune décision ne peut donc être tranchée par un `DateTime.UtcNow` qui varierait d'une machine à l'autre. L'heure simulée (`simulatedTimeSeconds`) est dérivée du tick et de l'échelle temporelle (`simulatedSecondsPerTick`). Deux exécutions ne « pensent » pas à des moments différents : elles pensent dans le même temps.

### Reprendre sans rejouer

Le monde se capture au complet — état, cognition, générateur — et se restaure à l'identique. Un run interrompu, relancé depuis son instantané, doit produire exactement la suite qu'aurait eue un run ininterrompu. Les tests de reprise vérifient cette égalité, tick après tick, plutôt que de la supposer.

La persistance SQLite accompagne cette promesse : schéma versionné, transactions atomiques, reprise au dernier état valide, rotation des sauvegardes bornée pour éviter les effets collatéraux. Un monde peut être mis en pause, déplacé, repris — sans que la trajectoire s'en trouve changée.

## ECHOS, l'observateur qui ne touche à rien

De l'autre côté du flux, ECHOS consomme l'observabilité de SYNE **sans jamais modifier l'état**. Trois disciplines y veillent.

Le flux suit un ordre strict, « snapshot puis événements », tick après tick. ECHOS vérifie cette continuité, détecte les **discontinuités** — les trous — et les consigne plutôt que de les masquer. Chaque métrique porte sa **provenance** : elle indique si elle est mesurée, ou si un repli neutre a dû s'appliquer faute de fenêtre exploitable. Rien de reconstitué n'est présenté comme mesuré. Enfin, les comparaisons restent honnêtes : `/api/compare` distingue un résumé léger (`light=1`) d'une comparaison complète fondée sur l'empreinte bit-à-bit, et le verdict « bit-identique » n'est émis que lorsque les conditions permettent de l'établir.

Les rapports de calibration sont versionnés (`schemaVersion 2`) et dérivés des séries persistées, sans jamais être réinjectés dans le moteur. L'observatoire écrit ses lectures ; il ne réécrit pas le monde.

## Contrôler ce qui entre

Rejouer suppose aussi de **maîtriser les entrées**. La configuration reste cohérente : profils `reference` et `prism`, surcouches partielles appliquées uniformément, car `start`, `prepare` et `reset` suivent les mêmes règles. Un seul jeu de valeurs est maintenu par profil, pour éviter la dérive silencieuse.

Chaque exécution porte un **`run_id` canonique** (`run-<seed>-<12hex>`), qui rend traçable le lien entre la graine, l'exécution et ses artefacts : paquets `.livexp`, rapports ECHOS. Le Launcher orchestre en conséquence : il dérive les graines, applique les politiques d'échec de manière explicite, consigne `provenance.json` dès la création du run — et en reprise, il ne rejoue pas les runs déjà validés, empreintes et journaux vérifiés d'abord.

Reste le temps. `simulatedSecondsPerTick` modifie la représentation temporelle du monde, et `metersPerUnit` celle de l'espace, à titre informatif. Mais les **effets par action** et l'ordre causal restent inchangés. L'horloge est instanciée au démarrage : elle n'est pas une variable globale que quelque chose pourrait glisser en douce.

## La preuve par les oracles

Tout cela ne vaut que s'il est vérifié. Le projet traduit donc ses principes en preuves concrètes.

Des **oracles de checksum** épinglent des états dorés sur les parcours critiques : toute trajectoire qui dévie casse un test. Quand le changement est volontaire, il s'accompagne d'un bump `engineVersion` en MINOR. La règle tient en deux lignes : version inchangée, trajectoire inchangée ; trajectoire changée, version incrémentée et empreintes recalculées.

Les campagnes multi-seeds donnent à cette règle sa matière. Dix graines ont été exécutées sur 2 500 ticks : cent agents vivants sur chacune, et dix checksums distincts. La graine 12345, répétée trois fois — dont une avec export de flux —, retrouve chaque fois la même empreinte, `0xdb5979d695a2dc6a`. L'export ne change pas l'état ; le rejeu ne change pas la trajectoire. Des **tests bout-en-bout** enchaînent un SYNE publié et un ECHOS réel, y compris sous Windows, avec vérification octet à octet des paquets `.livexp` et des rapports archivés. Et la détection de trous n'est pas décorative : les discontinuités de ticks sont journalisées structurellement et comptées (`gaps_detected`), jamais lissées. À chaque relevé, la chaîne repasse en **0 échec**.

## Une discipline, pas un supplément d'âme

Dans LIVEX, « ça fonctionne » ne suffit pas. « Ça se rejoue » est la norme. Cette discipline est contraignante : elle impose de contrôler ce qui entre, de versionner ce qui change, de nommer ce qu'on ne mesure pas. Mais elle est aussi ce qui transforme une curiosité — « peut-il émerger quelque chose ? » — en une **affirmation vérifiable** : « dans ces conditions précises, oui, et voici comment le rejouer ».

C'est précisément cette rigueur qui donne du poids à tout phénomène observé. S'il émerge, il doit pouvoir réapparaître à l'identique. Et s'il ne réapparaît pas, vous saurez au moins que ce n'était pas une propriété du monde, mais un accident de votre instrument.

Pour le détail, le [chapitre 11 de la monographie](https://github.com/donovan-dev-web/LIVEX/blob/main/docs/monographie/11-reproductibilite-determinisme.md) déroule la promesse, sa vérification et ses limites. Les prochains relevés paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) — et un ★ sur le [dépôt](https://github.com/donovan-dev-web/LIVEX) aide ce travail à être vu.
