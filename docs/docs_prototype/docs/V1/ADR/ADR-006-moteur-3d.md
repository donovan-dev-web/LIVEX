# ADR-006 — Renderer 3D (moteur Godot)

## Statut

Accepté pour la Phase 10.

## Contexte

Le moteur de simulation est volontairement découplé de toute représentation graphique
(ADR-001) : il expose son état via le Transport Layer (WebSocket, Phase 4). La Phase 10
demande un renderer 3D pour visualiser les agents et les ressources en temps réel, sans
lui donner le pouvoir de décision.

## Décision

Utiliser **Godot 4.7 (édition .NET) en langage C#**, en scène unique procédurale :

- une scène `main.tscn` + trois scripts C# (`SimClient.cs`, `CameraController.cs`, `Hud.cs`)
  dans `godot-renderer/`, avec `godot-renderer.csproj` (`Godot.NET.Sdk 4.7.2`, `net8.0` +
  `RollForward=LatestMajor` pour tourner sur le runtime .NET 10 installé) ;
- **aucun asset 3D** : tout est construit à la volée (sol via `PlaneMesh`, agents via
  `CapsuleMesh`, ressources via `SphereMesh`, matériaux `StandardMaterial3D`) ;
- le renderer **s'abonne au WebSocket de simulation et ne décide rien** : il applique
  strictement les snapshots (`kind=snapshot`) et les événements (`kind=event`), et expose
  l'API de contrôle du moteur (port 5181) sans logique métier (principe §7 de
  `docs/V1/04-ARCHITECTURE.md`) ;
- la caméra est indépendante (orbite/zoom/follow) et gérée par un script dédié.

Le choix **C# plutôt que GDScript** pérennise le mono-repo 100 % .NET (mêmes conventions,
typage fort, build/debug via `dotnet`), au prix d'un cercle build-éditeur à respecter
(modification → `dotnet build` → relance de la scène).

## Alternatives

- GDScript : plus léger à prototyper, mais casse la cohérence C#/.NET du dépôt et
  n'apporte pas de typage fort au transport.
- Godot 4.x en mode « non .NET » : édition incompatible avec le build C# du projet.
- Moteur web (Three.js) : déjà couvert par la Web UI 2D (Phase 6) ; le renderer 3D
  enrichit la visualisation, pas la duplication.
- Unreal/Unity : surdimensionné pour une fenêtre de visualisation/survol.

## Conséquence

- Le renderer ne requiert aucun service de plus : le WebSocket (5180) et l'API de contrôle
  (5181) existent déjà ; aucun contrat transport n'a été modifié pour la V1.
- Infobésité : pas d'assets, de plugin ou de pipeline d'import à maintenir.
- **Limite V1** : le contrat transport ne diffuse ni les obstacles ni la taille du monde
  (hors périmètre V1, voir décision utilisateur) → le renderer affiche un sol fixe 500×500
  et ignore les obstacles. A espérer en V2 si la visualisation doit refléter le terrain.
- Lancement : documentation + switch optionnel `-Godot` / `-GodotPath` dans `start-all.ps1`
  (pas de lancement automatique par défaut).