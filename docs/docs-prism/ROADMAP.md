# ROADMAP.md

**Composant** : PRISM
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../ROADMAP.md` (racine), `TRANSPORT_API.md`
**Source Monographie** : §5.15 (évolution), §5.14 (limites)

---

## 1. Principes

- Road map **en ordre, sans dates** (décision utilisateur).
- PRISM **consomme les contrats SYNE** (WS 5180 / HTTP 5181) et s'**intègre aux vues ECHOS** (interface définitive intégrée à ECHOS).

## 2. Les phases (ordre)

| # | Intitulé | Contenu |
| :-- | :-- | :-- |
| 0 | Socle Godot .NET | Scène `main.tscn`, `SimClient.cs` (WS 5180, reconnexion 1,5 s), caméra, HUD de base |
| 1 | Rendu du monde | Mapping 2D→3D, sol PlaneMesh, obstacles, ressources (sphères, taille normalisée) |
| 2 | Rendu des entités | Capsules, code couleur santé/action, interpolation, indicateur de cap, animation de mort |
| 3 | Sélection & inspection | Raycast clic, `CircleShape2D`, `BeliefViewer` |
| 4 | Intégration ECHOS | Consommer les vues du tableau de bord (intégrées à ECHOS) en complément de PRISM |
| 5 | Visualisation croyances | Heatmap 50×50, bulles (> 0.8, 2 s) |
| 6 | Visualisation sociale | Graphe de relations, heatmap confiance 256×256, graphe D3 |
| 7 | Visualisation des groupes | Couleurs par groupe (hash), GroupPanel |
| 8 | Communication visuelle | Pulsations lumineuses (éclairs, 0,5 s), file à minuterie |
| 9 | Robuste & perf | Culling > 1000 entités, limites V1 corrigées (taille du monde transmise), minimap |
| 10 | Tests & validation | Stratégie `TESTING.md`, checklist visuelle, tests transport |

## 3. Risques et atténuation

| Risque | Atténuation |
| :-- | :-- |
| Le choix du moteur définitif change | PRISM = framework intermédiaire : seuls les adaptateurs changent (principe invariant). |
| Rendu lourd à haute échelle (1000 capsules) | Culling, LOD, minimap (phases 9+). |
| Dissonance entre interface Electron et rendu Godot | Définition claire du partage via API ECHOS :5000 + contrats partagés. |

## 4. Évolution future (§5.15)

- **Mode joueur-habitant** : l'utilisateur pourra incarner une entité dans le monde simulé.
- Représentation des constructions et territoires.
- Affichage multi-échelle (zoom région → vue globale).
- Intégration de données ECHOS directement dans la scène.
- Effets visuels environnementaux (saisons, météo, jour/nuit).

Le **moteur graphique définitif** (éventuellement Unreal/Unity) sera choisi après comparaison des besoins de PRISM, du pipeline d'assets, des performances et des contraintes de développement.

---

## Points restés ouverts dans ce document
- Aucune date n'est posée.
- Le moteur graphique définitif reste ouvert ([OUVERT], §5.2.3) — pivot potentiel documenté.