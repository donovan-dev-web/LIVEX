# RENDERING_SPEC — Visualiseur Silk.NET, identité visuelle et interaction

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
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
| Meshing | Un maillage par chunk ; **faces visibles uniquement** ; fusion gloutonne des faces coplanaires de même type ; remaillage incrémental des chunks *dirty* ; file de remaillage bornée par image |
| Atlas | Atlas de textures **généré par programme** au démarrage (palette sobre, pas de ressources externes lourdes) ; une tuile par type de matériau, une couleur de repli |
| Lumière | Éclairage directionnel + occlusion ambiante par sommet ; **cycle jour/nuit** synchronisé sur le calendrier du monde ; la nuit se lit dans l'image (pas seulement sur une horloge) |
| Culling | Frustum culling par chunk ; distance de rendu paramétrable |
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

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
