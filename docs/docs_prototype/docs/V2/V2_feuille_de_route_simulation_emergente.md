# V2 --- Feuille de route conceptuelle

## Simulation systémique émergente --- Agents autonomes, décision et interactions

> **Statut :** document de cadrage initial de la V2\
> **Source principale :** Guillaume Asselin, *Une approche multi-agents
> pour le développement d'un jeu vidéo*, Université de Montréal, 2013.\
> **Objectif :** transformer les enseignements du mémoire en
> orientations concrètes pour la conception de la V2, sans confondre
> système multi-agents et émergence.

------------------------------------------------------------------------

## 1. Objet de ce document

Ce document constitue une première feuille de route pour la V2 du moteur
de simulation.

Il ne s'agit pas encore de définir les classes, interfaces ou structures
de données définitives. Son rôle est de déterminer :

-   les concepts à conserver ;
-   les concepts à introduire en V2 ;
-   les concepts à repousser ;
-   les concepts à éviter ;
-   les principes d'architecture à documenter ensuite ;
-   les critères permettant de distinguer un comportement émergent d'un
    comportement simplement scripté.

Le mémoire d'Asselin constitue ici une **source de comparaison et de
justification**, et non un modèle à reproduire tel quel.

Le mémoire définit un système multi-agents comme un ensemble d'agents
autonomes qui interagissent dans un environnement commun. Son objectif
est principalement d'améliorer le comportement d'agents non humains dans
un jeu vidéo. \[Source : mémoire, résumé et introduction.\]

------------------------------------------------------------------------

# 2. Conclusion générale de l'analyse

La lecture du mémoire confirme une direction importante pour la V2 :

> **La V2 doit enrichir l'agent plutôt que multiplier les scripts de
> comportement.**

Le mémoire montre plusieurs niveaux d'architecture :

1.  agent réactif ;
2.  agent conservant une trace du monde ;
3.  agent délibératif ;
4.  agent basé sur l'utilité ;
5.  architecture BDI.

Pour notre projet, la direction la plus pertinente est une combinaison
de :

-   perception ;
-   mémoire ;
-   croyances/connaissances ;
-   besoins et états internes ;
-   objectifs/désirs ;
-   évaluation par utilité ;
-   intention ;
-   action ;
-   interaction avec l'environnement et les autres agents.

Le mémoire indique notamment que les agents délibératifs sont plus
flexibles que les agents purement réactifs, au prix d'un raisonnement
plus coûteux. La fonction d'utilité permet de gérer les compromis entre
objectifs contradictoires. \[Source : mémoire, section sur les agents
délibératifs et l'utilité.\]

**Décision V2 : adopter cette direction comme architecture conceptuelle
de référence.**

------------------------------------------------------------------------

# 3. Ce que le mémoire apporte directement à la V2

## 3.1. Perception → décision → action

Le mémoire rappelle qu'un agent peut être considéré comme une fonction
reliant ses perceptions à ses actions. Il perçoit l'environnement avec
ses capteurs et agit à l'aide de ses effecteurs.

### Application V2

L'agent devra conserver une séparation claire entre :

``` text
Perception
    ↓
État interne / connaissances
    ↓
Décision
    ↓
Intention
    ↓
Action
```

Cette séparation est importante car elle permet de modifier la logique
décisionnelle sans modifier les mécanismes physiques ou biologiques de
l'agent.

### Décision

**À ajouter en V2.**

------------------------------------------------------------------------

# 4. Mémoire et représentation partielle du monde

Le mémoire décrit les agents capables de conserver une trace du monde.

Ils ne doivent plus dépendre uniquement de ce qu'ils perçoivent à
l'instant présent. Leur état interne peut intégrer :

-   l'état précédent du monde ;
-   l'évolution supposée du monde ;
-   les conséquences de leurs propres actions ;
-   les informations déjà observées.

Le mémoire explique que cette mémoire permet de construire une
perception enrichie du monde avant de prendre une décision.

## Application V2

Introduire une mémoire d'agent permettant de stocker des informations
telles que :

``` text
"J'ai vu de la nourriture dans cette zone."
"Cette personne était présente hier."
"Ce lieu est dangereux."
"Ce groupe occupe généralement cette région."
"J'ai déjà échoué dans cette situation."
```

La mémoire ne doit cependant pas devenir une copie complète du monde.

### Principe

> **Un agent ne doit connaître que ce qu'il peut raisonnablement
> connaître.**

Cela est essentiel pour la simulation émergente : les agents doivent
disposer d'informations différentes selon leurs expériences, leurs
perceptions et leurs interactions.

### Décision

**À ajouter en V2.**

------------------------------------------------------------------------

# 5. Croyances et connaissances

Le modèle BDI présenté dans le mémoire distingue les croyances, les
désirs et les intentions.

Les croyances représentent les informations que possède l'agent sur son
environnement. Elles peuvent être révisées à partir des perceptions.

## Application V2

Il est pertinent de distinguer :

``` text
MONDE RÉEL
    ↓ perception
INFORMATIONS OBSERVÉES
    ↓ interprétation / mémoire
CROYANCES DE L'AGENT
```

Un agent pourrait donc avoir une croyance incorrecte.

Exemple :

``` text
Monde réel :
    le village possède encore 20 unités de nourriture.

Agent :
    croit qu'il reste 5 unités.
```

Cette différence est extrêmement intéressante pour l'émergence.

Elle permet notamment :

-   erreurs de décision ;
-   rumeurs ;
-   informations obsolètes ;
-   stratégies fondées sur de fausses croyances ;
-   conflits entre groupes ayant des informations différentes.

### Décision

**À ajouter en V2, mais sous une forme simple.**

Il ne faut pas commencer par un moteur logique complexe. Une
représentation structurée de faits connus, avec niveau de confiance et
date de dernière observation, est suffisante pour une première version.

------------------------------------------------------------------------

# 6. Besoins et états internes

Le mémoire est principalement orienté vers des agents de jeu poursuivant
des objectifs. Notre simulation vise un monde beaucoup plus général.

La V2 doit donc introduire une couche supplémentaire :

``` text
État interne
    ↓
Besoins
    ↓
Objectifs
```

Exemples :

``` text
faim
soif
fatigue
sécurité
besoin social
besoin économique
besoin de repos
```

Ces besoins ne constituent pas directement des actions.

Ils influencent la priorité des objectifs.

Exemple :

``` text
Faim élevée
+
nourriture disponible
+
danger modéré
        ↓
objectif : trouver / obtenir de la nourriture
```

Cette approche est préférable à :

``` text
SI faim > 80
ALORS manger
```

car elle permet plusieurs solutions possibles.

------------------------------------------------------------------------

# 7. Utilité : un élément central de la V2

Le mémoire explique que les simples buts présentent une limitation
importante : ils indiquent principalement si un état est atteint ou non.

La fonction d'utilité permet au contraire de comparer différents états
et différents compromis.

Elle permet notamment de gérer :

-   plusieurs objectifs ;
-   des objectifs contradictoires ;
-   des probabilités de réussite ;
-   différents coûts.

## Application V2

La décision d'un agent pourrait être conceptuellement :

``` text
Utility(action) =
    bénéfices
    - coûts
    - risques
    + satisfaction des besoins
    + cohérence avec les objectifs
```

Exemple :

``` text
Action A : chercher de la nourriture proche
    + faim réduite
    + faible risque
    + faible coût énergétique

Action B : chasser un animal rare
    + forte récompense potentielle
    - coût énergétique élevé
    - risque élevé
```

Deux agents ayant des pondérations différentes peuvent choisir des
actions différentes dans exactement la même situation.

### Exemple

``` text
Agent prudent :
    sécurité = très importante
    richesse = secondaire

Agent ambitieux :
    richesse = très importante
    sécurité = secondaire
```

Le comportement différent ne nécessite pas deux scripts différents.

Il peut provenir des paramètres internes de l'agent.

### Décision

**À ajouter en V2 --- priorité élevée.**

------------------------------------------------------------------------

# 8. BDI : à adopter comme modèle conceptuel, pas comme framework lourd

Le mémoire présente BDI :

-   **Beliefs** --- croyances ;
-   **Desires** --- désirs/options ;
-   **Intentions** --- intentions.

Le cycle conceptuel est particulièrement adapté à notre simulation :

``` text
Perception
    ↓
Révision des croyances
    ↓
Génération des possibilités
    ↓
Désirs / objectifs
    ↓
Délibération
    ↓
Intention
    ↓
Action
```

## Application V2

Nous pouvons reprendre ce modèle sans nécessairement implémenter un
système BDI académique complet.

Architecture recommandée :

``` text
Agent
 ├── Perception
 ├── Memory
 ├── Beliefs
 ├── InternalState
 ├── Needs
 ├── Goals
 ├── Decision
 ├── Intention
 └── Actions
```

### Pourquoi ?

Parce que cette séparation permet de répondre à une question
fondamentale :

> Pourquoi l'agent réalise-t-il cette action ?

On peut alors retracer :

``` text
Action
← intention
← objectif
← besoin
← croyance
← perception / mémoire
```

Cela sera extrêmement important pour le débogage et l'analyse de la
simulation.

### Décision

**À ajouter en V2 sous forme conceptuelle et modulaire.**

------------------------------------------------------------------------

# 9. Behaviour Trees : conserver l'idée, éviter d'en faire le cerveau principal

Le mémoire utilise un arbre de décisions / Behaviour Tree pour organiser
les comportements.

L'auteur souligne plusieurs avantages :

-   fonctions élémentaires réutilisables ;
-   structure facilement modifiable ;
-   combinaison de comportements ;
-   meilleure flexibilité qu'une machine à états finie dans son
    contexte.

Il constate également que les arbres sont pratiques pour tester
plusieurs combinaisons de comportements.

## Application V2

Les Behaviour Trees peuvent rester utiles pour des comportements
**locaux ou procéduraux**.

Exemple :

``` text
Action : manger

    chercher nourriture
        ↓
    se déplacer
        ↓
    vérifier disponibilité
        ↓
    consommer
```

Ils peuvent également servir à orchestrer une action complexe.

### Mais :

**Ne pas faire du Behaviour Tree le système décisionnel global de
l'agent.**

Un énorme arbre du type :

``` text
SI faim
    SI danger
        SI ...
            ...
```

finirait par reproduire exactement le problème que la V2 cherche à
éviter :

> transformer toutes les possibilités comportementales en règles
> explicitement prévues par le développeur.

### Décision

**Conserver comme outil d'exécution / décomposition d'actions.**

**Éviter comme cerveau central de la simulation.**

------------------------------------------------------------------------

# 10. Machines à états : à éviter comme architecture principale

Le mémoire souligne plusieurs limites des machines à états finies :

-   rigidité ;
-   multiplication des conditions ;
-   faible réutilisabilité ;
-   comportement facilement prévisible ;
-   modification difficile lorsque le nombre d'états augmente.

Ces limites deviennent encore plus importantes dans notre projet.

Une architecture :

``` text
Idle
 ↓
Hungry
 ↓
SearchingFood
 ↓
Eating
 ↓
Sleeping
```

peut être utile pour représenter certains états techniques.

Mais elle ne doit pas déterminer seule les décisions de haut niveau.

### Décision

**Ne pas utiliser une FSM comme architecture cognitive principale.**

Elle peut néanmoins être utilisée localement pour gérer des états
techniques simples.

------------------------------------------------------------------------

# 11. Stratégies prédéfinies : le principal élément à ne pas reproduire

C'est probablement le point de divergence le plus important entre le
mémoire et notre V2.

Dans le mémoire, les agents possèdent six stratégies explicitement
programmées :

1.  aller vers la nourriture la plus proche ;
2.  aller vers la nourriture de plus grande valeur ;
3.  voler une ressource au joueur ;
4.  s'isoler ;
5.  sacrifier des points pour bloquer le joueur ;
6.  coopérer pour bloquer le joueur.

Les stratégies sont ensuite sélectionnées selon l'état de la partie et
de manière semi-aléatoire.

Cette architecture fonctionne très bien pour l'objectif du mémoire.

Mais elle est insuffisante pour notre objectif.

## Pourquoi ?

Parce que :

``` text
Développeur
    ↓
définit les comportements
    ↓
agent choisit parmi ces comportements
```

produit principalement de la variation comportementale.

Alors que nous cherchons :

``` text
Développeur
    ↓
définit les règles du monde
    ↓
définit les capacités / besoins / contraintes
    ↓
agents interagissent
    ↓
comportements collectifs
    ↓
résultats non entièrement prévus
```

### Décision

**Éviter les listes fermées de stratégies comme modèle central.**

------------------------------------------------------------------------

# 12. Coopération et communication

Le mémoire démontre un intérêt concret de la communication inter-agents.

Dans son système, un agent annonce sa destination. Les autres agents
évitent alors cette destination.

Il existe également une communication permettant de demander de l'aide
pour une action collective.

Le mémoire insiste sur l'importance de la quantité et de la qualité des
informations échangées, tout en signalant le risque de surcharge.

## Application V2

La communication doit être considérée comme une **ressource du
système**, et non comme une fonction magique.

Un agent devrait pouvoir :

``` text
envoyer une information
recevoir une information
mémoriser une information
évaluer sa fiabilité
ignorer une information
transmettre une information
```

Exemple :

``` text
Agent A :
"J'ai trouvé de la nourriture."

Agent B :
reçoit l'information

Agent B :
évalue distance + confiance + besoin

Agent B :
décide éventuellement de se déplacer.
```

------------------------------------------------------------------------

# 13. Ne pas donner une communication parfaite

Le mémoire utilise une communication très simple et directe.

Pour notre simulation, il serait intéressant de conserver certaines
limitations :

-   portée ;
-   délai ;
-   coût ;
-   connaissance du destinataire ;
-   fiabilité ;
-   possibilité d'erreur ;
-   information partielle.

Cela permettrait d'obtenir des phénomènes comme :

``` text
A sait quelque chose
B ne le sait pas
C croit que B le sait
D reçoit une information ancienne
```

Ces situations sont beaucoup plus intéressantes pour une simulation
émergente qu'une base de données globale accessible à tous.

### Décision

**Ajouter en V2, mais progressivement.**

------------------------------------------------------------------------

# 14. Coalition et organisation

Le mémoire montre qu'un groupe peut coordonner ses agents autour d'un
objectif commun.

Il décrit notamment une forme de coalition dans laquelle plusieurs
agents modifient leur comportement pour augmenter leurs chances
collectives.

Pour notre projet, cette idée peut être généralisée :

``` text
Agent
 ↓
relation
 ↓
groupe
 ↓
organisation
```

Mais il faut éviter de créer directement :

``` text
GroupController
    → commande tous les agents
```

Cela détruirait une partie de l'autonomie recherchée.

## Approche recommandée

Le groupe doit plutôt émerger de :

-   relations ;
-   intérêts communs ;
-   communication ;
-   confiance ;
-   objectifs compatibles ;
-   dépendance mutuelle ;
-   échanges de ressources ;
-   menaces communes.

Ainsi, une organisation peut apparaître sans être entièrement définie à
l'avance.

### Décision

**Préparer les primitives en V2, mais repousser les organisations
complexes à une V3.**

------------------------------------------------------------------------

# 15. Navigation : reprendre le principe, changer l'échelle

Le mémoire utilise A\* pour la recherche de chemins et souligne un
problème essentiel :

> les obstacles dynamiques imposent des recalculs continus.

L'auteur indique que le coût reste faible dans son exemple avec trois
agents et une petite grille, mais qu'il pourrait devenir problématique
avec beaucoup plus d'agents et un environnement plus vaste.

## Application V2

La navigation doit être séparée de la décision.

``` text
Décision :
    "Je veux rejoindre cette zone."

Navigation :
    "Voici comment m'y rendre."
```

L'agent ne devrait donc pas contenir directement toute la logique de
pathfinding.

### Architecture

``` text
Agent
   ↓
Destination
   ↓
Navigation Service
   ↓
Path
   ↓
Movement
```

Cela permettra plus tard de remplacer la technologie de navigation sans
modifier le modèle cognitif.

------------------------------------------------------------------------

# 16. Ne pas recalculer tout pour tout le monde à chaque tick

Le problème des obstacles dynamiques observé dans le mémoire devient
critique à notre échelle.

Avec plusieurs milliers d'agents, il serait dangereux de faire :

``` text
À chaque tick :

pour chaque agent
    recalculer le chemin complet
```

## Principe V2

Les recalculs doivent être déclenchés par des événements ou des
changements significatifs :

``` text
destination changée
OU
chemin bloqué
OU
danger important
OU
environnement modifié
OU
intervalle de réévaluation atteint
```

Cette approche permet de dissocier :

-   simulation logique ;
-   décision ;
-   navigation ;
-   fréquence de mise à jour.

### Décision

**Principe architectural important à intégrer en V2.**

------------------------------------------------------------------------

# 17. Environnement : surtout ne pas le rendre omniscient

Le jeu du mémoire utilise un environnement entièrement observable : les
agents connaissent en permanence la position des ressources et des
autres serpents.

C'est parfaitement adapté à son cas.

Mais pour notre projet, cette propriété doit être abandonnée.

## V2

Le monde possède :

``` text
État réel du monde
```

Chaque agent possède :

``` text
Perception personnelle
+
Mémoire personnelle
+
Croyances personnelles
```

Donc :

``` text
MONDE
 ├── vérité réelle
 │
 ├── Agent A → représentation partielle
 ├── Agent B → représentation partielle
 └── Agent C → représentation partielle
```

### Pourquoi ?

Parce qu'une connaissance globale partagée par tous les agents
empêcherait une grande partie des phénomènes émergents liés à
l'information.

### Décision

**Passer d'un monde entièrement observable à un monde partiellement
observable.**

------------------------------------------------------------------------

# 18. Monde dynamique

Le mémoire classe son environnement comme dynamique : les ressources et
les agents changent continuellement.

Cela confirme que la V2 doit considérer le monde comme un système en
évolution permanente.

Une action doit produire des conséquences :

``` text
Agent coupe du bois
    ↓
stock de bois diminue
    ↓
forêt évolue
    ↓
ressource future modifiée
    ↓
autres agents affectés
```

L'intérêt de la simulation vient précisément de ces chaînes de
conséquences.

### Décision

**Principe fondamental de V2.**

------------------------------------------------------------------------

# 19. Éviter les règles globales artificielles

Une règle comme :

``` text
SI village A possède moins de 20 nourriture
ALORS village B envoie automatiquement 10 nourriture
```

est très efficace pour reproduire un scénario précis.

Mais elle réduit l'intérêt de la simulation si elle est utilisée
systématiquement.

Une approche plus intéressante serait :

``` text
Village A
    manque de nourriture

Agents de A
    cherchent des solutions

Agent B
    possède un surplus

Relation A-B
    confiance élevée

B reçoit une demande

B évalue :
    coût du don
    relation
    besoin de A
    ses propres besoins

B décide éventuellement d'aider
```

Le comportement collectif devient alors une conséquence des mécanismes
du monde.

------------------------------------------------------------------------

# 20. Émergence : critère de conception de la V2

Il faut définir précisément ce que nous voulons appeler « émergent ».

Un comportement sera considéré comme intéressant s'il :

1.  n'est pas une action directement ordonnée par une règle globale ;
2.  résulte de l'interaction de plusieurs mécanismes locaux ;
3.  peut varier selon les conditions initiales ;
4.  peut produire des conséquences secondaires ;
5.  peut influencer à son tour les décisions futures.

Exemple :

``` text
besoin de nourriture
+
ressources limitées
+
commerce
+
relations
+
information partielle
+
distance
+
risque
        ↓
déplacement de population
        ↓
concentration d'agents
        ↓
augmentation de la demande
        ↓
hausse des échanges
        ↓
apparition d'une spécialisation
        ↓
formation d'un nouveau centre économique
```

Aucun système n'a besoin de contenir une règle :

``` text
"Créer une ville commerciale."
```

La ville peut apparaître comme conséquence de la dynamique.

------------------------------------------------------------------------

# 21. Différence entre émergence et comportement scripté

Cette distinction doit être intégrée à la documentation V2.

## Comportement scripté

``` text
SI événement X
ALORS comportement Y
```

## Comportement paramétré

``` text
Agent choisit Y
car Y maximise son utilité
```

## Comportement émergent

``` text
Plusieurs agents
+
règles locales
+
ressources
+
interactions
+
boucles de rétroaction
        ↓
phénomène collectif non spécifié explicitement
```

La V2 doit privilégier le troisième niveau.

------------------------------------------------------------------------

# 22. Ce qu'il faut ajouter en V2

## Priorité P0 --- architecture de base

À implémenter :

-   [ ] Perception séparée de la décision
-   [ ] État interne
-   [ ] Besoins
-   [ ] Objectifs
-   [ ] Mémoire
-   [ ] Croyances / connaissances
-   [ ] Intentions
-   [ ] Actions modulaires
-   [ ] Fonction d'utilité
-   [ ] Environnement partiellement observable
-   [ ] Système d'interactions

## Priorité P1 --- enrichissement

-   [ ] Communication inter-agents
-   [ ] Confiance dans les informations
-   [ ] Informations datées
-   [ ] Relations entre agents
-   [ ] Coûts des interactions
-   [ ] Navigation découplée
-   [ ] Réévaluation de décision adaptative
-   [ ] Événements du monde

## Priorité P2 --- dynamique collective

-   [ ] Groupes
-   [ ] Coopération
-   [ ] Conflits
-   [ ] Échanges
-   [ ] Coalitions
-   [ ] rôles dynamiques
-   [ ] dépendances entre agents

## P3 --- expérimentation

-   [ ] apprentissage
-   [ ] évolution des préférences
-   [ ] inférence avancée
-   [ ] transmission culturelle
-   [ ] institutions
-   [ ] structures sociales complexes

------------------------------------------------------------------------

# 23. Ce qu'il faut éviter en V2

## 23.1. Éviter le Behaviour Tree comme cerveau global

Il est utile pour exécuter une action complexe, mais ne doit pas
contenir toute la psychologie de l'agent.

------------------------------------------------------------------------

## 23.2. Éviter les énormes FSM

Elles deviennent rapidement rigides et difficiles à maintenir.

------------------------------------------------------------------------

## 23.3. Éviter une liste fermée de comportements

Ne pas construire :

``` text
enum Strategy
{
    Hunt,
    Trade,
    Steal,
    Flee,
    Socialize,
    ...
}
```

puis demander simplement à l'agent d'en sélectionner une.

Cette approche est trop proche du modèle du mémoire, où les stratégies
sont explicitement définies.

------------------------------------------------------------------------

## 23.4. Éviter l'omnipotence de l'agent

Un agent ne doit pas avoir accès directement à :

``` text
World.AllAgents
World.AllResources
World.AllEvents
```

pour prendre ses décisions.

Il doit passer par ses perceptions et connaissances.

------------------------------------------------------------------------

## 23.5. Éviter l'apprentissage automatique trop tôt

Le mémoire lui-même identifie l'apprentissage comme une piste
d'amélioration mais souligne le travail supplémentaire nécessaire.

La V2 doit d'abord démontrer que les mécanismes déterministes produisent
déjà une dynamique intéressante.

------------------------------------------------------------------------

## 23.6. Éviter les systèmes sociaux complexes trop tôt

Il ne faut pas implémenter immédiatement :

-   politique ;
-   religion ;
-   économie complexe ;
-   institutions ;
-   cultures ;
-   langues ;
-   diplomatie avancée.

Ces systèmes doivent pouvoir émerger ou être ajoutés progressivement à
partir de primitives plus simples.

------------------------------------------------------------------------

# 24. Architecture conceptuelle V2 proposée

``` text
┌──────────────────────────────────────────────┐
│                    AGENT                     │
├──────────────────────────────────────────────┤
│                                              │
│  Perception                                  │
│      ↓                                       │
│  Memory                                      │
│      ↓                                       │
│  Beliefs / Knowledge                         │
│      ↓                                       │
│  Internal State                               │
│      ↓                                       │
│  Needs                                        │
│      ↓                                       │
│  Goals / Desires                              │
│      ↓                                       │
│  Utility Evaluation                           │
│      ↓                                       │
│  Deliberation                                 │
│      ↓                                       │
│  Intention                                    │
│      ↓                                       │
│  Action Selection                             │
│      ↓                                       │
│  Action Execution                             │
│                                              │
└──────────────────┬───────────────────────────┘
                   ↓
            ENVIRONMENT
                   ↓
        Other Agents / Resources
                   ↓
             New Perceptions
```

Cette architecture reprend plusieurs principes présentés dans le mémoire
tout en les adaptant à une simulation systémique persistante.

------------------------------------------------------------------------

# 25. Séparation des responsabilités

La V2 devrait viser une séparation nette :

  Module           Responsabilité
  ---------------- ------------------------------------------------
  Perception       déterminer ce que l'agent peut observer
  Memory           conserver des informations passées
  Beliefs          représenter ce que l'agent croit savoir
  Internal State   représenter son état interne
  Needs            déterminer ses tensions / besoins
  Goals            représenter les états désirables
  Utility          comparer les possibilités
  Decision         choisir une intention
  Action           représenter ce que l'agent peut faire
  Navigation       calculer comment atteindre une destination
  Communication    transmettre / recevoir des informations
  Environment      simuler les règles du monde
  Relationship     représenter les relations avec d'autres agents

Cette séparation permettra également de tester chaque couche
indépendamment.

------------------------------------------------------------------------

# 26. Boucle décisionnelle

Une boucle V2 pourrait être :

``` text
1. Percevoir
       ↓
2. Mettre à jour les connaissances
       ↓
3. Mettre à jour l'état interne
       ↓
4. Mettre à jour les besoins
       ↓
5. Générer les objectifs pertinents
       ↓
6. Générer les actions possibles
       ↓
7. Évaluer les actions
       ↓
8. Choisir une intention
       ↓
9. Exécuter l'action
       ↓
10. Produire des conséquences
       ↓
11. Modifier le monde
       ↓
12. Nouveaux événements / perceptions
       ↓
13. Recommencer
```

Cette boucle ne doit pas nécessairement être exécutée intégralement à
chaque tick pour chaque agent.

La fréquence de chaque sous-système doit être configurable.

------------------------------------------------------------------------

# 27. Importance de la fréquence de décision

Tous les systèmes n'ont pas besoin de tourner à la même fréquence.

Exemple conceptuel :

``` text
Movement        → haute fréquence
Perception      → fréquence moyenne
Needs           → fréquence moyenne
Decision        → fréquence adaptative
Long-term goals → faible fréquence
Social analysis → faible fréquence
```

Un agent peut donc continuer à marcher sans recalculer entièrement ses
objectifs à chaque frame.

Cela est particulièrement important pour les milliers d'agents visés par
le projet.

------------------------------------------------------------------------

# 28. Paramétrage des agents

Un objectif important de la V2 est de pouvoir produire des agents
différents sans créer des classes différentes.

Exemple :

``` text
Agent A
    riskTolerance = 0.2
    socialNeed = 0.8
    wealthPreference = 0.4

Agent B
    riskTolerance = 0.8
    socialNeed = 0.3
    wealthPreference = 0.9
```

Les deux utilisent les mêmes mécanismes.

Leur comportement diverge à cause de leurs paramètres.

C'est préférable à :

``` text
FarmerAgent
MerchantAgent
CowardAgent
GreedyAgent
SocialAgent
```

si ces classes ne représentent pas de véritables différences
structurelles.

------------------------------------------------------------------------

# 29. Importance des boucles de rétroaction

Pour obtenir une dynamique émergente, la V2 doit favoriser les boucles :

``` text
Action
 ↓
Modification du monde
 ↓
Modification des ressources
 ↓
Modification des besoins
 ↓
Modification des décisions
 ↓
Nouvelles actions
```

Exemple :

``` text
surexploitation d'une forêt
        ↓
diminution du bois
        ↓
augmentation du prix
        ↓
recherche de nouvelles sources
        ↓
migration
        ↓
développement d'une nouvelle région
        ↓
nouvelles routes
        ↓
nouveaux échanges
```

C'est cette rétroaction qui doit devenir le moteur principal de
l'émergence.

------------------------------------------------------------------------

# 30. Le rôle de la communication

Le mémoire montre qu'une information très simple --- la destination d'un
agent --- peut modifier les décisions des autres et améliorer la
coordination.

Pour la V2, nous devons généraliser cette idée :

``` text
Information
    ↓
Transmission
    ↓
Interprétation
    ↓
Croyance
    ↓
Décision
```

La communication devient donc elle-même une cause potentielle de
comportements émergents.

------------------------------------------------------------------------

# 31. Rôles dynamiques

Le mémoire indique qu'un SMA peut posséder une organisation, une
division des tâches ou une hiérarchie, et que certains rôles peuvent
évoluer dynamiquement.

Cette idée est pertinente pour notre projet, mais elle doit être
introduite progressivement.

### V2

Prévoir la possibilité qu'un agent ait :

``` text
role
```

mais permettre que le rôle soit une conséquence de ses compétences, de
ses objectifs et de la situation.

Exemple :

``` text
forte compétence chasse
+
besoin de nourriture du groupe
+
demande collective
        ↓
agent devient temporairement chasseur
```

### V3

Explorer des rôles sociaux et organisations complexes.

------------------------------------------------------------------------

# 32. Apprentissage : position recommandée

L'apprentissage ne doit pas être au cœur de la V2.

Le mémoire le présente comme une amélioration possible, mais reconnaît
le travail supplémentaire qu'il implique.

Pour notre projet, l'ordre recommandé est :

``` text
V1
Règles simples

V2
Agents autonomes + utilité + mémoire + interactions

V3
Adaptation / apprentissage

V4
Apprentissage avancé / évolution éventuelle
```

La raison est méthodologique :

si les agents apprennent dès le départ, il devient beaucoup plus
difficile de déterminer si un phénomène provient :

-   des règles ;
-   des paramètres ;
-   de l'apprentissage ;
-   des conditions initiales ;
-   des interactions.

La V2 doit donc rester suffisamment déterministe et explicable pour
permettre l'analyse.

------------------------------------------------------------------------

# 33. Analyse et instrumentation

Le mémoire mesure notamment les performances de différentes stratégies
et analyse les résultats après plusieurs parties.

Notre simulation doit aller plus loin sur ce point.

La V2 doit prévoir dès le départ un système permettant d'enregistrer :

``` text
Agent actions
Agent decisions
Agent goals
Agent needs
Agent beliefs
Agent relationships
Resource changes
Population changes
Group formation
Economic exchanges
Deaths / births
Migration
```

Mais il faut distinguer :

``` text
Simulation state
```

et

``` text
Analysis / logging
```

Le moteur ne doit pas être rempli de logs coûteux en permanence.

------------------------------------------------------------------------

# 34. Critères de validation de la V2

La V2 ne doit pas seulement être évaluée par :

> « Est-ce que les agents prennent des décisions plausibles ? »

Elle doit également être évaluée par :

### Niveau individuel

-   Les agents répondent-ils à leurs besoins ?
-   Leurs décisions sont-elles cohérentes avec leurs préférences ?
-   Leur mémoire influence-t-elle leurs décisions ?
-   Deux agents différents peuvent-ils prendre des décisions différentes
    ?

### Niveau interaction

-   Les agents peuvent-ils coopérer ?
-   Peuvent-ils entrer en conflit ?
-   Les informations modifient-elles réellement les comportements ?
-   Les relations influencent-elles les décisions ?

### Niveau collectif

-   Des groupes apparaissent-ils ?
-   Des concentrations de population apparaissent-elles ?
-   Des flux de ressources apparaissent-ils ?
-   Des spécialisations apparaissent-elles ?
-   Des comportements collectifs non explicitement codés
    apparaissent-ils ?

### Niveau systémique

-   Une modification locale produit-elle des conséquences à distance ?
-   Existe-t-il des boucles de rétroaction ?
-   Les changements du monde modifient-ils à leur tour les agents ?
-   Les résultats varient-ils selon les conditions initiales ?

------------------------------------------------------------------------

# 35. Expériences V2 à prévoir

La V2 devrait être développée avec des expériences contrôlées.

Exemple :

``` text
Expérience A
10 agents
1 ressource
aucune communication

Expérience B
10 agents
1 ressource
communication activée

Expérience C
10 agents
plusieurs ressources
préférences différentes

Expérience D
100 agents
ressources limitées
relations

Expérience E
1000 agents
monde persistant
```

L'objectif n'est pas uniquement de voir si « ça marche ».

Il faut comparer les dynamiques.

------------------------------------------------------------------------

# 36. Le mémoire comme référence, pas comme architecture finale

Les éléments du mémoire à reprendre :

-   définition d'un agent autonome ;
-   perception et action ;
-   mémoire / trace du monde ;
-   agents délibératifs ;
-   fonction d'utilité ;
-   BDI ;
-   communication ;
-   coordination ;
-   coopération ;
-   organisation ;
-   séparation de la navigation et de la décision ;
-   attention aux obstacles dynamiques ;
-   nécessité de mesurer les résultats.

Les éléments à ne pas reproduire directement :

-   environnement entièrement observable ;
-   petit ensemble fermé de stratégies ;
-   sélection semi-aléatoire des stratégies ;
-   Behaviour Tree comme système décisionnel central ;
-   objectif global unique ;
-   coopération explicitement conçue autour d'un scénario ;
-   architecture directement dépendante du jeu ;
-   apprentissage introduit trop tôt.

------------------------------------------------------------------------

# 37. Roadmap V2 proposée

## Phase V2.0 --- Architecture cognitive

Objectif :

> construire un agent capable de percevoir, mémoriser, avoir des besoins
> et prendre une décision.

Implémenter :

-   Perception
-   Memory
-   Beliefs
-   Internal State
-   Needs
-   Goals
-   Utility
-   Decision
-   Intention
-   Actions

------------------------------------------------------------------------

## Phase V2.1 --- Monde partiellement observable

Objectif :

> supprimer l'omnipotence des agents.

Implémenter :

-   perception locale ;
-   portée ;
-   mémoire ;
-   connaissances datées ;
-   croyances ;
-   informations manquantes.

------------------------------------------------------------------------

## Phase V2.2 --- Interactions

Objectif :

> permettre aux agents de modifier réellement leurs décisions
> mutuellement.

Implémenter :

-   communication ;
-   relations ;
-   échanges ;
-   coopération ;
-   conflits ;
-   demandes ;
-   réponses.

------------------------------------------------------------------------

## Phase V2.3 --- Dynamique systémique

Objectif :

> produire des chaînes de causalité longues.

Implémenter :

-   ressources ;
-   consommation ;
-   production ;
-   événements ;
-   conséquences différées ;
-   rétroactions.

------------------------------------------------------------------------

## Phase V2.4 --- Groupes

Objectif :

> observer les premiers comportements collectifs.

Implémenter progressivement :

-   groupes ;
-   rôles ;
-   intérêts communs ;
-   coopération ;
-   coalitions ;
-   changement de rôle.

------------------------------------------------------------------------

## Phase V2.5 --- Instrumentation

Objectif :

> rendre les comportements observables et analysables.

Implémenter :

-   journal de décisions ;
-   historique des actions ;
-   métriques ;
-   événements ;
-   statistiques ;
-   outils d'analyse.

------------------------------------------------------------------------

# 38. Architecture technique cible

La V2 doit conserver la séparation déjà envisagée entre simulation et
représentation.

``` text
                 ┌─────────────────────┐
                 │   Simulation Engine │
                 │       .NET/C#       │
                 └──────────┬──────────┘
                            │
              ┌─────────────┴─────────────┐
              │                           │
       Simulation API              Event Stream
              │                           │
              └─────────────┬─────────────┘
                            │
                ┌───────────┴───────────┐
                │                       │
          Unity / 3D              Analyzer
```

Le moteur de simulation doit pouvoir fonctionner sans rendu graphique.

Unity doit représenter le monde, pas en être le cerveau.

------------------------------------------------------------------------

# 39. Principe architectural majeur

La V2 doit pouvoir fonctionner dans cette situation :

``` text
Lancer la simulation
        ↓
aucune fenêtre graphique
        ↓
10 000 agents
        ↓
plusieurs milliers de ticks
        ↓
export des résultats
```

Si cela fonctionne, Unity devient simplement une représentation possible
du monde simulé.

Cela facilite :

-   tests ;
-   benchmarks ;
-   expériences ;
-   génération de statistiques ;
-   exécution accélérée ;
-   reproductibilité ;
-   analyse de phénomènes émergents.

------------------------------------------------------------------------

# 40. Synthèse des décisions V2

  -----------------------------------------------------------------------
  Concept                 Décision                Justification
  ----------------------- ----------------------- -----------------------
  Agents autonomes        **Ajouter**             Fondement du SMA

  Perception              **Ajouter**             Base de l'autonomie

  Mémoire                 **Ajouter**             Permet une
                                                  représentation
                                                  évolutive du monde

  Croyances               **Ajouter**             Permet information
                                                  partielle et erreurs

  Besoins                 **Ajouter**             Adaptation au modèle
                                                  systémique

  Objectifs               **Ajouter**             Délibération

  Utilité                 **Ajouter --- priorité  Gestion des compromis
                          haute**                 

  BDI                     **Adopter               Sépare croyances,
                          conceptuellement**      désirs et intentions

  Behaviour Tree          **Limiter**             Utile pour exécuter des
                                                  actions complexes

  FSM                     **Limiter**             Trop rigide comme
                                                  cerveau global

  Stratégies fermées      **Éviter**              Trop scripté

  Communication           **Ajouter**             Coordination et
                                                  dynamique sociale

  Communication parfaite  **Éviter**              Réduit l'incertitude et
                                                  l'émergence

  Monde omniscient        **Éviter**              Tous les agents
                                                  deviennent
                                                  artificiellement
                                                  informés

  Navigation              **Ajouter**             Nécessaire à grande
                                                  échelle

  A\*                     **Possible**            Pertinent mais doit
                                                  être découplé et
                                                  optimisé

  Recalcul permanent      **Éviter**              Problème de passage à
                                                  l'échelle

  Groupes                 **Préparer**            Base des phénomènes
                                                  collectifs

  Organisations complexes **Reporter**            Trop tôt pour V2

  Machine d'inférence     **Reporter**            Coût et complexité
  complexe                                        

  Machine learning        **Reporter**            Rend l'analyse plus
                                                  difficile

  Analyse / métriques     **Ajouter dès V2**      Indispensable pour
                                                  étudier l'émergence
  -----------------------------------------------------------------------

------------------------------------------------------------------------

# 41. Conclusion

Le mémoire d'Asselin constitue une référence particulièrement utile pour
la V2 car il montre concrètement comment passer d'un agent individuel à
un système multi-agents capable de coopération, communication et
coordination.

Son principal enseignement pour notre projet n'est toutefois pas de
reproduire son architecture.

Au contraire, il permet de comprendre **où se situe la limite entre un
SMA comportemental et une simulation émergente**.

Le mémoire repose principalement sur :

``` text
Agents
+
Objectif
+
Stratégies prédéfinies
+
Communication
+
Coordination
```

La V2 doit aller vers :

``` text
Agents
+
Perceptions
+
Mémoire
+
Croyances
+
Besoins
+
Préférences
+
Objectifs
+
Utilité
+
Intentions
+
Actions
+
Relations
+
Ressources
+
Environnement dynamique
+
Information partielle
        ↓
Interactions répétées
        ↓
Rétroactions
        ↓
Comportements collectifs
        ↓
ÉMERGENCE
```

La différence fondamentale est donc la suivante :

> **Le système ne doit pas principalement savoir quoi faire. Il doit
> savoir comment décider.**

Le rôle du moteur n'est pas de programmer une société à l'avance.

Il est de définir suffisamment précisément :

-   les capacités ;
-   les contraintes ;
-   les besoins ;
-   les ressources ;
-   les règles du monde ;
-   les mécanismes d'interaction ;
-   les mécanismes de décision ;

pour que les agents puissent produire eux-mêmes une dynamique
collective.

C'est cette orientation qui doit servir de base à la documentation
technique détaillée de la V2.

------------------------------------------------------------------------

# 42. Référence principale

**Asselin, Guillaume.** *Une approche multi-agents pour le développement
d'un jeu vidéo.* Mémoire de maîtrise, Département d'informatique et de
recherche opérationnelle, Université de Montréal, juin 2013.

Le mémoire définit le SMA comme un ensemble d'agents autonomes
interagissant dans un environnement commun et montre concrètement
l'utilisation de la perception, de la mémoire, des agents délibératifs,
des arbres de décision, de la recherche de chemins, de la communication
et de la coopération.

Les principales observations utilisées dans ce document proviennent
notamment des sections consacrées aux agents, aux agents délibératifs et
BDI, aux SMA, à la conception des agents, à la
communication/coordination et aux résultats expérimentaux.
