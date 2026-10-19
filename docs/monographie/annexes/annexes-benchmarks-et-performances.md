# Annexe K. Benchmarks et performances

Cette annexe rassemble les mesures de performance du projet : les chiffres, mais surtout les conditions dans lesquelles ils ont été obtenus. Un débit sans machine, sans protocole et sans charge n'est pas une donnée : c'est une impression.

**Pourquoi cette annexe importe.** La question directrice porte sur ce que le monde produit, mais un monde qui ne tourne pas assez vite ne produit rien d'observable. La performance n'est donc pas un sujet d'ingénierie annexe : c'est une condition de l'observation. Elle est aussi le lieu où le projet a commis ses erreurs les plus instructives.

## 1. La règle de lecture

Trois distinctions structurent tout ce qui suit.

- **Mesure** : obtenue en exécutant le code, sur une machine nommée, selon un protocole publié. Seules ces valeurs engagent le projet.
- **Objectif** : une cible visée, non atteinte par définition. Elle ne s'écrit jamais comme un résultat.
- **Historique** : mesuré sur une version antérieure du code, sur des runs dont on sait aujourd'hui qu'ils étaient mal calibrés. Conservé pour la trajectoire, jamais pour la promesse.

## 2. État de référence de la mesure

**Machine de référence V0.1, figée le 8 octobre 2026** : Intel Core **i7-8750H** (6 cœurs / 12 threads, 2,2 à 4,1 GHz), **14 Gio** de RAM, Ubuntu x86-64, SDK **.NET 10.0.401**, alimentation sur secteur.

Protocole : compilation **Release**, monde **500 × 500**, cellule spatiale **50**, configuration par défaut, **300 ticks**, seeds {12345, 999, 7}, une seule passe mono-thread (le déterminisme l'exige). Deux passes consécutives du 8 octobre 2026 donnent des checksums FNV-1a identiques et des débits à ±3 %.

```bash
dotnet run -c Release --project syne/Simulation.Console -- --benchmark
```

Les ticks et les populations sont ajustables par `--benchmark-ticks` et `--benchmark-populations`.

## 3. Résultats mesurés (version 0.17.0)

| Population | Débit | Tick moyen | Cible (objectif V2) | Verdict |
| --: | --: | --: | :-- | :-- |
| 50 | **de 1 354 à 2 257 t/s** | de 0,44 à 0,73 ms | ≥ 30 t/s | tenue, environ 45× au-dessus |
| 500 | **de 42,8 à 53,9 t/s** | de 18,5 à 23,3 ms | ≥ 20 t/s | tenue, environ 2,1× au-dessus |
| 1 000 | **de 5,9 à 6,5 t/s** | de 153 à 169 ms | ≥ 10 t/s | **non tenue, écart d'environ 1,7×** |

La part de temps passé dans les phases instrumentées est élevée, de l'ordre de **90 à 99 %** selon les cellules et la campagne. Le seuil que le projet s'impose, à savoir qu'au moins 30 % du temps de tick soit attribué aux sous-systèmes instrumentés et non au travail hors scopes, est tenu partout. Aux paliers supérieurs ou égaux à 500, la **perception** domine (0,03 à 0,12 ms par entité-tick), suivie des **événements** (environ 15 ms par tick à 1 000 entités) puis de la **communication** (environ 3 ms par tick) ; l'allocation et le ramasse-miettes n'apparaissent qu'en queue.

## 4. Trois corrections qui changent la lecture

Ces corrections ont plus de valeur que les chiffres eux-mêmes, parce qu'elles montrent comment une métrique peut mentir.

**Des runs où la population mourait.** Des mesures publiées au jalon antérieur (1 187 t/s à 500, 505 t/s à 1 000) avaient été obtenues sur des runs dont la population s'éteignait en cours de route par défaut de calibration : les derniers ticks, presque vides, gonflaient le débit. Depuis la décision ADR-016, les populations survivent sans mortalité, et les chiffres du tableau 3 sont **les premières mesures honnêtes à charge complète**.

**Une part de computation mal calculée.** `TickBudgetSnapshot.ComputationShare()` sommait des temps **par échantillon** alors que les phases intra-entité (perception, mémoire, besoins, actions) sont échantillonnées par entité et par tick, tandis que communication et événements le sont une fois par tick. La part affichée revenait donc au ratio (communication + événements) par tick, sous-estimant les phases intra-entité d'un facteur proche de la population. Depuis la correction (calibration B1, `engineVersion` 0.17.0), la part est le temps **par tick** de chaque phase divisé par le temps de tick : les valeurs observées deviennent 90 à 98 %, le complément étant le travail hors scopes (tri de l'ordre causal, allocations, ramasse-miettes).

**Une instrumentation à prouver neutre.** Object pooling, buffer de tri de perception, collecteur de budget : tout a été vérifié non altérant. Le checksum doré `0x27fad50065d8c4a4` est inchangé, et trois tests s'en portent garants : `ScaleChecksum_IsBitForBitReproducible`, `BudgetCollection_DoesNotAlterTrajectory`, et l'égalité de trajectoire entre runs instrumentés et non instrumentés.

## 5. Contrôle du déterminisme de la performance

`--benchmark` publie un **checksum FNV-1a canonique** de l'état : un préfixe `population=N;ticks=T`, puis une ligne `id;x;y;énergie` par entité, triée par identifiant. À seed égale, ce checksum est reproductible bit à bit. C'est la preuve que la mesure ne mesure pas du bruit : deux exécutions du même scénario donnent le même état, donc le même chemin de calcul.

## 6. Planchers anti-régression en intégration continue

La CI n'asserte **pas** les objectifs du tableau 3 : elle ne tourne pas sur la machine de référence. Elle asserte des **planchers** environ sept fois en dessous des mesures réelles :

| Population | Plancher CI | Mesure réelle |
| --: | --: | --: |
| 50 | ≥ 120 t/s | de 1 354 à 2 257 t/s |
| 500 | ≥ 30 t/s | de 42,8 à 53,9 t/s |
| 1 000 | ≥ 20 t/s | de 5,9 à 6,5 t/s |

La mesure retenue est la meilleure de cinq essais, sur plusieurs fenêtres de charge. Une régression d'ordre de grandeur (retour à une perception naïve quadratique, boucle cassée) casse le test ; une variation machine ne le casse pas. Les deux assemblées de tests de SYNE sont lancées en **deux étapes séquentielles** dans la CI : exécutées en parallèle, elles se partagent les processeurs du runner et le plancher de 500 entités tombait sous le seuil sans qu'aucune régression n'existe.

Un second garde-fou porte sur la primitive de voisinage : `QueryCircle` de rayon 50, monde 1 000 × 1 000 (cellule 50, 1 000 entités réparties uniformément), 50 requêtes, doit rester sous **10 ms par requête**, une marge très large choisie pour rester déterministe en CI tout en bloquant toute régression d'ordre quadratique. Le balayage est borné : une requête ne visite que la fenêtre de 3 × 3 cellules qui l'intersecte, jamais la population entière.

## 7. Référence historique et cibles

**Prototype (mesures de 2026, monde 500 × 500, seeds 12345, 999, 7)** : 20 entités sur 2 000 ticks, environ 1 900 à 3 200 ticks par seconde, 30 à 127 Mo ; 100 entités sur 1 000 ticks, environ 315 à 400 ticks par seconde, 125 à 325 Mo ; 1 000 entités sur 150 ticks, environ 4,5 à 5,4 ticks par seconde, 354 à 1 241 Mo. Le goulot identifié était la perception quadratique. L'introduction de la grille spatiale a porté le palier de 1 000 entités d'environ 4,6 à environ 6,3 ticks par seconde (+37 %) et celui de 2 000 d'environ 0,6 à environ 0,9 (+50 %), pour une mémoire ramenée à 39 à 42 Mo.

**Budget de tick visé** (1 000 entités, 100 ms) : perception 20 ms, mémoire et croyances 15 ms, besoins et objectifs 10 ms, utilité 20 ms, communication 15 ms, actions 15 ms, événements 5 ms.

**Objectifs V2** : 50 entités, ≥ 30 ticks par seconde, ≤ 15 Mo, couverture ≥ 80 % ; 500 entités, ≥ 20 t/s, ≤ 40 Mo ; 1 000 entités, ≥ 10 t/s, ≤ 80 Mo. C'est à cette grille que le tableau 3 se compare, et c'est le palier de 1 000 qui reste ouvert.

## 8. Le coût de l'observabilité

Observer n'est pas gratuit, et le projet mesure ce qu'il en coûte. Sur un même monde, la simulation seule tient dans environ **50 Mo**, tandis que la pile d'observation complète atteint environ **250 à 280 Mo**, répartis entre l'API (95 à 115 Mo), l'ingestion (environ 80 Mo) et le serveur de simulation observé (75 à 85 Mo). Ces chiffres sont **indicatifs** : ils dépendent du matériel, du runtime et de la charge.

Le protocole compare deux scénarios exécutant **exactement le même monde** (mêmes entités, mêmes ticks, même seed), de sorte que tout écart de débit soit imputable à l'observabilité et non au scénario. Deux leviers permettent d'isoler les causes : `--analysis-every` (planifier l'analyse un tick sur N, ce qui filtre le coût de l'analyse) et `--ticks-per-second` (qui borne la cadence). Attention à une confusion fréquente : la colonne de ratio « coût ECHOS » mêle l'atténuation due à la cadence et le surcoût réel ; **comparer les débits** est plus sûr.

## 9. Ce que ces chiffres ne permettent pas de conclure

- Ils ne valent que pour **une machine** et un runtime nommés. Aucun résultat n'a été reproduit sur une seconde plateforme : la performance est aujourd'hui validée sur une configuration, pas sur une classe de configurations.
- Ils ne disent **rien** sur le comportement : un débit élevé ne prouve pas l'émergence, et un débit faible ne l'infirme pas.
- La cible de 1 000 entités reste **non tenue** (écart d'environ 1,7×). Ce n'est pas un blocage pour l'état décrit, dont les scénarios tournent à 100 agents ou moins, mais c'est un chantier identifié et non un acquis.
- Les chiffres de mémoire dépendent de la gestion mémoire du runtime ; ils ne s'interprètent pas comme une empreinte minimale absolue.
