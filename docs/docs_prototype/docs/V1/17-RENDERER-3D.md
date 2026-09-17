# Renderer 3D (Phase 10) — Godot

Renderer 3D indépendant du moteur de simulation. Il s'abonne au WebSocket de
simulation (Phase 4), reconstruit le monde à chaque snapshot et n'**expose jamais
aucune décision** : il ne fait que réfléter l'état du moteur (principe §7 de
`docs/V1/04-ARCHITECTURE.md`). Le contrôle du moteur est un simple relais
utilisateur → API HTTP (port 5181).

## Stack

| Élément | Choix | Motivations / limites |
|---|---|---|
| Moteur | Godot **4.7.2** édition **.NET** (mono) | Forward+, Jolt Physics, driver `d3d12` |
| Langage | **C#** (`Godot.NET.Sdk 4.7.2`, `net8.0` + `RollForward=LatestMajor`) | cohérence avec le mono-repo .NET ; tourne sur le runtime .NET 10 installé |
| Assets | **aucun** — tout est procédural | robuste, zéro pipeline d'import |
| Scène | `main.tscn` unique (`WorldRoot` → SimClient / Agents / Resources / Camera / HUD) | | 

## Fichiers

```text
godot-renderer/
├── project.godot
├── main.tscn               # scène unique
├── godot-renderer.csproj   # Godot.NET.Sdk 4.7.2, net8.0 + RollForward
├── godot-renderer.slnx
├── scripts/
│   ├── SimClient.cs        # transport WS + reconstruction du monde + API de contrôle
│   ├── CameraController.cs # caméra de survol (orbite/zoom/follow)
│   └── Hud.cs              # HUD de debug + contrôle + légende + inspection
└── addons/godot_mcp_toolkit (plugin MCP de l'éditeur, .mcp.json)
```

## Repère monde

La simulation est **2D** : positions `(x, y)` sur un monde 500×500. Le renderer les
projette sur le **plan XZ** de Godot (`x → x`, `y → z`), l'axe Y étant la hauteur.

## Transport & données

- **WebSocket** : `ws://127.0.0.1:5180/` — abonné en permanence, reconnexion auto
  (1,5 s). Deux messages JSON `camelCase` :
  - `snapshot` = `{ kind, runId, tick, simulatedTimeMinutes, aliveCount,
    agents:[{id,health,energy,hunger,thirst,x,y,action}],
    resources:[{id,type,x,y,quantity,capacity}] }`
  - `event` = `{ kind, type, tick, agentId?, action?, targetId?, cause?, value? }`
    (les types écoutés côté renderer : `AgentDied`, `ResourceDepleted` → journal).
- **API de contrôle** (`http://127.0.0.1:5181/api/control/`): `start`, `pause`,
  `resume`, `reset` (corps `{ "seed": int?, "runId": string? }`), interroge `state`
  toutes les 2 s pour afficher l'état moteur dans le HUD.

Contrat complet : `docs/V1/09-EVENTS-API.md` et
`simulation-core/Simulation.Core/Transport/`.

## Rendu

- **Sol** : `PlaneMesh` 500×500 gris (couleur par défaut) + **zones colorées en
  surimpression** (taches semi-transparentes par ressource : verte = nourriture,
  bleue = eau) pour visualiser les zones ; **lumière** directionnelle + environnement
  d'ambiance (pas de Sky, robuste). NB : dans cette version de Godot (4.7), `PlaneMesh`
  génère un plan dans le plan **XZ** (normale +Y) — **aucune rotation n'est appliquée**
  (une `Rotation.x = -90°` le rendrait vertical ; vérifié par AABB).
- **Agents** : capsules (r=1,6 · h=4) avec une couleur par **santé** (vert→rouge, défaut)
  ou par **action** (A / bouton HUD) : Eat/Drink/Rest/Explore/Gather/Talk/Attack/Flee/MoveTo.
  Interpolation de position (lissage vers la cible de snapshot) ; mort animée
  (rétractation vers zéro + `queue_free`, gérée aussi pour les disparitions). Sélection
  par clic 3D **ou liste du HUD** → panneau d'inspection (santé, énergie, faim, soif, action courante).
- **Ressources** : sphères (eau = bleu, nourriture = vert), taille normalisée par
  `quantity/capacity` (+ tache de zone associée sur le sol).

## Caméra (`CameraController`)

- orbite : **clic droit + glisser** ; zoom : **molette** ;
- déplacement : **ZQSD** ; sélection : **clic gauche** ; suivi de l'agent sélectionné :
  **F** (zoom serré progressif sur l'agent) ; annuler le suivi : **Échap** ;
- **A** : bascule santé/action (géré par SimClient).

## HUD (`Hud.cs`)

L'interface est **définie dans `main.tscn`** (contrôles natifs Godot, éditables dans
l'éditeur : `PanelContainer`, `VBox/HBoxContainer`, `TabContainer`, `ItemList`,
`RichTextLabel`…). `Hud.cs` ne fait que **câbler les nœuds et mettre à jour** —
aucun widget n'est construit ni mis en page en code.

- **épinglé**, à gauche (ne couvre pas toute la fenêtre) : titre, état WebSocket,
  tick / temps simulé / vivants / FPS, état moteur (via polling 2 s), boutons
  **Démarrer / Pause / Reprendre / Réinitialiser**, bouton couleur, panneau d'inspection ;
- **onglets** (`TabContainer`) :
  - **Paramètres** : URL WS + bouton « Connecter », run id, seed ;
  - **Commandes & légende** : raccourcis + légende des couleurs par action (BBCode) ;
  - **Agents** : liste cliquable (id · action · santé) pour sélectionner chaque agent
    sans clic 3D ;
  - **Journal** : morts, ressources épuisées.

## Lancer le renderer

Prérequis : Godot **4.7.2 édition .NET**, SDK .NET installé (10), moteur de simulation
allumé (`.\start-all.ps1`, ou manuellement, voir README).

```bash
# 1) Build C# (souvent déjà fait par l'éditeur)
cd godot-renderer
dotnet build

# 2) Ouvrir main.tscn dans Godot et lancer la scène (F5 / playtest)
#    — ou lancer le renderer en cli si un binaire Godot a été construit
#    — ou via start-all.ps1 -Godot (voir ci-dessous)
```

Depuis `start-all.ps1` :

```powershell
.\start-all.ps1 -Godot                 # lance aussi le renderer si trouvé sur le PATH
.\start-all.ps1 -Godot -GodotPath "C:\Games\Godot\Godot_v4.7.2-stable_mono_win64.exe"
```

Dans l'éditeur, scène ouverte, le HUD permet de pointer une autre URL WS et de
commander le moteur. Le renderer démarre côté visualisation même si le moteur est **en
pause** : les boutons du HUD (ou l'API 5181) lancent la run.

## Statut de validation (Phase 10)

Validé en playtest contre le moteur réel (`serve 5180` + contrôle 5181) :

- connexion WebSocket + flux snapshot/event **(connecté, tick/temps/vivants/FPS mis à jour)** ;
- spawn/mouvement des 20 agents et des 15 ressources (interpolation, réapparition) ;
- relais de contrôle complet depuis le HUD : **start** (seed/run id appliqués au moteur),
  **pause** (tick figé), **resume** (tick repart), **reset** (monde reconstruit) ;
- bascule de couleur santé/action ; sélection d'agent + inspection ; suivi caméra (F)
  à distance constante ; journal d'événements.
- **HUD basé scène** (édition directe dans l'éditeur, onglets natifs) validé en
  playtest : boutons (start/couleur), listes agents cliquables + inspection, onglets.
- **Orientation des plans corrigée** : `PlaneMesh` en plan XZ par défaut (vérifié via
  AABB au runtime : sol `(-250,0,-250, size 500,0,500)`, zones `(-23,0,-23, size
  46,0,46)`, rotation 0) — plus aucune rotation −90° X qui rendait les plans verticaux.
- **Erreurs `sim_client.gd` purgées** : fichier supprimé lors de la migration C# ;
  les erreurs de cache de l'éditeur ont été effacées (rafraîchissement forcé).
- Build : `dotnet build` 0 erreur / 0 warning. Capture de référence :
  `docs/` — voir ADR-006 pour les limites V1 (obstacles / taille du monde non diffusés).

## Limites & V2 (hors périmètre V1)

- **Obstacles et taille de monde non diffusés** par le contrat transport (V1) : sol fixe
  500×500, obstacles ignorés. Parler à la V2 (ajouter obstacles/taille au `WorldSnapshot`).
- Sons, animations squelettiques, minimap, sélection par identify-render : hors cible V1.
  Le renderer reste volontairement un *observateur* : toute évolution du contrat transport
  côté moteur sera reflétée sans logique décisionnelle.