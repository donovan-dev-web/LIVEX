# SOCIAL_NETWORK.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `DATA_MODEL.md`, `COGNITIVE_ARCHITECTURE.md`, `COMMUNICATION_PROTOCOL.md`, `API_CONTRACTS.md`
**Source Monographie** : §3.17 (groupes), §6.7 (réseau social), ADR-008 ; décisions n°23 (relations 2 dimensions), n°24 (groupes par cohésion émergente)

---

## 1. Le principe : cohésion émergente (pas de script de coalition)

Les groupes ne sont **jamais scriptés** (décision n°24 **[TRANCHÉE]**) : ils naissent de la
**cohésion constatée** entre les entités — la combinaison de la **proximité sociale**
(confiance réciproque) et des **représentations partagées** (croyances communes et buts
communs). Aucune entité ne « décide » de créer un groupe : le système de groupes observe
les relations existantes et en déduit les structures émergentes.

Formellement, à chaque révision, on construit le **graphe de cohésion** :

```text
cohésion(P, Q)   = min(confiance(P→Q), confiance(Q→P)) × affinité(P, Q)
affinité(P, Q)   = 1 + (but partagé ?  goalAlignmentBonus : 0)
                      + nbCroyancesPartagées × sharedBeliefBonus
lien(P, Q)       ⇔  min trust ≥ trustThreshold  ET  affinité > 1
```

- un **lien social** exige une confiance réciproque ≥ `trustThreshold` **et** au moins une
  part commune (croyance ou but) — la confiance seule ne suffit pas (décision n°24) ;
- une **croyance partagée** = même fait `(subject, predicate, value)` présent dans les deux
  ensembles avec une confiance ≥ 0.5 de part et d'autre (ex. après une pulsation
  « perceived-… », `entity-3 position 12.00,14.00`) ;
- un **but partagé** = même intention courante non-idle des deux entités.

## 2. Révision périodique (LOD déterministe)

Le sous-système `GroupSystem` (propriété `Cognition.Groups` du pipeline) s'exécute après la
boucle des entités, la communication de masse et les décréments croyances/confiance, à la
fréquence `groups.reviewIntervalTicks` (défaut **10** ticks) :

1. **liens sociaux** : toutes les paires (identifiant croissant) vérifiant le critère de cohésion ;
2. **composantes connexes** du graphe (union-find déterministe, racine = plus petit id) ;
3. composantes de taille **≥ `groups.minGroupSize`** (défaut 3) deviennent des candidats **groupes** ;
4. **cycle de vie** : membres identiques ⇒ même groupe (id, duré cumulée) ; sinon dissolution ± formation.

## 3. Cycle de vie

- **Formation** : un noyau de membres liés qui n'avait pas de groupe actif se voit attribuer
  un identifiant séquentiel (`group_formed`).
- **Continuité** : tant que l'ensemble des membres est identique, le groupe persiste (sa
  cohésion moyenne, son leader et sa décision sont **recalculés à chaque révision**).
- **Dissolution** : tout changement de composition dissout le groupe (`group_dissolved`) et
  les membres restants dépassant `minGroupSize` reforment un nouveau groupe ([PP] churn
  émergent). Le bilan de vie émet `lifetime` (tick courant − tick de formation),
  `success` (le groupe a-t-il produit au moins une décision collective ?),
  `membersOut`/`membersIn` (turnover brut).

## 4. Leader émergent (SYNE-061)

Le leader n'est pas élu ni nommé : c'est le membre dont la **somme des confiances internes
entrantes** est maximale (les autres lui font le plus confiance), départage déterministe par
identifiant minimal. Le leader porte les événements de formation et de décision (`agentId`).

## 5. Décisions collectives (SYNE-061)

À chaque révision, les **intentions courantes** des membres votent : le vote d'un membre est
**pondéré par sa confiance envers le leader émergent** (la confiance « compte », décision
n°24). L'intention dominante est adoptée comme **décision collective** du groupe si sa part
pondérée ≥ `groups.consensusThreshold` (défaut 0.5). La décision est exposée dans le
snapshot (`decision`, `consensus`) et émise en événement `group_decision`. Un groupe ayant
déjà produit une décision est marqué `success` à sa dissolution.

## 6. Interactions avec les autres systèmes

- **Naissance (SYNE-062)** : la reproduction repose sur la **confiance réciproque du couple**
  (`reproduction.consentTrustThreshold`) — même brique relationnelle que la cohésion. Un
  groupe est donc un vivier possible de fusions consenties, mais la fusion n'**exige pas**
  d'appartenance à un groupe (émergence, pas de contrainte scriptée).
- **Croyances/buts** : les pulsations (`perceived-{id}#{position}`, COMMUNICATION_PROTOCOL.md)
  et les co-perceptions d'un même jalon alimentent les croyances partagées qui conditionnent
  les liens.
- **Confiance** : `Relationships` (Relationships.cs) fournit `TrustWith` ; la confiance
  décroît ×`decayFactorPerTick` sans interaction et se renforce à chaque pulsation reçue —
  la **portée de pulsation** (défaut 55 u. depuis le jalon ph6) conditionne donc la densité
  du graphe.

## 7. Déterminisme

Aucun tirage du PRNG global : toutes les itérations suivent des ordres triés par identifiant,
les composantes dérivent d'un union-find à racine minimale (DETERMINISM.md §3). Deux runs au
même seed + config produisent exactement la même séquence de groupes (`GroupBirthDeterminismTests`).

## 8. Observabilité (API_CONTRACTS.md)

- **Snapshot** : champ `groups[]` — `{groupId, members[], size, leaderId, bornTick, cohesion, decision, consensus}`.
- **Événements typés** : `group_formed` `{groupId, size, cohesion, members[]}` ;
  `group_dissolved` `{groupId, lifetime, success, membersOut, membersIn, members[]}` ;
  `group_decision` `{groupId, decision, consensus}` (agentId = leader).

## 9. Paramètres (annexe — `groups.*`)

| Paramètre | Défaut | Décision | Note |
| :-- | :-- | :-- | :-- |
| `groups.enabled` | true | n°24 | Active la révision des groupes |
| `groups.reviewIntervalTicks` | 10 | — | Fréquence LOD de la révision |
| `groups.trustThreshold` | 0.3 | n°24 | Confiance réciproque minimale d'un lien |
| `groups.minGroupSize` | 3 | n°24 | Taille minimale d'un groupe |
| `groups.sharedBeliefBonus` | 0.10 | n°24 | Bonus d'affinité par croyance partagée |
| `groups.goalAlignmentBonus` | 0.20 | n°24 | Bonus d'affinité par but partagé |
| `groups.consensusThreshold` | 0.5 | n°24 | Quorum pondéré d'une décision collective |

## Points restés ouverts

- Mécanique de rôles (Monographie §6.7) au-delà du leader : attributions d'action, ressources
  de groupe — jalons ultérieurs.
- Interaction groupes ↔ conflits (SYNE-070+).