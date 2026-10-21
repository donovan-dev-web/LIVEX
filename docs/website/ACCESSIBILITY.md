# ACCESSIBILITY.md — accessibilité

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `../../site/src/layouts/BaseLayout.astro`, `../../site/src/components/`, `../../site/src/styles/base.css`

---

## 1. Objectif

Le site vise **WCAG 2.2 niveau AA**. Il fonctionne **sans JavaScript** pour tout
son contenu ; le menu mobile, la recherche, les sommaires, le partage d'article
et la bascule de thème sont des améliorations progressives.

## 2. Structure et repères

| Élément | Implémentation |
| :-- | :-- |
| Langue du document | `<html lang="fr|en">`, ajusté sur la 404 par script. |
| Lien d'évitement | `.skip` → `#contenu` (`<main id="contenu" tabindex="-1">`). |
| Repères | `<header>`, `<main>`, `<footer>`, `<nav>` labellisés (`aria-label` : « Navigation principale », « Fil d'Ariane », « Navigation de pied de page », « Langue »). |
| Titres | Un seul `<h1>` par page ; hiérarchie `h2`/`h3` cohérente. |
| Fil d'Ariane | `PageHeader`, `aria-label` issu des traductions. |

## 3. Focus et clavier

- `:focus-visible` : contour cobalt (`--co`) de 2 px, décalage 3 px.
- Tous les contrôles sont atteignables au clavier : liens, sous-menus,
  sélecteur de langue, recherche, bouton de thème, accordéons FAQ.
- Le lien d'évitement devient visible au focus.
- Les sous-menus s'ouvrent au clic et se ferment avec `Échap`, qui rend le
  focus au bouton déclencheur.
- La modale de recherche piège volontairement le focus tant qu'elle est
  ouverte, verrouille le défilement et le rend au déclencheur à la fermeture.
- Aucun piège de focus hors modale ; aucun menu dépendant du survol.

## 4. Contrastes et couleurs

- Les jetons de couleur (`DESIGN_SYSTEM.md` §2) sont choisis pour respecter le
  contraste AA sur fond clair **et** sombre.
- L'information n'est **jamais** portée par la seule couleur : les statuts
  (`StatusBadge`) joignent un **glyphe** distinct et un **libellé** texte ; le
  glyphe est `aria-hidden`, le texte porte le sens.
- Le thème sombre est une variante manuelle, testée pour le contraste.

## 5. Sans JavaScript (dégradation)

- La navigation, le contenu, les tableaux et la FAQ fonctionnent sans JS.
- Le **menu mobile** (≤ 900 px) est piloté par un petit script
  (`Header.astro`) ; sans JS, un `<noscript>` rétablit la barre défilante, et
  le pied de page ainsi que le plan du site listent toutes les pages.
- La **recherche** Pagefind et les **sommaires techniques** sont des
  améliorations : sans JS, la recherche n'est pas disponible et les sommaires
  restent masqués (aucun contrôle vide). Les articles du journal conservent
  leur sommaire rendu côté serveur.
- La 404 contient un `<noscript>` offrant les liens Accueil (FR) et Home (EN),
  car le choix bilingue y repose sur un script.
- La bascule de thème et le partage d'article sont des améliorations : sans JS,
  le thème clair (défaut) s'affiche et les liens de partage restent des liens.
- **Aucune redirection automatique** : pas de `meta refresh` ni de script de
  redirection linguistique.

## 6. Mouvement

- `@media (prefers-reduced-motion: reduce)` neutralise transitions et
  animations et coupe le défilement fluide global (`base.css`).
- La seule animation continue est la pulsation décorative de l'illustration du
  hero (`.hero-svg .dt`), conditionnée par
  `@media (prefers-reduced-motion: no-preference)`. Aucun canvas animé.

## 7. Images et illustrations

- Le logo LIVEX de l'en-tête est porté par un lien avec `aria-label` ; l'image
  a `alt=""` (décorative).
- L'illustration d'accueil (`HeroFigure`) est un SVG `role="img"` avec un
  `aria-label` descriptif localisé ; sa légende précise « illustration
  conceptuelle, pas une capture ».
- Toute capture réelle doit recevoir une alternative textuelle pertinente.

## 8. Formulaires et données

- Le site ne collecte **aucune donnée** : pas de formulaire, pas de cookie, pas
  de traceur, polices auto-hébergées.
- Le seul stockage local est la préférence de thème
  (`localStorage['livex-theme']`), sans portée d'identification.

## 9. Déclaration

La page `accessibilite/` porte la déclaration d'accessibilité destinée au
public. Elle doit rester cohérente avec les choix ci-dessus.

## 10. Vérification

- `npm run check` (types), `npm run build` et `npm run check:links` avant toute
  publication (`MAINTENANCE.md` §9).
- Audit automatisé : un seul `h1` par page, hiérarchie de titres sans saut,
  `alt` et dimensions des images, `rel` sur les liens `target="_blank"`,
  JSON-LD valide, ancres internes résolues.
- Lighthouse CI (`lighthouse.yml`) sur l'accueil, l'architecture et les
  résultats.
- Vérification manuelle clavier + lecteur d'écran des pages modifiées.
- Tester les deux thèmes, le zoom à 200 % et la recherche après un build.

### 10.1 Contrôle du 21 octobre 2026

Contrôle automatisé sur le build complet (80 pages) :

| Point | Résultat |
| :-- | :-- |
| Hiérarchie des titres (sauts de niveau) | 0 |
| Identifiants dupliqués | 0 |
| Ancres internes | 207 résolues, 0 cassée |
| `alt` et dimensions des images | 105 images, 0 manquante |
| `rel="noopener"` sur `target="_blank"` | 0 manquant |
| JSON-LD | 100 % analysable |
| `hreflang` / `canonical` | 225 alternates valides, 0 écart |
| Lighthouse (accueil, EN, architecture, résultats, article, plan du site) | performance 100, accessibilité 100, bonnes pratiques 100, SEO 100 |

Contrôle manuel piloté (navigateur) : recherche (ouverture, saisie, résultats,
`Échap`, restitution du focus), sous-menus (`aria-expanded`, `Échap`), thème
sombre persisté, menu mobile (390 px et 320 px, absence de débordement
horizontal).

L'évaluation humaine avec un lecteur d'écran reste à faire : c'est la limite
déclarée sur la page `accessibilite/`.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter les exigences d'accessibilité du site. |
| 14 octobre 2026 | Dégradation sans JS du menu mobile ; référence de section corrigée. | Ajout du menu mobile responsive. |
| 21 octobre 2026 | Recherche modale, sommaires, sous-menus cliquables, animation du hero et audits automatisés. | Revue globale et mise en conformité WCAG 2.2 AA. |
