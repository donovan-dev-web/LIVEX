# B5 — Snapshot et deltas de régions 2.5D (prototype initial)

**Statut** : Mesure initiale ; charge déterministe synthétique  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Transport** : JSON et compression BCL (Deflate/Brotli)

## Protocole

- Monde de 256 × 256 colonnes, en 256 régions de 16 × 16.
- Chaque colonne JSON porte cinq champs `byte` : `surfaceZ`, `materialId`, `flags`, `biomeId`, `strataProfileId`.
- Snapshot complet versionné contenant les 256 régions et 65 536 colonnes.
- Delta synthétique d'un tick contenant les 256 régions et quatre colonnes modifiées par région, soit 1 024 modifications et 1,56 % des colonnes. Les versions passent de 1 à 2.
- Encodage JSON camelCase via `System.Text.Json`. Sérialisation et compression mesurées séparément sur le même payload JSON, dix itérations après échauffement ; médiane de trois processus Release.
- Compression BCL avec `CompressionLevel.Fastest`. La taille affichée est la moyenne des dix sorties.
- Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Charge | JSON | Encodage JSON | Deflate | Compression Deflate | Brotli | Compression Brotli |
| :-- | --: | --: | --: | --: | --: | --: |
| Snapshot complet | 4 794 556 o | 11,74 ms | 307 312 o | 3,72 ms | 158 123 o | 4,50 ms |
| Delta d'un tick | 111 364 o | 0,37 ms | 11 528 o | 0,14 ms | 7 697 o | 0,41 ms |

Échantillons des trois processus : encodage JSON du snapshot 11,74 / 12,73 / 11,30 ms ; Deflate 3,83 / 3,66 / 3,72 ms ; Brotli 4,24 / 4,50 / 4,75 ms. Pour le delta : encodage JSON 0,95 / 0,37 / 0,30 ms ; Deflate 0,19 / 0,12 / 0,14 ms ; Brotli 0,41 / 0,43 / 0,37 ms. Le delta représente 2,32 % de la taille JSON du snapshot complet. Brotli compresse davantage, au prix d'un temps de compression plus élevé dans ces mesures. Le snapshot complet est une opération d'initialisation et ne doit pas être interprété comme un coût par tick.

## Limites et suite

Le terrain utilise de petits identifiants et valeurs répétitifs, favorables à la compression. Le delta force quatre changements de drapeau par région ; il ne représente pas une distribution réelle de *dirty regions*. Le protocole ne mesure ni échange WebSocket, ni framing, ni désérialisation, ni allocations de buffers, ni compatibilité/reprise à partir d'une version de base. Les sorties comprimées ne sont pas encore directement branchées au flux de lecture PRISM.

Le résultat justifie de garder les deltas régionaux et la compression BCL dans le plan de lecture, sans trancher Deflate contre Brotli. Refaire la mesure sur des changements issus du simulateur de production avant le gel G1.

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
