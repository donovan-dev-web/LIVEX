# PRISM III. L'échelle, la cadence et le transport

Un monde logique et un monde rendu ne parlent pas la même langue : l'un compte en unités abstraites et en ticks, l'autre en centimètres et en images par seconde. Ce chapitre décrit les deux ponts qui les relient : une **échelle** spatiale, `k`, et une **cadence** temporelle, `R`, puis les deux canaux qui transportent les données de l'un vers l'autre.

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande un **environnement** virtuel, et non un tableau de nombres. Pour qu'un observateur humain puisse juger si une structure apparaît, il faut la voir : des entités qui se rassemblent, un territoire qui se dessine, un flux qui se déplace. Or voir oblige à convertir, et convertir mal produit une illusion : un monde trop rapide paraît agité, un monde trop lent paraît mort, une échelle fausse fait paraître géants des voisins. Ce chapitre traite donc la partie du projet qui rend l'émergence **regardable sans la trahir**.

## 1. Deux transports, pas un

PRISM parle à SYNE par deux canaux distincts, tous deux locaux par défaut.

| Sens | Transport | Adresse par défaut | Rôle |
| :-- | :-- | :-- | :-- |
| SYNE vers PRISM | WebSocket | `ws://127.0.0.1:5180` | Monde préparé, instantanés, deltas, événements |
| PRISM vers SYNE | HTTP | `http://127.0.0.1:5181` | Commandes de contrôle, lecture d'état |

La séparation n'est pas cosmétique. Les **données** d'un monde sont volumineuses et continues : elles doivent circuler en flux, dans un sens, sans attendre de réponse. Les **commandes** sont rares, ponctuelles et attendues : elles doivent recevoir un accusé de réception, donc un cycle requête-réponse. Réunir les deux sur un même canal obligerait à traiter le contrôle comme un cas particulier d'un flux massif, ce qui rendrait chaque commande dépendante de la santé d'un tuyau de données. Deux transports, c'est deux régimes de panne séparés.

Les deux serveurs sont activés au lancement du moteur (observabilité pour l'un, service de contrôle pour l'autre) et écoutent en local par défaut. Lancer SYNE et lancer le projet Unreal sont deux actions distinctes : le monde ne doit pas dépendre de son afficheur.

## 2. L'échelle spatiale : un facteur, une convention

**Nom** : Facteur d'échelle spatiale.

```math
k = \frac{\text{tuile}_{WP}}{\text{cellSize}} \qquad k = 100\ \text{uu par unité SYNE}
```

**Explication globale** : Une unité du monde logique vaut cent unités Unreal, et Unreal travaille en centimètres. Une unité SYNE vaut donc **un mètre**. Cette coïncidence n'est pas un hasard : elle a été choisie pour que les grandeurs du monde (rayon de perception, portée de pulsation, dimensions du terrain) se lisent directement en unités humaines.

**Pourquoi cette convention** : Un facteur d'échelle doit satisfaire deux contraintes opposées. Trop petit, il rend le monde minuscule et les entités invisibles ; trop grand, il épuise le budget de rendu et casse la navigation. Le choix de `k = 100` fixe le monde à une taille où une entité se déplace d'une fraction de mètre par tick, ce qui reste perceptible à l'image, et où le terrain complet tient dans une mémoire de rendu raisonnable. Le facteur est calculé à partir de la taille de tuile du partitionnement de monde, et non codé en dur : un changement de découpe spatiale ne doit pas changer l'échelle perçue.

**La conséquence, en chiffres.** Le monde du profil de rendu mesure 2 240 unités de côté. À un mètre par unité, cela fait **2,24 kilomètres de côté**, soit environ **5 km²**. La grille logique compte 70 cellules de 32 unités, c'est-à-dire des cellules de 32 mètres. Une aire de 5 km² n'est pas un décor : c'est une région, avec des distances qui obligent à anticiper, et c'est précisément ce qu'on veut d'un monde où l'exploration doit être coûteuse.

## 3. La cadence : convertir un tick en seconde

**Nom** : Ratio de temps simulé.

```math
R = \text{ticksParSeconde} \times \text{secondesSimuléesParTick} \qquad \text{1 seconde réelle} = R\ \text{secondes simulées}
```

**Explication globale** : Le moteur avance par ticks, et chaque tick représente une durée simulée. Le produit des deux donne le rapport entre le temps de l'observateur et le temps du monde. Avec un tick de cinq secondes simulées et six ticks par seconde, **une seconde réelle vaut trente secondes simulées**.

**Pourquoi cette forme** : Les deux facteurs répondent à deux besoins différents. Le nombre de ticks par seconde est un **budget de calcul** : il faut que le moteur tienne la charge. La durée simulée par tick est une **échelle narrative** : elle décide ce qu'un tick signifie. Les confondre serait une erreur ; les multiplier donne la seule grandeur qui compte pour un spectateur humain, à savoir « à quelle vitesse le monde vit ». Une journée simulée de 86 400 secondes correspond alors à environ 48 minutes réelles : assez court pour qu'une expérience tienne dans une session, assez long pour que les rythmes du monde (dérive des besoins, saisons) ne soient pas instantanés.

**Une distinction qui évite un piège.** Le nombre de ticks par seconde est celui du **moteur de simulation**, jamais le framerate du rendu. Un jeu qui s'affiche à soixante images par seconde peut recevoir six instantanés par seconde : c'est le rôle du rendu d'**interpoler** entre deux états reçus, et non d'exiger du moteur qu'il produise des états à la cadence de l'écran. Confondre les deux ferait dépendre la science de la puissance graphique, exactement ce que la séparation des composants interdit. Le profil de référence du moteur tourne à dix ticks par seconde ; le profil destiné au rendu est réglé à six, pour que le flux reste soutenable pendant qu'Unreal dessine.

## 4. Le cycle de préparation

Un monde ne se lance pas : il se **prépare**. L'ordre importe, et le plugin l'impose par une séquence explicite.

```text
Connect
  vers Prepare(graine, ticksParSeconde)
  vers OnWorldInitialized (topologie du monde)
  vers construction de la présentation (côté Unreal)
  vers Ready(version du monde)
  vers OnControlResult
  vers Start(graine, ticks maximum)
  vers OnSnapshot
```

Deux propriétés de ce cycle méritent d'être retenues. D'abord, la **topologie** arrive une fois, dans le message de monde initialisé : obstacles, taille du monde, valeurs d'échelle et de cadence. Ensuite, chaque **instantané** porte l'état complet et daté. Le cycle existe parce que le monde doit être construit avant d'être animé : générer un décor après le premier tick produirait des entités qui marchent dans le vide.

L'ordre est vérifié. Après une préparation explicite, le moteur exige l'accusé de réception avant le démarrage ; ignorer la séquence ou réutiliser une graine incompatible produit une erreur explicite (par exemple un `409 world_not_ready`). C'est une exigence de reproductibilité déguisée en protocole : un monde dont on ne sait pas s'il a été préparé correctement n'est pas un monde reproductible. La lecture d'état renvoie d'ailleurs les paramètres effectifs du monde préparé, et non des valeurs par défaut devinées.

## 5. Ce que le moteur exporte, ce que le rendu invente

Une frontière stricte sépare les deux côtés, et elle est la garantie que le rendu ne décide rien.

| Élément | Qui le produit |
| :-- | :-- |
| Positions, besoins, croyances, objectifs, actions | SYNE |
| Groupes, relations, messages, ressources | SYNE |
| Taille du monde, échelle, cadence, obstacles | SYNE |
| Décor, matériaux, éclairage, niveau de détail, sons | PRISM |

PRISM **génère de la présentation**, jamais de la décision. Le choix d'une couleur pour un groupe, d'un modèle pour une entité, d'un effet pour une pulsation lumineuse : tout cela appartient au rendu et n'influence pas le monde. Cette répartition est la traduction visuelle d'un principe déjà rencontré à propos de l'observatoire : **montrer n'est pas décider**. Si le rendu pouvait modifier le monde, un changement de décor deviendrait un paramètre expérimental caché.

## 6. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré les deux conversions qui rendent le monde visible sans le changer. L'échelle `k` fait qu'une unité logique devient un mètre ; le ratio `R` fait qu'un tick devient une durée compréhensible. Sans ces deux ponts, l'émergence resterait une série de nombres ; avec eux, elle devient un spectacle vérifiable, qu'un humain peut regarder et contester.

La séparation des transports porte la même exigence que celle du reste du projet : les données circulent directement du monde vers l'observateur, les commandes passent par un canal qui répond. Un monde dont l'affichage dépendrait du contrôle serait fragile au moment même où il devient intéressant.

**Ce que ce chapitre ne permet pas de conclure.**

- `k = 100` et la cadence de six ticks par seconde sont des conventions choisies, non des valeurs mesurées ; elles découlent du budget de rendu et de l'échelle narrative, pas d'une étude.
- La borne de vitesse des entités et la densité sociale qui en dépend sont des calculs de cohérence, non des résultats observés.
- Le rendu n'a pas été validé comme **fidèle** : l'interpolation entre deux états est une reconstruction plausible, pas une mesure.
- PRISM reste verrouillé en V0.1 : ce chapitre décrit un transport documenté et un plugin de travail, non un affichage complet en service.
- `syne-mock` parle les mêmes transports mais **n'est pas équivalent** au moteur : une validation faite sur le mock ne vaut pas pour SYNE.
