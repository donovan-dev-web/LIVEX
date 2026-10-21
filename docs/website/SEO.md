# SEO.md — référencement et métadonnées

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
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
| `og:image` | `media/og-livex.png` (ou prop `image`), 1200 × 630. |
| `og:image:alt`, `og:image:width`, `og:image:height` | Texte alternatif localisé, `1200`, `630`. |
| `og:locale` | `fr_FR` / `en_US`. |
| `og:locale:alternate` | L'autre langue publiée. |
| `twitter:card` | `summary_large_image`. |
| `twitter:title`, `twitter:description`, `twitter:image`, `twitter:image:alt` | Reprise des valeurs Open Graph. |
| `article:published_time`, `article:author`, `article:section`, `article:tag` | Uniquement avec `ogType="article"` et la prop `article`. |

L'image Open Graph par défaut vit dans `site/public/media/og-livex.png`
(1200 × 630). Les images par section sont un chantier ultérieur : toutes les
pages partagent pour l'instant cette image.

## 3. Multilingue (SEO)

- Chaque page déclare son propre `hreflang` **et** celui de sa version dans
  l'autre langue, plus `x-default` (français). Détail : `I18N.md` §6.
- Les chemins d'alternates sont calculés sur la version **sans langue** du
  chemin (`stripLangFromPath`), ce qui garantit la réciprocité FR↔EN.
- Aucune redirection automatique côté serveur : les deux versions sont
  accessibles et indexables.
- **Pages sans équivalent dans l'autre langue** (ex. articles du journal,
  FR-only) : passer `alternates={{}}` à `BaseLayout` pour supprimer le
  `hreflang` et le sélecteur de langue, au lieu de pointer vers une URL
  inexistante. Le journal EN (`/en/actualites/`) reste une page d'état qui
  renvoie explicitement vers les articles français.

## 4. Sitemap

- Généré par `@astrojs/sitemap` avec l'option `i18n`
  (`defaultLocale: 'fr'`, `locales: { fr, en }`).
- Produit `sitemap-index.xml` (+ `sitemap-0.xml`) dans `dist/`, avec les
  alternates `xhtml:link` pour chaque URL (deux langues par page).
- `robots.txt` déclare `Sitemap: https://donovan-dev-web.github.io/LIVEX/sitemap-index.xml`.

## 5. Données structurées (JSON-LD)

Chaque page émet trois nœuds de base — `WebSite`, `Organization` et `Person` —
puis un nœud additionnel selon le contexte :

```json
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "name": "LIVEX",
  "url": "https://donovan-dev-web.github.io/LIVEX/",
  "description": "<description du site>",
  "inLanguage": "fr | en",
  "license": "https://www.gnu.org/licenses/gpl-3.0.html",
  "sameAs": ["https://github.com/donovan-dev-web/LIVEX"]
}
```

- `Organization` : nom, URL racine, logo (`favicon.svg`), dépôt.
- `Person` : auteur et URL du portfolio.
- Accueil : `SoftwareApplication` via la prop `jsonLd`.
- Journal : `Article` via la prop `jsonLd` de `actualites/[...slug].astro`
  (auteur, éditeur, image, date, section, mots-clés, `mainEntityOfPage`).
- Les breadcrumbs sont émis par `PageHeader.astro` en `BreadcrumbList`.

`jsonLd` accepte un objet ou un tableau ; les blocs sont sérialisés avec
`JSON.stringify` et validés à l'audit.

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
| `public/favicon.svg` | Favicon vectoriel. |
| `public/icon-192.png`, `public/icon-512.png` | Icônes PWA (manifeste). |
| `public/apple-touch-icon.png` | Icône iOS (180 × 180). |
| `public/robots.txt` | `Allow: /` + URL du sitemap. |
| `public/llms.txt` | Guide destiné aux assistants IA / crawlers (convention [llmstxt.org](https://llmstxt.org/)) : résumé + liens clés, y compris `getting-started`, `etat-du-projet` et `monographie`. |
| `public/site.webmanifest` | Manifeste PWA (nom, `start_url`/`scope` relatifs, icônes SVG et PNG). |
| `public/media/og-livex.png` | Image Open Graph par défaut (1200 × 630). |
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
| 21 octobre 2026 | Dimensions/alt OG, métadonnées `article:*`, JSON-LD `Organization`/`Person`/`Article`, icônes PWA. | Revue globale du SEO et des métadonnées. |
