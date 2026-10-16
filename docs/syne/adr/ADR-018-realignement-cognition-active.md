# ADR-018 : Réalignement de la cognition active (arbitrages de modèle)

**Composant** : SYNE
**Statut** : [Acceptée]
**Dernière mise à jour** : 16 octobre 2026
**Dépend de** : ADR-005 (temporalité), ADR-007 (mémoire oubliante), ADR-013 (obstacles et ligne de vue), ADR-015/016 (calibrations de survie)
**Source** : `ANALYSE_DIVERGENCES_FONDAMENTALES_ET_PLAN_REALIGNEMENT.md` (audit du 16/10/2026), phases 0 à 6 ; constats S-01…S-31, divergences D-01…D-20, points bloquants B-01…B-12

---

## Contexte

L'audit confronte la vision normative (`docs/monographie/`) et l'implémentation
de SYNE (engineVersion 0.15.0). Il conclut à un **écart de modèle**, pas à une
absence de code : la perception locale, la mémoire, les croyances, l'utilité
décomposée, les groupes et la persistance existent et sont testés, mais
**mémoire et croyances ne causaient pas les décisions** (`Recall` jamais appelé
dans le chemin actif), l'objectif ne portait **ni cible ni condition
d'accomplissement** (`Goal = (DesireKind, BornTick)`), le déplacement était un
offset pseudo-aléatoire recalculé à chaque tick (B-01), et l'ordre effectif du
tick contredisait la fenêtre causale annoncée au chapitre III (S-15 / S-31 /
D-11 / B-06).

L'audit demande explicitement (§9 phase 0) de **trancher quatre arbitrages avant
d'écrire du code**, et interdit de laisser l'implémentation masquer ces
arbitrages. Cette ADR les consigne.

## Décisions

### D1 — Sémantique du tick : fenêtre causale fermée (chapitre III)

**Décision** : les conséquences d'action sont **différées au commit de fin de
tick**. Pendant l'itération des entités, aucun déplacement n'est appliqué au
monde et aucun effet porté à une autre entité n'est appliqué : ils sont mis en
file puis appliqués après la boucle, dans l'ordre des identifiants
(`Actions/TickCommitBuffer.cs`). Ce qu'un agent perçoit à un tick est l'état
figé en début de tick ; l'ordre des identifiants cesse d'être une loi causale.

**Exception déclarée et bornée** : le prélèvement d'un **gisement** reste
séquentiel dans l'ordre des identifiants. Une action terminale (Eat/Drink) doit
savoir si elle a réussi *avant* d'appliquer son effet sur le besoin ; différer
le prélèvement imposerait une révision a posteriori du résultat ou une double
dépense silencieuse. La conséquence visible est bornée à la quantité d'un stock
partagé, jamais à la perception.

**Conséquence** : la monographie (chapitre III) reste normative, sans
modification ; le test de la fenêtre est écrit côté pipeline
(`CausalWindowTests`).

### D2 — Économie des ressources : sources localisées, réserve globale conservée

**Décision** : le modèle du run de référence devient **localisé**
(`resources.localized = true` par défaut). Un gisement
(`World/ResourceNode.cs`) est une source **perceptible, mémorisable, croyable et
consommable** : Eat/Drink exigent le gisement à portée d'interaction
(`resources.interactionRadius`), un objectif de poursuite porte la **cible crue**
(position observée, tick d'observation, confiance), l'objectif est **accompli à
l'arrivée**, et les réserves globales publiées deviennent l'**agrégat** des
gisements (l'observabilité continue de publier la même forme de séries).

**Motif** : la question directrice porte sur des structures non programmées ; la
convergence et la compétition autour de gisements sont exactement l'objet
d'étude que l'audit identifie comme limite de portée du modèle global (S-17 /
D-10 / B-03 / B-04). Les emplacements sont **la règle unique partagée avec la
description du monde** publiée à PRISM (aucune ressource affichée que le moteur
ne simule pas), générés par un LCG local dérivé de la graine : **aucun tirage
du PRNG de simulation** (déterminisme et non-intrusion de l'observabilité).

**Alternative conservée** : `resources.localized = false` reste un mode
**déclaré** (réserve mondiale renouvelable). Dans ce mode, la localité de la
décision n'est **pas revendiquée** : `SeekFood`/`SeekWater` y sont l'état
historique, et les tests qui portent sur l'atomicité de la réserve mondiale le
déclarent explicitement.

### D3 — Loi des groupes : observateur agrégateur, cohésion spatiale exigée

**Décision** : la revue des groupes reste un **observateur agrégateur global**
(le chapitre VII la décrit ainsi) : calcul des composantes sur les esprits, puis
propagation possible d'un objectif collectif à l'utilité individuelle, pondérée
par le consensus et la confiance envers le leader. Elle est un **organe de
mesure du collectif, pas une loi d'action locale** : la cause individuelle reste
l'utilité, et l'objectif collectif n'est qu'un modificateur déclaré de son
bénéfice. `groups.proximityRequired` exige une **proximité spatiale actuelle**
pour qu'un lien subsiste (arbitrage de l'audit sur `AreBonded`).

**Conséquence** : la tension relevée entre §2b.3 (« lois locales ») et le
chapitre VII est tranchée en faveur d'une **distinction explicite entre
interaction locale et calcul d'observation**, consignée ici plutôt que corrigée
à l'aveugle dans le code.

### D4 — Preuve d'émergence : critères observables, pas d'illustration

**Décision** : ce qui est revendiqué, c'est la **capacité d'étude**, pas
l'émergence. Les critères publiables sont ceux des gates de l'audit :
un agent se rend à une cible **connue** en un nombre de ticks borné ; deux agents
à état physique identique mais à **connaissances différentes** font des choix
différents, de façon **traçable** ; la chaîne
`action ← but ← croyance ← souvenir ← perception` est remontable depuis la trace
décisionnelle ; même configuration ⇒ même empreinte canonique, et la reprise
reprend la trajectoire **et** l'explication.

**Conséquence méthodologique** : distinguer systématiquement **zéro observé /
absence de mesure / non mesuré** : l'événement `decision_made` publie
`utility = null` pour une absence, nomme l'état (`fresh`, `holdover`,
`interrupted`, `none`) et publie un score reporté sous `heldUtility` — jamais un
score ancien sous le nom du tick courant, jamais `0.0` pour une absence.

### D5 — Exutoires et dérives des besoins sociaux

**Décision** : `Socialize` et `Explore` **satisfont** désormais leur besoin à
l'arrivée (`socialRecovery` / `curiosityRecovery` du catalogue). Les dérives
social/curiosité, que la calibration B1 avait ralenties (0,0002 / 0,0005) parce
qu'une dérive vive ne produisait qu'un déplacement muet, sont **rétablies**
(0,003 / 0,002) pour produire un **cycle** : franchissement du seuil
(~230 / ~150 ticks), poursuite ciblée, arrivée, satisfaction, redescente.

**Symétrie** : `SafetyDriftRate` dérive bien vers l'**insécurité**
(`Safety` diminue), `Flee` rétablit la sûreté à l'arrivée. L'effet des taux
retenus est **mesuré** sur le run de référence (voir §Mesures), pas supposé.

## Mesures et preuves

| Élément | Valeur mesurée |
| :-- | :-- |
| Trajectoire de référence (25 entités, 200 ticks, seed 12345) | `0xc8116c934c424b28` (perception), `0x6f1a7ec30e563337` (état complet) — ré-épinglés avec l'engineVersion 0.16.0 |
| Débit à 1 000 entités (Debug, machine de développement) | meilleur de 5 essais × 40 ticks : **25,3 t/s** (39,5 ms/tick) ; premier essai à froid ≈ 14,5 t/s (JIT du chemin cognitif complet). Plancher anti-régression du CI (20 t/s) **conservé** et vérifié sous charge de suite. Le chemin par observation a été allégé pour le tenir : le récit canonique n'est plus formaté que pour une écriture effective (la déduplication écarte la plupart des réobservations) et la source de provenance est calculée une fois par entité et par tick — mesures ci-dessous |
| Choix différent à connaissances différentes | `CognitionKnowledgeTests` (gate de la phase 2) |
| Cible hors portée interdite | `LocalizedResourceActionTests` (gisement hors de portée d'interaction), `AdrCognitivePrimitivesTests.InterEntityPrimitives_AreBlocked_WithoutBelievedTarget` (gate de la phase 3) |
| Fenêtre causale fermée | `CausalWindowTests` (conséquences différées au commit de fin de tick) |
| Chaîne de transmission | `TransmissionChainTests` (perception → mémoire → message → croyance dégradée) |
| Identité de run | `RunIdentityTests` (hash canonique de configuration et d'état, empreinte publiée) |
| Reprise bit-à-bit | `PersistenceTests.ReloadFromSnapshot_ContinuesBitForBitIdenticalToUninterruptedRun` |

## Alternatives écartées

- **Conserver le séquentiel et réviser la monographie** : écarté — le chapitre
  III est explicite et le tampon de commit coûte un tri de file par tick.
- **Réserve mondiale uniquement** : écarté comme *défaut* — la convergence vers
  une cible connue devient alors inobservable ; conservé comme mode déclaré.
- **Remplacer l'agrégateur de groupes par un mécanisme purement local** :
  écarté — le chapitre VII décrit l'observateur central ; la décision retenue
  est de le déclarer comme tel et d'ajouter la condition spatiale.
- **Attendre un protocole statistique avant tout ré-épinglage** : impossible —
  la trajectoire change par construction ; les pins sont ré-épinglés dans le
  même commit, avec leurs causes, et les tests de déterminisme voisins
  (même graine ⇒ même journal, graines différentes ⇒ journaux différents)
  restent la propriété vérifiée.

## Conséquences

- Les checksums dorés changent (**altération volontaire de trajectoire**) :
  engineVersion **0.15.0 → 0.16.0**, pins ré-épinglés dans le même commit.
- Les tests portant sur le **modèle global** (`ActionExecutorTests`,
  `ActionExecutorReserveAtomicityTests`, `AdrCognitivePrimitivesTests`,
  `CognitionPipelineTests` pour le holdover et l'interruption) déclarent
  désormais explicitement `resources.localized = false` : ils vérifient une
  mécanique, pas le modèle de ressources par défaut.
- `GoalSnapshotDto` porte la cible : sans elle, une reprise repartait avec une
  intention **sans destination** et divergeait du run continu au tick suivant.
- `DecisionRecord.Utility` devient **nullable** : une absence de décision n'est
  ni `0.0` ni `NaN` (le `NaN` rendait de surcroît le snapshot non
  sérialisable en JSON).
- `BeliefSet` garde **une croyance courante par (sujet, prédicat)** : un sujet
  mobile ne crée plus une croyance par position (croissance non bornée et
  balayage de toutes les croyances par observation à densité). Le chemin
  historique `ApplyEvidence` suit la même règle : une valeur contradictoire
  **remplace** la croyance courante, et la pénalité de conflit reste effective
  (le prior pénalisé est révisé vers le signal au lieu de pénaliser un objet
  aussitôt écarté).
- **Coût par observation réduit** (le débit à 1 000 entités est dominé par la
  perception — des dizaines de voisins par agent et par tick) : le récit canonique
  d'une observation n'est formaté qu'à l'écriture effective en mémoire, et la
  source `perception-<id>` est calculée une fois par entité et par tick. Phase de
  perception : ≈ 24 ms → ≈ 14–18 ms par tick à 1 000 entités (mêmes empreintes,
  mêmes croyances — la source est comparée par valeur).
