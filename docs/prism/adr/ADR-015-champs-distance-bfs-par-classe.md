# ADR-015 : Champs de distance BFS par classe de cible, en complément de l'A\*

**Composant** : PRISM (noyau, navigation)
**Statut** : [Proposed]
**Dernière mise à jour** : 14 novembre 2026
**Dépend de** : ADR-013 (convention spatiale : le bloc est l'unité de monde), ADR-007 (déterminisme entier)
**Tranche** : PRISM-143 ; complète PRISM-104 (recherche bornée et mutualisée)
**Porte** : implémentation après G5, et après fermeture de PRISM-104

---

## Contexte

La navigation de PRISM est aujourd'hui **un chemin par agent** : à la réception d'un `Aller`,
`AgentLifecycleSystem` lance un A\* cellulaire sur `MovementGraph` (surface 2.5D, 256 × 256
colonnes), avec un cache à validité par ensemble de régions lues (`RegionReadSet`, PRISM-104).
`RegionGraph` et `HierarchicalPathfinder` existent (PRISM-053) mais ne sont **pas câblés** : le
corridor de régions peut donner un chemin plus long que l'optimum, et la bascule attend une
mesure.

La mesure du 14 novembre 2026 (graine 7, pack `minimal`, production
`stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace`, relevé `PRISM_BENCHMARK_JSON` de l'hôte,
`prism/build/revert/host.jsonl` et `prism/build/adr/host.jsonl`) donne les chiffres suivants. Les
deux scénarios portent les empreintes couplées de référence, donc les trajectoires sont celles de
la campagne.

| Grandeur | 100 agents × 25 ticks (`fbd59443744b0dbd`) | 1 000 agents × 8 ticks (`27f05beab5435150`) |
| :-- | --: | --: |
| Recherches de chemin | 558 (21,0 par tick) | 1 941 (243 par tick) |
| Recherches par agent et par tick | 0,223 | 0,243 |
| Échecs (`failed`) | **474, soit 85 %** | **1 363, soit 70 %** |
| Nœuds développés (cumul) | 2 361 086 | 3 183 111 |
| Nœuds par tick (médiane) | 119 786 | 74 682 (ticks 1 à 6) |
| Nœuds par recherche (moyenne) | 4 231 | 1 640 |
| Sources de cache (`cacheHits`) | 43 | 40 |
| Invalidation de cache | 0 | 0 |
| Nœuds par recherche, du premier au dernier tick | 400 → 30 000 | 200 → 7 000 |

Trois faits en sortent, et deux contredisent l'intuition du ticket.

1. **Le cache ne mutualise presque rien.** 43 sources pour 558 recherches, 40 pour 1 941 : chaque
   agent vise *sa* case, choisie par SYNE, pas une classe. Deux agents qui cherchent « de la
   pierre » demandent deux recherches distinctes. Mutualiser suppose donc de changer **ce que
   l'agent vise**, ce qui est un changement d'architecture, pas une optimisation de cache.
2. **Le coût par recherche monte avec la marche** (400 → 30 000 nœuds) : plus les agents
   s'éloignent, plus l'A\* développe de nœuds pour conclure. Les 85 % et 70 % d'échecs sont des
   recherches qui **épuisent le graphe** — le cas le plus cher.
3. **Le volume suit la population** : 0,223 puis 0,243 recherche par agent et par tick, dans deux
   scénarios de tailles très différentes. Ce coût est donc linéaire en agents, sans terme constant
   qui le dilue.

La géométrie rend l'alternative calculable simplement : monde 256 × 256, régions de 16 × 16
(256 cases), soit 256 régions ; cinq classes de cible dans le pack `minimal` (pierre `stone`,
bois `wood`, eau `drinkable`, culture `crop`, minerai `ore`). Un **BFS multi-sources par niveaux**
depuis toutes les cases d'une classe donne, en entiers, la distance de chaque case à la ressource
la plus proche de cette classe : un champ par région et par classe répond à tous les agents qui
visent la classe, à coût constant par agent.

## Décision

**Hybride retenu**, et non le remplacement de l'A\* :

- **Champs de distance BFS par classe**, calculés par région et par classe, servent les besoins
  collectifs (« aller vers la ressource la plus proche de telle classe ») : un BFS par niveaux,
  en entiers, multi-sources, déterministe par construction (aucun atome, aucun ordre dépendant du
  timing). Le champ est invalidé par le compteur de révision de région déjà en place
  (`WorldGrid`), et recalculé à la demande pour les seules classes effectivement demandées.
- **L'A\* reste la voie des cibles exactes** : station, conteneur, coordonnée nommée, tout objectif
  qui désigne une case et non une classe. `HierarchicalPathfinder` reste le candidat pour ces
  recherches, sous réserve de la mesure B1 déjà prévue.
- **Le contrat ne change pas** : SYNE continue d'émettre `Aller`. C'est l'hôte qui résout une
  cible de classe par champ et une cible exacte par A\*. Un client qui veut une case précise la
  demande comme aujourd'hui.
- **Implémentation après G5**, comme le ticket l'exige, et après fermeture de PRISM-104 : la
  mesure de la voie classique doit être complète avant de changer d'architecture.

## Ce que la mesure dit du gain — et ce qu'elle ne dit pas

Un champ complet coûte **256 régions × 256 cases × 5 classes = 327 680 visites entières**, soit
640 Kio en `ushort` pour les cinq classes du pack. C'est **du même ordre, et jusqu'à 2,7 fois
plus**, que les 119 786 nœuds qu'un tick d'A\* développe déjà à 100 agents. **Le champ n'est donc
pas moins cher sur son propre coût de calcul**, et le recalculer intégralement à chaque tick
serait une régression. Trois choses le justifient néanmoins, et elles sont mesurées :

1. **Il supprime les échecs rejoués.** 70 à 85 % des recherches échouent, et un échec d'A\* est une
   recherche qui épuise le graphe. Un champ répond « inatteignable » à coût constant, une fois par
   région et par classe, au lieu d'une fois par agent et par tick.
2. **Son coût est indépendant du nombre d'agents.** L'A\* suit la population (0,24 recherche par
   agent et par tick, mesuré aux deux tailles) : à 5 000 agents il doublerait encore, le champ non.
3. **Il est la seule voie portable sur GPU sans casser le déterminisme** : un BFS par niveaux se
   calcule depuis le niveau précédent, sans atome ni ordre d'écriture dépendant du timing, et reste
   entier. Un A\* parallèle départage ses ex æquo selon l'ordonnancement (PRISM-140).

Il ne dit **pas** que le gain est acquis : la comparaison à 1 000 agents reste à faire (étape 3 du
ticket), et le seuil d'acceptation est fixé d'avance — **phase `ApplyWrites` ≤ 90 ms à 1 000 agents**
(budget déjà inscrit dans `PERFORMANCE.md` §2), sans échec rejoué.

## Conséquences

### Positives

- Le besoin collectif (« de la pierre la plus proche ») cesse d'être payé par agent.
- Les 70 à 85 % de recherches qui échouent disparaissent du chemin chaud.
- Le champ est calculable en entiers, sans flottants, sans atomes : compatible avec la garde de
  déterminisme de `Prism.Core` (`WorldArchitectureTests.CoreSourcesPassDeterminismGuard`).
- Une base unique pour une éventuelle accélération GPU, sans changer le modèle de monde.

### Négatives

- **La trajectoire change.** Les empreintes de campagne et de R1 seront à re-épingler, avec
  justification datée dans `ISSUES.md` §Mises à jour, selon la procédure PRISM-107. C'est
  exactement le cas que cette procédure réserve aux changements de sémantique assumés.
- Deux mécanismes de navigation à maintenir, et une règle de choix à documenter côté hôte.
- Le champ doit être invalidé finement (par région et par classe) : une invalidation trop large
  refait le coût complet, une invalidation trop étroite donne une distance fausse.
- La mémoire croît avec les classes × les régions (640 Kio pour 5 classes ici, mais proportionnelle
  au monde).

### Risques

- **Faux « inatteignable »** si l'invalidation rate une écriture : un agent conclurait qu'une
  ressource n'existe plus. Mitigation : `PRISM_VERIFY_CACHES=1` recalcule le champ sans mémo et
  lève à la première divergence, comme il le fait déjà pour la perception.
- **Changement de comportement non désiré** : les agents iraient vers la ressource la plus proche
  au lieu de la case choisie par SYNE, ce qui peut modifier l'exploration observée dans les
  résultats scientifiques. D'où la porte G5 et la comparaison chiffrée avant bascule.

## Alternatives écartées

| Voie | Pourquoi non |
| :-- | :-- |
| **Plafond de nœuds par recherche** (essayé le 14 novembre 2026) | Mesuré puis **rejeté** : à 100 agents le tick tombe de 348 à 150,8 ms (−57 %), mais à 1 000 agents il remonte à 649,8 ms avec 17,8 % d'erreur-type (étendue 56 %), et l'empreinte n'est stable qu'à 100 agents. Cause : un échec borné n'est pas une absence prouvée, donc il ne se mémorise pas comme elle et le client repaie la recherche (1 940 recherches sur 8 ticks). Détail dans `PERFORMANCE.md` §2 ter et `ISSUES.md` PRISM-104 |
| **Remplacement complet de l'A\* par des champs** | Perd les cibles exactes (station, conteneur, coordonnée), qui sont une partie réelle du contrat `Aller`. Un champ ne répond pas à « va à cette case » |
| **Corridor hiérarchique seul** (PRISM-053) | Déjà implémenté, non câblé : peut rendre un chemin **plus long** que l'optimum, donc change aussi la trajectoire, avec un gain non démontré et sans supprimer les échecs rejoués — il ne répond pas au fait mesuré |
| **Mutualiser le cache de chemins par classe** | Ne marche pas : la clé d'un chemin est une paire de cases. Mesuré : 43 et 40 sources pour 558 et 1 941 recherches. Il faudrait changer la cible, ce qui est précisément cette décision |
| **Paralléliser l'A\* par agent** (PRISM-140) | Autorisé mais insuffisant ici : il répartit un travail dont 70 à 85 % est perdu, et le départage des ex æquo doit être figé pour rester déterministe. Complémentaire, pas alternatif |

## Validation / rejet

- **Étape 1 (faite, cet ADR)** : décision documentée, chiffres à l'appui.
- **Étape 2** : BFS multi-sources par niveaux, entiers, par région et par classe, avec repli A\* pour
  les cibles exactes. Tests : égalité du champ avec un BFS de référence sur carte ouverte, murs,
  régions déconnectées ; déterminisme à deux exécutions ; invalidation par révision de région ;
  silence de `PRISM_VERIFY_CACHES=1`.
- **Étape 3** : mesure à 1 000 agents contre la baseline de PRISM-106, comparée aux résultats de
  PRISM-104 ; **critère : `ApplyWrites` ≤ 90 ms**, aucun échec rejoué.
- **Étape 4** : écart de trajectoire documenté, re-épinglage explicite et justifié si le champ est
  retenu ; aucune suite affaiblie (PRISM et SYNE vertes).
- **Réouverture** : si le critère de l'étape 3 n'est pas tenu, l'implémentation est retirée et la
  décision repasse en `[Superseded]` avec la mesure qui l'a contredite.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 14 novembre 2026 | Création ; **hybride retenu**, implémentation gatée sur G5 | PRISM-143 : la mesure de la voie classique (70 à 85 % d'échecs, coût linéaire en agents) désigne la cible de classe, pas le cache de chemins |
