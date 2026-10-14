# ACCESSIBILITY.md — accessibilité

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : `../../site/src/layouts/BaseLayout.astro`, `../../site/src/components/`, `../../site/src/styles/base.css`

---

## 1. Objectif

Le site vise **WCAG 2.2 niveau AA**. Il fonctionne **sans JavaScript** pour tout
son contenu ; seules la bascule de thème et le choix de langue de la 404 sont
progressivement améliorés par JS.

## 2. Structure et repères

| Élément | Implémentation |
| :-- | :-- |
| Langue du document | `<html lang="fr|en">`, ajusté sur la 404 par script. |
| Lien d'évitement | `.skip` → `#contenu` (`<main id="contenu">`). |
| Repères | `<header>`, `<main>`, `<footer>`, `<nav>` labellisés (`aria-label` : « Navigation principale », « Fil d'Ariane », « Navigation de pied de page », « Langue »). |
| Titres | Un seul `<h1>` par page ; hiérarchie `h2`/`h3` cohérente. |
| Fil d'Ariane | `PageHeader`, `aria-label` issu des traductions. |

## 3. Focus et clavier

- `:focus-visible` : contour cobalt (`--co`) de 2 px, décalage 3 px.
- Tous les contrôles sont atteignables au clavier : liens, sélecteur de langue,
  bouton de thème, accordéons FAQ.
- Le lien d'évitement devient visible au focus.
- Aucun piège de focus ; pas de menu dépendant du survol.

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
- La 404 contient un `<noscript>` offrant les liens Accueil (FR) et Home (EN),
  car le choix bilingue y repose sur un script.
- La bascule de thème est une amélioration : sans JS, le thème clair (défaut)
  s'affiche.
- **Aucune redirection automatique** : pas de `meta refresh` ni de script de
  redirection linguistique.

## 6. Mouvement

- `@media (prefers-reduced-motion: reduce)` neutralise transitions et
  animations et coupe le défilement fluide global (`base.css`).
- Le site n'a **aucune animation continue** ni canvas animé.

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

- `npm run check` (types) et `npm run build` avant toute publication
  (`MAINTENANCE.md` §9).
- Vérification manuelle clavier + lecteur d'écran des pages modifiées.
- Tester les deux thèmes et le zoom à 200 %.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter les exigences d'accessibilité du site. |
| 14 octobre 2026 | Dégradation sans JS du menu mobile ; référence de section corrigée. | Ajout du menu mobile responsive. |
