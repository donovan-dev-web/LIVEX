# MAINTENANCE.md — maintenance et contribution au site

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `COMPONENTS.md`, `I18N.md`, `CONTENT.md`, `DEPLOYMENT.md`

---

## 1. Boucle de travail

```bash
cd site
npm ci
npm run dev        # itérer
npm run check      # types
npm run build      # build de production
```

Avant de proposer une modification : `npm run check` **et** `npm run build`
doivent passer, et le rendu doit être vérifié via `npm run preview` (le site
n'est pas servi à la racine, mais sous `/LIVEX/`).

## 2. Ajouter une page

1. Créer `site/src/pages/<chemin>.astro` (français) sur le modèle d'une page
   existante : `BaseLayout` + `PageHeader` (+ `StatusBadge` si besoin).
2. Créer le miroir `site/src/pages/en/<chemin>.astro` (mêmes slug et structure,
   `lang="en"`, texte anglais).
3. Choisir un **index** de section (prochain numéro libre, `PAGES.md` §1) et le
   passer à `PageHeader`. Utiliser ce préfixe pour les sous-titres (`x.y`).
4. Les `crumbs` : passer des chemins **bruts** (`'technique/'`), sans `url()`.
5. Enregistrer la page dans `NAV` ou `FOOTER_NAV` (`site/src/data/site.ts`) si
   elle doit apparaître dans la navigation. Pour l'ajouter à un sous-menu, la
   déclarer dans le `children` de l'entrée parente (en-tête) **et** dans
   `FOOTER_NAV` (repli accessible) le cas échéant.
6. Mettre à jour `plan-du-site` (FR et EN), en respectant les blocs A–G.
7. Pour une page EN de niveau 2, passer `translated` à `BaseLayout`
   (`I18N.md` §4).

## 3. Ajouter un article au journal

1. Créer `site/src/content/actualites/<slug>.md`.
2. En-tête (frontmatter) requis :

   ```yaml
   ---
   title: "Titre de l'article"
   description: "Résumé d'une phrase."
   date: 2026-10-14
   tags: ["syntaxe", "interne"]
   draft: false
   ---
   ```

3. L'index `actualites/` et le flux `feed.xml` le récupèrent automatiquement
   (tri par date décroissante ; `draft: true` l'exclut).
4. Les articles sont **français uniquement** ; `/en/actualites/` signale la
   situation (`CONTENT.md` §6).

## 4. Ajouter une fiche de version

1. Vérifier qu'une version est **réellement publiée** (tag/notes) avant de
   créer sa fiche — sinon, ne rien ajouter.
2. Ajouter une entrée à `VERSIONS` dans `site/src/data/versions.ts` : `slug`
   (`v0-1`), `label` (`V0.1`), `date` / `dateEn`, `status` (`StatusId`),
   résumés FR/`En`, `highlights`, `changes`, `fixes`, `known`, `assets`.
3. Les pages `versions/<slug>/` (FR) et `en/versions/<slug>/` sont générées
   automatiquement ; référencer la fiche depuis `versions/` si utile.
4. Mettre à jour `RELEASE` (`published`, `latest`, `latestDate`) lorsqu'une
   version devient publique, puis la page Télécharger en conséquence.

## 5. Mettre à jour un fait (version, test, couverture)

1. Vérifier la source de gouvernance (`docs/governance/LIVEX-STATUS.md`,
   `VERSIONING.md`, `CHANGELOG.md`).
2. Modifier `site/src/data/release.ts` (et/ou `components.ts`).
3. Si l'affirmation est nouvelle ou sensible, l'ajouter à `claims.ts`
   (`CONTENT.md` §5).
4. Ne **jamais** écrire un chiffre directement dans une page.

## 6. Modifier le design

- Couleurs, rayons, grille, thème sombre : `site/src/styles/tokens.css`
  uniquement (`DESIGN_SYSTEM.md` §2). Les deux thèmes doivent rester conformes
  au contraste AA.
- Styles d'un composant : `components.css`. Styles de base : `base.css`.
- Ajouter une police : fichier `woff2` dans `src/assets/fonts/`, `@font-face`
  dans `base.css`, préchargement dans `BaseLayout.astro`.

## 7. Ajouter une chaîne d'interface

1. Ajouter la clé dans `ui.fr` **et** `ui.en` (`site/src/i18n/ui.ts`).
2. L'utiliser via `t('clé')` (le repli est le français).
3. Ne jamais coder une chaîne bilingue dans un composant.

## 8. Pièges connus

| Piège | Correctif |
| :-- | :-- |
| Lien interne cassé (404) | Toujours passer par `url()`, jamais de chemin brut avec `/LIVEX/`. |
| Double préfixe `/LIVEX/en/en/…` | Ne pas encapsuler un `crumb.href` dans `url()` : `PageHeader` le fait. |
| Import introuvable dans `src/pages/en/**` | Corriger la profondeur relative (`../../../` pour un fichier à deux niveaux sous `en/`). |
| Lien RSS anglais mort | Le flux est français : `url('actualites/feed.xml', 'fr')`. |
| Sous-menu d'en-tête inatteignable au clavier | Le parent est un `<button>` avec `aria-expanded`/`aria-controls` ; ne pas revenir à une ouverture au survol ou à `:focus-within`. `Échap` doit rendre le focus au bouton. |
| Menu mobile qui reste ouvert en passant au bureau | Le script écoute `matchMedia('(min-width: 901px)')` et retire `data-nav-open` ; ne pas casser cet écouteur. |
| Sous-menu qui reste ouvert après navigation | `set(false)` ferme aussi les `.ni.open` et remet `aria-expanded="false"` ; conserver ce nettoyage. |
| Recherche vide en développement | L'index Pagefind est produit par `postbuild` ; lancer `npm run build` (ou `pagefind --site dist`) avant de tester la recherche. En dev, la modale affiche une aide. |
| Sommaire technique invisible | `TableOfContents` reste `hidden` sans JS ou avec moins de trois sections ; c'est volontaire pour ne pas exposer un contrôle vide. |
| Clignotement de thème | Le script d'application doit rester en ligne dans le `<head>`, avant le rendu. |
| Chiffre modifié « à la main » | Le centraliser dans `data/` + `claims.ts`. |
| `npm run dev` sans `/LIVEX/` | Le `base` est non racine : utiliser les URLs fournies par le serveur. |
| Lighthouse CI en échec sur les assets | Le workflow construit avec `BASE_PATH=/` et `SITE_URL=http://localhost:4321` pour que `astro preview` serve la racine ; ne pas retirer ces variables. |

## 9. Checklist avant publication

- [ ] `npm run check` : 0 erreur, 0 avertissement, 0 indice.
- [ ] `npm run build` : succès ; nombre de pages attendu (80 pages, 404 incluse) et index Pagefind généré (79 pages indexées, FR + EN).
- [ ] `npm run check:links` : tous les liens internes résolus.
- [ ] Liens internes et ancres résolus (aucun 404) ; alternates `hreflang` présents.
- [ ] Bandeau de traduction sur les pages EN de niveau 2 ; absent au niveau 1.
- [ ] Chiffres alignés sur les sources et le registre d'affirmations.
- [ ] Thème clair **et** sombre vérifiés ; navigation clavier OK (sous-menus
      d'en-tête cliquables, `Échap`, focus rendu).
- [ ] Responsive vérifié : bureau, tablette paysage, tablette portrait et
      mobile (menu hamburger ≤ 900 px, sous-menus repliés puis cliquables,
      cibles tactiles).
- [ ] Recherche testée après build (Pagefind) ; modale, focus piégé, `Échap`.
- [ ] Lighthouse CI (workflow `lighthouse.yml`) vert ou avertissements justifiés.
- [ ] Aucune chaîne française résiduelle dans `src/pages/en/`.

## 10. Conventions

Le site suit `../NAMING_CONVENTIONS.md` : identifiants en anglais, texte et
commentaires en français, UTF-8 sans BOM, LF. La documentation du site suit le
même format que le reste de `docs/` (en-tête de métadonnées, sections
numérotées, table de mises à jour).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter la maintenance et la contribution au site. |
| 14 octobre 2026 | Section « Ajouter une fiche de version » ; sous-menus et comptage de pages actualisés. | Alignement sur le cahier des charges (S14). |
| 14 octobre 2026 | Pièges et checklist pour le menu mobile et les sous-menus ; référence de section corrigée. | Ajout du menu mobile responsive. |
| 21 octobre 2026 | Pièges recherche/sommaire/Lighthouse ; checklist à 80 pages et Pagefind. | Revue globale et validation de la recherche, des sommaires et de la CI. |
