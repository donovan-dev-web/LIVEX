# B2 — Perception 2.5D (prototype initial)

**Statut** : Mesure initiale ; dans les postes révisés, payload contractuel incomplet  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Contrat** : [`API_CONTRACTS.md`](../API_CONTRACTS.md) §5

## Protocole

- Monde synthétique de 256 × 256 colonnes, avec 256 régions de 16 × 16 et un masque de tag déterministe par colonne.
- 1 000 positions d'agents déterministes, calculées avant la mesure.
- Niveau proche : détail des colonnes portant un tag dans le carré de rayon 16.
- Niveau moyen : masque de tags non nul des régions dans un carré de rayon 3 régions, borné par la carte.
- Niveau lointain : aucun détail.
- Le DTO comprend le niveau de protocole, le tick, l'identité et la position de l'agent, puis les trois niveaux. Les positions des 1 000 agents sont mesurées dans un message distinct.
- Chronométrage séparé de la construction en mémoire et de l'encodage UTF-8 JSON camelCase via `System.Text.Json`. Le sérialiseur est échauffé avant la mesure ; 3 processus Release distincts, résultats rapportés en médiane.
- Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Mesure | Médiane | Échantillons |
| :-- | --: | :-- |
| Construction des 1 000 perceptions | 32,70 ms | 32,70 ; 34,54 ; 31,50 ms |
| Encodage JSON des 1 000 perceptions | 34,78 ms | 34,78 ; 34,62 ; 39,70 ms |
| Taille moyenne d'une perception | 3 235 octets | identique sur les 3 exécutions |
| Taille maximale d'une perception | 4 542 octets | identique sur les 3 exécutions |
| Positions complètes des 1 000 agents | 31 050 octets/tick | identique sur les 3 exécutions |

Le jeu synthétique produit 35 581 détails proches et 39 312 résumés régionaux au total. L'encodage JSON seul dépasse la première hypothèse de 20 ms pour sérialisation/échange/flux, mais reste dans le poste révisé de 40 ms ; le transport réseau n'est pas inclus.

## Limites et suite

Les masques représentent un seul type de caractéristique synthétique. L'état personnel (inventaire, outil, maîtrise et statuts) est minimal et synthétique ; le prototype ne modélise ni visibilité/occlusion, catégories d'objets réelles, structures, observation différentielle, ni `maxObservedPerAgent`. Le calcul des masques régionaux est pré-calculé hors de la mesure ; il sera invalidé/recalculé selon les régions *dirty* dans l'architecture réelle. La mesure d'encodage exclut l'allocation de buffers réseau et l'échange WebSocket. Les tailles ne constituent donc pas encore un budget contractuel de production.

Les prochains essais doivent appliquer le plafond et les deltas prévus au contrat, mesurer les allocations et inclure l'enveloppe lockstep complète. Le résultat ne justifie pas encore un changement de transport ou de codec.

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
