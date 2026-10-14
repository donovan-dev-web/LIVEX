# DEPLOYMENT.md — build et déploiement

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : `../../.github/workflows/pages.yml`, `../../site/package.json`, `../CI_CD.md`

---

## 1. Environnement

| Élément | Valeur |
| :-- | :-- |
| Runtime | Node.js **20+** |
| Gestionnaire | npm |
| Dossier de travail | `site/` |
| Versions épinglées | `site/package-lock.json` (`npm ci`) |

## 2. Scripts npm (`site/package.json`)

| Script | Commande | Rôle |
| :-- | :-- | :-- |
| `dev` | `astro dev` | Serveur de développement sous `/LIVEX/`. |
| `build` | `astro build` | Build de production dans `site/dist/`. |
| `preview` | `astro preview` | Sert le build local sous `/LIVEX/`. |
| `check` | `astro check` | Vérification de types et diagnostics. |

Workflow local recommandé :

```bash
cd site
npm ci
npm run check
npm run build
npm run preview      # vérifier sous /LIVEX/
```

## 3. Workflow de déploiement (`.github/workflows/pages.yml`)

| Aspect | Valeur |
| :-- | :-- |
| Nom | « Site (build & deploy) » |
| Déclencheurs | `push` sur `main` touchant `site/**` ou le workflow ; `workflow_dispatch`. |
| Permissions | `contents: write` |
| Concurrence | `pages-${ref}`, `cancel-in-progress: true` |
| Runner | `ubuntu-latest` |
| Répertoire | `site` |

Étapes :

1. **Checkout** (`actions/checkout@v4`).
2. **Setup Node** 20 avec cache npm (`site/package-lock.json`).
3. **Install** — `npm ci`.
4. **Type-check** — `npm run check`.
5. **Build** — `npm run build`.
6. **Upload artifact** — `site/dist` (échec si absent).
7. **Publish** — `peaceiris/actions-gh-pages@v4` :
   - `publish_dir: ./site/dist`
   - `publish_branch: gh-pages`
   - `force_orphan: true` (historique de `gh-pages` réinitialisé à chaque déploiement)
   - `enable_jekyll: false`
   - commit `chore(site): déploiement <sha>` par `github-actions[bot]`.

Le site est ensuite servi par GitHub Pages depuis `gh-pages` à l'adresse
`https://donovan-dev-web.github.io/LIVEX/`.

## 4. Chemin de base

`astro.config.mjs` fixe `site` et `base: '/LIVEX/'`. Concrètement :

- Les URL produites commencent par `/LIVEX/` (ex. `/LIVEX/comprendre/`).
- Le prévisualisation **doit** passer par `npm run preview` (un serveur
  statique à la racine donnerait des 404 sur les liens).
- Une branche publiée sans le `base` correct casse tous les liens internes et
  les ressources.

## 5. CI transverse

Le workflow transverse `ci.yml` (voir `../CI_CD.md`) valide la structure du
dépôt et les composants ; il ne construit **pas** le site. Le contrôle du site
(type-check + build) est porté par `pages.yml`, déclenché sur `main`.

## 6. Publication et retour arrière

- **Publication** : fusionner sur `main` (la `base`/le `build` doivent être
  verts localement d'abord).
- **Retour arrière** : le déploiement est un artefact statique — revenir à un
  commit antérieur de `main` (ou relancer un `workflow_dispatch` sur une
  révision connue-bonne) et laisser `pages.yml` republier `gh-pages`.
- `force_orphan: true` signifie que l'historique de `gh-pages` n'est pas
  conservé : **ne rien pousser directement** sur `gh-pages`.

## 7. Notes

- Le site n'utilise aucun secret (déploiement via `GITHUB_TOKEN`).
- Ne pas modifier `pages.yml` sans adapter `base` si l'URL du site change.
- Un build avec la collection journal vide émet un avertissement `glob-loader`
  bénin (`CONTENT.md` §6).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter le build et le déploiement du site. |
