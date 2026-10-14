# Chapitre 3b. Concepts et références : ce que nous devons à l'histoire, formules à l'appui

Ce chapitre paie les dettes. Chaque concept central est présenté avec son histoire, sa formule quand elle existe, son usage exact dans LIVEX et sa mise en garde. Le but est que les références éclairent le propos au lieu de le décorer, et que chaque formule soit un engagement vérifiable plutôt qu'un ornement.

Une remarque de méthode ouvre le chapitre. Les formules de ce livre ne sont pas des abstractions : chacune est implémentée quelque part dans le code, avec des valeurs par défaut, et chacune produit des traces que l'observatoire peut mesurer. Une formule qu'on ne pourrait pas vérifier n'aurait pas sa place ici. Inversement, toute constante citée est un paramètre réel, modifiable, dont le changement serait visible dans les trajectoires.

## 3b.1 Émergence

**Histoire.** Des automates cellulaires aux essaims, l'idée que du global puisse naître du local sans plan central traverse toute la pensée des systèmes complexes.

**Formule d'appui.** L'entropie de Shannon mesure la dispersion d'une distribution :

```text
H = -Σ p_i log2 p_i
```

et sa version normalisée, `H / log2 k`, où `k` est le nombre de catégories possibles. Appliquée aux énoncés de croyances ou aux objectifs, l'entropie vaut zéro en cas d'unanimité et croît quand les positions se dispersent. Une population qui tient toutes la même croyance a une entropie nulle ; une population dispersée sur `k` valeurs également probables a une entropie maximale de `log2 k`.

**Usage dans LIVEX.** Quatre niveaux instrumentés (spatial, social, informationnel, abstrait-économique), avec des chaînes causales retraçables et une provenance explicite pour chaque mesure. L'entropie sert notamment à la diversité des croyances et à la diversité des objectifs.

**Mise en garde.** Surprenant, mais analysable. Ni magie, ni conscience. Jamais une loi générale tirée d'une seule scène.

## 3b.2 Systèmes complexes

**Histoire.** Holland, Gell-Mann, la Santa Fe Institute : des résultats qui modifient les conditions qui les ont produits.

**Usage dans LIVEX.** Des entités adaptatives, des groupes revus à intervalle régulier qui modifient les interactions futures, des boucles instrumentées comme des répétitions mesurées (paires répétées, part amplifiée). Le projet ne parle jamais de « boucles causales prouvées » : il compte des répétitions, et le nom employé l'avoue.

**Mise en garde.** La rétroaction est à la fois le cœur du projet et son point critique. C'est précisément pour cela que la mesure doit être sans contact.

## 3b.3 Multi-agents

**Histoire.** *Sugarscape* (1996) : migration, commerce et conflit produits par des règles simples en milieu contraint.

**Usage dans LIVEX.** Huit traits tirés autour d'une valeur neutre, une interaction locale jusqu'à cinquante-cinq unités pour la parole, un environnement à réserves finies.

**Mise en garde.** Les modèles internes faillibles de LIVEX (mémoire exponentielle, croyances bornées) le distinguent de *Sugarscape* : face à la même situation, deux entités peuvent décider différemment.

## 3b.4 Intelligence collective

**Histoire.** Les essaims (1999) : la robustesse sans chef, par le suivi de voisins.

**Usage dans LIVEX.** Non pas le suivi, mais le raisonnement (utilité transparente) croisé aux contraintes (coûts calibrés).

**Mise en garde.** Il n'y a pas d'essaim dans LIVEX. Il y a de la cognition individuelle contrainte. La nuance est importante : elle déplace l'explication du suivi vers la décision.

## 3b.5 BDI et utilité

**Histoire.** Rao et Georgeff (1995) : croyances, désirs, intentions.

**Formules LIVEX.** Plusieurs formules structurent la cognition. Elles sont citées ici dans le langage exact du moteur.

La confiance perceptuelle décroît avec la distance, avec un plancher :

```text
confiance = 1 - (distance / rayon) × 0,3, bornée à un plancher de 0,7
```

La saillance d'un souvenir décroît exponentiellement avec son âge, le taux dépendant de la catégorie :

```text
salience(t) = salience(0) × e^(-taux × délai)
taux : observation 0,01   événement 0,005   interaction 0,002
oubli lorsque la saillance passe sous 0,01 ; capacité bornée à mille souvenirs
```

La révision de croyance applique une pénalité aux concurrentes d'un signal contraire et un bonus à celles qui s'alignent, avec des bornes par pas, une érosion temporelle et une expiration :

```text
conflit : -0,1 (plancher 0,1)   alignement : +0,2 (plafond 1)
érosion : ×0,999 par tick       expiration : 100 ticks (plafond 0,4)
changement borné par pas (maxChangePerSnap)
```

La priorité d'un objectif combine le besoin, la probabilité de succès et l'urgence :

```text
priorité = besoin × probabilité × urgence
```

La décision d'action repose sur l'utilité :

```text
utilité = (bénéfice - coût - risque) × confiance × personnalité + urgence
```

avec un bonus d'alignement sur l'objectif (facteur 1,2), une urgence sigmoïde du besoin :

```text
urgence = 1 / (1 + e^(-0,1 × (besoin - 50))) × 20
```

majorée de plus cinq quand l'objectif vieillit et de plus dix quand un seuil critique est franchi, un hystérésis d'un vingtième (0,05) contre l'oscillation, et une interruption centralisée quand un concurrent dépasse de dix le score courant ou qu'un besoin critique survient.

La dégradation de l'information par saut suit une décroissance géométrique, avec sa version théorique :

```text
confiance effective = c × 0,9^sauts
dégradation théorique = 1 - 0,9^sauts
```

La cohésion d'un lien social combine confiance réciproque et affinité :

```text
cohésion(P, Q) = min(confiance(P→Q), confiance(Q→P)) × affinité(P, Q)
affinité(P, Q) = 1 + (but partagé ? bonus : 0) + (croyances partagées × bonus)
lien(P, Q) ⇔ confiance réciproque ≥ 0,3 ET affinité > 1
```

La viabilité d'un groupe se lit par une pente aux moindres carrés sur une fenêtre de ticks (deux cents pour la référence) ; la densité d'un réseau se compte en `n(n-1)/2` paires possibles ; la fréquence d'observation d'un niveau de détail suit `1 / 2^LOD`.

**Usage dans LIVEX.** Une décision chiffrée et tracée (l'exemple complet de Charlie), une communication dégradée, des groupes à seuils.

**Mise en garde.** BDI est un cadre organisationnel, pas une théorie de l'esprit. Le calcul reste transparent et réfutable, sans réseau opaque.

## 3b.6 Génératifs et langage

**Histoire.** Park et al. (2023) : une richesse sociale sans reproductibilité.

**Usage dans LIVEX.** Aucun modèle au cœur du monde. Un usage futur éventuel, comme expérience identifiée, face à une base qui s'en passe.

**Mise en garde.** Coût, probabilisme, intraçabilité, incohérence : quatre raisons incompatibles avec la question au centre.

## 3b.7 Table de référence des formules

Pour la commodité du lecteur, le tableau suivant rassemble les formules du livre dans leur forme courante. Chacune est détaillée au chapitre qui la mobilise ; le catalogue fait foi en cas d'écart.

| Notion | Formule | Grandeur mesurée |
| :-- | :-- | :-- |
| Entropie | `H = -Σ p_i log2 p_i` | dispersion des croyances ou objectifs |
| Entropie normalisée | `H / log2 k` | dispersion relative, entre 0 et 1 |
| Confiance perceptuelle | `1 - (d / r) × 0,3`, plancher 0,7 | fiabilité d'une observation |
| Saillance d'un souvenir | `s(0) × e^(-taux × délai)` | force d'un souvenir |
| Révision de croyance | conflit `-0,1`, alignement `+0,2`, érosion `×0,999` | confiance d'une croyance |
| Priorité d'objectif | `besoin × probabilité × urgence` | urgence d'un désir |
| Utilité | `(b - c - r) × confiance × personnalité + urgence` | score d'une action |
| Urgence | `1 / (1 + e^(-0,1 × (besoin - 50))) × 20` | pression d'un besoin |
| Dégradation par saut | `c × 0,9^sauts` | fiabilité d'un message relayé |
| Cohésion | `min(confiance réciproque) × affinité` | force d'un lien |
| Fréquence de détail | `1 / 2^LOD` | cadence de décision |
| Pente de viabilité | moindres carrés sur 200 ticks | tendance d'une population |
| Densité de réseau | `arêtes / (n(n-1)/2)` | densité des relations |

## 3b.8 Les formules comme engagements

Ce chapitre a nommé les formules ; les chapitres techniques les détaillent une à une, avec leur origine, leur signification, leur mise en situation, leurs conséquences et leurs limites. Le lecteur qui veut comprendre d'où vient l'entropie de Shannon, ce que signifie une décroissance exponentielle de la mémoire, ou pourquoi l'urgence prend la forme d'une sigmoïde, trouvera ces explications **dans le composant qui les emploie** : les formules de l'agent (traits, besoins, mémoire, croyances, utilité, risque) sont traitées dans la partie SYNE, et celles de l'observation (entropies, densités, indices composites, viabilité) dans la partie ECHOS. Une loi se raconte là où elle agit : c'est délibérément que le livre ne les isole pas dans un chapitre unique.

Refermer ce chapitre, c'est mesurer la dette : aux automates l'audace, aux essaims la robustesse, à *Sugarscape* la preuve miniature, au BDI le cadre, aux génératifs la leçon en creux, et à chaque formule sa borne. Les concepts, ici, n'embellissent pas le propos : ils l'obligent.

Une dernière remarque, qui vaut avertissement. Une formule donne une fausse impression de certitude. Écrire `utilité = (bénéfice - coût - risque) × confiance × personnalité + urgence` ne signifie pas que la décision est vraie, ni même qu'elle est bonne : cela signifie seulement qu'elle est **calculable** et **reproductible**. Un agent peut prendre, par ce calcul, une décision absurde, parce que ses croyances étaient fausses ou son estimation du risque erronée. La formule n'est pas un oracle ; c'est une règle du jeu. Le livre y reviendra chaque fois qu'une décision surprenante apparaîtra : la surprise est souvent le signe que la formule a bien fonctionné, à partir d'entrées imparfaites.

## 3b.9 Ce que ce chapitre apporte à la question directrice

Ce chapitre a transformé des références en contraintes. Chaque concept est désormais défini, chiffré et assorti d'une limite. Le lecteur dispose des instruments nécessaires pour lire les chapitres techniques sans perdre le fil de la question.

Le chapitre suivant change de registre. Après les fondements conceptuels, il expose la méthode : comment le projet décide de ce qu'il tient pour vrai, et pourquoi « ne croire que ce qui se rejoue » n'est pas une formule mais un dispositif.

**Ce que ce chapitre ne permet pas de conclure.**

- Il ne prouve pas que les formules citées soient les bonnes : ce sont des engagements de conception, dont la plupart des constantes sont héritées et non calibrées.
- Il n'établit pas que le vocabulaire du projet soit celui du domaine : plusieurs métriques ont dû être renommées parce que leur nom affirmait plus que leur mesure.
- Il ne démontre pas que ces concepts suffisent à expliquer les comportements observés : ils décrivent des mécanismes, pas des résultats.
- Il ne garantit pas que les bornes chiffrées tiennent à l'échelle : une valeur raisonnable à cent entités peut devenir absurde à mille.
