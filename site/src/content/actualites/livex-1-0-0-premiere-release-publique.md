---
title: "LIVEX 1.0.0 : première release publique pour Windows et Linux"
description: "La release livex-v1.0.0 rassemble l'installateur, SYNE, ECHOS, le Launcher, PRISM et PRISM-LDK, avec des paquets core et full pour Windows et Linux."
date: 2026-10-31
tags: ["livex", "release", "installateur", "windows", "linux"]
draft: false
---

LIVEX franchit une étape importante : la version **1.0.0** est publiée sous le tag [`livex-v1.0.0`](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0). La release propose des installateurs Windows x64 et Linux x64, chacun en variantes `core` et `full`.

## Une distribution installable

La publication regroupe le Launcher, le moteur SYNE, l'observatoire ECHOS et PRISM, ainsi que les contrats et ressources nécessaires. L'installateur guide l'utilisateur dans le choix des composants et des chemins ; il prend aussi en charge la mise à jour, la réparation et la désinstallation. La variante `full` embarque les modules facultatifs pour les environnements hors ligne.

PRISM est livré comme application Unreal packagée pour Windows et Linux. Le plugin PRISM-LDK, dont le nom de module Unreal est `PrismLdk`, est également réalisé et packagé pour intégrer les contrats SYNE dans des Blueprints Unreal Engine 5.8.

## Ce que cette version affirme

Les tests exécutés pour la release couvrent SYNE, ECHOS, `syne-mock`, le Launcher, PRISM/PrismLdk et l'installateur : **1 566 cas**, dont 1 550 réussis, 16 ignorés et aucun échec. Les cinq résultats Unreal `SuccessWithWarnings` sont des réussites accompagnées d'avertissements. Ces tests valident les scénarios couverts ; ils ne démontrent pas une émergence générale ni ne signifient que chaque fonction envisagée pour PRISM est achevée.

Le relevé Tokei 13.0.0 associé au checkout compte 1 152 fichiers et 111 608 lignes de code. Le détail du périmètre, des composants et des limites se trouve dans le [statut du projet](https://github.com/donovan-dev-web/LIVEX/blob/main/docs/governance/LIVEX-STATUS.md).

## Télécharger et poursuivre

- [Release LIVEX 1.0.0](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0)
- [Guide utilisateur de l'installateur](https://github.com/donovan-dev-web/LIVEX/blob/main/docs/installer/GUIDE-UTILISATEUR.md)
- [Documentation de PRISM et PRISM-LDK](https://github.com/donovan-dev-web/LIVEX/tree/main/docs/prism)

La publication rend le projet accessible sous forme de produit installable. Les résultats scientifiques restent, eux, circonscrits aux protocoles, graines, scénarios et horizons rapportés.
