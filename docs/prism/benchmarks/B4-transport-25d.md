# B4 — Lockstep WebSocket JSON et liaison embarquée (prototype initial)

**Statut** : Payload complet synthétique mesuré ; modèle différentiel contractuel à mesurer  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Transport** : [`ADR-012`](../adr/ADR-012-transport.md)

## Protocole

- 100 ticks lockstep, chacun avec 1 000 commandes déterministes `Move`, soit 100 000 commandes.
- Même requête `TickRequest` et même traitement des commandes via `ProcessTick` dans les modes réseau et embarqué.
- La réponse comprend les 1 000 perceptions B2 complètes (proche, moyen, état personnel), les positions des 1 000 agents et 1 000 événements `action_completed` par tick. Les perceptions complètes servent de borne haute synthétique ; ce ne sont pas les différences prévues par le contrat.
- Mode embarqué : appel direct à `ProcessTick` avec les DTO préconstruits, sans sérialisation ni copie réseau.
- Mode réseau : `HttpListener` et `ClientWebSocket` sur loopback ; sérialisation JSON camelCase UTF-8 côté client, réception et désérialisation côté hôte, traitement des 1 000 commandes, sérialisation d'une réponse puis réception/désérialisation côté client.
- Chaque tick est validé par un checksum déterministe. Dix échanges d'échauffement précèdent les 100 ticks mesurés. Latence round-trip par tick ; percentiles calculés sur les 100 échantillons.
- Résultats rapportés : médiane de trois processus Release distincts. Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Mesure | Médiane | Échantillons |
| :-- | --: | :-- |
| 100 locksteps embarqués | 63,88 ms | 54,79 ; 69,13 ; 63,88 ms |
| 100 locksteps WebSocket JSON loopback | 6 970,44 ms | 6 910,76 ; 6 970,44 ; 7 136,94 ms |
| Commandes/s, liaison embarquée | 1 565 499 | 1 825 184 ; 1 446 554 ; 1 565 499 |
| Commandes/s, WebSocket | 14 346 | 14 470 ; 14 346 ; 14 012 |
| Latence médiane (p50) par tick | 69,44 ms | 69,20 ; 69,44 ; 71,21 ms |
| Latence p95 par tick | 93,67 ms | 93,67 ; 92,93 ; 96,12 ms |
| Payload de requêtes + réponses | 347 922 680 octets | identique sur les 3 exécutions |

Le p95 loopback dépasse le poste transport indicatif de 40 ms/tick. Cette mesure est volontairement une borne haute : elle renvoie des snapshots complets répétés et 1 000 événements par tick ; elle ne respecte pas le transport différentiel spécifié et ne constitue donc pas une mesure représentative du trafic normal. L'appel embarqué est environ 109 fois plus rapide en temps total sur les 100 ticks, mais il évite volontairement sérialisation, transport et parsing.

## Limites et suite

Les perceptions ne sont pas différentielles et le prototype ne modélise pas les changements réellement survenus entre deux ticks ; il répète le même snapshot à chaque réponse. Les événements sont uniformément des succès `action_completed`. Le test n'inclut ni authentification de session, contention de clients, bruit réseau, compression, allocations/mémoire ou traitement du vrai monde. Le loopback mesure la pile WebSocket sur une seule machine, pas un aller-retour interprocessus sur un réseau représentatif.

Ce résultat valide le chemin technique WebSocket et montre que le snapshot complet dépasse le budget de transport ; il ne justifie pas à lui seul un codec binaire. Implémenter et mesurer les différences réelles, le plafond `maxObservedPerAgent`, les événements réalistes et les allocations avant de conclure sur le budget ou de clore PRISM-014/G1.

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
