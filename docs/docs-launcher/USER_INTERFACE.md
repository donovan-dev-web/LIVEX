# USER_INTERFACE.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `VISION.md`, `COMPONENTS.md`, `ARCHITECTURE.md`
**Source Monographie** : —

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

La navigation est organisée par **modes d'utilisation**, pas par couches techniques.
Les deux modes scientifique sont de poids égal ; le mode Contrôle est le socle.

```text
┌────────────────────────────────────────────────────────────────┐
│  livex-launcher          Profil : Analyse          [État global] │
├───────────────┬────────────────────────────────────────────────┤
│  Modes        │                                                │
│               │                                                │
│  ● Contrôle   │                                                │
│  ● Analyse    │             Zone de contenu                   │
│  ○ Immersion  │                                                │
│    (verrouillé)          de la vue sélectionnée                │
│               │                                                │
├───────────────┴────────────────────────────────────────────────┤
│  Composants   SYNE ●   ECHOS ●   PRISM ○ verrouillé            │
├────────────────────────────────────────────────────────────────┤
│  Campagne   calibration-saison-3   5/12   ██████░░░░░          │
└────────────────────────────────────────────────────────────────┘
```

| Zone | Rôle | Persistance |
| :-- | :-- | :-- |
| **Barre de titre** | Identité, profil actif, état global | Non |
| **Navigation des modes** | Les trois modes, avec verrouillage visible | Non |
| **Zone de contenu** | Vue du mode sélectionné | Non |
| **Barre des composants** | État synthétique de chaque composant | Non |
| **Barre de campagne** | Campagne active et progression, le cas échéant | Non |

La barre des composants et la barre de campagne sont **toujours visibles**. L'état
de la pile ne doit jamais nécessiter une navigation pour être connu.

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
| **Rapport** | **Visualisation du rapport d'émergence**, navigation par section |
| **Télémétrie** | Ouverture de l'interface d'ECHOS dans le navigateur, facultative |

#### Le lecteur de rapport

La visualisation du rapport d'émergence dans le Launcher est une fonctionnalité de
premier plan. L'utilisateur lit son résultat **dans l'outil qui a lancé la
campagne**, sans changer d'application.

| Règle | Comportement |
| :-- | :-- |
| **Source unique** | Le lecteur affiche `analysis/emergence_report.md`, écrit par ECHOS. |
| **Fidélité** | L'affichage rend le contenu produit par ECHOS. Toute présentation ajoutée doit être signalée comme telle. |
| **Navigation** | Le rapport est découpé selon ses sections : Expérience, Configuration, Simulation, Runs, Analyse individuelle, Analyse agrégée, Motifs émergents, Synthèse statistique, Variance entre runs, Anomalies, Conclusion, Reproductibilité. |
| **Provenance** | Chaque section affiche son horodatage et la version d'ECHOS qui l'a produite. |
| **Absence** | Sans ECHOS, aucun rapport n'est produit. L'absence est affichée, jamais remplacée par une approximation locale. |

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

La mise en page est identique pour tous les modes, afin que l'utilisateur n'ait
rien à réapprendre.

```text
┌────────────────────────────────────────────────────────────┐
│  Titre de la vue                          Actions à droite  │
├────────────────────────────────────────────────────────────┤
│                                                            │
│   Contenu principal                                        │
│   (listes, panneaux, formulaires)                          │
│                                                            │
│                                                            │
├────────────────────────────────────────────────────────────┤
│  Détail de la sélection          │  Actions contextuelles │
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
| **L'état est visible en permanence** | La barre des composants est toujours présente |
| **Une couleur par état** | Vert, ambre, orange, rouge, gris, avec un libellé textuel |
| **La couleur ne suffit jamais** | Chaque état porte aussi un texte, pour l'accessibilité |
| **Densité d'outil** | Information dense, sans espace superflu |
| **Hiérarchie en trois niveaux** | En-tête de vue, contenu, détail |
| **Aucun effet décoratif** | Pas d'animation permanente, pas de dégradé |
| **Action destructive explicitée** | Suppression, annulation et réinitialisation sont distinguées |
| **Locus du verrouillage** | Un cadenas avec son motif, à l'entrée du mode |

L'interface ne comporte **aucun rendu temps réel**. Elle affiche des états et des
progressions échantillonnées, ce qui évite toute promesse de fluidité qu'elle ne
pourrait pas tenir.

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

- `VISION.md` — intentions, non-objectifs, égalité des modes
- `COMPONENTS.md` — états, causes, profils
- `OBSERVABILITY.md` — compte-rendus, alertes, métriques
- `EXPERIMENTS.md` — campagnes, progression
- `adr/ADR-002-modes-analyse-et-immersion-de-poids-egal.md` — égalité des modes

---

## Points restés ouverts dans ce document

- **Panneau de mesures.** La forme du panneau de mesures et sa visibilité par
  défaut ne sont pas tranchées. Voir `OBSERVABILITY.md` §9.
- **Personnalisation.** La réorganisation des panneaux et la taille des colonnes
  sont fixées en V0.1, mais la manière de les rendre persistants reste à définir.
- **Dialogue de reprise.** Le contenu exact du dialogue proposé pour un paquet
  `recoverable` doit être spécifié avec `PACKAGE_FORMAT.md`.
- **Échelle de densité.** La densité d'information est qualifiée ici mais aucun
  budget de lignes par vue n'est fixé.
- **Thème sombre.** Aucun thème sombre n'est prévu en V0.1. La décision doit être
  prise avant l'implémentation de l'interface.
- **Ordre des modes.** L'ordre Contrôle, Analyse, Immersion est retenu. Il faut
  confirmer qu'il ne suggère pas de hiérarchie entre les deux modes, alors même
  que la documentation les déclare égaux.
