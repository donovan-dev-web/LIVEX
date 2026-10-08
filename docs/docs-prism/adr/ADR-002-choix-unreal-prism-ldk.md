# ADR-002 : Unreal Engine et plugin PRISM-LDK pour PRISM

**Composant** : PRISM
**Statut** : [Accepted]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : [`ADR-001-choix-godot.md`](ADR-001-choix-godot.md) (supersédé), `../../adr/ADR-003-api-http-rest.md`, `../../adr/ADR-004-websocket-temps-reel.md`
**Source Monographie** : §5.2.3 (choix du moteur), §2.4.2 (indépendance du rendu), §5.15 (évolution)

---

## Contexte

L'ADR-001 retenait **Godot 4.7.2 édition .NET** pour un prototype de visualisation, et laissait explicitement ouvert le choix du moteur graphique définitif. L'ADR-001 était toutefois calibrée pour un prototype de quelques scènes, et son analyse « Unity/Unreal surdimensionnés » portait sur ce périmètre, pas sur celui de LIVEX.

SYNE et ECHOS étant désormais opérationnels (jalons U0 → U8 livrés), PRISM doit représenter un monde simulé complet : grille d'agents, ressources, constructions, territoires, saisons, réseaux sociaux, groupes et communication par pulsations. Cela déplace la question : il ne s'agit plus de visualiser un prototype mais de porter l'expérience interactive du projet final, avec une interface de diagnostic, une inspection d'entité et des vues de beliefs et de relations.

Trois exigences contraignent ce choix :

1. **Indépendance du rendu** (Monographie §2.4.2) — SYNE ne doit dépendre d'aucun moteur graphique. Le moteur choisi ne peut donc pas devenir un propriétaire de l'état simulé.
2. **Contrats versionnés** — PRISM consomme `world_initialized`, le snapshot global par tick, les deltas et les événements en JSON sur WebSocket `5180`, et pilote le cycle de contrôle HTTP `5181`. Ces contrats sont la source de vérité, pas le moteur de rendu.
3. **Budget de rendu** — le monde peut compter jusqu'à ~1000 entités et la restitution doit rester fluide à cette échelle, avec un coût de mise à jour stable par tick.

Le dépôt ne disposait d'aucune décision d'architecture formelle pour ce choix : l'ADR-001 avait été marquée « historique » sans qu'un ADR successeur ne prenne le relais.

## Décision

**PRISM est le projet Unreal final de LIVEX. Il intègre le plugin Unreal PRISM-LDK** (*LIVEX Development Kit*, nom de module Unreal `PrismLdk`).

Les éléments structurants de la décision :

- **Moteur** : Unreal Engine 5.8 pour le projet PRISM. `prism/LDK/LDK.uproject` est un **hôte technique** de développement, de compilation et de test du plugin ; il ne constitue pas un second produit ni le projet complet LIVEX.
- **Plugin `PrismLdk`** : Runtime, phase de chargement `Default`, exposé à Blueprint. Il est la seule couche C++ du dépôt côté client.
- **Frontière du plugin** : `PrismLdk` se limite au transport (WebSocket `5180`, HTTP `5181`), au parsing JSON, à la conversion en types Blueprint et à la diffusion d'événements. **SYNE reste le seul moteur décisionnel et l'autorité de l'état simulé.** Le plugin ne décide pas du comportement des entités et ne devient pas un moteur de simulation parallèle.
- **Exposition Blueprint** : types, fonctions et événements (`OnWorldInitialized`, `OnSnapshot`, `OnWorldDelta`, `OnSyneEvent`, `OnControlResult`, `OnError`) sont exposés à Blueprint afin de privilégier les systèmes natifs d'Unreal ; le C++ reste une couche mince.
- **Absence de dépendance C#** : le projet Unreal ne référence pas les assemblies de `Simulation.Core`. L'échange passe exclusivement par les contrats HTTP/WebSocket.

## Conséquences

### Positives
- Le choix du moteur définitif est tranché et l'ADR-001 peut être marquée supersédée sans trou de gouvernance.
- Un socle de code exécutable existe (`prism/LDK/Plugins/PrismLdk/`), ce qui rend l'intégration vérifiable plutôt que purement déclarative.
- Le pipeline d'assets Unreal et Blueprint couvre les besoins de présentation (matériaux, HUD, widgets, graphes) que le prototype ne visait pas.
- La frontière d'intégration est testable indépendamment du moteur de rendu.

### Négatives
- Le build C++ dépend d'une chaîne d'outils Unreal (Visual Studio, SDK Windows) et n'est pas reproductible dans la CI actuelle — aucun runner Unreal n'est présumé (`CI_CD.md`).
- Le coût de compilation d'Unreal est élevé : le cycle d'itération est plus lent que celui du prototype Godot.
- Les assets `.uasset` / `.umap` sont des binaires merges, sans diff lisible et sensibles à la conversion de fin de ligne — d'où les règles de `.gitattributes`.

### Risques
- **Le plugin dérive vers un moteur de rendu ou de décision** si la couche C++ s'épaissit. Mitigation : frontière documentée dans `ARCHITECTURE.md` §4, code C++ restreint à l'interopérabilité et aux types exposés.
- **La validation par le mock est prise pour une preuve** : `syne-mock/` simplifie les décisions, le pathfinding et les systèmes sociaux. Mitigation : documentée comme non équivalente à SYNE ; les vérifications décisionnelles et trajectorielles se font contre SYNE réel.
- **Un état visuel divergent de l'état simulé** : snapshots et événements décrivent la même mutation. Mitigation : le snapshot est la source de vérité de l'état courant, les événements restent des notifications.

## Alternatives considérées

- **Godot 4.7.2 édition .NET (piste [HÉRITÉ] de l'ADR-001)** : conservé comme prototype ; l'archive `docs/docs_prototype/` a été retirée par la revue documentaire V0.1 (PR #502). Refusé comme moteur définitif — le périmètre de représentation (réseau social, inspection, vues de beliefs et de relations) et le budget de rendu à ~1000 entités dépassent ce que le prototype couvrait.
- **Unity** : refusé. Écosystème comparable à Unreal pour la qualité de rendu, mais le plugin livré ici est écrit contre les API Unreal ; retenoir Unity aurait imposé un second adaptateur sans gain identifié, SYNE et ECHOS étant déjà livrés.
- **Moteur graphique laissé volontairement ouvert (position de l'ADR-001)** : cette non-décision n'est plus tenable. Le projet PRISM est l'implémentation finale attendue de LIVEX ; maintenir le moteur ouvert repousserait indéfiniment l'écriture du code. La décision est désormais réversible : seuls les adaptateurs changent, pas les contrats.
- **Rendu ECHOS uniquement (React/TypeScript) sans PRISM** : refusé. L'interface d'observation d'ECHOS reste un outil d'analyse, pas la représentation interactive du monde.

## Validation / rejet

- **Contrat** : le cycle `Prepare` → `world_initialized` → `Ready` → `Start`, puis la réception des snapshots, deltas et événements, sont validés contre **SYNE réel** — pas contre `syne-mock`.
- **Intégration** : le plugin doit compiler dans l'hôte `prism/LDK/LDK.uproject` **et** dans le projet Unreal PRISM final. Les deux validations sont exigées (`CONTRIBUTING.md` §4).
- **CI** : tant qu'aucun runner Unreal n'est disponible, la compilation du plugin **n'est pas couverte** par GitHub Actions. Ce point reste ouvert et tracé dans `CI_CD.md` §« Points restés ouverts » ; il ne doit pas être présenté comme une validation acquise.
- **Réouverture** : si le budget de rendu à ~1000 entités, ou le pipeline d'assets, ne tiennent pas Unreal en pratique, la décision est réexaminée. Le contrat de transport n'est pas remis en cause : `GITFLOW.md` et `VERSIONING.md` s'appliquent intégralement.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 27 septembre 2026 | Création | Adoption d'Unreal et du plugin PRISM-LDK ; supersède l'ADR-001 |
