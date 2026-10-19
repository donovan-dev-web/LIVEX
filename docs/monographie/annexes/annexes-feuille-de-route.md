# Annexe L. Feuille de route

Le livre décrit un état, arrêté au 12 octobre 2026. Cette annexe dit ce qui vient après, dans quel ordre, et selon quelle règle, sans poser de date. Les mises à jour de code et de gouvernance postérieures à cette coupe sont décrites séparément dans le [statut courant du dépôt](../../governance/LIVEX-STATUS.md); elles ne sont pas rétroprojetées dans les campagnes historiques du livre.

**Pourquoi cette annexe importe.** Une feuille de route est l'endroit où un projet est le plus tenté de mentir : en transformant un souhait en étape, et une étape en acquis. La règle adoptée ici est inverse : ne figure ci-dessous que ce qui est **déjà instrumenté** par un jalon, une décision ou un test, ou ce qui est explicitement **hors périmètre**. Un projet qui publie ses intentions doit aussi publier ses refus.

## 1. Trois catégories, pas deux

La distinction habituelle oppose « fait » et « à faire ». Elle est insuffisante pour décrire ce projet, qui en compte une troisième, la plus délicate.

- **Livré** : présent dans le code, couvert par des tests.
- **Livré mais non activé** : le mécanisme existe et fonctionne, mais il est désactivé par défaut, non calibré ou sans effet observable à ce stade. Ce n'est ni un acquis scientifique ni un travail futur : c'est une capacité en réserve.
- **À faire** : identifié, ordonné, non commencé ou non franchi.

Confondre les deux premières catégories est l'erreur la plus coûteuse d'un projet comme celui-ci : un mécanisme livré mais inerte ne produit rien, et le présenter comme livré laisse croire que le monde contient ce qu'il ne contient pas encore.

## 2. Le moteur (SYNE) : douze phases livrées, des capacités en réserve

Les douze phases techniques (socle, cognition BDI et perception, mémoire et croyances, décision et utilité, actions, communication, groupes, ressources et environnement, fidélités, observabilité, performance, tests et couverture, persistance et reprise) sont **livrées**, chacune avec sa version de moteur et ses issues. Le récit de cet enchaînement a été fait au fil du chapitre 5.

Ce qui reste se répartit ainsi.

**Capacités livrées mais non activées.** Le chapitre 14 en donne la liste : saisons, territoires, livres, constructions, inventaire, engagements, primitives cognitives, contrôle de saillance. Leur point commun est net : le mécanisme existe et fonctionne, il est désactivé par défaut, et **son activation change la trajectoire du monde sans avoir été calibrée**. Les territoires sont suivis mais inactifs ; le cycle des saisons est une fonction pure du tick, sans dépendance observée à ce jour ; les constructions sont des obstacles configurables, mais la mécanique agentique (qui construit, à quel coût, avec quels matériaux) n'est pas tranchée.

**Des spécifications ouvertes.** Les sources spatiales de ressources, la latence des signaux, le modèle d'héritage et la résolution chiffrée des conflits d'interaction relèvent du même mouvement : chaque question a son endroit pour être tranchée, et cet endroit est nommé dans les documents de composant plutôt que laissé implicite.

*A0-2, SYNE 0.17.0 (17 octobre 2026).* **Les sources spatiales de ressources ne sont plus une spécification ouverte : elles sont un jalon ordonné.** Le modèle localisé est devenu celui du profil de référence après franchissement du gate de viabilité : 3 graines sans mort à 2 500 ticks, sensibilité à la graine, quête adossée à un dépôt connu et compétition observable. Les réserves globales restent une baseline déclarée et testée séparément. Le gate établit la viabilité à 2 500 ticks, non la tenue à long horizon, à la raréfaction ou à l'hiver. Les prochaines étapes restent la **calibration à horizon long** (raréfaction d'un gisement, saison, hiver), puis la **concurrence mesurée** sur un gisement partagé. Les critères et résultats détaillés sont dans le [rapport expérimental pérenne](../../governance/realignment/EXPERIMENTAL-VALIDATION-2026-10.md). Les autres questions — latence des signaux, héritage, conflits d'interaction — restent ouvertes.

**Un écart de performance.** La cible de 1 000 entités n'est pas tenue, d'environ 1,7×. L'optimisation de la passe de perception et le traitement par lots des événements sont le chantier ; il ne bloque pas l'état décrit, dont les scénarios tournent à 100 agents ou moins, mais il borne la revendication de scalabilité.

**Une calibration.** Les constantes de besoins et de seuils reposent sur des bases provisoires. Une constante non calibrée n'est pas fausse ; elle est non éprouvée, ce qui est une manière de dire la même chose.

**Un risque à surveiller.** Les mesures évoquées dans l'annexe K montrent qu'un défaut de calibration peut ruiner un run sans qu'aucune erreur ne soit levée. Toute phase ultérieure doit donc porter sa propre vérification de viabilité, et ne pas supposer que les paramètres hérités sont sains.

## 3. L'observatoire (ECHOS) : phases livrées, retraits assumés, indice à finir

Onze phases : **neuf livrées** et **deux retirées** (l'interface intégrée et le shell Electron), par la décision ADR-007 du 5 octobre 2026. Conséquence directe : l'observatoire n'a pas d'interface, et la présentation est portée par les fenêtres natives du Launcher. Le retrait est une décision, pas un manque ; il protège la règle fondatrice selon laquelle observer ne touche pas le monde.

Dans la coupe du livre, le chantier principal restant est **l'indice de viabilité dynamique**. Sa livraison était alors prévue **avant la publication du livre**, mais il n'était pas disponible à cette date. Cette échéance n'est pas une preuve d'achèvement : le relevé de code du 18 octobre ne trouve pas de moteur `ViabilityIndicators` ni de sorties `ViabilityScore`/`ViabilityCoverage`; l'endpoint `/api/runs/{id}/viability` reste un profil de séries sources et de calibration. Le chapitre ECHOS VII demeure donc une spécification, pas la description d'un mécanisme en service. Son découpage en trois phases commande l'ordre des travaux : le moteur de fenêtre courte, puis les séquences et la trajectoire en post-run, puis l'exposition contractuelle et la calibration. Ses constantes sont **proposées et héritées, non calibrées** : tant que la phase 3 n'est pas faite, ses seuils de régime ne sont pas des normes, et l'indice peut seulement **décrire** un régime, jamais **juger** un run. Deux conséquences assumées par construction : sans crise spontanée, aucun score de résilience n'est publié, le système affiche une absence de crise observée plutôt qu'un chiffre trompeur ; et le score composite reste le composite publié, l'indice ne le remplaçant pas par principe, mais seulement si la calibration démontre un gain de stabilité ou d'interprétabilité.

La comparaison entre runs et la reconstruction causale sont livrées. La **campagne de calibration** qui doit fournir des constantes mesurées, et non des valeurs de départ raisonnables, reste à mener : elle porte à la fois sur l'indice et sur les constantes de besoins et de seuils du moteur.

## 4. Le rendu (PRISM) : une version de travail, une validation en attente

PRISM et son plugin sont en **version de travail**. Les contrats de transport sont stabilisés et documentés, mais l'étape décisive est l'autre : **valider contre SYNE réel** le cycle de préparation, d'initialisation et de démarrage, puis les instantanés, les différences et les événements. Tant que cette validation n'a pas eu lieu, une réussite contre des simulacres ne prouve pas l'interopérabilité ; la règle « un simulacre n'est pas une acceptation » est explicite dans le projet.

Au-delà de la validation : la génération de présentation, la mise à jour stable par identifiant, les vues d'inspection et de relations, la mesure du coût des instantanés selon le nombre d'entités, puis l'entretien des tests d'interopérabilité et de compatibilité de contrat. Ces étapes n'ajoutent aucune décision au monde : le rendu ne décide pas.

## 5. Le Launcher : livré par endroits, non franchi ailleurs

Le cas du Launcher illustre la distinction de la section 1. État observé :

| Jalon | État au 12 octobre 2026 (coupe du livre) |
| :-- | :-- |
| G0 Socle de conception | écrit |
| G1 Format de paquet | présent dans le code |
| G2 Orchestration sans interface | présent dans le code |
| G3 Campagnes | accepté selon le relevé 17/10 contre SYNE publié; le run E2E local du 18/10 est à 31/33, deux assertions J3 encore en échec |
| G4 Interface d'orchestration | parcours principaux présents ; variantes et validation complètes ouvertes |
| G5 Session complète avec ECHOS | livré côté Launcher, **non franchi** : la condition exige un scénario vert contre un ECHOS **réel**, l'implémentation n'a été prouvée que contre un banc de simulacres |
| G6 Livraison | contrôle `--check` complet, installateurs multiplateformes non livrés |
| G7 Mode Immersion | **verrouillé**, faute de PRISM conforme |

Le jalon G5 mérite d'être retenu comme cas d'école : le logiciel existe, mais l'acceptation doit être rapportée avec son run et sa date. Le rapport du 17/10 documente 33/33; la vérification locale du 18/10 a deux échecs J3 sur composants réels. Pour le checkout courant, la porte reste donc ouverte — exactement le genre de distinction que la règle des preuves impose.

## 6. L'installateur : une spécification, cinq jalons

L'installateur est à l'état de **spécification**, sans ligne de code : quatre décisions d'architecture, une spécification d'exigences et de parcours, dix étapes dont six sans effet. Cinq jalons sont définis : décisions, manifestes et artefacts de publication, cœur de l'installateur (plan, journal, annulation, intégrité, mode silencieux, simulation à blanc, réparation, désinstallation), assistant graphique, mises à jour, signature et fonctionnement hors-ligne, puis Windows. Ces jalons décrivent un ordre de travail : aucun calendrier n'est annoncé.

Deux points de méthode valent d'être notés, car ils conditionnent le calendrier autant que le code. D'abord, l'installateur **ne peut pas précéder ce qu'il installe** : tant qu'un composant n'est pas publié et accepté sous un système, sa ligne d'installation est vide. Ensuite, **Linux d'abord** : la plateforme Windows dépend d'une acceptation des composants qui n'est pas acquise, et le catalogue restera honnête sur ce point plutôt que d'annoncer une installation qui livrerait moins. Les risques identifiés (blocage antivirus sur un installateur non signé, indisponibilité d'une bibliothèque native pour la version de l'interpréteur choisie) sont des risques de livraison, pas des inconnues techniques.

## 7. Ce qui reste à faire pour la question directrice

La feuille de route des composants n'est pas celle de la question. Trois travaux relèvent de la recherche et non de l'ingénierie.

**Un protocole de verdict statistique.** Le nombre de runs nécessaires pour qu'une différence entre configurations soit autre chose qu'une observation n'est pas déterminé. Aucune campagne ne devrait être interprétée comme un résultat avant que ce point soit tranché.

*Tranché le 17 octobre 2026 (BL-04).* Le protocole de verdict est appliqué : pré-enregistrement des métriques et seuils avant le run, K ≥ 5 graines avec réussite dans au moins ⌈0,8 K⌉, contre-factuel baseline et ablation/contraste, provenance en trois états et interdiction de déplacer un critère après observation. La première campagne (3 bras × 5 graines) publie un verdict confirmé et un non confirmé sans déplacement de seuil. Seule la métrique préenregistrée prétend au verdict; toute autre lecture demeure exploratoire. Le protocole et les verdicts sont conservés dans le [rapport expérimental pérenne](../../governance/realignment/EXPERIMENTAL-VALIDATION-2026-10.md).

**Des scénarios de crise.** La résilience ne peut se mesurer que sur des crises, et le projet s'interdit de manipuler le monde pour en fabriquer. Il faut donc des scénarios d'épreuve légitimes, définis en amont.

**Une seconde configuration de référence.** Performance, déterminisme et acceptation reposent aujourd'hui sur une seule machine et un seul runtime. Une mesure unique n'est pas une loi.

## 8. L'ordre, et ce que cette annexe ne promet pas

L'ordre qui se dégageait des dépendances dans la coupe du livre était : valider l'interopérabilité du rendu contre le moteur réel, franchir la session complète contre un observatoire réel, achever l'indice de viabilité, calibrer, puis publier des composants et installer. Le statut postérieur note que des specs PRISM et un rapport Launcher réel 33/33 ont depuis été documentés, mais que le run Launcher local du 18 octobre est 31/33; ces preuves distinctes ne modifient pas la date de cette coupe et le J3 complet n'est pas considéré vert dans le relevé local courant. Les jalons de publication sont en aval de l'acceptation, jamais l'inverse.

Cette annexe ne promet pas de date : elles n'apparaissent dans le projet que comme des jalons observés, jamais comme des prévisions. Elle ne promet pas non plus que l'ordre soit immuable : toute inversion devra être justifiée explicitement. Elle ne compte pas comme travail le passage à une **répartition multi-machines** (direction documentée, gardée ouverte, non implémentée), le **support de systèmes autres que Windows et Linux**, ni le recours à des **modèles de langage comme moteur de décision**, qui reste une piste documentée et non un engagement. Écarter ces trois pistes n'est pas un jugement sur leur intérêt : c'est un refus de gonfler la cible.

> Une feuille de route utile se reconnaît à ceci : on peut y perdre des points. Les sections qui précèdent en perdent trois : un jalon livré mais non franchi, un écart de performance non comblé, une calibration non faite.
