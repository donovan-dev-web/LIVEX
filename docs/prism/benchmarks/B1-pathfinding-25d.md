# B1 — Pathfinding 2.5D (prototype initial)

**Statut** : Mesure initiale ; dans le poste indicatif révisé, tick complet à confirmer  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Décision liée** : ADR-011 ; optimisation hiérarchique prévue par PRISM-053

## Protocole

- Carte synthétique de 256 × 256 colonnes, découpée en 256 régions de 16 × 16.
- Relief entier entre 24 et 27, généré par interpolation entière déterministe sur un treillis de 64 colonnes.
- Obstacles synthétiques déterministes (environ une colonne sur 37).
- Paires départ/cible pseudo-aléatoires déterministes ; toutes les routes sont recalculées, sans cache.
- A* à 8 voisins, coûts entiers, montée maximale de 1 et descente maximale de 3.
- 32 recherches d'échauffement par mode, puis trois processus Release distincts ; temps représentés par la médiane.
- Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Routes | Mode | Médiane | Échecs | Cellules développées |
| :-- | :-- | --: | --: | --: |
| 100 | Séquentiel | 153,53 ms | 0 | 167 202 |
| 100 | Parallèle, degré maximal 12 | 11,61 ms | 0 | 167 202 |
| 1 000 | Séquentiel | 469,05 ms | 0 | 1 848 865 |
| 1 000 | Parallèle, degré maximal 12 | 81,23 ms | 0 | 1 848 865 |

Échantillons des trois processus : séquentiel 100 agents 175,84 / 153,53 / 139,18 ms ; parallèle 40,85 / 11,61 / 10,20 ms ; séquentiel 1 000 agents 660,69 / 441,63 / 469,05 ms ; parallèle 81,23 / 76,78 / 81,93 ms. La forte dispersion du premier échantillon parallèle à 100 agents est conservée et reflète la variabilité de la machine partagée.

Le terrain comportait 123 613 arêtes cardinales entre colonnes libres ; ses 247 226 transitions dirigées étaient franchissables selon les limites de pente. Aucune transition de ces arêtes n'a été rejetée.

## Conclusion

Le mode parallèle respecte le budget global de départ de 50 ms pour 100 agents et, à 1 000 routes, le poste indicatif révisé à 90 ms. Cette réallocation suit les mesures B1–B4 et augmente le budget total de 200 à 250 ms (+25 %), avec une marge de 10 ms. La mesure ne couvre que la recherche : les autres phases du tick complet doivent encore être ajoutées, et G1 n'est donc pas franchie.

Le mode parallèle réduit le temps mesuré sans modifier le nombre de cellules développées. Les requêtes sont indépendantes et ne modifient pas le monde ; cette mesure ne valide pas encore le parallélisme du tick complet.

## Limites et suite

Ce prototype ne reconstruit pas les listes de cellules du chemin, ne mesure pas les allocations ni la mémoire du cache et n'implémente pas le cache de chemins. Le terrain et les demandes ne représentent pas encore une simulation de production. Les timings sont indicatifs d'une machine de développement partagée. PRISM-053 (recherche hiérarchique par portails) et le cache restent à évaluer avant de figer le budget B1.

Les premières exécutions avaient des échecs dus à un débordement `int` dans l'interpolation fixe du bruit. Le produit intermédiaire est désormais calculé en `long` ; les résultats ci-dessus sont postérieurs à ce correctif.

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
