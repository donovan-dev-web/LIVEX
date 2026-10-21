# COMPONENTS.md — composants Astro

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `../../site/src/components/`, `../../site/src/layouts/BaseLayout.astro`, `I18N.md`

---

Le site ne comporte qu'un petit nombre de composants. Tous reçoivent la
**langue** (`lang`) et s'appuient sur les helpers de `site/src/i18n/ui.ts` et
`site/src/utils/url.ts`.

## 1. `BaseLayout.astro`

Enveloppe de toutes les pages. Fournit `<html>`, `<head>`, l'en-tête, le pied
de page et les scripts de thème.

### Propriétés

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `title` | `string` | requis | Titre de page ; ` · LIVEX` est ajouté sauf si le titre commence déjà par « LIVEX ». |
| `description` | `string` | requis | Métadonnée `description` + OG/Twitter. |
| `ogType` | `'website' \| 'article'` | `'website'` | Type Open Graph. |
| `article` | `{ publishedTime?, author?, section?, tags? }` | — | Métadonnées `article:*` pour le journal. |
| `image` | `string?` | OG par défaut | Image Open Graph. |
| `noindex` | `boolean` | `false` | Ajoute `<meta name="robots" content="noindex">`. |
| `canonical` | `string?` | URL courante | Surcharge l'URL canonique (la 404 pointe vers `404.html`). |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue du document. |
| `translated` | `boolean` | `false` | Affiche le bandeau « traduction automatique ». |
| `jsonLd` | objet ou tableau | — | JSON-LD additionnel (`SoftwareApplication`, `Article`, etc.). |
| `alternates` | `Partial<Record<Lang, string>>` | — | Chemin de repli pour les pages sans équivalent dans l'autre langue. |

### Émis dans `<head>`

`canonical`, alternates `hreflang` (fr/en + `x-default`), favicon, icône Apple
Touch, manifeste, `theme-color`, lien RSS, balises Open Graph et Twitter (avec
dimensions et texte alternatif de l'image), préchargement des polices, et des
blocs **JSON-LD** `WebSite`, `Organization` et `Person`. Le détail est dans
`SEO.md`.

### Comportement

- Applique le thème stocké **avant** le rendu.
- Insère le lien d'évitement (`.skip`), le bandeau `translated` éventuel,
  `Header`, `<main id="contenu" tabindex="-1">` (le `slot`), puis `Footer`.
- Câble le bouton `#theme-toggle` (fin de `<body>`).

## 2. `Header.astro`

Barre supérieure collante.

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue courante. |
| `langLinks` | `Partial<Record<Lang, string>>` | — | Chemins de la même page dans chaque langue ; une langue absente n'est pas proposée. |

Contenu : logo LIVEX (lien accueil), navigation principale (`NAV`), recherche
(`Search`), sélecteur de langue (FR/EN), bouton de thème.

- L'onglet courant reçoit `aria-current="page"` (comparaison exacte des URL
  normalisées via `url()`).
- Le sélecteur de langue pointe vers la **même page** dans l'autre langue
  (`url(langPath, code)`), avec `hreflang` et `aria-current`.
- Cas particulier `404` : `langPath` est forcé à `''` pour éviter un lien
  `/en/404/`.
- **Sous-menus** : une entrée de `NAV` dotée de `children` rend un `<div
  class="ni">` contenant un **bouton** parent (`aria-expanded`,
  `aria-controls`, flèche `▾` `aria-hidden`) puis un `<ul class="sub">`.
  L'ouverture est **cliquable uniquement** (pas de survol ni de
  `:focus-within`) : un seul sous-menu est ouvert à la fois, `Échap` ferme le
  panneau et rend le focus au bouton. Le panneau est masqué par défaut, ce qui
  évite les sous-menus ouverts au chargement.
- **Menu mobile** : sous 900 px, un bouton `#nav-toggle.nav-burger`
  (`aria-expanded`, `aria-controls="main-nav"`) ouvre un panneau plein écran
  (`header.site[data-nav-open] nav.main`) où les sous-menus sont repliés et
  s'ouvrent au clic. Un script `is:inline` gère l'ouverture/fermeture (clic,
  `Échap`, clic extérieur, retour au format bureau) et un `<noscript>` assure
  un repli sans JS. Piège d'accessibilité documenté dans `MAINTENANCE.md` §8.

## 3. `Footer.astro`

Pied de page.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` |

Contenu : navigation de pied de page (`FOOTER_NAV`), lien dépôt GitHub, lien
licence, mention de copyright et double licence (GPL-3.0-or-later pour le code, CC BY 4.0
pour le contenu), et rappel « aucun traceur, aucun cookie, polices
auto-hébergées ».

## 4. `PageHeader.astro`

En-tête des pages intérieures (fil d'Ariane + index + titre + chapô).

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `index` | `string` | requis | Numéro de section affiché (ex. `"12"`). |
| `eyebrow` | `string` | requis | Surtitre (ex. `"Télécharger · Installation"`). |
| `title` | `string` | requis | Titre `<h1>` (`.ptitle`). |
| `lead` | `string?` | — | Chapô. |
| `crumbs` | `{ label, href? }[]` | `[]` | Fil d'Ariane après « Accueil ». |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue. |

> **Important** : les `href` de `crumbs` sont des **chemins bruts** (ex.
> `'telecharger/'`), **non** pré-enveloppés par `url()` — le composant applique
> lui-même `url(href, lang)`. Doubler l'appel produit un chemin préfixé deux
> fois.

## 5. `StatusBadge.astro`

Badge de statut de composant.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `status` | `StatusId` | requis |
| `label` | `string?` | libellé du statut |
| `class` | `string?` | — |
| `lang` | `'fr' \| 'en'` | `'fr'` |

Correspondance (`StatusId` défini dans `site/src/data/components.ts`) :

| Statut | Libellé FR / EN | Glyphe | Couleur |
| :-- | :-- | :-- | :-- |
| `disponible` | Disponible / Available | `●` | `--ok` |
| `experimental` | Expérimental / Experimental | `▲` | `--ve` |
| `developpement` | En développement / In development | `◆` | `--co` |
| `envisage` | Envisagé / Envisaged | `○` | `--mu` (filet pointillé) |
| `hors-perimetre` | Hors périmètre / Out of scope | `✕` | `--mu` (barré) |

Le glyphe est `aria-hidden` ; le texte porte le sens.

## 6. `HeroFigure.astro`

Illustration de la page d'accueil : vue éclatée isométrique des **trois
couches** de LIVEX, en SVG.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` |

- Le SVG porte un `role="img"` et un `aria-label` descriptif localisé.
- La légende précise explicitement « **illustration conceptuelle, pas une
  capture** » et nomme les couches : **01** SYNE · vérité du monde · **02**
  ECHOS · observation · **03** PRISM · représentation 3D.
- Les couleurs internes viennent de classes `.hero-svg .*` (thématiques).

Ce composant est partagé par l'accueil FR et l'accueil EN (mêmes tracés, texte
localisé).

## 6 bis. `SyneSchema.astro`

Schéma conceptuel de la **boucle de calcul de SYNE**, en SVG.

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue des libellés. |
| `variant` | `'compact' \| 'full'` | `'compact'` | `compact` : monde, tick, traces (carte, index Architecture). `full` : boucle complète monde → perception → décision → action, et sorties (snapshot, traces). |
| `class` | `string?` | — | Classes additionnelles. |

Pourquoi un schéma plutôt qu'une capture : **SYNE est un moteur, il n'a pas
d'interface** — il n'y a donc aucune capture possible à publier. Le schéma
remplace les emplacements `.ph`, qui sont retirés de la page SYNE.

- Le SVG porte `role="img"` et un `aria-label` localisé ; les libellés sont
  du texte réel (donc traduits et accessibles), pas des images.
- Les couleurs viennent de classes `.syne-schema .*` (thématiques : le schéma
  s'adapte au thème clair et sombre).
- La légende de la figure rappelle « **illustration conceptuelle, pas une
  capture** ».

## 7. `CtaBand.astro`

Bandeau d'appel à l'action réutilisable (conversion).

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue. |
| `eyebrow` | `string?` | — | Surtitre optionnel. |
| `title` | `string` | requis | Titre du bandeau. |
| `lead` | `string?` | — | Chapô. |
| `primaryLabel` | `string` | requis | Libellé du bouton principal. |
| `primaryHref` | `string` | requis | Cible du bouton principal. |
| `secondaryLabel` | `string?` | — | Libellé du bouton secondaire. |
| `secondaryHref` | `string?` | — | Cible du bouton secondaire. |

Les liens externes (`http…`) reçoivent `rel="noopener"` et `target="_blank"` automatiquement.

## 8. `CardLink.astro`

Carte cliquable (panneau `.pl`) pour les parcours d'entrée.

| Prop | Type | Défaut | Rôle |
| :-- | :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue. |
| `title` | `string` | requis | Titre de la carte. |
| `text` | `string` | requis | Description. |
| `href` | `string` | requis | Cible. |
| `cta` | `string?` | libellé par défaut | Libellé du lien. |
| `eyebrow` | `string?` | — | Surtitre optionnel. |

## 9. `Search.astro`

Recherche plein texte (Pagefind), montée dans l'en-tête.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` |

- Bouton `#search-toggle` (`aria-expanded`, `aria-controls="search-modal"`).
- Modale `role="dialog"` `aria-modal="true"`, fermeture par `Échap`, clic sur
  le fond ou bouton ; focus piégé dans la modale, verrouillage du défilement,
  focus rendu au déclencheur à la fermeture.
- Pagefind est chargé **au premier clic** (`pagefind-ui.js` + CSS), depuis
  `${BASE_URL}pagefind/` ; l'index est produit par `postbuild`
  (`pagefind --site dist`). En développement, l'index peut être absent : la
  modale affiche alors une aide.
- La langue de l'interface Pagefind suit `<html lang>` ; aucune option
  `language` n'est passée.

## 10. `TableOfContents.astro`

Sommaire repliable des pages techniques.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` |

- `<details class="toc" data-toc hidden>` + `<nav aria-label>`.
- Le script collecte les en-têtes `main .sh`, leur attribue un `id` et construit
  la liste ; le sommaire n'est révélé que s'il contient au moins trois entrées.
- Sans JavaScript, le composant reste masqué : aucun contrôle vide n'est
  exposé. Les articles du journal ont leur propre sommaire rendu côté serveur.

## 11. Sources de données associées

Les composants s'appuient sur `site/src/data/` (`site.ts`, `components.ts`,
`release.ts`, `versions.ts`, `monograph.ts`, `claims.ts`) — voir `CONTENT.md`.
Aucune chaîne factuelle n'est écrite en dur dans un composant.

## 12. Conventions

- Un composant = un fichier dans `src/components/`.
- Le texte visible est localisé (jamais une chaîne bilingue codée en dur, sauf
  les libellés purement techniques) ; le français et l'anglais vivent dans
  `src/i18n/ui.ts` ou dans les données bilingues.
- Les assets sont **importés** depuis `src/assets/`, jamais référencés par
  chemin absolu.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter l'interface des composants. |
| 14 octobre 2026 | Sous-menus d'en-tête (`children` de `NAV`) et menu mobile documentés. | Ajout des menus déroulants et du menu hamburger. |
| 21 octobre 2026 | Sous-menus cliquables (`aria-expanded`/`aria-controls`), composants `Search` et `TableOfContents`, nouvelles props de `BaseLayout`. | Revue globale et ajout de la recherche Pagefind et des sommaires. |
