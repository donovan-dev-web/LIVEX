# ADR-008 : Contrat SYNE↔PRISM — quatre familles, macro-actions, transport

**Composant** : PRISM
**Statut** : [Proposed]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : ADR-004, `../../syne/API_CONTRACTS.md`, `../../launcher/INTEGRATION_CONTRACT.md`
**Source Monographie** : §2.4 (contrats de transport)

---

## Contexte

Aujourd'hui SYNE publie (WebSocket 5180) et PRISM consomme, avec un contrôle HTTP (5181). En V2 le flux devient bidirectionnel et chaque tick échange des commandes, des observations et des événements pour jusqu'à 1 000 agents. La question du transport (« décision n°28 ») ne doit pas bloquer la spécification.

## Décision

**Le contrat est défini indépendamment du transport**, en quatre familles : *commandes* (SYNE→PRISM), *observations* et *événements* (PRISM→SYNE), *flux de lecture* (PRISM→ECHOS et visualiseur). Un **ordre de tick fixe** en pas de verrouillage (*lockstep*) : SYNE est l'horloge maîtresse.

Choix de liaison :

| Liaison | Usage | Format |
| :-- | :-- | :-- |
| **Plan de contrôle** : HTTP (comme `component.json` l'exige) | Santé, préparation, démarrage, pause, arrêt, reprise | JSON |
| **Plan de données SYNE↔PRISM** : WebSocket, une requête `tick` et une réponse par tick | Commandes → observations/événements | JSON au départ ; codec binaire compact derrière le *même schéma* si le benchmark B4 le justifie |
| **Hôte embarqué** : appel direct d'une interface `IWorldHost` dans le même processus | Campagnes de runs, benchmarks | Mêmes objets de contrat, sans réseau |
| **Flux de lecture** : WebSocket (snapshots de chunks, deltas, métriques) | ECHOS, visualiseur | JSON/binaire de chunks |

Autres règles : versionnement de protocole dans chaque message, commandes idempotentes, identifiants stables pendant un run, échec toujours motivé, macro-actions plutôt qu'ordres par bloc (détail : `../API_CONTRACTS.md`, `../AGENT_INTERFACE.md`).

## Conséquences

### Positives
- La spécification avance sans le transport ; on bascule de liaison sans toucher aux sémantiques.
- Le mode embarqué rend les campagnes aussi rapides que la machine le permet.

### Négatives
- Un pas de verrouillage par tick expose la latence réseau ; le mode embarqué la supprime.
- Deux liaisons à tester (réseau, embarqué) avec les mêmes tests de contrat.

### Risques
- Volume des observations à 1 000 agents (B2/B4). Mitigation : différences par tick, filtre d'intérêt, plafond par agent.

## Alternatives considérées

- **gRPC** : refusé, nouvelle dépendance et chaîne d'outils sans gain mesuré.
- **Tout en processus unique** : refusé comme seul mode, car l'indépendance des composants et l'orchestration par le Launcher sont des acquis.
- **SYNE asynchrone de PRISM** : refusé, le déterminisme exige un ordre causal fixe.

## Validation / rejet

Validé au jalon G3 (contrat gelé, tests de contrat sur les deux liaisons, run de bots reproductible). La liaison binaire n'est adoptée que sur preuve de B4.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
