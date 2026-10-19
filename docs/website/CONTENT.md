# CONTENT.md — données éditoriales et registre d'affirmations

**Composant** : Site vitrine (LIVEX)
**Statut** : [STABLE]
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : `../../site/src/data/`, `../../site/src/content.config.ts`, `../governance/LIVEX-STATUS.md`, `../../VERSIONING.md`

---

## 1. Principe : source unique, données factuelles datées

Aucune page n'écrit un chiffre, une version ou un statut « en dur ». Tout
provient de `site/src/data/`, lui-même aligné sur les sources de gouvernance.
Modifier un fait = modifier **un fichier de données**, pas N pages.

| Fichier | Contenu | Source d'autorité |
| :-- | :-- | :-- |
| `data/site.ts` | Métadonnées du site et navigation | `../presentation/VISION.md` |
| `data/components.ts` | Les trois composants + statuts | `../ARCHITECTURE.md`, `../presentation/GLOSSARY.md` |
| `data/release.ts` | État du projet, versions, tests, couverture | `../governance/LIVEX-STATUS.md`, `VERSIONING.md`, `CHANGELOG.md` |
| `data/versions.ts` | Historique des versions **publiées** (fiches par version) | `VERSIONING.md`, `CHANGELOG.md` |
| `data/claims.ts` | Registre des affirmations sensibles | §5 ci-dessous |

## 2. `site.ts`

| Export | Rôle |
| :-- | :-- |
| `SITE` | Nom, titre, descriptions (FR et `descriptionEn`), dépôt, licence, auteur, langue, favicon. |
| `NAV` | Navigation d'en-tête (6 entrées, dont 4 avec sous-menu). |
| `FOOTER_NAV` | Navigation de pied de page (19 entrées). |

Chaque entrée de navigation est un `NavItem` :

```ts
interface NavItem {
  key: string;                              // identifiant
  href: string;                             // chemin relatif SANS préfixe de langue
  label: Record<Lang, string>;              // { fr, en }
  children?: NavItem[];                     // sous-menu déroulant (en-tête seulement)
}
```

`children` n'est utilisé que par `NAV` (en-tête) ; `FOOTER_NAV` reste plat et
sert de repli d'accessibilité quand le menu mobile (≤ 900 px) limite l'accès
aux sous-menus.

Le `href` ne contient **jamais** `base` ni `en/` : le préfixe est ajouté par
`url()` au rendu.

## 3. `components.ts`

Décrit SYNE, ECHOS et PRISM, avec un miroir anglais `COMPONENTS_EN` (mêmes
`key`, mêmes `href`, texte traduit).

| Champ | Rôle |
| :-- | :-- |
| `key` | `'syne' \| 'echos' \| 'prism'`. |
| `name`, `acronym` | Nom et développement de l'acronyme (autorité : `GLOSSARY.md`). |
| `role`, `summary` | Rôle court et résumé affiché sur l'accueil. |
| `version`, `versionLabel` | Version du dépôt et libellé. |
| `status` | `{ id, label, note }` — note **datée**. |
| `href` | Page de détail (`technique/<composant>/`). |
| `contracts` | Contrats d'interface publics éventuels. |

**Acronymes autoritaires** (ne pas déformer) :

| Composant | Développement |
| :-- | :-- |
| SYNE | Systems & Emergent Network Engine |
| ECHOS | Emergent Cognition & Holistic Observation System |
| PRISM | Perceptual Rendering & Interactive Simulation Module |

### 3.1 Statuts (`StatusId`)

Cinq statuts, dans l'ordre `STATUS_ORDER` :

`disponible` · `experimental` · `developpement` · `envisage` · `hors-perimetre`.

Un statut **décrit l'état du dépôt**, jamais une intention. Le rendu visuel est
assuré par `StatusBadge` (`COMPONENTS.md` §5).

## 4. `release.ts`

| Export | Contenu |
| :-- | :-- |
| `RELEASE` | `published` (aucune release à ce jour), version `planned` (V0.1), URL releases/changelog. |
| `PROJECT_STATE` | Date d'observation, branche/commit, fichiers, lignes de code, tests, couverture, CI, Windows. |
| `VERSION_TABLE` | Tableau des versions par composant (page Versions). |
| `RELEASE_EN`, `PROJECT_STATE_EN`, `VERSION_TABLE_EN` | Miroirs anglais **des seules chaînes localisées** ; les valeurs numériques restent dans les objets français. |

**Invariant** : `RELEASE.published` reste `false` tant qu'aucun tag public
n'existe. La page Télécharger demeure neutre en conséquence.

### 4.1 `versions.ts`

Historique des **versions publiées**, distinct de `VERSION_TABLE` (qui décrit
les versions *courantes par composant*). Tableau `VERSIONS: VersionEntry[]`,
**vide à ce jour** (aucune release). Chaque entrée génère une fiche
`versions/<slug>/` (FR) et `en/versions/<slug>/` (EN) : résumé, nouveautés,
corrections, limites connues, table de téléchargements (taille, empreinte
SHA-256). Champs FR et `*En` pour le miroir anglais. Voir `MAINTENANCE.md` pour
l'ajout d'une fiche.

## 5. `claims.ts` — registre des affirmations

Chaque affirmation chiffrée ou sensible publiée sur le site est **enregistrée**
avec sa source interne et sa date de vérification.

```ts
interface Claim {
  id: string;
  statement: string;     // formulation autorisée
  status: 'vérifiée' | 'prudente';
  source: string;        // document interne + section
  date: string;          // date de vérification (AAAA-MM-JJ)
}
```

Affirmations enregistrées à ce jour : `tests`, `coverage`, `ci`, `release`,
`prism-scope` (voir le fichier). Toute nouvelle affirmation publiée doit être
ajoutée ici **avant** sa mise en ligne.

## 6. Collection « actualites » (journal)

- Schéma : `site/src/content.config.ts`, collection `actualites`, loader `glob`
  sur `site/src/content/actualites/**/*.md`.
- Champs : `title`, `description`, `date` (coercition date), `draft` (défaut
  `false`), `tags` (défaut `[]`).
- Les brouillons (`draft: true`) sont exclus de l'index et du flux.
- Index : `actualites/index.astro` (état vide assumé tant qu'aucun article) ;
  chaque titre renvoie vers son article.
- Article : `actualites/[...slug].astro` — `getStaticPaths` sur la même
  collection (hors brouillons), corps rendu via `render()` (`astro:content`).
- Flux : `actualites/feed.xml.ts` (`@astrojs/rss`), `language: fr`, articles
  non brouillons triés par date décroissante.
- Les articles ne sont **pas** traduits en anglais (niveau 3, `I18N.md` §4).

> Le build avertit `[glob-loader] No files found matching "**/*.md"` quand la
> collection est vide : avertissement **bénin**, attendu.

## 7. Règles éditoriales

1. **Source et date obligatoires** pour tout chiffre (registre §5).
2. **Pas de capture inventée** : un emplacement `.ph` reste un cadre pointillé
   tant qu'aucune capture réelle n'existe.
3. **Pas de promesse** : les statuts et la page Télécharger décrivent le
   dépôt, pas une feuille de route commerciale.
4. **Double licence explicite** : code sous GNU GPL v3.0 ou ultérieure, contenu CC BY 4.0 (voir aussi
   `footer` et `licence/`).
5. **Ton** : sobre, factuel, sans superlatif ; « illustration conceptuelle »
   clairement étiquetée.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Documenter les données éditoriales et le registre d'affirmations. |
| 14 octobre 2026 | Ajout de `data/versions.ts`, de `children` dans `NavItem`, et de la route d'article `actualites/[...slug].astro`. | Alignement sur le cahier des charges (S14, S18). |
