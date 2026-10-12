# Chapitre 2b. Décomposer la question, terme à terme

**Question : peut-on construire un environnement virtuel régi par des lois fondamentales, au sein duquel des entités autonomes peuvent produire des comportements et des structures qui n'ont pas été explicitement programmés ?**

Ce chapitre prend la question au mot, littéralement. Chaque terme est défini, situé dans son histoire, traduit en exigences testables, puis confronté à ce qui est réellement mis en place dans l'état V0.1, et à ce qui ne l'est pas. C'est le contrat de lecture de tout le livre : quand le texte dira « émergence », « autonome » ou « lois », le lecteur saura quelle promesse exacte est tenue.

Les valeurs citées sont les valeurs par défaut du moteur à la version décrite, sauf mention contraire. Elles proviennent du fichier de configuration et des contrats documentés.

## 2b.1 « Environnement virtuel » : un monde sans matière, mais avec conséquences

Un environnement virtuel n'est pas un décor. C'est un espace calculé où des positions ont des conséquences : se déplacer coûte, voir dépend de la distance et des obstacles, puiser épuise une réserve partagée. Sans conséquences, il n'y a pas de monde, seulement une image.

Historiquement, la tentation a été le décoratif : des paysages immenses, des habitants-horloges, des économies immobiles dès qu'on détourne le regard. LIVEX tranche autrement. Le monde est un plan continu fini et borné par un clamp, de dimensions configurables, 500 par 500 unités pour la référence. Il n'est ni un tore magique (on ne réapparaît pas d'un bord à l'autre) ni un espace infini. On y bute.

Quatre réserves globales le font respirer en fin de tick, sans aléa : nourriture (20 000 unités, régénérées de 20 par tick), eau (20 000, régénérées de 10), bois (50, régénérées de 0,1) et minerai (0, sans régénération). Une géographie de cercles bloque le pas et masque le regard, et un pathfinding parcelles permet de contourner les obstacles. Des rythmes purs, sans aléa, scandent le monde : saisons calculées en fonction du tick, territoires définis par la présence, savoirs figés payés en énergie.

Ce qui est réellement construit : un métabolisme aveugle qui contraint sans ordonner. Ce qui ne l'est pas : des sources spatiales par zone, une physique réaliste, une économie monétaire. Ces éléments sont hors périmètre assumé.

Le mot « virtuel » signale aussi une limite à garder en tête : le monde est logique, pas matériel. Il ne simule pas la matière ; il calcule des états. Sa crédibilité ne vient pas du réalisme physique, mais de la cohérence des conséquences. On peut formuler cette cohérence par une règle simple, qui vaut pour tout le livre : **aucune action n'est gratuite, et aucune conséquence n'est inventée**. Un déplacement consomme, un repas ponctionne une réserve, un message perd de la fiabilité. Rien ne se produit sans cause inscrite.

## 2b.2 « Entités autonomes » : décider sans recevoir d'ordres, et sans conscience

Autonome signifie ici fonctionnel, non métaphysique. Une entité autonome sélectionne ses actions sans commande externe, à partir de son état interne et de son modèle faillible du monde. Elle n'a ni conscience, ni volonté, ni libre arbitre au sens philosophique. LIVEX ne prétend pas résoudre le problème philosophique du libre arbitre. Une décision produite par un algorithme peut être autonome au sens technique sans être libre au sens métaphysique.

L'histoire du terme va des automates aux agents BDI. Les architectures BDI, proposées par Rao et Georgeff en 1995, organisent le raisonnement en trois composantes : les croyances (ce que l'entité tient pour vrai), les désirs ou objectifs (ce qu'elle cherche), et les intentions (ce à quoi elle s'engage). Entre les deux, les architectures réactives et les fonctions d'utilité ont appris à rendre la décision explicable.

LIVEX opérationnalise cette architecture. Chaque entité reçoit huit traits singuliers tirés autour d'une valeur neutre, six besoins à taux et seuils (faim, soif, fatigue, sécurité, social, curiosité), une perception locale et faillible, une mémoire à décroissance exponentielle, des croyances révisables, un catalogue d'actions aux coûts calibrés, et une décision par utilité transparente.

La fonction d'utilité est la clé de l'autonomie fonctionnelle :

```text
utilité = (bénéfice − coût − risque) × confiance × personnalité + urgence
```

avec un bonus d'alignement quand l'action sert l'objectif courant, et une urgence qui croît selon une sigmoïde du niveau de besoin, majorée lorsque l'objectif vieillit ou devient critique. Le tout sans tirage : la décision est donc rejouable.

Un mot sur ce que « sans commande externe » veut dire exactement. Il serait facile de tricher en pilotant les entités par une règle qui, de fait, décide à leur place : par exemple, « quand la faim dépasse un seuil, va manger ». Un tel mécanisme serait un script déguisé. LIVEX évite cette triche par une exigence précise : le seuil ne déclenche pas une action, il génère un **objectif**. L'objectif entre en concurrence avec les autres par la priorité, et c'est l'utilité qui tranche entre les actions candidats. Une entité affamée peut donc, selon ses autres objectifs, son risque et sa personnalité, choisir de ne pas manger tout de suite. La commande est remplacée par une préférence, la préférence par un score, et le score par une décision.

Ce qui est construit : des trajectoires singulières produites par des règles communes, tracées dans des enregistrements de décision. Ce qui ne l'est pas : l'apprentissage, le langage naturel et la conscience. Ces éléments ne sont jamais affirmés.

## 2b.3 « Lois fondamentales » : peu de règles, aveugles, sans exception narrative

Fondamentales veut dire trois choses : petites, locales, sans cas spécial pour le collectif. Comme en physique, où peu de lois engendrent une grande complexité sans la décrire. L'histoire de cette idée va des automates cellulaires, où des règles minimales produisent des dynamiques riches, aux essaims, où la robustesse naît sans chef. La leçon est constante : contraindre sans ordonner.

LIVEX pose ces lois sous la forme d'une boucle d'une quinzaine d'étapes en ordre causal strict : percevoir, se souvenir, croire, s'user, éprouver ses manques, désirer, envisager, peser, trancher, s'engager, agir, encaisser, transformer le monde, raconter, recommencer. Les coûts et les gains sont fixes. La communication est publique, locale et dégradable. Les groupes exigent un minimum de membres et se revoient à intervalle régulier. Les naissances se font par consentement, les morts par dissolution, les territoires par présence, les saisons par le tick.

Aucune règle ne dit « si le tick égale mille » ni « si la population dépasse tel seuil ». Les sept principes du chapitre précédent interdisent l'exception narrative. Ce qui est construit : des lois qui rendent possible sans programmer. Ce qui ne l'est pas : la garantie que le possible advienne. L'émergence reste à observer, jamais promise.

On peut donner un critère opératoire de ce qu'est une « loi fondamentale » dans ce projet. Une règle est fondamentale si trois conditions sont réunies :

- elle ne mentionne que des grandeurs **locales** (l'état d'une entité, ses voisins proches, la réserve partagée) ;
- elle est **la même pour tous** (aucune exception par identité, par rang, par région) ;
- elle est **sans mémoire du collectif** (elle ne compte pas les populations, ne mesure pas les tendances, ne se souvient pas des siècles).

Ces trois conditions excluent tout mécanisme qui, sous couvert de simuler le tout, déciderait au niveau du tout. Le moteur ne connaît pas « la société » : il connaît des entités et des voisins. S'il existe, quelque part, un composant qui regarde le collectif, c'est l'observatoire, et il n'écrit jamais dans le monde.

## 2b.4 « Comportements et structures non explicitement programmés » : l'émergence faible comme critère

Émergence faible signifie : du surprenant mais analysable. Des propriétés du niveau système, non spécifiées comme objectifs, retraçables aux interactions. L'histoire, de *Sugarscape* aux essaims puis aux agents génératifs, montre le critère de vérification. Peut-on remonter la chaîne

```text
action ← intention ← objectif ← besoin ← croyance ← mémoire ← perception
```

jusqu'aux causes locales, rejouer à graine identique, et distinguer l'observé de l'aveugle (la provenance de chaque mesure) ?

LIVEX instrumente quatre niveaux d'observation. Un niveau spatial (les convergences vers les sources, où des assoiffés font foule sans ordre de foule). Un niveau social (relations, groupes, consensus). Un niveau informationnel (volumes, délais d'émission, concentrations, décroissances théoriques). Un niveau abstrait, de type économique (accès aux réserves).

Les formules d'appui sont peu nombreuses et explicites : l'entropie de Shannon pour la dispersion des croyances ou des objectifs, la variance pour l'hétérogénéité, une pente aux moindres carrés sur une fenêtre de ticks pour la viabilité, et une décroissance théorique de la confiance par saut pour la communication. Ce qui est construit : des chaînes traçables, des séries comparables avec ou sans empreinte, des rapports déterministes. Ce qui ne l'est pas : une loi générale tirée d'une scène, une conscience, une société humaine. Ces extensions sont explicitement refusées.

Ce dernier point mérite une insistance, parce qu'il est le plus facile à trahir. Rien n'est plus tentant, devant une structure spectaculaire, que de conclure « une société est née ». Le livre s'interdit ce raccourci. Une structure observée est un **fait** : un groupe s'est formé, à tel tick, avec tels membres. Ce qu'elle **signifie** en termes sociologiques est une **interprétation**, qui n'engage pas le moteur. Et l'**hypothèse** qu'elle représenterait un phénomène social général est une troisième chose, qui demanderait des méthodes et des données hors des moyens de ce projet. Ces trois niveaux, fait, interprétation, hypothèse, ne se confondent jamais dans le livre.

## 2b.5 Exigences testables : la grille de lecture de tout le livre

La décomposition produit une grille. Pour chaque terme de la question, ce qui suit est exigé et ce qui est construit.

| Terme de la question | Ce qu'il exige | Ce qui est construit dans l'état décrit |
| :-- | :-- | :-- |
| Environnement virtuel | un espace où les positions ont des conséquences | plan continu borné, réserves partagées, obstacles, rythmes purs |
| Entités autonomes | une décision sans commande externe, explicable | BDI opérationnalisé, utilité transparente, sans tirage |
| Lois fondamentales | des règles locales, aveugles, sans exception narrative | boucle en ordre causal strict, coûts fixes, aucun cas collectif écrit |
| Comportements et structures non programmés | émergence faible, traçable et rejouable | quatre niveaux instrumentés, provenance des mesures, chaînes causales |

On peut compléter la grille par une colonne de vérifications, qui indique sur quelle preuve repose chaque ligne. Cette colonne est ce qui transforme une promesse en engagement.

| Terme | Preuve attendue | Où la trouver |
| :-- | :-- | :-- |
| Environnement virtuel | configuration et tests du monde | chapitre SYNE I, annexe C |
| Entités autonomes | tests de cognition et trace de décision | chapitre SYNE II, annexe A |
| Lois fondamentales | boucle à ordre strict, absence de cas collectif | chapitre SYNE III, annexe D |
| Structures non programmées | moteurs de métriques et analyse causale | chapitres ECHOS, chapitre 13 |

Refermer cette décomposition, c'est tenir le contrat : chaque mot de la question vaut désormais exigences testables et état réel. Chaque chapitre suivant n'aura plus qu'à montrer comment ces exigences sont remplies, sans jamais les redéfinir en cachette.

## 2b.6 Ce que ce chapitre apporte à la question directrice

Ce chapitre a transformé une phrase en grille. Il a montré que « environnement virtuel », « autonome », « lois » et « non programmé » ont des définitions opératoires, chacune reliée à un mécanisme réel. Il a également posé la limite de l'entreprise : les lois rendent possible, elles ne garantissent pas que le possible advienne.

Le chapitre suivant recule d'un pas. Il situe LIVEX dans les lignées scientifiques dont il hérite (Artificial Life, systèmes complexes, multi-agents, intelligence collective, BDI, agents génératifs) et montre comment chacune a fourni une réponse partielle à la question, dont le projet tente la synthèse.

**Ce que ce chapitre ne permet pas de conclure.**

- Il ne démontre pas que les définitions proposées suffisent à trancher la question : elles la rendent instruisible, ce qui est déjà beaucoup.
- Il n'établit pas que chaque exigence testable soit aujourd'hui satisfaite : la grille sert à situer ce qui l'est et ce qui ne l'est pas.
- Il ne rend pas la notion de « non explicitement programmé » décidable dans l'absolu : elle ne l'est que relativement à un code, une configuration et une version donnés.
- Il ne garantit pas que la grille couvre tous les cas d'intérêt : un phénomène non prévu par les catégories restera invisible.
