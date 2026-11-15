# ASSETS — Éléments graphiques à produire

**Composant** : PRISM — visualiseur
**Statut** : [ASSETS DÉPOSÉS ET CHARGÉS — RENDU VÉRIFIÉ LE 15 NOVEMBRE 2026]
**Dernière mise à jour** : 15 novembre 2026
**Dépend de** : [`RENDERING_SPEC.md`](RENDERING_SPEC.md), [`CONTENT_SPEC.md`](CONTENT_SPEC.md), [`PACKAGING.md`](PACKAGING.md)
**Source Monographie** : —

---

## 1. Ce qui existe, ce qui est à produire

Les chargeurs sont **câblés** : `ViewerAssets.Load` lit `assets/` (ou `--assets <dir>`) au démarrage, décode le PNG avec `ViewerPng` (aucune dépendance externe) et construit l'atlas, la palette des matériaux, celle des agents et la police du HUD. Chaque fichier reste **optionnel** : absent ou invalide, il produit un avertissement sur la sortie d'erreur et le générateur procédural prend le relais. Le jeu du dépôt (`textures/viewer-atlas.png` 256 × 256, `config/terrain-palette.json`, `config/agent-palette.json`, `fonts/hud-5x7.json`) est en place et chargé sans avertissement ; la capture `tmp/prism-captures/assets-final.png` en montre le rendu.

Ce document liste deux familles d'éléments :

1. **Ce qui se dépose tel quel dans un pack de contenu** (gabarits d'arbres, fiches de minerais, gabarits de structures) : aucun code à écrire, le compilateur les valide déjà.
2. **Ce qui remplace un générateur** (atlas de textures, palette des agents, police du HUD, captures de référence) : l'asset est spécifié ici, mais son chargement demande le point d'intégration indiqué à chaque fiche. Tant que ce point n'est pas câblé, l'asset n'a aucun effet.

Règle générale : **un asset absent ne casse jamais le rendu**. Le générateur prend le relais avec le repli décrit dans chaque fiche.

## 2. Conventions communes

| Sujet | Règle |
| :-- | :-- |
| Racine | `prism/assets/` (nouveau dossier, à côté de `prism/content/`) |
| Nommage | `kebab-case`, ASCII, sans espace ni accent : `viewer-atlas.png`, `tree-oak-small.json` |
| Images | PNG **RGBA 8 bits**, non entrelacé, sRGB, aucune métadonnée obligatoire |
| Transparence | alpha droit (jamais prémultiplié) ; l'alpha n'est utilisé que par les overlays, jamais par le terrain |
| Filtrage | le viewer utilise `Nearest` (pas de mipmaps) : pas de détail plus fin qu'un texel par pixel de tuile |
| Licences | en-tête `SPDX-License-Identifier: GPL-3.0-or-later` dans `prism/assets/README.md`, plus une ligne par fichier dans `prism/assets/CREDITS.md` (auteur, origine, licence) |
| Poids | ≤ 512 Ko par PNG, ≤ 256 Ko par JSON de gabarit ; tout asset plus lourd doit être justifié dans la revue |
| Versions | les assets suivent la version du viewer (`viewer.component.json`) ; un changement visuel cassant incrémente la version mineure |

Arborescence cible :

```text
prism/assets/
├── README.md                  # licence, crédits, inventaire
├── CREDITS.md                 # auteur / origine / licence par fichier
├── textures/
│   └── viewer-atlas.png       # atlas unique du monde (voir §3.1)
├── config/
│   ├── terrain-palette.json   # ids de matériaux → rôles (voir §3.2)
│   └── agent-palette.json     # couleurs des agents (voir §3.4)
├── fonts/
│   └── hud-5x7.json           # police bitmap du HUD (voir §3.5)
└── captures/                  # captures de référence pour la CI (voir §3.8)
```

Les gabarits voxel ne vont **pas** dans `prism/assets/` : ce sont des données de monde, ils appartiennent à un pack de contenu (`prism/content/<profil>/templates/`, `materials/`). Le profil publié par défaut est `minimal` (`CONTENT_PROFILE` dans `scripts/release/publish_prism_v2.py`) ; un pack de démonstration visuelle peut vivre à côté (`prism/content/viewer-demo/`) sans changer la publication.

## 3. Fiches détaillées

### 3.1 Atlas de textures du monde — `viewer-atlas.png`

| Champ | Valeur |
| :-- | :-- |
| Nom | `viewer-atlas.png` |
| Emplacement | `prism/assets/textures/viewer-atlas.png` |
| Format | PNG RGBA 8 bits, non entrelacé, 256 × 256 px |
| Découpe | grille **4 × 4** de tuiles **64 × 64 px** (16 tuiles) |
| Chargé par | `ViewerAssets.Load` → `ViewerTextureAtlas.Load` (`ViewerPng` pour le décodage) |
| Repli | atlas procédural `ViewerTextureAtlas.Create()` si le fichier est absent ou invalide |

**Ordre des tuiles imposé** (index = `row * 4 + column`, haut-gauche = 0) :

| Index | Rôle | Matériau lié | Remarque |
| :-- | :-- | :-- | :-- |
| 0 | gazon | `grass` | teinte claire, brins verticaux |
| 1 | sable | `sand` | grain fin, sans motif directionnel |
| 2 | pierre | `stone` | grain irrégulier, contraste modéré |
| 3 | terre | `dirt` / labour | mottes, pas de lignes droites |
| 4 | socle rocheux | `bedrock` | sombre, très bruité |
| 5 | eau | `water` | ondulations horizontales statiques, alpha **opaque** (la transparence vient du shader) |
| 6 | structure | planches / pierre taillée | bandes verticales façon bois |
| 7 | minerai | tout matériau tagué `ore` | granulat clair, éclats francs |
| 8 | feuillage | arbre (blocs de feuilles) | touffes, pas de feuille isolée lisible à 1 texel |
| 9 | tronc | arbre (blocs de bois) | fibres verticales |
| 10–14 | réservées | — | laisser neutres (gris moyen) pour un usage futur (agents, icônes) |
| 15 | repli | matériau inconnu | damier discret gris/anthracite, jamais transparent |

**Contraintes visuelles**

- La couleur finale est `couleurDeMatériau × texel × grain × teinteLumineuse` : la tuile doit rester **claire et désaturée** (luminance moyenne 0,75–0,95), sinon le terrain devient noir la nuit.
- Pas de texte, pas de logo, pas de transparence dans les tuiles 0–9 : un pixel alpha < 255 y produit des trous dans le terrain.
- Pas de couture : chaque tuile est répétée par face, les bords opposés doivent se raccorder.
- Les faces latérales utilisent la même tuile que le dessus, orientée « haut » vers le haut de l'image : les fibres (bois, roche) doivent être verticales dans l'image.

**Intégration (déjà câblée)** : `ViewerAssets.Load` cherche `textures/viewer-atlas.png`, `ViewerTextureAtlas.Load` vérifie un carré divisible en 4 × 4 tuiles homogènes de 4 à 128 px, et `Program` transmet l'atlas au GPU. L'ordre des tuiles est celui de la table ci-dessus ; `GetTileIndex` et `GetTileBounds` n'ont pas changé. Déposer le fichier suffit.

**Recette** : `dotnet test prism/Prism.Viewer.Tests --filter ViewerTextureAtlasTests` reste vert ; capture `--screenshot` montrant les 9 tuiles lisibles ; aucune couture visible sur une falaise.

### 3.2 Table des matériaux du monde — `terrain-palette.json`

| Champ | Valeur |
| :-- | :-- |
| Nom | `terrain-palette.json` |
| Emplacement | `prism/assets/config/terrain-palette.json` |
| Format | JSON UTF-8, camelCase, une entrée par rôle |
| Chargé par | `ViewerAssets.LoadTerrainPalette` (repli : les ids d'aperçu `Program.cs`) |

```json
{
  "schema": 1,
  "grass": 1,
  "sand": 2,
  "stone": 3,
  "soil": 4,
  "bedrock": 5,
  "water": 6,
  "ore": 7,
  "tree": 8,
  "leaves": 9
}
```

**Pourquoi c'est nécessaire** : en mode client, la grille vient du host et les ids de matériaux viennent du pack ; le viewer suppose aujourd'hui `1…6`. Un pack qui déclare `grass = 12` s'affiche avec les mauvaises couleurs. Ce fichier rend la correspondance explicite.

**Intégration (déjà câblée)** : `ViewerAssets.LoadTerrainPalette` fusionne les clés présentes avec la palette de repli ; une clé absente garde sa valeur par défaut et un id nul est refusé. **Reste à faire côté contrat** : `GeneratedWorldSnapshot` ne publie pas la table du pack ; en mode client, ce fichier est donc la seule source pour un pack qui n'utilise pas `1…6`.

**Recette** : un pack dont les ids diffèrent des valeurs par défaut produit les mêmes couleurs qu'avec les ids standard.

### 3.3 Arbres voxel — gabarits de contenu

| Champ | Valeur |
| :-- | :-- |
| Nom | `tree-<espèce>-<taille>.json` (ex. `tree-oak-small.json`) |
| Emplacement | `prism/content/<profil>/templates/` |
| Format | JSON de gabarit (schéma `CONTENT_SPEC.md` §3.4), camelCase |
| Chargé par | `ContentPackCompiler` → `TreePlacementGenerator` ; aucun code à écrire |
| Rendu | volumes de `TerrainMeshBuilder` (une boîte par bloc, culling par face) |

```json
{
  "id": "tree_oak_small",
  "displayName": { "fr": "Chêne nain", "en": "Small oak" },
  "size": [5, 5, 6],
  "blocks": [
    { "at": [0, 0, 0], "block": "log" },
    { "at": [0, 0, 1], "block": "log" },
    { "at": [0, 0, 2], "block": "log" },
    { "at": [0, 0, 3], "block": "leaves" },
    { "at": [1, 0, 3], "block": "leaves" },
    { "at": [-1, 0, 3], "block": "leaves" },
    { "at": [0, 1, 3], "block": "leaves" },
    { "at": [0, -1, 3], "block": "leaves" },
    { "at": [1, 0, 4], "block": "leaves" },
    { "at": [-1, 0, 4], "block": "leaves" },
    { "at": [0, 1, 4], "block": "leaves" },
    { "at": [0, -1, 4], "block": "leaves" },
    { "at": [0, 0, 4], "block": "leaves" },
    { "at": [0, 0, 5], "block": "leaves" }
  ],
  "materials": [],
  "workTicks": 1,
  "placement": { "flatGround": true, "clearVolume": true },
  "integrityThresholdPermille": 1000,
  "isTree": true
}
```

**Règles de production**

- Origine = **premier bloc au-dessus de la surface** de la colonne plantée : le bloc `[0,0,0]` est le bas du tronc, jamais la terre.
- `blocks` ne peut désigner que des matériaux du pack ; les blocs doivent tenir dans `size` et dans le monde.
- Un arbre n'a **ni `materials` ni `effect`** (le validateur le refuse) ; `workTicks` est sans effet.
- Le tronc occupe la colonne `[0,0]` sur au moins 2 blocs, le houppier est contigu au tronc (pas de feuille flottante), hauteur totale ≤ 12 blocs.
- Deux arbres ne se chevauchent pas : `TreePlacementGenerator` abandonne une plantation dont un bloc est déjà occupé.
- Densité et espacement viennent du pack (`forest` dans les paramètres), pas du gabarit.

**Jeu minimal à livrer** : un feuillu 5 × 5 × 6 (`tree_oak_small`), un conifère 3 × 3 × 8 (`tree_pine_tall`), un buisson 3 × 3 × 3 (`tree_bush`). Variantes de couleur : dupliquer le gabarit avec un autre matériau de feuilles, jamais avec un autre `id` de matériau inexistant.

**Recette** : `dotnet run --project prism/Prism.Viewer -- --embedded --content prism/content/<profil>` montre des arbres en volume ; `dotnet test prism/Prism.Content.Tests` vert (gabarits validés).

### 3.4 Palette des agents — `agent-palette.json`

| Champ | Valeur |
| :-- | :-- |
| Nom | `agent-palette.json` |
| Emplacement | `prism/assets/config/agent-palette.json` |
| Format | JSON UTF-8, couleurs sRGB 0–255 |
| Remplaçant | les constantes de `AgentVisualMeshBuilder.Build` (inactif, chargé, en action, tête, jambes) |

```json
{
  "schema": 1,
  "idle": [51, 145, 173],
  "carrying": [209, 125, 51],
  "acting": [148, 102, 199],
  "headBoost": 0.12,
  "legDarken": 0.72
}
```

**Contraintes**

- Les trois états doivent rester distinguables en vision deutan/protan : ne pas différencier uniquement par la teinte, garder des luminances distinctes (inactif sombre, chargé moyen, action clair).
- `headBoost` ∈ [0, 0,3] et `legDarken` ∈ [0,5, 1,0] : au-delà, la silhouette perd sa lecture de volume.
- Pas de skin texturée : les agents sont dessinés avec `uAtlasMix = 0`. Ajouter une texture par agent serait une évolution (UV sur les boîtes + tuiles 10–12 de l'atlas), hors périmètre de cette fiche.

**Intégration (déjà câblée)** : `AgentVisualMeshBuilder.Build(samples, palette)` reçoit `ViewerAssets.Agents` ; `ViewerAgentPalette.Load` valide les trois couleurs (0–255), `headBoost` ∈ [0, 0,3] et `legDarken` ∈ [0,5, 1]. Les proportions restent dans le code : encombrement ≈ 0,96 × 0,46 × 1,88 (L × P × H), pieds à `z`, sommet de la tête à `z + 1,88`.

**Recette** : `dotnet test prism/Prism.Viewer.Tests --filter AgentVisualMeshBuilderTests` ; une capture montre trois agents de couleurs distinctes.

### 3.5 Police du HUD — `hud-5x7.json`

| Champ | Valeur |
| :-- | :-- |
| Nom | `hud-5x7.json` |
| Emplacement | `prism/assets/fonts/hud-5x7.json` |
| Format | JSON UTF-8 : caractère → 7 entiers, chacun = 5 bits (bit 4 = colonne de gauche) |
| Remplaçant | la table `Glyphs` de `ViewerHudMeshBuilder` |
| Rendu | panneau 470 × 380 px, marge 16, interligne 17, agrandissement 2 (avance 6 px à l'échelle 1, donc 12 px à l'échelle 2) |

```json
{
  "schema": 1,
  "width": 5,
  "height": 7,
  "advance": 6,
  "glyphs": {
    "A": [14, 17, 17, 31, 17, 17, 17],
    "B": [30, 17, 17, 30, 17, 17, 30],
    "0": [14, 17, 19, 21, 25, 17, 14],
    ":": [0, 12, 12, 0, 12, 12, 0],
    "-": [0, 0, 0, 31, 0, 0, 0],
    " ": [0, 0, 0, 0, 0, 0, 0]
  }
}
```

**Jeu de caractères exigé** (le HUD n'affiche rien d'autre) : `A`–`Z`, `0`–`9`, espace, `-`, `:`, `/`, `.`, `,`, `=`, `+`, `%`, `(`, `)`, `?`, `!`. Tout caractère absent s'affiche comme `?`.

**Contraintes** : glyphes strictement monospace 5 × 7, un pixel de colonne vide entre deux caractères (d'où l'avance de 6 à l'échelle 1), pas d'antialiasing (le rendu est un rectangle par pixel allumé), majuscules uniquement.

**Intégration (déjà câblée)** : `ViewerHudMeshBuilder.Build(state, largeur, hauteur, police)` reçoit `ViewerAssets.Font` ; `ViewerHudFont.Load` valide `width`/`height` (3–16), `advance` ≥ largeur, le nombre de lignes et l'amplitude des bits, et ajoute le glyphe `?` par défaut s'il manque. `GlyphScale = 2` est conservé.

**Recette** : capture 1280 × 800 ; les 20 lignes du HUD tiennent dans le panneau sans chevauchement ; `dotnet test --filter ViewerHudMeshBuilderTests` vert.

### 3.6 Minerais — fiches de matériaux

| Champ | Valeur |
| :-- | :-- |
| Nom | `<minerai>.json` (ex. `copper-ore.json`) |
| Emplacement | `prism/content/<profil>/materials/` |
| Format | fiche matériau (`CONTENT_SPEC.md` §3.1) avec `tags: ["ore", "mineral"]` |
| Chargé par | `WorldGenerationConfigCompiler` (tout matériau tagué `ore` devient un filon) |
| Rendu | boîtes de minerai, tuile 7, visibles **uniquement en vue en coupe** |

```json
{
  "id": "copper_ore",
  "displayName": { "fr": "Minerai de cuivre", "en": "Copper ore" },
  "solid": true,
  "hardness": 10,
  "minToolTier": 1,
  "tags": ["ore", "mineral"],
  "drops": [{ "item": "copper_chunk", "qty": [1, 2] }],
  "perceivable": true
}
```

**Paramètres de filon appliqués par le compilateur** (non réglables dans la fiche) : profondeur 2 à `min(verticalSize / 2, 24)` blocs sous la surface, échelle de treillis 18, seuil 10 000. Conséquence : un minerai est toujours enterré, donc jamais visible hors coupe.

**Limite connue à documenter dans la revue** : tous les minerais partagent la **même** couleur et la même tuile (`TerrainMaterialPalette.Ore`). Pour distinguer cuivre / fer / or, il faut soit une entrée de palette par minerai (fichier `terrain-palette.json` étendu), soit une teinte par matériau publiée par le host — ce n'est pas le cas dans le contrat `1.0.0`.

**Recette** : `--embedded` avec un pack contenant un matériau `ore`, puis `X` / `[` / `]` pour couper : les filons apparaissent en volumes colorés dans la section.

### 3.7 Structures — gabarits et fantômes

| Champ | Valeur |
| :-- | :-- |
| Nom | `<structure>.json` (ex. `shelter-3x3.json`) |
| Emplacement | `prism/content/<profil>/templates/` |
| Format | gabarit de structure (`CONTENT_SPEC.md` §3.4), **sans** `isTree` |
| Rendu | blocs posés en volumes ; chantier en fantôme translucide rempli au prorata de `workCompleted / workRequired` |

```json
{
  "id": "shelter_3x3",
  "displayName": { "fr": "Abri 3 × 3", "en": "3×3 shelter" },
  "size": [3, 3, 3],
  "blocks": [
    { "at": [0, 0, 0], "block": "planks" },
    { "at": [1, 0, 0], "block": "planks" },
    { "at": [2, 0, 0], "block": "planks" }
  ],
  "materials": [{ "item": "plank", "qty": 24 }, { "item": "log", "qty": 4 }],
  "workTicks": 120,
  "placement": { "flatGround": true, "clearVolume": true },
  "effect": { "type": "shelter", "volume": [[0, 0, 0], [2, 2, 2]], "fatigueRecoveryBonusPermille": 500 },
  "integrityThresholdPermille": 800
}
```

**Contraintes** : `size` = trois dimensions **positives** (rester ≤ 16 par axe pour qu'un fantôme reste lisible) ; `blocks` non vide, chaque `block` doit désigner un matériau du pack et tenir dans `size` ; `materials` cohérent avec les blocs ; `effect` **obligatoire** hors arbre (le compilateur refuse un gabarit sans effet mesurable) ; `effect.volume` inclus dans `size` ; `workTicks` ≥ 0 (utiliser ≥ 1 : le fantôme divise par le travail requis) ; `integrityThresholdPermille` ∈ [0, 1000].

**Recette** : un chantier en cours affiche un volume translucide dont la hauteur suit l'avancement ; une fois achevé, les blocs réels remplacent le fantôme.

### 3.8 Captures de référence

| Champ | Valeur |
| :-- | :-- |
| Nom | `viewer-<mode>-seed<seed>-z<taille>[-cut<z>].png` (ex. `viewer-preview-seed12345-z96.png`) |
| Emplacement | `prism/assets/captures/` (versionnées) ; les essais libre restent dans `tmp/prism-captures/` (non versionné) |
| Format | PNG 1280 × 800, produit par `ViewerScreenshot` (aucun retraitement) |
| Repli | aucune capture n'est requise pour exécuter le viewer |

Commandes de production (mêmes réglages pour toutes les baselines) :

```bash
cd prism
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --size 96 --seed 12345 --screenshot ../tmp/prism-captures/viewer-preview-seed12345-z96.png --screenshot-frames 25
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --size 96 --seed 12345 --cut 18 --screenshot ../tmp/prism-captures/viewer-preview-seed12345-z96-cut18.png --screenshot-frames 25
dotnet run --project Prism.Viewer/Prism.Viewer.csproj -- \
  --embedded --content content/minimal --ticks-per-second 20 \
  --screenshot ../tmp/prism-captures/viewer-embedded-z256.png --screenshot-frames 150
```

**Contraintes** : capture sans fenêtre redimensionnée (1280 × 800), `--ticks-per-second 0` interdit (le HUD doit être daté), pas de retouche ni de recadrage. Une capture de référence n'est acceptée que si elle est reproductible sur deux exécutions successives (comparaison octet à octet des PNG, l'encodeur est déterministe).

**Intégration CI** (non câblée) : comparer les pixels avec une tolérance nulle hors HUD (horloge) ou masquer la zone du panneau ; un écart hors tolérance échoue le job.

## 4. Où déposer, comment expédier

| Étape | Fichier / commande |
| :-- | :-- |
| Dépôt des assets viewer | `prism/assets/…` (nouveau) |
| Données de monde | `prism/content/<profil>/…` (déjà publié par `publish_prism_v2.py`) |
| Publication | `scripts/release/publish_prism_v2.py` copie `prism-host`, `prism-viewer`, `prism-content` **et**, si `prism/assets/` existe, le dépose en `prism-viewer/bin/assets/` (à côté du binaire, puisque l'exécutable est dans `bin/`). Aucune modification de manifeste n'est nécessaire : le binaire cherche `assets/` dans son dossier |
| Recette locale | `dotnet build prism/Prism.sln` puis `dotnet test prism/Prism.Viewer.Tests/Prism.Viewer.Tests.csproj` |
| Recette visuelle | §3.8, captures relues avant/après |

## 5. Correspondance code ↔ asset

| Asset | Fichier de code à modifier | Constante / méthode de référence |
| :-- | :-- | :-- |
| `viewer-atlas.png` | `ViewerTextureAtlas.cs` | `Create()`, `DefaultTileSize`, `GetTileIndex`, `GetTileBounds` |
| `terrain-palette.json` | `Program.cs` + `TerrainMeshBuilder.cs` | `new TerrainMaterialPalette(...)`, `TerrainMaterialPalette.GetColor` |
| `tree-*.json` | aucun (contenu) | `TreePlacementGenerator`, `WorldGenerationConfigCompiler` |
| `agent-palette.json` | `AgentVisualMeshBuilder.cs` | `Build(IEnumerable<ViewerAgentSample>)` |
| `hud-5x7.json` | `ViewerHudMeshBuilder.cs` | `Glyphs`, `GlyphScale`, `AddText` |
| minerais | aucun (contenu) | `WorldGenerationConfigCompiler` (tag `ore`), `OreDepositGenerator` |
| structures | aucun (contenu) | `StructureTemplateRuntimeCompiler`, `ViewerOverlayMeshBuilder.BuildConstructionGhosts` |
| captures | `ViewerScreenshot.cs` | `Save`, `EncodePng` |

## 6. Ordre de production conseillé

1. **Atlas** (P0) : sans lui, tout le reste est une variation de couleur.
2. **Gabarits d'arbres + fiches de minerais** (P0) : immédiatement visibles, aucun code.
3. **Police du HUD** (P1) : confort de lecture, remplacement mécanique.
4. **Palette des agents** (P1) : lisibilité des états en capture.
5. **Palette des matériaux** (P1) : nécessaire dès qu'un pack réel n'utilise pas `1…6`.
6. **Structures** (P2) : dépend du contenu de jeu, pas du rendu.
7. **Captures de référence + job CI** (P2) : après stabilisation du rendu.

## 7. Décisions ouvertes

- **Chargeur d'images** : *tranché* — décodeur interne `ViewerPng` (PNG 8 bits RGB/RGBA, non entrelacé, filtres 0–4, CRC vérifié), sans dépendance.
- **Maillages externes** : le monde est voxel ; il n'existe **aucun** chargeur glTF/OBJ et ce document ne le suppose pas. Un décor non voxel serait un changement de pipeline (et un risque de désynchronisation avec l'état autoritaire).
- **Skins d'agents texturées** : possible via les tuiles 10–12 et des UV par boîte, mais demande de toucher `AgentVisualMeshBuilder` et le shader (`uAtlasMix` aujourd'hui coupé pour les agents).
- **Couleur par minerai** : dépend d'une donnée absente du contrat `1.0.0` (aucune teinte par matériau publiée).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 15 novembre 2026 | Création : inventaire des assets à produire (atlas, palettes, gabarits, police, captures), emplacements, formats, points d'intégration et recettes | Demande de fiche de production graphique pour la P11 |
