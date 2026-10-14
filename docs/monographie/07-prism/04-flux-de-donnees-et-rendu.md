# PRISM IV. Le flux de données et le rendu

Recevoir des données et dessiner un monde sont deux problèmes distincts. Ce chapitre décrit le contrat de lecture du rendu : quel message fait autorité, comment un instantané se projette dans une scène, et pourquoi un affichage peut mentir alors même que les données sont justes.

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande des structures **observables**. Or un affichage n'est pas neutre : il sélectionne, lisse, amplifie. Une couleur mal choisie peut faire croire à une alliance qui n'existe pas ; une interpolation trop généreuse peut transformer une oscillation en trajectoire fluide. Ce chapitre est donc celui où le projet pose une règle de lecture visuelle, afin que ce que le spectateur croit voir corresponde à ce que le monde a produit.

## 1. L'instantané fait autorité

Le monde envoie trois sortes de messages : le monde initialisé (une fois), les instantanés (un par tick), et les événements, dont les mutations de monde. Leur statut n'est pas le même.

| Message | Fréquence | Statut |
| :-- | :-- | :-- |
| Monde initialisé | une fois, avant les instantanés | topologie : taille, échelle, cadence, obstacles |
| Instantané | un par tick | **source de vérité** de l'état courant |
| Deltas de monde | ponctuel | mutations (obstacles), utile au journal |
| Événements typés | ponctuel | décisions, actions, communications, groupes |

La règle de consommation est explicite et elle a une raison précise : **l'instantané est la référence**, les événements servent au journal et aux effets ponctuels. Un événement peut décrire une modification qui figure **aussi** dans l'instantané du même tick ; l'appliquer une seconde fois produirait un doublon. Le cas typique est un obstacle détruit : l'événement le signale, et l'instantané suivant ne le contient plus. Appliquer les deux conduit à supprimer deux fois la même chose.

Cette règle n'est pas un détail d'implémentation : elle définit ce que le spectateur voit comme **vrai**. Un rendu qui recomposerait l'état à partir des seuls événements dériverait inexorablement, parce qu'un flux d'événements peut perdre un message là où un instantané complet se corrige à chaque tick.

## 2. Construire le monde avant le premier tick

L'ordre de la séquence de préparation n'est pas protocolaire : il est structurel. Le rendu doit **générer son décor** avant que le premier instantané n'arrive, à partir de la topologie du monde initialisé.

Un piège connu est documenté par le projet : la liste des obstacles peut être **vide** dans un monde valide. Un rendu qui conclurait « aucun obstacle reçu, donc le monde n'est pas prêt » attendrait indéfiniment, ou pire, générerait un décor sans obstacles puis les verrait apparaître. La règle est donc de distinguer **l'absence de donnée** de **la donnée vide**, la même distinction, transposée au rendu, que celle qui gouverne l'observatoire entre un zéro observé et un zéro non mesuré. Un monde sans obstacle est un monde où les entités voient loin ; ce n'est pas une erreur de transport.

## 3. Projeter un instantané

La projection d'un instantané dans une scène suit des étapes stables.

- **Les entités** : chaque entité du monde possède un acteur dans la scène, identifié de façon persistante. Une entité qui disparaît (mort) libère son acteur ; une entité qui naît en crée un.
- **Les positions** : converties par le facteur d'échelle `k`. La conversion est unique et centralisée : deux conversions divergentes dans deux graphes produiraient un monde en double.
- **Les groupes** : la couleur d'une entité dérive de son appartenance à un groupe déclaré par le monde. Le rendu **n'infère pas** de communauté : il affiche celles que le moteur a formées.
- **Les états internes lisibles** : besoins, objectifs et croyances peuvent être projetés en indicateurs (barres, teintes, panneaux), mais uniquement comme **lecture**, jamais comme source.
- **Les pulsations** : le message est un signal public et local ; sa portée est une donnée du monde, pas une décision du rendu.

Un principe traverse ces étapes : **le rendu ne déduit rien**. Une valeur qu'il ne reçoit pas reste absente ; une couleur qu'il invente serait une interprétation déguisée en donnée. C'est la traduction visuelle de la règle d'or : un affichage montre un monde, il ne le déclare pas.

## 4. La cadence de rendu n'est pas celle du monde

Le monde avance par ticks ; l'écran se rafraîchit à sa propre cadence. Le rendu doit donc **interpoler** entre deux états reçus.

Cette interpolation a trois conséquences qu'il faut assumer.

D'abord, elle **adoucit** les trajectoires : une entité qui change de direction brusquement apparaîtra en courbe. Le spectateur qui mesure une vitesse sur l'image mesure la courbe, non le tick.
Ensuite, elle **retarde** légèrement l'image : le monde affiché est toujours en retard d'un intervalle d'interpolation.
Enfin, elle ne doit **jamais** être réinjectée dans le monde : l'affichage ne produit pas d'état. Un rendu qui renverrait sa position interpolée comme une correction serait une source de non-déterminisme, exactement ce que le projet interdit.

Il faut donc lire un rendu comme une **traduction** : fidèle sur les positions, approximative sur les instants, muette sur les décisions. Quand une question porte sur un instant précis, la trace du moteur fait foi, pas l'image.

## 5. Ce que le rendu ne doit pas faire

Trois interdits structurent cette partie, et ils reprennent les interdits du projet.

- **Ne pas décider** : aucune règle du rendu ne modifie le monde. Un décor ne change pas un comportement.
- **Ne pas inférer** : aucune structure (groupe, communauté, réseau) ne doit être dessinée si le monde ne l'a pas déclarée.
- **Ne pas promettre** : une visualisation ne prouve rien. Un joli rassemblement à l'écran n'est pas une société ; c'est une image, qui doit être lue avec les mêmes précautions que n'importe quelle métrique.

Le dernier interdit est le plus important, parce qu'il concerne l'humain plutôt que la machine : une image frappe plus qu'un tableau, et c'est exactement pourquoi elle doit être encadrée.

## 6. Ce que ce chapitre apporte à la question directrice

Ce chapitre a établi comment un monde devient visible **sans devenir faux**. La hiérarchie des messages (l'instantané fait autorité), la construction du décor avant le premier tick, l'interdiction d'inférer et la nécessité d'assumer l'interpolation forment ensemble une règle de lecture : ce que le spectateur voit est une traduction du monde, avec ses conventions explicites, et non une interprétation du rendu.

C'est ce qui permet de dire qu'une structure observée à l'écran l'a bien été **dans le monde**. Sans cette règle, le rendu serait le lieu idéal d'une illusion confortable : montrer une coordination qui n'existe pas.

**Ce que ce chapitre ne permet pas de conclure.**

- La fidélité visuelle n'est pas mesurée : rien n'établit qu'un spectateur juge correctement une structure à partir de l'image.
- L'interpolation n'est pas validée comme reconstruction exacte des trajectoires entre deux ticks.
- Les choix de représentation (couleurs, indicateurs, échelles d'affichage) ne sont pas standardisés et pourraient induire des lectures différentes.
- PRISM reste verrouillé en V0.1 : ce chapitre décrit un contrat de lecture et une intégration de travail, non un rendu complet et validé.
