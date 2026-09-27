# Intégration du plugin PRISM-LDK au projet Unreal PRISM

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM** est le projet Unreal final de LIVEX ; il porte le monde
présenté, les acteurs, le rendu, l'interface et les interactions.
**PRISM-LDK** (*LIVEX Development Kit*, nom de module Unreal `PrismLdk`) est
le plugin d'intégration inclus dans PRISM. LDK désigne le plugin, pas un
projet complet distinct.

Dans le checkout actuel, `prism/LDK/LDK.uproject` fournit un hôte technique
pour compiler et tester le plugin ; ce fichier n'est pas un second produit
ni le projet complet LIVEX. Le C++ de PRISM-LDK reste une couche mince qui
expose contrats/types/événements et fonctions de contrôle à Blueprint ; SYNE
reste le moteur décisionnel et l'autorité de l'état simulé.

## 1. Vérifier l'environnement

1. Pour développer ou valider le plugin, ouvrir l'hôte Unreal du checkout :
   `prism/LDK/LDK.uproject` avec la version Unreal Engine configurée pour le
   dépôt (actuellement documentée comme 5.8.3).
2. Vérifier que le plugin PRISM-LDK (`PrismLdk`) est activé dans **Edit → Plugins**, puis compiler
   la cible Editor et vérifier les dépendances Unreal `HTTP`, `Json` et
   `WebSockets`.
3. Compiler et tester ensuite dans le projet Unreal PRISM ; l'hôte `LDK.uproject`
   sert au développement du plugin et ne remplace pas la validation dans PRISM.

Pour des tests de développement, lancer `syne-mock` depuis son dossier avec
`npm start`. Il expose par défaut le contrôle HTTP sur `127.0.0.1:5181` et le
WebSocket sur `127.0.0.1:5180`. C'est un simulateur de développement, **pas
une implémentation équivalente de SYNE** : ses délibérations et systèmes
sociaux sont approximatifs, le détour local d'obstacle ne remplace pas l'A*
de SYNE et les trajectoires ne sont pas garanties bit à bit. Valider les
décisions et comportements métier avec SYNE réel.

Le terminal du mock affiche les lignes préfixées par `[SYNE-MOCK ...]` :

- `SERVER listening` : ports HTTP et WebSocket ouverts ;
- `WEBSOCKET connected/disconnected` : connexions Unreal ;
- `HTTP request/response` : trafic HTTP ;
- `CONTROL request/response` : phase demandée et code retourné ;
- `PREPARE`, `READY`, `START`, `PAUSE`, `RESUME`, `STOP`, `RESET` :
  transitions de simulation.

Dans Unreal, ouvrir **Window → Output Log** et filtrer sur `LogPrismLdk`.
Les commandes HTTP et les phases sont journalisées au niveau `Log`, les
messages WebSocket individuels, snapshots et deltas au niveau `Verbose`, et
les erreurs au niveau `Error`.

## 2. Créer le contrôleur Blueprint

Dans le GameInstance Blueprint :

1. Utiliser le nœud **Get Prism Ldk Subsystem** fourni par le plugin. Ce nœud
   doit être exécuté depuis le GameInstance Blueprint et sa sortie doit être
   branchée sur la variable `LDK` avant tout appel. Ne pas créer une variable
   `Prism Ldk Subsystem` sans lui affecter la sortie du nœud : une variable
   non initialisée vaut `None`.
2. Appeler `Configure` si les adresses ne sont pas celles par
   défaut.
3. Brancher `OnConnected`, `OnDisconnected` et `OnError` sur un widget de
   diagnostic.
4. Appeler `Connect` uniquement au moment où l'utilisateur ou le Blueprint
   décide de lancer la connexion. Le subsystem ne se connecte pas au démarrage.
5. Dans `OnConnected`, appeler `Prepare(42, 10)` ou fournir la seed et la
   fréquence ticks/seconde choisies par l'utilisateur.

Le second paramètre `TicksPerSecond` est visible sur le nœud Blueprint
`Prepare` (valeur par défaut : `10`). Choisir sa valeur à la préparation avant
de lancer le monde ; le champ sera confirmé sur la structure
`WorldDescription.TicksPerSecond`.

Alternative native Blueprint : **Get Game Instance Subsystem** avec la classe
`PrismLdkSubsystem`, puis conserver directement sa sortie. Dans les deux cas,
il ne faut jamais appeler `Configure`, `Connect` ou `Get Connection State` sur
la variable avant cette affectation.

### Différence entre l'état et les événements

`Get Connection State` est une fonction pure : elle renvoie seulement
`EPrismSyneConnectionState`. Elle s'utilise avec `Switch on
EPrismSyneConnectionState` pour lire l'état à un instant donné.

`OnConnected`, `OnWorldInitialized`, `OnSnapshot`, `OnControlResult` et
`OnError` sont des **Event Dispatchers**. Ils ne se branchent pas sur le fil
d'exécution de `Connect`. Pour les utiliser :

1. Faire glisser la variable `LDK` dans le graphe.
2. Depuis la référence `LDK`, choisir **Assign OnConnected** (ou
   **Bind Event to OnConnected**).
3. Unreal crée un `Custom Event` associé au dispatcher. Donner à cet événement
   un nom comme `EVT_SyneConnected`.
4. Mettre la logique à exécuter dans ce `Custom Event`, par exemple
   `Prepare(Seed, TicksPerSecond)`, en utilisant la même référence `LDK`.

Répéter la même opération pour les autres dispatchers :

| Dispatcher | Custom Event à créer | Action typique |
|---|---|---|
| `OnConnected` | `EVT_SyneConnected` | appeler `Prepare` |
| `OnWorldInitialized` | `EVT_SyneWorldInitialized` | générer les tuiles |
| `OnControlResult` | `EVT_SyneControlResult` | vérifier `bOk`, puis continuer |
| `OnSnapshot` | `EVT_SyneSnapshot` | mettre à jour les agents |
| `OnWorldDelta` | `EVT_SyneWorldDelta` | ajouter/retirer les obstacles |
| `OnError` | `EVT_SyneError` | afficher le code d'erreur |
| `OnDisconnected` | `EVT_SyneDisconnected` | afficher l'état déconnecté |

L'ordre Blueprint recommandé est donc :

```text
Event Init
 → Get Prism Ldk Subsystem
 → Set LDK
 → Assign OnConnected → EVT_SyneConnected
 → Assign OnWorldInitialized → EVT_SyneWorldInitialized
 → Assign OnControlResult → EVT_SyneControlResult
 → Assign OnSnapshot → EVT_SyneSnapshot
 → Assign OnError → EVT_SyneError
 → Configure
```

`Assign OnConnected` ne déclenche pas l'événement immédiatement ; il
enregistre le `Custom Event`. Le `Custom Event` sera appelé plus tard par le
plugin lorsque la connexion WebSocket sera réellement établie.

Pour une connexion explicitement déclenchée depuis un bouton, appeler
`LDK.Connect` dans l'événement `OnClicked` du bouton, après avoir configuré le
subsystem et enregistré les dispatchers. `bAutoReconnect` ne déclenche jamais
la première connexion : il ne sert qu'à retenter une connexion qui avait été
demandée explicitement puis interrompue de façon inattendue. Appeler
`Disconnect` annule cette intention et toute reconnexion automatique.
Par défaut, la reconnexion est activée avec un délai de 2 secondes ; ces
options sont exposées par `FPrismSyneConnectionOptions`.

La seed doit être conservée dans le GameInstance afin de pouvoir reproduire un
run et de relancer `Reset` avec les mêmes paramètres.

## 3. Générer le monde avant le premier tick

Dans `OnWorldInitialized`, construire la scène en phases et ne confirmer
`Ready` qu'une fois toutes les phases terminées :

1. **Topographie** — lire `Width`, `Height`, `CellSize`, `CellCountX` et
   `CellCountY`, puis parcourir `Cells` pour créer les tuiles, appliquer le
   terrain logique, l'altitude abstraite et la navigabilité.
2. **Obstacles** — parcourir le tableau `Obstacles`. Chaque entrée porte
   `Id`, `Position.X/Y` et `Radius` ; instancier les obstacles/collisions
   visuels depuis ces données. `Cells[].ObstacleIds` est une relation
   complémentaire pour identifier les tuiles affectées, pas un remplacement
   des coordonnées/rayons d'obstacle.
3. **Ressources** — parcourir `Resources` et instancier les éléments. La
   description initiale fournit trois emplacements déterministes par type
   (`food`, `water`, `wood`, `mineral`) sur une grille assez grande. `X/Y` sont
   des **indices de cellule**, pas des coordonnées continues du monde.
   `Quantity` décrit ici la quantité associée à l'emplacement initial ; les
   stocks que SYNE simule et renvoie dans les snapshots restent des réserves
   globales par type, pas des stocks localisés synchronisés avec ces acteurs.
4. **Agents** — parcourir `Agents` et créer chaque acteur à sa position
   initiale. Garder ces acteurs dans une map indexée par `Id`, afin que le
   premier snapshot mette à jour les mêmes acteurs sans doublons.
5. **Régions/clusters** — parcourir `Regions` pour préparer les volumes,
   sous-niveaux ou données de World Partition nécessaires.

`Agents` dans `WorldDescription` contient déjà `Id`, `Species` et `Position`.
`TicksPerSecond` confirme la cadence demandée à `Prepare`; il s'agit de ticks de
simulation par seconde réelle, pas d'une fréquence de rendu Unreal.
Le premier `WorldSnapshot` reprend ces identifiants et positions initiales.
SYNE ne possède pas actuellement de configuration de terrains/biomes :
`TerrainType` vaut `plains` pour toutes les cellules. Le monde configurable
comprend les obstacles et territoires, mais pas encore de palette de terrains.
La description préparée contient `Obstacles[]` sous la forme
`{id, x, y, radius}` pour les obstacles initiaux configurés ; le même obstacle
peut aussi être référencé par son ID dans les cellules qu'il recouvre.

Ne pas appeler `Start` avant la fin de cette génération. Le monde décrit par
SYNE est un plan logique 2D. Il faut distinguer trois repères :

1. **Grille d'indexation interne SYNE** : elle sert à accélérer les requêtes
   spatiales (perception/voisinage). Elle n'est pas le terrain Unreal et ses
   cases ne doivent pas être interprétées comme des tuiles visibles.
2. **Grille logique exportée par `WorldDescription`** : `Cells` est la grille
   destinée à la génération procédurale. Une cellule a les coordonnées entières
   `(X,Y)` ; `CellSize` exprime sa largeur en unités de position SYNE. La position
   d'un agent (`Position.X/Y`) reste continue en unités SYNE, pas un numéro de
   cellule. Pour retrouver sa cellule : `CellX = floor(X / CellSize)` et
   `CellY = floor(Y / CellSize)`, bornés aux dimensions du monde.
3. **Grille de tuiles Unreal** : pour l'exemple de conversion utilisé dans ce
   guide, une cellule logique correspond à une tuile de **100 × 100 Unreal
   Units (uu)**. Cette échelle est une convention illustrative, non imposée
   par PRISM-LDK ou SYNE ; le projet PRISM peut en choisir une autre et
   adapter ses conversions. Les
   coordonnées de la tuile sont `TileOrigin = (CellX * 100, CellY * 100)` et
   son centre `(CellX * 100 + 50, CellY * 100 + 50)`. Cela donne une échelle
   `100 uu / CellSize` par unité SYNE ; avec `CellSize = 10`, une unité SYNE
   vaut donc 10 uu. Pour le monde par défaut (500 unités SYNE, CellSize 10),
   les 50 × 50 tuiles couvrent 5000 × 5000 uu, soit 50 × 50 mètres en UE
   (1 uu = 1 cm).

Interpréter les coordonnées selon leur type de donnée, pas seulement selon le
nom `X/Y` :

| Donnée | Repère des coordonnées | Conversion/usage Unreal |
|---|---|---|
| `Cells[].X/Y` | Indices entiers de cellule | Tuile `(X,Y)` ; origine `(100X,100Y)` uu dans l'exemple |
| `Resources[]` de `WorldDescription` | Indices entiers de cellule | Marqueur initial à instancier dans cette tuile |
| `Obstacles[]` de `WorldDescription` | Position continue SYNE + rayon | Centre à convertir en position monde ; utiliser le rayon pour dimensionner la collision |
| `Regions[].X/Y` | Indices entiers de cellule (origine de région) | Découpage/cluster, pas une position d'Actor |
| `Agents[].Position.X/Y` et agents des snapshots | Coordonnées continues du monde SYNE | Calculer la cellule avec `floor(position / CellSize)` |
| `Obstacles[]`, `WorldChanges[]`, `Territories[]` | Coordonnées continues du monde SYNE | Convertir en tuile via `floor(position / CellSize)` ou appliquer l'échelle choisie |

La structure Blueprint `FPrismSyneResource` est actuellement partagée entre
les deux tableaux : pour les ressources initiales, `bHasPosition` et
`bPositionIsCellIndex` sont vrais et `Position.X/Y` sont des indices de cellule ;
pour les ressources des snapshots, ce sont des stocks globaux sans position.
Ne pas utiliser ces stocks de snapshot pour replacer les acteurs de ressource.
Les snapshots SYNE peuvent dépasser 2 milliards de ticks : les champs `Tick`
exposés par le plugin sont des entiers 64 bits.

Pour convertir directement une position continue en unités Unreal, utiliser
`UnrealX = SyneX * 100 / CellSize` et
`UnrealY = SyneY * 100 / CellSize` (origine du monde à `(0,0)`). Avec
`CellSize = 10`, l'agent en `(12.5, 23.0)` est dans la cellule `(1,2)` et son
point XY théorique est `(125,230)` uu. Pour placer l'acteur sur le terrain réel,
utiliser la cellule calculée puis projeter/choisir un point du NavMesh dans les
limites de cette tuile ; le point Unreal projeté n'a pas à reproduire exactement
la position SYNE.

### Pourquoi `World.Obstacles` peut être vide

Le parseur du plugin lit le tableau JSON `world.obstacles` et journalise
maintenant le nombre d'obstacles reçus. Un tableau vide dans `OnWorldInitialized`
signifie généralement que le serveur a préparé un monde sans layout : SYNE a
`world.obstacles = false` et `world.obstacleLayout = []` par défaut. Le nœud Blueprint `Prepare` du plugin
envoie seulement `Seed` et `TicksPerSecond`, pas de configuration d'obstacles.
Il faut donc fournir le layout côté serveur, ou étendre le contrat/nœud de
préparation pour transmettre cette configuration. L'API HTTP SYNE accepte
`config.world` dans la requête `prepare`, mais le nœud Blueprint actuel ne
l'expose pas. L'endpoint `prepare` du mock accepte maintenant également
`config.world`; dans son fichier de démarrage, le layout peut aussi être
configuré à la racine. Activer uniquement le booléen sans fournir de layout ne
crée aucun obstacle.

Exemple de requête directe à l'API HTTP SYNE :

```json
{
  "seed": 42,
  "ticksPerSecond": 10,
  "config": {
    "world": {
      "obstacles": true,
      "obstacleLayout": [
        { "id": "rock-1", "x": 150, "y": 120, "radius": 8 }
      ]
    }
  }
}
```

Dans la configuration de démarrage du mock, le même layout se place sous
`world` à la racine, avec `obstacles: true` et `obstacleLayout: [...]`.
Les coordonnées et le rayon de cet obstacle sont en unités continues SYNE. Le
parseur Unreal transforme `x/y` en `FPrismSyneWorldObstacle.Position` et conserve
le rayon dans `Radius`.

L'échelle utilisée dans les exemples ci-dessus est seulement une convention de
rendu de ce guide. La grille logique est une simplification de la topographie. Les cellules
exportées ont actuellement toutes le terrain `plains` et une hauteur `0` ;
SYNE ne simule ni altitude, ni pente, ni NavMesh. Unreal reste responsable du
relief et de la position verticale.

Pour placer un agent initial, utiliser sa coordonnée SYNE pour sélectionner la
tuile, puis rechercher un point valide du NavMesh **à l'intérieur des limites
de cette tuile** et y placer l'Actor. Quand un snapshot indique que l'agent a
changé de cellule logique, Blueprint peut lancer `AI Move To` vers un point
atteignable tiré dans la tuile cible. Contraindre/projeter le point NavMesh dans
les bornes de la tuile : une recherche par rayon seule peut choisir un point
dans une tuile voisine. Ne lancer pas un nouvel `AI Move To` à chaque snapshot
si la cellule cible n'a pas changé.

Le point NavMesh et son Z sont une **projection visuelle Unreal**, non la
position simulée. Garder l'ID SYNE et les coordonnées logiques comme état
autoritaire ; ne pas renvoyer à SYNE la position aléatoire ou l'altitude Unreal.
Les déplacements en sous-cellule peuvent être interpolés visuellement, mais
l'Actor peut rester décalé du point continu exact SYNE afin de respecter relief
et NavMesh. L'échelle ci-dessus est une convention de rendu, pas une propriété
du moteur SYNE.

Créer une fonction Blueprint `BuildWorldFromDescription` afin de pouvoir
remplacer plus tard les tuiles ou la stratégie World Partition sans modifier
la connexion SYNE.

## 4. Accuser réception puis démarrer

Après la génération réussie :

1. Appeler `Ready(WorldVersion)`, normalement `"1.0"`.
2. Attendre `OnControlResult` avec `bOk = true`.
3. Appeler `Start(Seed, MaxTicks)` avec le même seed que `Prepare`. Le run
   réutilise exactement le monde et le `TicksPerSecond` mémorisés ; pour changer
   l'un de ces paramètres, appeler `Prepare` à nouveau et régénérer le monde.
4. Afficher l'état courant depuis `GetStatus`.

Le flux normal est :

```text
Connect → Prepare(Seed, TicksPerSecond) → OnWorldInitialized → génération Unreal
→ Ready → OnControlResult(ok) → Start → OnSnapshot
```

Après un `Prepare` explicite, SYNE refuse `Start` avec `409 world_not_ready`
tant que `Ready` n'a pas été accepté.

## 5. Projeter les snapshots dans la scène

Dans `OnSnapshot` :

1. Indexer les acteurs d'agents par leur identifiant SYNE.
2. Pour chaque agent du tableau `Agents`, créer ou retrouver l'acteur.
3. Convertir `Position.X/Y` en cellule logique via `floor(position / CellSize)`.
   Si cette cellule a changé, échantillonner un point du NavMesh dans la tuile
   correspondante et appeler `AI Move To`.
4. Mettre à jour les variables d'état : énergie, faim, soif, fatigue,
   intention et action. Le tableau `Actions` rapporte aussi les résultats et
   deltas d'action du tick, avec l'identifiant de l'agent.
5. Réconcilier les obstacles à partir de `Obstacles` (état complet) et appliquer
   `WorldChanges` pour les mutations de ce tick.
6. Mettre à jour les réserves, groupes, territoires, saisons et livres depuis
   les tableaux complets du snapshot.
7. Détruire ou désactiver les acteurs qui ne sont plus présents si le
   contrat de simulation l'indique.

Il est recommandé de ne pas appeler `Get All Actors Of Class` à chaque tick.
Utiliser une `Map<int64, Actor>` conservée dans un composant ou un
GameInstance Blueprint.

### Cadence de simulation et fréquence de rendu

La fréquence Unreal (FPS) et `TicksPerSecond` sont indépendantes. Un tick SYNE
représente **une minute simulée** ; le paramètre donné à `Prepare(Seed,
TicksPerSecond)` règle seulement combien de ticks sont calculés par seconde
réelle. La configuration ne doit donc pas être mise à 60 uniquement parce que
le jeu vise 60 FPS.

À 60 FPS Unreal, les rapports théoriques sont :

| Ticks/s configurés | Temps réel par tick | Frames Unreal par tick à 60 FPS | Temps simulé par seconde réelle | Accélération vs temps réel |
|---:|---:|---:|---:|---:|
| 1 | 1 s | 60 | 1 min | 60× |
| 5 | 200 ms | 12 | 5 min | 300× |
| 10 (défaut) | 100 ms | 6 | 10 min | 600× |
| 15 | 66,7 ms | 4 | 15 min | 900× |
| 30 | 33,3 ms | 2 | 30 min | 1 800× |
| 60 | 16,7 ms | 1 | 60 min | 3 600× |
| 120 | 8,3 ms | 0,5 (2 ticks/frame) | 120 min | 7 200× |

Exemple d'appel depuis le GameInstance : `Prepare(42, 10)` demande 10 ticks de
simulation par seconde ; laisser `TicksPerSecond` à 10 (valeur par défaut)
produit le même réglage. `world.ticksPerSecond` confirme la valeur préparée.
`Start` doit réutiliser le seed déjà préparé ; il ne reconstruit pas un monde
explicitement préparé et refuse un seed différent. Pour changer seed ou cadence,
relancer `Prepare`, reconstruire la scène, puis accuser `Ready`.

Les formules sont `secondes réelles/tick = 1 / TicksPerSecond`,
`frames/tick = FPS Unreal / TicksPerSecond` et
`minutes simulées/seconde réelle = TicksPerSecond`. Les valeurs de temps
simulé supposent le contrat SYNE actuel d'une minute par tick.

Le déplacement SYNE est une mise à jour logique, pas une animation 3D. Lorsqu'un
agent exécute une action de déplacement, `speed` (initialisé entre 0,5 et 1,5
unités SYNE par tick pour le profil standard) borne le pas de déplacement de
ce tick. Les agents peuvent effectuer des actions non motrices ; les obstacles,
les cibles et les décisions peuvent réduire ou annuler ce déplacement.

| Cadence | Snapshots globaux/s | Résultats `Actions`/s pour N agents vivants | Pas max si mouvement à chaque tick | Équivalent Unreal à 100 uu/cellule (CellSize=10) |
|---:|---:|---:|---:|---:|
| 1 tick/s | 1 | N | 0,5–1,5 unités SYNE/s | 5–15 uu/s (0,05–0,15 m/s) |
| 5 ticks/s | 5 | 5N | 2,5–7,5 unités SYNE/s | 25–75 uu/s (0,25–0,75 m/s) |
| 10 ticks/s (défaut) | 10 | 10N | 5–15 unités SYNE/s | 50–150 uu/s (0,5–1,5 m/s) |
| 30 ticks/s | 30 | 30N | 15–45 unités SYNE/s | 150–450 uu/s (1,5–4,5 m/s) |
| 60 ticks/s | 60 | 60N | 30–90 unités SYNE/s | 300–900 uu/s (3–9 m/s) |

À `CellSize=10`, traverser une tuile logique de 10 unités demande environ
7–20 ticks de mouvement réussi selon le trait `speed`, soit environ 0,67–2 s
à 10 ticks/s **si** l'agent se déplace à chacun de ces ticks. Ce calcul n'est
pas une durée garantie : chaque tick produit une décision/action, mais toutes
les actions ne sont pas motrices.

| Travail par tick SYNE | Nombre produit par tick | Débit à TicksPerSecond = T |
|---|---:|---:|
| Snapshot global | exactement 1, tous les agents et l'état dynamique complet | T snapshots/s |
| Résultat d'action | 1 par agent vivant (`Actions[]`) | N × T entrées/s si N agents restent vivants |
| `decision_made` | 1 événement par agent vivant | jusqu'à N × T événements/s |
| Déplacement logique | 0 ou 1 pas par agent selon l'action choisie | au plus N × T pas/s |

`Actions[]` indique ce qui a été exécuté ; `Agents[]` contient l'état après
exécution, la position continue, l'intention et l'action courante. Le moteur
n'assure pas une proportion fixe d'actions `Move` par seconde. Les tableaux
per-agent présents dans un snapshot ne sont donc pas des demandes d'animation
à rejouer : utiliser la position/tuile cible pour la navigation Unreal et
`Actions[]` pour le résultat décisionnel.

### Contrat global des snapshots

SYNE et `syne-mock` émettent exactement **un objet `snapshot` global par tick**.
Le plugin diffuse un seul `OnSnapshot` correspondant ; `FPrismSyneWorldSnapshot`
expose les agents, réserves, obstacles, groupes, territoires, livres, mutations
de ce tick (`WorldChanges`) et actions/résultats de ce tick (`Actions`). Les
positions et identifiants d'agents de `Agents[]` sont l'état complet de la
population vivante, pas une liste de changements.

La grille/topologie (`Cells`, terrain, dimensions) est initialisée une fois via
`OnWorldInitialized`. Elle est statique dans le contrat actuel : SYNE n'a pas
de changement de terrain, altitude ou végétation dynamique. `WorldChanges[]`
rapporte actuellement l'ajout/retrait d'obstacles ; `Obstacles[]` est la liste
complète courante. `Resources[]` expose les stocks globaux de SYNE, et non une
quantité localisée/déplétée par nœud spatial ; les emplacements de ressource
du `WorldDescription` sont des marqueurs de génération et ne sont pas encore
des stocks locaux simulés.

Les notifications historiques `OnWorldDelta` et `OnSyneEvent` restent
disponibles et peuvent refléter les mêmes mutations/actions que le snapshot.
Pour éviter les doubles effets, Blueprint doit choisir `OnSnapshot` comme
source de vérité de l'état ; utiliser les dispatchers d'événements pour le
journal, l'UI ou les effets ponctuels, sans réappliquer leurs changements.

### Énumérations Blueprint des valeurs connues

Le plugin expose des enums pour faciliter les `Switch` en Blueprint :
`EPrismSyneTerrainType` (`Plains`), `EPrismSyneResourceType` (`Food`,
`Water`, `Wood`, `Mineral`), `EPrismSyneSeason` et `EPrismSyneAction`
(`Idle`, `SeekFood`, `SeekWater`, `Rest`, `Flee`, `Socialize`, `Explore`,
`Eat`, `Drink`), ainsi que le résultat `Executed`/`Blocked`.
Pour permettre l'évolution indépendante du moteur, les structures conservent
aussi la valeur texte originale ; toute valeur inconnue produit l'enum
`Unknown` sans perdre le texte. Les UENUM sont définis dans le code du plugin,
pas comme des assets Enum éditables par projet ; ajouter une nouvelle valeur
officielle du contrat demande une mise à jour et compilation du plugin.

Pour garder le rendu fluide, ne déplace pas les Actors à la cadence du tick
comme s'ils étaient synchronisés avec le rendu : mémorise position précédente
et cellule cible, puis interpole/termine le `AI Move To` indépendamment du FPS.
À 60 FPS et 10 ticks/s, il y a en moyenne 6 frames Unreal par tick ; à 60
ticks/s, environ 1 frame par tick. Si plusieurs ticks arrivent entre deux
frames, consomme-les dans l'ordre ou applique le dernier état après avoir
traité les mutations, sans supposer un callback de snapshot par frame.

## 6. Appliquer les mutations du monde

Dans `OnWorldDelta` :

- `Kind = added` : créer l'obstacle et mettre à jour la collision de la
  cellule concernée ;
- `Kind = removed` : retirer l'obstacle et restaurer la cellule si aucun autre
  obstacle ne la bloque ;
- utiliser `Tick` et `RunId` pour ignorer un delta appartenant à un ancien run.

Les deltas actuels concernent principalement les obstacles et constructions.
Les ressources, biomes, végétations et hauteurs dynamiques devront être
traités lorsqu'ils seront ajoutés au contrat SYNE.

## 7. Construire le menu de contrôle

Créer un Widget Blueprint avec les boutons suivants :

| Bouton | Action |
|---|---|
| Connecter | `Connect` |
| Préparer | `Prepare` |
| Démarrer | `Start` après `Ready` |
| Pause | `Pause` |
| Reprendre | `Resume` |
| Arrêter | `Stop` |
| Réinitialiser | `Reset` puis nouvelle préparation si nécessaire |
| Statut | `RequestStatus` |

Désactiver les boutons selon `GetConnectionState` et `GetStatus.State`.
Afficher `OnError` avec le code (`world_not_ready`, `run_active`, etc.) au lieu
de supposer que chaque commande réussit.

## 8. Gestion des événements et diagnostic

Brancher `OnSyneEvent` sur un dispatcher Blueprint. Conserver une branche
`Unknown` et afficher au minimum `Type`, `Tick`, `RunId` et `ValueJson`.
Les événements sont extensibles : ne pas dépendre d'un ordre unique autre que
celui documenté pour les snapshots.

Pour un premier écran de diagnostic, afficher :

- état de connexion ;
- état SYNE ;
- fréquence de simulation `TicksPerSecond` ;
- `RunId` ;
- tick courant et `MaxTicks` ;
- nombre d'agents vivants ;
- version du monde ;
- dernier code d'erreur.

## 9. Tests d'acceptation Unreal

La première intégration est terminée lorsque le scénario suivant fonctionne :

1. Lancer le mock pour valider le transport et les graphes Blueprint, ou
   lancer SYNE réel pour valider les comportements de simulation.
2. Ouvrir une map du projet Unreal PRISM, qui intègre PRISM-LDK (pas une map
   supposée fournie par le plugin).
3. Cliquer **Connecter** et recevoir `OnConnected`.
4. Cliquer **Préparer** et recevoir `OnWorldInitialized`.
5. Observer les tuiles, obstacles et ressources générés.
6. Cliquer **Ready**, puis **Démarrer**.
7. Voir les agents se déplacer à chaque `OnSnapshot`.
8. Mettre en pause, reprendre et arrêter.
9. Vérifier qu'un `Reset` vide l'index d'acteurs et ne conserve pas les
   snapshots de l'ancien `RunId`.
10. Vérifier qu'une fermeture du mock produit `OnDisconnected` et
    `OnError`, sans crash du jeu.

Les différences et limites du simulateur de développement sont détaillées
dans [`../../syne-mock/README.md`](../../syne-mock/README.md). Les détails JSON
émis par le mock sont dans
[`../../syne-mock/docs/CONTRACT_REFERENCE.md`](../../syne-mock/docs/CONTRACT_REFERENCE.md) ;
le contrat public est dans
[`../docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md).
