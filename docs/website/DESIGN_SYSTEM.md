# DESIGN_SYSTEM.md — jetons de design et styles

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `../../site/src/styles/tokens.css`, `../../site/src/styles/base.css`, `../../site/src/styles/components.css`

---

## 1. Intention

Le site reprend la maquette v3 « **planche d'observation** » : fond papier de
laboratoire, grille fine, filets fins, accent cobalt (observation) et
vermillon (événement). L'esthétique est sobre et technique ; les illustrations
sont conceptuelles et étiquetées comme telles.

## 2. Jetons de design (`tokens.css`)

Tous les jetons sont définis sur `:root` (thème clair) et surchargés par
`:root[data-theme='dark']`. Aucune couleur codée en dur ne doit apparaître
hors de ce fichier.

### 2.1 Couleurs

| Jeton | Rôle | Clair | Sombre |
| :-- | :-- | :-- | :-- |
| `--bg` | papier de labo (fond) | `#F2F2EC` | `#090C16` |
| `--sf` | surface (planches, cartes) | `#FAFAF6` | `#10152A` |
| `--ink` | encre (texte, cadres) | `#0A0E1A` | `#ECEDE6` |
| `--mu` | gris instrument (annotations) | `#5A6070` | `#9AA1B5` |
| `--ln` | filets | `#CFD0C6` | `#2A314B` |
| `--gr` | quadrillage | `#E3E4DB` | `#151A2E` |
| `--co` | cobalt · observation | `#2432E8` | `#8A96FF` |
| `--ve` | vermillon · événement | `#CC3209` | `#FF7A45` |
| `--vef` | vermillon vif | `#FF5A1F` | `#FF7A45` |
| `--ok` | validé | `#0B7A4B` | `#4FD39A` |

### 2.2 Géométrie

| Jeton | Valeur | Rôle |
| :-- | :-- | :-- |
| `--r` | `2px` | Rayon de bordure (angles volontairement nets). |
| `--w` | `1180px` | Largeur maximale de la colonne de contenu (`.w`). |
| `--grid` | `48px` | Pas du quadrillage de fond. |

`color-scheme` est positionné par thème (`light` / `dark`) pour que les
contrôles natifs (barres de défilement, sélecteurs) suivent le thème.

## 3. Thèmes (clair / sombre)

- Le **clair est le défaut**. Le sombre s'active via l'attribut
  `data-theme="dark"` sur `<html>`.
- La préférence est mémorisée dans `localStorage` sous la clé `livex-theme`.
- Elle est appliquée par un script en ligne **avant le rendu** (pas de
  clignotement) et basculée par le bouton `#theme-toggle` de l'en-tête
  (`COMPONENTS.md` §3).
- Le site **ne suit pas** `prefers-color-scheme` : le choix reste explicite.
- La balise `<meta name="theme-color">` déclare les deux couleurs système.

## 4. Typographie

Deux polices, **auto-hébergées** (`src/assets/fonts/`, licence SIL OFL),
déclarées dans `base.css` :

| Police | Usage | Détail |
| :-- | :-- | :-- |
| **Instrument Serif** | titres (`h1`–`h4`, `.ptitle`, `figcaption` partiel, chiffres `.n`) | 400, normal + italique (le `<i>` d'un titre passe en cobalt). |
| **JetBrains Mono** | corps de texte, annotations, code | Variable 400–700. |

Le corps utilise JetBrains Mono à `0.86rem` / interligne `1.7`. Les fichiers
`woff2` sont **préchargés** dans `BaseLayout.astro`.

## 5. Fond et grille

Le `body` dessine un quadrillage par deux dégradés linéaires répétés
(`var(--gr)` sur un pas de `var(--grid)`), produisant l'effet « papier
millimétré ».

## 6. Classes utilitaires et composants (`base.css`)

| Classe | Rôle |
| :-- | :-- |
| `.w` | Colonne centrée de largeur `--w`. |
| `.sf` | Force la police serif. |
| `.mu` / `.co` / `.ve` | Couleur gris instrument / cobalt / vermillon. |
| `.lead` | Paragraphe d'introduction. |
| `.sr-only` | Contenu réservé aux lecteurs d'écran. |
| `.skip` | Lien d'évitement (« Aller au contenu »). |
| `pre` / `:not(pre) > code` | Blocs de code et code en ligne. |
| `:focus-visible` | Contour de focus cobalt (`ACCESSIBILITY.md` §3). |

## 7. Composants visuels (`components.css`)

| Classe | Rôle |
| :-- | :-- |
| `.br` | Cadre à repères (coins de visée), en `::before`/`::after`. |
| `.sh` | Étiquette de section : index vermillon + filet dégradé. |
| `section.block` | Espacement vertical des sections. |
| `.hero` | Grille d'accueil (texte / figure). |
| `.ey` | Surtitre vermillon (petites capitales espacées). |
| `.pl`, `figure.pl` | Panneau encadré ; `.pl small` pour une note. |
| `.g3` / `.g2` | Grilles 3 colonnes encadrée / auto-ajustée. |
| `.n` | Grand chiffre serif italique cobalt. |
| `.bd` (+ `.ok` `.ex` `.dv` `.en` `.hp`) | Badges de statut (voir `StatusBadge`). |
| `.bn` | Ligne d'architecture à très grand nom (contour au survol). |
| `.ct` | Bandeau de mention centré (filet pointillé). |
| `.ph` | Emplacement de capture (cadre pointillé hachuré). |
| `figure.shot` | Figure d'une capture réelle. |
| `.cc` | Encart « prudence » (filet vermillon à gauche). |
| `.row` | Ligne de relevé/journal (date · titre · statut). |
| `.releve` | Relevé technique (seed/tick). |
| `.ov` + `table.tb` | Tableau à défilement horizontal. |
| `details.faq` | Accordéon natif (signe `+`/`–` vermillon). |
| `.crumb` | Fil d'Ariane. |
| `.ptitle` | Titre de page intérieure. |
| `.toc` | Sommaire repliable (technique) ou sommaire d'article. |
| `.tnote` | Bandeau de traduction automatique (`I18N.md` §5). |
| `.fm` | Barre de formule (serif italique + annotations). |
| `.vs` | Bandeau d'état (badge + phrase). |

## 8. Responsive

Deux points de rupture structurent la mise en page :

- **`900px`** — bascule de navigation. Au-dessus : barre horizontale et
  **sous-menus cliquables** (`.ni`, `.sub`, `.ni-arrow`) ouverts par le bouton
  parent, sans survol. En dessous : bouton hamburger (`.nav-burger`) et panneau
  plein écran (`header.site[data-nav-open] nav.main`) avec sous-menus repliés.
- **`820px`** — passage en une seule colonne (`base.css` global) : `.hero`,
  `.g3` et `.bn` s'empilent, les `.row` aussi.

Un réglage mineur à **`380px`** masque le mot-symbole « LIVEX » (le logo seul
reste). Les marges sûres (`env(safe-area-inset-*)`) sont gérées pour les
encoches mobiles.

## 9. Règles

- Ne jamais écrire une couleur/rayon/grille en dur : utiliser un jeton.
- Toute nouvelle classe de composant va dans `components.css`, jamais en style
  en ligne, sauf exception ponctuelle (déjà présente dans les pages).
- Respecter `prefers-reduced-motion` (déjà global, `base.css`).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter les jetons et les styles du site. |
| 14 octobre 2026 | Sous-menus d'en-tête, menu mobile et points de rupture 900/820/380 px. | Ajout des menus déroulants et du menu mobile. |
| 21 octobre 2026 | Sous-menus cliquables (plus de survol) ; sommaire repliable. | Revue globale et accessibilité des sous-menus. |
