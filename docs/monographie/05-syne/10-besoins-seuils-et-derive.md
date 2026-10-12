# SYNE X. Les besoins : six manques, une énergie, des seuils

Une entité qui n'aurait besoin de rien n'agirait jamais. Ce chapitre décrit les six besoins de SYNE, l'énergie qui les rend possibles, la manière dont ils dérivent au fil des ticks, et les seuils à partir desquels ils deviennent des désirs.

**Pourquoi ce chapitre importe pour la question directrice.** Imaginez un moteur qui tourne à vide : il ne produit rien tant que rien ne l'écarte de son équilibre. Les besoins sont ce qui écarte le monde de son équilibre, tick après tick, sans qu'aucun scénario n'ait été écrit. Ils répondent à la première moitié de la question directrice (des **lois fondamentales**, locales et aveugles) en fournissant la seule loi qui ne s'arrête jamais : le manque. Un monde sans besoins se figerait dès la première seconde ; un monde avec des besoins ne peut pas s'arrêter, parce qu'il y a toujours quelque chose à combler.

## 1. Six manques et une réserve

Chaque entité porte sept grandeurs internes. Les six premières sont ses **besoins** ; la septième est son **énergie**, qui n'est pas un besoin mais la monnaie de l'action.

| Grandeur | Symbole | Plage | Sens |
| :-- | :-- | :-- | :-- |
| Faim | `hunger` | 0 à 100 | croît avec le jeûne |
| Soif | `thirst` | 0 à 100 | croît avec la déshydratation |
| Fatigue | `fatigue` | 0 à 100 | croît avec l'effort accumulé |
| Sécurité | `safety` | 0 à 1 | décroît avec la menace ; 1 = serein |
| Social | `social` | 0 à 1 | croît avec l'isolement |
| Curiosité | `curiosity` | 0 à 1 | croît avec l'ennui |
| Énergie | `energy` | 0 à 100 | décroît avec l'effort ; 0 = mort |

Deux familles d'échelles coexistent, et la distinction n'est pas cosmétique. Faim, soif et fatigue sont des **quantités** de 0 à 100, dont on peut lire la gravité directement dans le nombre. Sécurité, social et curiosité sont des **taux** de 0 à 1, dont le sens est inverse : pour la sécurité, une valeur haute est bonne ; pour le social et la curiosité, une valeur haute est un manque. Cette hétérogénéité est un reste de l'histoire du projet, où la sécurité et le social ont été ajoutés après les besoins physiologiques. Elle est assumée et documentée, mais elle impose une vigilance : comparer une valeur de faim à une valeur de curiosité n'a aucun sens avant de les avoir ramenées à une échelle commune, ce que fait la fonction d'élan décrite plus bas.

**Ce que cette liste doit à l'histoire.** Les six besoins reprennent une intuition ancienne de la physiologie et de la psychologie : celle de l'**homéostasie**, formulée par Walter Cannon au début du vingtième siècle, selon laquelle un organisme vivant travaille à maintenir ses grandeurs internes dans une plage tolérable. À cette idée s'ajoute celle de la **réduction de pulsion** de Clark Hull, pour qui un manque interne produit une tension, et la tension une conduite. LIVEX n'emprunte pas la théorie : il en garde l'architecture minimale, un état interne qui dérive et un seuil qui déclenche. La hiérarchie de Maslow a été écartée, car elle suppose des priorités ordonnées, alors que le projet veut précisément que l'ordre des priorités **émerge** du calcul plutôt que d'être décrété.

## 2. La dérive : pourquoi un manque grandit tout seul

Le mécanisme central est d'une simplicité volontaire : à chaque tick, chaque besoin augmente d'un pas fixe, puis est ramené dans sa plage.

**Nom** : Dérive par tick d'un besoin.

**Explication globale** : La faim, la soif et la fatigue avancent d'un taux constant. La sécurité dérive vers l'insécurité, le social vers l'isolement, la curiosité vers l'ennui. L'énergie, elle, ne dérive pas : elle ne change que par l'action.

**Expression mathématique** :

```math
n(t+1) = \mathrm{clamp}\bigl(n(t) + r_n,\; 0,\; n_{\max}\bigr) \qquad
\begin{array}{ll}
n_{\max} = 100 & n \in \{\text{hunger}, \text{thirst}, \text{fatigue}, \text{energy}\} \\[2pt]
n_{\max} = 1 & n \in \{\text{safety}, \text{social}, \text{curiosity}\}
\end{array}
```

avec, pour les valeurs par défaut :

```math
r_{\text{hunger}} = 0.5 \quad r_{\text{thirst}} = 0.7 \quad r_{\text{fatigue}} = 0.3
```

```math
r_{\text{safety}} = 0.001 \quad r_{\text{social}} = 0.0002 \quad r_{\text{curiosity}} = 0.0005
```

**Pourquoi cette équation** : Une dérive linéaire est la loi la plus sobre possible : elle ne dépend pas de l'état passé, ne se souvient de rien, et produit un comportement parfaitement prévisible en l'absence d'action. C'est exactement ce qu'on attend d'une **loi fondamentale** : une règle aveugle dont les conséquences ne sont pas écrites. Le taux est ce qui règle la cadence du monde. Une faim qui monte de 0,5 par tick atteint son seuil de déclenchement au bout de cent ticks ; la soif, plus rapide, au bout d'environ soixante-et-onze ; la fatigue, au bout de deux cent trente-trois. Le monde a donc plusieurs horloges simultanées, et c'est cette désynchronisation qui empêche toutes les entités de chercher la même chose au même moment.

**Usage dans le projet** : La dérive est appliquée à chaque tick, avant la perception et la décision, à l'échelle temporelle configurée. Toutes les valeurs sont configurables ; les valeurs par défaut sont celles de la calibration de référence.

**Interprétation** : Une dérive trop rapide sature les besoins et produit des entités en crise permanente, qui ne font plus que survivre. Une dérive trop lente produit un monde apathique, où rien n'oblige à bouger. Les valeurs retenues placent les seuils entre soixante et deux cents ticks, c'est-à-dire à l'échelle d'une vie d'entité.

**Une leçon de calibration, qui vaut plus qu'une formule.** Le besoin social illustre parfaitement la différence entre un paramètre plausible et un paramètre calibré. Sa dérive avait d'abord été fixée à 0,001 par tick. Avec un seuil de déclenchement à 0,7, ce besoin était franchi vers le tick 700, puis ne redescendait plus : faute de mécanisme de satisfaction, il déclenchait l'action de socialisation **en permanence**, jusqu'à 96 % des décisions en fin de course. Les entités marchaient sans gagner d'énergie, et mouraient lentement. La calibration a divisé ce taux par cinq (0,0002). Le besoin de curiosité a subi la même correction (0,002 → 0,0005), pour la même raison : il n'existe pas encore d'action qui le satisfasse. Cette histoire est instructive parce qu'elle n'est pas un détail : elle montre qu'un besoin sans exutoire devient une tyrannie, et que le projet préfère ralentir un besoin plutôt que d'inventer une action factice pour lui. Ici, la leçon vaut pour tout le livre : un mécanisme peut être juste **localement** et toxique **globalement**, et seule une campagne révèle l'écart.

## 3. Les seuils : du manque au désir

Un besoin qui dérive ne produit rien tant qu'il reste sous un seuil. Au-delà, il devient un **désir**, c'est-à-dire une action candidate soumise à la décision.

| Besoin | Seuil de déclenchement | Sens du franchissement |
| :-- | :-- | :-- |
| Faim | 50 | chercher à manger |
| Soif | 50 | chercher à boire |
| Fatigue | 70 | se reposer |
| Sécurité | 0,5 (déclenche **sous** ce niveau) | fuir |
| Social | 0,7 | socialiser |
| Curiosité | 0,3 | explorer |

Le franchissement n'est pas instantané par nature : il se produit à un tick précis, mais l'entité ne « décide » pas d'avoir faim. Elle constate un état, et cet état produit une candidature. C'est la distinction fondamentale entre le besoin et le désir : le premier est subi, le second est produit.

Un état peut aussi devenir **critique**. Deux seuils, plus hauts que les seuils de déclenchement, définissent l'urgence vitale : une faim supérieure à 85, ou une énergie inférieure à 10. Ces seuils ne servent pas à créer un désir supplémentaire, mais à autoriser une **interruption** : une entité engagée dans une action peut l'abandonner si un besoin critique survient. Nous retrouverons ces seuils dans le chapitre de la décision, car ils sont le seul mécanisme qui peut passer outre la règle d'inertie.

Il faut noter que la faim critique (85) et le seuil de déclenchement (50) ne sont pas redondants. Le premier déclenche une candidature ordinaire, qui entre en concurrence avec les autres ; le second brise une décision déjà prise. Une entité affamée à 60 peut continuer à explorer si l'exploration a une meilleure utilité ; une entité affamée à 86 doit y renoncer. Cette gradation reproduit une intuition quotidienne : on peut ignorer une faim modérée, on ne peut pas ignorer une faim extrême.

## 4. La fonction d'élan : ramener six échelles à une seule

Parce que les besoins n'ont pas tous la même échelle, l'évaluateur a besoin d'une traduction commune. C'est le rôle de l'**élan**.

```math
\mathrm{drive}(a) =
\begin{cases}
\text{hunger} & a \in \{\text{SeekFood}, \text{Eat}\} \\[2pt]
\text{thirst} & a \in \{\text{SeekWater}, \text{Drink}\} \\[2pt]
\text{fatigue} & a = \text{Rest} \\[2pt]
(1 - \text{safety}) \times 100 & a = \text{Flee} \\[2pt]
\text{social} \times 100 & a = \text{Socialize} \\[2pt]
\text{curiosity} \times 100 & a = \text{Explore}
\end{cases}
```

Cette traduction ne change rien au monde : elle rend les six besoins **comparables** au moment du calcul. Elle est aussi l'endroit où se lit l'inversion de sens du besoin social et de la curiosité : une valeur haute y produit un élan élevé, contrairement à la sécurité, où c'est la valeur basse qui presse. La fonction d'élan est ce qui permet à une entité de mettre sur la même balance sa faim et son envie d'explorer, alors que ces deux besoins sont mesurés dans deux unités différentes.

## 5. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré le ressort le plus élémentaire du monde : le manque. Une seule loi, `n(t+1) = n(t) + r`, appliquée à six grandeurs avec des cadences différentes, suffit à rendre un monde impossible à laisser tranquille. Aucun besoin n'a été écrit pour « produire » un comportement particulier ; chacun produit seulement une candidature, que la décision arbitre. C'est cette séparation entre **la cause** (un manque) et **la conduite** (une action choisie) qui rend possibles des comportements non programmés : le besoin pose la question, jamais la réponse.

La synchronisation des besoins produit d'ailleurs, à elle seule, un phénomène collectif observable. Comme la soif dérive plus vite que la faim, les entités ne se dirigent pas toutes vers les mêmes ressources au même moment : des flux se croisent, des files se forment, et des points de rencontre apparaissent aux endroits où deux ressources sont proches. Ces flux ne sont écrits nulle part ; ils résultent d'horloges individuelles désaccordées.

**Ce que ce chapitre ne permet pas de conclure.**

- Les taux de dérive ne sont pas calibrés biologiquement : ce sont des choix de rythme, à l'échelle d'une vie simulée.
- Le besoin social et le besoin de curiosité n'ont **aucune action de satisfaction** en V0.1 : leur dérive a été ralentie pour éviter qu'ils ne dominent, mais ils ne peuvent pas être comblés. Un besoin que rien ne satisfait reste un mécanisme incomplet.
- Le seuil de sécurité à 0,5 et la dérive de sécurité à 0,001 restent des valeurs héritées, non éprouvées par une campagne.
- Rien ne démontre qu'une dérive linéaire soit préférable à une dérive non linéaire ; le choix de la simplicité n'est pas un résultat expérimental.
