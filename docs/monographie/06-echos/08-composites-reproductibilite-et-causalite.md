# ECHOS VIII. Composites, reproductibilité et causalité

Mesurer une chose à la fois ne suffit pas : il faut aussi savoir la résumer, la comparer et l'expliquer. Ce chapitre traite les trois gestes qui restent, à savoir agréger, confronter deux runs et remonter une chaîne de causes, ainsi que les limites qui les accompagnent.

**Pourquoi ce chapitre importe pour la question directrice.** Reconnaître une structure non programmée ne consiste pas à pointer un événement : il faut pouvoir dire qu'un monde a produit **davantage** de complexité qu'un autre, que deux exécutions de la même graine se ressemblent, et pourquoi un groupe est apparu à ce moment-là. Un projet qui ne saurait que montrer des captures d'écran ne démontrerait rien. Ce chapitre est celui où l'observation devient une **preuve rejouable** : un résumé publié, une comparaison chiffrée, une chaîne causale reconstruite.

## 1. Le score d'émergence : un résumé publié, jamais un oracle

```math
\mathrm{EmergenceScore} = \mathrm{clamp}\left(\sum_{i=1}^{6} w_i \cdot c_i,\; 0,\; 1\right) \qquad
\sum_i w_i = 1.0
```

| Composante `c_i` | Poids `w_i` |
| :-- | --: |
| Diversité des croyances normalisée | 0,15 |
| Diversité des objectifs normalisée | 0,15 |
| Délai de couverture des émetteurs, normalisé | 0,10 |
| Coefficient de clustering | 0,15 |
| Part moyenne des répétitions d'action | 0,20 |
| Couverture de communauté | 0,25 |

**Nom** : Score d'émergence composite.

**Pourquoi cette forme** : Six composantes **déjà normalisées** sont combinées par une somme pondérée. Cette architecture répond à un problème précis : additionner des entropies brutes aurait favorisé mécaniquement les situations à nombreuses catégories, et une division finale arbitraire (présente dans un prototype antérieur) rendait le score contradictoire avec la somme de ses poids. Ici, les poids somment à un et chaque composante est bornée, donc le score est borné sans correction supplémentaire.

**Deux décisions de méthode accompagnent ce score.**

- **Les contributions sont publiées.** Chaque terme est émis comme métrique à part entière, et l'égalité `Σ Contribution_i = EmergenceScore` est un invariant testé. N'importe quelle interface peut donc décomposer le score sans le recalculer.
- **Les poids sont publiés, non codés en dur.** Une analyse de sensibilité peut les examiner sans modifier silencieusement la formule. Les valeurs actuellement retenues (0,15 ; 0,15 ; 0,10 ; 0,15 ; 0,20 ; 0,25) sont héritées et **non calibrées** : la couverture de communauté pèse le plus, suivie des répétitions d'action. C'est un choix affiché comme un choix.

**Un avertissement invariant.** Le score est une **heuristique d'observation**, de statut exploratoire, jamais une preuve scientifique d'émergence. Un champ d'avertissement est présent dans toute sortie d'indicateurs et doit être affiché tel quel par toute interface qui consomme le score. La provenance d'une sortie composite est, elle aussi, calculée depuis ses **dépendances réelles** : le score n'est marqué « non mesuré » que si l'une de ses six composantes l'est. Exiger l'ensemble des moteurs marquait « non mesuré » un score entier à cause d'une métrique incidente qui n'entrait pas dans sa formule.

## 2. La complexité du système : ce qu'elle n'est pas

```math
\mathrm{SystemComplexity} = \mathrm{clamp}\left(\frac{\mathrm{BeliefDiversityNorm} + \mathrm{GoalDiversityNorm} + \mathrm{CoverageDelay\_Norm}}{3},\; 0,\; 1\right)
```

**Pourquoi cette version bornée** : une version antérieure intégrait un délai exprimé en ticks, c'est-à-dire un nombre non borné, qui faisait croître l'indicateur avec la durée du run. Un monde plus long paraissait donc plus complexe, ce qui est absurde. La forme actuelle est une moyenne de **trois dispersions normalisées**, bornée sur `[0, 1]`, avec un test de non-régression : un délai au-delà de l'horizon de diffusion produit une contribution neutre (zéro), et la complexité plafonne alors aux deux tiers.

**Un avertissement indispensable** : ce n'est **pas** une approximation de la complexité de Kolmogorov, ni une complexité algorithmique. Le mot prêterait à confusion ; l'indicateur n'a aucun rapport avec la longueur minimale d'un programme qui engendrerait le monde. Il faut le lire pour ce qu'il est : une moyenne de trois dispersions, de statut exploratoire.

## 3. Les phénomènes auto-détectés

L'observatoire signale quelques phénomènes dont la condition est satisfaite.

```math
\text{Phénomène déclenché} \iff \forall\, (\mathrm{métrique}, \mathrm{seuil}) \in \mathrm{signaux} : \mathrm{valeur}(\mathrm{métrique}) > \mathrm{seuil}
```

| Phénomène | Condition |
| :-- | :-- |
| Franchissement de seuil de communautés | plus de deux communautés inférées |
| Répétitions d'action soutenues | plus de cinq paires (agent, action) répétées |
| Convergence forte des objectifs | convergence supérieure à 0,7 |
| Concentration des émetteurs | part de l'émetteur dominant supérieure à 0,3 |
| Dynamique organisationnelle | plus de cinq communautés **et** sorties par dissolution supérieures à 1 |

Chaque détection publie la **trace des signaux déclencheurs** : la métrique, sa valeur observée et le seuil. Deux précautions encadrent ces sorties. Les **libellés** décrivent ce qui est réellement observé, un comptage, pas une formation ; des répétitions, pas une boucle causale ; une concordance de catégories, pas une coordination. Et les **seuils** sont hérités et non calibrés : une absence de détection n'est **pas** l'absence du phénomène, et l'interface doit l'écrire à côté d'une liste vide.

## 4. La provenance : le point d'information non mesuré

Une règle traverse tout le composant, et ce chapitre est le bon endroit pour la reformuler, parce que les composites y sont particulièrement exposés.

| Affichage | Signification |
| :-- | :-- |
| `0.0`, mesuré | **zéro observé**, la formule a tourné, le résultat est nul |
| `0.0`, non mesuré | **repli neutre**, aucune valeur calculable |
| `null` | **pas d'observation** à ce tick |

La règle de consommation est sans ambiguïté : un repli neutre ne doit **jamais** être affiché comme un zéro. C'est la faute la plus grave qu'un observatoire puisse commettre : une courbe plate à zéro suggère un monde où rien ne se passe, alors qu'elle peut signifier qu'un instrument n'a rien vu. Plusieurs métriques du projet sont restées à zéro non parce que le monde était vide, mais parce que la mesure manquait. Depuis, la provenance est un champ obligatoire, publié point par point.

## 5. La reproductibilité entre deux runs

Comparer deux exécutions demande de réduire chacune à une vue comparable. Deux distributions servent de base.

```math
P_b(\mathrm{key}) = \frac{\mathrm{count}(\mathrm{key})}{\sum_{\mathrm{key}} \mathrm{count}(\mathrm{key})} \qquad
P_s(\mathrm{paire}) = \frac{w(\mathrm{paire})}{\sum_{\mathrm{paire}} w(\mathrm{paire})}
```

La première est la distribution des croyances ; la seconde, la distribution **sociale**, où le poids d'une paire est la moyenne des confiances des deux directions (ou de l'unique direction observée). Ces deux vues ont été choisies parce qu'elles sont ordonnées de façon stable et sans perte d'information sur ce qui compte : les faits tenus pour vrais, et les liens établis.

```math
d(A, B) = \min\left(1.0,\; \sqrt{\frac{\sum_{\mathrm{key}} \left(p_A(\mathrm{key}) - p_B(\mathrm{key})\right)^2}{2}}\right)
```

La distance L2 normalisée est bornée à 1 par la division par 2 et une borne explicite. Elle vaut zéro pour deux distributions identiques et un pour deux distributions sans recouvrement.

```math
\mathrm{empreinte} = \mathrm{SHA\text{-}256}\bigl(\mathrm{json.dumps}(\mathrm{vue\_canonique},\; \mathrm{sort\_keys=True},\; \mathrm{separators}=(",", ":"))\bigr)
```

L'empreinte est un condensé cryptographique de la vue canonique : deux runs identiques ont la même empreinte, et une différence, même minime, la change. Elle ne dit pas **où** se situe la différence ; c'est la distance qui le fait.

```math
\mathrm{IsReproducible} = (\mathrm{seed}_A = \mathrm{seed}_B) \land (\mathrm{version}_A = \mathrm{version}_B) \land (\mathrm{empreinte}_A = \mathrm{empreinte}_B)
```

```math
\mathrm{ReproducibilityScore} =
\begin{cases}
1.0 & \text{si } \mathrm{IsReproducible} \\[4pt]
1.0 - \dfrac{\mathrm{CognitiveDiff} + \mathrm{SocialDiff}}{2.0} & \text{sinon}
\end{cases}
```

**Pourquoi cette forme** : la reproductibilité n'est pas seulement une distance, c'est une **conjonction de conditions**. Deux runs peuvent être proches sans être reproductibles : s'ils n'ont ni la même graine ni la même version, leur ressemblance n'est pas une preuve de déterminisme. La condition stricte (graine égale, version égale, empreinte égale) tranche ; le score pondéré (moyenne des deux distances cognitive et sociale) ne sert qu'à **quantifier** l'écart quand la condition stricte échoue.

**Ce que cette mesure protège.** Le déterminisme est un contrat du projet, et la reproductibilité est la façon de le vérifier. Un écart entre deux runs de même graine signalerait une fuite : un tirage non reproductible, un ordre d'itération instable, un horodatage introduit dans un calcul. La mesure est donc moins une métrique d'émergence qu'un instrument de diagnostic du moteur.

## 6. Les statistiques post-run

```math
\mathrm{stats}(V) = \{\mathrm{count}: |V|,\; \mathrm{min}: \min(V),\; \mathrm{mean}: \mathrm{fsum}(V)/|V|,\; \mathrm{max}: \max(V)\}
```

```math
\beta = \frac{\mathrm{Cov}(x, y)}{\mathrm{Var}(x)} = \frac{\sum_i (x_i - \bar{x})(y_i - \bar{y})}{\sum_i (x_i - \bar{x})^2}
```

La pente par moindres carrés est calculée de façon déterministe, et la somme flottante (`fsum`) est utilisée pour éviter les dérives d'arrondi sur de longues séries. Deux signaux de viabilité en découlent :

```math
\mathrm{energySlopePerTick} = \beta(\text{énergie moyenne},\ 200\ \text{derniers ticks})
```

```math
\mathrm{actionSharesWhenHungry}[\alpha] = \frac{|\{\text{décisions } \alpha \text{ sur les ticks où faim} > 70\}|}{|\{\text{toutes les décisions sur ces ticks}\}|}
```

Le second est une mesure de **comportement conditionnel** : que fait une population quand elle a faim ? C'est l'un des rares indicateurs qui relie directement une condition interne à une conduite observée, et il a une valeur particulière pour la question directrice : il permet de vérifier qu'un monde ne se contente pas d'agir au hasard, mais répond à ses états.

## 7. La chaîne causale

Constater qu'un groupe est apparu ne suffit pas : la question directrice demande de pouvoir expliquer **pourquoi**. ECHOS reconstruit une chaîne de causes à partir des traces persistées, hors ligne, de façon déterministe.

```math
\mathrm{Action} \leftarrow \mathrm{Intention} \leftarrow \mathrm{Objectif} \leftarrow \mathrm{Besoin} \leftarrow \mathrm{Croyance} \leftarrow \mathrm{Mémoire} \leftarrow \mathrm{Perception}
```

Chaque couche est un nœud, alimenté par une source précise : la ligne de trace de décision du tick pour l'action, le besoin et le compteur de mémoire ; l'événement de décision pour l'intention ; le contexte d'agents le plus récent disponible pour l'objectif et les croyances ; les derniers messages reçus pour la perception. Les éléments multiples d'une couche sont agrégés dans un détail, et une couche sans donnée porte un libellé vide, la chaîne est alors signalée comme **tronquée**, plutôt que complétée par une valeur inventée.

Trois propriétés rendent cette reconstruction utilisable. Elle est **déterministe** : tous les ordres sont stables, aucun tirage n'intervient. Elle détecte les **répétitions** : une action déjà choisie aux ticks précédents marque la chaîne comme cyclique, avec la liste des ticks concernés. Enfin, elle est **bornée** : la profondeur d'affichage est plafonnée, et toute troncature est signalée. Un cache borné, invalidé lorsqu'un run est réanalysé, évite de recalculer deux fois la même chaîne.

**Les limites de l'analyse causale.** Trois obstacles sont structurels. Un phénomène social a généralement **de nombreuses causes concourantes** : la chaîne présente une ligne, pas un réseau. Les **boucles de rétroaction** rendent l'attribution difficile : qui a causé quoi, quand ? Enfin, il existe un **biais de conception** : l'observatoire observe un système que le concepteur a défini, et les catégories de mesure dépendent de ce qu'il a choisi d'instrumenter. Une chaîne reconstruite est donc une lecture argumentée, jamais une démonstration de causalité au sens expérimental.

C'est la distinction, rappelée dès le début de cette partie, entre **corrélation et causalité**. Une corrélation n'est pas suffisante pour démontrer une émergence ; la chaîne causale fait mieux, sans prétendre faire tout.

## 8. Ce que ce chapitre apporte à la question directrice

Ce chapitre a fermé la boucle de l'observation. Une mesure devient un résumé comparable, un résumé devient une comparaison de runs, une comparaison devient une reconstruction causale. La question directrice demande des comportements non programmés **et analysables** : la première moitié dépend de SYNE, la seconde de ces instruments. Un monde qui produirait des comportements surprenants que rien ne permettrait d'expliquer ne répondrait qu'à moitié à la question.

Les trois gestes de ce chapitre portent aussi une même discipline : ne jamais transformer une mesure en vérité. Le score est une heuristique avec un avertissement ; la complexité n'est pas celle de Kolmogorov ; la reproductibilité est une conjonction de conditions, pas une ressemblance ; la chaîne causale est une reconstruction, pas une preuve. C'est cette discipline, plus que les formules, qui rend l'observatoire crédible.

**Ce que ce chapitre ne permet pas de conclure.**

- Le score d'émergence et ses poids ne sont pas calibrés : une analyse de sensibilité reste à faire.
- Les seuils des phénomènes sont hérités ; l'absence de détection n'est pas l'absence du phénomène.
- La chaîne causale est déterministe mais linéaire : elle ne représente ni les causes multiples, ni les boucles.
- La reproductibilité vérifie l'absence de fuite non déterministe, elle ne prouve pas la **validité** du modèle.
- Le triomphe d'un indicateur ne prouve rien : une métrique élevée reste une description, et l'observateur doit garder l'avertissement affiché.
