# CAPTURES.md — inventaire des captures et visuels

**Composant** : Site vitrine (LIVEX)
**Statut** : [EN COURS]
**Dernière mise à jour** : 21 octobre 2026

---

## 1. Objectif

Ce document recense les visuels utilisés ou attendus par le site vitrine, afin de savoir lesquels sont déjà disponibles et lesquels restent à produire. Aucune illustration ne doit tenir lieu de capture réelle : un emplacement réservé (`ph`) reste vide tant qu'une capture authentique n'existe pas.

## 2. Visuels existants (`src/assets/captures/`)

Ce sont les sources utilisées par le site. Astro les optimise à la compilation
(WebP, `width`/`height`, `loading="lazy"`).

| Fichier | Type | Usage actuel | Statut |
| :-- | :-- | :-- | :-- |
| `echos-agents.png` | Capture réelle | Page ECHOS (suivi d'agents), accueil | Disponible |
| `echos-relations.png` | Capture réelle | Page ECHOS (relations entre agents), article V0.2 | Disponible |
| `launcher-home.png` | Capture réelle | Accueil (SYNE), page Launcher, article V0.2 | Disponible |
| `launcher-experiences.png` | Capture réelle | Page Launcher (campagnes), article Launcher | Disponible |
| `launcher-prism.png` | Capture réelle | Accueil (PRISM), page PRISM, articles | Disponible |

Dans les articles du journal, les images sont référencées en Markdown relatif
(`![alt](../../assets/captures/fichier.png)`) afin de bénéficier de la même
optimisation que les pages.

## 3. Visuels existants (`public/captures/`)

Miroir public non optimisé : c'est le **kit média** téléchargeable (page
« Presse »), pas la source d'affichage du site.

| Fichier | Type | Usage | Statut |
| :-- | :-- | :-- | :-- |
| `echos-agents.png` | Capture réelle | Miroir public | Disponible |
| `echos-analysis.gif` | Animation | Analyse ECHOS | Disponible |
| `echos-relations.png` | Capture réelle | Miroir public | Disponible |
| `launcher-campaign.gif` | Animation | Campagne Launcher | Disponible |
| `launcher-experiences.png` | Capture réelle | Miroir public | Disponible |
| `launcher-home.png` | Capture réelle | Miroir public | Disponible |
| `launcher-prism.png` | Capture réelle | Miroir public | Disponible |
| `livex-banner.svg` | Illustration | Bannière | Disponible |
| `livex-overview.gif` | Animation | Vue d'ensemble | Disponible |
| `prism-render.gif` | Animation | Rendu PRISM | Disponible |

## 4. Captures manquantes (emplacement réservé)

| Nom attendu | Type | Page / composant | Description à produire | Statut |
| :-- | :-- | :-- | :-- | :-- |
| Capture SYNE | Capture 16:9 | `technique/syne/` (Fig. 1) | Vue du moteur SYNE en fonctionnement (monde, agents, ticks) | À réaliser — l'accueil affiche aujourd'hui `launcher-home.png` à la place, avec une alternative qui nomme la source |
| Capture Launcher — orchestration | Capture 16:9 | `technique/launcher/` | Écran de pilotage des composants (remplacé partiellement par `launcher-experiences.png`, qui montre la table des expériences) | Partiel |
| Capture ECHOS — analyse | Capture 16:9 | `technique/echos/` | Vue d'analyse et de métriques (`echos-agents.png` montre le suivi, pas les métriques) | Partiel |
| Capture PRISM 16:9 | Capture 16:9 | Accueil / `technique/prism/` | Rendu PRISM plein cadre (le site utilise aujourd'hui la vue PRISM dans le Launcher) | Partiel |

## 5. Règles

- Un visuel n'est publié qu'une fois authentique (voir `ACCESSIBILITY.md` §7 et `DESIGN_SYSTEM.md` §7.3).
- Toute capture doit porter une alternative textuelle pertinente ; l'alternative nomme ce qui est visible, pas ce qui est supposé.
- Les illustrations conceptuelles (ex. `HeroFigure`) doivent rester étiquetées comme telles.
- Le site rend les captures via `astro:assets` (WebP, dimensions explicites) ; `public/captures/` reste le kit média brut.
- Mettre à jour ce tableau lors de l'ajout ou du retrait d'un visuel.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Inventaire aligné sur l'usage réel : sources `src/assets/captures`, kit média `public/captures`, captures partielles signalées | Précision de l'inventaire des visuels |
| 20 octobre 2026 | Création | Inventaire initial des captures et visuels du site. |
