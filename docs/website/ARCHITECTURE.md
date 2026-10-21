# ARCHITECTURE.md — architecture technique du site vitrine

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `../../site/astro.config.mjs`, `../CI_CD.md`, `DEPLOYMENT.md`

---

## 1. Vue d'ensemble

Le site est un **site statique généré par Astro**. Il ne possède aucune base de
données, aucun serveur applicatif et aucune dépendance tierce chargée à
l'exécution : le navigateur reçoit des fichiers HTML/CSS/SVG autonomes,
construits à partir du dépôt.

| Caractéristique | Choix | Raison |
| :-- | :-- | :-- |
| Génération | Astro (`output: static`) | HTML statique, hébergeable sur GitHub Pages. |
| Rendu client | Aucun framework (pas de React/Vue) | Jeu de données statique ; économie de poids et de complexité. |
| Interactivité | `<script is:inline>` (thème, navigation, recherche, sommaires, article, 404) et `<details>` natifs | Fonctionne sans JS de framework ; progressive. |
| Multilingue | i18n natif Astro | Routes FR/EN, alternates `hreflang` générés. |
| Contenu éditorial | Astro Content Layer (loader `glob`) + plugin `rehype` local | Articles du journal versionnés en Markdown ; `scope="col"` ajouté aux tableaux. |
| Typographie | Polices auto-hébergées (`woff2`) | Aucun appel tiers (confidentialité). |
| URL de base | `/LIVEX/` (surchargeable par `BASE_PATH`) | Publication de projet GitHub Pages et builds de test à la racine. |

Le site **ne dépend jamais** du simulateur pour fonctionner : toutes les
données affichées sont figées dans `site/src/data/` (voir `CONTENT.md`).

## 2. Arborescence

```text
site/
├─ astro.config.mjs        # site, base, i18n, intégration sitemap
├─ package.json            # scripts npm et dépendances
├─ tsconfig.json           # configuration TypeScript
├─ public/                 # copié tel quel à la racine du build
│  ├─ favicon.svg
│  ├─ robots.txt
│  ├─ site.webmanifest
│  └─ .well-known/security.txt
├─ src/
│  ├─ assets/              # ressources importées et optimisées par Astro
│  │  ├─ fonts/            # Instrument Serif, JetBrains Mono (woff2)
│  │  ├─ marks/            # logos SVG (livex, syne, echos, prism)
│  │  ├─ brand/            # illustrations de fond (non utilisées comme logos)
│  │  └─ captures/         # captures d'écran réelles
│  ├─ components/          # composants Astro (voir COMPONENTS.md)
│  ├─ data/                # données éditoriales typées (voir CONTENT.md)
│  ├─ i18n/                # chaînes et utilitaires de langue (voir I18N.md)
│  ├─ layouts/             # BaseLayout.astro
│  ├─ pages/               # routes du site (voir PAGES.md)
│  │  └─ en/               # miroir anglais
│  ├─ styles/              # tokens.css, base.css, components.css
│  ├─ utils/url.ts         # construction d'URL (base + langue)
│  └─ content.config.ts    # schéma de la collection « actualites »
└─ dist/                   # build (non versionné)
```

Les dossiers générés (`dist/`, `.astro/`, `node_modules/`) ne sont jamais
versionnés (`.gitignore`).

## 3. Configuration Astro (`astro.config.mjs`)

| Option | Valeur | Effet |
| :-- | :-- | :-- |
| `site` | `https://donovan-dev-web.github.io` | Origine utilisée pour les URL canoniques, le sitemap et le RSS. |
| `base` | `/LIVEX/` | Préfixe de toutes les URL (projet Pages). |
| `trailingSlash` | `'always'` | Une URL canonique se termine toujours par `/`. |
| `build.format` | `'directory'` | Chaque page produit `<slug>/index.html`. |
| `i18n.defaultLocale` | `'fr'` | Langue source, servie à la racine. |
| `i18n.locales` | `['fr', 'en']` | Langues publiées. |
| `i18n.routing.prefixDefaultLocale` | `false` | Le français n'est pas préfixé ; l'anglais l'est (`/en/`). |
| `integrations` | `@astrojs/sitemap` (avec `i18n`) | Génère `sitemap-index.xml` + alternates. |
| `devToolbar.enabled` | `false` | Pas de barre d'outils en développement. |

Conséquence directe : **toute URL interne doit passer par le helper `url()`**
(`src/utils/url.ts`), qui applique `base` puis le préfixe de langue. Une URL
écrite « à la main » casse sur GitHub Pages (le `base` n'est pas `/`).

## 4. Rendu et interactivité

Le build produit du HTML statique. Les scripts suivants s'exécutent côté client,
tous en amélioration progressive :

| Script | Emplacement | Rôle |
| :-- | :-- | :-- |
| Thème (application) | `BaseLayout.astro`, `<head>` | Lit `localStorage['livex-theme']` **avant le rendu** pour éviter tout clignotement clair→sombre. |
| Thème (bascule) | `BaseLayout.astro`, fin de `<body>` | Écoute `#theme-toggle`, met à jour `data-theme`, `aria-pressed` et la préférence stockée. |
| Navigation | `Header.astro` (`is:inline`) | Ouvre/ferme les sous-menus (clic, `Échap`) et le panneau `#main-nav` (`aria-expanded`) sous 900 px ; fermeture au clic extérieur et au retour au format bureau. |
| Recherche | `Search.astro` (`is:inline`) | Charge Pagefind au premier clic, gère la modale (focus piégé, `Échap`, défilement verrouillé, focus rendu). |
| Sommaire | `TableOfContents.astro` (`is:inline`) | Construit la liste depuis `main .sh`, attribue les `id` et révèle le sommaire à partir de trois entrées. |
| Article | `actualites/[...slug].astro` (`is:inline`) | Boutons de partage, copie du lien, barre de progression de lecture. |
| 404 (choix de langue) | `404.astro` | Affiche le bloc FR ou EN selon le chemin, ajuste `lang`. |

L'accordéon FAQ utilise l'élément natif `<details>` : aucune ligne de JS. Les
**sous-menus de bureau** sont cliquables uniquement (bouton + `aria-expanded`),
sans dépendance au survol. Le site reste lisible sans JavaScript ; la recherche
et les sommaires sont des améliorations.

## 5. Flux de données

```text
docs/ (référence) ──► site/src/data/*.ts ──► composants Astro ──► HTML statique
                              ▲
                              └── site/src/content/actualites/*.md ──► flux RSS
```

1. L'équipe met à jour `site/src/data/` (chiffres, statuts, liens) à partir des
   sources de gouvernance (`../governance/LIVEX-STATUS.md`, `VERSIONING.md`).
2. Les pages importent ces données et les rendent ; **aucune valeur factuelle
   n'est écrite en dur dans une page**.
3. La collection `actualites` alimente l'index du journal, les articles
   (`actualites/[...slug].astro`) et `feed.xml`.
4. `data/versions.ts` alimente les fiches de version (`versions/<version>/`) ;
   sources vides = routes générées à zéro page (aucune erreur de build).

## 6. Gestion des ressources

- **Images et SVG** : importés depuis `src/assets/` (Astro gère le chemin et
  l'empreinte) ; les SVG de marque sont importés via `.src`.
- **Images dans les articles** : syntaxe Markdown avec un chemin relatif vers
  `src/assets/` (`![alt](../../assets/captures/fichier.png)`). Astro les
  optimise (WebP, dimensions, `loading="lazy"`). Un `<img>` écrit en dur dans
  le Markdown n'est pas optimisé : à éviter. `public/captures/` reste le kit
  média brut, non optimisé.
- **Polices** : définies en `@font-face` dans `base.css`, fichiers dans
  `src/assets/fonts/`, préchargées dans `BaseLayout.astro`.
- **Emplacements de capture** : tant qu'une capture réelle n'existe pas, la
  classe `.ph` affiche un cadre pointillé — **jamais** une illustration ou un
  logo à la place d'une capture (règle éditoriale, `../governance/LIVEX-STATUS.md`).

## 7. Points sensibles

- **`base` non standard** : tester le build via `npm run preview`, qui sert sous
  `/LIVEX/`, et non via un serveur statique à la racine.
- **Encodage** : écrire les fichiers en UTF-8 sans BOM (accents du contenu
  français).
- **Pas de JS obligatoire** : toute fonctionnalité doit dégrader proprement
  sans JavaScript (voir `ACCESSIBILITY.md` §5).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter l'architecture du site vitrine. |
| 14 octobre 2026 | Routes dynamiques (`versions/<version>/`, `actualites/<slug>/`) dans le flux de données. | Alignement sur le cahier des charges (S14, S18). |
| 14 octobre 2026 | Menu mobile ajouté (4e script client) ; dégradation sans JS documentée. | Menu responsive pour tablette portrait et mobile. |
| 21 octobre 2026 | Sous-menus cliquables ; scripts recherche, sommaire et article documentés. | Revue globale et ajout de Pagefind, des sommaires et du partage d'articles. |
