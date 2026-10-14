# Chapitre 9b. L'installateur : exigences, parcours et garanties

Installer un logiciel de recherche n'est pas une formalité. Ce chapitre décrit ce que l'installateur doit faire, comment il le fait, et surtout ce qu'il s'interdit. Il est écrit comme une spécification lisible, parce que l'installateur est le seul composant du projet qui touche à la machine de l'utilisateur : c'est donc celui où les garanties comptent le plus.

**Pourquoi ce chapitre importe pour la question directrice.** Une observation scientifique n'a de valeur que si elle est reproductible **ailleurs**. Une campagne qui ne tourne que sur la machine de son auteur, avec des dépendances installées à la main, n'est pas reproductible : elle est locale. L'installateur est le composant qui rend la reproduction possible sur un autre poste, en installant exactement les versions annoncées, sans privilèges particuliers et sans dépendre de ce qui traînait déjà sur la machine.

## 1. Une exigence, une vérification

Le document de spécification formule chaque exigence de façon **vérifiable**, et la relie à un test. C'est une discipline héritée du reste du projet : ce qui n'est pas vérifiable n'est pas une exigence, c'est une intention.

Les exigences fonctionnelles couvrent le parcours et les effets. L'assistant guide l'utilisateur par étapes ordonnées, avec retour arrière possible tant que l'installation n'a pas commencé ; la licence doit être affichée en entier et explicitement acceptée ; le contrôle système est exécuté **avant tout changement**, et chaque résultat est classé bloquant, avertissement ou information, avec sa cause en clair ; l'utilisateur choisit deux répertoires, celui du programme et celui de l'espace de travail, validés avant de continuer ; les composants sont des options distinctes, chacune affichant sa taille de téléchargement et sa taille installée ; les runtimes nécessaires sont installés avec le composant qui les exige ; la configuration finale écrit variables, manifestes, registre et raccourcis ; l'installation se termine par un contrôle de santé du Launcher, dont le résultat est affiché **et** journalisé ; une installation interrompue est annulée et l'état précédent restauré ; une installation existante est détectée et ouvre le choix entre mettre à jour, réparer, modifier ou désinstaller ; la désinstallation **ne supprime jamais l'espace de travail** sans demande explicite et confirmée ; un mode non interactif réalise exactement les mêmes opérations que l'assistant ; une mise à jour remplace un composant de façon atomique, conserve la version précédente jusqu'à validation et refuse de s'exécuter pendant qu'un run est actif.

Les exigences non fonctionnelles sont, dans ce composant, les plus intéressantes, parce qu'elles disent ce que l'installateur refuse.

| Exigence | Portée |
| :-- | :-- |
| Aucune élévation de privilèges en installation par utilisateur | l'installation ne modifie pas le système |
| Aucun artefact utilisé avant vérification de son empreinte SHA-256 | ce qui est téléchargé est vérifié |
| Le binaire démarre sans runtime préinstallé | l'installateur ne suppose rien du poste |
| Aucune écriture hors des répertoires choisis, du profil utilisateur, des raccourcis et des variables déclarées | la machine reste propre |
| Journal lisible, sans donnée personnelle hors chemins | traçabilité sans surveillance |
| Annulation possible à tout moment, retour à l'état initial | l'utilisateur garde la main |
| Fonctionnement au clavier seul, contraste conforme | accessibilité |
| TLS obligatoire pour le catalogue et les artefacts ; aucune télémétrie | ni interception, ni espionnage |

Deux d'entre elles méritent d'être soulignées, parce qu'elles sont rares. **Aucune télémétrie** : l'installateur n'envoie rien sur la façon dont il a été utilisé. Et **aucune écriture hors périmètre** : les seuls emplacements touchés sont ceux que l'utilisateur a désignés ou qui lui appartiennent en propre. Ces deux règles font de l'installateur un logiciel local, à une époque où beaucoup ne le sont plus.

## 2. Le parcours : dix étapes, dont six sans effet

Le parcours comporte dix étapes, et l'ordre n'est pas décoratif : les six premières ne modifient **rien** sur la machine. Un utilisateur qui abandonne avant l'étape sept laisse son poste exactement tel qu'il l'a trouvé.

| Étape | Contenu | Effet |
| :-- | :-- | :-- |
| 1 | présentation : ce qui sera installé, ce qui ne le sera pas | aucun |
| 2 | licence, affichée en entier, acceptation explicite | aucun |
| 3 | contrôle système, résultats classés et motivés | aucun |
| 4 | choix des répertoires, validés | aucun |
| 5 | choix des composants, avec tailles et espace libre | aucun |
| 6 | résumé des choix | aucun |
| 7 | installation, avec progression et annulation possible | **écritures** |
| 8 | configuration automatique | **écritures** |
| 9 | contrôle de santé final | lecture |
| 10 | fin, avec le journal d'installation | aucun |

Le contrôle système de l'étape 3 est le point où se décide la suite : chaque vérification renvoie un statut et un message. Un manque de disque est bloquant, une version de système ancienne peut être un avertissement, une information signale ce qui n'empêche rien. La distinction est importante : un contrôle qui se contenterait de dire « incompatible » obligerait l'utilisateur à deviner pourquoi.

L'étape 5 explicite les **dépendances entre composants** : choisir un composant qui exige un autre l'installe automatiquement, et l'interface l'explique. Rien n'est installé en silence.

## 3. Les modes d'exécution

L'installateur possède un mode **interactif** et un mode **non interactif**, et l'exigence est explicite : le second doit réaliser **exactement** les mêmes opérations que le premier. Cette équivalence est ce qui rend l'installation automatisable sans créer deux comportements divergents, un mode silencieux qui ferait « presque » la même chose serait une source inépuisable d'écarts entre deux postes.

Les modes ont des **codes de sortie** documentés, et un **journal d'installation** stable et lisible. Ces deux éléments relèvent de la même logique : une installation doit pouvoir être diagnostiquée sans être rejouée, et son échec doit être distinguable de son succès par un script.

## 4. Les garanties de mise à jour

La mise à jour obéit à trois règles qui reprennent les ADR du composant. Elle est **atomique** par composant : un remplacement partiel n'est pas un état acceptable. Elle **conserve la version précédente** jusqu'à validation, ce qui rend un retour arrière possible. Et elle **refuse de s'exécuter pendant qu'un run est actif** : modifier un binaire sous un monde en cours d'exécution serait la meilleure façon de produire une campagne irreproductible, un résultat obtenu avec deux versions du moteur, dont aucune ne serait la bonne.

Le catalogue est **signé**, et chaque artefact est vérifié par empreinte avant usage. Un catalogue non signé serait un point d'entrée idéal pour un artefact substitué : la vérification n'est pas une formalité de sécurité, c'est ce qui permet de dire que la version installée est bien celle qui est annoncée.

## 5. Ce que ce chapitre apporte à la question directrice

Ce chapitre a décrit le composant qui rend la reproductibilité **portable**. Les exigences vérifiables, les six étapes sans effet, l'équivalence des modes, l'atomicité des mises à jour et la vérification des empreintes forment un ensemble : installer LIVEX revient à reproduire un environnement précis, sans privilèges, sans télémétrie, sans toucher au reste de la machine.

C'est la condition matérielle de la question directrice. Une structure observée doit pouvoir l'être par quelqu'un d'autre, sur un autre poste, avec les mêmes versions. Sans installateur, chaque reproduction serait une reconstruction manuelle, donc une variante.

**Ce que ce chapitre ne permet pas de conclure.**

- L'installateur est à l'état de **spécification** : il n'est pas implémenté, et ce chapitre décrit des exigences, non un comportement observé.
- La liaison entre chaque exigence et un test est prévue, mais aucun test n'existe encore.
- La vérification d'intégrité repose sur des empreintes et un catalogue signé, ce qui établit la provenance, non l'absence de faille dans la chaîne de distribution.
- Le « refus de mise à jour pendant un run actif » suppose que l'installateur puisse détecter un run actif : la méthode de détection n'est pas fixée dans ce chapitre.
- Rien ne garantit encore qu'une installation complète produise un environnement identique sur deux systèmes d'exploitation différents.
