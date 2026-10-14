# Chapitre 9. L'installateur : promettre sans affirmer

Il reste, pour que la pile soit praticable, une brique qui n'existe pas encore : l'installateur. Ce chapitre est le seul du livre, avec une partie du chapitre des limites, à décrire une capacité **en développement** puisqu'il s'agit, à l'état décrit, d'une spécification sans code. Le livre le dit d'emblée, parce que présenter une promesse comme une livraison serait exactement la faute que tout le projet combat.

Il pourrait sembler étrange de consacrer un chapitre entier à un composant qui n'existe pas. La raison est méthodologique : la manière dont un projet traite ce qu'il n'a pas encore construit est un révélateur de sa rigueur. Un projet qui gonfle ses promesses traite l'inachevé par le silence ou par l'anticipation. Un projet qui tient à l'honnêteté le traite par un cadrage explicite. Ce chapitre est ce cadrage.

**Pourquoi ce chapitre importe pour la question directrice.** Une réponse n'a de portée que si d'autres peuvent reproduire l'expérience ; c'est pourquoi une pile installable fait partie de la démarche. Ce chapitre compte aussi pour une raison de méthode : il montre comment le projet traite ce qu'il n'a pas encore construit. En cadrant un composant absent au lieu de le promettre, il applique à l'inachevé la même honnêteté qu'aux résultats. Une réponse à la question directrice qui reposerait sur des capacités fantômes ne vaudrait rien.

## 1. Le rôle prévu

L'installateur LIVEX est l'application prévue pour déposer, configurer, vérifier, mettre à jour et retirer la pile sur un poste Windows ou Linux, au moyen d'un assistant graphique en étapes et d'un mode non interactif équivalent. Il déposerait des composants dont les manifestes décrivent le comportement ; le Launcher les détecterait ensuite par son mécanisme de détection existant.

L'installateur ne contiendrait ni logique de simulation, ni orchestration, ni analyse. Comme le Launcher, il orchestrerait sans posséder.

Ce dernier point est une règle d'architecture, pas une formule. L'installateur n'a pas à connaître le fonctionnement de SYNE, d'ECHOS ou de PRISM : il dépose des exécutables et des manifestes. C'est le manifeste du composant qui décrit son comportement, et c'est le Launcher qui l'interprète. L'installateur reste donc un livreur, non un expert des composants qu'il livre. Cette séparation signifie qu'un nouveau composant pourra être installé sans modifier l'installateur, à condition qu'il fournisse un manifeste.

## 2. Le périmètre

Le périmètre prévu est précis, et ses exclusions le sont autant.

Dans le périmètre : un assistant graphique pour Windows x64 et Linux x64 (Ubuntu LTS), un mode non interactif équivalent, l'installation du Launcher, de SYNE, d'ECHOS, du mock, du plugin PRISM-LDK et de la documentation, un contrôle de la configuration système avant installation, une configuration automatique (chemins, variables, manifestes), une mise à jour par composant avec retour arrière, et la réparation d'une installation incomplète.

Hors périmètre : macOS et les architectures ARM, une interface web ou une installation à distance, l'installation d'Unreal Engine, de Visual Studio ou du SDK .NET (hors mode développeur), l'optimisation ou la réparation du système hôte, l'édition des paramètres de simulation, la mise à jour silencieuse en arrière-plan, et la migration des paquets de campagne (propriété du Launcher).

La liste des exclusions mérite une remarque. Ce n'est pas une liste de limites subies, mais de refus réfléchis. Installer Unreal Engine ou Visual Studio dépasserait le rôle de l'installateur et modifierait profondément le système hôte ; éditer les paramètres de simulation confondrait l'installation et la configuration ; faire une mise à jour silencieuse priverait l'utilisateur du contrôle. Chaque exclusion protège une frontière. Un installateur qui ferait tout serait intrusif ; cet installateur est conçu pour ne rien faire de plus que déposer et configurer la pile.

## 3. Les principes

Sept principes cadrent l'installateur.

1. Aucune élévation de privilèges par défaut : l'installation se fait par utilisateur, une installation système étant une option explicite et hors périmètre de la version 0.1.
2. Les données de l'utilisateur ne sont jamais touchées : l'espace de travail est séparé du programme, et une mise à jour ou une désinstallation le conserve.
3. L'opération est transactionnelle : toute installation ou mise à jour est annulable, et une interruption laisse l'état précédent utilisable.
4. Embarquer plutôt qu'exiger : les environnements d'exécution nécessaires sont fournis avec les composants, et l'installateur ne modifie pas le système au-delà du répertoire choisi, des raccourcis et de variables utilisateur.
5. Une seule logique, deux interfaces : l'assistant et le mode non interactif exécutent le même plan.
6. Honnêteté de l'offre : un composant n'est proposé sur un OS que si son manifeste le déclare et que le catalogue le marque accepté.
7. Vérifier avec le même code que le Launcher : la dernière étape exécute la vérification du Launcher, sans logique de diagnostic parallèle.

Le principe numéro cinq mérite d'être appuyé, car il évite une source classique de divergences. Si l'assistant graphique et le mode non interactif étaient deux implémentations distinctes, ils finiraient par se comporter différemment, et l'on ne saurait plus laquelle fait référence. En imposant une seule logique exécutée par deux interfaces, le projet garantit que le comportement est identique qu'on clique ou qu'on scripte. C'est la même philosophie que le Launcher, où l'orchestration est unique et l'interface n'est qu'un affichage.

## 4. Le parcours de l'assistant

Le parcours prévu compte dix étapes, dont les six premières ne modifient rien sur la machine.

| Étape | Contenu | Écriture disque |
| --: | :-- | :-- |
| 1 | Présentation : ce qui sera installé, et ce qui ne le sera pas | non |
| 2 | Licence : texte affiché en entier, acceptation explicite | non |
| 3 | Contrôle système : résultats classés, avec cause | non |
| 4 | Répertoires : programme et espace de travail, validés | non |
| 5 | Composants et options : choix, tailles, dépendances | non |
| 6 | Récapitulatif : actions, chemins, variables, raccourcis | non |
| 7 | Installation : progression globale et par étape, annulable | oui |
| 8 | Configuration automatique : variables, manifestes, registre, raccourcis, reçu | oui |
| 9 | Vérification : exécution de la vérification du Launcher | oui |
| 10 | Fin : résumé, journal, lancement, documentation | non |

La séparation entre les six premières étapes, qui n'écrivent rien, et les suivantes, est délibérée. Tant que rien n'est écrit, l'utilisateur peut tout annuler sans conséquence. Ce n'est qu'une fois le récapitulatif confirmé que l'installateur commence à modifier la machine. Cette progressivité protège l'utilisateur : il peut explorer les options, vérifier la compatibilité, choisir les composants, et renoncer, sans laisser de trace.

Le contrôle système vérifie notamment le système d'exploitation, l'architecture, l'espace disque, les droits d'écriture, la présence des bibliothèques graphiques nécessaires à l'interface, la session graphique, le réseau, la disponibilité des ports et l'existence d'une installation antérieure. Il réutilise le code du Launcher pour les vérifications communes, afin que le contrôle initial et la vérification finale ne divergent pas.

## 5. Les exigences

Les exigences fonctionnelles et non fonctionnelles sont écrites de façon vérifiable, chacune reliée à un test. On y trouve l'acceptation obligatoire de la licence, le contrôle avant tout changement, le choix validé des répertoires, la sélection de composants avec tailles, l'installation des environnements requis, la configuration automatique, la vérification finale, l'annulation et la restauration en cas d'échec, la détection d'une installation existante (mise à jour, réparation, modification, désinstallation), la désinstallation qui ne retire jamais l'espace de travail sauf demande explicite, un mode non interactif équivalent, une mise à jour atomique qui refuse de s'exécuter pendant un run actif, et une interface en français.

Les exigences non fonctionnelles imposent : aucune élévation par défaut, vérification d'empreinte avant usage d'un artefact téléchargé, binaire auto-porteur, aucune écriture hors des répertoires déclarés, journal lisible sans donnée personnelle superflue, annulation possible à tout moment, fonctionnement au clavier seul, et TLS obligatoire avec absence d'envoi de télémétrie.

Deux exigences méritent d'être soulignées. La mise à jour « refuse de s'exécuter pendant un run actif » : cette règle relie l'installateur à l'orchestration, car remplacer un binaire pendant qu'il tourne corromprait le run. Et l'« absence d'envoi de télémétrie » avec « TLS obligatoire » : l'installateur ne rapporte rien à personne, et toute communication réseau passe par un canal chiffré. Ces choix traduisent une éthique : l'outil est au service de l'utilisateur, non l'inverse.

## 6. Ce que le livre refuse de faire

Ce chapitre ne décrit aucune capture d'écran de l'installateur, parce qu'il n'existe pas. Il ne présente aucune durée d'installation ni aucune taille de téléchargement, parce qu'elles ne sont pas mesurées. Il ne promet aucune date. Il décrit une spécification, qui porte explicitement, dans le dépôt, un avertissement indiquant qu'aucune ligne de code ne l'implémente et qu'aucune affirmation du document ne doit être lue comme décrivant un état courant.

C'est une exigence de la stratégie de communication du projet : ne pas annoncer une capacité comme disponible avant de l'avoir validée. Un chapitre honnête sur l'installateur est donc un chapitre qui dit surtout ce qu'il n'est pas encore.

## 7. Les points ouverts

La spécification laisse plusieurs questions ouvertes, ce qui est normal pour un composant non implémenté : le format de livraison Linux, l'emplacement par défaut de l'espace de travail, la version de Python embarquée, la signature des artefacts, la persistance du choix d'espace de travail, le mécanisme de mise à jour du Launcher lui-même, l'hébergement du catalogue et des artefacts, et le parcours hors ligne.

Ces points sont consignés comme des points ouverts, non résolus en silence. Ils illustrent, une fois encore, la méthode : ce qui n'est pas tranché est écrit comme non tranché.

## 8. Ce que ce chapitre apporte à la question directrice

Ce chapitre a décrit, sans la surestimer, une brique manquante. Il a montré comment le projet traite l'inachevé : le porter au grand jour, le cadrer, en écrire les exigences, et refuser de le présenter comme prêt. C'est cohérent avec tout le livre : ne croire que ce qui se rejoue, et ne pas confondre une promesse avec une livraison.

Ce que ce chapitre ne permet pas encore de conclure : qu'une pile installable suffise à faire de LIVEX un instrument scientifique. Pour cela, il faut relier les composants entre eux, sur le terrain, de bout en bout. C'est l'objet de la partie suivante, qui commence par l'intégration système.
