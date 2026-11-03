# RENDERING_SPEC — Visualiseur Silk.NET, identité visuelle et interaction

**Composant** : PRISM
**Statut** : [IMPLÉMENTATION EN COURS — PRISM-038/110 (SOCLES)]
**Dernière mise à jour** : 1er novembre 2026
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
| Atlas | Atlas de textures **généré par programme** au démarrage (palette sobre, pas de ressources externes lourdes) ; une tuile par type de matériau, une couleur de repli |
| Lumière | Éclairage directionnel + occlusion ambiante par sommet ; **cycle jour/nuit** synchronisé sur le calendrier du monde ; la nuit se lit dans l'image (pas seulement sur une horloge) |
| Culling | Frustum culling par région ; distance de rendu paramétrable |
| Agents | Silhouettes simples (boîtes 1 × 1 × 2) colorées par état ; interpolation visuelle entre ticks **uniquement dans le rendu** ; marqueur de l'action en cours |
| Chantiers | Blocs planifiés rendus en **fantômes** (plan visible), blocs posés progressivement |
| Eau | Surface semi-transparente statique (pas de simulation d'écoulement) |
| Interface | Interface en mode immédiat : HUD (tick, jour/saison, vitesse), inspecteur |

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

## 8. Performance et robustesse

Le rendu ne verrouille jamais le tick (double tampon de chunks) ; si le flux est plus rapide que le rendu, le visualiseur **saute** des deltas intermédiaires en conservant la cohérence (ré-instantané). Cible : `PERFORMANCE.md` §6.

## 9. Tests

Tests de meshing déterministes (même chunk → même maillage), tests d'interface par captures de référence sous rendu logiciel (Mesa) en CI optionnelle, test d'indépendance (run identique avec/sans visualiseur).

### Implémentation v0

`Prism.Viewer` génère un monde de débogage déterministe et ouvre une fenêtre OpenGL 3.3. Les faces coplanaires de même matériau/zone sont fusionnées gloutonnement ; les façades exposées sont également regroupées. `IncrementalTerrainMesher` met en cache les régions, traite un budget de régions par frame et invalide les régions voisines lorsqu'une modification traverse une frontière. Les surcharges solides les plus hautes (dont l'eau statique) déterminent la surface visible. Les caméras orbite et libre se sélectionnent par `O` et `F` : en orbite, `WASD` translate la cible et `Q/E` zoome ; en mode libre, `WASD` déplace la caméra horizontalement et `Q/E` la déplace verticalement. Les flèches contrôlent le regard/la rotation autour de la cible et `Échap` ferme la fenêtre. La seed et la taille sont configurables avec `--seed` et `--size` (`16` à `512`) ; `--help` affiche l'aide.

Les matériaux utilisent une palette de couleurs intégrée et un grain procédural discret calculé dans le shader, sans texture ni ressource externe. Le viewer affiche également trois silhouettes d'agents de démonstration déterministes, construites depuis des snapshots PRISM : la couleur distingue un inventaire vide d'un inventaire chargé, et le chargement est matérialisé par un petit bloc de portage. Elles sont désactivables avec `--no-demo-agents` et ne représentent pas un flux live. `AgentVisualMeshBuilder` accepte les mêmes snapshots et convertit leur position fixe (milli-cellules) en coordonnées du monde.

Un HUD immédiat en géométrie 2D affiche la seed, les dimensions, le nombre d'agents et le mode de caméra, avec les raccourcis et un avertissement explicite de preview statique. Il n'affiche pas de tick ni de calendrier tant qu'aucun host n'est connecté. `Tab` parcourt les agents de démonstration et expose dans l'inspecteur leur identifiant, position et inventaire (quantité et nombre de piles). `C` centre la caméra sur l'agent sélectionné ; `Tab` change également la cible tant que le suivi est actif. Ces agents étant statiques, le suivi est lui aussi une preview et attend la connexion live pour suivre un mouvement. La géométrie HUD suit la taille du viewport et reste indépendante de la caméra. Le raccourci `V` active un premier calque de zones (plaines, forêt, roche, rivage), signalé dans le HUD ; il recolore les surfaces sans modifier le monde et reconstruit les régions dans le budget de meshing par image. Une occlusion ambiante topologique assombrit les sommets près d'un relief plus élevé. Le culling de frustum écarte les régions entièrement hors du volume caméra avant leur draw call.

Le cycle jour/nuit est calculé par `ViewerDayNightCycle` à partir du tick et du calendrier (`--day-length`, `--season-length`), sans dépendre du rendu : élévation solaire, azimut, fraction de journée, jour/saison et intensité lumineuse sont déterministes. Le shader reçoit une teinte lumineuse (`uLightTint`) et la couleur du ciel suit l'état solaire, de sorte que la nuit se lit dans l'image ; le HUD affiche le tick, le jour, la saison, l'heure et le pourcentage de lumière. En aperçu statique, l'horloge avance à vitesse configurable (`--ticks-per-second`, `0` pour figer, `--tick` pour démarrer plus loin) ; le branchement sur les ticks d'un host viendra avec le mode client.

`ViewerEntityStore` centralise la lecture des entités : snapshots complets (`entity_snapshot`), mises à jour par observation, objets au sol, position précédente/courante pour l'interpolation visuelle, et action active déduite des événements `macro_step_completed` / `action_completed` / `action_failed`. Il porte aussi les sept calques d'inspection (`ViewerLayer`) et sert de base au suivi caméra et à l'inspecteur live. Le branchement au flux WebSocket et l'affichage des calques dans la fenêtre restent à faire.

Les autres calques, l'atlas de textures, les modes client/rejeu, les chantiers, l'inspection des blocs/structures et les interventions restent à faire. Le suivi caméra n'est pas encore connecté à un flux d'entités. Le test d'indépendance du rendu, B8 et la validation graphique par plateforme sont différés jusqu'à l'implémentation complète du composant.

L'inspecteur courant est limité aux snapshots statiques de démonstration ; `Tab` affiche l'ID, la position et l'inventaire de l'agent sélectionné. Les actions, maîtrises, chantiers et données cognitives ne sont pas simulés ni affichés.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
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
