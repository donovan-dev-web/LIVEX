# ADR — Intentions Partagées (Coordination de Groupe)

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Extension du Système de Groupes (§3.17), sans modification du Système de Décision individuel (§3.14)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3)

---

## 1. Contexte

Le Système de Groupes (§3.17) permet aux entités de former des coalitions et de prendre des décisions collectives par vote (§3.17.5) :

```
bestGoal = goals.MaxBy(g => AverageScore(g))
POUR CHAQUE member: member.AddGoal(bestGoal)
```

Le vote assigne le même objectif à chaque membre, mais chaque membre repart ensuite exécuter sa propre boucle de délibération individuelle (§3.8.1, §3.14), indépendamment des autres. Aucune structure ne représente :
- la **répartition des rôles** au sein d'une action collective (qui chasse, qui protège, qui collecte),
- la **synchronisation** entre membres (savoir si les autres ont commencé, terminé, ou abandonné leur part),
- la **réaction coordonnée** à l'abandon d'un membre (si un chasseur du groupe change d'objectif en cours de route, les autres n'en sont jamais informés par construction).

Un groupe qui vote « chasser ensemble » peut donc produire, dans les faits, deux ou trois plans individuels non coordonnés qui se ressemblent par coïncidence plutôt qu'une action collective réelle.

C'est la couche connue en BDI multi-agent sous le nom d'**intentions partagées** (Joint Intentions — Cohen & Levesque ; SharedPlans — Grosz & Kraus) : un objet de coordination distinct de l'intention individuelle (§3.8.2), qui encode non seulement « je veux X » mais « je sais que nous voulons collectivement X, je sais quelle est ma part, et je dois signaler aux autres si j'abandonne ».

## 2. Décision

Introduire une structure **`GroupIntention`**, distincte de l'intention individuelle, générée à l'issue du vote (§3.17.5) au lieu du simple `AddGoal` :

```
GroupIntention:
  goalId
  members: [entityId]
  roles: Map<entityId, RoleAssignment>   // ex. {A: Hunter, B: Scout, C: Guard}
  status: Forming | Active | PartiallyFailed | Completed | Abandoned
  lastSyncTick: ulong
```

Modification du protocole de décision collective (§3.17.5) :

```
1. Vote → bestGoal (inchangé)
2. Répartition des rôles : GenerateRoleAssignment(bestGoal, members)
   basé sur les compétences/traits de chaque membre (réutilise §3.7.4)
3. Chaque membre reçoit un objectif individuel dérivé : "ma part du plan collectif"
   au lieu de l'objectif brut identique pour tous
4. Signal d'abandon : si un membre interrompt son objectif dérivé (conditions
   existantes de §3.15.6), un message est émis au groupe (réutilise §3.16,
   type Announcement ou nouveau type GroupStatus)
5. Le leader (ou tout membre, selon décision à trancher) peut déclencher une
   réévaluation collective si le statut devient PartiallyFailed
```

Ce mécanisme réutilise entièrement l'infrastructure existante : le Système de Décision individuel (§3.14) reste inchangé — chaque membre continue d'évaluer sa propre utilité pour sa part du plan — et le Système de Communication (§3.16) porte les signaux de synchronisation sans nouveau canal.

## 3. Ce qui ne change pas

- Le vote initial (§3.17.5) reste le mécanisme de sélection du goal collectif.
- La fonction d'utilité individuelle (§3.14) n'est pas modifiée : elle évalue toujours des actions candidates, désormais dérivées du rôle assigné plutôt que du goal brut.
- Les conditions de dissolution du groupe (§3.17.6) restent inchangées ; `GroupIntention.status` s'y ajoute comme signal supplémentaire, pas comme remplacement.

## 4. Points à trancher

1. **Granularité des rôles** — générés dynamiquement par objectif (coûteux, riche) ou choisis dans une liste fermée de rôles prédéfinis par type d'objectif collectif (simple, cohérent avec la doctrine de primitives simples, §9.6.3 point 15) ?
2. **Autorité de répartition** — le leader du groupe (§3.17.3/3.17.4) assigne-t-il les rôles unilatéralement, ou chaque membre peut-il négocier/refuser un rôle (lien possible avec l'ADR Engagements Communicationnels) ?
3. **Réaction à un abandon partiel** — le groupe abandonne-t-il tout le plan collectif, réassigne-t-il le rôle vacant, ou continue-t-il en mode dégradé ? Probablement dépendant du type d'objectif — à documenter cas par cas plutôt que comme règle générale.
4. **Fréquence de synchronisation** — un signal à chaque changement de statut (réactif, cohérent avec l'esprit du moteur événementiel) ou une vérification périodique (plus simple, moins précise) ?
5. **Traçabilité** — les décisions dérivées d'une `GroupIntention` doivent apparaître dans les Decision Records (§3.14.12) avec une référence au plan collectif, pour qu'ECHOS puisse mesurer la cohérence effective des actions de groupe (extension naturelle de `GroupDynamicsMetrics`, §4.3.7).

## 5. Dépendances et synergies

- Synergie forte avec l'**ADR — Engagements Communicationnels** : un rôle assigné dans un plan collectif est, de fait, une forme d'engagement envers le groupe ; les deux ADR pourraient partager la même structure de suivi d'engagement sous-jacente.
- Bénéficie de l'**ADR — Politique de Reconsidération** : le signal d'abandon d'un membre est exactement le type d'événement qui devrait déclencher une reconsidération chez les autres membres du groupe, plutôt qu'attendre leur prochain cycle de décision naturel.

## 6. Conséquences

**Positives :**
- Rend les actions de groupe réellement coordonnées, condition nécessaire pour observer des phénomènes collectifs non triviaux (division du travail organisée, pas seulement convergente par hasard — cf. la preuve de concept V1 déjà obtenue de façon spontanée, §9.2.3, qu'une coordination explicite pourrait renforcer ou, à l'inverse, permettre de mieux distinguer de l'émergence spontanée).
- Donne à ECHOS un signal direct et fiable pour mesurer la cohésion de groupe, au-delà des métriques actuelles.

**Risques / coûts :**
- Complexifie sensiblement le Système de Groupes, déjà l'un des plus riches du document.
- Risque de sur-ingénierie si peu de scénarios V0.1/V3 impliquent réellement des actions de groupe complexes — recommandation : introduire d'abord une version minimale (rôles fixes, pas de renégociation) avant d'enrichir.

## 7. Statut de la décision

[OUVERTE] — prévue pour V3, à introduire après stabilisation du Système de Groupes existant, en version minimale d'abord (doctrine §9.6.3, point 13 : ne pas introduire trop tôt des systèmes sociaux complexes).
