# ECHOS V. Les outils mathématiques partagés

Avant de mesurer quoi que ce soit, un observatoire doit choisir ses instruments. Ce chapitre décrit les sept outils élémentaires que toutes les métriques d'ECHOS réutilisent : l'entropie de Shannon et sa version normalisée, la moyenne, la variance, le rapport borné, la fonction de borne et l'indice de concordance. Aucune de ces briques n'est propre au projet ; ce qui l'est, c'est la discipline qui les entoure.

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande des structures non programmées. Pour les reconnaître, il faut des mesures **comparables d'un tick à l'autre, d'un run à l'autre, d'une graine à l'autre**. Une grandeur calculée avec un instrument différent à chaque endroit ne comparerait rien : elle produirait des nombres dont on ne saurait pas dire s'ils diffèrent parce que le monde a changé ou parce que la mesure a changé. Les outils de ce chapitre existent pour rendre les nombres comparables, et donc la question directrice vérifiable plutôt que racontable.

## 1. L'entropie de Shannon : mesurer le désordre d'une distribution

**Nom** : Entropie de Shannon.

**Explication globale** : L'entropie mesure le **degré de surprise moyen** d'une distribution de probabilités. Elle vaut zéro lorsqu'un seul cas est possible (aucune surprise) et atteint son maximum lorsque tous les cas sont équiprobables (surprise maximale).

**Expression mathématique** :

```math
H(P) = -\sum_{i=1}^{k} p_i \cdot \log_2(p_i) \qquad \text{pour tout } p_i > 0
```

**Pourquoi cette équation** : Le logarithme apparaît parce que l'information de plusieurs événements indépendants doit **s'additionner**, et que seul le logarithme transforme un produit en somme : `log(a × b) = log(a) + log(b)`. La base 2 fait compter l'information en **bits**, l'unité associée à une décision binaire équiprobable. Le signe négatif corrige le fait qu'une probabilité inférieure à un a un logarithme négatif. Le produit `p_i × log₂(p_i)` pondère chaque surprise par sa fréquence : un événement très surprenant mais presque impossible compte peu. Enfin, la convention `p = 0` est exclue de la somme, car `0 × log(0)` vaut zéro par limite et non par indéfinition.

**Usage dans le projet** : L'entropie sert à mesurer la dispersion des croyances, des objectifs et des actions. Deux familles de métriques reposent dessus : la diversité des croyances et celle des objectifs.

**Interprétation** : `H = 0` en bits signifie unanimité parfaite. `H = log₂(k)` signifie dispersion maximale sur `k` catégories. Une entropie ne dit rien de la **vérité** des croyances : un monde où tout le monde se trompe de la même façon a une entropie basse, ce qui n'est ni bon ni mauvais en soi.

**Ce que cette formule doit à l'histoire.** Claude Shannon, ingénieur chez Bell Labs, publie en 1948 *A Mathematical Theory of Communication* : cherchant à mesurer la quantité d'information qu'un canal peut transporter, il montre qu'une seule fonction satisfait des exigences raisonnables (continuité, maximum en cas d'équiprobabilité, additivité pour des sources indépendantes). Le nom « entropie » lui aurait été suggéré par John von Neumann, qui y voyait une parenté formelle avec l'entropie de la thermodynamique. Une expression voisine de la même grandeur fut proposée indépendamment par Norbert Wiener, dans le même mouvement qui donna naissance à la cybernétique. L'unité de mesure, le **bit**, fut popularisée par les ingénieurs des télécommunications et devint le vocabulaire commun de la compression, de la cryptographie et de l'apprentissage automatique.

## 2. L'entropie normalisée : comparer malgré l'échelle

**Nom** : Entropie de Shannon normalisée.

**Explication globale** : Diviser l'entropie par son maximum atteignable ramène toute dispersion à une fraction. Elle vaut zéro en cas d'unanimité et un en cas de dispersion maximale, quel que soit le nombre de catégories.

**Expression mathématique** :

```math
H^*(P) = \frac{H(P)}{\log_2(k)} = \frac{-\sum_{i=1}^{k} p_i \cdot \log_2(p_i)}{\log_2(k)}
```

avec `k` le nombre de catégories possibles.

**Pourquoi cette équation** : Une entropie brute dépend du nombre de catégories : la même dispersion sur dix valeurs donne une entropie plus grande que sur deux. Additionner des entropies brutes favoriserait donc mécaniquement les situations à nombreuses catégories, ce qui n'est pas ce qu'on veut mesurer. La normalisation ramène chaque entropie à la fraction de son maximum, ce qui la rend comparable d'une famille à l'autre. C'est la même logique que le pourcentage, qui ramène des comptages hétérogènes à une base de cent.

**Usage dans le projet** : Deux conventions coexistent et ne se mélangent jamais : les entropies **brutes**, en bits, pour la lecture directe d'une dispersion ; les entropies **normalisées**, en fractions, comme entrée des indices composites. Un score composite n'additionne que des versions normalisées.

**Interprétation** : `H* = 0` : unanimité. `H* = 1` : dispersion maximale atteignable par la population observée. `H* = 0,5` : la moitié du désordre possible.

**Limite** : normaliser suppose de connaître `k`, le nombre de catégories possibles. Dans un monde ouvert, où de nouvelles valeurs de croyance peuvent apparaître, ce nombre n'est pas évident : le projet le fixe par famille et le publie, plutôt que de le laisser implicite.

## 3. La moyenne : un résumé qu'il faut lire

**Nom** : Moyenne arithmétique.

```math
\bar{x} = \frac{1}{N} \sum_{i=1}^{N} x_i
```

La moyenne résume une population par une seule valeur. Sa justification est d'être la plus sobre possible, et sa limite est connue depuis longtemps : elle est sensible aux valeurs extrêmes et peut ne correspondre à aucun individu. Dans ECHOS, elle sert à des niveaux moyens (confiance, taille de communauté) et à des ratios (consommation par tick). Un exemple suffit à montrer pourquoi elle ne peut jamais être lue seule : une moyenne de confiance de 0,5 recouvre aussi bien une population où tous se font modérément confiance qu'une population partagée entre des liens très forts et des méfiances profondes. D'où l'outil suivant.

## 4. La variance : ce que la moyenne cache

**Nom** : Variance de population.

```math
\sigma^2 = \frac{1}{N} \sum_{i=1}^{N} (x_i - \bar{x})^2
```

**Pourquoi un dénominateur `N` et non `N − 1`** : la population est **entière**, on mesure toutes les entités du monde, pas un échantillon. La correction de Bessel (`N − 1`), qui compense le biais d'un échantillon, serait ici une erreur d'interprétation : il n'y a rien à estimer. Le drapeau est donc fixé une fois pour toutes, et documenté, parce qu'une confusion sur ce point suffit à rendre deux séries incomparables.

**Ce que cette formule doit à l'histoire.** La variance et l'écart-type sont nés de l'astronomie et de la géodésie du dix-neuvième siècle, où l'on cherchait à quantifier l'erreur de mesures répétées. Gauss et Laplace ont formalisé la méthode des moindres carrés, dont la variance est un ingrédient central. Le carré des écarts empêche les écarts positifs et négatifs de s'annuler, et pénalise davantage les grands écarts, un choix de conception, pas une nécessité logique.

**Usage dans le projet** : La variance sert à mesurer l'hétérogénéité des confiances, la dispersion des croyances autour de leur moyenne, la variance des tailles de communautés et celle de l'expression des traits. Elle est le complément naturel de l'entropie : l'entropie mesure la variété des **catégories**, la variance celle des **valeurs**.

**Limite** : la variance est sensible aux extrêmes, et son unité est le carré de celle de la grandeur mesurée. Dans les compositions, on lui préfère souvent une forme bornée (voir la stabilité dynamique, ECHOS VII).

## 5. Le rapport borné et la fonction de borne : les garde-fous

**Nom** : Rapport borné.

```math
\mathrm{safe\_ratio}(a, b) =
\begin{cases}
a / b & \text{si } b \neq 0 \\[3pt]
0.0 & \text{si } b = 0
\end{cases}
```

**Nom** : Fonction de borne.

```math
\mathrm{clamp}(x, a, b) = \max\bigl(a,\; \min(b, x)\bigr)
```

Ces deux fonctions ne mesurent rien : elles empêchent la mesure de produire un nombre absurde. Le rapport borné évite la division par zéro, qui produirait une exception ou une valeur infinie et interromprait toute une série. La fonction de borne ramène toute valeur dans un intervalle annoncé. Leur importance est disproportionnée à leur simplicité : sans elles, un monde vide (population nulle, aucun message, aucune communauté) ferait échouer l'analyse au lieu de produire une mesure honnête.

Le choix de la valeur de repli est aussi un choix épistémique. Le rapport borné retourne zéro, jamais un maximum ni une valeur arbitraire : une donnée absente ne doit pas être interprétée comme une observation de cas favorable. C'est la même règle qui, dans le calcul de la viabilité, empêche d'attribuer à un monde sans crise une résilience parfaite.

## 6. L'indice de concordance : quand tout le monde est d'accord

**Nom** : Indice de concordance.

```math
C(P) = \sum_{i=1}^{k} p_i^2
```

**Explication globale** : La somme des carrés des parts est la probabilité que deux individus tirés **au hasard avec remise** partagent la même catégorie. Elle vaut 1 en cas d'unanimité et `1/k` en cas de dispersion uniforme.

**Pourquoi cette équation** : L'indice de concordance mesure la **concentration** là où l'entropie mesure la dispersion. Les deux sont liés mais pas inversés : `C` est maximale quand `H` est minimale. Son intérêt pratique est d'être borné sur `[1/k, 1]` et de se lire en probabilité, ce qui est souvent plus parlant qu'une entropie en bits : « deux entités prises au hasard ont 0,9 de chance de viser la même chose » se comprend mieux que « l'entropie vaut 0,15 bit ».

**Usage dans le projet** : C'est l'outil des métriques de convergence. La concordance des catégories de buts mesure la probabilité que deux entités partagent une catégorie ; la convergence des objectifs est une version normalisée sur la population.

**Ce que cette formule doit à l'histoire.** La somme des carrés des parts est un outil ancien de l'économie industrielle, où elle porte le nom d'indice de Herfindahl-Hirschman : il mesure la concentration d'un marché, et vaut de `1/n` (marché parfaitement concurrentiel) à 1 (monopole). La même grandeur est utilisée en écologie pour mesurer la dominance d'une espèce, et en génétique pour la diversité des allèles. Le fait qu'une même formule serve à un marché, à un écosystème et à un monde d'agents n'est pas un hasard : c'est la mesure la plus naturelle de « combien de catégories se partagent réellement la population ».

## 7. Les principes qui encadrent ces outils

Les sept outils ci-dessus n'ont de sens que dans un cadre. Trois principes, empruntés aux fondations analytiques, s'appliquent à leur emploi.

- **Neutralité et déterminisme** : aucune formule ne modifie l'état de SYNE ni ne dépend d'un générateur aléatoire ou d'un horodatage. Les mêmes entrées donnent les mêmes sorties ; les distributions et les listes sont triées avant émission. C'est ce qui rend une analyse rejouable.
- **Zéro observé n'est pas non mesuré** : une valeur de repli neutre (zéro par convention) est la bonne réponse du moteur, mais ce n'est pas une observation. Chaque sortie porte une provenance qui distingue « la formule a tourné et donné zéro » de « la donnée manquait ».
- **Règle d'or** : aucun indice ne transforme une mesure en vérité scientifique. Les outils de ce chapitre produisent des nombres à propos d'un monde, jamais des preuves sur la vie, l'intelligence ou la société.

## 8. Ce que ce chapitre apporte à la question directrice

Ce chapitre a posé les briques sur lesquelles repose toute la mesure du projet. Il montre que l'observatoire n'invente presque rien : il emprunte à la théorie de l'information, à la statistique et à l'économie industrielle des instruments éprouvés, puis les discipline. La question directrice exige des comportements non programmés ; la mesure de ces comportements exige des grandeurs comparables ; et cette comparabilité repose, en dernière analyse, sur sept formules élémentaires et sur la règle qui les empêche de mentir.

**Ce que ce chapitre ne permet pas de conclure.**

- Ces outils ne mesurent pas ce qu'on ne leur demande pas : une entropie ne détecte aucune causalité, une variance ne prouve aucun phénomène.
- La normalisation dépend d'un nombre de catégories fixé par famille ; elle reste donc un choix publié, non une vérité.
- Chaque constante associée (fenêtre, seuil, horizon) est un paramètre, et la majorité des constantes d'ECHOS sont héritées et non calibrées.
- Aucun de ces outils ne dit si une valeur élevée est **souhaitable** : c'est une question d'interprétation, traitée plus loin, jamais incluse dans la formule.
