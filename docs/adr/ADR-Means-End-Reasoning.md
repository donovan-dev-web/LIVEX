# ADR — Means-End Reasoning (Plans Alternatifs par Objectif)

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Extension du Système d'Objectifs (§3.13) et du Système d'Actions (§3.15), sans modification du Système de Décision (§3.14)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3)

---

## 1. Contexte

La génération d'objectifs (§3.13.1) mappe un besoin non satisfait directement à un objectif unique et une cible unique :

```
Faim > 60 → SeekerFood(pos from beliefs)
```

Les exemples chiffrés de la boucle cognitive (§3.8.3, §3.13.4) montrent la fonction d'utilité (§3.14) comparer des actions correspondant chacune à **un objectif différent** (`Eat` vs `Rest` vs `Interact`), jamais plusieurs actions candidates pour **un même objectif**. Le chemin `SeekerFood → MoveTo → Gather/Eat` est quasiment déterministe une fois la cible connue.

Or un même besoin peut légitimement être satisfait par plusieurs stratégies aux profils de coût/risque très différents — récolter, échanger, voler, attaquer, demander. C'est la pièce classiquement appelée **means-end reasoning** dans les architectures BDI complètes (PRS, JACK, Jadex) : une bibliothèque de plans candidats entre le Désir et l'Intention, absente de la boucle actuelle qui va directement d'un objectif à une action quasi unique.

Fait notable : `Attack` possède un effet chiffré en V1 (§3.15.4 : `Santé cible -5 × Agressivité/tick`) mais **a disparu de la liste d'actions V2** (§3.15.2). Aucune action `Steal` ou `Buy` n'existe à ce jour dans aucune version.

## 2. Décision

Transformer l'étape 5-6 de la boucle (§3.8.1, OBJECTIFS/FILTRAGE) d'un mapping *besoin → objectif → cible* vers un mapping *besoin → objectif → N plans candidats*, chaque plan étant filtré par faisabilité (§3.13.2) puis scoré par la fonction d'utilité existante (§3.14) **sans modification de cette dernière** — elle reçoit simplement plus de candidats en entrée.

```
GénérerPlansCandidats(objectif, entité, croyances):
  plans ← []
  SI objectif == SeekerFood:
    SI ressource connue et accessible: plans.Add(Plan(Gather, ...))
    SI entité avec surplus connue et relation de confiance suffisante (§3.19): 
       plans.Add(Plan(Trade, ...))
    SI ressource visible avec propriétaire identifié: 
       plans.Add(Plan(Steal, ...))
    SI entité-proie ou cible identifiée: 
       plans.Add(Plan(Attack, ...))
  RETOURNER FiltrerFaisabilité(plans)  // §3.13.2, inchangé
```

Chaque plan candidat alimente Benefit/Cost/Risk/Confidence/Urgency (§3.14) avec des valeurs propres à sa nature — c'est ici que les sous-systèmes déjà existants sont réutilisés plutôt que dupliqués :

| Plan | Benefit | Cost | Risk (source) |
|---|---|---|---|
| `Gather` | Fixe, connu | Faible (déplacement) | Quasi nul |
| `Trade` | Fixe, négocié | Coût d'une ressource propre | Dépend de la confiance (§3.19.3) |
| `Steal` | Fixe | Faible | Probabilité de détection (perception, §3.9) × dégât sur relation/réputation si découvert (§3.16.12, §3.19.3) |
| `Attack` | Variable | Risque santé propre | Système de conflit (§6.10), dégât relationnel durable |

## 3. Ce qui ne change pas

- Le calcul d'utilité (§3.14) reste inchangé dans sa formule — seul le volume de candidats évalués augmente.
- Le filtrage de faisabilité (§3.13.2) s'applique identiquement à chaque plan.
- Les objectifs générés à partir des besoins (table de §3.13.1) restent les mêmes ; seule leur traduction en actions concrètes se diversifie.

## 4. Actions à ajouter/rétablir dans la liste V2 (§3.15.2)

1. **`Attack`** — rétablir dans la liste V2, avec l'effet déjà documenté en V1 (§3.15.4) comme base.
2. **`Steal`** — nouvelle action : préconditions (ressource ou possession identifiée avec propriétaire), effet (transfert de ressource), risque de détection lié à la perception de l'entité lésée ou de témoins.
3. **`Buy`** *(distinct de `Trade` existant, §3.15.2)* — à clarifier : `Trade` semble déjà couvrir l'échange volontaire ; vérifier s'il faut une action séparée ou si `Buy` est un cas particulier de `Trade` avec une ressource comme contrepartie systématique.

## 5. Points à trancher

1. **Coût de génération des plans** — énumérer tous les plans candidats à chaque cycle de délibération a un coût ; envisager de ne les générer que lorsqu'un objectif est effectivement retenu comme prioritaire (après §3.13.3), pas pour tous les objectifs non satisfaits.
2. **Risque social durable du vol/de l'attaque** — le Cost/Risk ne doit pas être seulement immédiat (probabilité d'échec de l'action elle-même) mais intégrer une pénalité de réputation/relation persistante (§3.19), sans quoi rien n'empêche une entité de répéter le comportement sans conséquence — un point de cohérence avec le Système de Relations (§3.19.4, impact sur les décisions).
3. **Interaction avec le Système de Groupes** (§3.17) — voler/attaquer un membre du même groupe devrait avoir un profil de risque différent (conditions de dissolution, §3.17.6) que cibler un inconnu.
4. **Interaction avec le Système de Conflit** (§6.10) — `Attack` comme plan candidat pour satisfaire un besoin primaire (faim) est conceptuellement différent d'un conflit territorial ou relationnel ; faut-il un seul système de conflit unifié ou deux origines distinctes menant au même mécanisme d'exécution ?
5. **Explicabilité** — le Decision Record (§3.14.12) doit logguer non seulement l'action choisie mais l'ensemble des plans candidats évalués et leurs scores, pour que le choix entre `Gather` et `Steal` reste analysable par ECHOS.
6. **Cohérence de doctrine** — vérifier que l'ajout de `Steal`/`Attack` comme plans réguliers ne contredit pas le point 13 de la doctrine (§9.6.3 : « Ne pas introduire trop tôt des systèmes sociaux complexes ») — recommandation : introduire d'abord `Gather`/`Trade` uniquement, ajouter `Steal`/`Attack` comme extension explicitement postérieure.

## 6. Dépendances et synergies

- Bénéficie indirectement de l'**ADR — Perception des Événements** : un événement perçu (pénurie annoncée, conflit visible) pourrait directement influencer la génération de plans candidats (ex. une pénurie perçue augmente la probabilité de générer un plan `Steal`).
- Aucune dépendance stricte sur l'**ADR — Politique de Reconsidération** : le means-end reasoning s'exécute au moment où la boucle complète tourne, indépendamment du filtre de saillance qui décide *si* elle tourne.
- Synergie avec l'**ADR — Système d'Apprentissage Expérientiel** (ExperienceMod, §3.14) déjà documentée séparément : la table Q pourrait apprendre, par contexte, quel *type de plan* a historiquement le mieux fonctionné pour une entité donnée — un raffinement naturel une fois plusieurs plans candidats disponibles.

## 7. Conséquences

**Positives :**
- Corrige une incohérence déjà présente (`Attack` documenté avec effet mais absent de la liste V2 active).
- Ouvre une source d'émergence sociale directement mesurable par ECHOS (fréquence de vol/coopération, corrélée aux traits de personnalité et à la rareté des ressources) — pertinent pour §4.3 (moteurs de métriques) et le score d'émergence composite (§4.4.1).
- Réutilise intégralement les sous-systèmes existants (relations, confiance, conflit) sans réinvention.

**Risques / coûts :**
- Charge de calcul supplémentaire à la génération de plans (cf. point 1 ci-dessus).
- Risque de complexité sociale prématurée si `Steal`/`Attack` sont introduits avant que `Gather`/`Trade` soient stabilisés et testés isolément (doctrine points 3 et 13).

## 8. Statut de la décision

[OUVERTE] — documentée pour V3, introduction recommandée en deux temps (`Gather`/`Trade` d'abord, `Steal`/`Attack` ensuite) pour respecter la doctrine de progressivité (§9.6.2).
