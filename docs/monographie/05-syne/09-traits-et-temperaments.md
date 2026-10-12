# SYNE IX. Les huit traits, ou ce qui fait qu'une entité n'est pas une autre

Si toutes les entités décidaient avec les mêmes chiffres, le monde produirait une seule trajectoire, répétée mille fois. La diversité d'un monde ne vient pas seulement de ce que ses habitants perçoivent ou savent : elle vient de ce qu'ils sont. Ce chapitre décrit les huit traits de personnalité de SYNE, la façon dont ils modulent les décisions, et la façon dont ils se transmettent.

**Pourquoi ce chapitre importe pour la question directrice.** Imaginez deux jumeaux élevés dans la même maison, avec les mêmes repas et les mêmes voisins. S'ils finissaient par mener des vies identiques, on en conclurait que le monde décide à leur place. S'ils divergent, c'est qu'il y a en eux une différence qui n'était pas dans le décor. Les traits jouent ce rôle dans LIVEX : ce sont les quelques nombres qui font qu'une entité n'est pas interchangeable, et donc que les comportements observés ne peuvent pas être attribués au seul environnement. Sans eux, la question directrice se réduirait à « qu'est-ce que le décor impose ? ». Avec eux, elle devient « qu'est-ce que des individus différents, placés dans le même monde, finissent par produire ensemble ? ».

## 1. Huit nombres, une identité

Une entité porte huit traits, chacun étant un nombre réel. Ils ne décrivent pas une humeur passagère : ils sont fixes pour la vie de l'entité, sauf héritage à la naissance.

| Trait | Rôle dans le système |
| :-- | :-- |
| `bravery` | Tempère la fuite : une entité brave fuit moins volontiers |
| `curiosity` | Favorise l'exploration |
| `sociability` | Favorise les actions sociales (socialiser, donner) |
| `greed` | Favorise la constitution de stock et l'accès aux ressources |
| `pessimism` | Déclaré et hérité ; non encore consommé par la décision (V0.1) |
| `aggression` | Détermine les dégâts d'une attaque |
| `strength` | Déclaré et hérité ; non encore consommé par la décision (V0.1) |
| `speed` | Longueur du pas de déplacement par tick |

Le champ `aggression` du moteur correspond à la clé de configuration `aggressiveness` ; la correspondance est faite à la création de l'entité. Les valeurs par défaut de configuration sont toutes à 1,0, c'est-à-dire neutres.

Cette liste de huit traits est plus courte que les inventaires de la psychologie, et c'est volontaire. Une loi fondamentale doit tenir sur une ligne, et un trait qui n'est lu par aucune formule n'est pas encore une loi : c'est un emplacement réservé. Le tableau ci-dessus distingue donc explicitement les traits **consommés** par la décision (bravoure, curiosité, sociabilité, avarice, agressivité, vitesse) des traits **déclarés** et déjà transmis mais pas encore lus par le calcul (pessimisme, force). Cette distinction est un engagement de méthode : le livre ne présente pas comme actif ce qui n'est que prévu.

## 2. La plage et le neutre : le choix de l'intervalle

Chaque trait appartient à un intervalle borné, avec une valeur neutre centrale.

**Nom** : Domaine de validité d'un trait.

**Explication globale** : Un trait n'est jamais négatif ni arbitrairement grand. La valeur 1,0 est le neutre : elle ne favorise ni ne défavorise rien. En dessous, le trait joue contre la conduite visée ; au-dessus, il joue pour.

**Expression mathématique** :

```math
\forall\, \tau \in \mathcal{T},\quad \tau \in [0,\, 2] \qquad \text{avec } \tau_{\text{neutre}} = 1.0 \qquad \mathcal{T} = \{\text{bravery}, \ldots, \text{speed}\}
```

**Pourquoi cette équation** : La borne inférieure à 0 empêche un trait de devenir un multiplicateur négatif, qui inverserait le sens d'une action. La borne supérieure à 2 borne le levier maximal d'un seul trait à un facteur deux autour du neutre, ce qui évite qu'une entité extrême écrase toute la population. Le neutre à 1,0 place la moyenne naturelle de la configuration là où aucune correction n'est appliquée, ce qui rend lisible tout écart. Le choix d'un intervalle symétrique autour du neutre est aussi une commodité d'interprétation : la valeur 1,5 signifie « à mi-chemin entre neutre et maximum », dans tous les traits à la fois.

**Usage dans le projet** : La validation est faite à la construction de l'ensemble des traits. Une valeur hors bornes, non finie ou manquante est une erreur de configuration, jamais une valeur silencieusement corrigée ; l'entité n'est pas créée.

**Interprétation** : Une population tirée uniformément autour du neutre possède un comportement moyen proche du neutre et une variance contrôlée. Une population où l'avarice est systématiquement haute produit une course aux réserves ; une population apathique produit de l'immobilité. Dans les deux cas, l'effet vient d'une propriété des individus, et non du monde.

**Ce que cette forme doit à l'histoire.** Étalonner un trait sur une plage symétrique autour d'une valeur neutre est une pratique courante en simulation sociale, où l'on préfère des indices standardisés à des quantités physiques. En psychologie, la mise à l'échelle des traits de tempérament (du modèle à cinq facteurs aux inventaires plus anciens) obéit à la même logique : un score centré, comparé à une moyenne de référence, plutôt qu'une quantité absolue. Le projet a repris la convention sans prétendre mesurer une personnalité réelle : il a seulement besoin d'une échelle comparable d'une entité à l'autre.

## 3. Le modulateur de personnalité : comment un trait pèse sur le choix

Un trait n'agit jamais seul ni directement : il entre dans la formule d'utilité par un **modulateur**, c'est-à-dire un facteur multiplicatif appliqué au score de l'action.

**Nom** : Modulateur de personnalité d'une action.

**Explication globale** : Pour chaque famille d'action, un trait est choisi comme levier. Le modulateur vaut `0,5 + trait` pour les actions favorisées, `0,5 + (2 − trait)` pour les actions contrariées (la fuite, contrariée par la bravoure), et 1,0 pour les actions sans trait associé. Le résultat est plancheré à 0,1.

**Expression mathématique** :

```math
\mathrm{PM}(a) = \max\!\left(0.1,\; \mu(a)\right) \qquad
\mu(a) =
\begin{cases}
0.5 + \mathrm{greed} & a \in \{\text{SeekFood}, \text{Eat}, \text{SeekWater}, \text{Drink}, \text{Take}\} \\[3pt]
0.5 + \mathrm{curiosity} & a = \text{Explore} \\[3pt]
0.5 + \mathrm{sociability} & a \in \{\text{Socialize}, \text{Give}\} \\[3pt]
0.5 + (2 - \mathrm{bravery}) & a = \text{Flee} \\[3pt]
1.0 & \text{sinon}
\end{cases}
```

**Pourquoi cette équation** : Le facteur `0,5 + trait` place le neutre à 1,5 et un trait nul à 0,5 : un trait neutre ne modifie donc pas *l'échelle relative* entre deux actions, puisque toutes les actions favorisées d'un même trait partagent le même facteur. Ce qui change est la préférence relative entre familles : une entité curieuse voit l'exploration monter par rapport au repos, sans que son envie de manger change. La forme `0,5 + (2 − trait)` renverse la logique pour la fuite : plus la bravoure est haute, plus le gain à fuir est faible. Le plancher à 0,1 évite qu'un modulateur s'annule et supprime tout à fait une action, ce qui fermerait une branche du comportement.

**Usage dans le projet** : Le modulateur est calculé par l'évaluateur d'utilité à chaque évaluation d'action candidate. Il est publié dans le score de chaque action, ce qui permet de lire, dans une trace de décision, quelle part du score vient de la personnalité plutôt que du besoin.

**Interprétation** : Un modulateur de 1,0 signifie « le trait n'a rien à dire ici ». Un modulateur de 1,8 signifie que l'action est fortement favorisée par un trait ; 0,6, qu'elle est freinée. Comme le modulateur multiplie la partie `(bénéfice − coût − risque) × confiance`, il n'agit que sur la composante délibérative du score : il ne peut jamais, à lui seul, déclencher une action face à une urgence, puisque celle-ci est ajoutée après le produit.

**Ce que cette forme doit à l'histoire.** Moduler une préférence par un facteur propre à l'individu est l'un des gestes les plus anciens de la décision formalisée : la fonction d'utilité de Bentham admettait déjà que des sensibilités différentes donnent des plaisirs différents. La nouveauté ici n'est pas le principe, mais la **traçabilité** : le facteur est un nombre publié, relu dans la trace de décision, et non une préférence tacite.

## 4. Les traits consommés, et ceux qui ne le sont pas encore

Tous les traits n'agissent pas par le modulateur. Le tableau suivant sépare nettement ce qui est implémenté de ce qui ne l'est pas.

| Trait | Où il agit réellement | Effet |
| :-- | :-- | :-- |
| `bravery` | Modulateur de `Flee` | Moins de fuite quand la bravoure monte |
| `curiosity` | Modulateur de `Explore` | Plus d'exploration |
| `sociability` | Modulateur de `Socialize`, `Give` | Plus d'action sociale |
| `greed` | Modulateur des actions de ressource et de `Take` | Plus de collecte et de stock |
| `aggression` | Calcul des dégâts d'une attaque | `dégâts = 5 × agressivité` |
| `speed` | Exécution du déplacement | Pas borné par la vitesse |
| `strength` | Aucune formule en V0.1 | Déclaré, transmis, non consommé |
| `pessimism` | Aucune formule en V0.1 | Déclaré, transmis, non consommé |

Deux traits sont donc des emplacements réservés. C'est une conséquence directe de la doctrine du projet : une valeur qui n'est lue par aucune formule n'influence rien, et il serait malhonnête de la présenter comme un mécanisme. `strength` et `pessimism` existent pour que l'héritage et les fichiers de configuration soient complets, et pour qu'une version ultérieure puisse les brancher (résistance à un combat, estimation pessimiste du risque, par exemple) sans changer le format. En attendant, leur seul effet est indirect : ils pèsent dans la moyenne des traits d'une entité, et donc dans la diversité d'expression des traits, l'une des **douze** métriques de la famille cognitive d'ECHOS (voir ECHOS VI).

Un mot sur la vitesse, parce que son effet est physique et non décisionnel. Le pas de déplacement d'un tick est borné par `speed` : une entité rapide parcourt davantage de terrain, ce qui augmente mécaniquement son rayon d'action utile et son taux de rencontre. La vitesse ne rend pas une action plus désirable ; elle rend l'action plus efficace. C'est une distinction importante : un trait peut agir sur le choix, comme la bravoure, ou sur l'exécution, comme la vitesse.

## 5. L'héritage : comment un trait se transmet

Les entités se reproduisent, et leurs traits se transmettent. La fusion n'est pas une moyenne naïve : elle combine une**dominance** et une **mutation**.

**Nom** : Fusion des traits à la naissance.

**Explication globale** : Chaque trait de l'enfant est un mélange contrôlé de ceux de ses deux parents. Le mélange part d'une moyenne égalitaire, puis se déplace vers le parent dont le trait s'écarte le plus du neutre, selon un coefficient de dominance. Une mutation, si elle est activée, ajoute ensuite une perturbation bornée à chaque trait, déterminée par la graine de naissance et l'indice du trait.

**Expression mathématique** :

```math
\tau_{\mathrm{moy}} = \frac{\tau_A + \tau_B}{2} \qquad
\tau_{\mathrm{expr}} = \begin{cases} \tau_A & \text{si } |\tau_A - 1| \ge |\tau_B - 1| \\[3pt] \tau_B & \text{sinon} \end{cases}
```

```math
\tau_{\mathrm{fusionné}} = \tau_{\mathrm{moy}} \times (1 - d) + \tau_{\mathrm{expr}} \times d \qquad d = \text{dominance} \in [0, 1]
```

```math
\tau_{\mathrm{enfant}} = \mathrm{clamp}\!\bigl(\tau_{\mathrm{fusionné}} + \Delta,\; 0,\, 2\bigr) \qquad
\Delta = \begin{cases} (\mathrm{tirage}(\text{graine}, \text{trait}) - 0.5) \times 2 \times m & \text{si } \mathrm{tirage} < r_{\mathrm{mut}} \\[3pt] 0 & \text{sinon} \end{cases}
```

**Pourquoi cette équation** : La moyenne pure dilue les traits extrêmes à chaque génération et ramènerait toute population vers le neutre en quelques générations, effaçant précisément ce que les traits servaient à produire. La dominance, qui va de 0 (moyenne) à 1 (le parent exprimant l'emporte), préserve les individualités fortes : un parent très brave transmet sa bravoure même face à un parent neutre. Le critère de choix du parent exprimant est l'écart au neutre, et non la valeur brute : un trait nul est aussi « exprimant » qu'un trait maximal, ce qui évite de privilégier systématiquement le plus grand. La mutation, tirée par un générateur stable indexé par trait et par graine, garantit qu'une population ne se fige jamais sur un jeu de valeurs identique, tout en restant reproductible : les mêmes parents, à la même graine, produisent le même enfant. Le plafonnement à `[0, 2]` empêche la dérive hors du domaine.

**Usage dans le projet** : La fusion a lieu au moment de la naissance. Les paramètres de dominance et de mutation sont configurables, et leurs valeurs par défaut conservent la compatibilité avec la version initiale (dominance nulle, mutation nulle). L'héritage porte aussi une part de savoir : les souvenirs et les croyances des parents peuvent être transmis, filtrés par un seuil de saillance, ce qui distingue un patrimoine génétique d'un patrimoine culturel.

**Interprétation** : Avec une dominance nulle, l'enfant est exactement la moyenne de ses parents : la population converge doucement. Avec une dominance forte, un trait extrême se perpétue. Avec une mutation non nulle, l'écart-type des traits dans la population cesse de décroître et atteint un équilibre, c'est cette variance résiduelle qui maintient la diversité d'expression des traits, donc la variété des trajectoires.

## 6. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré comment le projet fabrique de l'irréductibilité individuelle avec huit nombres et quelques formules. Une même situation peut désormais produire des choix différents, non parce que le hasard est intervenu, mais parce que deux entités n'ont pas les mêmes leviers. La question directrice demande des comportements **non explicitement programmés** : les traits sont exactement cela. Personne n'a écrit « cette entité explorera le nord-est au tick 4000 » ; on a écrit une préférence, et le monde a fait le reste.

C'est aussi ce qui donne un sens mesurable à la diversité. ECHOS mesure la **diversité d'expression des traits** : si la population converge vers le neutre, la métrique baisse, et l'on peut dater cette uniformisation. Un monde qui perd sa diversité de tempéraments est un monde qui perd une partie de sa capacité à produire de l'inattendu.

**Ce que ce chapitre ne permet pas de conclure.**

- Rien n'établit que huit traits suffisent, ni que cette liste soit la bonne. C'est un choix de modélisation minimal, révisable.
- Deux traits (`strength`, `pessimism`) sont déclarés et transmis mais **non consommés** par la décision en V0.1 : leur influence actuelle est indirecte, par la moyenne et la diversité.
- Les valeurs par défaut des traits sont neutres et ne sont pas calibrées : aucune campagne n'a établi qu'une population neutre est plus ou moins intéressante qu'une population dispersée.
- L'héritage n'a pas encore été observé sur des générations longues ; la dérive de la variance des traits dans le temps reste à mesurer.
