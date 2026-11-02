# CAPTURES.md — inventaire des captures et visuels

**Composant** : Site vitrine (LIVEX)
**Statut** : [EN COURS]
**Dernière mise à jour** : 21 octobre 2026

---

## 1. Objectif

Ce document recense les visuels utilisés ou attendus par le site vitrine, afin de savoir lesquels sont déjà disponibles et lesquels restent à produire. Aucune illustration ne doit tenir lieu de capture réelle : un emplacement réservé (`ph`) reste vide tant qu'une capture authentique n'existe pas.

Deux cas distincts, à ne pas confondre :

- une **capture** montre une interface réelle (ECHOS, PRISM, le Launcher) ;
- un **schéma conceptuel** explique un mécanisme (SYNE, le hero, la pile).

## 2. Visuels existants (`src/assets/captures/`)

Ce sont les sources utilisées par le site. Astro les optimise à la compilation
(WebP, `width`/`height`, `loading="lazy"`).

| Fichier | Type | Usage actuel | Statut |
| :-- | :-- | :-- | :-- |
| `echos-agents.png` | Capture réelle | Page ECHOS (suivi d'agents), accueil, index Architecture | Disponible |
| `echos-relations.png` | Capture réelle | Page ECHOS (relations entre agents), article V0.2 | Disponible |
| `launcher-home.png` | Capture réelle | Page Launcher, article V0.2 | Disponible |
| `launcher-experiences.png` | Capture réelle | Page Launcher (campagnes), article Launcher | Disponible |
| `launcher-prism.png` | Capture réelle | Accueil (PRISM), page PRISM, index Architecture, articles | Disponible |
| `installer-home.png` | Capture réelle | Page publique d'installation (accueil de l'assistant) | Disponible |
| `installer-components.png` | Capture réelle | Page publique d'installation (sélection des composants) | Disponible |

Dans les articles du journal, les images sont référencées en Markdown relatif
(`![alt](../../assets/captures/fichier.png)`) afin de bénéficier de la même
optimisation que les pages.

### 2.1 Schémas conceptuels (SVG en ligne)

| Composant | Source | Usage | Statut |
| :-- | :-- | :-- | :-- |
| Hero — vue éclatée des trois couches | `components/HeroFigure.astro` | Accueil | Disponible |
| Boucle de calcul SYNE | `components/SyneSchema.astro` (`variant="compact"`) | Accueil, index Architecture | Disponible |
| Boucle de calcul SYNE (détaillée) | `components/SyneSchema.astro` (`variant="full"`) | Page SYNE | Disponible |

**SYNE n'aura jamais de capture** : c'est un moteur, il n'a pas d'interface.
Là où la mise en page demande une image, le site montre un schéma conceptuel,
explicitement étiqueté « illustration conceptuelle, pas une capture ».

## 3. Kit média (`public/captures/`)

Miroir public non optimisé : c'est le **kit média** téléchargeable (page
« Presse »), pas la source d'affichage du site.

| Fichier | Type | Usage | Statut |
| :-- | :-- | :-- | :-- |
| `echos-agents.png` | Capture réelle | Kit média | Disponible |
| `echos-relations.png` | Capture réelle | Kit média | Disponible |
| `launcher-experiences.png` | Capture réelle | Kit média | Disponible |
| `launcher-home.png` | Capture réelle | Kit média | Disponible |
| `launcher-prism.png` | Capture réelle | Kit média | Disponible |
| `livex-banner.svg` | Illustration | Bannière | Disponible |

**Aucune animation GIF n'est versionnée dans le site.** Les placeholders
provisoires qui s'y trouvaient ont été supprimés : un GIF est lourd, non
optimisé par `astro:assets`, et n'était affiché nulle part. Si une animation
est un jour nécessaire, elle reste dans le dépôt (`docs/captures/`) et le site
la sert en WebP ou MP4.

## 4. Captures manquantes (emplacement réservé)

| Nom attendu | Type | Page / composant | Description à produire | Statut |
| :-- | :-- | :-- | :-- | :-- |
| Capture SYNE | Capture 16:9 | `technique/syne/` (Fig. 1) | — | **Sans objet** : SYNE est un moteur, sans interface à capturer. Le schéma `SyneSchema` tient lieu de visuel, étiqueté comme conceptuel. |
| Capture Launcher — orchestration | Capture 16:9 | `technique/launcher/` | Écran de pilotage des composants (remplacé partiellement par `launcher-experiences.png`, qui montre la table des expériences) | Partiel |
| Capture ECHOS — analyse | Capture 16:9 | `technique/echos/` | Vue d'analyse et de métriques (`echos-agents.png` montre le suivi, pas les métriques) | Partiel |
| Capture PRISM 16:9 | Capture 16:9 | Accueil / `technique/prism/` | Rendu PRISM plein cadre (le site utilise aujourd'hui la vue PRISM dans le Launcher) | Partiel |

## 5. Règles

- Un visuel n'est publié qu'une fois authentique (voir `ACCESSIBILITY.md` §7 et `DESIGN_SYSTEM.md` §7.3).
- Toute capture doit porter une alternative textuelle pertinente ; l'alternative nomme ce qui est visible, pas ce qui est supposé.
- Les illustrations conceptuelles (ex. `HeroFigure`, `SyneSchema`) doivent rester étiquetées comme telles.
- Un composant sans interface (SYNE) ne reçoit pas d'emplacement `.ph` : on lui donne un schéma, pas un vide.
- Le site rend les captures via `astro:assets` (WebP, dimensions explicites) ; `public/captures/` reste le kit média brut.
- Les animations (GIF) appartiennent au dépôt, pas au site ; si elles sont publiées, elles le sont en WebP ou MP4.
- Mettre à jour ce tableau lors de l'ajout ou du retrait d'un visuel.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | GIF supprimés (site et dépôt), schémas conceptuels SYNE, captures SYNE sans objet, règle sur les animations | Le site n'affichait aucun GIF et SYNE n'a pas d'interface à capturer |
| 21 octobre 2026 | Inventaire aligné sur l'usage réel : sources `src/assets/captures`, kit média `public/captures`, captures partielles signalées | Précision de l'inventaire des visuels |
| 20 octobre 2026 | Création | Inventaire initial des captures et visuels du site. |
