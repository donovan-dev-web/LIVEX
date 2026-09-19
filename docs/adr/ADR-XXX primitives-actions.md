ADR-XXX — Refonte du système d'actions vers des primitives atomiques
Statut : Proposé

Contexte
--------
Le système d'actions actuel (§3.15.2, liste V2) mélange plusieurs niveaux d'abstraction :
- `MoveTo` et `Explore` exécutent le même mécanisme (déplacement tick par tick vers
  une cible) mais ne diffèrent que par la stratégie de sélection de la cible (connue
  via croyances vs inconnue). Cette différence relève du système d'objectifs (§3.13),
  pas du système d'actions.
- `Eat`, `Drink`, `Gather` et `Trade` sont des actions composites (elles enchaînent
  implicitement acquisition + consommation, ou n'ont aucune mécanique définie) et non
  des primitives, contrairement à la doctrine de la V0.1 (§9.6.3, point 15 :
  « préférer des primitives simples pouvant produire plusieurs phénomènes »).
- Le document présente par ailleurs des comportements absents de la liste canonique :
  `Flee(threat_agent)` est généré comme objectif en §3.13.1, et `Attaquer` est chiffré
  dans l'exemple d'utilité en §6.10.2 — sans qu'aucune des deux ne figure dans la
  table §3.15.2. C'est une incohérence à corriger, pas une extension de confort.
- `Trade` n'a par ailleurs aucune spécification de protocole (contrairement à la
  Communication, §3.16, qui en a une complète), alors qu'un système d'échange
  suppose un inventaire et un protocole de proposition/acceptation (cf. ADR
  « Système d'inventaire »).

Décision
--------
Remplacer la liste d'actions par un ensemble de primitives atomiques, chacune
correspondant à un seul effet indivisible sur le monde ou sur l'entité :

| Primitive        | Rôle                                                                 |
|-------------------|----------------------------------------------------------------------|
| SeDéplacer        | Se déplacer vers une position cible (fusionne MoveTo et Explore ;    |
|                   | la stratégie de choix de cible reste dans le système d'objectifs).   |
| Prendre           | Transférer une ressource du monde vers l'inventaire de l'entité,     |
|                   | sous réserve de capacité (poids/slots) et du flag `stockable`.       |
| Consommer         | Réduire un besoin en utilisant une ressource, depuis l'inventaire OU |
|                   | directement sur une ressource adjacente non stockable.               |
| Donner            | Transférer une ressource à une autre entité sans contrepartie        |
|                   | attendue (don).                                                      |
| Échanger          | Proposer et exécuter un transfert bidirectionnel conditionné à       |
|                   | l'acceptation d'un autre agent (voir protocole de proposition        |
|                   | ci-dessous).                                                         |
| Attaquer          | Infliger un effet négatif à une autre entité (référencé en §6.10.2,  |
|                   | absent de la liste actuelle).                                        |
| SeDéfendre        | Réduire l'effet d'une attaque reçue (référencé implicitement en      |
|                   | §3.17.6 « défense collective », jamais formalisé comme primitive).   |
| Fuir              | Se déplacer en priorité loin d'une menace (référencé en §3.13.1,     |
|                   | absent de la liste actuelle).                                        |
| Observer          | Inchangée (§3.15.2).                                                 |
| CommuniquerÀ      | Inchangée, mais porte désormais explicitement la sémantique          |
|                   | `Trading` héritée du prototype (§3.16.3, « Offre 5 nourriture contre |
|                   | 3 eau ») comme un contenu possible de pulsation, au même titre que   |
|                   | Information/Request/Warning.                                        |
| Attendre / Penser | Inchangées (§3.15.2).                                                |

Actions supprimées de la liste canonique : `Eat`, `Drink`, `Gather`, `Trade`,
`Explore`. Elles deviennent des **objectifs** résolus par une courte séquence fixe
de primitives, définie dans le système d'objectifs (§3.13), par exemple :
`Objectif Eat → [SeDéplacer(source) →] Prendre →] Consommer` (les étapes entre
crochets sont sautées si l'entité a déjà la ressource en inventaire ou est déjà
adjacente à une ressource non stockable).

Portée V0.1 : cette résolution objectif → séquence reste une **recette fixe et
courte par type d'objectif**, câblée dans le système d'objectifs — pas un
planificateur générique (type GOAP). Cela respecte la doctrine « pas d'apprentissage
automatique / pas de mécanisme trop complexe avant d'avoir validé le socle »
(§9.6.3, points 13-14) et l'ordre de construction progressif (§9.6.2). Un
planificateur générique reste une piste ouverte pour une V2+, si le besoin de
séquences plus longues ou combinatoires apparaît à l'usage.

Alternatives envisagées
------------------------
1. **Conserver les actions composites actuelles** (`Eat`, `Trade`, etc.) —
   rejeté : contredit directement la doctrine §9.6.3 point 15, et n'explique pas
   l'incohérence Flee/Attaquer déjà présente ailleurs dans le document.
2. **Planificateur générique (GOAP/HTN) dès la V0.1** — rejeté pour l'instant :
   changement d'architecture trop lourd par rapport à l'étape « Cognition »
   (Étape 3, §9.6.2) du plan de reconstruction ; réévaluable en V2 si les recettes
   fixes se révèlent insuffisantes.

Conséquences
------------
- Le système d'objectifs (§3.13) doit être étendu : chaque type d'objectif définit
  désormais une courte séquence de primitives, et non plus un mapping direct
  objectif → action unique (l'exemple §3.13.4 devra être réécrit sur cette base).
- Le système de décision (§3.14) doit définir de nouvelles formules de
  benefit/cost/risk/confidence pour `Prendre`, `Consommer`, `Donner`, `Échanger`,
  `Attaquer`, `SeDéfendre`, `Fuir` — les formules actuelles pour `Eat`/`Drink`/
  `Gather`/`Trade` (§3.14.3, §3.14.6) sont à retirer ou à réinterpréter en termes
  des nouvelles primitives.
- Le protocole de communication (§3.16.3) doit réintégrer explicitement la
  sémantique `Trading` comme contenu de pulsation publique (quantité + ressource
  offerte + ressource demandée), diffusée à toute entité en ligne de vue — pas à
  un destinataire privilégié — ce qui est cohérent avec le principe de publicité
  déjà posé en §3.16.1.
- Les exemples pédagogiques existants (§6.4.2, ligne « une entité qui mange car sa
  faim est élevée ») restent valables sur le fond mais doivent être reformulés en
  termes de `Consommer` (et `Prendre` si l'entité n'a pas de stock).
- `Donner` sans contrepartie ouvre un espace comportemental que `Trade` seul ne
  permettait pas (don, influence, corruption) — cohérent avec la mécanique de
  confiance déjà existante (§3.19.3, §3.16.12), sous réserve de faire évoluer le
  gain de confiance d'un bonus plat (+0.1) vers une fonction de la valeur donnée
  (question à trancher séparément, voir note ouverte ci-dessous).
- Cette liste devient la référence de la section 3.15.2, qui doit être mise à jour
  en conséquence, ainsi que toute mention croisée (§3.13, §3.14, §6.4.2, §6.10.2).

Point ouvert à trancher séparément
-----------------------------------
[OUVERT] Le gain de confiance issu d'une interaction positive (`trust += 0.1`,
§3.19.3) doit-il devenir proportionnel à la valeur de la ressource donnée/échangée ?
Sans cela, l'émergence d'influence/corruption liée aux dons de valeurs différentes
ne peut pas se distinguer d'un simple « bonjour ». Cette décision est indépendante
de la présente ADR mais en est un prérequis fonctionnel pour que `Donner` produise
l'effet social recherché.
