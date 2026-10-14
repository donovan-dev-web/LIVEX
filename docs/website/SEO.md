# SEO.md — référencement et métadonnées

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : `../../site/src/layouts/BaseLayout.astro`, `../../site/astro.config.mjs`, `../../site/public/`

---

## 1. Métadonnées émises

`BaseLayout.astro` génère, pour chaque page :

| Balise | Source | Note |
| :-- | :-- | :-- |
| `<title>` | prop `title` | Suffixe ` · LIVEX` ajouté sauf si le titre commence par « LIVEX ». |
| `<meta name="description">` | prop `description` | — |
| `<link rel="canonical">` | `Astro.url.pathname` + `Astro.site` | URL absolue. |
| `<meta name="robots">` | `noindex` | Présent uniquement si `noindex` (404). |
| alternates `hreflang` | `publishedLangs` | Voir §3. |
| Open Graph / Twitter | §2. | — |
| JSON-LD `WebSite` | §5. | — |

## 2. Réseaux sociaux (Open Graph / Twitter)

| Balise | Valeur |
| :-- | :-- |
| `og:type` | `website` (ou `article`). |
| `og:site_name` | `LIVEX`. |
| `og:title`, `og:description`, `og:url` | Identiques au titre, à la description et à l'URL canonique. |
| `og:image` | `media/og-livex.png` (ou prop `image`). |
| `og:locale` | `fr_FR` / `en_US`. |
| `og:locale:alternate` | L'autre langue publiée. |
| `twitter:card` | `summary_large_image`. |

L'image Open Graph par défaut vit dans `site/public/media/og-livex.png`.

## 3. Multilingue (SEO)

- Chaque page déclare son propre `hreflang` **et** celui de sa version dans
  l'autre langue, plus `x-default` (français). Détail : `I18N.md` §6.
- Les chemins d'alternates sont calculés sur la version **sans langue** du
  chemin (`stripLangFromPath`), ce qui garantit la réciprocité FR↔EN.
- Aucune redirection automatique côté serveur : les deux versions sont
  accessibles et indexables.
- **Pages sans équivalent dans l'autre langue** (ex. articles du journal,
  FR-only) : passer `alternates={{ en: 'actualites/' }}` à `BaseLayout` pour
  que `hreflang` et le sélecteur de langue pointent vers la page EN la plus
  proche au lieu d'une URL inexistante (404).

## 4. Sitemap

- Généré par `@astrojs/sitemap` avec l'option `i18n`
  (`defaultLocale: 'fr'`, `locales: { fr, en }`).
- Produit `sitemap-index.xml` (+ `sitemap-0.xml`) dans `dist/`, avec les
  alternates `xhtml:link` pour chaque URL (deux langues par page).
- `robots.txt` déclare `Sitemap: https://donovan-dev-web.github.io/LIVEX/sitemap-index.xml`.

## 5. Données structurées (JSON-LD)

Un nœud `WebSite` est émis sur chaque page :

```json
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "name": "LIVEX",
  "url": "<URL canonique>",
  "description": "<description>",
  "inLanguage": "fr | en",
  "license": "https://opensource.org/licenses/MIT",
  "sameAs": ["https://github.com/donovan-dev-web/LIVEX"]
}
```

## 6. Flux RSS

- Route `actualites/feed.xml.ts` (`@astrojs/rss`, `prerender = true`).
- Titre « Journal LIVEX », `language: fr`, `trailingSlash: true`.
- Articles non brouillons, triés par date décroissante.
- Déclaré dans `<head>` : `<link rel="alternate" type="application/rss+xml" …>`.
- Le flux est **unique et français** ; les pages anglaises y renvoient via la
  version française.

## 7. Fichiers publics

| Fichier | Rôle |
| :-- | :-- |
| `public/favicon.svg` | Favicon. |
| `public/robots.txt` | `Allow: /` + URL du sitemap. |
| `public/llms.txt` | Guide destiné aux assistants IA / crawlers (convention [llmstxt.org](https://llmstxt.org/)) : résumé + liens clés. |
| `public/site.webmanifest` | Manifeste PWA (nom, `start_url`/`scope` `/LIVEX/`, icône). |
| `public/media/og-livex.png` | Image Open Graph par défaut. |
| `public/.well-known/security.txt` | Contact sécurité, expiration, langues, politique. |
| `public/.nojekyll` | Empêche GitHub Pages d'ignorer les dossiers commençant par `_`. |

## 8. Points d'attention

- **`base`** : toute URL (canonical, OG, sitemap) doit inclure `/LIVEX/` — d'où
  le passage systématique par `url()` / `AbsoluteUrl` et la config `astro`.
- **404** : `noindex`, jamais listée dans le sitemap.
- **Journal vide** : aucune URL d'article tant qu'aucun `.md` n'est publié.
- Ne pas ajouter d'URL absolue locale « à la main » dans les pages ou la doc.
- **`robots.txt` et `llms.txt` d'un site de projet** : servis sous `/LIVEX/` ;
  les crawlers ne lisent `robots.txt` qu'à la **racine du domaine**
  (`https://donovan-dev-web.github.io/robots.txt`), non contrôlée ici. Le
  sitemap doit donc être soumis via la Search Console. `llms.txt` est informatif
  (aucune garantie de lecture).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter le SEO et les métadonnées du site. |
| 14 octobre 2026 | Ajout de `public/llms.txt` et note sur la racine de domaine (robots.txt d'un site de projet). | Réponse au constat d'un `404 /llms.txt` et clarification robots. |
| 14 octobre 2026 | Ajout de la prop `alternates` (pages sans équivalent EN : journal, 404). | Supprimer les liens `hreflang`/sélecteur vers des pages inexistantes (404). |
