# B3 — Croissance programmée ou aléatoire (prototype initial)

**Statut** : Mesure initiale ; résultat aligné sur Q-09  
**Date** : 2 novembre 2026  
**Référence** : `Prism.Benchmarks`, exécutions Release  
**Décision liée** : Q-09, [`SIMULATION_LOOP.md`](../SIMULATION_LOOP.md) §3

## Protocole

- Monde de 256 × 256 colonnes, avec une culture de stade 0 dans chacune (65 536 cultures).
- Simulation de 500 ticks, répétée trois fois par processus ; médiane sur trois processus Release distincts.
- Variante programmée : un callback au terme de chaque stade, avec une durée de 40 ticks et une gigue entière déterministe de -5 à +5 tirée une fois par culture. Chaque callback avance le stade 0→1→2→3 et programme le suivant. L'initialisation du premier échéancier est hors chronométrage ; le traitement des callbacks et la programmation des suivants sont chronométrés.
- Variante aléatoire : trois cellules tirées par région et par tick via `Mix64(tick, regionId, draw)`. Chaque tirage fait avancer la culture sélectionnée d'un stade, jusqu'au stade 3.
- Les deux variantes partent de cultures au stade 0 ; calcul des résultats et empreinte d'état vérifiés identiques à chaque répétition d'une même variante.
- Exécution séquentielle. Machine : Intel Core i7-8750H, 6 cœurs / 12 processeurs logiques ; Windows, .NET SDK 10.0.403.

## Résultats

| Variante | Temps médian / tick | Travail compté | Transitions | Cultures mûres après 500 ticks |
| :-- | --: | --: | --: | --: |
| Programmée | 0,002 ms | 196 608 callbacks | 196 608 | 65 536 |
| Aléatoire | 0,016 ms | 384 000 tirages | 190 743 | 61 143 |

Les temps par tick sont les médianes de trois mesures de 500 ticks. Les trois échantillons de processus ont donné 0,002 / 0,002 / 0,002 ms par tick pour la variante programmée, et 0,016 / 0,015 / 0,016 ms par tick pour la variante aléatoire.

## Conclusion

Les callbacks programmés sont plus rapides dans ce microbenchmark et mûrissent toutes les cultures pendant la fenêtre mesurée. La variante aléatoire est environ 8 fois plus lente par tick et en laisse davantage avant le stade mûr à 500 ticks. Les variantes n'ont donc pas le même résultat : ce test compare un mécanisme d'évolution déclenché à échéance à un mécanisme de tentatives périodiques, et non deux implémentations fonctionnellement équivalentes.

La recommandation Q-09 documentée — mises à jour programmées pour les cultures et aléatoires pour les phénomènes diffus — reste cohérente avec cette mesure et avec la sémantique voulue. Aucun changement de décision n'est requis.

## Limites et suite

La simulation n'applique ni sol, eau, saison, récolte, flétrissement, conditions de croissance, événements ou autres règles du monde. Les cultures sont uniformément mûrissantes ; le générateur d'échéances initiales et la création du monde sont hors mesure. Les mises à jour aléatoires sont séquentielles ; le déterminisme du traitement parallèle par région reste à valider. Ce prototype ne remplace pas PRISM-046, B3 sur le code de production.

Reproduction depuis `prism/` :

```powershell
dotnet run --project Prism.Benchmarks --configuration Release
```
