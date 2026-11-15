# RENDERING_SPEC — Visualiseur Silk.NET, identité visuelle et interaction

**Composant** : PRISM
**Statut** : [IMPLÉMENTÉ — PRISM-110 À PRISM-118 LIVRÉS (PHASE P11), VALIDATION GRAPHIQUE WINDOWS/LINUX DIFFÉRÉE]
**Dernière mise à jour** : 15 novembre 2026
**Dépend de** : [`ARCHITECTURE.md`](ARCHITECTURE.md), [`API_CONTRACTS.md`](API_CONTRACTS.md) §6, [`adr/ADR-005-stack-dotnet-silknet-headless.md`](adr/ADR-005-stack-dotnet-silknet-headless.md)
**Source Monographie** : —

---

## 1. Rôle et frontières

Le visualiseur est un **client de lecture**. Il ne simule rien, ne décide de rien et peut être arrêté sans conséquence pour le run. Son seul droit d'écriture est l'envoi d'**interventions** journalisées (§6), désactivées par défaut.

## 2. Rendu

| Sujet | Spécification |
| :-- | :-- |
| API graphique | OpenGL 3.3 *core* minimum via Silk.NET (fenêtre GLFW) |
| Meshing | Un maillage par région horizontale ; **surfaces et façades visibles uniquement** ; fusion gloutonne des faces coplanaires de même type ; remaillage incrémental des régions *dirty* ; file de remaillage bornée par image |
| Arbres, minerais, blocs posés | Chaque surcharge solide non aqueuse est un **volume** (six faces, culling par face et par bord du monde), quel que soit son niveau : un minerai enterré reste invisible sous la surface et réapparaît dans la vue en coupe |
| Atlas | Atlas de textures **généré par programme** au démarrage (palette sobre, pas de ressources externes lourdes) ; une tuile par type de matériau, une couleur de repli |
| Lumière | Éclairage directionnel + occlusion ambiante par sommet ; **cycle jour/nuit** synchronisé sur le calendrier du monde ; la nuit se lit dans l'image (pas seulement sur une horloge) |
| Culling | Frustum culling par région ; distance de rendu paramétrable |
| Agents | Silhouettes simples (boîtes 1 × 1 × 2) colorées par état ; interpolation visuelle entre ticks **uniquement dans le rendu** ; marqueur de l'action en cours |
| Chantiers | Blocs planifiés rendus en **fantômes** (plan visible), blocs posés progressivement |
| Eau | Surface semi-transparente statique (pas de simulation d'écoulement) ; seule matière qui relève la surface visible de la colonne |
| Capture | Capture PNG autonome de la fenêtre (`--screenshot`, `F12`), encodage sans dépendance externe |
| Interface | Interface en mode immédiat : HUD (tick, jour/saison, vitesse), inspecteur |

Les assets externes (atlas, palettes, gabarits voxel, police, captures de référence) et leurs spécifications de production sont décrits dans [`ASSETS.md`](ASSETS.md). Le chargement est câblé : `--assets <dir>` (défaut `assets/` à côté du binaire, puis `prism/assets/`), décodage PNG interne, et repli procédural pour tout fichier absent ou invalide.

## 3. Identité visuelle

Objectif : « un monde voxel **lisible**, **modifiable** et cohérent avec l'idée d'un monde simulé ».

- **Palette sobre et scientifique** (cohérente avec la charte du site LIVEX) : couleurs désaturées pour le terrain, accents **par signification** (ressources, structures, agents) plutôt que décoratifs.
- **Aucune animation décorative** qui laisserait croire à de la vie là où il n'y a que de l'état : le mouvement vient des agents, du cycle jour/nuit et des chantiers.
- Mode **inspection** : calques de lecture (voir §5).
- Capture d'écran et export de séquences reproductibles (même run, même vue).

## 4. Caméras et navigation

| Mode | Usage |
| :-- | :-- |
| Orbite | Vue d'ensemble, rotation autour d'un point |
| Libre | Exploration |
| Suivi d'un agent | Pointe sur un `agentId` |
| Vue en coupe | Plan de coupe horizontal réglable (lire sous la surface) |

Souris : glisser avec le bouton gauche orbite, glisser avec le bouton droit ou le milieu déplace la carte dans le plan du sol, la molette zoome (mêmes bornes que `Q`/`E`). Clavier : `O` orbite, `F` libre, `WASD` déplace, flèches orientent, `Q`/`E` zooment, `Tab` change d'agent, `C` suit, `X` coupe, `[`/`]` déplacent le plan, `F12` capture l'image.

## 5. Inspection et calques

Clic sur un agent : inventaire, action active, maîtrises, recette en cours (PRISM) ; croyances, désirs, plan (si SYNE publie sa vue cognitive, sinon masqué). Clic sur un bloc : type, état, tags, durabilité d'une structure. Calques : zones, ressources par tag, fraîcheur des stocks, chemins fréquentés (carte de chaleur), structures et accès, propriété, chantiers.

## 6. Interventions (« monde modifiable »)

Désactivées par défaut. Quand elles sont activées par configuration, une intervention (poser/retirer un bloc, déposer un objet) est émise comme **commande journalisée** (`DETERMINISM.md` §7) ; le run est marqué `interventions: true`. Ce n'est pas un mode jeu : c'est un outil d'expérimentation (perturber le monde et observer la réponse).

## 7. Modes d'exécution

| Mode | Description |
| :-- | :-- |
| **Client** | `Prism.Viewer --connect` : se connecte au flux de lecture d'un host |
| **Embarqué** | `Prism.Viewer --embedded` : héberge un host sur un thread dédié (développement, démonstration) |
| **Rejeu** | Relit un run enregistré (deltas) ; contrôle de vitesse |

Les trois modes sont câblés dans `Prism.Viewer` : `--connect <ws://host:port/data/read>` (client), `--embedded [--content <dir>]` (hôte PRISM hébergé dans le viewer) et `--replay <fichier>`. `--record <fichier>` enregistre le flux consommé (une enveloppe JSON par ligne), et `ViewerReplayCursor` rejoue au rythme `--ticks-per-second` (`0` = tout le fichier). `ViewerLiveConnection` borne la file d'attente à 512 enveloppes : au-delà, les deltas les plus anciens sont sautés et la session se resynchronise sur le prochain `world_snapshot`, sans jamais bloquer le flux. En mode client, la grille du viewer vient du `world_snapshot` de l'hôte (`GeneratedWorldSnapshot`), et les `region_delta` sont appliqués à la grille : le viewer ne génère pas un monde qu'il prétend live. Commandes complètes (aperçu, embarqué, hôte + client, ports) : [`MANUAL_RUN.md`](MANUAL_RUN.md).

## 8. Performance et robustesse

Le rendu ne verrouille jamais le tick (double tampon de chunks) ; si le flux est plus rapide que le rendu, le visualiseur **saute** des deltas intermédiaires en conservant la cohérence (ré-instantané). Cible : `PERFORMANCE.md` §6.

## 9. Tests

Tests de meshing déterministes (même chunk → même maillage), tests d'interface par captures de référence sous rendu logiciel (Mesa) en CI optionnelle, test d'indépendance (run identique avec/sans visualiseur).

### Implémentation v0

`Prism.Viewer` génère un monde de débogage déterministe et ouvre une fenêtre OpenGL 3.3. Les faces coplanaires de même matériau/zone sont fusionnées gloutonnement ; les façades exposées sont également regroupées. `IncrementalTerrainMesher` met en cache les régions, traite un budget de régions par frame et invalide les régions voisines lorsqu'une modification traverse une frontière. L'eau statique relève la surface visible de la colonne ; les autres surcharges solides (arbres, minerais, blocs posés) sont des volumes et n'aplatissent plus la colonne qu'elles surplombent. Les caméras orbite et libre se sélectionnent par `O` et `F` : en orbite, `WASD` translate la cible et `Q/E` zoome ; en mode libre, `WASD` déplace la caméra horizontalement et `Q/E` la déplace verticalement. Les flèches contrôlent le regard/la rotation autour de la cible et `Échap` ferme la fenêtre. La seed et la taille sont configurables avec `--seed` et `--size` (`16` à `512`) ; `--help` affiche l'aide.

Les matériaux utilisent une palette de couleurs intégrée et un grain procédural discret calculé dans le shader, sans texture ni ressource externe. Le viewer affiche également trois silhouettes d'agents de démonstration déterministes, construites depuis des snapshots PRISM : la couleur distingue un inventaire vide d'un inventaire chargé, et le chargement est matérialisé par un petit bloc de portage. Elles sont désactivables avec `--no-demo-agents` et ne représentent pas un flux live. `AgentVisualMeshBuilder` accepte les mêmes snapshots et convertit leur position fixe (milli-cellules) en coordonnées du monde.

Un HUD immédiat en géométrie 2D affiche la seed, les dimensions, le nombre d'agents et le mode de caméra, avec les raccourcis et un avertissement explicite de preview statique. Il n'affiche pas de tick ni de calendrier tant qu'aucun host n'est connecté. `Tab` parcourt les agents de démonstration et expose dans l'inspecteur leur identifiant, position et inventaire (quantité et nombre de piles). `C` centre la caméra sur l'agent sélectionné ; `Tab` change également la cible tant que le suivi est actif. Ces agents étant statiques, le suivi est lui aussi une preview et attend la connexion live pour suivre un mouvement. La géométrie HUD suit la taille du viewport et reste indépendante de la caméra. Le raccourci `V` active un premier calque de zones (plaines, forêt, roche, rivage), signalé dans le HUD ; il recolore les surfaces sans modifier le monde et reconstruit les régions dans le budget de meshing par image. Une occlusion ambiante topologique assombrit les sommets près d'un relief plus élevé. Le culling de frustum écarte les régions entièrement hors du volume caméra avant leur draw call.

Le cycle jour/nuit est calculé par `ViewerDayNightCycle` à partir du tick et du calendrier (`--day-length`, `--season-length`), sans dépendre du rendu : élévation solaire, azimut, fraction de journée, jour/saison et intensité lumineuse sont déterministes. Le shader reçoit une teinte lumineuse (`uLightTint`) et la couleur du ciel suit l'état solaire, de sorte que la nuit se lit dans l'image ; le HUD affiche le tick, le jour, la saison, l'heure et le pourcentage de lumière. En aperçu statique, l'horloge avance à vitesse configurable (`--ticks-per-second`, `0` pour figer, `--tick` pour démarrer plus loin) ; le branchement sur les ticks d'un host viendra avec le mode client.

`ViewerEntityStore` centralise la lecture des entités : snapshots complets (`entity_snapshot`), mises à jour par observation, objets au sol, position précédente/courante pour l'interpolation visuelle, et action active déduite des événements `macro_step_completed` / `action_completed` / `action_failed`. Il porte aussi les sept calques d'inspection (`ViewerLayer`) et sert de base au suivi caméra et à l'inspecteur live. Le branchement au flux WebSocket et l'affichage des calques dans la fenêtre restent à faire.

L'atlas, les sept calques, les modes client/embarqué/rejeu et les interventions sont désormais livrés (voir §9).

L'inspecteur suit l'agent sélectionné en direct : `Tab` parcourt les identifiants connus du magasin, le HUD affiche son identifiant, sa position, son inventaire, son action active et l'agent suivi par la caméra. Les maîtrises, les croyances et le plan cognitif ne sont pas publiés par le contrat `1.0.0` : l'inspecteur ne les invente pas et les omet.

### Implémentation P11 (15 novembre 2026)

- **Atlas (PRISM-111)** : `ViewerTextureAtlas` génère au démarrage un atlas RGBA 4 × 4 déterministe (16 px par tuile), une tuile par matériau de la palette plus une tuile de structure et une tuile de repli. Le maillage porte désormais des coordonnées UV (8 flottants par sommet : position, couleur, texture) et le shader mélange la tuile à la couleur de matériau (`uAtlasMix`). Aucune ressource externe n'est chargée.
- **Entités et actions live (PRISM-112)** : les silhouettes sont reconstruites à chaque image depuis `ViewerEntityStore.SampleAgents` avec interpolation visuelle ; la couleur distingue inactif, chargé et en action, et un marqueur au-dessus de la tête porte l'action en cours. Les chantiers sont rendus en fantômes (`ViewerOverlayMeshBuilder.BuildConstructionGhosts`) : volume coloré par état, rempli au prorata de `workCompleted / workRequired`. Les objets au sol sont rendus en cubes et colorés par fraîcheur quand le calque est actif.
- **Suivi et coupe (PRISM-113)** : `C` suit ou relâche l'agent sélectionné, et la cible est mise à jour à chaque image depuis le flux live. `X` active/désactive un plan de coupe horizontal (`uCutZ`, `discard` au-dessus), `[` et `]` le déplacent de 2 blocs.
- **Inspecteur et sept calques (PRISM-114)** : `1` à `7` basculent `ViewerLayer` (`Zones`, `Resources`, `Freshness`, `Paths`, `Structures`, `Ownership`, `Construction`) ; `V` reste l'alias du calque des zones. `Zones` et `Resources` recolorent le terrain, `Freshness` colore les objets au sol, `Paths` affiche la carte de chaleur des cellules occupées (`ViewerMovementHeatmap`), `Construction` affiche les fantômes. Le contrat ne publiant ni propriétaire ni ACL de site, les calques `Structures` et `Ownership` n'ajoutent aucune donnée inventée : ils restent sélectionnables et affichent l'état réellement reçu (sites de construction et objets au sol).
- **Interventions (PRISM-116)** : désactivées par défaut, activées par `--interventions <journal.jsonl>` (journal marqué `"interventions": true`). `P` pose un bloc, `R` retire la surcharge la plus haute de la colonne visée. Chaque intervention est journalisée, convertie en commande du contrat gelé (`Poser`/`Retirer`, aucun kind nouveau), transmise au host par `POST /control/intervention` (`--control`) ou à l'hôte embarqué (`PrismHostRuntime.QueueIntervention`), qui l'applique au tick suivant. Hors run, le host refuse explicitement l'intervention.
- **Indépendance du rendu (PRISM-117)** : `ViewerRenderIndependenceTests` exécute deux runs identiques, l'un avec une session de viewer abonnée au flux, l'autre sans lecteur, et exige la même empreinte de monde et le même état d'entités.
- **Plan de contrôle (15 novembre 2026)** : le viewer ouvre un endpoint HTTP `control` (`--control-port`) avec `GET /health/ready`, `GET /info` et `POST /control/play|pause|stop|quit` (ainsi que `shutdown` et `intervention`). `play` est traduit en `start` ou `resume` selon l'état de l'hôte, les autres commandes sont relayées telles quelles à l'hôte (`--control`), qui reste l'autorité du monde. Le Launcher s'en sert pour les boutons Play/Pause/Stop/Quitter et n'exécute aucun tick avant Play ; une fermeture demandée par HTTP est traitée sur le fil de rendu (pas de segfault GLFW). Le viewer demeure un client en lecture seule pour l'état du monde : il n'écrit jamais le monde et ne décide d'aucune commande de simulation.
- **Manifeste et B8 (PRISM-118)** : `prism/viewer.component.json` déclare `type: viewer`, `contributesTo: [immersion]`, les capacités `renderCadence`/`readOnlyClient`/`replay`, les endpoints `control` et `readStream` consommés et la dépendance `prism-host` ; `publish_prism_v2.py` le copie à côté du binaire. `--benchmark-frames <n> --benchmark-output <fichier>` mesure les temps d'image (moyenne, p50, p95, images/s) et écrit un relevé B8. Aucun relevé B8 ni validation graphique Windows/Linux n'a été exécuté dans cette passe (validation différée à la demande du propriétaire).

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 15 novembre 2026 | Plan de contrôle du viewer (`/health/ready`, `/info`, `POST /control/play\|pause\|stop\|quit`), relayé à l'hôte ; run gaté depuis le Launcher (Hôte/Viewer), aucune image avant Play | Chaîne réelle pilotée par le viewer ; l'hôte reste l'autorité du monde |
| 15 novembre 2026 | Passe de vérification : volumes d'arbres et de minerais, réconciliation des overrides sur delta (un bloc miné disparaît), module de capture PNG, caméra souris, correction de l'uniforme `uCutEnabled` et de l'arrêt du host embarqué | Vérification de la P11 sur une fenêtre réelle |
| 15 novembre 2026 | Atlas procédural et UV de sommet ; entités et actions live, chantiers fantômes, calques, coupe, modes client/embarqué/rejeu, interventions journalisées, test d'indépendance, manifeste viewer et harnais B8 | PRISM-110 à PRISM-118 (P11) |
| 3 novembre 2026 | Socle du mode client (`ViewerReadStreamClient`/`ViewerLiveSession`) et resynchronisation documentés ; support Linux précisé | PRISM-115 |
| 3 novembre 2026 | Magasin d'entités live, interpolation et suivi d'action | PRISM-112 |
| 3 novembre 2026 | Cycle jour/nuit déterministe, teinte lumineuse et HUD calendrier | PRISM-111 |
| 2 novembre 2026 | Occlusion ambiante par sommet aux ruptures de relief | P11 |
| 2 novembre 2026 | Culling de frustum des régions avant rendu | P11 |
| 2 novembre 2026 | Calque des zones, inspecteur agent et HUD adapté au viewport | PRISM-114 |
| 2 novembre 2026 | HUD de preview avec seed, dimensions, agents, caméra et raccourcis | P11 |
| 2 novembre 2026 | Grain procédural des matériaux et silhouettes voxel d'agents dans le viewer v0 | P11 |
| 2 novembre 2026 | Caméras orbite/libre documentées et contrôlées au clavier | PRISM-113 |
| 2 novembre 2026 | Meshing glouton incrémental et budget par frame documentés | PRISM-110 |
| 2 novembre 2026 | Viewer OpenGL v0, maillage par région et caméra clavier ajoutés | PRISM-038 |
| 1er novembre 2026 | Création | Refonte V2 |
