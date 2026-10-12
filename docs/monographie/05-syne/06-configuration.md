# SYNE VI. Configuration et calibration

Le monde n'est pas seulement décrit par son code : il est décrit par ses réglages. Ce chapitre expose la configuration qui rend un run reproductible, les profils qui changent l'échelle, les options de ligne de commande, puis raconte les calibrations qui ont rendu le monde viable. C'est ici que le livre montre comment une erreur a été découverte et corrigée, sans la dissimuler.

Ce chapitre est aussi un chapitre d'aveux. Un livre qui ne décrirait que les mécanismes d'un système abouti manquerait l'essentiel : la manière dont on découvre qu'il ne fonctionne pas, et dont on le répare. La seconde moitié de ce chapitre raconte comment un monde est mort, comment on a compris pourquoi, et comment on l'a sauvé. C'est cette partie qui donne sa valeur au reste.

**Pourquoi ce chapitre importe pour la question directrice.** Les réglages d'un instrument ne sont pas un détail : ils décident de ce qu'on peut jouer. Dans LIVEX, les lois du monde sont des paramètres, et la manière dont on les règle détermine si un monde vit ou meurt. Ce chapitre est aussi celui où le projet raconte comment il a sauvé un monde mourant sans écrire de comportement : il a ajusté les **contraintes**, jamais les conduites. C'est une distinction décisive pour la question : régler une loi n'est pas programmer un résultat.

## 1. Le principe

La configuration est un contrat reproductible : la même configuration, la même graine et la même version moteur doivent reproduire la même trajectoire. Les valeurs non présentes dans un fichier partiel conservent les défauts intégrés. La priorité est la suivante : défauts, puis profil éventuel, puis surcouche de configuration, puis options de ligne de commande.

Cette priorité en quatre niveaux mérite d'être retenue, car elle explique un incident du projet. Les défauts du code forment la base. Un profil, s'il est choisi, écrase une partie des défauts. Une surcouche de configuration écrase ensuite ce qui reste. Enfin, les options passées en ligne de commande ont le dernier mot. Chaque niveau est plus spécifique que le précédent. Une erreur dans cette chaîne, par exemple un profil oublié, produit un monde différent de celui attendu, sans que rien ne signale l'erreur : le monde tourne, mais avec les mauvais réglages. Le projet a connu ce cas, et l'a corrigé.

## 2. Le fichier de configuration

Un fichier de configuration regroupe les paramètres en sections : simulation, agents, ressources, communication, monde, groupes, reproduction, aléatoire et performance. Les valeurs par défaut de la version 0.15.0 sont rassemblées ci-dessous pour les sections principales.

**Simulation.** Dimensions 500 par 500, taille de cellule 10, nombre maximal de ticks 1 000 000, cadence 10 ticks par seconde, durée simulée par tick 60 secondes, sauvegarde automatique tous les 1000 ticks, cinq sauvegardes conservées.

**Agents.** Cent entités initiales. Huit traits à 1,0. Besoins : faim 0,5, soif 0,7, fatigue 0,3, dérive de sécurité 0,001, dérive sociale 0,0002, dérive de curiosité 0,0005 ; seuils 50, 50 et 70. Perception : rayon 50, décroissance de confiance 0,3, rotation 4, ligne de vue active, taille de cellule spatiale 50. Mémoire : capacité 1000, seuil de rappel 0,01, décroissances 0,01, 0,005 et 0,002. Croyances : force de révision 0,3, plafond par pas 0,5, bonus d'alignement 0,2, pénalité de conflit 0,1, expiration 100 ticks, plafond expiré 0,4, érosion 0,999 par tick. Confiance : initiale 0,5, décroissance 0,9 par tick, bonus de vérité 0,05, pénalité de mensonge 0,2, bonus d'engagement tenu 0,10, pénalité d'engagement rompu 0,15. Actions : coût de déplacement 0,03, gain d'énergie au repos 1,5, récupération de fatigue au repos 2,0 ; délibération tous les 10 ticks, bonus d'alignement 1,2, marge d'hystérésis 0,05, marge de conflit 0,5 ; interruptions actives avec marge 10, faim critique 85, énergie critique 10. Cheminement : actif, cellule 10, expansion maximale 4096 cellules, cache de 256 chemins. Mortalité : active, énergie fatale 0.

**Ressources.** Nourriture 20 000 (régénération 20), eau 20 000 (10), bois 50 (0,1), minerai 0 (0). Aucune dégradation périodique par défaut.

**Communication.** Portée 55, relais désactivé, deux sauts au maximum, un envoi et une réception par tick, incompréhension 0,05, décroissance de confiance par saut 0,9, coûts énergétiques neutres (zéro).

**Groupes.** Actifs, révision tous les 10 ticks, seuil de confiance 0,3, taille minimale 3, bonus de croyance partagée 0,1, bonus d'alignement de but 0,2, seuil de consensus 0,5.

**Reproduction.** Active, tentative tous les 100 ticks, seuil de confiance de consentement 0,6, une naissance par tick au maximum, portée de fusion 40, énergie minimale de fusion 30, ligne de vue requise, absence de besoin critique requise.

**Monde.** Mètres par unité 1,0 ; saisons, territoires, livres, événements et obstacles désactivés par défaut.

**Aléatoire.** Graine 12345 et moteur `xoshiro256**`.

**Performance.** Perception parallèle, grille spatiale, cache de décision et communication par lots activés.

| Section | Paramètre notable | Valeur par défaut |
| :-- | :-- | --: |
| Simulation | taille du monde | 500 × 500 |
| Simulation | cadence | 10 ticks par seconde |
| Simulation | durée simulée par tick | 60 secondes |
| Agents | effectif initial | 100 |
| Agents | rayon de perception | 50 |
| Agents | délibération | tous les 10 ticks |
| Ressources | nourriture, eau | 20 000 (20 et 10) |
| Communication | portée, incompréhension | 55, 0,05 |
| Groupes | seuil, taille minimale, consensus | 0,3, 3, 0,5 |
| Reproduction | consentement, fusion | 0,6, 30 |
| Aléatoire | graine, moteur | 12345, xoshiro256** |

## 3. Les profils

Deux profils sont fournis. Le profil de référence reprend les défauts, avec une cadence de 10 ticks par seconde. Le profil de représentation décrit un monde plus vaste : 2 240 par 2 240, cellule de 32, 6 ticks par seconde et 5 secondes simulées par tick. Ce second profil est destiné à la scène Unreal, non à la simulation scientifique.

Un profil s'applique **sous** la surcouche de configuration : les défauts viennent d'abord, le profil ensuite, la surcouche enfin, les options de ligne de commande en dernier. Ce point a son importance : un profil oublié au moment de la préparation du monde produisait un run sur le seul profil de référence. La correction garantit que la configuration résolue complète est transmise à la préparation.

La distinction entre les deux profils est fondamentale pour la lecture du livre. Le profil de référence est celui de la science : c'est sur lui que les calibrations ont été faites et que les campagnes tournent. Le profil de représentation est celui de la scène : il agrandit le monde pour l'immersion visuelle, mais il ne change pas l'économie du moteur (les réserves sont globales, le coût de déplacement est forfaitaire). Un résultat scientifique obtenu sur le profil de représentation vaudrait ce que vaut une mesure faite dans des conditions non calibrées : rien de solide. Le livre y revient dans le chapitre PRISM.

## 4. Les options de ligne de commande

L'exécutable accepte plusieurs options : mode sans interface, taille du monde, graine, nombre maximal de ticks, fichier de configuration, activation du service de contrôle, port de contrôle, activation de l'observabilité, port d'observabilité, et choix d'un profil de simulation. Les ports par défaut sont 5180 pour l'observabilité et 5181 pour le contrôle, sur l'interface locale.

Les options de ligne de commande sont le dernier mot parce qu'elles servent aux expériences ponctuelles : on veut parfois lancer un run avec une graine précise, une taille de monde donnée, un horizon limité, sans écrire de fichier de configuration. La chaîne de priorité garantit alors que ce réglage ponctuel écrase tout le reste, sans ambiguïté.

## 5. L'échelle temporelle

La version 0.15.0 introduit un second levier de temporalité, à côté de la cadence : la **durée simulée par tick**. Leur rapport porte un nom, `R`, et détermine combien de temps simulé s'écoule par seconde réelle. Par défaut, un tick vaut une minute simulée et la cadence est de dix ticks par seconde, soit dix minutes simulées par seconde réelle.

Ce levier a une conséquence non triviale. Certaines quantités sont des **processus continus du temps simulé** : elles doivent être mises à l'échelle. D'autres sont des **cadences mécaniques de calcul** : elles restent en ticks. D'autres encore sont des **effets ponctuels par action** : elles ne changent pas. Cette classification, dite A, B et C, est un point technique central.

| Classe | Nature | Exemples | Traitement |
| :-- | :-- | :-- | :-- |
| A | processus du temps simulé | taux de besoins, régénérations, décroissances, durées | multiplié par le facteur d'échelle |
| B | cadence mécanique de calcul | délibération, rotation de perception, budgets, sauvegarde | reste en ticks |
| C | effet ponctuel par action | récupération d'énergie d'un repas | inchangé par comparaison |

La classification n'est pas une élégance : sans elle, un cycle de besoin paierait douze fois plus d'énergie à un certain ratio, et la trajectoire deviendrait incohérente. La mise à l'échelle est calculée une fois, au démarrage du run, de façon déterministe. Au ratio neutre (une minute par tick, compatibilité historique), chaque opération retourne simplement sa valeur d'entrée : aucune arithmétique n'est appliquée, et la trajectoire de référence reste inchangée au bit près.

L'idée derrière cette classification est simple à énoncer, difficile à appliquer. Une grandeur physique du monde simulé, par exemple la vitesse à laquelle la faim monte, doit s'écouler avec le temps simulé : si un tick vaut cinq minutes au lieu d'une, la faim doit monter cinq fois plus par tick. Une cadence technique, par exemple la fréquence à laquelle on décide, est une décision d'ingénierie, pas une grandeur du monde : elle reste en ticks. Un effet ponctuel, par exemple l'énergie rendue par un repas, est une quantité discrète par action : il ne dépend pas de la durée du tick. Confondre ces trois classes produit des mondes incohérents, où changer l'échelle temporelle déforme l'économie au lieu de la préserver.

## 6. Les calibrations : comment un monde a été sauvé

Deux calibrations ont volontairement changé la trajectoire. Les raconter est une obligation de méthode, car elles montrent comment un diagnostic se conduit.

**Calibration D1.** Elle équilibre la survie du profil de référence : le bénéfice de manger et de boire devient monotone (`min(besoin, 100) × 0,6`), et des gains d'énergie sont portés par les actions Eat et Drink. Cause isolée : les entités mangeaient et buvaient plus tôt et plus souvent, et la trajectoire divergeait dès les premières délibérations. Les empreintes dorées ont été recalculées et assumées dans le même commit que la décision.

**Calibration B1.** Elle recalibre les **défauts** eux-mêmes : le coût de déplacement passe de 0,5 à 0,03, les gains d'énergie de Eat et Drink de zéro à 2,0 et 1,0, les dérives sociale et de curiosité sont abaissées, et les réserves comme les coûts de communication sont recalés.

Le diagnostic de B1 mérite d'être détaillé, car il résume tout le projet.

1. **Symptôme.** Le monde mourait avant le tick 2 500.
2. **Causes isolées.** D'abord, une dégradation périodique annulait la régénération : l'apport net devenait nul. Ensuite, le mouvement, action majoritaire (75 à 98 % des ticks en régime établi), coûtait plus que le revenu amorti des repas, ce qui faisait décroître l'énergie sans borne jusqu'à l'épuisement. Enfin, les besoins social et de curiosité, sans mécanisme de satisfaction, franchissaient leur seuil et déclenchaient des actions coûteuses en permanence, jusqu'à 96 % des décisions en fin de run.
3. **Corrections.** Suppression de la dégradation par défaut et régénération nette de 20 par tick ; coût de déplacement abaissé à 0,03 ; gains d'énergie ajoutés ; dérives abaissées.
4. **Assomption.** La trajectoire change, donc les empreintes dorées sont recalculées et la version moteur est incrémentée, dans le même geste.

Le monde est devenu viable sur l'horizon de 2 500 ticks, à cinquante et cent entités, sur plusieurs graines. C'est une correction choisie, non un accident : les contraintes ont été ajustées, les comportements n'ont pas été écrits.

Ce diagnostic est un modèle de méthode, et il vaut d'être relu comme tel. Le symptôme était visible : le monde mourait. Mais un symptôme n'est pas une cause. Le projet a isolé **trois** causes distinctes qui se cumulaient : une dégradation qui annulait la régénération, un coût de déplacement trop élevé, et des besoins sans retour qui saturaient les décisions. Chacune aurait suffi à tuer le monde à terme ; ensemble, elles le condamnaient vite. La correction n'a pas consisté à empêcher la mort par décret (par exemple en rendant les entités immortelles), mais à ajuster les **contraintes** pour que la survie devienne possible par un comportement normal. C'est la différence essentielle entre corriger un monde et écrire un comportement : on ne dit pas aux entités quoi faire, on rend le monde vivable.

## 7. La discipline des empreintes

Chaque calibration qui change la trajectoire impose trois gestes simultanés : incrémenter la version mineure ou majeure, mettre à jour la version moteur diffusée, et recalculer les empreintes dorées. Deux empreintes sont épinglées par défaut : une empreinte de perception (25 entités, 200 ticks, graine 12345) et une empreinte d'état complet. Toute altération bit-à-bit de la trajectoire change ces valeurs, et donc échoue un test : c'est ainsi que le projet garantit qu'aucune modification silencieuse n'a lieu.

Une empreinte dorée est un hachage de la trajectoire. Elle joue le rôle d'un sceau : tant qu'aucune modification n'a touché la simulation, elle reste identique. Si quelqu'un change, sans le vouloir, une règle qui affecte la trajectoire, l'empreinte change, et le test échoue. Le développeur doit alors décider : soit il s'agit d'un bug qu'il faut corriger, soit il s'agit d'un changement voulu qu'il faut assumer en recalculant l'empreinte et en incrémentant la version. Dans les deux cas, le changement ne peut pas passer inaperçu. C'est cette discipline, simple en apparence, qui protège la promesse de reproductibilité sur la durée.

## 8. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré que le monde est réglable, reproductible et calibré. Il a surtout montré la méthode face à l'échec : mesurer, isoler une cause, corriger, assumer, re-verrouiller. Un monde viable n'est pas un monde où l'on interdit la mort par décret ; c'est un monde dont les contraintes rendent la survie possible sans décider des conduites.

Ce que ce chapitre ne permet pas encore de conclure : que le monde produise des structures au-delà des individus. Le chapitre suivant entre dans les systèmes collectifs : groupes, confiance, territoires, saisons, reproduction et mort.
