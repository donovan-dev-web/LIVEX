# USER_INTERFACE.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `VISION.md`, `COMPONENTS.md`, `ARCHITECTURE.md`, `GUI.md`
**Source Monographie** : —

---

## 0. Portée de ce document

Ce document décrit **ce que l'interface permet et quelles règles elle respecte**.
Il ne donne aucune dimension. Les positions, les tailles, les couleurs et la
typographie sont spécifiés dans **`GUI.md`**.

| Document | Question |
| :-- | :-- |
| `USER_INTERFACE.md` | Que fait l'interface, et sous quelles contraintes |
| `GUI.md` | À quoi ressemble-t-elle, au pixel près |

La maquette de référence est `maquettes/Interface futuriste du launcher LIVEX.png`,
en 1536 × 1024. Quand ce document et cette image semblent en désaccord, c'est
**l'image qui fait foi** pour tout ce qui est visible.

---

## 1. Intentions de l'interface

L'interface du Launcher est une interface d'**orchestration**. Elle doit permettre
de comprendre l'état de la pile et d'agir dessus, et rien de plus.

| Elle doit | Elle ne doit pas |
| :-- | :-- |
| Montrer ce qui tourne, ce qui manque, ce qui est cassé | Montrer des résultats scientifiques |
| Permettre de démarrer, arrêter, suspendre | Remplacer l'interface d'ECHOS |
| Permettre de créer et suivre une campagne | Analyser une campagne |
| Donner accès aux journaux et aux diagnostics | Servir d'explorateur de fichiers |
| Rester lisible en un coup d'œil | Recomposer la pile à chaque session |

## 2. Organisation générale

### 2.1 Ce que montre la maquette

La maquette de référence ne présente pas une navigation à trois modes, mais une
navigation à **sept entrées**, dans une sidebar de 222 px :

```text
┌───────────────┬────────────────────────────────────────────────┐
│  LIVEX        │  UN MONDE VIRTUEL          Mode  [Standard]    │
│               ├───────────────┬──────────────────────────────┤
│  ▌Accueil     │               │                              │
│   Expériences │   Hero        │   État du système            │
│   Campagnes   │               │   ────────────────           │
│   Analyse     │  COMPOSANTS   │   SYNE    Démarré  PID 4821   │
│   Rapports    │  ┌────┬────┬──┤   ECHOS   Démarré  PID 4937   │
│   Configuration│ │SYNE│ECHOS│PR│   PRISM   Arrêté              │
│   Logs        │  └────┴────┴──┤   ────────────────           │
│               │  Expériences  │   Ressources                 │
│               │  ┌──────────┐│   RAM ▓▓▓░░ 42 %             │
│               │  │ tableau  ││   CPU ▓▓░░░ 28 %             │
│               │  └──────────┘│   ────────────────           │
│  ▌LIVEX       │               │   Logs récents               │
│  Des entités… │               │   INFO  SYNE démarré        │
└───────────────┴───────────────┴──────────────────────────────┘
   LIVEX Launcher │ v0.3.0 │ Mode:Standard      ● Système prêt
```

Trois zones, toujours visibles : **sidebar** à gauche, **contenu** au centre,
**colonnes d'état** à droite. La disposition exacte est dans `GUI.md` §2.

### 2.2 Deux organizing concepts, un seul écran

La maquette fait apparaître une tension réelle, qu'il ne faut pas passer sous
silence : ce document raisonne en **trois modes**, la maquette en **sept entrées**.

| Les trois modes de ce document | Les sept entrées de la maquette |
| :-- | :-- |
| Contrôle | Accueil, Expériences, Configuration |
| Analyse | Campagnes, Analyse, Rapports |
| Immersion | PRISM dans l'état de la pile, pas une entrée |

Les deux ne décrivent pas le même niveau. Les **modes** sont un découpage
conceptuel du produit, largement repris dans `VISION.md` et
`adr/ADR-002-modes-analyse-et-immersion-de-poids-egal.md`. Les **entrées de la
sidebar** sont un découpage écran.

Correspondance proposée :

| Entrée | Contenu | Mode conceptual |
| :-- | :-- | :-- |
| Accueil | Synthèse de la pile | Transverse |
| Expériences | Liste des runs | Contrôle |
| Campagnes | Liste des campagnes | Analyse |
| Analyse | Lecture des rapports | Analyse |
| Rapports | Rapports exportés | Analyse |
| Configuration | Profil, moteur, session | Contrôle |
| Logs | Journaux de session | Contrôle |

**Décision à trancher.** Soit la navigation à sept entrées est retenue et les
modes restent un concept de documentation, soit la navigation à trois modes est
retenue et la maquette est à refaire. Le statu quo — deux découpages concurrents
non réconciliés — n'est pas tenable. Voir §12.

### 2.3 Zones et persistance

| Zone | Rôle | Persistance |
| :-- | :-- | :-- |
| **Barre de titre** | Identité, mode, état global | Non |
| **Sidebar** | Navigation, identité en pied | Non |
| **Contenu** | Vue sélectionnée | Non |
| **Colonne d'état** | État du système, ressources, journaux | Non |

La colonne d'état est **toujours visible**. L'état de la pile ne doit jamais
nécessiter une navigation pour être connu.

La maquette ne comporte pas de barre de composants ni de barre de campagne
dédiées comme dans le schéma initial : ces informations sont **portées par la
colonne d'état** et par la section COMPOSANTS du contenu. C'est une équivalence
de fait, à confirmer.

## 3. Les trois modes

### 3.1 Mode Contrôle

Mode **socle**, disponible dès que SYNE est présent. Il ne dépend d'aucun autre
composant et reste accessible même si les deux autres modes sont dégradés.

| Élément | Rôle |
| :-- | :-- |
| **Pile** | Composants installés, versions, états, actions de cycle de vie |
| **Profil** | Sélection des modes, validation des capacités, profils enregistrés |
| **Configuration** | Configuration du moteur, paramètres de session |
| **Diagnostics** | Journaux de session, mesures d'orchestration, points d'accès |

C'est la vue par défaut au démarrage, et la seule qui reste complète quand un
mode scientifique est indisponible.

### 3.2 Mode Analyse

Mode porté par ECHOS, qui **calcule**. Le Launcher y **présente** ce qu'ECHOS a
produit, sans rien recalculer.

| Élément | Rôle |
| :-- | :-- |
| **Campagnes** | Liste des campagnes, création, progression, actions de cycle de vie |
| **Paquet** | Ouverture d'un `.livexp`, état, scellement, reprise |
| **Rapport** | **Visualisation du rapport d'émergence** rendu depuis son Markdown, navigation par section |
| **Documentation** | Consultation de la documentation embarquée, rendue par le **même lecteur Markdown** |
| **Télémétrie** | Ouverture de l'interface d'ECHOS dans le navigateur, facultative |

#### Le lecteur Markdown

La visualisation du rapport d'émergence dans le Launcher est une fonctionnalité de
premier plan. L'utilisateur lit son résultat **dans l'outil qui a lancé la
campagne**, sans changer d'application.

| Règle | Comportement |
| :-- | :-- |
| **Technologie unique** | Un seul composant de rendu Markdown, un seul rendu visuel : il sert le **rapport d'émergence** et la **documentation embarquée**. Deux technologies de rendu pour deux contenus serait une dérive de conception. |
| **Source unique** | Le lecteur affiche `analysis/emergence_report.md`, écrit par ECHOS. |
| **Fidélité** | L'affichage rend le contenu produit par ECHOS. Toute présentation ajoutée doit être signalée comme telle. |
| **Navigation** | Le rapport est découpé selon ses sections : Expérience, Configuration, Simulation, Runs, Analyse individuelle, Analyse agrégée, Motifs émergents, Synthèse statistique, Variance entre runs, Anomalies, Conclusion, Reproductibilité. |
| **Provenance** | Chaque section affiche son horodatage et la version d'ECHOS qui l'a produite. |
| **Absence** | Sans ECHOS, aucun rapport n'est produit. L'absence est affichée, jamais remplacée par une approximation locale. |
| **Documentation** | La documentation embarquée est rendue par le même lecteur, sans interprétation du contenu ; elle ne remplace jamais un rapport absent. |

Le choix de la bibliothèque de rendu Markdown reste à trancher à l'implémentation
(critères : fidélité du rendu, tableaux, ancrages de section, coût d'intégration
Avalonia) — le **contrat** ci-dessus, lui, est fixé.

La documentation ainsi consultable est celle **embarquée dans l'installation**
(dossier `docs/` de l'arborescence, `ARCHITECTURE.md` §10.2) : guides du paquet,
référence des profils, aide au diagnostic. Le lecteur ne modifie jamais les
fichiers qu'il affiche, rapport comme documentation.

La distinction tient en une règle : **le Launcher ne calcule rien de scientifique,
mais il sait afficher un résultat**. Voir `adr/ADR-003-analyse-propriete-de-echos.md`.

#### La télémétrie facultative

L'interface web d'ECHOS peut être ouverte à tout moment depuis le mode Analyse pour
observer l'analyse en direct pendant une simulation. C'est un **complément**, pas un
prérequis : aucune campagne ne dépend d'une fenêtre de navigateur, et le Launcher
reste complet si elle n'est jamais ouverte.

Ouvrir l'interface d'ECHOS est une **délégation** : le Launcher ouvre le
navigateur, ECHOS reste propriétaire de son rendu interactif.

### 3.3 Mode Immersion

Mode porté par PRISM, conçu sur le même modèle que le mode Analyse, avec ses propres
éléments.

| Élément | Rôle |
| :-- | :-- |
| **Scène** | Connexion à la scène PRISM, état du monde, commandes de vol |
| **Session** | Contrôle de la session PRISM, pause, reprise, capture |
| **Aperçu** | Vignette de la scène, hors de tout rendu temps réel dans le Launcher |

Le Launcher n'affiche pas de scène temps réel : il n'a pas vocation à être une
surface d'animation continue, comme le rappelle `VISION.md` §6. Il expose l'état
de la session et les commandes, et laisse PRISM rendre le monde.

### 3.4 Le mode verrouillé

L'entrée **Immersion** est visible, marquée verrouillée, et non sélectionnable. Un
clic explique la raison et le jalon attendu, au lieu d'être simplement inerte.

```text
┌──────────────────────────────────────────────┐
│  Mode Immersion                              │
│                                              │
│  PRISM n'est pas encore implémenté.           │
│  Ce mode sera disponible quand PRISM sera    │
│  livré.                                     │
│                                              │
│  Jalon attendu : G4 — voir ROADMAP.md        │
│                                              │
│           [ Compris ]                        │
└──────────────────────────────────────────────┘
```

Le verrouillage est un **état de conception**, pas une fonctionnalité manquante.
L'interface ne doit jamais laisser croire que le mode est abandonné.

## 4. Mise en page de la zone de contenu

La mise en page est identique pour toutes les vues, afin que l'utilisateur n'ait
rien à réapprendre : la sidebar et la colonne d'état ne changent jamais, seule la
zone centrale est remplacée.

Le gabarit commun, tel que la maquette le montre pour l'accueil :

```text
┌────────────────────────────────────────────────────────────┐
│  Bandeau — titre de la vue, actions à droite                │
├────────────────────────────────────────────────────────────┤
│                                                            │
│   Contenu principal                                        │
│   (liste, panneau, tableau, formulaire)                     │
│                                                            │
├────────────────────────────────────────────────────────────┤
│  Action primaire                    Actions contextuelles  │
└────────────────────────────────────────────────────────────┘
```

| Zone | Rôle | Règle |
| :-- | :-- | :-- |
| **En-tête de vue** | Titre et actions principales | Toujours présent |
| **Contenu** | Liste ou panneau principal | Occupe l'espace restant |
| **Panneau de détail** | Détail de l'élément sélectionné | Masquable |
| **Actions contextuelles** | Actions sur la sélection | Toujours cohérentes avec la sélection |

La sélection dans le contenu pilote le panneau de détail. Sans sélection, le
panneau propose un contenu d'attente, jamais un espace vide.

L'accueil de la maquette **empile** deux blocs dans cette zone : un panneau
COMPOSANTS avec ses trois cartes, puis un panneau des dernières expériences avec
son tableau. C'est le seul écran documenté ; les autres suivent le même gabarit
mais leur composition interne reste à écrire. Voir `GUI.md` §14.

## 5. Conception des vues

### 5.1 Liste de composants

| Colonne | Contenu | Tri |
| :-- | :-- | :-- |
| Composant | Nom et type | Par type |
| Version | Version déclarée | Non trié |
| État | Pastille et libellé | Par gravité |
| Cause | Cause principale | Non trié |
| Uptime | Durée depuis le démarrage | Non trié |
| Actions | Démarrer, arrêter, suspendre | — |

La cause est **toujours visible** pour un état autre qu'**Inactif**. Une ligne en
erreur sans cause lisible est un défaut de conception de la vue.

### 5.2 Liste de campagnes

| Colonne | Contenu |
| :-- | :-- |
| Campagne | Identifiant et titre |
| Paquet | État du paquet : vivant, scellé, récupérable |
| Progression | Runs terminés sur total |
| Durée | Durée écoulée, estimation |
| État | En cours, terminée, interrompue, échouée |

Les actions sont **contextuelles** au paquet : une campagne dont le paquet est scellé
n'offre ni reprise, ni réexécution.

### 5.3 Vue de progression

La vue de progression est la vue d'attente d'une campagne. Elle affiche de l'état,
jamais de résultat.

| Élément | Contenu |
| :-- | :-- |
| Progression globale | Runs terminés sur total, avec barre |
| Run en cours | Numéro, graine, horizon de ticks |
| Progression du run | Barre sur le tick courant |
| Durée | Écoulée, et estimation de fin |
| Compteurs | Terminés, échoués, restants |
| Journal de campagne | Événements récents, en lecture |

L'estimation de fin est présentée comme une **estimation**, jamais comme une
prédiction de résultat. Voir `EXPERIMENTS.md` §8.

## 6. Principes visuels

| Principe | Application |
| :-- | :-- |
| **L'état est visible en permanence** | La colonne d'état et la section COMPOSANTS sont toujours présentes |
| **Une couleur par état** | Vert, ambre, orange, rouge, gris, avec un libellé textuel |
| **La couleur ne suffit jamais** | Chaque état porte aussi un texte, pour l'accessibilité |
| **Densité d'outil** | Information dense, sans espace superflu |
| **Hiérarchie en trois niveaux** | En-tête de vue, contenu, détail |
| **Le décor ne porte jamais d'information** | Halos, ombres et dégradés sont ambiants, jamais significatifs |
| **Action destructive explicitée** | Suppression, annulation et réinitialisation sont distinguées |
| **Locus du verrouillage** | Un cadenas avec son motif, à l'entrée du mode |

L'interface ne comporte **aucun rendu temps réel**. Elle affiche des états et des
progressions échantillonnées, ce qui évite toute promesse de fluidité qu'elle ne
pourrait pas tenir.

### 6.1 Réconciliation avec la maquette

La maquette contredit deux formulations historiques de ce document. Les principes
sont maintenus, leur expression est révisée.

| Ancien principe | Position de la maquette | Décision |
| :-- | :-- | :-- |
| « Aucun effet décoratif — pas de dégradé » | Fond en dégradé radial, dégradés de panneaux, halos par carte, halo du hero | **Révisé** : les dégradés sont admis comme traitement de fond, jamais comme signal |
| « Pas d'animation permanente » | Jauges de ressources animées sur 0.8 s | **Confirmé** : l'animation existante est une transition de valeur, pas une animation permanente |

La distinction qui tient : **un dégradé ne dit rien**. Il donne de la profondeur
au fond et sépare deux plans. Ce qui dit quelque chose — vert, ambre, bleu,
« En cours », « Arrêté » — reste strictement réservé à l'état, et toujours
accompagné d'un libellé.

Aucun détail de ces traitements n'est normatif ici ; il est spécifié dans
`GUI.md` §3.

## 6.2 Typographie

La maquette utilise deux familles, jamais une seule :

| Famille | Rôle | Traitement |
| :-- | :-- | :-- |
| **Montserrat** | Marque, titres de composant, mentions capitales | Interlettrage large, jusqu'à 0.42 em |
| **Inter** | Tout le reste | Interlettrage normal |

Les mentions en capitales très espacées sont une **signature assumée** et ne
doivent pas être resserrées pour suivre une convention. Les tailles et
interlettrages sont dans `GUI.md` §3.2.

## 7. États vides et cas limites

| Situation | Rendu attendu |
| :-- | :-- |
| Aucun composant installé | Explication et emplacement de recherche, pas une liste vide |
| Composant absent | Ligne présente, état **Absent**, emplacement attendu |
| Aucun profil enregistré | Le profil par défaut est proposé, avec la liste des profils disponibles |
| Aucune campagne | Explication du mode, avec création en action principale |
| Paquet corrompu | Erreur nommant l'entrée fautive, avec ouverture en lecture partielle |
| Campagne en cours | Bandeau persistant indiquant qu'un paquet est vivant |
| Composant en incidence | Bandeau d'alerte avec cause et action, non bloquant pour les autres modes |

## 8. Accessibilité

| Exigence | Contenu |
| :-- | :-- |
| **Navigation au clavier** | Toutes les actions accessibles sans souris |
| **Contrastes** | Contrastes conformes pour les textes d'état et les libellés |
| **libellés explicites** | Aucun état n'est signalé par la couleur seule |
| **Lecture d'écran** | Chaque contrôle porte un rôle et un libellé |
| **Taille de texte** | Interface utilisable jusqu'à 200 % de zoom |
| **Mouvement** | Aucune animation indispensable à la compréhension |
| **Focus visible** | Indicateur de focus permanent sur le contrôle actif |
| **Ordre de tabulation** | Suit l'ordre visuel, sans piège de focus |

## 9. Ouverture de l'interface d'ECHOS

| Règle | Comportement |
| :-- | :-- |
| **Navigateur externe** | Ouverture dans le navigateur par défaut de l'utilisateur |
| **Aucune vue embarquée** | Pas de webview, pas de rendu interne du contenu d'ECHOS |
| **Aucun secret en URL** | Aucun jeton d'accès dans la ligne d'adresse |
| **Contexte transmis** | Identifiant de campagne, de paquet et de port, par les seules voies prévues par ECHOS |
| **Perte de focus** | L'utilisateur peut continuer à utiliser le Launcher pendant que le navigateur est ouvert |
| **Échec d'ouverture** | Si aucun navigateur n'est disponible, l'adresse est proposée à la copie |

Le Launcher ne **réplique** aucun élément de l'interface d'ECHOS. Toute fonction
d'analyse visible dans le Launcher serait une violation de la frontière.

## 10. Localisation

| Élément | V0.1 |
| :-- | :-- |
| **Langue de l'interface** | Français, seule langue |
| **Format de date et d'heure** | Localisé, avec fuseau affiché |
| **Format numérique** | Localisé, avec unité explicite |
| **Texte des composants** | Non traduit par le Launcher |
| **Documentation** | Français |

Aucune infrastructure de traduction n'est prévue en V0.1. Les chaînes sont
centralisées pour qu'une deuxième langue soit ajoutable sans refonte.

## 11. Références

- `GUI.md` — **spécification pixel** : grille, positions, couleurs, typographie
- `VISION.md` — intentions, non-objectifs, égalité des modes
- `COMPONENTS.md` — états, causes, profils
- `OBSERVABILITY.md` — compte-rendus, alertes, métriques
- `EXPERIMENTS.md` — campagnes, progression
- `adr/ADR-002-modes-analyse-et-immersion-de-poids-egal.md` — égalité des modes
- `maquettes/Interface futuriste du launcher LIVEX.png` — maquette de référence

---

## Points restés ouverts dans ce document

- **Navigation à sept entrées ou à trois modes.** C'est le point le plus lourd.
  Ce document raisonne en trois modes, la maquette en sept entrées, et les deux
  découpages ne se recouvrent pas. Il faut choisir, puis aligner l'autre. Voir
  §2.2.
- **Bibliothèque de rendu Markdown.** Le lecteur de rapport et de documentation
  (§3.2) exige un seul composant de rendu ; le choix de la bibliothèque et son
  intégration Avalonia restent à trancher à l'implémentation.
- **Panneau de mesures.** La forme du panneau de mesures et sa visibilité par
  défaut ne sont pas tranchées. Voir `OBSERVABILITY.md` §10.
- **Personnalisation.** La réorganisation des panneaux et la taille des colonnes
  sont fixées en V0.1, mais la manière de les rendre persistants reste à définir.
- **Dialogue de reprise.** Le contenu exact du dialogue proposé pour un paquet
  `recoverable` doit être spécifié avec `PACKAGE_FORMAT.md`.
- **Échelle de densité.** La densité d'information est qualifiée ici mais aucun
  budget de lignes par vue n'est fixé.
- **Thème sombre.** Aucun thème clair n'est prévu en V0.1. La décision doit être
  prise avant l'implémentation de l'interface.
- **Ordre des modes.** L'ordre Contrôle, Analyse, Immersion est retenu. Il faut
  confirmer qu'il ne suggère pas de hiérarchie entre les deux modes, alors même
  que la documentation les déclare égaux.
- **Vues non documentées.** Une seule vue est spécifiée dans `GUI.md`, parce
  qu'une seule est documentée. Les six autres entrées de navigation restent à
  écrire.
- **Écart maquette / prototype.** Le rendu du prototype HTML ne correspond pas à
  la maquette sur le fond, le hero et le grain. Il faut décider lequel des deux
  sert de référence de production. Voir `GUI.md` §12.
