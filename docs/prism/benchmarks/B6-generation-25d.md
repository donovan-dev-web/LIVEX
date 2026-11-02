# B6 — Génération déterministe par régions (prototype initial)

**Statut** : Mesure initiale ; parallélisme régional confirmé sur le prototype  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Dimensions** : 256 × 256 colonnes, 256 régions de 16 × 16

## Protocole

- Génération déterministe des altitudes de surface, obstacles et caractéristiques sur 65 536 colonnes.
- La même fonction `GenerateRegion` écrit des régions disjointes dans les trois tableaux de sortie.
- Mode séquentiel : parcours des 256 régions dans l'ordre croissant.
- Mode parallèle : `Parallel.For` sur les identifiants de régions avec un degré maximal de parallélisme égal à `Environment.ProcessorCount` (12).
- Appel de génération et ordonnanceur parallèle échauffés avant la mesure ; allocations des tableaux exclues du chronométrage, coût de dispatch inclus dans le temps parallèle.
- Pour chaque lancement, trois exécutions parallèles sont comparées bit à bit au résultat séquentiel ; toute divergence interrompt le benchmark.
- Résultats représentés par les médianes de trois processus Release distincts. Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Mode | Temps médian | Échantillons |
| :-- | --: | :-- |
| Séquentiel | 3,43 ms | 3,60 ; 3,43 ; 3,42 ms |
| Parallèle, degré maximal 12 | 0,94 ms | 0,97 ; 0,93 ; 0,94 ms |

L'accélération observée est d'environ **3,7×**. Les tableaux `surfaceZ`, `blocked` et `features` sont identiques entre les modes pour les trois exécutions.

## Conclusion

La génération régionale indépendante peut être parallélisée de manière déterministe et réduit le temps mesuré sur cette machine. Le résultat confirme le modèle d'exécution prévu par région ; il ne justifie pas à lui seul un budget de génération du monde complet.

## Limites et suite

La charge ne génère ni strates réelles, filons, eau, arbres, sites d'apparition, contenu, empreinte du monde ni structures. La distribution de travail est parfaitement uniforme et ne comporte pas de régions plus coûteuses. L'égalité vérifiée porte uniquement sur les trois tableaux du prototype, sans preuve Windows/Linux. Le parallélisme de la génération complète et ses contrôles de jouabilité relèvent de B6 sur code de production (PRISM-039).

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
