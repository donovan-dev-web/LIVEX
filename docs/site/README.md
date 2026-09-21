# Docs Site — GitHub Pages (DocFX)

Site de documentation LIVEX publié sur `https://donovan-dev-web.github.io/LIVEX/`
via la **branche dédiée `gh-pages`**. Il contient une **page de présentation**
(`index.md`), les **docs clés** du monorepo (copiées par `sync-docs.sh`) et la
**référence API** de `Simulation.Core` (générée par DocFX depuis le XML).

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
cd docs/site
docfx metadata
docfx build

# Résultat : docs/site/_site/
```

Serveur de prévisualisation : `cd docs/site && docfx serve _site`.