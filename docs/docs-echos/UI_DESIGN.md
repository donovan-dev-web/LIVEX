# UI_DESIGN.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `FRONTEND_VISION.md`, `USER_STORIES.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.2.2, §4.7, §5.13 (interface intégrée)

---

## 1. Design system

### 1.1 Palette

| Rôle | Couleur (hex) | Usage |
| :-- | :-- | :-- |
| **Fond principal** | `#0D1117` | arrière-plan des vues (hérité `bg-gray-900` du prototype) |
| **Fond surfaces** | `#161B22` | cartes, panneaux, conteneurs |
| **Bordure** | `#30363D` | séparateurs, contours de cartes |
| **Texte primaire** | `#E6EDF3` | texte courant |
| **Texte secondaire** | `#8B949E` | libellés, sous-titres |
| **Accent** | `#00D4A0` | actions, focus, valeurs positives |
| **Alerte/Danger** | `#F85149` | limites, scores hors bornes, conflits |
| **Info** | `#58A6FF` | indicateurs, liens d'analyse |
| **Success stable** | `#3FB950` | validations, healthy |

> Thème **sombre uniquement** en V0.1 (héritage prototype). Le thème clair est une amélioration P2, **après** validation des contrastes scientifiques (limites §5.14 Monographie).

### 1.2 Typographie

- **Police** : Inter (système) avec fallback `system-ui, -apple-system, Segoe UI`.
- **Échelle** : `12 / 13 / 14 / 16 / 20 / 24 / 30 px` (taille de base 14 px).
- **Chiffres** : `tabular-nums` pour tous les KPI et jauges (pas d'oscillation des largeurs dans la timeline).
- **Monospace** : `JetBrains Mono` (traces, JSON, sortie console).

### 1.3 Grille & espacement

- **Grille responsive** : 12 colonnes ; grandeur d'écartement 4 px (`4 / 8 / 12 / 16 / 24 / 32`).
- **Bandeau d'application** (40 px) : identité du run + horloge tick + controls globaux.
- **Hauteur de ligne de table** : 28 px (lisible, dense).

### 1.4 Composants réutilisables

| Composant | Spécification |
| :-- | :-- |
| `KPICard` | Titre, valeur `tabular-nums`, delta (▲/▼ coloré), hint « score ≠ preuve » au survol |
| `Gauge` | Jauge 180°, borne min/max affichée, zone de limite grisée |
| `TimelineChart` | Série sous-échantillonnée (`?every=N`), axe temps, survol = valeur exacte |
| `EntityBadge` | Pastille couleur groupe + émoticône besoin dominant, survol = sonde |
| `GroupChip` | Étiquette de groupe avec code couleur hash (fidèle §5.9.1) |
| `ConfidenceEdge` | Arête de graphe dont l'opacité suit le niveau de confiance |

---

## 2. Écrans

### 2.1 Écran A — Tableau de bord (`/dashboard`)

- **Haut** : 4 `KPICard` (entités actives, score émergence, groupes, messages/tick).
- **Milieu** : grille de `Gauge` (diversité croyances, diversité objectifs, clustering, vitesse de diffusion) + `TimelineChart` principal.
- **Bas** : liste des phénomènes détectés (lien → Écran D).
- *Sources* : `GET /api/runs/{id}/metrics`, `/api/emergent-phenomena`.

### 2.2 Écran B — Exploration (`/explore`)

- **Onglets** : *Entités* | *Groupes* | *Communications*.
- **Entités** : liste paginée + `AgentInspector` au clic (sondage 500 ms).
- **Groupes** : `GroupList` + `GroupDetail` (membres, rôle, timeline de formation/dissolution).
- **Communications** : `MessageHeatmap` (matrice entité×entité).
- *Sources* : `GET /api/groups`, `/api/beliefs/{agentId}`, `/api/communication-heatmap`.

### 2.3 Écran C — Graphe social (`/social-graph`)

- Vue **force-directed D3** 2D/3D : nœuds = entités, arêtes = relations, couleur/opacité = confiance.
- Sélection d'un nœud → son `AgentInspector`.
- *Sources* : `GET /api/relationships/{agentId}`, flux WebSocket (sondage 2 s).

### 2.4 Écran D — Analyse & causalité (`/analysis`)

- **Vue métriques** : tous les scores et séries (7 moteurs).
- **Vue causale** : graphe des chaînes reconstruites (depuis `decision_traces`), navigation par profondeur.
- **Vue comparaison** : deux runs côte à côte (`?a={run1}&b={run2}`).
- *Sources* : `GET /api/compare`, `GET /api/runs/{id}/export`.

### 2.5 Écran E — Pilotage & calibration (`/control`)

- **SimulationControls** : play / pause / step, vitesse, seed, reset.
- **CalibrationForm** : paramètres éditables → ECHOS relaie à SYNE (`:5181`) — jamais en direct.
- **RecordingPanel** : enregistrer / sauvegarder / charger / exporter un run.
- **Bandeau d'état** : WebSocket (connecté/reconnexion), tick courant, FPS.

### 2.6 Écran F — Journal & limites (`/log`)

- **Console de débogage** (3 niveaux : structuré / traces / texte — `LOGGING_INSTRUMENTATION.md`).
- **Panneau « limites de validité »** : rappels des interdits et des bornes — le **contrepoint** de l'écran D (cf. `LIMITATIONS.md`).
- **Export** : CSV/JSON (bouton, `?every=`).

---

## 3. Flux de navigation

```mermaid
flowchart LR
    A[Tableau de bord] -->|phénomène| D[Analyse & causalité]
    A -->|plonge| B[Exploration]
    B -->|graphe| C[Graphe social]
    B -.->|entité| I[AgentInspector\n(sondage 500 ms)]
    A -->|commande| E[Pilotage & calibration]
    A -->|doute| F[Journal & limites]
    D -->|comparer| G[Comparaison\n? a = run1, b = run2]
    C -.->|confiance| I
```

> Les transitions `-.->` sont des **panneaux latéraux** (pas de navigation complète) : l'inspecteur d'entité s'ouvre **sur** le graphe ou la liste sans quitter le contexte.

## 4. Règles d'interface (rappel contractuel)

1. **Les vues ne calculent pas** : elles affichent ce que l'API REST ECHOS :5000 fournit (message passe à travers, aucune métrique recomputée).
2. **Piloter = relayer** : play/pause/calibration passent par ECHOS → SYNE :5181 ; un contrôle n'est jamais appliqué en direct au moteur.
3. **Limites accessibles** : chaque score expose, à un survol/clic, sa zone de non-validité — l'interface ne masque jamais les mises en garde.

---

## Points restés ouverts dans ce document
- Pixel-perfect vs lint visuel (tests snapshot) : choix à trancher en implémentation (phase 6), doc `TESTING.md` ECHOS.
- Contraste exact des états de graphe D3 à valider avec le rendu PRISM (éclair rouge = Warning, etc. — §5.10 Monographie).
