---
title: "L'installateur LIVEX : du paquet à une installation maîtrisée"
description: "Présentation de l'assistant LIVEX 1.0.0, de ses profils core et full, de la maintenance et des choix faits pour protéger les espaces de travail."
date: 2026-10-31
tags: ["installateur", "distribution", "windows", "linux"]
draft: false
---

Un projet qui se lance seulement depuis son dépôt reste difficile à essayer. Avec LIVEX 1.0.0, l'installateur devient un composant livré à part entière : il transforme les archives de release en installation utilisable, et accompagne aussi son entretien.

## Un assistant pour installer

L'assistant graphique Windows et Linux présente la licence, les composants, les chemins, un récapitulatif et la progression. Il installe pour l'utilisateur courant, sans privilèges administrateur et sans demander d'installer .NET ou Python. Le dossier logiciel est distinct de la configuration et de l'espace de travail.

Le socle comprend le Launcher, SYNE et ECHOS. PRISM est inclus dans la distribution comme composant de base et coché par défaut ; Unreal Engine n'est pas nécessaire pour lancer l'application PRISM packagée. PRISM-LDK, la documentation hors ligne et SYNE-Mock sont des modules facultatifs. `core` permet de télécharger les actifs facultatifs lorsqu'ils sont choisis ; `full` les embarque.

## Une installation reste maintenable

L'assistant reconnaît une installation existante et propose ses opérations de maintenance : mise à jour, réparation et désinstallation. Les portées de désinstallation distinguent les fichiers du logiciel, les réglages et l'espace de travail afin d'éviter la suppression accidentelle de résultats de recherche. Les journaux et les codes de sortie aident au diagnostic.

Un mode démonstration permet de parcourir les écrans sans écrire sur la machine. Il montre l'enchaînement de l'assistant, mais ne remplace pas une installation réelle sur une machine propre.

## Validation et limites

Les suites automatisées Unit, App, Integration et EndToEnd de l'installateur ont été exécutées pour la release : **118 tests réussis**. Le projet prend en charge Windows 10/11 x64 et Linux x64 ; macOS, installation machine et mise à jour automatique ne font pas partie de cette version.

Les installateurs `core` et `full` se trouvent dans la [release publique `livex-v1.0.0`](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0). Pour le parcours détaillé, voir le [guide utilisateur](https://github.com/donovan-dev-web/LIVEX/blob/main/docs/installer/GUIDE-UTILISATEUR.md) et la [documentation de référence](https://github.com/donovan-dev-web/LIVEX/tree/main/docs/installer).
