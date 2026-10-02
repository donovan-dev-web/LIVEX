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
| Montrer ce qui tourne, ce qui manque, ce qui est cassé | Calculer des résultats scientifiques |
| Permettre de démarrer et arrêter | Remplacer l'interface d'ECHOS |
| Permettre de créer et suivre une campagne | Analyser une campagne |
| Donner accès aux journaux et aux diagnostics | Servir d'explorateur de fichiers |
| Rester lisible en un coup d'œil | Recomposer la pile à chaque session |

## 2. Organisation générale

### 2.1 Ce que montre la maquette

La maquette de référence montre une sidebar de **sept entrées**, dans une largeur
de 222 px :

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

### 2.2 Modes de lancement et écrans

Le sélecteur supérieur choisit un **mode de lancement**, pas un écran. La sidebar
reste disponible dans les quatre modes et contient neuf écrans :

La pile affichée comporte trois rôles : SYNE, ECHOS et PRISM. `syne-mock` reste
détecté comme une installation distincte, mais il n'est pas une quatrième carte :
c'est le mode « Émulé » du rôle SYNE. L'état du système regroupe le moteur réel et
son émulateur sous une seule ligne SYNE ; ils ne peuvent pas être démarrés
simultanément.

| Écran | Contenu livré |
| :-- | :-- |
| Accueil | État des composants, actions de démarrage/arrêt, expériences récentes |
| Expériences | Liste des paquets `.livexp` connus et ouverture |
| Campagnes | Paramètres d'une campagne, progression, annulation et reprise |
| Analyse | Rapport d'émergence ECHOS du paquet ouvert, s'il existe |
| Rapports | Même rapport source et export Markdown non modifié |
| Configuration | Mode, profils, sélection de composants, installations/version actives et ajout d'un dossier de composant portant un manifeste valide |
| Logs | Dernières entrées du journal de session, ouverture du dossier et export de l'historique de session en NDJSON ; ouverture d'un paquet, consultation et export individuel des journaux de run. |
| Monitoring | Santé, PID, version et ressources locales échantillonnées ; les métriques détaillées des flux ne sont pas encore exposées. |
| Documentation | Documents Markdown du projet distribués avec l'application |

Les quatre modes (`Console`, `Standard`, `Développement`, `Personnaliser`)
déterminent les composants demandés et leur rôle. Ils ne remplacent pas les écrans.
PRISM est un composant de la pile, pas une entrée de navigation ; son profil reste
verrouillé tant que son manifeste ne satisfait pas `INTEGRATION_CONTRACT.md` §11.1.

La maquette historique représente sept écrans. La navigation applicative est
étendue à neuf entrées : Monitoring et Documentation sont ajoutés pour répondre
au périmètre V1. La maquette n'est donc plus exhaustive sur ce point.

### 2.3 Zones et persistance

| Zone | Rôle | Persistance |
| :-- | :-- | :-- |
| **Barre de titre** | Identité, mode, état global | Non |
| **Sidebar** | Navigation, identité en pied | Non |
| **Contenu** | Vue sélectionnée | Non |
| **Colonne d'état** | État du système, ressources, journaux | Non |

La colonne d'état est **toujours visible**. L'état de la pile ne doit jamais
nécessiter une navigation pour être connu.

La V1 expose les dernières entrées du journal Launcher et permet d'exporter ce
journal de session. Depuis Logs, un paquet `.livexp` peut être ouvert pour
consulter ou exporter les journaux stdout/stderr associés à ses runs. Les entrées
sont lues sans extraction ; l'affichage est limité à 16 Mio et l'export à 256 Mio
par fichier.
Les commandes de pause restent hors périmètre.

La maquette ne comporte pas de barre de composants ni de barre de campagne
dédiées comme dans le schéma initial : ces informations sont **portées par la
colonne d'état** et par la section COMPOSANTS du contenu.

## 3. Modes de lancement V1

Le mode `Standard` est la valeur par défaut. Le mode choisit un profil ; les
composants peuvent aussi être démarrés individuellement depuis leurs cartes.

| Mode | Profil demandé | Comportement et limites |
| :-- | :-- | :-- |
| **Console** | Expérience (`syne` + `echos`) | Cible les runs sans PRISM. La campagne du Launcher tourne en arrière-plan de l'interface. Le moteur SYNE réel est sélectionné par défaut ; les adaptateurs headless ECHOS et syne-mock sont disponibles sous Linux, mais SYNE réel ne satisfait pas encore le contrat de lancement batch. |
| **Standard** | Simulation seule par défaut | Utilisation interactive avec SYNE réel par défaut ; l'utilisateur peut choisir « Émulé » dans Configuration et démarrer les composants manuellement ou choisir un profil. |
| **Développement** | SYNE émulé + `echos` | Impose le mode émulation de SYNE et démarre les services Linux déclarés par manifeste, dont le contrôle HTTP et le WebSocket du mock. L'émulateur n'est pas un exécuteur de campagne `.livexp` compatible avec `ProcessRunExecutor`. |
| **Personnaliser** | Sélection explicite | Sélection des rôles par composant ; SYNE est une seule option avec le choix « Réel / Émulé ». Les installations valides et leurs versions restent détectables et sélectionnables dans Configuration. Les manifestes réels ne publient pas encore plusieurs variantes UI/headless sélectionnables. |

Les manifestes de composants réels fournis dans le dépôt sont actuellement des
adaptateurs Linux ; les exécutables Windows ne sont pas encore empaquetés. Le
bouton « Ajouter un dossier de composant » inspecte `component.json`, refuse
les manifestes invalides, les exécutions absentes et les types inconnus, puis
enregistre le dossier pour les prochaines détections dans
`~/.livex/components.registry`. Le choix de l'installation active est mémorisé
dans `~/.livex/active-components.json` et réappliqué quand cette installation
est à nouveau détectée.

La résolution affiche les composants manquants et refuse un profil non
satisfaisable. Dans le code livré, l'intégration bout en bout est vérifiée avec les
stubs du Launcher. Cela ne prouve pas que SYNE, ECHOS ou `syne-mock` réels
respectent déjà le contrat : voir `INTEGRATION_CONTRACT.md` et `ISSUES.md`.

### 3.1 Campagnes et rapports

Une campagne peut configurer son titre, simulation, nombre de runs, horizon en
ticks, nombre initial d'agents et graine de base. Les graines sont dérivées de
façon reproductible ; la politique d'échec reste celle du domaine. Le Launcher
affiche la progression, permet l'annulation et propose la reprise d'un paquet
récupérable.

Le lecteur Markdown montre sans recalcul le rapport `analysis/emergence_report.md`
stocké dans le paquet. L'export écrit le contenu Markdown original à l'emplacement
choisi. L'absence de rapport est signalée explicitement.

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

Le rendu Markdown utilise `Markdown.Avalonia` pour le rapport et la documentation.

La documentation ainsi consultable est celle **embarquée dans l'installation**
(dossier `docs/` de l'arborescence, `ARCHITECTURE.md` §10.2) : guides du paquet,
référence des profils, aide au diagnostic. Le lecteur ne modifie jamais les
fichiers qu'il affiche, rapport comme documentation.

La distinction tient en une règle : **le Launcher ne calcule rien de scientifique,
mais il sait afficher un résultat**. Voir `adr/ADR-003-analyse-propriete-de-echos.md`.

L'interface d'ECHOS dans un navigateur n'est pas encore déclenchable depuis les
écrans V1. L'analyse et la production du rapport nécessitent les opérations
headless documentées dans `INTEGRATION_CONTRACT.md` §10, encore à valider contre
ECHOS réel.

### 3.2 PRISM et le verrou Immersion

PRISM apparaît dans la pile et peut être demandé par un profil. Sa sélection n'est
possible que si une installation détectée fournit un manifeste conforme aux
exigences publiées. Sinon, le profil est refusé avec la cause et le jalon G7. Le
Launcher n'héberge aucun rendu 3D : PRISM reste propriétaire de cette surface.

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
| Paquet corrompu | Erreur de lecture explicite ; aucune ouverture partielle n'est prétendue |
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

## 9. Ouverture de l'interface d'ECHOS (à réaliser)

Ce parcours est une cible documentée, pas une commande disponible dans l'interface
V1 actuelle.

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

- **Navigation étendue.** Les deux entrées V1 ajoutées à la maquette historique
  n'ont pas encore de spécification pixel dédiée dans `GUI.md`.
- **Variantes de composants.** SYNE réel/émulé et les installations actives sont
  sélectionnables ; les manifestes réels ne déclarent pas encore les variantes
  UI/headless sélectionnables.
- **Panneau de mesures.** La forme du panneau de mesures et sa visibilité par
  défaut ne sont pas tranchées. Voir `OBSERVABILITY.md` §10.
- **Journaux.** L'écran expose l'historique du journal Launcher. La navigation
  unifiée vers les journaux stdout/stderr de chaque run reste à spécifier.
- **Échelle de densité.** La densité d'information est qualifiée ici mais aucun
  budget de lignes par vue n'est fixé.
- **Accessibilité et adaptativité.** Les exigences du §8 doivent être vérifiées
  sur l'application rendue et les tailles de fenêtre supportées.
- **Écart maquette / prototype.** Le rendu du prototype HTML ne correspond pas à
  la maquette sur le fond, le hero et le grain. Il faut décider lequel des deux
  sert de référence de production. Voir `GUI.md` §12.
