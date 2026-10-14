---
title: "Reproductibilité et déterminisme au cœur de LIVEX"
description: "À graine identique, LIVEX vise un rejeu aligné sur ses oracles. Déterminisme, traçabilité et contrôle strict des entrées guident chaque choix."
date: 2026-10-03
tags: ["reproductibilite", "determinisme", "tests", "contrats"]
draft: false
---

Si l'on veut pouvoir dire « voici la chaîne de causes qui a produit ce groupe », il faut pouvoir **rejouer** exactement le même monde. C'est pourquoi la reproductibilité et le déterminisme sont des exigences structurantes dans LIVEX, pas des options.

## Pourquoi c'est indispensable

La question directrice exige une réponse instrumentée, pas argumentative. Pour cela :

- **Réponse factuelle** : « à cette graine, sous cette configuration cohérente, ce phénomène apparaît au tick N ».
- **Validation par rejeu** : un observateur indépendant doit obtenir le même résultat en rejouant.
- **Traçabilité** : toute affirmation doit remonter à des interactions locales traçables.

Sans reproductibilité, l'interprétation l'emporte sur le fait. Sans déterminisme contrôlé, on ne peut pas distinguer un effet structurel d'une fluctuation.

## SYNE : déterminisme par conception

SYNE est conçu pour un déroulement **déterministe** à partir d'entrées contrôlées. Quelques principes guident cette exigence :

- **Graine unique** : le PRNG est initialisé une seule fois à partir de `seed`. Tous les tirages aléatoires passent par ce générateur.
- **Boucle à ticks** : l'avancement est discret, sans horloge système non maîtrisée dans le cœur décisionnel.
- **Ordre causal strict** : perception → évaluation (formule d'utilité) → sélection d'action → application des effets → mises à jour → fin de tick. Cet ordre est invariant.
- **Pas d'effets non-déterministes** dans le cœur** : pas de `DateTime.UtcNow` utilisé pour trancher une décision ; l'heure simulée (`simulatedTimeSeconds`) est dérivée du tick et de l'échelle temporelle (`simulatedSecondsPerTick`).
- **Snapshot & restore** : capture d'état complet (monde, cognition, PRNG) et restauration exacte permettent un rejeu bit-à-bit identique au parcours ininterrompu.
- **Persistance SQLite** : schéma versionné, transactions atomiques, reprise au dernier état valide. La rotation des sauvegardes est bornée pour éviter les effets collatéraux.

## ECHOS : observation sans intrusion

ECHOS consomme le flux d'observabilité de SYNE sans modifier l'état. Pour préserver la reproductibilité :

- **Consommation passive** : il n'envoie pas de commandes modifiant l'état décisionnel.
- **Alignement strict** : le flux suit « snapshot puis événements » par tick. ECHOS détecte les **discontinuités de ticks** (trous) et les consigne sans les masquer.
- **Provenance** : chaque métrique signale si elle est **mesurée** ou si un repli neutre a dû être appliqué (absence de fenêtre). Cela évite de présenter comme mesuré ce qui est reconstitué.
- **Comparaisons honnêtes** : `/api/compare` distingue un **résumé léger** (`light=1`) d'une **comparaison complète** fondée sur l'empreinte bit-à-bit. Le verdict « bit-identique/reproductible » n'est émis que lorsque les conditions permettent de l'établir.
- **Rapports de calibration versionnés** : `schemaVersion 2` avec `outcome`, `extinctionTick`, bloc `viability`, `actionSharesWhenHungry`, `resourceRegime`. Ces champs sont dérivés depuis les séries persistées, sans réinjection dans SYNE.

## Contrôler ce qui entre

La reproductibilité exige aussi de **maîtriser les entrées** :

- **Configuration cohérente** : profils (`reference`, `prism`), surcouches partielles appliquées de manière uniforme (start/prepare/reset suivent les mêmes règles). Un seul jeu de valeurs par profil est maintenu pour éviter la dérive.
- **Run_id canonique** : `run-<seed>-<12hex>` rend traçable le lien entre seed, exécution et artefacts (paquets `.livexp`, rapports ECHOS).
- **Orchestration déterministe** : le Launcher dérive les graines, applique les politiques d'échec de manière explicite, et consigne `provenance.json` à la création du run. La reprise se fait sans rejouer les runs déjà validés (empreintes et journaux vérifiés).
- **Échelle temporelle maîtrisée** : `simulatedSecondsPerTick` (et `metersPerUnit` informatif) modifie la représentation temporelle/scalaire, mais les **effets par action** et l'ordre causal restent inchangés. L'horloge est instanciée au démarrage, pas globale mutable.

## Preuves, pas affirmations

Le projet traduit ces principes en **preuves concrètes** :

- **Oracles de checksum** : états dorés épinglés pour les parcours critiques ; tout changement de trajectoire justifié (bump `engineVersion` MINOR quand la trajectoire change volontairement).
- **Campagnes reproductibles** : campagnes multi-seeds (ex. 50/100 agents × 3 seeds) montrant stabilité sur 2500 ticks avec critères explicites (0 extinction, |pente| < seuil, population finale ≈ initiale).
- **Tests bout-en-bout** : parcours réels SYNE publié + ECHOS réel (y compris Windows), vérification octet-à-octet de paquets `.livexp` et de rapports archivés.
- **Détection de trous** : journalisation structurée des discontinuités de ticks, comptage `gaps_detected`, sans masquage.

## Une discipline nécessaire

Dans LIVEX, « ça fonctionne » ne suffit pas. « Ça se rejoue » est la norme. Cette discipline est contraignante, mais elle est aussi ce qui permet de transformer une curiosité (« peut-il émerger quelque chose ? ») en une **affirmation vérifiable** (« dans ces conditions précises, oui, et voici comment le rejouer »).

C'est précisément cette rigueur qui donne du poids à tout phénomène observé : s'il émerge, il doit pouvoir réapparaître à l'identique.
