# ADR-019 : Planificateur intégré — chaînage arrière borné plutôt que GOAP

**Composant** : SYNE
**Statut** : [Acceptée]
**Dernière mise à jour** : 6 novembre 2026
**Dépend de** : ADR-018 (réalignement de la cognition active), ADR-005 (temporalité), ADR-007 (mémoire oubliante) ; côté PRISM, `docs/prism/adr/ADR-009-planification-dans-syne-savoir-des-recettes.md` (la planification vit dans SYNE, choix « par spike en phase P9 »)
**Source** : `docs/prism/AGENT_INTERFACE.md` §3-4, `docs/prism/ROADMAP.md` §5 (P9, risque R3), `docs/prism/ISSUES.md` SYNE-223

---

## Contexte

La feuille de route P9 demande un **planificateur intégré à la délibération**, et
les deux documents de référence laissent explicitement le choix algorithmique
ouvert : « GOAP ou HTN ; choix par spike en phase P9 »
(`AGENT_INTERFACE.md` §4, ADR-009 côté PRISM). Ce choix n'était consigné nulle
part, alors que le socle de SYNE-223 est déjà livré (`Cognition/Planner.cs`,
`Cognition/CognitionPipeline.Planning.cs`) : laisser l'implémentation tenir lieu
de décision est exactement ce qu'ADR-018 interdit.

Le contexte contraint fortement la réponse :

- **Budget** — risque R3 de la feuille de route : « le planificateur dépasse le
  budget de SYNE » (probabilité moyenne, impact élevé). La cible de charge est
  1 000 agents ; toute recherche par délibération est payée 1 000 fois.
- **Déterminisme** — ADR-006 : aucun tirage implicite, ordres stables, empreintes
  reproductibles. Une recherche guidée par un coût flottant rend les décisions
  dépendantes de détails numériques difficiles à épingler.
- **Honnêteté épistémique** — l'agent ne connaît ni le monde entier ni son
  inventaire à venir : il planifie sur des **croyances** et des **recettes
  connues**, et PRISM exécute en n'acceptant que des sources **perçues**. Un
  planificateur qui suppose un état du monde complet décrirait un monde que
  l'agent n'a pas.
- **Traçabilité** — `AGENT_INTERFACE.md` §4 impose la chaîne
  `Action ← Plan ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`.
- **Replanification sur échec** — le contrat PRISM motive toujours ses échecs
  (`source_not_found`, `not_drinkable`, `not_perceived`…) : la boucle de
  réparation est **événementielle**, pas permanente.

## Décision

**Nous retenons un chaînage arrière borné sur règles de décomposition (HTN
léger), et non GOAP, pour la V2 de PRISM.**

Propriétés retenues (toutes déjà vraies du socle livré, et verrouillées par
test) :

1. **Décomposition par règles** : une intention est décomposée par une règle
   `intention → étapes ordonnées` (aujourd'hui : poursuite vers la cible connue,
   puis action terminale). Aucune recherche dans un espace d'états.
2. **Bornage** : `agents.planner.maxPlanSteps` tronque le plan ; le plan est
   construit **une fois** par clé et mis en cache (capacité
   `agents.planner.cacheCapacity`, éviction déterministe).
3. **Clé de cache stable** : `« <intention>@<cible|-> »` — le plan est une
   fonction de l'intention et de la cible crue, jamais d'une horloge implicite.
4. **Replanification sur échec seulement** : une étape `Blocked` invalide le
   plan et le retire (`replanOnFailure`) ; la délibération suivante reconstruit.
   Une interruption (besoin critique) ne touche pas au plan.
5. **Plans transitoires** : non persistés, redérivés à l'identique après une
   reprise (aucun état caché ne manque).
6. **Aucun tirage PRNG** : le plan et son avancement sont des fonctions pures de
   l'état (positions, cible, issue d'action).

Le planificateur reste **désactivé par défaut** (`agents.planner.enabled =
false`) : la trajectoire du scénario de référence est bit-à-bit identique, et la
neutralité a été mesurée (25 ticks avec et sans planificateur, positions et
besoins identiques à chaque tick).

## Conséquences

### Positives

- **Coût prévisible** : construire un plan est une décomposition en O(1) règles,
  mise en cache ; aucune recherche par délibération, donc R3 maîtrisé.
- **Déterminisme trivial** : aucune heuristique flottante, aucun ordre de
  parcours dépendant d'un dictionnaire — les empreintes restent épinglables.
- **Traçabilité directe** : chaque étape porte l'étiquette de sa cible
  (`node:resource-food-3`), donc la chaîne causale demandée est lisible sans
  reconstruction.
- **Cohérence épistémique** : les règles ne consomment que l'intention et la
  cible **crue** ; rien n'est supposé du monde.
- **Réparation alignée sur le contrat** : la replanification est déclenchée par
  les échecs motivés de PRISM, ce qui est précisément le signal disponible.

### Négatives

- **Pas de composition arbitraire** : atteindre un but qui exige une chaîne
  (« avoir une pioche » → miner → fabriquer) demande d'écrire les règles
  correspondantes ; GOAP les ordonnerait automatiquement.
- **Duplication possible des règles** : à mesure que les recettes entrent dans
  les plans, plusieurs intentions peuvent partager des préfixes identiques.
- **Couverture partielle aujourd'hui** : seules les chaînes de survie
  localisées sont modélisées ; la couverture de bout en bout du chemin `Blocked`
  et les plans de recettes restent à faire (SYNE-223, « enrichissement »).

### Risques

- **Rigidité** : un jeu de règles trop pauvre donnerait des plans naïfs et un
  comportement qui semble scripté. Mitigation : les règles sont **des données**
  dérivées des croyances (recettes connues), pas des scripts par scénario.
- **Croissance non bornée des règles** : à surveiller ; si le nombre de règles
  explose pour exprimer les chaînes de production, la décision est à réouvrir
  (voir ci-dessous), pas à contourner par des cas particuliers.

## Alternatives considérées

- **GOAP (recherche dans un espace d'états, A* sur les préconditions)** :
  apporterait la composition automatique multi-étapes et l'ordonnancement des
  sous-buts. Écarté pour la V2 : coût par délibération à 1 000 agents (R3),
  déterminisme à épingler sur une heuristique flottante, et surtout **état du
  monde complet requis** alors que le modèle est explicitement local et
  croyant. Il ne devient intéressant que lorsque les chaînes de production
  seront nombreuses — c'est la condition de réouverture ci-dessous.
- **HTN hiérarchique complet** (décomposition récursive de tâches composées) :
  même famille que la décision retenue, mais la récursion et la gestion des
  tâches composées n'ont pas de contenu tant que les recettes ne sont pas
  planifiées. Retenu sous forme **aplatie** (règles → étapes) pour ne pas
  payer une machinerie sans usage.
- **Planification hors ligne partagée entre esprits** (cache inter-agents) :
  séduisante pour le budget, mais elle mutualiserait des plans dont la cible est
  **datée et crue par un agent** ; la clé de cache actuelle est déjà
  déterministe et peu coûteuse. Notée comme enrichissement de SYNE-223, pas
  comme substitut.

## Validation / rejet

La décision est vérifiée par :

- `Simulation.Core.Tests/Cognition/PlannerTests.cs` — construction, troncature,
  clé, cache borné/éviction, invalidation, franchissement d'étape ;
- la **preuve de neutralité** (planificateur actif ou non, même trajectoire sur
  25 ticks) ;
- le **budget** : le planificateur ne doit pas faire sortir la cible de charge
  des budgets de `PERFORMANCE.md` (mesure B7) ;
- le chemin `Blocked` de bout en bout (replanification observée, compteur
  `PlanReplans`).

**Conditions de réouverture** : si exprimer les chaînes de production
(outils → minage → fabrication → pose) exige des règles qui dupliquent des
préfixes au point de rendre le jeu illisible, ou si le budget mesuré laisse une
marge telle qu'une recherche par délibération redevient abordable, la décision
est réexaminée et cet ADR passe `Superseded`.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 6 novembre 2026 | Création (choix consigné après livraison du socle SYNE-223) | Le choix GOAP/HTN était renvoyé à un « spike P9 » jamais consigné ; P9 exige la décision écrite |
