# Annexe F. Décisions d'architecture (ADR)

Un **Architecture Decision Record** (ADR) est une note courte et datée qui consigne une décision structurante, son contexte et ses conséquences. Le projet en compte trente-sept, répartis par composant. Cette annexe les recense, parce qu'un livre qui prétend expliquer le **pourquoi** des choix ne peut pas se contenter de décrire le résultat : il doit montrer où le choix a été pris, et sous quel argument.

Les ADR sont versionnés dans le dépôt, sous `docs/<composant>/adr/`. Leur statut évolue : une décision peut être proposée, acceptée, remplacée. Une décision remplacée n'est jamais supprimée : elle devient le contexte d'une décision ultérieure, ce qui permet de comprendre une trajectoire de conception, et non seulement son point d'arrivée.

## 1. Socle transverse (`docs/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-003 | API HTTP REST légère |
| ADR-004 | WebSocket en temps réel |
| ADR-005 | Refonte du système d'actions vers des primitives atomiques |
| ADR-006 | Système d'inventaire (capacité en poids et en emplacements, ressources stockables) |
| ADR-007 | Means-End Reasoning (plans alternatifs par objectif) |
| ADR-008 | Politique de reconsidération (contrôle de saillance) |
| ADR-009 | Engagements communicationnels (promesses) |

Ces sept décisions concernent l'ensemble du système. Deux méritent d'être signalées pour le lecteur : l'ADR-005 formalise le passage d'actions composites à des **primitives atomiques** ; c'est l'origine de la liste d'actions manipulée par la formule d'utilité (SYNE XII) ; et l'ADR-009 introduit les **engagements**, qui étendent la notion de besoin à des états non physiologiques : un objectif peut naître d'une promesse, avec un bénéfice calculé depuis la confiance envers le demandeur, sans que la formule d'utilité change d'un iota.

## 2. SYNE (`docs/syne/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-001 | `Simulation.Core` : le cœur du moteur |
| ADR-002 | `Simulation.Console` : un exécutable dédié |
| ADR-005 | Temporalité : un tick = une minute |
| ADR-006 | PRNG reproductible |
| ADR-007 | Mémoire oubliante |
| ADR-008 | Communication non confidentielle |
| ADR-009 | Énergie comme monnaie d'action |
| ADR-010 | Abandon de la morphologie physique |
| ADR-011 | Persistance : du JSON à SQLite |
| ADR-012 | Configuration moteur (JSON et priorités de ligne de commande) |
| ADR-013 | Obstacles : ligne de vue en V1 |
| ADR-014 | Baselines de calibration V0.1 |
| ADR-015 | Calibration de survie du scénario de référence (décision D1) |
| ADR-016 | Calibration B1 : stabilité du scénario par défaut sur 2 500 ticks |
| ADR-017 | Échelle temporelle configurable |

Trois de ces décisions structurent tout le livre. L'**ADR-006** (PRNG reproductible) est la condition du chapitre de la reproductibilité : sans un générateur sérialisable et identique d'un runtime à l'autre, aucune comparaison de runs ne serait possible. L'**ADR-007** (mémoire oubliante) est la décision qui a rendu le savoir périssable, et donc rendu la communication utile. L'**ADR-009** (énergie comme monnaie d'action) explique pourquoi l'énergie n'est pas un besoin comme les autres : c'est la ressource qui paie les actions, ce qui fait de son budget un enjeu de survie.

Les ADR-014 à ADR-016 racontent autre chose, et c'est une histoire précieuse : la **calibration**. Elles consignent les campagnes qui ont montré qu'un couple de paramètres plausibles rendait le scénario de référence instable ou mortel, et qui ont fixé les valeurs qui le rendent viable. Une décision de calibration n'est pas un réglage cosmétique : elle change ce que le monde produit, et elle est donc consignée comme une décision d'architecture.

## 3. ECHOS (`docs/echos/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-001 | Choix de la stack applicative (FastAPI, front local) |
| ADR-002 | Mode de calcul causal : hors ligne, sur traces persistées |

L'ADR-002 porte une décision de méthode, rappelée à plusieurs reprises dans le livre : le calcul causal n'est **pas** effectué en ligne, sur le flux temps réel. Il est reconstruit après coup, à partir des traces persistées, parce qu'une analyse en ligne pourrait ralentir le moteur et, à la marge, modifier le monde observé. Cette décision est un cas particulier de la règle d'or : observer sans toucher.

## 4. PRISM (`docs/prism/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-002 | Unreal Engine et plugin PRISM-LDK pour PRISM |
| ADR-003 | Échelle et cadence SYNE ↔ Unreal (facteur 100, 6 ticks par seconde, monde d'environ 5 km²) |

L'ADR-003 est le seul endroit où l'échelle physique du monde est fixée : le facteur 100 convertit les unités logiques en centimètres Unreal, et la cadence de 6 ticks par seconde découle de l'échelle temporelle. Une décision de **rendu** détermine donc, en retour, la lecture du temps dans toute la communication entre composants, un bon exemple du fait que la découpe en composants n'isole jamais complètement.

## 5. Launcher (`docs/launcher/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-001 | .NET et Avalonia pour le Launcher |
| ADR-002 | Deux modes d'utilisation de poids égal (analyse et immersion) |
| ADR-003 | ECHOS calcule, le Launcher présente |
| ADR-004 | Le paquet `.livexp` |
| ADR-005 | Exécution séquentielle et mono-espace en V0.1 |
| ADR-006 | PRISM conçu mais verrouillé |
| ADR-007 | Consoles natives et fenêtre d'analyse dans le Launcher, ECHOS sans interface |

L'**ADR-003** est une décision d'architecture au sens fort : elle **retire** à ECHOS toute interface et transfère la présentation au Launcher. Elle explicite une frontière (« celui qui calcule n'affiche pas ») qui se retrouve dans la conception du livre lui-même, où l'observatoire est décrit comme un instrument aveugle. L'**ADR-005** explique pourquoi les campagnes de la V0.1 s'exécutent en séquence : un seul moteur, un run à la fois. L'**ADR-006** documente le fait que PRISM, bien que conçu, est verrouillé en attendant, une décision rare et courageuse dans un projet technique : celle d'annoncer ce qui n'est pas prêt.

## 6. Installateur (`docs/installer/adr/`)

| Référence | Décision |
| :-- | :-- |
| ADR-001 | Installateur Avalonia séparé du Launcher, cœur sans interface |
| ADR-002 | Installation par composant, sans élévation, dans un espace de travail dédié |
| ADR-003 | Runtimes embarqués plutôt que dépendances système |
| ADR-004 | Catalogue signé et mise à jour manuelle atomique par composant |

L'ADR-002 explique pourquoi aucune commande d'administration n'est nécessaire : l'installation se fait par composant, dans un espace de travail dédié, sans privilèges. L'ADR-003 choisit d'**embarquer** les runtimes plutôt que de dépendre de l'environnement de la machine, au prix d'une taille plus grande et au bénéfice d'une installation reproductible.

## 7. Comment lire cette annexe

Cette liste n'est pas un inventaire administratif. Elle montre une méthode : les choix du projet ne sont pas dispersés dans le code, ils sont **écrits, datés et argumentés**. Un lecteur qui veut comprendre pourquoi l'architecture est ce qu'elle est peut suivre les ADR comme une chronique. C'est également ce qui permet à un relecteur extérieur de contester une décision : un choix consigné est un choix critiquable, alors qu'un choix implicite est simplement subi.

**Ce qu'il ne faut pas en conclure.** Le nombre d'ADR n'est pas une mesure de qualité, et un ADR « proposé » n'est pas une décision appliquée. Plusieurs décisions listées ici concernent des composants dont l'état est spécifié mais non implémenté (installateur, PRISM) : la liste décrit l'intention architecturale, pas l'état d'exécution. Le statut réel de chaque composant est donné dans le corps du livre.
