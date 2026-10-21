# DEPLOYMENT.md — build et déploiement

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `../../.github/workflows/pages.yml`, `../../site/package.json`, `../CI_CD.md`

---

## 1. Environnement

| Élément | Valeur |
| :-- | :-- |
| Runtime | Node.js **22** (workflows) ; `20+` en local |
| Gestionnaire | npm |
| Dossier de travail | `site/` |
| Versions épinglées | `site/package-lock.json` (`npm ci`) |

## 2. Scripts npm (`site/package.json`)

| Script | Commande | Rôle |
| :-- | :-- | :-- |
| `dev` | `astro dev` | Serveur de développement sous `/LIVEX/`. |
| `build` | `astro build` | Build de production dans `site/dist/`. |
| `postbuild` | `pagefind --site dist` | Index de recherche Pagefind (lancé automatiquement après `build`). |
| `preview` | `astro preview` | Sert le build local sous `/LIVEX/`. |
| `check` | `astro check` | Vérification de types et diagnostics. |
| `check:links` | `node scripts/check-links.mjs` | Vérifie les liens internes du build. |
| `check:text` | `node scripts/check-text.mjs` | Audit du texte du build : entités HTML non décodées, mojibake, chaînes françaises en pages anglaises, composants sans `lang="en"`. |

Workflow local recommandé :

```bash
cd site
npm ci
npm run check
npm run build        # déclenche aussi postbuild (Pagefind)
npm run check:links
npm run check:text   # audit du texte (entités, langues, lang="en")
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
2. **Setup Node** 22 avec cache npm (`site/package-lock.json`).
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

`astro.config.mjs` fixe par défaut `site` et `base: '/LIVEX/'`. Concrètement :

- Les URL produites commencent par `/LIVEX/` (ex. `/LIVEX/comprendre/`).
- La prévisualisation **doit** passer par `npm run preview` (un serveur
  statique à la racine donnerait des 404 sur les liens).
- Une branche publiée sans le `base` correct casse tous les liens internes et
  les ressources.
- `BASE_PATH` et `SITE_URL` peuvent surcharger ces valeurs (variables
  d'environnement) pour un build de test à la racine : c'est ce que fait le
  workflow Lighthouse (`BASE_PATH=/`, `SITE_URL=http://localhost:4321`) afin
  que `astro preview` serve `dist/` à la racine.
- Les deux variables doivent être présentes **au moment du `preview`**, pas
  seulement du build : `astro preview` relit `astro.config.mjs` au démarrage
  et sert `dist/` sous le `base` courant. Un build racine suivi d'un
  `npm run preview` sans `BASE_PATH=/` sert `/LIVEX/` et renvoie 404 sur
  `http://localhost:4321/`.

## 5. CI transverse

Le workflow transverse `ci.yml` (voir `../CI_CD.md`) valide la structure du
dépôt et les composants ; il ne construit **pas** le site. Le contrôle du site
(type-check + build) est porté par `pages.yml`, déclenché sur `main`.

Le workflow `lighthouse.yml` (intitulé « Site CI ») s'exécute sur les pull
requests touchant `site/**` (et en manuel). Il comporte deux jobs :

- **Texte et liens** : build, `npm run check:links` et `npm run check:text`
  (entités HTML non décodées, mojibake, chaînes françaises résiduelles en
  pages anglaises, composants sans `lang="en"`) ;
- **Lighthouse** : build avec `BASE_PATH=/`, démarrage de `astro preview` sur le
  port `4321`, puis `@lhci/cli` sur trois pages (accueil, architecture,
  résultats) avec `lighthouserc.json`.

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
- Le workflow Lighthouse utilise `LHCI_GITHUB_APP_TOKEN` s'il est configuré ;
  l'upload vers `temporary-public-storage` fonctionne sans ce secret.
- Rejouer Lighthouse en local (mêmes seuils que la CI) :

  ```bash
  cd site
  SITE_URL=http://localhost:4321 BASE_PATH=/ npm run build
  SITE_URL=http://localhost:4321 BASE_PATH=/ npx @lhci/cli@0.14.0 autorun
  ```

  Sous Windows (PowerShell), les variables se définissent avec
  `$env:SITE_URL=...` / `$env:BASE_PATH=...`. Sans Chrome installé, pointer
  `CHROME_PATH` vers un Chromium (Edge) : `$env:CHROME_PATH="C:\Program Files
  (x86)\Microsoft\Edge\Application\msedge.exe"`.
- Ne pas modifier `pages.yml` sans adapter `base` si l'URL du site change.
- Un build avec la collection journal vide émet un avertissement `glob-loader`
  bénin (`CONTENT.md` §6).
- L'index Pagefind est produit dans `dist/pagefind/` par `postbuild` ; il est
  publié avec le reste du site et ne doit pas être versionné.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter le build et le déploiement du site. |
| 21 octobre 2026 | Pagefind, `check:links`, Node 22, workflow Lighthouse et surcharge `BASE_PATH`/`SITE_URL` (build **et** preview). | Revue globale et validation de la recherche et de la CI performance. |
