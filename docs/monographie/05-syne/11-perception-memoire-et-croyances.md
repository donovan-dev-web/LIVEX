# SYNE XI. Voir, se souvenir, croire : les formules d'une information faillible

Une entité ne connaît pas le monde : elle en reçoit des impressions, elle en garde des traces, elle en tire des convictions. Ce chapitre décrit les trois mécanismes qui transforment une observation en savoir utilisable, la confiance perceptuelle, la décroissance du souvenir, la révision des croyances, et pourquoi leur imperfection est une pièce du projet, non un défaut.

**Pourquoi ce chapitre importe pour la question directrice.** Supposez un monde où chaque entité verrait tout, partout, parfaitement, et se souviendrait de tout. Ce monde serait déterministe au sens le plus pauvre : il n'y aurait qu'une seule information possible, donc une seule décision rationnelle, donc un seul comportement. Aucune structure nouvelle n'y apparaîtrait, parce que rien n'y serait incertain. La question directrice exige des comportements non programmés ; cela suppose que les entités **ne sachent pas** la même chose, et que ce qu'elles savent soit douteux. Les trois formules de ce chapitre fabriquent cette ignorance : elles sont la source de la variété.

## 1. La perception : voir flou, et pas toujours

La perception d'une entité est bornée par un rayon, filtrée par la ligne de vue, et échantillonnée dans le temps.

- **Rayon** : 50 unités par défaut (plage 20 à 70, décision n°6). Une entité ne voit que ce qui l'entoure.
- **Ligne de vue** : un obstacle sur le segment qui la sépare d'une cible masque cette cible.
- **Perception étagée** : chaque entité n'observe qu'un tick sur quatre, selon son identifiant (`id modulo 4`). Les observations du monde sont donc décalées d'une entité à l'autre.
- **Grille spatiale** : le moteur ne balaie que la fenêtre de trois par trois cellules voisines, ce qui borne le coût du calcul.

**Nom** : Confiance accordée à une observation.

**Explication globale** : Plus une chose est loin, moins l'entité en est sûre. La confiance décroît linéairement avec la distance, et s'arrête sur un plancher : une observation lointaine est peu fiable, mais jamais nulle.

**Expression mathématique** :

```math
c(d) = \mathrm{clamp}\!\left(1 - \frac{d}{r} \times 0.3,\; 0.7,\; 1.0\right)
```

avec `d` la distance observée et `r` le rayon de perception (défaut 50).

**Pourquoi cette équation** : Le coefficient 0,3 fixe la perte maximale à 30 % de la confiance, ce qui laisse une observation à la limite du rayon encore exploitable (0,7). Un plancher plus bas aurait rendu la vision lointaine inutilisable, décourageant l'exploration ; un plancher plus haut aurait aplati la distinction entre proximité et distance. La décroissance est linéaire, et non exponentielle, parce que la distance est bornée par le rayon : sur un intervalle court, une droite suffit et se lit immédiatement. Le choix d'un clamp, plutôt que d'une division par une fonction, évite toute indéfinition aux bords.

**Usage dans le projet** : La confiance est attachée à chaque observation (`confidence`) et suit la donnée jusqu'aux croyances. C'est elle qui pèse, plus tard, dans la confiance globale d'une action.

**Interprétation** : Au contact, la confiance vaut 1 ; à mi-portée, environ 0,85 ; au bord du rayon, 0,7. Deux entités qui observent la même cible depuis deux distances n'en gardent pas la même certitude, ce qui suffit à désaccorder deux savoirs, et donc, à terme, deux décisions.

## 2. La mémoire : ce qui s'efface

Tout ce qui est perçu n'est pas conservé également. Chaque souvenir a une **saillance**, et celle-ci décroît avec le temps.

**Nom** : Saillance d'un souvenir.

**Explication globale** : La force d'un souvenir suit une décroissance exponentielle depuis son instant de stockage. Le taux de décroissance dépend de la catégorie du souvenir : une observation s'efface vite, un événement moins vite, une interaction lentement.

**Expression mathématique** :

```math
s(t) = \exp\!\bigl(-\,\lambda_{c} \times (t - t_0)\bigr)
```

avec `t₀` le tick de stockage, `t` le tick courant et `λ_c` le taux de la catégorie `c` :

```math
\lambda_{\text{observation}} = 0.01 \qquad
\lambda_{\text{événement}} = 0.005 \qquad
\lambda_{\text{interaction}} = 0.002
```

**Pourquoi cette équation** : L'exponentielle présente trois propriétés commodes. Elle ne devient jamais négative. Elle ne s'annule jamais tout à fait, ce qui évite une disparition brutale. Elle est sans mémoire : le taux d'effacement à un instant donné ne dépend pas de l'âge déjà écoulé, ce qui la rend résumable par une **demi-vie**. Une catégorie supplémentaire n'exige qu'un taux supplémentaire, sans changer la formule. Enfin, l'exponentielle est le modèle le plus sobre disponible pour un oubli progressif, et elle a l'avantage d'être déjà celui des mesures psychologiques classiques.

**Usage dans le projet** : Un souvenir dont la saillance tombe sous 0,01 est **oublié** : il n'est plus rappelé. La capacité est bornée à mille souvenirs ; au-delà, le moins saillant est évincé. Les taux sont configurables par catégorie.

**Interprétation** : La demi-vie d'une observation est d'environ 69 ticks, celle d'un événement d'environ 139, celle d'une interaction d'environ 347. La durée avant oubli, quand la saillance atteint le seuil de 0,01, est environ 460 ticks pour une observation, 921 pour un événement et 2 303 pour une interaction. Autrement dit : **on oublie bien plus vite ce qu'on a vu que ce qu'on a vécu avec quelqu'un.** Cette hiérarchie n'est pas cosmétique. Elle donne un poids disproportionné aux relations dans la mémoire collective des entités : un mauvais souvenir d'un voisin pèse plus longtemps qu'une simple vue du paysage, ce qui favorise la formation de relations durables.

**Ce que cette forme doit à l'histoire.** La décroissance exponentielle de la mémoire s'inspire directement de la **courbe de l'oubli** décrite par Hermann Ebbinghaus en 1885. En mémorisant des syllabes sans signification, Ebbinghaus avait constaté que la rétention chute rapidement dans les premières heures, puis de plus en plus lentement : une forme qui ressemble à une exponentielle décroissante. LIVEX n'emprunte pas les chiffres (expérimenter sur soi ne fixe pas des taux pour une simulation), mais il emprunte la **forme** et l'idée d'une hiérarchie de rétention. La mémoire du projet est donc une courbe de l'oubli paramétrée, non une mesure du cerveau.

**Conséquences pour la question directrice.** C'est peut-être la formule la plus lourde du livre. Parce que le savoir se périme, une entité ne peut jamais se reposer sur ce qu'elle a appris : elle doit réobserver, ou recevoir une information fraîche. L'oubli crée donc un besoin permanent d'exploration et rend la communication utile, recevoir un message récent vaut mieux que se fier à un souvenir ancien. Sans oubli, une entité qui aurait appris une fois où se trouve l'eau s'y rendrait éternellement, et le monde se figerait en routines. Avec l'oubli, rien n'est acquis, et le monde ne peut pas se reposer.

## 3. Les croyances : une opinion qui se corrige

Une croyance est un fait tenu pour vrai, avec un niveau de confiance, une source et une date de péremption. Elle est « vraie » pour l'entité dès que sa confiance atteint 0,5.

**Nom** : Révision d'une croyance.

**Explication globale** : Une croyance se rapproche du signal qu'elle reçoit, dans une proportion fixée par une force de révision. La variation totale est plafonnée par pas, pour qu'un signal trompeur ne puisse pas inverser d'un coup une conviction forte.

**Expression mathématique** :

```math
b' = \mathrm{clamp}\!\Bigl(b + \mathrm{clamp}\bigl((s - b) \times \alpha,\; -\mu,\; +\mu\bigr),\; 0,\; 1\Bigr)
```

avec `b` la confiance courante, `s` le signal observé, `α` la force de révision (défaut 0,3) et `μ` le plafond de variation par pas (défaut 0,5).

À cette mise à jour continue s'ajoutent trois règles discrètes, appliquées selon la source et le contenu du signal :

```math
\begin{array}{ll}
\text{même fait, même source} & b \leftarrow \min(1.0,\; b + 0.2) \\[2pt]
\text{même fait, source différente et } b \ge 0.5 & b \leftarrow \dfrac{b + s}{2} \\[4pt]
\text{fait contradictoire} & b_{\text{concurrent}} \leftarrow \max(0.1,\; b_{\text{concurrent}} - 0.1) \\[2pt]
 & \text{puis création d'une croyance au signal}
\end{array}
```

Enfin, chaque croyance s'érode de 0,999 par tick, et une croyance dont la date de péremption (100 ticks après sa naissance) est dépassée voit sa confiance plafonnée à 0,4, c'est-à-dire ramenée **sous le seuil de vérité**.

**Pourquoi cette équation** : La forme `b + (s − b) × α` est la mise à jour la plus simple qui respecte une intuition épistémique : une croyance se déplace d'autant plus qu'elle est loin du signal, et d'autant moins qu'elle en est proche. Elle ne peut pas dépasser le signal, ne l'oscille pas autour de lui, et reste bornée. Le plafond `μ` traite un problème que cette forme laisse ouvert : un signal unique et faux pouvait, avec une force élevée, retourner une opinion. Le plafond garantit qu'aucune preuve isolée ne renverse une conviction établie, ce qui est une exigence de prudence, pas de vérité. La règle d'alignement (+0,2) récompense la confirmation par la même source ; la règle de moyenne traite l'accord entre sources différentes ; la pénalité (−0,1) traite la contradiction, en affaiblissant les croyances concurrentes sans les supprimer, une entité garde donc la trace de ce qu'elle croyait.

**Usage dans le projet** : Les croyances alimentent la génération des objectifs, la confiance d'une action, la détection des buts partagés entre entités et, à terme, les liens sociaux. Elles sont publiées dans la trace de décision, ce qui permet de reconstruire, après coup, pourquoi une entité a fait ce qu'elle a fait.

**Interprétation** : Une croyance à 0,5 est un seuil de bascule : en dessous, elle ne guide pas la décision ; au-dessus, elle fait autorité pour l'entité. L'expiration à 100 ticks est la règle la plus structurante : un fait appris cesse d'être fiable au bout d'une centaine de ticks, sa confiance étant plafonnée à 0,4, donc ramenée sous le seuil de vérité. Cette contrainte a une conséquence directe : l'entité ne peut pas se reposer sur un savoir ancien, elle doit le rafraîchir par une observation ou par un message. Un effet secondaire de la pénalité de conflit mérite d'être souligné : quand deux entités sont vues à deux endroits incompatibles, les deux croyances s'affaiblissent, et l'entité se retrouve dans un état d'incertitude où elle devra réobserver. Le conflit ne produit pas une erreur : il produit une vérification.

**Ce que cette forme doit à l'histoire.** La mise à jour `b + (s − b) × α` est la version la plus dépouillée d'un raisonnement bayésien : une opinion se corrige proportionnellement à la surprise. En logique, la **révision des croyances** a été formalisée par Alchourrón, Gärdenfors et Makinson (1985), qui ont montré qu'une révision rationnelle devait satisfaire quelques postulats, dont la persistance des croyances et la priorité des nouvelles informations. LIVEX n'implémente pas cette théorie : il en garde la leçon la plus utile, à savoir qu'une croyance ne se remplace pas d'un bloc, mais se corrige par étapes bornées, en gardant la trace de ce qu'elle était.

## 4. Ce que ce chapitre apporte à la question directrice

Ces trois formules ne mesurent pas l'intelligence : elles fabriquent de l'**ignorance partagée**. Parce que la perception est faillible, deux entités voient deux mondes ; parce que la mémoire s'efface, aucune ne peut se reposer sur son savoir ; parce que les croyances se corrigent par étapes et expirent, chacune finit par se tromper un peu différemment. C'est de cette dispersion que naît la variété des trajectoires. Un monde où tous sauraient tout ne produirait qu'un seul comportement ; un monde où chacun se trompe à sa manière produit des comportements qu'on n'a pas pu écrire, mais qu'on peut reconstruire, la perception, le souvenir et la croyance étant tous trois tracés.

**Ce que ce chapitre ne permet pas de conclure.**

- Les taux d'oubli, la force de révision et le plafond de variation sont des choix de modélisation, non des mesures ; ils ne prétendent pas décrire une cognition réelle.
- Rien n'établit qu'un plancher de confiance perceptuelle à 0,7 soit optimal ; il traduit une préférence pour l'exploration utile, sans preuve.
- L'expiration à 100 ticks suppose un temps homogène ; à l'échelle temporelle configurable, cette durée en ticks ne correspond pas à une durée vécue identique.
- Aucune campagne n'a encore mesuré l'effet isolé de l'oubli sur la diversité observée ; l'intuition qu'il la nourrit reste une hypothèse à tester.
