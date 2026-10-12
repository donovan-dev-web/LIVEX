# Launcher III. Observabilité et supervision

Une campagne dure des heures et engage plusieurs composants. Quand quelque chose s'arrête au milieu, la question n'est pas de savoir si le monde était intéressant, mais **quel composant a lâché, quand, et pourquoi**. Ce chapitre décrit la chaîne de supervision du Launcher : les niveaux d'observation, le modèle d'état, le contrat de santé et les règles de collecte.

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice demande des observations fiables. Or une observation faite sur une campagne interrompue ne vaut rien : si un run s'est arrêté au tick 12 000 sur 50 000 prévus, l'absence de tel phénomène ne prouve pas son absence, elle prouve que le monde ne s'est pas déroulé. La supervision est donc la condition de **validité** des résultats : elle garantit qu'une observation porte sur un monde qui a réellement vécu, et elle permet de dater l'incident qui a privé le spectateur de la fin.

## 1. Six niveaux d'observation, jamais dupliqués

| Niveau | Objet | Exemples |
| :-- | :-- | :-- |
| L0 | la machine | processeur, mémoire, disque libre, réseau |
| L1 | un processus | identifiant, durée de vie, mémoire, redémarrages |
| L2 | le service d'un composant | disponibilité, latence, erreurs, connexions |
| L3 | un run en cours | tick courant, cadence de tick, population, événements par seconde |
| L4 | une campagne | progression, échecs, durée médiane, estimation de fin |
| L5 | LIVEX entier | santé agrégée, alertes actives |

Deux principes structurent cette échelle. Un niveau supérieur **se compose** des niveaux inférieurs : il ne duplique jamais une mesure, il l'agrège. Et le niveau de simulation se nourrit du flux existant, sans ouvrir un second canal : la supervision ne doit pas créer de trafic supplémentaire dans un monde qu'elle observe.

## 2. Les états d'un composant

Neuf états décrivent un composant, et deux distinctions évitent les confusions les plus fréquentes.

| État | Signification |
| :-- | :-- |
| Non installé | absent du poste |
| Arrêté | installé, non lancé |
| Démarrage | lancement en cours |
| Actif | processus vivant, API répondante, critères de santé satisfaits |
| Dégradé | processus et API répondent, mais un critère est hors seuil |
| Injoignable | processus vivant, mais la sonde de disponibilité échoue ou le tick est figé |
| Arrêt en cours | arrêt demandé |
| Échec | terminaison anormale |
| Inconnu | pas d'information |

**Dégradé n'est pas Injoignable.** Un composant dégradé répond encore : sa cadence de tick est trop basse, ou un client attendu s'est absenté. Un composant injoignable, lui, est vivant mais ne répond plus. La différence détermine la réaction : on attend un composant dégradé, on intervient sur un composant injoignable.

**Un tick figé en pause n'est pas un blocage.** Le moteur peut être légitimement suspendu. La détection de blocage ne s'applique donc que lorsque la simulation se déclare en cours : un run est déclaré **bloqué** si le tick n'avance plus pendant une durée de garde, alors que l'état de simulation est « en cours ». Cette règle évite l'alerte la plus stupide possible : signaler comme panne un monde volontairement mis en pause par l'utilisateur.

**« Connecté » n'est pas un état.** C'est une **relation** : « ECHOS connecté à SYNE » décrit un lien entre deux composants, et doit s'afficher comme attribut de ce lien, jamais comme l'état de l'un des deux. Confondre les deux rendrait deux problèmes distincts illisibles : un service en panne, et un lien rompu entre deux services sains.

## 3. La règle d'agrégation

Chaque composant porte un poids selon le profil actif, requis ou optionnel.

| Situation | État global |
| :-- | :-- |
| Tous les composants requis actifs, aucun optionnel en échec | sain |
| Un composant requis dégradé, ou un optionnel en échec ou injoignable | dégradé |
| Un composant requis en échec, injoignable ou arrêté alors que le profil l'exige | hors service |
| Aucun composant démarré | inactif |

La règle est « le pire état parmi les composants requis, adouci pour les optionnels ». Elle n'est pas symétrique, et c'est voulu : PRISM peut être indisponible sans empêcher une campagne d'analyse de produire des résultats, alors qu'un moteur de simulation absent rend la campagne impossible. L'état global affiche **toujours sa cause principale** : non pas « dégradé », mais « dégradé, SYNE : cadence de tick basse ». Un état sans cause oblige l'utilisateur à enquêter, ce qui est exactement le travail qu'un tableau de bord doit épargner.

## 4. Le contrat de santé

Chaque composant expose, sur son port de contrôle, cinq points d'entrée.

| Point d'entrée | Sémantique |
| :-- | :-- |
| `GET /health/live` | le processus est vivant |
| `GET /health/ready` | prêt à travailler |
| `GET /health/details` | détail par sous-vérification, avec durée |
| `GET /info` | identifiant, version, version de protocole, build, plateforme, graine courante |
| `GET /metrics` | métriques du composant, en texte ou en JSON |

Le détail de santé est une **liste de vérifications** nommées, chacune avec son statut et sa durée. Une réponse utile ressemble à : processus sain, interface saine, progression du tick dégradée avec le message « cadence 2,1/s en dessous du seuil ». Le message est ce qui permet d'agir ; le statut seul ne suffit pas.

Le point d'entrée d'information (`/info`) joue un rôle particulier pour la science : il publie la **version** de protocole, la **graine courante** et le **monde chargé**. C'est ce qui permet de vérifier a posteriori qu'une comparaison entre deux runs portait bien sur la même version et la même graine, la condition de reproductibilité, exposée comme un service.

**Être prêt a un sens différent selon le composant.** Pour le moteur, être prêt signifie que l'interface de contrôle écoute **et** qu'un monde est chargé ou chargeable. Pour l'observatoire, être prêt signifie que son interface d'analyse répond ; sa connexion au moteur est un état de lien, non une condition de disponibilité, un observatoire sans flux reste un observatoire. Pour le rendu, être prêt suppose une scène chargée, si son moteur permet de l'exposer ; à défaut, un battement de cœur sur le flux existant est la solution de repli, et cette question reste explicitement à confirmer.

## 5. Nommer les métriques

Les métriques suivent une convention unique : `livex_<composant>_<nom>_<unité>`, avec un jeu d'étiquettes volontairement restreint (instance, expérience, run, composant).

Une interdiction mérite d'être citée telle quelle : **aucune étiquette par agent**. Une étiquette qui prendrait pour valeur l'identifiant d'une entité produirait un nombre de séries égal au nombre d'entités, multiplié par le nombre de métriques. À mille entités, la collecte s'effondre. Ce n'est pas une limite technique anodine, c'est une contrainte de conception de l'observabilité : on supervise un système par ses grandeurs agrégées, pas par ses individus. Les individus, eux, sont dans les traces, qui n'ont pas la même structure ni le même coût.

## 6. Journaux, corrélation et innocuité

Deux exigences complètent la collecte. Les journaux doivent être **corrélables** : un même identifiant permet de relier une entrée de journal d'un composant à celle d'un autre, et de rattacher une ligne au run et à la campagne en cours. Sans corrélation, un incident multi-composants devient une énigme.

Et la supervision doit être **innocue** : son coût en temps et en volume reste borné, et elle n'introduit aucun tirage dans le moteur. C'est la même règle que pour la mesure analytique : observer n'est acceptable que si l'observation ne change pas ce qui est observé.

## 7. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré comment une campagne devient **auditable**. Les six niveaux d'observation permettent de descendre d'un état global jusqu'à la cadence de tick d'un run ; les neuf états distinguent une panne d'un ralentissement et une pause d'un blocage ; la règle d'agrégation pondère l'importance selon le profil ; le contrat de santé fait de la version et de la graine des données publiques.

C'est ce qui autorise à dire d'une observation qu'elle est **valide** : le monde a tourné jusqu'au tick annoncé, avec la version et la graine annoncées, sans incident silencieux. Une structure observée dans ces conditions peut être discutée ; une structure observée sur un run interrompu ne peut pas.

**Ce que ce chapitre ne permet pas de conclure.**

- La durée de garde de la détection de blocage est à calibrer selon la durée de tick attendue : sa valeur n'est pas fixée.
- Le contrat de santé dépend d'une exposition HTTP par composant, ce qui n'est pas confirmé pour tous (le cas du rendu est explicitement en attente).
- La supervision prouve qu'un run s'est déroulé, non que ses résultats sont intéressants ni valides scientifiquement.
- Aucune validation à l'échelle n'a été faite sur des campagnes longues : le coût réel de la collecte sur plusieurs heures reste à mesurer.
