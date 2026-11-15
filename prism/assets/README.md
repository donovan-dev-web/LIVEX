<!--
SPDX-FileCopyrightText: 2026 Donovan Chartrain
SPDX-License-Identifier: GPL-3.0-or-later
-->

# Assets du viewer PRISM

Dossier de dépôt des éléments graphiques externes du visualiseur. **Tous les fichiers sont
optionnels** : le viewer génère l'atlas, les palettes et la police par programme, puis remplace
chaque élément dès qu'il trouve le fichier correspondant. Un fichier absent ou invalide produit
un avertissement sur la sortie d'erreur, jamais un échec de rendu.

Spécifications complètes, contraintes et recettes : [`docs/prism/ASSETS.md`](../../docs/prism/ASSETS.md).

## Arborescence attendue

```text
prism/assets/
├── README.md                  # ce fichier
├── CREDITS.md                 # auteur / origine / licence par fichier
├── textures/
│   └── viewer-atlas.png       # 256 × 256, grille 4 × 4 de tuiles 64 × 64 RGBA
├── config/
│   ├── terrain-palette.json   # ids de matériaux → rôles (grass, sand, stone, …)
│   └── agent-palette.json     # couleurs des agents (idle, carrying, acting)
└── fonts/
    └── hud-5x7.json           # police bitmap du HUD (5 × 7)
```

## Chargement

- Ordre de recherche : `--assets <dir>` si fourni, sinon `assets/` à côté du binaire, sinon
  `prism/assets/` du dépôt (exécution depuis `bin/Debug/net10.0`).
- Décodage PNG interne (`ViewerPng`) : 8 bits par canal, RGB ou RGBA, non entrelacé, CRC vérifié.
- Publication : `scripts/release/publish_prism_v2.py` copie ce dossier en `prism-viewer/assets/`
  lorsqu'il existe ; le binaire le trouve alors à côté de lui.

## Licence

Les assets déposés ici sont distribués sous GPL-3.0-or-later, comme le reste du dépôt, sauf
mention contraire dans `CREDITS.md`. Toute image tierce doit y être déclarée avec sa licence
d'origine et son auteur avant d'être versionnée.
