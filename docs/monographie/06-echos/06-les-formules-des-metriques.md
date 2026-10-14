# ECHOS VI. Les formules des métriques, famille par famille

Ce chapitre est le registre raisonné de l'observatoire. Il déroule, famille par famille, ce que chaque moteur calcule, avec quelle formule, et pourquoi cette forme plutôt qu'une autre. Le catalogue versionné reste la référence ; ce chapitre en est la lecture continue.

**Pourquoi ce chapitre importe pour la question directrice.** Une émergence qu'on n'a pas provoquée se reconnaît d'abord à des signes : des croyances qui se dispersent, des messages qui circulent, des réseaux qui se structurent, des objectifs qui convergent, des actions qui se répètent, des ressources qui tiennent, des groupes qui naissent et meurent. Chacun de ces signes doit devenir un nombre, sinon il reste une impression. Ce chapitre est donc celui où la question directrice devient **mesurable**, et où l'on voit, formule par formule, que chaque nombre est un choix et non une évidence.

## 1. Les constantes partagées

Sept constantes reviennent dans plusieurs familles. Elles sont publiées ensemble parce qu'elles définissent la fenêtre dans laquelle les métriques ont un sens.

| Constante | Valeur | Rôle |
| :-- | --: | :-- |
| `WINDOW_SIZE` | 100 ticks | Fenêtre glissante des répétitions d'action |
| `FREQUENCY_THRESHOLD` | 2 | Fréquence minimale pour compter une répétition |
| `AMPLIFICATION_FACTOR` | 1,5 | Seuil d'amplification au-delà de la fréquence uniforme |
| `_DECAY_PER_HOP` | 0,9 | Rétention théorique par saut (10 % de perte) |
| `_DIFFUSION_COVERAGE` | 0,8 | Part de population devant avoir émis |
| `_CRITICAL_RATIO` | 0,2 | Seuil de crise : part de capacité sous 20 % |
| `_RECOVERED_RATIO` | 0,8 | Seuil de retour : part de capacité au-dessus de 80 % |
| `_RATE_WINDOW_TICKS` | 1000 | Fenêtre de référence des taux normalisés |

Une remarque sur cette dernière constante, qui a une raison précise : les taux de formation et de dissolution de groupes sont rapportés à mille ticks. Un comptage brut aurait crû mécaniquement avec la longueur du run, rendant deux runs de durées différentes incomparables ; le taux normalisé corrige ce biais. C'est le même grief qui a fait réécrire la complexité du système (ECHOS VIII), et il mérite d'être retenu comme une règle générale : **un comptage brut n'est pas une mesure comparable**.

## 2. Diversité cognitive : douze métriques

Cette famille mesure la dispersion des croyances, des objectifs et des actions. Elle est la plus dense du catalogue.

```math
\mathrm{BeliefDiversity} = H(P_b) = -\sum_i p_i \cdot \log_2(p_i) \qquad
\mathrm{BeliefDiversityNorm} = \frac{H(P_b)}{\log_2(k_b)}
```

L'entropie brute mesure la variété des triplets de croyances observés ; la version normalisée divise par le maximum atteignable.

```math
\mathrm{BeliefDisagreement} = \frac{1}{|F_{\mathrm{div}}|} \sum_{f \in F_{\mathrm{div}}} \frac{n_f - m_f}{n_f} \qquad
\mathrm{BeliefConfidenceVariance} = \frac{1}{N_c} \sum_j (c_j - \bar{c})^2
```

Le **désaccord** mesure la part des croyances non majoritaires, en excluant les faits unanimes : il répond à la question « y a-t-il débat ? », là où l'entropie répond à « y a-t-il variété ? ». La **variance des confiances** mesure si les entités croient leurs informations avec la même intensité.

```math
\mathrm{GoalDiversity} = H(P_g) \qquad
\mathrm{GoalDiversityNorm} = \frac{H(P_g)}{\log_2(k_g)} \qquad
\mathrm{GoalConvergence} = \frac{\max_i c_i}{n} \qquad
\mathrm{GoalCoverage} = \frac{|\{a : \mathrm{goals}(a) \neq \emptyset\}|}{n}
```

Ici apparaît un couple utile : la **diversité** des objectifs et leur **convergence** ne varient pas dans le même sens. Un monde peut être divers (beaucoup de buts différents) tout en ayant une forte part de population dans un but majoritaire ; les deux métriques se lisent ensemble.

```math
\mathrm{ActionDiversity} = \frac{H(P_a)}{\log_2(k_a)} \qquad
\mathrm{DecisionCount} = |\{e : \mathrm{type}(e) = \texttt{decision\_made}\}| \qquad
\mathrm{AverageGoalAge} = \frac{1}{|G|} \sum_{g \in G} \mathrm{age}(g)
```

L'**âge moyen des objectifs** mérite une justification : il distingue un monde qui décide souvent de choses nouvelles d'un monde qui poursuit longtemps les mêmes. Il entre dans la composante de persistance de l'indice de viabilité (ECHOS VII).

```math
\mathrm{TraitExpressionDiversity} = \frac{1}{|\mathcal{T}|} \sum_{t \in \mathcal{T}} \sigma_t^2
```

Cette dernière métrique mesure la variance moyenne des traits exprimés dans la population. Elle est le thermomètre de la diversité des tempéraments décrite au chapitre SYNE IX : si elle baisse, la population s'uniformise.

**Pourquoi douze métriques pour une seule famille.** Parce que la « diversité » n'est pas une grandeur : c'est un mot qui recouvre plusieurs questions distinctes. Les confondre produirait un chiffre unique, flatteur et faux. Le catalogue a donc préféré douze nombres précis à un indice vague.

## 3. Propagation de l'information

Six métriques décrivent la circulation des pulsations.

```math
\mathrm{MessageVolume} = \frac{|\{m \in M : \mathrm{tick}(m) = \mathrm{tick}_{\mathrm{courant}}\}|}{n} \qquad
\mathrm{EmitterCoverageDelay} = \mathrm{tick}_{80\%} - \mathrm{tick}_{\mathrm{premier\,message}}
```

Le volume rapporte les messages du tick à la population ; le **délai de couverture des émetteurs** mesure le temps qu'il faut pour que 80 % des entités aient émis au moins une fois. Ce dernier indicateur porte un avertissement qu'il faut lire en entier : il mesure l'**émission**, pas la réception. Un monde où tout le monde parle vite n'est pas un monde où tout le monde s'entend. Le nom de la métrique a d'ailleurs été corrigé pour cette raison.

```math
\mathrm{TheoreticalHopDecay} = \frac{1}{|H|} \sum_{h \in H} \left(1 - 0.9^{\,h}\right) \qquad
\mathrm{MaxMessageHops} = \max_{m \in M} \mathrm{hops}(m)
```

La **décroissance théorique par saut** est une mesure de modèle : elle applique la règle de dégradation (10 % de perte par relais) aux sauts observés. Elle ne mesure pas une dégradation constatée, mais ce que le modèle prédit pour la profondeur effectivement atteinte.

```math
\mathrm{SenderConcentration} = \frac{\max_a c_a}{|M|} \qquad
\mathrm{SenderCoverage} = \frac{|\{a : a \text{ a émis} \geq 1 \text{ message}\}|}{n}
```

La **concentration de l'émetteur dominant** mesure si un seul émetteur occupe l'espace sonore. Elle porte elle aussi un avertissement de nom : elle ne mesure **aucune centralité de réseau**, seulement la part des émissions attribuée à l'entité la plus bavarde.

## 4. Complexité sociale

```math
\mathrm{AverageTrustLevel} = \frac{1}{|R|} \sum_{r \in R} \mathrm{trust}(r) \qquad
\mathrm{TrustVariance} = \frac{1}{|R|} \sum_{r \in R} \left(\mathrm{trust}(r) - \bar{T}\right)^2
```

```math
\mathrm{NetworkDensity} = \frac{|E|}{n \cdot (n - 1) / 2} \qquad
\mathrm{AverageOutDegree} = \frac{1}{n} \sum_{a} \frac{\deg^+(a)}{n - 1}
```

La **densité** compare les arêtes uniques observées au nombre de paires possibles, `n(n−1)/2`. La convention des arêtes uniques est importante : une relation de confiance est une **paire**, pas deux arcs, sinon la densité doublerait artificiellement selon la façon de compter les directions.

```math
\mathrm{ClusteringCoefficient} = \frac{1}{n} \sum_a C_a \qquad
C_a = \begin{cases} \dfrac{t_a}{d_a (d_a - 1)/2} & \text{si } d_a \ge 2 \\[6pt] 0 & \text{sinon} \end{cases}
```

Le coefficient de clustering mesure à quel point les voisins d'une entité sont eux-mêmes voisins. Il se calcule sur un voisinage **symétrisé**, ce qui évite qu'une relation dirigée produise une valeur ambiguë. Une entité isolée ou à un seul voisin obtient zéro plutôt que d'être exclue, afin que la moyenne porte sur toute la population.

```math
\mathrm{CommunitySizeMatch} = \frac{|\{s \in S_{\mathrm{hist}} : s \in S_{\mathrm{courant}}\}|}{|S_{\mathrm{hist}}|}
```

La **correspondance de taille** compare les tailles de communautés observées à celles de l'historique. Elle répond à une question précise : les structures qui existent ressemblent-elles à celles qui existaient ?

Les communautés ne sont pas déclarées par le monde : elles sont **inférées** à partir du graphe de confiance, par une propagation d'étiquettes déterministe.

```math
\mathrm{labels}^{(t+1)}(a) = \underset{\ell \in L(a)}{\mathrm{argmin}}\left(-\mathrm{count}(\ell),\; \ell\right)
```

Une entité adopte l'étiquette majoritaire dans son voisinage, les égalités étant tranchées par un critère stable. La méthode est déterministe et sans paramètre de résolution : elle ne suppose pas un nombre de communautés, mais elle est sensible à la densité du graphe. C'est assumé.

## 5. Convergence des objectifs

```math
\mathrm{GlobalGoalAlignment} = \frac{\max_i c_i}{n} \qquad
\mathrm{GoalCategoryConcordance} = \sum_i p_i^2 \qquad
\mathrm{GoalTypeCounts} = \{\text{catégorie}_i \mapsto c_i\}
```

L'alignement global est la part de la population dans la catégorie majoritaire ; la concordance est l'indice de concordance appliqué aux catégories de buts. Cette dernière a porté un autre nom, « potentiel de coopération », jusqu'à ce qu'on constate qu'elle ne testait aucune compatibilité par paire : elle mesure une **concordance de catégories**, et son nouveau nom le dit.

## 6. Répétitions d'action

```math
\mathrm{RepeatedActionPairs} = |\{(a, \alpha) : \mathrm{freq}(a, \alpha) > 2\}|
```

```math
\mathrm{RepeatedActionShare} = \frac{1}{|L|} \sum_{(a,\alpha) \in L} \frac{\mathrm{freq}(a, \alpha)}{W}
```

```math
\mathrm{ActionDistributionBalance} = \mathrm{clamp}\left(1 - \sum_i \left|p_i - \tfrac{1}{k}\right|,\; 0,\; 1\right)
```

```math
\mathrm{AmplifiedRepetitions} = \left|\left\{(a,\alpha) \in L : \mathrm{freq}(a,\alpha) / f_{\mathrm{attendue}} > 1.5\right\}\right|
```

Cette famille porte l'avertissement le plus explicite du catalogue. Un nom initial, « détecteur de boucles de rétroaction », a été corrigé : ces métriques comptent des **répétitions**, elles ne démontrent **aucune causalité circulaire**. Compter qu'une entité répète la même action dix fois dans la fenêtre n'établit pas qu'elle le fait parce que son action a modifié le monde. Le nom a été changé pour que la métrique dise la vérité.

L'**équilibre de la distribution** mérite un mot : il vaut 1 quand toutes les actions sont également fréquentes, 0 quand une seule accapare tout. Il est calculé par la distance L1 entre la distribution observée et la distribution uniforme, ce qui est une mesure d'écart, non une entropie.

## 7. Durabilité des ressources

```math
\mathrm{ResourceFillRatio} = \frac{1}{|R_c|} \sum_{r \in R_c} \mathrm{clamp}\left(\frac{\mathrm{quantity}(r)}{\mathrm{capacity}(r)},\; 0,\; 1\right)
```

```math
\mathrm{CriticalResourceCount} = |\{r \in R_c : \mathrm{quantity}(r)/\mathrm{capacity}(r) < 0.2\}|
```

```math
\mathrm{ConsumptionPerTick} = \frac{\sum_{e \in E_c} \mathrm{amount}(e)}{T_{\mathrm{fenêtre}}} \qquad
\mathrm{ResourceCoverage} = \frac{|R_c|}{|R|}
```

```math
\mathrm{RecoveryTime} = \frac{1}{|C|} \sum_{c \in C} \left(\mathrm{tick}_{\mathrm{résolution}}(c) - \mathrm{tick}_{\mathrm{ouverture}}(c)\right)
```

```math
\mathrm{RecoveryEpisodes} = |C| \qquad
\mathrm{UnresolvedCrisisCount} = \begin{cases} 1 & \text{si aucune résolution observée} \\ 0 & \text{sinon} \end{cases}
```

Deux définitions structurent cette famille : une **crise** est une réserve sous 20 % de sa capacité ; un **retour** est un passage au-dessus de 80 %. L'écart entre les deux seuils n'est pas une négligence : il évite qu'une réserve qui oscille autour de la frontière produise une suite de crises et de résolutions artificielles. Le comptage des crises non résolues est explicitement **censuré** : une crise encore ouverte au dernier tick observé n'est pas un échec, c'est une observation incomplète, et le catalogue interdit de la présenter comme un zéro rassurant.

## 8. Dynamique des groupes

```math
\mathrm{InferredCommunities} = |\{g \in G : |g| \ge 2\}| \qquad
\mathrm{AverageCommunitySize} = \frac{1}{|G|} \sum_{g \in G} |g| \qquad
\mathrm{CommunityCoverage} = \frac{\sum_{g \in G} |g|}{n}
```

```math
\mathrm{AverageGroupLifetime} = \frac{1}{|D|} \sum_{d \in D} \mathrm{lifetime}(d) \qquad
\mathrm{GroupFormationRate} = |\mathrm{formed}| \cdot \frac{1000}{\max(1, T_{\mathrm{fenêtre}})}
```

```math
\mathrm{DissolvedGroupSuccessShare} = \frac{1}{|D_s|} \sum_{d \in D_s} \mathrm{success}(d) \qquad
\mathrm{MemberExitsPerDissolution} = \frac{1}{|D_e|} \sum_{d \in D_e} \mathrm{membersOut}(d)
```

**La distinction la plus importante de tout le catalogue.** Il existe deux ensembles de structures sociales, et il serait faux de les additionner : les **groupes déclarés** par le moteur SYNE, nés d'un constat de cohésion, et les **communautés inférées** par l'analyse à partir du graphe de confiance. Un groupe déclaré est une structure que le monde a formée ; une communauté inférée est une structure que l'observateur a déduite. Le catalogue les sépare explicitement, et les métriques de l'un ne se mélangent jamais avec celles de l'autre. Confondre les deux reviendrait à attribuer au monde une découverte de l'instrument.

## 9. Ce que ce chapitre apporte à la question directrice

Ce chapitre a transformé sept intuitions en une cinquantaine de nombres. Chacun répond à une question distincte, chacun est borné, chacun est publié avec sa provenance. Ensemble, ils forment la grille par laquelle le projet peut dire non seulement **si** quelque chose apparaît, mais **à quel rythme**, **à quelle échelle** et **avec quelle stabilité**. La question directrice demande de reconnaître des structures non programmées ; ce chapitre établit ce que « reconnaître » veut dire en pratique.

**Ce que ce chapitre ne permet pas de conclure.**

- Aucune de ces métriques ne mesure une causalité : elles comptent, elles ne démontrent pas.
- Les seuils et les constantes sont majoritairement hérités et non calibrés ; un phénomène peut être réel et non détecté par manque de métrique.
- Plusieurs familles mesurent ce qui est **instrumenté**, non ce qui est important : le biais de conception est une limite structurelle de tout observatoire.
- Une valeur élevée n'est jamais un verdict : la dispersion, la densité ou la répétition sont des descriptions, pas des jugements.
