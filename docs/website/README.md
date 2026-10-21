# README.md — documentation du site vitrine

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : `ARCHITECTURE.md`, `../presentation/VISION.md`, `../../VERSIONING.md`

---

Cette section documente le **site vitrine public** de LIVEX : le site statique
hébergé sur GitHub Pages, sa construction, son contenu et ses règles de
maintenance. Elle ne décrit pas le simulateur ni ses composants — voir
`../syne/`, `../echos/`, `../prism/`, `../launcher/`.

## 1. En bref

| Élément | Valeur |
| :-- | :-- |
| URL publique | https://donovan-dev-web.github.io/LIVEX/ |
| Technologie | Astro (site statique, JavaScript client minimal et progressif) |
| Sources | dossier `site/` du dépôt |
| Langues | français (langue source) et anglais (miroir `/en/`) |
| Recherche | Pagefind, index généré au build (`postbuild`) |
| Hébergement | GitHub Pages (branche `gh-pages`) |
| Déploiement | `.github/workflows/pages.yml` |
| Performance | `.github/workflows/lighthouse.yml` (Lighthouse CI sur PR) |
| Licence | code du site sous GNU GPL v3.0 ou ultérieure (GPL-3.0-or-later), contenu rédactionnel sous CC BY 4.0 |

## 2. Rôle du site

Le site est une **vitrine de recherche** : il présente le projet, son
architecture, son état réel (daté) et sa documentation, sans promesse ni
capture trompeuse. Trois principes gouvernent son contenu :

1. **Source et datation** — toute affirmation chiffrée renvoie à une source
   interne datée (`CONTENT.md` §5).
2. **Neutralité de l'état** — aucune version publique n'existe à ce jour ;
   les pages de téléchargement restent volontairement neutres.
3. **Séparation des couches** — le site ne prétend pas reproduire le
   comportement du moteur ; il le décrit.

## 3. Structure de cette documentation

| Document | Contenu |
| :-- | :-- |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Stack, arborescence de `site/`, pipeline de build, flux de données. |
| [PAGES.md](PAGES.md) | Cartographie des pages FR/EN, numérotation et navigation. |
| [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) | Jetons de design, thèmes, typographie, grille et classes CSS. |
| [COMPONENTS.md](COMPONENTS.md) | Composants Astro (`BaseLayout`, `Header`, `Footer`, `PageHeader`, `StatusBadge`, `HeroFigure`, `Search`, `TableOfContents`). |
| [I18N.md](I18N.md) | Multilinguisme FR/EN : routage, hreflang, niveaux de traduction. |
| [CONTENT.md](CONTENT.md) | Données éditoriales, statuts de composant et registre d'affirmations. |
| [CAPTURES.md](CAPTURES.md) | Inventaire des visuels : sources optimisées, kit média, captures manquantes. |
| [SEO.md](SEO.md) | Métadonnées, sitemap, RSS, robots, Open Graph, JSON-LD. |
| [ACCESSIBILITY.md](ACCESSIBILITY.md) | Exigences et choix d'accessibilité (WCAG 2.2 AA). |
| [DEPLOYMENT.md](DEPLOYMENT.md) | Scripts npm, workflow GitHub Actions, publication `gh-pages`. |
| [MAINTENANCE.md](MAINTENANCE.md) | Procédures pour ajouter une page, un article, modifier le design. |
| [REVIEW-2026-10-21.md](REVIEW-2026-10-21.md) | Revue globale du site : mesures, corrections appliquées et reste à faire. |

## 4. Démarrage rapide

Prérequis : Node.js 20 ou supérieur (les workflows utilisent Node 22).

```bash
cd site
npm ci
npm run dev        # serveur local (http://localhost:4321/LIVEX/)
npm run check      # vérification de types (astro check)
npm run build      # build de production + index Pagefind (postbuild)
npm run check:links # liens internes du build
npm run preview    # prévisualisation du build
```

Le détail des scripts et du déploiement est dans [DEPLOYMENT.md](DEPLOYMENT.md).

## 5. Conventions héritées

Le site suit les conventions générales du projet
(`../NAMING_CONVENTIONS.md`, `../CI_CD.md`, `.editorconfig`) :

- **Identifiants** (fichiers, dossiers, classes, variables) en anglais ;
  **texte visible** et **commentaires** en français.
- Encodage **UTF-8 sans BOM**, fins de ligne **LF**, une ligne vide en fin de
  fichier.
- Liens relatifs uniquement dans la documentation Markdown.
- Un fichier source Astro = un composant ou une page.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Aucune documentation ne couvrait le site vitrine. |
| 21 octobre 2026 | Pagefind, `check:links`, composants `Search`/`TableOfContents`, Lighthouse CI. | Revue globale et validation de la recherche et de la performance. |
