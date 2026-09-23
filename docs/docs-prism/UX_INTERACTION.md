# UX_INTERACTION.md

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `VISUALIZATION_SPEC.md`
**Source Monographie** : §5.11, §5.12, §5.13

---

## 1. Caméra et interaction

| Action | Contrôle |
| :-- | :-- |
| Rotation | Clic droit + glisser (orbit) |
| Zoom | Molette (sensible 0.1, borné 0.5 – 5.0) |
| Déplacement | ZQSD / WASD |
| Sélection | Clic gauche |
| Suivre l'entité | Touche F |
| Ne plus suivre | Échap |
| Recentrer | Espace (centre monde, zoom 1.0) |

## 2. HUD

Le HUD affiche en superposition :

- Tick courant.
- Nombre d'entités vivantes.
- Score d'émergence (format F2).
- Diversité des croyances (format F2).
- Liste des phénomènes détectés.
- État du WebSocket (connecté / reconnexion).
- Temps simulé (jours/heures).
- FPS.

**Contrôles de simulation** accessibles depuis le HUD :
- Start / Pause / Resume / Reset.
- Onglets : Paramètres (URL WS, run id, seed), Commandes & légende, Liste des entités, Journal (décès, ressources épuisées).

## 3. L'interface d'analyse (intégrée à ECHOS — héritage du prototype)

PRISM n'est pas la seule interface. L'application d'analyse, héritée du prototype web et **intégrée à ECHOS** en V0.1, fournit un tableau de bord complémentaire. Composants hérités :

| Composant | Rôle |
| :-- | :-- |
| `DashboardPage` | Vue d'ensemble : métriques en temps réel |
| `KPICards` | 4 cartes : entités actifs, score émergence, groupes, messages/tick |
| `MetricsPanel` | Jauges : diversité des croyances, diversité des objectifs, coefficient de clustering, vitesse de diffusion |
| `TimelineChart` | Évolution des métriques dans le temps |
| `AgentInspector` | Inspection d'entité (sondage toutes les 500 ms) |
| `SocialGraph` | Graphe social D3 (sondage toutes les 2 s) |
| `GroupExplorer` | Liste/détail des groupes |
| `MessageHeatmap` | Matrice entité×entité des communications |
| `SimulationControls` | Contrôles play/pause/step |
| `SpeedControl` | Contrôle de la vitesse |
| `RecordingPanel` | Panneau d'enregistrement des runs |

## 4. Technologie de l'interface

- **Vite + React 18 + TypeScript**.
- **Recharts** pour les graphiques.
- **D3.js** pour la visualisation de graphes.
- **Tailwind CSS** (thème sombre : `bg-gray-900`).

---

## Points restés ouverts dans ce document
- L'assemblage exact PRISM (Godot) ↔ interface web ECHOS (React/Vite servie par FastAPI) en V0.1 (fenêtres séparées, partage de données via API ECHOS :5000) sera précisé à l'implémentation.