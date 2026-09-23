# Docs Site — GitHub Pages (DocFX)

Site de documentation LIVEX publié sur `https://donovan-dev-web.github.io/LIVEX/`
via la **branche dédiée `gh-pages`**. Il contient une **page d'accueil de
présentation** (`index.md`), les **docs clés** du monorepo (copiées par
`sync-docs.sh`) et la **référence API** de `Simulation.Core` (générée par DocFX
depuis le XML).

## Architecture du site

- **`index.md`** — landing de présentation (hero, cartes des trois composants,
  fonctionnalités) en HTML embarqué dans le markdown DocFX.
- **`docfx.json`** — build avec le **template overlay** `template/` :
  `"template": ["default", "template"]` (le défaut DocFX est surchargé sans le
  réécrire) ; `_appTitle: "LIVEX"`, `_appLogoPath: logo.svg`,
  `_appFaviconPath: favicon.ico`.
- **`template/`** — thème maison (palette LIVEX alignée sur `UI_DESIGN.md`) :
  - `styles/main.css` — surcharge du thème par défaut (fond `#0D1117`, surfaces
    `#161B22`, accent `#00D4A0`, typo Inter + JetBrains Mono, hero/cards).
  - `logo.svg` + `favicon.ico` — marque LIVEX (le `favicon.ico` est généré par
    un script PIL à partir de la même marque, multi-tailles 16→64 px).
- **`toc.yml`** — navigation recomposée (présentation d'abord, hiérarchie des
  trois modules, docs frondend ECHOS ajoutées).
- **`sync-docs.sh`** — copie les docs clés dans `articles/` (source unique
  conservée à l'origine ; tableau `ECHOS` augmenté de `FRONTEND_VISION.md` et
  `UI_DESIGN.md`).

## Contexte

- Source de la documentation : le monorepo (`docs/`, `docs/docs-syne/`, …). Les
  copies dans `articles/` sont **régénérées** à chaque build — ne jamais éditer
  `articles/` à la main.
- Branche `gh-pages` = **artefact de build**, réécrite par la CI ; ne pas
  committer à la main dessus.
- Déploiement automatique : workflow `.github/workflows/docs-pages.yml`
  sur `push develop` (et `workflow_dispatch`).

## Build local

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH

# 1. XML de documentation SYNE (net10)
dotnet build syne/Simulation.Core -c Release

# 2. Synchronisation des docs clés
bash docs/site/sync-docs.sh

# 3. Génération du site (metadata API + build)
cd docs/site && docfx metadata && docfx build

# Résultat : docs/site/_site/
```

Serveur de prévisualisation : `cd docs/site && docfx serve _site`.

Le thème overlay est purement déclaratif (CSS + ressources) : aucun fichier du
template DocFX par défaut n'est modifié, seuls `styles/main.css`, `logo.svg` et
`favicon.ico` sont remplacés.