# Annexe E. Glossaire

Cette annexe définit les termes employés dans le livre. Lorsqu'un terme a un sens précis dans le projet, cette définition prime sur l'usage courant.

## A à C

**Action.** Unité de comportement exécutable d'une entité : idle, seekFood, seekWater, eat, drink, rest, flee, socialize, explore, plus des primitives déclaratives inertes (take, give, trade, attack, defend). La sélection résulte d'une délibération par utilité ; l'exécution est atomique, une action par entité et par tick.

**Agent, entité.** Être simulé autonome doté d'un état, de besoins, d'une perception, d'une mémoire, de croyances, de capacités et d'actions. Ce n'est ni un personnage scénarisé, ni un modèle d'apprentissage.

**Annotation de provenance.** Drapeau associé à chaque valeur de métrique, qui indique si la valeur a été calculée (vrai), si elle est un repli neutre faute de fenêtre (faux), ou si le tick n'a pas été observé (absence).

**Atomicité.** Propriété d'une action qui réussit entièrement ou échoue entièrement, sans effet partiel. Un déplacement qui tombe dans un obstacle laisse l'entité sur place.

**Autonomie fonctionnelle.** Capacité de sélectionner ses actions sans commande externe directe, à partir de son état interne et de son modèle. Sans conscience, volonté ni libre arbitre.

**BDI (Belief-Desire-Intention).** Cadre organisationnel du raisonnement : croyances (ce que l'entité tient pour vrai), désirs ou objectifs (ce qu'elle cherche), intentions (ce à quoi elle s'engage).

**Besoin.** Tension comportementale (faim, soif, fatigue, sécurité, social, curiosité) dont le niveau alimente la génération d'objectifs et l'utilité.

**Calibration.** Modification volontaire d'un ou plusieurs paramètres pour rendre le monde viable, accompagnée d'un changement de version et d'un recalcul des empreintes dorées.

**Campagne.** Ensemble paramétré de runs reproductibles, unité de partage et d'archivage. Le run est l'unité d'échec et de reprise.

**Cible de déplacement.** Point local vers lequel une entité se dirige, tiré dans un rayon égal au plus à la moitié du rayon de perception. Jamais un lieu lointain.

**Classe A, B, C.** Classification des quantités face à un changement d'échelle temporelle : processus du temps simulé (mise à l'échelle), cadences mécaniques (en ticks), effets ponctuels par action (inchangés).

**Cohésion.** Force d'un lien social, `min(confiance réciproque) × affinité`. Un lien exige une confiance réciproque suffisante et une affinité supérieure à un.

**Croyance.** Représentation structurée d'un fait (sujet, prédicat, valeur) assortie d'une confiance, d'une source et d'une péremption. « Vraie » pour l'entité dès que la confiance atteint 0,5.

## D à G

**Déterminisme bit-à-bit.** Propriété selon laquelle deux exécutions identiques (même graine, même configuration, même version moteur, même état initial) produisent exactement la même trajectoire.

**Disponibilité (statut).** Qualifie une capacité livrée et vérifiée dans la version annoncée. Les autres statuts sont : expérimental, en développement, envisagé, hors périmètre.

**Écart cognitif, écart social.** Normes normalisées bornées à l'intervalle [0, 1], comparant respectivement les distributions de croyances et les réseaux de confiance de deux runs.

**Émergence faible.** Apparition, au niveau du système, de propriétés non spécifiées comme objectifs par les règles locales, mais analysables et retraçables à ces règles.

**Empreinte dorée (checksum épinglé).** Valeur de hachage d'une trajectoire de référence, verrouillée par un test. Toute altération bit-à-bit la change.

**Environnement virtuel.** Espace calculé où des positions ont des conséquences (se déplacer coûte, voir dépend de la distance et des obstacles, puiser épuise une réserve).

**Fenêtre causale.** Intervalle durant lequel les conséquences d'une action n'affectent le monde qu'en fin de tick, et donc n'influencent les décisions qu'au tick suivant.

**Graine (seed).** Entier de 64 bits initialisant le générateur pseudo-aléatoire. Quatre éléments définissent un run : graine, configuration, version moteur, état initial.

**Groupe.** Structure sociale émergente, déduite d'un graphe de cohésion (confiance réciproque et affinité), d'au moins trois membres, revue à intervalle régulier. Jamais scriptée.

## H à P

**Hachage stable (splitmix64).** Fonction déterministe qui produit une valeur pseudo-aléatoire à partir d'entrées fixes (identifiant, tick), sans consommer le générateur global.

**Hystérésis.** Marge (0,05 par défaut) qu'une action concurrente doit dépasser avant de remplacer l'action courante, afin d'éviter l'oscillation.

**Instantané (snapshot).** État dynamique complet du monde à un tick. Source de vérité pour mettre à jour un consommateur ; les événements ne le remplacent pas.

**Interruption.** Évaluation centralisée, à tout tick, de la nécessité d'abandonner l'action en cours sur besoin critique ou action concurrente nettement supérieure.

**Loi fondamentale (sens du projet).** Règle locale et aveugle, sans cas spécial pour le collectif, qui contraint sans ordonner.

**Mémoire.** Enregistrement faillible d'expériences passées, à décroissance exponentielle de la saillance, de capacité bornée. À distinguer des croyances.

**Mémoire à décroissance.** Le souvenir s'efface selon `salience(t) = salience(0) × e^(-taux × délai)`, taux dépendant de la catégorie.

**Niveau de détail (LOD).** Espacement des décisions des entités éloignées, sans biais pour les proches. Fréquence `1 / 2^LOD`.

**Observabilité partielle.** Fait, pour une entité, de ne percevoir et connaître qu'une partie de l'état du monde. Source de diversité, d'erreur et d'exploration.

**Paquet `.livexp`.** Format unique du Launcher, conteneur ZIP64. Vivant pendant la campagne, scellé après. Déterministe au scellement.

**Perception.** Première étape de la boucle : transformation de l'état du monde en observations locales, potentiellement inexactes.

**PRISM.** Projet Unreal final de LIVEX. Montre sans décider.

**PRISM-LDK.** Plugin Unreal (*LIVEX Development Kit*, module `PrismLdk`) qui relie le projet PRISM au moteur.

**Pulsation.** Message lumineux public, local et dégradable échangé entre entités.

## Q à Z

**Rapport de référence.** Fichier unique où vit l'analyse agrégée d'une campagne, sans emplacement concurrent.

**Récupérable (état de paquet).** État d'un paquet dont l'écriture a été interrompue : la reprise est possible, le contenu est incomplet.

**Relais.** Retransmission d'un message compris par un récepteur, au plus une fois par entité, dans la limite d'un nombre de sauts maximal. Désactivé par défaut.

**Run.** Exécution complète et reproductible, définie par graine, configuration, version moteur et état initial.

**Saillance.** Poids d'un souvenir, décroissant avec son âge. Détermine son rappel et son oubli.

**Scellement.** Opération qui rend un paquet immuable : horodatage de fin, passage à l'état scellé, recalcul d'empreinte, figage du journal, normalisation des entrées.

**SYNE.** Moteur autoritaire de simulation et de décision. Décide.

**syne-mock.** Serveur Node.js d'émulation pour le développement. Outil, non moteur équivalent ; ses résultats ne valent pas comme résultats scientifiques.

**Taux d'incompréhension.** Probabilité qu'un message reçu soit mal compris (5 % par défaut). Tirée de façon déterministe.

**Territoire.** Zone délimitée par la présence d'entités autour d'un point de survie. Observé par présence, sans comportement agentique.

**Tick.** Unité de temps discrète. Par défaut, un tick vaut une minute simulée. Unité de causalité.

**Trace de décision.** Enregistrement complet d'une décision : besoins, croyances considérées, scores d'utilité par action, action choisie, drapeaux de délibération et d'interruption.

**Utilité.** Score qui départage les actions : `(bénéfice - coût - risque) × confiance × personnalité + urgence`. Transparent et rejouable.

**Viabilité.** Résultat de population d'un run : survivant, éteint ou inconnu ; avec un tick d'extinction éventuel.

**Zéro observé / non mesuré.** Distinction capitale : un zéro observé est une mesure dans une fenêtre publiée ; un zéro non mesuré est un repli neutre de l'instrument. Les deux ne doivent jamais être confondus.

## Termes de nommage

**ECHOS.** *Emergent Cognition & Holistic Observation System*. Mesure sans toucher.

**LIVEX.** *Living Intelligent Virtual Ecosystem eXperience*. Nom du projet complet.

**PRISM.** *Perceptual Rendering & Interactive Simulation Module*. Montre sans décider.

**SYNE.** *Systems & Emergent Network Engine*. Décide.

**LDK.** *LIVEX Development Kit*. Le projet Unreal minimal du dépôt, qui sert d'hôte technique au plugin.
