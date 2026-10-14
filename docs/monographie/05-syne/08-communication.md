# SYNE VIII. La communication par pulsations

Les entités perçoivent, décident et se lient. Il manque ce qui fait tenir un monde social : l'échange d'information. Ce chapitre décrit la communication entre entités dans le monde simulé. Il faut le distinguer soigneusement du transport entre composants : ici, il s'agit de pulsations lumineuses échangées entre entités, non du flux réseau entre le moteur et ses clients.

Cette distinction est la première chose à comprendre, parce qu'elle est une source constante de confusion. Le mot « communication » désigne deux choses très différentes dans le projet. D'un côté, la communication **entre composants** : le moteur parle à ECHOS et à PRISM par des messages réseau, sur des ports, avec des contrats. De l'autre, la communication **entre entités** : des agents simulés échangent des signaux dans le monde. Les deux ont lieu, mais la première est de l'ingénierie, tandis que la seconde est un objet d'étude. Ce chapitre ne parle que de la seconde. Quand un agent « émet », il ne s'agit pas d'une requête réseau : il s'agit d'un geste dans le monde simulé, qui se propage comme une onde lumineuse et que d'autres agents peuvent percevoir.

**Pourquoi ce chapitre importe pour la question directrice.** Pensez à des phares dans la nuit : chacun émet une lumière que d'autres peuvent voir de loin, sans savoir à qui elle s'adresse. La communication de LIVEX est de cette nature, publique et dégradable. Elle compte pour la question parce qu'une structure collective durable dépend de ce qui circule : sans échange d'information, les groupes resteraient des coïncidences géographiques. Ce chapitre montre par quel canal des croyances, vraies ou fausses, peuvent se propager et préparer, sans l'écrire, des structures plus stables.

## 1. Le principe : un signal public, local et dégradable

La communication est un signal lumineux.

- **Public.** Il est visible de quiconque le perçoit. Il n'existe pas de canal privé. Une entité qui intercepte un message peut le lire, quelle que soit la cible visée.
- **Local.** Le signal est nul au-delà d'une portée effective, 55 unités par défaut. Cette portée est distincte de la portée de perception (50 unités) : on peut entendre un peu plus loin qu'on ne voit.
- **Borné par la ligne de vue.** Un obstacle entre émetteur et récepteur interrompt la transmission.
- **Dégradable.** Le message perd de la confiance à chaque saut, et il peut être mal compris.

La publicité et la dégradation sont des choix délibérés. Elles introduisent la rumeur, l'erreur et l'interception, qui sont autant de sources de complexité. Une communication parfaite appauvrirait le monde.

Ce principe mérite d'être explicité comme un choix de conception philosophique, non comme une contrainte technique. Dans la plupart des systèmes multi-agents, la communication est adressée : A envoie un message à B, et personne d'autre ne le reçoit. C'est propre, efficace, et cela produit des sociétés d'agents parfaitement informés les uns des autres par paires. Ce n'est pas ce que le projet veut. En rendant le signal public, le projet introduit trois phénomènes impossibles dans un système adressé : l'**interception** (un tiers peut apprendre ce qu'il n'était pas censé savoir), la **rumeur** (une information se diffuse sans contrôle de son émetteur), et la **saturation** (un agent qui parle à tout le monde fait du bruit pour tout le monde). Ces trois phénomènes sont le tissu même des sociétés humaines, et ils n'apparaissent que parce que le canal est public.

La portée légèrement supérieure au rayon de perception (55 contre 50) est un choix fin qui crée une asymétrie utile. On peut **entendre** un signal venant d'un peu plus loin qu'on ne peut **voir** son émetteur. Cela signifie qu'un agent peut recevoir une information sans identifier clairement sa source, ou percevoir un signal d'un voisin qu'il n'a pas encore vu. Cette asymétrie entre voir et entendre n'est pas un caprice : elle reproduit une situation commune et riche, où l'on perçoit un message avant d'avoir identifié qui parle. Le monde gagne ainsi une source d'incertitude supplémentaire, cohérente avec l'observabilité partielle qui traverse tout le projet.

## 2. Les types de messages

Sept types de messages existent. Le moteur, à l'état décrit, implémente le type Information ; les autres sont prévus par le contrat et arrivent avec les besoins sociaux.

| Type | Usage |
| :-- | :-- |
| Information | partager une observation |
| Request | demander une collaboration |
| Response | répondre à une requête |
| Announcement | diffuser une intention de formation |
| Warning | alerter d'un danger |
| Trading | proposer un échange |
| Acknowledgement | confirmer une réception |

Un message porte un identifiant déterminé par hachage stable (émetteur, tick, séquence), un émetteur d'origine, une cible facultative, un type, une charge utile, une confiance, un nombre de sauts et le tick.

L'identifiant est déterministe : deux runs au même seed produisent les mêmes identifiants, sans consommer le générateur global. C'est une exigence qui traverse tout le moteur : tout ce qui ressemble à du hasard doit pouvoir être rejoué.

Le fait que sept types soient prévus mais qu'un seul soit réellement produit par la délibération est un point d'honnêteté qui doit être lu attentivement. La table ci-dessus n'est pas une description du comportement actuel : c'est un **contrat**, une liste de messages que le monde sait transporter. Dans l'état décrit, seule l'Information est émise, parce que c'est la seule dont le besoin social correspondant est prévu dans la cognition par défaut. Les six autres types existent dans le format, ils sont testables, mais aucune décision d'agent ne les génère encore. Décrire « les agents se demandent de l'aide » serait donc faux ; décrire « le format sait transporter une demande d'aide » est exact. La nuance est mince à l'écrit, mais elle sépare une capacité d'une réalité.

L'identifiant déterministe, calculé par hachage de l'émetteur, du tick et d'un compteur de séquence, prolonge la discipline générale. Aucune émission ne consomme le générateur pseudo-aléatoire global. Si un agent émet dix messages, le générateur ne bouge pas ; sa suite de nombres reste disponible intacte pour les autres usages. Cette économie de hasard est ce qui permet à un run de rester exactement rejouable, même quand des milliers de messages sont échangés.

## 3. La diffusion et le relais

Le signal est reçu par toutes les entités dans la portée, ligne de vue comprise, quelle que soit la cible visée. La réception peut être incomprise, selon un taux configurable (5 % par défaut). L'incompréhension est tirée d'un hachage déterministe (récepteur, message), sans générateur global.

Le relais est désactivé par défaut. Activé, un message compris peut être relayé par un récepteur, une seule fois par entité (pour éviter les boucles), tant que le nombre de sauts reste inférieur à un maximum (deux par défaut). Le relais partage le quota d'envoi et applique la décroissance de confiance.

La communication s'exécute en une passe par tick, selon un ordre strict : d'abord la diffusion (par identifiants croissants), puis le relais. La chaîne d'un message est bornée par le nombre maximal de sauts, ce qui garantit qu'un tick ne se prolonge pas indéfiniment.

La diffusion « à tous ceux qui écoutent » est ce qui fait du message public une **onde** plutôt qu'une lettre. L'émetteur ne choisit pas son destinataire : il parle, et quiconque est à portée entend. Ce n'est pas une faiblesse du protocole, c'est sa nature. Un agent qui veut partager une observation la partage avec l'espace autour de lui, et il ne contrôle ni qui l'entend, ni combien le retiennent.

L'incompréhension, tirée à 5 %, est le grain de sel qui empêche le monde d'être une machine à information pure. Un message sur vingt environ arrive déformé. Cette déformation ne vient pas d'un bruit ajouté au contenu ; elle vient du fait que le message est mal compris, ce qui le rend inutilisable comme observation. Combinée à la publicité et à la dégradation par saut, cette petite probabilité suffit à ce que l'information qui circule ne soit jamais parfaitement fiable. Le projet a besoin de cette impureté : un monde où toute information est exacte ne peut pas produire de rumeur, et la rumeur est une des structures collectives les plus intéressantes à observer.

Le relais, désactivé par défaut, est le mécanisme qui permettrait à une information de **voyager** au-delà de la portée directe de son émetteur. Sans relais, un message ne touche que le voisinage immédiat ; avec lui, un agent qui a compris peut le reprendre à son compte, et l'information gagne du terrain par une chaîne de porteurs. C'est le principe même d'une rumeur qui se propage de proche en proche. La limite d'une seule retransmission par entité évite qu'un message tourne en boucle indéfiniment, et la borne de sauts (deux par défaut) limite la portée totale de la propagation. Le relais est donc un mécanisme complet mais bridé : il existe, il fonctionne, il est désactivé par défaut pour préserver la trajectoire de référence, et son activation est un choix documenté.

## 4. La dégradation et la confiance

Un message porte une confiance, ajustée à la réception par la confiance du récepteur envers l'émetteur. La dégradation par saut est géométrique :

```text
confiance effective = confiance × 0,9^(nombre de sauts)
dégradation théorique = 1 - 0,9^(nombre de sauts)
```

Ainsi, un message direct porte une confiance pleine ; un message relayé une fois en perd environ un dixième ; un message relayé deux fois environ deux dixièmes. La provenance est préservée : le relais ne change pas l'émetteur d'origine, seulement le nombre de sauts et la confiance.

La réception marque aussi une interaction : une première rencontre établit une confiance initiale, une relation connue se renforce selon la véracité, et la confiance décroît sans interaction. Un mensonge constaté coûte plus qu'une vérité ne rapporte, ce qui modélise une défiance prudente.

La décroissance géométrique par saut (facteur 0,9) est une manière élégante d'exprimer une idée simple : l'information perd de sa valeur à mesure qu'elle s'éloigne de sa source. Une flèche de 0,9 est multiplicative, ce qui signifie que chaque saut coûte le même pourcentage, non la même quantité. Un message après un saut vaut 90 %, après deux sauts 81 %, après trois 72,9 %, et ainsi de suite, sans jamais atteindre zéro. Ce n'est pas un hasard de conception : une décroissance additive aurait pu rendre la confiance nulle après dix sauts, alors qu'une décroissance multiplicative garantit qu'un message garde toujours une trace de valeur, si faible soit-elle. C'est plus réaliste : une rumeur lointaine n'est jamais complètement fausse, elle est juste très peu fiable.

La distinction entre la **confiance du message** (sa fiabilité intrinsèque, dégradée par les sauts) et la **confiance envers l'émetteur** (relation durable, qui se construit et s'érode) est capitale, et il faut la tenir séparée. Un même message peut être très fiable en soi mais provenir d'un émetteur en qui l'on n'a guère confiance, et inversement. Le récepteur combine les deux au moment de réviser ses croyances. Cette double lecture, celle du contenu et celle de la source, est exactement ce que fait un être humain lorsqu'il pèse une information : il juge ce qu'on lui dit et il juge qui le lui dit. Le moteur distingue les deux, ce qui permet de mesurer des situations où « la source est douteuse mais le message est probablement vrai ».

## 5. Les coûts et la calibration

À l'état décrit, les coûts énergétiques d'émission et de réception sont **neutres** (zéro). Cette neutralité est le fruit d'une calibration, et son histoire est instructive.

Dans une version antérieure, envoyer et recevoir coûtaient de l'énergie, et ces coûts étaient proportionnels à la longueur de la charge utile. Mais à la fréquence d'émission du scénario de référence (environ 0,2 envoi par entité et par tick), le coût d'envoi atteignait environ 0,12 énergie par tick, soit quatre fois le coût de déplacement recalibré. Autrement dit, parler coûtait plus cher que marcher. En régime établi, cela épuisait lentement les entités. La calibration a neutralisé ces coûts, et a désactivé le relais par défaut, afin de rendre le monde viable.

Ce point est un exemple de plus de la méthode : on a mesuré, isolé une cause (le coût relatif de la parole), et corrigé par un réglage, non par un script.

Cette anecdote vaut d'être racontée parce qu'elle illustre un piège classique du modèle multi-agents : un coût mal calibré peut déformer tout l'équilibre d'un monde. Ici, le coût de la parole était devenu plus lourd que le coût du mouvement, ce qui aurait produit une société taciturne par accident, non par choix. On imagine la cascade : moins de messages, donc moins de croyances partagées, donc moins de groupes, donc moins de coopération. Un seul paramètre aurait ainsi façonné toute la vie sociale du monde, sans que personne ne l'ait voulu. La calibration a rétabli l'équilibre en rendant la parole gratuite en énergie, ce qui est un choix : parler est une dépense de temps et d'attention, pas d'énergie vitale.

On peut résumer la calibration par une règle que le projet suit partout : **aucune dimension du monde ne doit être plus contraignante qu'elle ne devrait**. Si parler coûte plus cher que survivre, le monde ne parle plus, et ce n'est pas une propriété qu'on a voulue. Le levier, chaque fois, est un réglage de paramètre, jamais une ligne de code qui forcerait un comportement. La méthode est constante : mesurer, isoler, corriger, assumer.

## 6. L'intégration cognitive

La communication s'intègre à la cognition de deux façons. D'une part, les messages entrants sont mis en file, bornée par quota, et la confiance du message est ajustée par la confiance envers l'émetteur. D'autre part, la révision des croyances par les messages, la production de requêtes et de réponses avec les besoins sociaux, et les engagements communicationnels constituent des mécanismes livrés mais désactivés par défaut. À l'état décrit, la file entrante est consommée principalement comme trace d'activité du tick, utile au relais et à l'observabilité.

Le livre dit cela sans détour : la communication complète, avec ses effets cognitifs complets, est partiellement livrée. Le transport, la dégradation, la confiance et la trace sont disponibles ; la révision des croyances par les messages et la production des types sociaux sont en développement. Un engagement tenu ou rompu, par exemple, est modélisé (bonus et pénalité de confiance distincts) mais reste désactivé par défaut.

Il faut bien comprendre ce que « partiellement livrée » signifie ici, car cela change l'interprétation des mesures. Le chemin qui va du message à la croyance est amorcé mais non terminé : un message est transporté, reçu, sa confiance est calculée, il est mis en file. Mais la dernière étape, celle où le contenu du message modifie les croyances du récepteur, n'est pas encore active. Cela signifie que, dans l'état décrit, un agent peut recevoir une information et ne pas en changer ses croyances. La communication existe comme **phénomène observable**, non encore comme **canal de révision cognitive**. C'est une frontière majeure, et elle est annoncée explicitement.

Cette frontière a une conséquence directe sur les groupes. Rappelons que la cohésion exige un point commun, une croyance partagée ou un but partagé. Si les messages ne modifient pas les croyances, alors la convergence par communication directe est encore incomplète ; les croyances partagées proviennent surtout d'observations indépendantes et convergentes, non d'un échange explicite. Les groupes continuent de se former, mais leur mécanisme de cohésion repose davantage sur le fait que tout le monde voit le monde au même endroit que sur le fait que quelqu'un a raconté ce qu'il avait vu. Ce constat est essentiel pour l'interprétation causale, et le chapitre consacré à celle-ci y reviendra : on ne peut pas attribuer à la communication un effet cognitif qu'elle n'exerce pas encore.

## 7. Observabilité

La communication est entièrement traçable. Les événements d'envoi et de réception exposent l'identifiant, l'émetteur d'origine, la cible, le type, la charge utile, le nombre de sauts et la confiance effective. Ces traces alimentent l'analyse : volumes échangés, délai pour qu'une fraction de la population émette, concentration de l'émetteur dominant, décroissance de la confiance par saut. Le chapitre ECHOS montrera comment ces données deviennent des métriques.

La traçabilité n'est pas qu'une commodité, c'est la condition de la scientificité. Puisque chaque envoi et chaque réception laisse un événement complet, on peut, après coup, reconstruire exactement qui a parlé à qui, quand, par quel chemin et avec quelle perte de confiance. Un observateur extérieur peut donc poser des questions précises au monde : combien de temps a-t-il fallu pour qu'une information touche la majorité ? Quel agent émet le plus ? La confiance décroît-elle vraiment comme la formule le prédit ? Chacune de ces questions a une réponse mesurable, non une impression. C'est ce qui permet au livre d'affirmer des choses sur le monde qu'il décrit sans jamais les inventer.

## 8. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré comment l'information circule dans le monde : un signal public, local, dégradable et traçable, dont la confiance se gagne et se perd. C'est par ce signal que des informations, vraies ou fausses, précises ou déformées, se propagent et préparent, sans l'écrire, des structures collectives plus durables.

On peut préciser ce que le chapitre a établi, et ce qu'il laisse ouvert. Il a montré qu'un monde peut **transporter** l'information de manière publique, imparfaite et traçable, avec une dégradation qui a un sens physique et une confiance qui se calcule. C'est l'infrastructure de toute vie sociale. Mais il a aussi montré, avec la même netteté, la frontière actuelle : le message ne réécrit pas encore les croyances, et les types sociaux ne sont pas encore produits par la délibération. La communication est donc un **canal disponible**, non encore un moteur cognitif complet.

Ce que ce chapitre ne permet pas encore de conclure : que la circulation de l'information produise des phénomènes stables, mesurables et reproductibles. Pour le savoir, il faut un observatoire : un composant qui mesure sans toucher, qui transforme ces événements en chiffres et qui dit honnêtement ce qu'il n'a pas vu. C'est précisément le rôle du composant suivant, ECHOS, dont la partie suivante décrit l'architecture, les métriques et l'analyse causale.
