# REFERENCE_SCENARIOS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `METRICS_SPEC.md`, `EMERGENCE_INDICATORS.md`, `TESTING.md`
**Source Monographie** : §4.3, §4.10.3 (validité des mesures)

---

## 1. Objectif et méthode

Ce document fixe les **cas étalons** d'ECHOS : des situations synthétiques dont
on connaît le contenu, pour vérifier que la mesure publiée **décrit** la
situation — pas seulement qu'elle est déterministe.

Il sert trois usages (`RAPPORT-ANALYSE-ECHOS-LAUNCHER.md` §9, P2 et P4) :

1. **Baselines avant calibration** : aucun seuil n'est ajusté avant d'avoir ce
   que la mesure doit dire sur ces cas ;
2. **Critères d'acceptation interprétables** : chaque assertion répond à une
   question analytique, pas à un nombre ;
3. **Exemples de rapports interprétés** (§3) : ce qu'un rapport doit écrire,
   y compris sur les cas **non mesurés** et sur les résultats
   **contradictoires**.

Exécution :

```console
cd echos && .venv/bin/python -m pytest echos/tests/test_reference_scenarios.py -q
```

Les tests valident le **sens** des résultats : une égalité numérique sans
question analytique attenante n'est pas un critère d'acceptation.

## 2. Les sept scénarios de référence

### 2.1 Aucune communication (zéro observé)

- **Question** : y a-t-il eu communication ?
- **Situation** : 4 entités vivantes, aucun événement `message_sent`, fenêtre
  publiée de 10 ticks.
- **Attendu** : `MessageVolume = 0`, `SenderConcentration = 0`,
  `SenderCoverage = 0`, **et** `measured = true` pour ces mesures.
- **Critère** : fenêtre publiée vide = **zéro observé**, pas « non mesuré ».
  Un rapport écrit « 0 message observé sur la fenêtre », jamais « mesure
  indisponible ».

### 2.2 Diffusion à un seul hub

- **Question** : toute la communication passe-t-elle par un seul émetteur ?
- **Situation** : 4 entités, 10 messages émis par `hub` seul.
- **Attendu** : `SenderConcentration = 1.0`, `SenderCoverage = 0.25` (1 émetteur
  sur 4), `InformationBottleneck` détecté (1.0 > 0.3).
- **Critère** : une concentration réelle déclenche le signal, et la **couverture
  publiée à côté** empêche de lire « 1 émetteur » comme « personne n'émet ».

### 2.3 Diffusion répartie

- **Question** : les entités émettent-elles toutes ?
- **Situation** : 6 entités, 60 messages répartis équitablement sur 10 ticks.
- **Attendu** : `SenderConcentration ≈ 0,1667` (10/60), `SenderCoverage = 1.0`,
  `InformationBottleneck` **non** détecté.
- **Critère** : l'absence de concentration empêche le signal « goulot » — une
  détection ne doit pas dépendre de la diversité mais de la concentration.

### 2.4 Couverture partielle (cas limite du délai)

- **Question** : le délai de couverture est-il lisible sans couverture ?
- **Situation** : 4 entités, messages d'un seul émetteur sur 5 ticks (couverture
  0,4 < 0,8).
- **Attendu** : `EmitterCoverageDelay = 0.0` **avec**
  `SenderCoverage = 0.25`, et `measured = true`.
- **Critère** : le délai seul est ambigu (« 0 » = non atteint ou immédiat ?) ;
  il ne s'affiche **jamais** sans la couverture à côté. C'est le motif de la
  publication de `SenderCoverage` (P1).

### 2.5 Réseau complet / réseau isolé

- **Question** : à quel point le réseau de confiance est-il dense ?
- **Situation A** : 4 entités, chacune se déclare confiance en toutes les autres.
- **Attendu A** : `NetworkDensity = 1.0`, `ClusteringCoefficient = 1.0`,
  `AverageOutDegree = 1.0`.
- **Situation B** : 4 entités, aucune relation.
- **Attendu B** : `NetworkDensity = 0.0`, `NumberOfCommunities = 0`,
  `InferredCommunities = 0`, `CommunityCoverage = 0`.
- **Critère** : **régression P0** — le dénominateur `n(n−1)/2` doit donner 1.0
  sur un réseau complet (l'ancien `n(n−1)` plafonnait à 0,5). Réseau isolé :
  aucune communauté n'est **inférée**, on ne décrète pas « n îles ».

### 2.6 Communauté stable en tailles, changeante en identité

- **Question** : deux communautés de même taille mais de membres différents
  sont-elles « stables » ?
- **Situation** : partition avant `[(a,b), (c,d)]`, après `[(a,c), (b,d)]` ;
  historique de tailles `[2, 2]`.
- **Attendu** : `CommunitySizeMatch = 1.0`, `measured = true`.
- **Critère — résultat contradictoire documenté** : la mesure compare des
  **tailles**, elle répond « stable » alors que les membres ont changé. Le
  rapport doit écrire cette limite explicitement (P1) : une stabilité d'identité
  exigerait que le contrat publie les membres.

### 2.7 Épuisement puis récupération

- **Question** : un cycle d'épuisement est-il revenu à un état sain ?
- **Situation** : réserve d'eau à 5 % (capacité 100), historique 90 → 10 → 5 → 85
  (ticks 1, 2, 3, 6).
- **Attendu** : `RecoveryEpisodes = 1`, `RecoveryTime = 4` (tick 2 → tick 6),
  `UnresolvedCrisisCount = 0`, `CriticalResourceCount = 1`,
  `ResourceFillRatio = 0.05`.
- **Variante censurée** : le run s'arrête pendant la crise (90 → 10 → 4).
- **Attendu variante** : `RecoveryEpisodes = 0`, `UnresolvedCrisisCount = 1`,
  `RecoveryTime = 0` — le temps **n'est jamais publié seul** : une crise ouverte
  n'est pas un retour à zéro.

### 2.8 Routine répétitive vs cycle alterné

- **Question** : la mesure distingue-t-elle répétition, routine et causalité ?
- **Situation A** : même agent `Eat` × 9, `Rest` × 3.
- **Attendu A** : `RepeatedActionPairs = 2`,
  `RepeatedActionCounts = {"Eat": 1, "Rest": 1}`, Σ des comptes == paires.
- **Situation B** : cycle alterné `SeekFood`/`Eat` × 20.
- **Attendu B** : `RepeatedActionPairs = 2` **et aucune sémantique causale
  publiée** (aucune clé « conséquence »).
- **Critère — limite assumée** : l'heuristique mesure des **fréquences**, pas
  des boucles causales. Un cycle alterné réel échappe à la détection *causale*
  parce qu'il n'y en a pas : le rapport dit « répétitions observées », jamais
  « boucle identifiée ».

### 2.9 Aucune décision vs une seule décision

- **Question** : « rien » et « un choix » se distinguent-ils ?
- **Situation** : snapshot sans événement, puis snapshot avec un seul
  `decision_made`.
- **Attendu** : `DecisionCount` 0 → 1 ; `ActionDiversity` 0.0 dans les deux cas
  (l'entropie d'une seule observation est nulle) ; `GoalCoverage = 0.0` côté
  instantané.
- **Critère** : le rapport dit « 1 décision, diversité d'action mesurée à 0 »
  et **ne confond pas** avec « aucune décision mesurée ».

## 3. Exemples de rapports interprétés

Trois modèles de sortie — le ton attendu : faits, dénominateurs, statut,
puis limite. Ni euphémisme, ni conclusion causale.

### 3.1 Cas étalon mesuré — « hub unique »

> **Propagation de l'information — fenêtre 10 ticks, 4 entités vivantes.**
> 10 messages observés, tous émis par la même entité : concentration des
> émetteurs **1,000** (fraction), couverture **0,25** (1 émetteur sur 4 vivants).
> Mesures mesurées (`measured = true`), fenêtre publiée complète, aucune lacune
> de tick.
> **Signal** : `InformationBottleneck` détecté **selon la règle**
> `SenderConcentration > 0,3` (valeur 1,000). Seuil hérité, non calibré.
> **À ne pas conclure** : la concentration porte sur les **émissions** ; rien
> n'indique ici qui reçoit, ni si l'émetteur est un hub structurel.

### 3.2 Cas non mesuré — « fenêtre absente »

> **Boucles de rétroaction — fenêtre publiée : aucune décision observée.**
> `RepeatedActionPairs`, `RepeatedActionShare`, `ActionDistributionBalance` et
> `AmplifiedRepetitions` : **non mesurées** (repli neutre du moteur,
> `measured = false`), faute de décisions dans la fenêtre. Leur valeur numérique
> est ignorée : « non mesuré » n'est pas « 0 observé ».
> **Conséquence pour le composite** : `EmergenceScore` repose sur
> `RepeatedActionShare` → provenance du composite **non mesurée**, score non
> publié comme mesure. Les autres contributions restent lisibles
> (`Contribution*`), chacune avec sa propre provenance.
> **À ne pas conclure** : l'absence de répétition n'est pas observée ; elle est
> **inconnue**. Une fenêtre avec des décisions mais zéro répétition produirait,
> elle, un vrai `0` mesuré.

### 3.3 Résultat contradictoire — « stable en tailles, pas en identité »

> **Complexité sociale — communauté.**
> `CommunitySizeMatch = 1,000` (fraction), mesuré : les tailles du partition
> précédent `[2, 2]` sont présentes dans le partition courant.
> **Constat contradictoire** : les **membres** ont changé (`a·b, c·d` →
> `a·c, b·d`). La mesure porte sur les tailles ; elle ne prouve aucune continuité
> d'identité.
> **Lecture retenue** : « tailles stables, composition modifiée — l'identité des
> communautés n'est pas mesurée par ce contrat. »
> Une mesure de stabilité d'identité exigerait que le contrat SYNE publie les
> membres : lacune ouverte (`METRICS_SPEC.md` §4, `RAPPORT-ANALYSE-ECHOS-LAUNCHER.md`
> §9 P1).

Autres formes obligatoires de prudence :

- **Crise censurée** : `UnresolvedCrisisCount = 1` + `RecoveryTime = 0` se
  rapportent comme « crise encore ouverte au dernier tick, durée de retour non
  observée » — jamais « récupération immédiate ».
- **Score composite** : `EmergenceScore` s'accompagne de ses `Contribution*`,
  du statut `exploratoire` et du `Disclaimer` (texte invariant, affiché tel quel).
- **Comparaison multi-runs** : un écart entre runs est publié avec son contexte
  (version, graine, conservation) et sa dispersion ; ce n'est pas un effet.

## 4. Référence de score composite

- **Main levée** : six composantes à 0,5 → `EmergenceScore = 0,5` et
  `SystemComplexity = 0,5` (les poids somment à 1,0).
- **Golden file** (`tests/golden/analysis_golden.json`) :
  `EmergenceScore = 0,7791446071170001` sur `tests/fixtures/snapshot_analysis.json`.
- **Invariants testés** : Σ `Contribution*` == `EmergenceScore` (1e-12) ;
  entropies brutes n'influent jamais le score ; score ∈ [0, 1] ;
  `SystemComplexity` borné et non croissant avec la durée du run.
- **Détection fixture** : seul `InformationBottleneck` — les autres règles
  restent sous leurs seuils (absence de détection ≠ absence de phénomène).

## 5. Ce que ces cas ne valident pas

- **Aucun seuil n'est calibré** : 20 %/80 % (ressources), 0,3 (concentration),
  0,7 (convergence), 5 (répétitions), horizon 100 ticks — valeurs héritées.
  Leur calibration exige des campagnes multi-runs (survivantes, éteintes,
  interrompues) avec mesure de faux positifs/négatifs et horizon documenté.
- **Aucune sensibilité** aux tailles de population, durées de run et fréquences
  d'échantillonnage n'est mesurée : les cas ci-dessus sont à taille fixe.
- **Aucune variabilité multi-runs** n'est établie : comparer deux runs d'un cas
  étalon reste à faire avant de publier une valeur comme « référence ».
- **Aucune projection de survie** n'est évaluée : si un tel indice est retenu,
  il devra être validé sur des seeds **exclus de la calibration**.

---

## Points restés ouverts dans ce document

- Développer les scénarios de sensibilité (taille de population, durée,
  fréquence d'échantillonnage) et les comparer à plusieurs graines.
- Ajouter les cas « efficacité des actions », « réception des messages »,
  « cycles démographiques », « groupes natifs » et « territoires » dès que le
  contrat SYNE publie les données correspondantes.
- Publier, une fois les campagnes lancées, les faux positifs/négatifs des seuils
  de risque et l'horizon de détection utilisés.
