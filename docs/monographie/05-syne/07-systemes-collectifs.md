# SYNE VII. Les systèmes collectifs

Voici le cœur de la question directrice : les structures collectives. Le chapitre décrit comment des groupes apparaissent sans être écrits, comment la confiance se construit et s'érode, comment les territoires, les saisons et les livres rythment le monde collectif, et comment naissances, morts et conflits complètent le cycle de vie. Aucune de ces structures n'est scriptée : toutes naissent de règles locales.

Il faut mesurer l'enjeu. Une société peut être programmée de deux manières, et les deux sont des échecs au regard de la question. On peut écrire les groupes : « au tick mille, ces entités forment une tribu ». On peut aussi écrire les conditions assez précisément pour que le résultat soit déterministe à la main : « toute entité proche d'une autre pendant cent ticks devient son alliée ». Dans les deux cas, la structure est un script déguisé. Ce que le projet cherche, c'est un troisième chemin : des règles locales si générales qu'elles ne mentionnent aucune structure, et pourtant assez riches pour qu'une structure en résulte. Ce chapitre décrit ce troisième chemin, mécanisme par mécanisme.

**Pourquoi ce chapitre importe pour la question directrice.** Voici le cœur de la question. Imaginez des villages qui se forment sans urbaniste, des alliances qui naissent sans traité, des rumeurs qui se répandent sans crieur : personne n'a écrit « fondez un groupe ». C'est exactement ce que ce chapitre cherche à produire et à mesurer. Chaque mécanisme décrit ici (confiance, cohésion, leader, territoire, naissance, mort) est local et aveugle, et pourtant il engendre des objets collectifs qu'aucune règle n'a nommés. C'est la réponse la plus concrète que le projet apporte à la seconde moitié de la question.

## 1. La confiance, brique relationnelle

Toutes les structures sociales reposent sur la confiance, relation **dirigée** entre deux entités (la confiance de A envers B n'est pas celle de B envers A).

- Une première rencontre établit une confiance initiale de 0,5.
- La confiance décroît de 0,9 par tick sans interaction.
- Une vérité constatée accorde un bonus de 0,05 ; un mensonge constaté inflige une pénalité de 0,2.
- Un engagement tenu accorde un bonus de 0,10 ; un engagement rompu inflige une pénalité de 0,15, distincte du mensonge factuel.

La confiance est donc une ressource fragile, qui se gagne lentement et se perd vite. C'est elle qui conditionne les liens sociaux, la cohésion des groupes et le consentement à la fusion.

Le caractère **dirigé** de la confiance est un point décisif, souvent mal compris. Si la confiance était symétrique, elle serait une propriété de la paire, et le réseau serait un graphe non orienté. En la rendant dirigée, le moteur autorise des situations que la symétrie interdirait : A peut faire confiance à B sans réciprocité, la relation est asymétrique, la défiance peut être unilatérale. C'est ce qui rend possible la notion de leader, qui est simplement celui auprès de qui les autres convergent, sans que lui ait besoin de leur rendre la pareille. C'est aussi ce qui rend le mensonge intéressant : tromper une entité qui vous fait confiance est un gain asymétrique.

L'asymétrie entre le gain d'une vérité (0,05) et le coût d'un mensonge (0,2) n'est pas un hasard. Elle encode une prudence : un acte de tromperie coûte quatre fois ce que rapporte un acte de sincérité. Dans un monde où l'information circule de façon imparfaite, cette asymétrie décourage la tromperie opportuniste, sans jamais l'interdire. Un agent peut mentir une fois ; s'il est pris, il lui faudra quatre vérités pour revenir à son niveau. La confiance se gagne donc par accumulation patiente et se perd par accident brutal, exactement comme dans les sociétés humaines que le projet prend pour inspiration.

Enfin, la décroissance sans interaction (un facteur multiplicatif par tick) a une conséquence structurante : **il faut entretenir une relation pour la garder**. Un lien qui n'est pas nourri s'éteint à petit feu, même sans conflit. C'est ce qui empêche un réseau de se figer : les vieilles alliances distantes s'effacent, laissant place à de nouvelles, plus locales. Le réseau social du monde n'est donc jamais un état stable, c'est un flux.

## 2. Les groupes : une cohésion constatée

Les groupes ne sont jamais scriptés. Aucune entité ne « décide » de créer un groupe. Le système observe les relations existantes et en déduit les structures. À chaque révision, on construit un **graphe de cohésion** :

```text
cohésion(P, Q) = min(confiance(P→Q), confiance(Q→P)) × affinité(P, Q)
affinité(P, Q) = 1 + (but partagé ? bonus : 0) + nombre de croyances partagées × bonus
lien(P, Q) ⇔ confiance réciproque ≥ seuil ET affinité > 1
```

Un lien social exige donc deux conditions : une confiance réciproque suffisante (au moins 0,3 par défaut) et au moins un point commun (une croyance partagée, un but partagé). La confiance seule ne suffit pas. Une croyance partagée est le même fait, avec une confiance d'au moins 0,5 de part et d'autre. Un but partagé est la même intention courante non neutre.

Cette double condition est le cœur conceptuel du chapitre. Elle dit qu'un groupe n'est pas seulement un amas de gens qui s'apprécient : c'est un amas de gens qui s'apprécient **et** qui partagent quelque chose. Deux entités peuvent se faire entièrement confiance et ne jamais former de groupe si elles s'ignorent mutuellement, parce que la cohésion exige l'affinité, c'est-à-dire un contenu commun. Réciproquement, deux entités qui partagent une croyance sans se connaître ne forment pas non plus de groupe. C'est la conjonction des deux qui compte.

*Précision de conformité (17 octobre 2026, arbitrage A0-3).* La règle de lien ci-dessus n'a **aucune clause de proximité spatiale** : deux membres distants restent liés tant que la confiance réciproque et l'affinité le permettent. Ce n'est pas un oubli — c'est la confiance qui fait le travail : elle décroît de 0,9 par tick sans interaction (§1), si bien qu'un lien distant s'éteint de lui-même faute d'être entretenu. La portée spatiale d'un lien n'est donc pas une condition de sa formation, c'est une **grandeur mesurée** : à chaque révision, les distances des paires liées sont instrumentées (maximum et moyenne par groupe ; `null` = non mesuré, jamais zéro) et exposées avec l'instantané du groupe, afin que la localité d'une structure soit observable plutôt que présupposée.

On voit ici une intention profonde du projet. Un groupe n'est pas seulement une affinité affective : c'est une **communauté de sens**, un ensemble d'entités qui tiennent pour vrai les mêmes choses ou visent les mêmes buts. C'est cette exigence qui relie les groupes aux croyances et à la communication. Pour qu'un groupe se forme, il faut qu'une information partagée ait circulé et que deux entités l'aient intégrée. Les groupes ne tombent donc pas du ciel : ils sont la trace, au niveau collectif, d'un échange d'information réussi. La partie du livre consacrée à la communication montrera comment cette information circule.

La révision s'exécute à fréquence fixe (tous les 10 ticks par défaut) et suit un ordre strict : on construit les liens entre toutes les paires dans l'ordre croissant des identifiants, on calcule les composantes connexes par union-find à racine minimale, on retient les composantes d'au moins trois membres, puis on applique le cycle de vie.

Le choix de l'**union-find à racine minimale** est un détail technique qui porte toute la reproductibilité des groupes. Un union-find est une structure qui regroupe des éléments en ensembles ; à racine minimale signifie qu'en cas de fusion de deux ensembles, c'est le plus petit identifiant qui devient représentant. Combiné à l'itération des paires dans l'ordre croissant des identifiants, cela garantit que deux runs identiques produisent exactement les mêmes composantes, dans le même ordre. Sans cette discipline, deux exécutions pourraient former les mêmes groupes mais avec des identifiants internes différents, et les traces ne se compareraient plus. Le moindre détail d'ordre est ici un engagement de reproductibilité.

Le seuil de trois membres n'est pas arbitraire : il évite qu'une simple paire d'amis soit étiquetée « groupe ». Une dyade est une relation ; un groupe est un ensemble qui a une vie propre, un leader, une décision. Le seuil marque la frontière entre la relation interindividuelle et la structure collective.

## 3. Le cycle de vie d'un groupe

- **Formation** : un noyau de membres liés qui n'a pas de groupe actif reçoit un identifiant séquentiel.
- **Continuité** : tant que l'ensemble des membres est identique, le groupe persiste, sa cohésion, son leader et sa décision étant recalculés à chaque révision.
- **Dissolution** : tout changement de composition dissout le groupe ; les membres restants au-delà de la taille minimale reforment un nouveau groupe.

Chaque dissolution émet un bilan : la durée de vie, un indicateur de succès (le groupe a-t-il produit au moins une décision collective ?) et le renouvellement de ses membres. C'est un churn émergent, mesuré plutôt que provoqué.

La règle de dissolution mérite d'être regardée de près, car elle est plus radicale qu'elle n'en a l'air. Un simple départ ou une simple arrivée dissout le groupe entier et en reforme un autre. On pourrait s'étonner de cette brutalité : pourquoi ne pas simplement ajouter ou retirer un membre ? Parce que ce choix rend la notion de « groupe durable » honnête. Un groupe n'est pas un objet que l'on garde à travers ses transformations ; c'est une **configuration stable** de relations. Dès que la configuration change, l'objet d'avant n'existe plus, et c'est un nouvel objet qui commence à vivre. La conséquence est que les groupes stables sont ceux dont la composition ne bouge pas, et que les groupes mouvants se dissolvent et se reforment sans cesse.

C'est cette règle qui donne au **churn** sa valeur de mesure. Si l'on voulait compter « combien de groupes se sont formés et dissous », une règle indulgente donnerait des chiffres artificiellement bas. Ici, chaque recomposition est comptée, et le livre y voit un indicateur réel : un monde où les groupes se dissolvent sans arrêt est un monde où les relations sont instables, et cela se mesure, sans que personne ne l'ait écrit. C'est un exemple pur de la philosophie du projet : ne pas définir la stabilité, la laisser émerger et la compter.

Le bilan de dissolution, avec son indicateur de succès, ajoute une dimension. Un groupe qui a produit au moins une décision collective avant de se dissoudre a « réussi » quelque chose ; un groupe qui n'a jamais rien décidé a existé sans effet. Cette distinction refine l'idée de structure : ce n'est pas l'existence d'un groupe qui compte, c'est son action.

## 4. Le leader et la décision collective

Le leader n'est pas élu ni nommé. C'est le membre dont la somme des confiances internes entrantes est maximale (les autres lui font le plus confiance), avec un départage déterministe par identifiant minimal.

À chaque révision, les intentions courantes des membres votent, chaque vote étant pondéré par la confiance envers le leader. L'intention dominante devient la décision collective du groupe si sa part pondérée atteint le seuil de consensus (0,5 par défaut). La décision est exposée dans le snapshot et émise en événement.

*Précision de conformité (17 octobre 2026, arbitrage A0-3).* Le mot « exposée » est à prendre au sens exact de la règle du chapitre 2b.3 — *celui qui regarde le collectif n'écrit jamais dans le monde* — et il vaut aussi pour le monde intérieur des agents : la décision collective est un **constat publié** (état du groupe, événement `group_decision`, instantané), jamais une cause d'utilité individuelle. Un mécanisme allant au-delà (une propagation de l'objectif collectif dans le score des membres, pondérée par le consensus et la confiance envers le leader) a existé et a été **retiré** le 17 octobre 2026 : si un membre tient compte de la décision de son groupe, il en fait un candidat individuel tracé, soumis à la même formule d'utilité que tout autre candidat, et la décision reste reconstruisible dans la trace. L'observateur agrégateur (revue centrale des paires) demeure : il mesure et expose, il ne code aucune structure et n'influe sur aucune délibération.

Il n'y a, dans cette version, ni rôle attribué au-delà du leader, ni ressource de groupe : ces mécanismes sont reportés. Le livre le dit, plutôt que de les laisser croire.

Le mode de désignation du leader est exactement l'inverse d'une élection. Dans une élection, chacun vote pour désigner celui qui commanderait. Ici, personne ne désigne : on **constate** qui, spontanément, reçoit le plus de confiance. Le leader est la conséquence d'un réseau de confiance asymétrique, non d'un mécanisme de décision. Un agent peut devenir leader sans avoir jamais cherché à l'être ; un autre peut en perdre le statut simplement parce que la confiance des autres s'est redistribuée. Cela illustre une fois de plus la méthode : l'ordre social n'est pas décrété, il est lu dans les relations.

La pondération du vote par la confiance envers le leader introduit une hiérarchie douce. Un membre en qui l'on a très confiance pèse plus qu'un membre marginal. Mais cette hiérarchie n'est pas fixe : elle se recompose à chaque révision, en même temps que le réseau de confiance. Le seuil de consensus (la part pondérée doit atteindre la moitié) empêche un groupe divisé de « décider » : si les intentions sont trop dispersées, aucune n'atteint la moitié et le groupe reste sans décision. C'est un garde-fou : un groupe peut ne pas décider, et c'est un résultat, non un échec à réparer.

Enfin, l'absence de rôle au-delà du leader est une honnêteté. On pourrait imaginer des éclaireurs, des gardiens, des artisans. Ce sont des rôles **attribués**, et le projet ne les a pas encore. Les nommer serait décrire un avenir, non un présent. Le lecteur doit savoir que, dans cette version, l'organisation interne d'un groupe se limite à un leader constaté et à un vote disponible.

## 5. Les territoires

Un territoire est une zone délimitée par la présence des entités autour d'un point de survie. À l'état décrit, la zone est un disque configuré, et le territoire effectif est la présence d'entités dans le disque (distance inférieure ou égale au rayon), recalculée en fin de tick. C'est une pure fonction des positions : aucun comportement agentique, aucune revendication.

L'ordre d'émission est déterministe : par zone, dans l'ordre de pose, par identifiant d'entité croissant, les sorties avant les entrées. Le suivi est désactivé par défaut ; activé, il n'ajoute que des champs additifs au snapshot et des événements. Les frontières fluides, la négociation territoriale et les ressources par territoire sont hors périmètre de cette version.

Ici encore, le mot est plus ambitieux que le mécanisme, et le livre choisit de le dire. Un « territoire », dans l'usage courant, évoque une propriété défendue, une frontière reconnue, une exclusivité. Dans l'état décrit, il n'y a rien de tout cela : un territoire est une zone où l'on constate la présence d'entités. C'est une **mesure**, pas une institution. Mais cette mesure n'est pas inutile : compter les présences autour d'un point de survie permet d'observer si une région devient un pôle d'activité, autrement dit si un lieu neutre finit par concentrer du monde. C'est une information de premier ordre pour la question directrice, et elle ne nécessite aucune règle de propriété. On observe la concentration, on ne l'ordonne pas.

L'ordre d'émission déterministe (sorties avant entrées, identifiants croissants) poursuit le même but que partout ailleurs : rendre la trace comparable d'un run à l'autre. Même avec un système purement observationnel, le projet ne s'autorise aucune latitude d'ordre qui aurait pu faire diverger deux exécutions.

## 6. Les saisons

La saison courante est une fonction pure du tick : l'index avance d'une unité toutes les durées de saison (360 ticks par défaut) et reboucle modulo quatre. Aucun tirage. Chaque saison module la régénération et la dégradation des réserves : par exemple, l'été favorise l'eau, l'automne le bois, l'hiver défavorise la nourriture et l'eau. Le cycle est livré mais désactivé par défaut ; activé, il émet un événement au basculement et n'altère que des champs additifs.

Le fait que la saison soit une **fonction pure du tick** a une élégance qui vaut d'être notée. Il n'y a pas d'état « saison courante » qui aurait à être sauvegardé, transmis ou resynchronisé : on peut recalculer la saison à partir du seul numéro de tick. Cela signifie qu'une entité peut « connaître » la saison sans la percevoir, et qu'un consommateur peut l'afficher sans interroger le moteur. C'est aussi une garantie de reproductibilité : la même séquence de ticks produit toujours la même séquence de saisons, sans dépendre d'aucune mémoire. La saison n'est pas un objet du monde, c'est une lecture du temps.

L'effet d'un rythme global sur des décisions locales est subtil et important. Aucune entité ne sait qu'une saison change. Elle ne dispose d'aucune horloge interne qui lui dirait « l'hiver arrive ». Elle constate simplement que le monde produit moins, que ses réserves se reconstituent plus lentement, que ses besoins coûtent plus cher à satisfaire. Le comportement collectif qui en résulte, par exemple une activité plus intense en été, n'est écrit nulle part : il est l'agrégation, sur des centaines d'entités, d'une même contrainte devenue plus sévère. C'est le type même de phénomène que le projet veut pouvoir observer et mesurer.

## 7. Les livres

Un livre matérialise un savoir figé au tick de sa rédaction. L'écriture est explicite : l'auteur paie un coût en énergie (20 par défaut), le livre est stocké, un événement est émis. La lecture enregistre un lecteur distinct, dans l'ordre de première lecture, et émet un événement avec le bénéfice configuré.

Un point d'honnêteté : le bénéfice de lecture est **posé en principe**. Son effet cognitif (sur les croyances ou la mémoire) dépend d'un futur moteur de mémoire, et reste reporté. Les livres sont donc, à l'état décrit, une capacité de transmission **observationnelle**, non un mécanisme cognitif complet. Le livre le dit clairement, plutôt que de laisser imaginer une culture déjà transmissible.

Le livre matérialise un savoir « figé au tick de rédaction ». Ce détail porte une idée forte : un livre n'est pas un message vivant, qui évoluerait au fil des interprétations. C'est un instant gelé du monde, une copie d'un état à un moment donné. Cela crée une distance entre l'auteur et le lecteur : l'auteur écrit ce qu'il croyait à l'instant t, le lecteur lit cette croyance plus tard, alors que le monde a changé. C'est le germe d'une mémoire collective, mais aussi d'une possible erreur transmise : le savoir le plus ancien peut être le plus périmé.

Il faut rester rigoureux sur ce que les livres font et ne font pas aujourd'hui. Un livre s'écrit (l'auteur paie 20 d'énergie), un livre se lit (un lecteur distinct est enregistré), et tout cela est tracé par des événements. Mais **le contenu du livre n'atteint pas encore les croyances du lecteur**. Le « bénéfice de lecture » est déclaré dans la configuration, il est émis dans l'événement, mais son effet cognitif dépend d'un moteur de mémoire qui n'est pas encore branché. Le lecteur doit retenir cette nuance : dans l'état décrit, on peut observer l'acte de lire, non la transmission d'une idée. C'est une frontière de version, et l'annoncer est plus honnête que de laisser croire à une culture déjà en marche. Un livre qui s'écrit et se lit sans transmettre est une promesse d'avenir, pas une réalité présente.

## 8. La reproduction : une fusion consentie

La naissance repose sur une **fusion consentie**. Deux entités fusionnent si elles se consentent mutuellement une confiance suffisante (au moins 0,6 par défaut). La fusion est soumise à des conditions de fidélité comportementale : proximité physique (distance au plus 40), ligne de vue dégagée, énergie suffisante (au moins 30 pour chacune) et absence de besoin critique.

La tentative a lieu à intervalle régulier (tous les 100 ticks par défaut), avec au plus une naissance par tick. L'enfant hérite des traits selon des mécanismes fins configurables : à l'état décrit, la fusion est égalitaire (moyenne des traits) et la mutation est nulle ; dominance et mutation existent comme paramètres, mais neutres par défaut. Un événement de naissance est émis, avec les identifiants des parents.

La reproduction n'exige pas l'appartenance à un groupe : un groupe est un vivier possible de fusions, mais la naissance relève de la même brique relationnelle que la cohésion (la confiance réciproque), sans contrainte scriptée.

Le mot **consentie** est chargé, et il faut le prendre au sérieux sans l'exagérer. Deux entités fusionnent si chacune accorde à l'autre une confiance d'au moins 0,6. Le consentement n'est donc pas une intention déclarée, ni une décision d'établir une descendance : c'est une conséquence d'un niveau de confiance mutuel. Une entité ne « veut » pas se reproduire ; elle fait assez confiance à une autre, et la condition est remplie. C'est une manière élégante de rattacher la reproduction à la même brique que tout le reste, la confiance, sans introduire un mécanisme ad hoc.

Les conditions annexes de la fusion sont, elles, l'expression d'une exigence de plausibilité. Fusionner à distance, ou à travers un mur, ou avec un partenaire épuisé, serait absurde. Le projet impose donc une proximité (40 unités), une ligne de vue dégagée, une énergie suffisante (30 pour chacune) et l'absence de besoin critique. Ce sont des conditions locales, vérifiables, aveugles au collectif. Elles empêchent une reproduction « magique » tout en laissant la décision émerger de la seule relation.

Le caractère **égalitaire et sans mutation** de l'héritage, dans l'état décrit, mérite d'être souligné précisément parce qu'il est neutre. Les paramètres de dominance et de mutation existent, mais ils sont neutralisés par défaut : un enfant reçoit la moyenne des traits de ses parents. La conséquence est qu'une reproduction réussie n'introduit aucune innovation génétique : elle tend vers un individu moyen. Les paramètres existent pour un avenir où l'on voudra explorer l'effet de la dominance ou de la mutation, mais tant qu'ils sont neutres, la descendance réduit la variété plutôt qu'elle ne l'augmente. C'est un fait de configuration, pas une spéculation, et il sera utile au chapitre de la calibration.

## 9. La mort

La mort est une dissolution complète. Une entité dont l'énergie atteint zéro meurt : elle est retirée du monde, de la grille, des groupes et de la cognition, puis un événement est émis. Aucun cadavre ne subsiste : seul un événement de trace marque la disparition. La mort est active par défaut.

Il n'y a, à l'état décrit, ni vieillissement biologique ni reproduction sexuée au sens classique : la génération repose sur la fusion consentie, et la fin de vie sur l'épuisement. Ce sont des choix de modèle, pas des oublis.

La mort par épuisement, sans vieillissement, a des conséquences précises. Un agent ne meurt pas parce qu'il est vieux, mais parce qu'il a mal géré son énergie. La sélection n'est donc pas darwinienne au sens de l'âge, elle est **énergétique** : survivent les agents dont le comportement est soutenable dans ce monde. On comprend mieux pourquoi la calibration a tant insisté sur le bilan énergétique : dans un monde sans vieillissement, la moindre dérive entre dépense et revenu finit par tuer lentement toute la population, non par accident, mais par accumulation. Le monde n'a pas de « fin naturelle » pour un agent : il n'a que des budgets.

Le retrait complet (monde, grille, groupes, cognition) est une exigence de cohérence. Une entité morte ne doit pas continuer à apparaître dans un voisinage, dans un groupe ou dans une confiance. Le nettoyage est donc total, et seul un événement conserve la trace. Cette rigueur évite les « fantômes » de simulation, ces artefacts où un agent supprimé continue d'influencer les autres par un état mal purgé. C'est un détail d'ingénierie, mais c'est un détail dont dépend la confiance dans les mesures.

## 10. Le conflit

Le conflit repose sur des primitives d'action (attaque, défense) livrées avec les ADR cognitifs, mais **jamais générées par la délibération par défaut**. Une attaque inflige des dégâts d'énergie proportionnels à l'agressivité et dégrade la sécurité de la cible ; une défense réduit de moitié les dégâts subis pendant le tick. Ces primitives échouent en blocage (aucun effet partiel), par souci d'atomicité.

La réintroduction de l'attaque dans la délibération est un sujet distinct, encadré par une doctrine de progressivité et par des considérations éthiques (le chapitre des limites y revient). À l'état décrit, la conflictualité collective est donc **envisagée**, non active.

Le choix de livrer les primitives sans les activer est délibéré et mérite d'être compris comme un choix de méthode. Le projet aurait pu ne pas écrire de code d'attaque du tout. Il a préféré l'écrire, le tester, le tracer, mais le laisser inerte. La raison tient en un mot : la responsabilité. Un monde où des entités autonomes peuvent se nuire pose des questions éthiques que le projet ne veut pas traiter par inadvertance. En gardant la conflictualité désactivée par défaut, il préserve la possibilité de l'étudier plus tard, dans un cadre choisi, sans la laisser surgir d'elle-même au détour d'une délibération mal réglée. La doctrine de progressivité qui encadre cette réintroduction est décrite dans le chapitre des limites.

## 11. Le déterminisme des systèmes collectifs

Aucun de ces systèmes ne tire au sort. Les liens suivent l'ordre trié des paires, les composantes viennent d'un union-find à racine minimale, le leader d'un maximum de confiance avec départage par identifiant minimal, et les mutations d'héritage (lorsqu'elles existent) d'un hachage stable. Deux runs au même seed produisent donc exactement la même séquence de groupes, la même séquence de naissances et de morts, et les mêmes appartenances de territoire.

Cette propriété n'est pas un luxe théorique : c'est elle qui transforme une observation en résultat. Si l'on observait qu'un village s'est formé au tick 1 200, sans déterminisme on ne pourrait pas répondre à la question « cela se reproduit-il ? ». Avec le déterminisme, on peut rejouer le même run et vérifier que le village se reforme exactement au même tick, avec les mêmes membres. On peut aussi changer un seul paramètre et observer la différence, en sachant que toute autre chose est identique. C'est cette capacité de comparaison contrôlée qui fait de LIVEX une expérience plutôt qu'une démonstration.

## 12. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré comment des structures collectives apparaissent sans être écrites : groupes par cohésion constatée, leader par confiance, décision par vote pondéré, territoires par présence, saisons par le tick, livres par coût payé, naissance par consentement, mort par dissolution. Chaque mécanisme est local, aveugle et tracé.

On peut maintenant nommer précisément ce que le chapitre a établi. Les **comportements non explicitement programmés** apparaissent ici pour la première fois au niveau collectif : un groupe, un leader, une décision de groupe ne sont écrits nulle part, et pourtant ils existent. Ce ne sont pas des métaphores : ce sont des objets mesurés, exposés dans les instantanés et comptés par l'observatoire. La question directrice demandait si de telles structures pouvaient apparaître sans être programmées ; ce chapitre en donne la réponse la plus directe du livre, avec la réserve essentielle de la suite.

Cette réserve, formulée clairement : le chapitre montre que des structures apparaissent, non qu'elles **durent** ou qu'elles produisent des phénomènes durables et intéressants. Une structure qui se forme et se dissout aussitôt n'est pas encore une société. Pour que du collectif se maintienne, il faut que les entités échangent de l'information, que les croyances se diffusent, que la confiance se construise par des actes. C'est le sujet du chapitre suivant : la communication par pulsations.

**Ce que ce chapitre ne permet pas de conclure.**

- Il ne démontre pas qu'une structure collective **dure** : un groupe mesuré à un tick peut se dissoudre au suivant, et le chapitre ne mesure que sa formation, non sa pérennité.
- Il n'établit pas qu'un groupe soit une société : une cohésion constatée et une décision majoritaire ne sont pas encore une organisation, et le livre ne leur prête pas ce sens.
- Il ne prouve pas que les règles retenues (seuil de confiance à 0,3, taille minimale de 3, quorum à 0,5) soient les bonnes : ce sont des valeurs de calibration ou d'héritage.
- Il ne garantit pas la portée des mécanismes livrés mais désactivés : saisons, territoires et livres changent la trajectoire et ne sont pas calibrés lorsqu'ils sont activés.
- Il ne dit rien des conflits collectifs : la conflictualité organisée est hors périmètre de cette version.
