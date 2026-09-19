# ADR — Engagements Communicationnels (Promesses)

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Extension du Système de Communication (§3.16), du Système d'Objectifs (§3.13) et du Système de Relations (§3.19)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3)

---

## 1. Contexte

Le protocole de messages (§3.16.3) inclut les types `Request` (« Peux-tu m'aider à chasser ? ») et `Response` (« Oui » / « Non »). Une fois qu'une entité B répond « Oui » à une demande de A, **rien dans le Système d'Objectifs (§3.13) ne relie cette réponse à la génération effective d'un objectif chez B**. Le `commitmentLevel` documenté à §3.8.2 s'applique exclusivement à l'engagement d'une entité envers son propre objectif — il n'existe aucune notion d'engagement pris envers une autre entité.

Conséquence directe sur le Système de Confiance (§3.16.12) : la mise à jour de `TrustLevel` ne réagit qu'à la véracité d'une information communiquée (`TrustLevel -= 0.15` si « information fausse/induite »). Une promesse non tenue (« j'ai dit oui mais je n'ai rien fait ») n'est pas distinguée d'un mensonge factuel et n'a actuellement **aucun effet mesurable** sur la confiance. Une entité peut donc répondre « oui » systématiquement sans jamais en subir de conséquence comportementale ou sociale.

## 2. Décision

Introduire une structure **`Commitment`**, créée à la réception d'un `Response` positif à un `Request`, distincte de l'intention individuelle standard (§3.8.2) :

```
Commitment:
  toEntityId
  requestType         // ex. HuntAssistance
  createdTick
  status: Pending | InProgress | Fulfilled | Broken | Expired
  expiryTick          // engagement non honoré au-delà → Broken
```

Modifications aux systèmes existants :

1. **Génération d'objectif dérivée d'un engagement** — un `Commitment` actif ajoute un objectif candidat au même titre que les besoins (§3.13.1), avec une priorité calculée non pas depuis un besoin physiologique mais depuis le niveau de confiance existant envers le demandeur et l'importance sociale de tenir parole (paramètre à définir, potentiellement lié à un trait de personnalité comme la fiabilité/loyauté).
2. **Mise à jour de la confiance étendue** (§3.16.12) :
   ```
   SI Commitment.status == Fulfilled : TrustLevel += bonus_fiabilité
   SI Commitment.status == Broken     : TrustLevel -= pénalité_promesse_rompue
   ```
   distincte de la pénalité actuelle pour information fausse — une promesse rompue et un mensonge factuel sont deux fautes de nature différente et ne devraient pas nécessairement avoir le même poids.
3. **Refus explicite comme option de premier ordre** — un `Response = Non` reste sans conséquence (comportement actuel, cohérent), mais l'introduction du coût d'un engagement rompu devrait rendre le refus honnête relativement plus « rationnel » qu'un oui non tenu pour une entité qui évalue correctement le risque — un effet secondaire souhaitable plutôt qu'un objectif direct.

## 3. Ce qui ne change pas

- Le protocole de messages (§3.16.3) reste le même dans sa structure ; seule la réception d'un `Response` positif déclenche une action supplémentaire (création du `Commitment`).
- La fonction d'utilité (§3.14) n'est pas modifiée — l'objectif dérivé d'un engagement est un candidat parmi d'autres, scoré normalement.
- Le mécanisme de dégradation de la confiance par absence d'interaction (§3.16.12, décroissance exponentielle) reste inchangé et s'applique indépendamment des engagements.

## 4. Points à trancher

1. **Priorité relative d'un engagement face à un besoin physiologique** — une entité affamée doit-elle pouvoir ignorer un engagement pris envers un allié ? Probablement oui (cohérent avec la hiérarchie de survie déjà implicite dans les besoins, §3.12), mais le coût sur la confiance doit refléter cet arbitrage plutôt que l'empêcher.
2. **Durée de validité par défaut** (`expiryTick`) — un engagement non honoré après combien de ticks devient-il `Broken` plutôt que rester indéfiniment `Pending` ?
3. **Engagement conditionnel** — un « Oui » peut-il être implicitement conditionné à la faisabilité constatée plus tard (la cible de chasse a disparu) sans être considéré comme rompu ? Il faudrait distinguer `Broken` (abandon volontaire) de `Expired`/`Invalidated` (devenu impossible), à l'image de la distinction déjà faite entre `Cancelled` et `Failed` pour les actions (§3.15.6).
4. **Visibilité sociale de la rupture** — la pénalité de confiance s'applique-t-elle seulement entre A et B (privé), ou peut-elle être communiquée/propagée comme une information à part entière (« B a trahi A »), avec la dégradation habituelle par saut (§3.16.6) ? Ce choix a un impact fort sur l'émergence de réputation à l'échelle du groupe.
5. **Interaction avec les groupes** — un rôle assigné dans une intention partagée (cf. ADR liée) constitue de fait un engagement implicite envers le groupe entier plutôt qu'envers une seule entité ; envisager une structure `Commitment` unifiée pouvant cibler soit une entité soit un groupe.

## 5. Dépendances et synergies

- Synergie directe avec l'**ADR — Intentions Partagées** : un rôle dans un plan collectif est un cas particulier d'engagement ; les deux structures (`Commitment` et `GroupIntention`) gagneraient à partager un mécanisme de suivi commun plutôt qu'être développées indépendamment.
- Alimente naturellement le Système de Relations (§3.19) : la fiabilité d'une entité (ratio engagements tenus/rompus) devient une dimension mesurable de la relation, au-delà de la seule confiance factuelle actuelle.
- Donne à ECHOS une nouvelle métrique directement exploitable : taux de promesses tenues par entité/groupe, corrélable aux traits de personnalité — une source d'émergence sociale mesurable (réputation, ostracisme des menteurs) qui s'inscrit dans les moteurs de métriques existants (§4.3).

## 6. Conséquences

**Positives :**
- Donne un poids réel à la communication sociale, aujourd'hui purement informationnelle une fois la réponse envoyée.
- Ouvre une voie d'émergence directement liée à la question centrale du projet (§9.1) : une société de confiance/réputation peut-elle apparaître sans règle explicite de moralité, seulement par la mémoire des engagements tenus ou rompus ?
- Réutilise entièrement l'infrastructure de confiance et de communication existante.

**Risques / coûts :**
- Ajoute un objet de suivi supplémentaire par paire d'entités en interaction — impact mémoire à borner (même logique que la mémoire, §3.10.5, et la table Q de l'ADR Apprentissage Expérientiel).
- Risque de complexité prématurée si introduit avant que le Système de Relations et de Communication soit stabilisé et testé isolément (doctrine §9.6.3, points 6 et 13).

## 7. Statut de la décision

[OUVERTE] — prévue pour V3, à introduire après stabilisation des Systèmes de Communication et de Relations, en version minimale (un seul type de `Commitment`, pas de propagation sociale de la rupture) avant extension.
