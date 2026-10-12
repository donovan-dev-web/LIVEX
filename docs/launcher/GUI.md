# GUI.md — Spécification de l'interface du Launcher

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Version cible** : 0.1.0
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `USER_INTERFACE.md`, `VISION.md`, `COMPONENTS.md`, `ARCHITECTURE.md`
**Source Monographie** : —

---

## 1. Objet et hiérarchie des documents

| Document | Portée | Question à laquelle il répond |
| :-- | :-- | :-- |
| `VISION.md` | Produit | Pourquoi l'interface existe |
| `USER_INTERFACE.md` | Fonctionnel | Ce que l'interface permet, quelles règles elle respecte |
| **`GUI.md`** | **Pixel** | **À quoi elle ressemble, à quelle position exacte** |

`GUI.md` **ne contredit pas** `USER_INTERFACE.md` : il le précise. Quand ce document
donne une dimension, `USER_INTERFACE.md` reste la référence pour l'intention.

### 1.1 Source de vérité

| Source | Rôle | Autorité |
| :-- | :-- | :-- |
| `maquettes/Interface futuriste du launcher LIVEX.png` | Image de référence | **Absolue** pour tout ce qui est visible |
| `maquettes/livex-launcher.html` | Prototype interactif | Autoritaire pour le **comportement**, indicatif pour le rendu |

La canvas de référence est **1536 × 1024 px**.

> **Écart connu.** Un rendu navigateur du prototype à 1536 × 1024 ne reproduit pas
> le PNG : le PNG est plus sombre, plus contrasté, texturé, et son hero mesure
> 205 px contre 185 px au CSS. **Le PNG fait foi, pas le CSS.** Les valeurs de ce
> document sont celles du PNG. Voir §12.

## 2. Grille globale

### 2.1 Structure

```text
1536 × 1024
┌────────────────────────────────────────────────────────────┐ y=0
│  TOPBAR                                        85 px       │
├────────────┬──────────────────────────┬────────────────────┤ y=85
│            │                          │                    │
│  SIDEBAR   │  MAIN                    │  COLONNE DROITE    │
│  224 px    │  924 px                  │  322 px            │
│            │                          │                    │
├────────────┴──────────────────────────┴────────────────────┤ y=976
│  FOOTER                                         48 px       │
└────────────────────────────────────────────────────────────┘ y=1024
```

Le corps est une grille à **trois colonnes**, séparées par un fond de page
visible — pas un trait.

| Axe | Valeur |
| :-- | :-- |
| Colonnes | 222 px / 1fr / 321 px |
| Gouttière | 17 px |
| Padding gauche | 16 px |
| Padding droit | 19 px |
| Padding bas | 14 px |
| Rangées | 85 px / 1fr / 48 px |

**Vérification arithmétique** : `16 + 222 + 17 + 924 + 17 + 321 + 19 = 1536`.

### 2.2 Bornes mesurées

| Zone | x | y | w | h |
| :-- | :-- | :-- | :-- | :-- |
| Topbar | 0 | 0 | 1536 | 85 |
| Sidebar | 16 | 85 | 222 | 877 |
| Main | 255 | 85 | 924 | 877 |
| Colonne droite | 1196 | 85 | 321 | 877 |
| Footer | 0 | 976 | 1536 | 48 |

Le main **déborde volontairement vers le haut** : le hero remonte de 22 px dans la
topbar (`margin-top: -22px`) et occupe y=63 à y=268.

## 3. Jetons

### 3.1 Couleurs

Le fond est un **dégradé radial**, pas une couleur plate : ce grain sombre est
signature de l'image.

| Rôle | Valeur | Usage |
| :-- | :-- | :-- |
| Fond de page, bord | `#07090d` | Fond le plus éloigné |
| Fond de page, pic | `#141821` | Centre du dégradé, en haut |
| Fond de page, mesuré médian | `#071017` | Valeur de référence de la canvas |
| Panneau | `#0d1016` | Fond des cartes de section |
| Panneau, bas de dégradé | `#0a0d12` | Bas des panneaux |
| Bordure | `#262b34` | Contours 1 px |
| Bordure interne | `#1a1e26` | Séparateurs de tableau |
| Bordure interne, logs | `#1c2028` | Séparateurs de journal |
| Or | `#e0a25a` | Accent principal, `.hero`, titres d'état |
| Or, dégradé | `#c98a3f` | Bouton primaire, dégradé bas |
| Texte | `#e8e6e1` | Corps de texte |
| Texte atténué | `#9aa0aa` | Libellés secondaires |
| Vert | `#2ecc71` | État démarré, « Système prêt » |
| Bleu | `#2d9bf0` | État en cours, entrées de journal |
| Bleu, barre | `#2d8fe6` → `#5cc4ff` | Dégradé des jauges |
| Violet | `#a04cf0` | Composant ECHOS |
| Cyan | `#1fa8e8` | Composant SYNE |
| Orange | `#f0902d` | Composant PRISM |

**Méthode de mesure.** Les couleurs de texte ci-dessus sont issues du prototype,
les kaléidoscopes de mesure étant bruités par le grain. Les valeurs du PNG sont
systématiquement **plus sombres et plus saturées** que celles du CSS. En cas de
divergence, **le PNG gagne**.

### 3.2 Typographie

| Usage | Police | Taille | Graisse | Interlettrage |
| :-- | :-- | :-- | :-- | :-- |
| Marque principale | Montserrat | 29 px | 500 | 0.34 em |
| Sous-marque | Montserrat | 11 px | 500 | 0.42 em |
| Surtitre | Montserrat | 10 px | 500 | 0.28 em |
| Titre de section | Inter | 15 px | 400 | — |
| Titre de composant | Montserrat | 17 px | 600 | 0.14 em |
| Corps | Inter | 12.5 px | 400 | — |
| Corps atténué | Inter | 12 px | 400 | — |
| Petit | Inter | 11.5 px | 400 | — |
| Navigation | Inter | 14.5 px | 400 | — |

Les interletrages larges des mentions en capitales sont **intentionnels** : c'est
la signature typographique de l'image. Ne pas les « corriger ».

### 3.3 Rayons, bordures, espacements

| Élément | Rayon | Bordure |
| :-- | :-- | :-- |
| Grand panneau | 12 px | 1 px `#262b34` |
| Hero | 0 en haut-gauche, 12 en bas-droite | — |
| Carte de composant | 6 px | 1.5 px, couleur du composant |
| Bouton | 6 px | 1 px `#3a3f4a` |
| Sélecteur de mode | 8 px | 1 px `#262b34` |
| Pastille d'état | 5 à 14 px | 1 px, teinte à 25 % |
| Tableau | 8 px | 1 px `#262b34` |
| Jauge | 3 px | — |

Espacements de référence : gouttière de grille **17 px**, entre blocs du main
**15 px**, marge interne de panneau **16 px**.

## 4. Topbar

Hauteur **85 px**, fond transparent sur le dégradé de page, filet inférieur en
dégradé (transparent → `#3a3126` à 30 % → `#1a1d24` à 70 % → transparent).

```text
x=34   ┌──┐  LIVEX            │  UN MONDE VIRTUEL    Mode  ┌─────────┐ ⚙ │ — □ ✕
       │▲ │  LAUNCHER         │  EN EVOLUTION             │Standard │   │
       └──┘                                                      └─────────┘
```

| Élément | x | y | Taille |
| :-- | :-- | :-- | :-- |
| Logo (triangle) | 34 | 14 | 62 × 56 |
| « LIVEX » | 121 | 22 | 139 × 26 |
| « LAUNCHER » | 119 | 54 | 108 × 14 |
| Séparateur vertical | ~279 | — | 1 px |
| « UN MONDE VIRTUEL » | 320 | 29 | 132 × 10 |
| « EN EVOLUTION » | 319 | 43 | 104 × 12 |
| « Mode » | 1148 | 28 | 35 × 14 |
| Sélecteur | ~1205 | 25 | 134 × 34 |
| Boutons d'état | ~1420 | 25 | 36 × 36 |

Le cluster de droite est ancré à **x = 1517** (padding droit 19 px). Il contient,
dans cet ordre : libellé « Mode », sélecteur, bouton paramètres, filet, réduire,
agrandir, fermer.

## 5. Sidebar

x=16, y=85, **222 × 877**. Fond en dégradé vertical `#0d1016` → `#0a0c11`, rayon
12 px, bordure 1 px.

### 5.1 Navigation

Padding haut **26 px**. Chaque entrée : hauteur **51 px**, marge basse 4 px,
padding gauche 20 px, gap 22 px entre icône (26 × 26) et libellé.

| Entrée | y du libellé | État |
| :-- | :-- | :-- |
| Accueil | 128 | **Sélectionné** |
| Expériences | 184 | — |
| Campagnes | 240 | — |
| Analyse | 298 | — |
| Rapports | 353 | — |
| Configuration | 413 | — |
| Logs | 469 | — |
| Monitoring | — | Ajout V1, sans coordonnées dans la maquette historique |
| Documentation | — | Ajout V1, sans coordonnées dans la maquette historique |

L'entrée sélectionnée se distingue par **trois** signes cumulés, jamais par la
couleur seule :

| Signe | Valeur |
| :-- | :-- |
| Fond | Dégradé horizontal `#4a3822` → `#2a2119` à 80 % |
| Filet gauche | 3 px, or `#e0a25a`, sur toute la hauteur |
| Texte | Blanc pur |
| Marge | Rétractée de 2 px à gauche, 8 px à droite, rayon 4 px |

### 5.2 Pied de sidebar

Ancré en bas, padding `0 20px 20px`. Petit triangle 50 × 48, « LIVEX » en
Montserrat 15 px interlettré 0.35 em, puis trois lignes en 11.5 px :

- « Des entités autonomes. » — y=900
- « Des comportements émergents. » — y=918
- « Un monde qui évolue. » — y=936

## 6. Hero

**y=63 à y=268 (205 px)**, x=255 à x=1179. **Remonte de 22 px dans la topbar** et
n'est arrondi qu'en bas à droite (12 px). C'est le seul élément qui casse la
grille verticale, et c'est voulu.

Fond : deux dégradés superposés — une voile horizontale opaque à gauche
(`#0a0c11`, 4 % d'opacité), puis un dégradé vertical chaud
`#5a4a3a` → `#3b3a48` (30 %) → `#1d2330` (70 %) → `#0c0f16`. Une silhouette de
montagne vectorielle occupe les 70 % de droite.

| Élément | Position |
| :-- | :-- |
| Filet or vertical | x=279, 3 px de large, y=119 à y=201 |
| « LIVEX » | x=319, y=125, 194 × 36 |
| « LAUNCHER » | x=320, y=173, 143 × 15 |
| Phrase 1 | x=284, y=218, 215 × 16 |
| Phrase 2 | x=284, y=237, 164 × 15 |

Le bloc de titre est ancré à `left: 28px`, `top: 56px` par rapport au hero. Les
deux phrases sont ancrées en bas, à 12 px du bord, avec une ombre portée pour
rester lisibles sur le dégradé.

## 7. Section « COMPOSANTS »

Panneau x=255 à x=1179, y=282 à y=623. En-tête à y=299.

| Élément | Position |
| :-- | :-- |
| Titre « COMPOSANTS » | x=311, y=299 |
| Sous-titre | x=310, y=316 |
| Icône d'en-tête | 26 × 26, or, avant le titre |

### 7.1 Cartes

Trois colonnes égales, gouttière **19 px**, padding `12px 14px 14px`.

| Carte | x | Largeur | Couleur |
| :-- | :-- | :-- | :-- |
| SYNE | 268 | 292 | Cyan `#1fa8e8` |
| ECHOS | 580 | 288 | Violet `#a04cf0` |
| PRISM | 887 | 280 | Orange `#f0902d` |

La maquette et l'application affichent trois cartes de rôle. `syne-mock` est
détecté et configuré comme le mode « Émulé » de la carte SYNE, jamais comme une
quatrième carte ; la ligne SYNE de l'état système représente l'instance réelle
ou émulée, et ces variantes sont exclusives.

Chaque carte : rayon 6 px, bordure **1.5 px** à sa couleur, ombre portée colorée
diffusée, dégradé d'angle en la couleur du composant, plus un halo radial en haut
à droite. Le halo est décoratif — il ne porte aucune information.

Structure interne, mesurée sur SYNE :

| Élément | y | Détail |
| :-- | :-- | :-- |
| Icône circulaire | 363 | 66 × 66, bordure 1.5 px à la couleur |
| Nom du composant | 378 | Montserrat 600, 17 px |
| Sous-titre | 404 | 12.5 px, peut occuper deux lignes |
| Technologie | 422 | 11.5 px, atténué |
| Description | 471 | 12.5 px, deux lignes, `margin-top: 22px` |
| Pastille d'état | 527 | y=527 à y=553, hauteur 26 px |
| Boutons | 577 | y=577 à y=615, hauteur 38 px |

Les largeurs de carte (292/288/280) sont **égales en intention** ; l'écart
mesuré vient de la gouttière et des bordures. Les columns sont un `1fr` triple.

**Pastille d'état** — forme capsule, rayon 14 px, hauteur 26 px, pastille
intérieure 9 px :

| État | Fond | Texte | Bordure |
| :-- | :-- | :-- | :-- |
| Démarré | `rgba(30,60,45,.6)` | Vert `#2ecc71` | Vert à 25 % |
| Arrêté | Gris translucide | Gris `#9da6b0` | Gris à 25 % |

**Boutons** — deux boutons égaux, gap 14 px, hauteur 38 px, fond
`rgba(18,21,29,.85)`, bordure `#3a3f4a`. L'icône (15 × 15) est à gauche du
libellé, gap 9 px. Le libellé bascule entre « Arrêter » et « Démarrer » selon
l'état, et l'icône change avec lui.

## 8. Section « Dernières expériences »

Panneau x=255 à x=1179, y=634 à y=962. L'en-tête est plus haut que celui de la
section précédente (padding haut 14 px).

| Élément | Position |
| :-- | :-- |
| Titre | x=316, y=654 |
| Lien « Voir toutes les expériences » | x=1004, y=660, aligné à droite |
| Tableau | x=270 à x=1161, y=698 à y=898 |
| Bouton « Nouvelle expérience » | x=321, y=924 |
| Lien « Gérer les expériences » | x=1033, y=926 |

### 8.1 Tableau

Fond `#0b0e13`, rayon 8 px, bordure 1 px. Largeur = 100 % − 32 px, marges
latérales 16 px. **Hauteur de ligne 33 px**, en-tête de 10 px.

| Colonne | x du libellé | Largeur |
| :-- | :-- | :-- |
| Nom | 288 | 225 px |
| Date | 514 | 193 px |
| Durée | 707 | 152 px |
| Statut | 859 | 200 px |
| Actions | 1075 | — |

Le padding gauche de la première colonne est de 20 px ; la dernière colonne est
alignée à droite. Le nom porte une icône de 20 × 20, gap 16 px.

Cinq lignes dans le PNG de référence :

| Nom | Date | Durée | Statut |
| :-- | :-- | :-- | :-- |
| Test Emergence 03 | 27 sept. 2026 14:32 | 10 000 ticks | Terminée |
| Campagne Biodiversite | 26 sept. 2026 22:17 | 5 runs | Terminée |
| Exploration Comportements | 25 sept. 2026 16:03 | 8 000 ticks | **En cours** |
| Test Performance | 24 sept. 2026 11:48 | 3 000 ticks | Terminée |
| Campagne Longue Duree | 22 sept. 2026 09:12 | 15 runs | Terminée |

**Pastille de statut** — largeur fixe 96 px, hauteur 24 px, rayon 5 px, pastille
9 px à gauche, gap 12 px, padding `3px 12px` :

| État | Fond | Texte |
| :-- | :-- | :-- |
| Terminée | `rgba(46,204,113,.13)` | Vert |
| En cours | `rgba(45,155,240,.15)` | Bleu |

Les actions sont deux icônes de 18 × 18, gap 18 px, à droite de la ligne.

## 9. Colonne droite

x=1196, largeur 321. Trois panneaux empilés, gouttière 14 px.

| Panneau | y | Hauteur |
| :-- | :-- | :-- |
| État du système | 85 à 342 | 257 |
| Ressources | 357 à 553 | 196 |
| Logs récents | 568 à 960 | 392 |

Le troisième panneau occupe tout l'espace restant : c'est le seul des trois
élasticité verticale.

### 9.1 État du système

Titre à x=1253, y=99, précédé d'une icône 32 × 26 en or. Séparateur 1 px sous le
titre, puis **trois lignes de 69 px**.

Chaque ligne : icône circulaire 48 × 48, nom en Inter 500 14.5 px, état en 12 px
avec pastille 9 px, et à droite le PID en 11.5 px atténué, puis une cheville.

| Ligne | Nom | État | PID | y du libellé |
| :-- | :-- | :-- | :-- | :-- |
| 1 | SYNE | Démarré | 4821 | 146 |
| 2 | ECHOS | Démarré | 4937 | 214 |
| 3 | PRISM | Arrêté | — | 282 |

### 9.2 Ressources

Titre à x=1213, y=370. Trois lignes, chacune : icône 26 × 26, libellé 11.5 px,
jauge, valeur alignée à droite sur 36 px.

| Ressource | y du libellé | y de la jauge | Valeur |
| :-- | :-- | :-- | :-- |
| Mémoire (RAM) | 406 | 428 | 42 % |
| CPU | 453 | 465 | 28 % |
| Stockage | 501 | 514 | 31 % |

La jauge est **plate** : 6 px de haut, rayon 3 px, fond `#2a2e37`, remplissage en
dégradé `#2d8fe6` → `#5cc4ff`, transition de largeur 0.8 s. Elle occupe la
largeur disponible entre le libellé et la valeur.

### 9.3 Logs récents

Titre à x=1252, y=587, précédé d'une icône 26 × 26 en or. Entrées séparées par un
filet 1 px `#1c2028`, padding `11px 0 10px`, texte 11.5 px / 1.35.

Chaque entrée : pastille 9 px (bleue par défaut), niveau sur 44 px, message, puis
un horodatage en 11 px atténué sur une ligne dédiée.

| Niveau | Couleur | Séparateur suivant |
| :-- | :-- | :-- |
| INFO | Bleu `#2d9bf0` | — |
| WARN | Ambre | — |

Cinq entrées dans le PNG de référence :

| Niveau | Message | Horodatage |
| :-- | :-- | :-- |
| INFO | SYNE démarré (PID 4821) | 27 sept. 2026 14:32:17 |
| INFO | ECHOS démarré (PID 4937) | 27 sept. 2026 14:32:21 |
| INFO | PRISM arrêté | 27 sept. 2026 12:17:03 |
| WARN | Tentative de démarrage de PRISM ignorée (mode Standard) | 27 sept. 2026 12:16:59 |
| INFO | Campagne « Test Emergence 03 » terminée | 27 sept. 2026 11:48:32 |

Le pied du panneau porte le lien « Voir tous les logs → » à x=1213, y=925, au
dessus d'un filet 1 px. L'entrée la plus ancienne est tronquée, pas masquée.

## 10. Footer

y=976 à y=1024, **48 px**. Fond en dégradé horizontal
`#0b0d12` → `#151109` à 50 % → `#0b0d12`, filet supérieur 1 px `#2b2419`.
Padding horizontal 32 px.

```text
x=30  LIVEX Launcher │ v0.3.0 │ Mode:Standard        ● Système prêt
                                                        x=1445
```

| Élément | x | y | Détail |
| :-- | :-- | :-- | :-- |
| « LIVEX Launcher » | 30 | 991 | 11.5 px |
| Séparateur | ~121 | — | 1 px, 16 px de haut |
| « v0.3.0 » | 131 | 990 | Atténué |
| Séparateur | ~172 | — | 1 px |
| « Mode:Standard » | 190 | 991 | — |
| « Système prêt » | 1445 | 991 | Ancré à droite, pastille verte 11 px avec halo |

La pastille « prêt » porte une ombre portée verte — c'est le **seul** élément
lumineux de la footer, et il doit attirer l'œil sur la disponibilité.

## 11. États et interactions

| État | Déclencheur | Rendu attendu |
| :-- | :-- | :-- |
| Composant démarré | Cycle de vie | Pastille verte, bouton « Arrêter », PID affiché |
| Composant arrêté | Cycle de vie | Pastille grise, bouton « Démarrer », PID masqué |
| Démarrage refusé | Mode ne permettant pas PRISM | Entrée WARN expliquant la cause |
| Navigation | Clic sur une entrée | Une seule entrée sélectionnée, filet or à gauche |
| Jauges | Échantillonnage périodique | Largeur animée sur 0.8 s, valeur textuelle mise à jour |
| Expérience en cours | Campagne active | Pastille bleue « En cours », libellé non ambigu |
| Système indisponible | Perte d'un composant | Pastille verte du footer passe au rouge, cause affichée |

**Règle transverse.** Aucun changement d'état ne se signale par la couleur
seule : chaque pastille porte son libellé. Voir `USER_INTERFACE.md` §8.

## 12. Écarts entre le PNG et le prototype HTML

Mesurés en rendant le prototype à 1536 × 1024 avec Chrome headless et en
comparant pixel à pixel.

| Écart | PNG | Prototype | Traitement |
| :-- | :-- | :-- | :-- |
| Hauteur du hero | 205 px | 185 px | **PNG** |
| Fond médian de la canvas | `#071017` | `#0d0f15` | **PNG**, plus sombre |
| Texture | Grain présent | Absente | **PNG** |
| Dégradé du footer | Ambre marqué | Presque neutre | **PNG** |

Le prototype est donc **fidèle sur la structure** — les trois colonnes, les
gouttières, les 85/48 px et l'ensemble des hauteurs de panneaux tombent juste —
mais **faux sur le rendu**. C'est la structure qu'on lui vole, pas ses pixels.

## 13. Reproduction : ordre de priorité

1. **Grille et gouttières** — rien ne se tient droit avant.
2. **Bornes des zones** — topbar 85, footer 48, corps 877.
3. **Débordement du hero** — 22 px vers le haut, arrondi asymétrique.
4. **Palette** — le fond en dégradé radial avant tout le reste.
5. **Interlettrages** — la signature Montserratcapitales.
6. **Pastilles et jauges** — l'information d'état, jamais la décoration.
7. **Grain** — en dernier, et seulement s'il ne dégrade aucune lisibilité.

## 14. Points ouverts

- **Grain de fond.** Le PNG porte un grain dont l'origine n'est pas établie
  (rendu, compression, ou ajout à la production). Il faut décider s'il fait
  partie de l'identité visuelle ou s'il n'est qu'un artefact de l'image.
- **Dégradé du footer.** Sa teinteambre est nettement plus marquée dans le PNG
  que dans le prototype. La valeur cible n'est pas tranchée.
- **Autres écrans.** Une seule vue est spécifiée ici, parce qu'une seule est
  documentée. Les écrans V1 sont plus nombreux que la maquette de référence ;
  les deux entrées ajoutées, Monitoring et Documentation, n'ont pas encore de
  coordonnées pixel spécifiées.
- **Redimensionnement.** Le PNG ne fixe qu'une résolution. Les largeurs fixes
  (222, 321, 134) supposent un seul gabarit ; le comportement en dessous de
  1280 px n'est pas spécifié.
- **Pointeur de synchronisation.** Rien ne garantit que les valeurs mesurées au
  pixel restent exactes après le premier ajustement de design.

## 15. Références

- `USER_INTERFACE.md` — intention fonctionnelle, règles, accessibilité
- `VISION.md` — finalité et non-objectifs
- `COMPONENTS.md` — états et causes des composants
- `OBSERVABILITY.md` — niveaux de journalisation, L0 à L5
- `EXPERIMENTS.md` — campagnes et progression
- `PACKAGING.md` — premier démarrage, fenêtre native
- `maquettes/Interface futuriste du launcher LIVEX.png` — source de vérité
- `maquettes/livex-launcher.html` — prototype de comportement