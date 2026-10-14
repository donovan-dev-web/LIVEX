# PAGES.md — cartographie des pages

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : `../../site/src/pages/`, `../../site/src/data/site.ts`, `I18N.md`

---

## 1. Principe de numérotation

Chaque page intérieure porte un **index** affiché par `PageHeader` (prop
`index`) sous forme de numéro de section (`01`, `02`, …). Les pages qui
décrivent cette page utilisent cet index comme préfixe de sous-section
(la page `05` produit les titres `5.1`, `5.2`, …).

- L'**accueil** et la **404** n'ont pas d'index.
- La numérotation suit l'**ordre de lecture**, pas l'arborescence des dossiers.
- La même numérotation est utilisée côté français et côté anglais.

## 2. Plan français (langue source)

| Index | Chemin | Rôle |
| :-- | :-- | :-- |
| — | `/` | Accueil : accroche, bandeau d'état, trois composants, journal, note de méthode. |
| 01 | `comprendre/` | Présentation du projet, méthode, ce qu'est LIVEX. |
| 02 | `comprendre/limites/` | Limites et portée : ce que le projet ne prétend pas. |
| 03 | `comprendre/emergence/` | Explication de l'émergence (définitions, exemples). |
| 04 | `comprendre/glossaire/` | Glossaire des termes du domaine. |
| 05 | `technique/` | Architecture : aperçu des couches et des composants. |
| 06 | `technique/syne/` | SYNE — moteur de simulation. |
| 07 | `technique/echos/` | ECHOS — observation et analyse. |
| 08 | `technique/prism/` | PRISM — rendu perceptif. |
| 09 | `technique/launcher/` | Launcher — orchestration de la pile. |
| 10 | `technique/resultats/` | Reproductibilité et mesures (déterminisme, tests, couverture). |
| 11 | `telecharger/` | Télécharger : état neutre (aucune release publique). |
| 12 | `telecharger/installation/` | Installation depuis les sources. |
| 13 | `versions/` | Versions des composants et état du dépôt. |
| 14 | `feuille-de-route/` | Feuille de route (jalons, périmètre). |
| 15 | `documentation/` | Accès à la documentation du dépôt. |
| 16 | `faq/` | Questions fréquentes (accordéons `<details>`). |
| 17 | `actualites/` | Journal (index) + flux RSS. |
| 18 | `suivre/` | Suivre le projet (canaux). |
| 19 | `participer/` | Participer / contribuer. |
| 20 | `a-propos/` | À propos du projet et de son auteur. |
| 21 | `a-propos/transparence-ia/` | Transparence sur l'usage de l'IA. |
| 22 | `mentions-legales/` | Mentions légales. |
| 23 | `confidentialite/` | Confidentialité (aucun traceur, aucun cookie). |
| 24 | `accessibilite/` | Déclaration d'accessibilité. |
| 25 | `licence/` | Licence et réutilisation (MIT / CC BY 4.0). |
| 26 | `plan-du-site/` | Plan du site, groupé par **blocs A–G** (FR et EN). |
| 27 | `presse/` | Kit média (éléments de langage, logo, faits clés, contact). |
| — | `404.html` | Page introuvable, bilingue, `noindex`. |

### 2.1 Routes dynamiques

Deux routes sont générées à partir de données ; elles produisent **zéro page**
tant que la source est vide, sans erreur de build :

| Chemin | Source | Rôle |
| :-- | :-- | :-- |
| `versions/<version>/` | `site/src/data/versions.ts` (`VERSIONS`) | Fiche d'une version publiée (notes, téléchargements, empreintes). Vide à ce jour (aucune release). |
| `actualites/<slug>/` | collection `actualites` (`src/content/actualites/*.md`) | Article de journal. Vide à ce jour. **Français uniquement** (niveau 3). |

## 3. Miroir anglais (`/en/`)

Toutes les pages ci-dessus existent en anglais sous le **même chemin**,
préfixé par `en/` — sauf :

- l'**accueil** anglais est `/en/` ;
- il n'existe **pas** de `/en/404` : la 404 est **unique** et bilingue
  (`404.astro`) ;
- le **flux RSS** reste français (`actualites/feed.xml`) ; `/en/actualites/`
  affiche un état « journal en français » ;
- les **articles** du journal ne sont publiés qu'en français (niveau 3,
  `I18N.md` §4) ; `en/versions/<version>/` existe en revanche pour chaque fiche
  de version (miroir de la route dynamique).

Le niveau de traduction de chaque page (1 ou 2) est défini dans `I18N.md` §4.
Le document de niveau 2 affiche un bandeau « traduction automatique ».

## 4. Navigation

### 4.1 En-tête (`NAV`)

Six entrées, servies par `Header.astro` depuis `site/src/data/site.ts`. Quatre
d'entre elles ouvrent un **sous-menu déroulant** (`children`) :

| Clé | Cible | Libellé FR / EN | Sous-entières |
| :-- | :-- | :-- | :-- |
| `comprendre` | `comprendre/` | Comprendre / Understand | Limites et portée · L'émergence expliquée · Glossaire |
| `architecture` | `technique/` | Architecture / Architecture | SYNE · ECHOS · PRISM · Launcher · Reproductibilité et mesures |
| `telecharger` | `telecharger/` | Télécharger / Download | Installation et prérequis |
| `journal` | `actualites/` | Journal / Journal | — |
| `documentation` | `documentation/` | Documentation / Documentation | — |
| `apropos` | `a-propos/` | À propos / About | Transparence sur l'usage de l'IA |

Sur **bureau et tablette paysage** (> 900 px), le sous-menu s'ouvre **au
survol** et **au focus clavier** (`:focus-within`), sans JavaScript : l'`<a>`
parent reste un lien réel, et ses liens enfants deviennent tabulables dès que
le parent a le focus.

Sur **tablette portrait et mobile** (≤ 900 px), la barre devient un **menu
déroulant** : un bouton hamburger (`#nav-toggle`, `aria-expanded`) ouvre un
panneau plein écran où les sous-menus sont **dépliés en ligne**, en retrait.
Le panneau se ferme au clic sur un lien, au clic à l'extérieur, via `Échap`, et
automatiquement au retour au format bureau. Sans JavaScript, un `<noscript>`
maintient la barre défilante (les liens restent aussi accessibles via le pied
de page et le plan du site).

### 4.2 Pied de page (`FOOTER_NAV`)

Dix-neuf entrées (`../CONTENT.md` §2), dont **Kit média** / **Press kit**, plus
deux liens externes : le dépôt GitHub et la licence.

## 5. Éléments transverses

| Élément | Chemin | Notes |
| :-- | :-- | :-- |
| Flux RSS | `actualites/feed.xml` | Généré par `actualites/feed.xml.ts`, langue `fr`. |
| Sitemap | `sitemap-index.xml` | Généré par `@astrojs/sitemap`, avec alternates `hreflang`. |
| Page 404 | `404.html` | Racine du build, pas de dossier `404/`. |
| Robots | `robots.txt` | Pointe vers le sitemap. |
| Manifeste | `site.webmanifest` | `start_url` et `scope` = `/LIVEX/`. |

## 6. Ajouter une page

Voir `MAINTENANCE.md` §2. Résumé : créer `src/pages/<chemin>.astro` (FR) et
`src/pages/en/<chemin>.astro` (EN), leur attribuer un index, les référencer
dans `NAV` ou `FOOTER_NAV` si nécessaire (ajouter une éventuelle sous-entrée
via `children`), et mettre à jour `plan-du-site`.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Cartographier les pages et la navigation. |
| 14 octobre 2026 | Ajout de `presse/` (27), des routes dynamiques `versions/<version>/` et `actualites/<slug>/`, des sous-menus d'en-tête et du pied de page à 19 entrées. | Alignement sur le cahier des charges (S14, S18, S23). |
| 14 octobre 2026 | Menu mobile (≤ 900 px) pour les sous-menus de navigation. | Rendre la navigation utilisable sur tablette portrait et mobile. |
