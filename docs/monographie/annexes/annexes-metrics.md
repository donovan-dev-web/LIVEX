# Annexe B. Dictionnaire des métriques

Cette annexe rassemble les métriques d'ECHOS dans leur forme courante. Le catalogue versionné (version 2.0.0, soixante et une fiches) fait foi ; en cas d'écart avec cette annexe, le catalogue et le contrat d'API priment. Chaque métrique porte un statut : mesurée (calculée sur les données présentes) ou exploratoire (heuristique ou composite non calibré).

Le rappel des états de données s'applique à toutes les lignes : zéro observé, fenêtre vide, couverture insuffisante, absent, non mesuré (repli neutre) et censuré (observation interrompue). Un zéro non mesuré ne doit jamais être affiché comme un zéro observé.

## B.1 Diversité cognitive

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Diversité des croyances | bits | mesurée |
| Diversité des croyances normalisée | fraction | mesurée |
| Désaccord de croyance | fraction | mesurée |
| Variance de confiance | confiance au carré | mesurée |
| Diversité des objectifs | bits | mesurée |
| Diversité des objectifs normalisée | fraction | mesurée |
| Convergence des objectifs | fraction | mesurée |
| Couverture des objectifs | fraction | mesurée |
| Diversité d'action | fraction | mesurée |
| Nombre de décisions | comptage | mesurée |
| Âge moyen des objectifs | ticks | mesurée |
| Diversité d'expression des traits | trait au carré | mesurée |

## B.2 Propagation de l'information

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Volume de messages | messages par entité et par tick | mesurée |
| Délai de couverture des émetteurs | ticks | mesurée |
| Décroissance théorique par saut | fraction | exploratoire |
| Nombre maximal de sauts | sauts | mesurée |
| Concentration de l'émetteur | fraction | mesurée |
| Couverture des émetteurs | fraction | mesurée |

## B.3 Complexité sociale

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Confiance moyenne | confiance | mesurée |
| Variance de confiance (réseau) | confiance au carré | mesurée |
| Densité du réseau | fraction | mesurée |
| Coefficient de clustering | fraction | mesurée |
| Degré sortant moyen | fraction | mesurée |
| Nombre de communautés | comptage | mesurée |
| Correspondance de taille des communautés | fraction | mesurée |

## B.4 Convergence des objectifs

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Alignement global des objectifs | fraction | mesurée |
| Diversité des objectifs (rappel) | bits | mesurée |
| Concordance de catégories de buts | fraction | mesurée |
| Comptages par catégorie d'objectif | non numérique | mesurée |

## B.5 Répétitions d'action

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Paires d'actions répétées | comptage | exploratoire |
| Part des répétitions d'action | fraction | exploratoire |
| Équilibre de distribution des actions | fraction | exploratoire |
| Répétitions amplifiées | comptage | exploratoire |
| Comptages de répétitions par action | non numérique | exploratoire |

Ce moteur émet un avertissement constant : il compte des répétitions, pas des boucles causales.

## B.6 Durabilité des ressources

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Taux de remplissage des réserves | fraction | mesurée |
| Nombre de réserves critiques | comptage | mesurée |
| Couverture des réserves | fraction | mesurée |
| Consommation par tick | unité par tick | mesurée |
| Temps de récupération | ticks | mesurée |
| Épisodes de récupération | comptage | mesurée |
| Crises non résolues | comptage | mesurée |

## B.7 Dynamique des groupes

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Communautés inférées | comptage | mesurée |
| Taille moyenne de communauté | entités | mesurée |
| Couverture de communauté | fraction | mesurée |
| Durée de vie moyenne de groupe | ticks | mesurée |
| Taux de formation de groupe | groupes par mille ticks | mesurée |
| Taux de dissolution de groupe | groupes par mille ticks | mesurée |
| Nombre de formations | comptage | mesurée |
| Nombre de dissolutions | comptage | mesurée |
| Part de succès des groupes dissous | fraction | exploratoire |
| Sorties de membres par dissolution | membres | exploratoire |

Avertissement : ces communautés sont inférées du graphe de confiance, distinctes des groupes déclarés par le moteur. Les deux ne sont jamais additionnés.

## B.8 Indicateurs d'émergence

| Métrique | Unité | Statut |
| :-- | :-- | :-- |
| Phénomènes détectés | non numérique | exploratoire |
| Avertissement | non numérique | mesurée |
| Score d'émergence | fraction | exploratoire |
| Contribution : diversité des croyances | fraction | exploratoire |
| Contribution : diversité des objectifs | fraction | exploratoire |
| Contribution : couverture des émetteurs | fraction | exploratoire |
| Contribution : clustering | fraction | exploratoire |
| Contribution : répétitions d'action | fraction | exploratoire |
| Contribution : couverture de communauté | fraction | exploratoire |
| Complexité du système | fraction | exploratoire |

L'invariant clé : la somme des six contributions égale exactement le score d'émergence.

## B.9 Méta-métriques de reproductibilité

| Méta-métrique | Définition |
| :-- | :-- |
| Reproductible | même graine, même version, même empreinte de contenu |
| Score de reproductibilité | 1,0 si reproductible, sinon décroissance selon les écarts |
| Écart cognitif | écart normalisé entre distributions de croyances |
| Écart social | écart normalisé entre réseaux de confiance |

## B.10 Fenêtres et seuils hérités

Plusieurs fenêtres et seuils sont hérités de la conception et non encore calibrés : fenêtre de décisions, seuil de répétition, seuils de criticité des ressources, horizon de diffusion. Ils ne doivent pas être présentés comme des références avant campagne de calibration. Toute modification d'une formule, d'une unité ou d'un seuil suit une revue et augmente la version du catalogue.
