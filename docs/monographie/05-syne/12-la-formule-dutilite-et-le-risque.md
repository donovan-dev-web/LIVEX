# SYNE XII. La formule d'utilité et la prise de risque

Voici le cœur du moteur décisionnel. Une entité perçoit, se souvient, croit, ressent un manque ; il lui reste à choisir. Ce chapitre décrit la formule qui tranche, terme par terme, et consacre une section entière à la notion la plus délicate de cette formule : le risque.

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande des comportements qui n'ont pas été programmés. Or un comportement n'est « non programmé » que s'il résulte d'un arbitrage, et non d'une règle écrite d'avance. La formule d'utilité est le lieu exact de cet arbitrage : c'est elle qui transforme six manques, une perception faillible et huit traits en une action unique. Personne n'a écrit « Charlie mangera au tick 5000 » ; on a écrit une règle de comparaison, et le monde a fourni les nombres. Si cette formule était absente, chaque action devrait être décidée par une condition écrite à la main, exactement ce que le projet refuse.

## 1. La formule, d'un seul trait

Une action candidate est notée par une utilité :

```math
U(a) = \bigl(B(a) - C(a) - R(a)\bigr) \times \mathrm{conf}(a) \times \mathrm{PM}(a) \;+\; \mathrm{urg}(a)
```

avec `B` le bénéfice, `C` le coût, `R` le risque, `conf` la confiance, `PM` le modulateur de personnalité et `urg` l'urgence. L'action retenue est celle d'utilité maximale, avec départage déterministe par l'ordre du catalogue en cas d'égalité.

**Nom** : Utilité d'une action candidate.

**Explication globale** : Une action vaut ce qu'elle rapporte, moins ce qu'elle coûte, moins ce qu'elle risque ; ce solde est pondéré par la confiance qu'on a dans l'information et par la personnalité de l'entité ; puis on ajoute une urgence, qui ne dépend pas de l'action mais de l'état du besoin.

**Pourquoi cette équation** : La structure du produit et de la somme n'est pas arbitraire, elle encode une thèse. Le facteur `(bénéfice − coût − risque)` est un **solde délibératif** : il compare des actions entre elles. Les facteurs `confiance` et `personnalité` sont des **modulateurs** : ils ne changent pas le signe du solde, ils l'atténuent. L'urgence, enfin, est une **addition** : elle ne dépend pas de l'action et s'ajoute après coup. Cette séparation a une conséquence que l'on peut énoncer précisément : une urgence vitale peut faire gagner une action dont le solde délibératif est mauvais, mais aucun modulateur ne peut, à lui seul, faire gagner une action dont le solde est nul ou négatif. Autrement dit, la personnalité nuance, l'urgence décide. Cette asymétrie est le vrai sujet de la formule.

**Ce que cette architecture doit à l'histoire.** L'idée de comparer des actions par une grandeur commune remonte à l'utilitarisme de Bentham, et sa formalisation moderne à la théorie de l'utilité espérée de von Neumann et Morgenstern (1944). Le projet n'emploie pas cette théorie au sens strict : il n'y a ici ni probabilités ni espérance mathématique, mais un score déterministe. Il en garde le geste, à savoir rendre les préférences comparables, et s'en écarte sur un point capital : aucune notion d'agent « rationnel » n'est postulée. Une entité de LIVEX peut prendre une décision absurde ; la formule garantit seulement que la décision est **calculable** et **reproductible**, pas qu'elle est bonne.

## 2. Le bénéfice : ce qu'une action apporte

Le bénéfice traduit la satisfaction attendue d'un besoin. Il n'est pas uniforme : chaque action possède sa propre formule.

```math
B(a) =
\begin{cases}
\min(\text{hunger},\, 100) \times 0.6 & a \in \{\text{SeekFood},\, \text{Eat}\} \\[2pt]
\min(\text{thirst},\, 100) \times 0.6 & a \in \{\text{SeekWater},\, \text{Drink}\} \\[2pt]
\min(\text{fatigue},\, 40) & a = \text{Rest} \\[2pt]
(1 - \text{safety}) \times 60 & a = \text{Flee} \\[2pt]
\text{social} \times 60 & a \in \{\text{Socialize},\, \text{Give}\} \\[2pt]
\min(\text{curiosity} \times 100,\, 30) & a = \text{Explore} \\[2pt]
15 & a = \text{Take} \\[2pt]
\max(\text{hunger},\, \text{thirst}) \times 0.54 & a = \text{Trade} \\[2pt]
0 & \text{sinon}
\end{cases}
```

Une subtilité, aujourd'hui la seule de ce terme. Le bénéfice est **multiplié par 1,2** lorsque l'action prolonge l'objectif courant : une entité déjà engagée valorise un peu plus ce qu'elle faisait déjà. Il n'y a pas d'autre bonification. En particulier, il n'existe **aucun alignement collectif** dans le score : un agent membre d'un groupe ne voit pas son bénéfice majoré parce que son groupe a adopté une décision. La décision collective est un **constat exposé** — porté par l'état du groupe, le snapshot et l'événement `group_decision` — jamais une cause d'utilité individuelle. Un agent qui décide de suivre la décision de son groupe en fait un candidat ordinaire, évalué par la même formule, et le suivi se lit dans la trace de sa décision comme n'importe quel autre choix. Ce point est arbitré et gardé par un test de non-réintroduction : un mécanisme qui pousserait le bénéfice à proportion du consensus et de la confiance envers le leader a existé, puis a été retiré, et sa réintroduction exige un nouvel arbitrage explicite (`AdrGroupObserverTests`).

**Pourquoi le plafond du bénéfice a une histoire.** La valeur `min(besoin, 100) × 0,6` n'est pas la version initiale. Le bénéfice de manger et de boire était d'abord plafonné à 30. Une campagne de calibration a montré la conséquence : à faim saturée, l'urgence ne compensait jamais un bénéfice bloqué à 30, et l'action de manger perdait systématiquement contre la socialisation (bénéfice 60) et l'exploration. Sur la campagne, l'utilité moyenne de manger valait 13,9 contre 83,7 pour ses concurrentes, les entités ne se nourrissaient plus. Le plafond a été porté à `min(besoin, 100) × 0,6`, c'est-à-dire un bénéfice **monotone** avec le besoin, plafonné à 60. La leçon dépasse ce cas : un plafond mal placé ne se contente pas de fausser un score, il peut rendre une action **structurellement perdante**, donc modifier les comportements observés dans tout le monde.

## 3. Le coût : ce qu'une action dépense

Le coût est simple, et c'est un choix. Pour la plupart des actions, il vaut le coût énergétique d'un déplacement (0,03 par défaut) ; pour quelques actions du catalogue déclaratif, il peut être fixé séparément.

```math
C(a) = \begin{cases} c_{\text{catalogue}}(a) & \text{si l'entrée du catalogue définit un coût} \\[2pt] r_{\text{mouvement}} & \text{sinon} \end{cases}
```

Le coût du mouvement a lui aussi une histoire de calibration. Il valait 0,5, puis 0,05, et le mouvement représentant entre 75 et 98 % des ticks en régime établi, son coût dépassait le revenu amorti de manger et de boire (environ 0,057 par tick) : l'énergie moyenne décroissait sans borne, jusqu'à l'extinction lente. La valeur 0,03 est ce qui rend une population viable. Le coût n'est donc pas un détail d'implémentation : c'est l'un des réglages qui décident si le monde tient ou s'éteint.

## 4. Le risque : la prise de risque comme loi

Le risque est le terme le plus intéressant de la formule, parce qu'il **ne dépend pas de l'état de l'entité**. C'est une constante par type d'action.

```math
R(a) =
\begin{cases}
0.60 & \text{Attack} \\
0.30 & \text{Flee} \\
0.25 & \text{Explore} \\
0.15 & \text{SeekFood},\ \text{SeekWater},\ \text{Give},\ \text{Trade} \\
0.10 & \text{Socialize},\ \text{Eat},\ \text{Drink},\ \text{Take} \\
0.05 & \text{Rest} \\
0.00 & \text{Defend}
\end{cases}
```

**Pourquoi cette équation, et pourquoi sous cette forme si simple.** Le risque est un **prix fictif**. Aucun danger n'est modélisé en V0.1 : il n'y a pas de prédateur qui infligerait des dégâts, pas de terrain qui blesserait, pas de probabilité d'échec matériel. Le risque est donc une pénalité forfaitaire, qui traduit une **intention de conception** : chercher de la nourriture expose davantage que se reposer, explorer expose davantage que socialiser, attaquer est l'action la plus coûteuse en risque. Son rôle est de freiner les comportements aventureux sans les interdire, et de rendre l'exploration non gratuite, sans quoi un monde où rien ne coûte ne produirait qu'une ruée permanente.

**Un point de méthode : ce que le risque n'est pas.** Il serait tentant de croire que la bravoure d'une entité diminue son risque : ce n'est pas le cas. Le risque est soustrait **avant** la multiplication par le modulateur de personnalité, et la bravoure n'apparaît que dans le modulateur de la fuite. Autrement dit, en V0.1, deux entités différentes face à la même action éprouvent exactement le même risque, et seule leur propension à fuir les distingue. C'est une limite assumée : la prise de risque n'est pas encore individualisée, elle est commune. Le tableau complet des traits (SYNE IX) précise d'ailleurs que `pessimism`, qui aurait naturellement porté une estimation subjective du risque, est déclaré mais pas encore consommé.

**Pourquoi des constantes, et non une estimation ?** Une estimation dépendrait d'une probabilité d'échec, donc d'un modèle. Or le projet refuse les modèles appris dans le cœur : une estimation du risque devrait être calibrée, versionnée et justifiée, et rien n'établit aujourd'hui qu'elle serait meilleure qu'une constante. Une constante a l'avantage d'être **lisible, traçable et réfutable** : on peut la modifier et voir ce que le monde devient. C'est exactement le genre de paramètre qu'une analyse de sensibilité peut interroger.

**Les conséquences de ce choix.** Un risque constant par action produit un effet de structure : toute action risquée est pénalisée du même montant, quel que soit le contexte. Chercher de l'eau coûte 0,15 de risque, que l'entité soit en pleine forme ou mourante. Comme l'urgence s'ajoute après la multiplication, un besoin critique peut toutefois surpasser cette pénalité : le mécanisme garantit donc qu'un individu affamé explore même si explorer coûte cher. C'est la combinaison du risque forfaitaire et de l'urgence additive qui rend le comportement à la fois prudent en régime normal et téméraire en régime critique.

## 5. La confiance et la personnalité

**Nom** : Confiance d'une action.

```math
\mathrm{conf}(a) = 0.5 \times \bigl(0.5 + \mathrm{tauxSuccès}(a) \times 0.5\bigr)
```

La confiance part d'un historique de succès, ce qui lui donne un domaine de 0,25 à 0,5. Elle est volontairement **basse** : le projet préfère des entités qui doutent. Sa forme en deux facteurs (une base, puis une modulation) fait qu'une action jamais essayée vaut 0,25, et une action toujours réussie 0,5. La confiance ne peut donc jamais dominer le solde délibératif ; elle ne fait que l'atténuer plus ou moins.

Le modulateur de personnalité `PM(a)`, décrit au chapitre IX, intervient au même niveau : il multiplie le solde délibératif par un facteur qui peut aller de 0,1 à 2,5 environ selon le trait et l'action. Son point d'insertion est essentiel : il agit **après** le solde et **avant** l'urgence, donc il ne peut pas créer de la volonté là où le calcul n'en trouve pas.

## 6. L'urgence : ce qui presse et ne se négocie pas

**Nom** : Urgence d'une action.

```math
\mathrm{urg}(a) = \frac{20}{1 + e^{-0.1\,(\mathrm{drive}(a) - 50)}} + 5 \cdot \mathbb{1}[\text{âge} > 100] + 10 \cdot \mathbb{1}[\text{état critique}]
```

**Pourquoi une sigmoïde.** L'urgence ne croît pas proportionnellement au besoin : elle reste plate tant que le manque est faible, se redresse autour du seuil de 50, puis sature à 20. Cette forme reproduit une intuition simple : on n'est pas vingt fois plus urgent parce qu'on a vingt fois plus faim ; on est urgent, puis on l'est extrêmement. La saturation évite qu'une faim extrême écrase toute autre considération, tout en garantissant que l'entité finit par se nourrir. La forme sigmoïde (la fonction logistique) vient de l'étude de la croissance des populations, formalisée par Verhulst au dix-neuvième siècle ; elle est ici employée comme courbe de réponse, non comme dynamique de population.

**Les deux majorations.** Une objectif qui dure plus de cent ticks gagne 5 points d'urgence : cela empêche qu'une envie ancienne soit indéfiniment perdante. Un état critique (faim supérieure à 85, énergie inférieure à 10) ajoute 10 points : c'est le signal vital. Ces deux additions sont les seules qui ne dépendent pas de la forme du besoin, et ce sont elles qui rendent la rupture possible.

## 7. Sélection, inertie et interruption

Une fois les scores calculés, la sélection retient la valeur maximale, avec un départage déterministe par ordre du catalogue. Trois mécanismes corrigent ensuite cette simplicité.

- **L'hystérésis.** Une nouvelle action n'est adoptée que si elle dépasse l'action courante d'une marge de 0,05. Sans elle, deux actions de scores proches produiraient un vacillement à chaque tick, une agitation absurde. Sa fonction est de lisser les **petites** différences.
- **L'interruption.** À tout tick, une action critique peut briser une décision déjà prise : si un besoin critique survient, ou si une action concurrente dépasse la courante de plus de 10 points d'utilité. Sa fonction est de casser les **grandes** inerties.
- **La cadence.** La délibération n'a pas lieu à chaque tick : elle est périodique, et entre deux passages c'est la dernière décision qui est « reportée ». Ce dispositif sera décrit au chapitre de la boucle ; retenons ici qu'il ne change pas la formule, seulement la fréquence à laquelle on la consulte.

L'hystérésis et l'interruption forment un couple : la première empêche de changer d'avis pour rien, la seconde autorise à changer d'avis quand tout est en jeu. Une entité de LIVEX est donc stable en régime normal et réactive en danger.

## 8. L'arithmétique de Charlie, terme à terme

Reprenons une entité ordinaire, à faim 72, social 0,2, énergie 40, avec une réserve accessible et un taux de succès de 0,8 sur les deux actions. Les constantes sont celles par défaut : coût de déplacement 0,03, avarice et sociabilité neutres (1,0), donc modulateurs de 1,5 pour les deux actions.

| Terme | Manger | Socialiser |
| :-- | --: | --: |
| Bénéfice `B` | `min(72,100) × 0,6` = 43,20 | `0,2 × 60` = 12,00 |
| Coût `C` | 0,03 | 0,03 |
| Risque `R` | 0,10 | 0,10 |
| Solde `B − C − R` | 43,07 | 11,87 |
| Confiance `0,5 × (0,5 + 0,8 × 0,5)` | 0,45 | 0,45 |
| Personnalité `0,5 + trait` | 1,50 | 1,50 |
| Produit | 29,07 | 8,01 |
| Urgence | ≈ 18,01 | ≈ 0,95 |
| **Utilité** | **≈ 47,1** | **≈ 9,0** |

Manger gagne, et l'écart est large. Ce qui est instructif n'est pas le résultat, c'est sa **décomposition** : 29 des 47 points de l'utilité de manger viennent du solde délibératif, et 18 de l'urgence. Chez la socialisation, l'urgence est presque nulle (0,95) et le solde ne suffit pas. Le calcul montre donc que la faim n'a pas « gagné » parce qu'elle est prioritaire par nature, mais parce que sa contribution d'urgence est forte et son bénéfice élevé. Modifiez le taux de dérive de la faim, et le résultat change : la priorité n'est nulle part écrite, elle est calculée.

## 9. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré comment six manques, huit traits et une information douteuse deviennent une action. La formule d'utilité est le mécanisme central du projet, parce qu'elle est le seul endroit où l'entité **arbitre**. Elle ne contient aucune règle du type « si faim alors manger » : le mot « si » n'y apparaît qu'à l'intérieur des bénéfices par action, jamais dans la décision. La décision est un classement, et le classement dépend de l'état. C'est précisément ce qui produit des comportements non programmés : l'ordre des priorités est une **conséquence** des nombres, et non une instruction.

Le risque mérite d'être vu comme une pièce du même dispositif. En pénalisant forfaitairement l'exploration, la recherche de nourriture et l'attaque, il donne un prix aux conduites aventureuses et empêche le monde de se réduire à une ruée permanente. Il crée donc une tension permanente entre le besoin et la prudence, tension dont les résolutions, run après run, sont observables.

**Ce que ce chapitre ne permet pas de conclure.**

- Les bénéfices, coûts et risques sont des constantes calibrées, non des grandeurs mesurées : ils traduisent des choix de conception.
- Le risque n'est **pas individualisé** : deux entités de bravoure différente affrontent la même pénalité. Le pessimisme, qui aurait porté une estimation subjective, n'est pas encore consommé.
- Aucune action de la V0.1 ne modélise un vrai danger (prédateur, blessure, échec matériel) : le risque est un prix, pas un événement.
- La confiance d'une action est bornée entre 0,25 et 0,5 : elle ne peut donc jamais renverser le solde délibératif. Ce choix protège la stabilité, mais il empêche aussi une entité d'être « sûre d'elle » au point de changer d'avis sur cette base seule.
- L'hystérésis (0,05) et la marge d'interruption (10) sont des valeurs héritées, non éprouvées par une campagne de calibration.
