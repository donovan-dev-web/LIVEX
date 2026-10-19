# COMPONENTS.md — composants Astro

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
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
| `image` | `string?` | OG par défaut | Image Open Graph. |
| `noindex` | `boolean` | `false` | Ajoute `<meta name="robots" content="noindex">`. |
| `lang` | `'fr' \| 'en'` | `'fr'` | Langue du document. |
| `translated` | `boolean` | `false` | Affiche le bandeau « traduction automatique ». |

### Émis dans `<head>`

`canonical`, alternates `hreflang` (fr/en + `x-default`), favicon, manifeste,
`theme-color`, lien RSS, balises Open Graph et Twitter, préchargement des
polices, et un bloc **JSON-LD** `WebSite`. Le détail est dans `SEO.md`.

### Comportement

- Applique le thème stocké **avant** le rendu.
- Insère le lien d'évitement (`.skip`), le bandeau `translated` éventuel,
  `Header`, `<main id="contenu">` (le `slot`), puis `Footer`.
- Câble le bouton `#theme-toggle` (fin de `<body>`).

## 2. `Header.astro`

Barre supérieure collante.

| Prop | Type | Défaut |
| :-- | :-- | :-- |
| `lang` | `'fr' \| 'en'` | `'fr'` |

Contenu : logo LIVEX (lien accueil), navigation principale (`NAV`), sélecteur
de langue (FR/EN), bouton de thème.

- L'onglet courant reçoit `aria-current="page"` (comparaison normalisée des
  URL via `url()`).
- Le sélecteur de langue pointe vers la **même page** dans l'autre langue
  (`url(langPath, code)`), avec `hreflang` et `aria-current`.
- Cas particulier `404` : `langPath` est forcé à `''` pour éviter un lien
  `/en/404/`.
- **Sous-menus** : une entrée de `NAV` dotée de `children` rend un `<div
  class="ni">` contenant l'`<a>` parent (avec `aria-haspopup="true"` et une
  flèche `▾` `aria-hidden`) puis un `<ul class="sub">`. Au-dessus de 900 px,
  l'ouverture se fait en **CSS pur** (`:hover`, `:focus-within`) — aucun
  JavaScript. Le parent reste un lien navigable ; ses enfants deviennent
  tabulables dès que le parent a le focus.
- **Menu mobile** : sous 900 px, un bouton `#nav-toggle.nav-burger`
  (`aria-expanded`, `aria-controls="main-nav"`) ouvre un panneau plein écran
  (`header.site[data-nav-open] nav.main`) où les sous-menus sont dépliés en
  ligne. Un script `is:inline` (~20 lignes) gère l'ouverture/fermeture (clic,
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

## 7. Sources de données associées

Les composants s'appuient sur `site/src/data/` (`site.ts`, `components.ts`,
`release.ts`, `versions.ts`, `claims.ts`) — voir `CONTENT.md`. Aucune chaîne
factuelle n'est écrite en dur dans un composant.

## 8. Conventions

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
