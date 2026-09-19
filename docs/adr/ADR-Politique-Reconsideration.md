# ADR — Politique de Reconsidération (Contrôle de Saillance)

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Nouvelle étape dans la Boucle Cognitive V2 (§3.8.1), en amont du Système de Décision (§3.14)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3)
**Dépend de :** ADR — Perception des Événements

---

## 1. Contexte

La boucle cognitive à 10 étapes (§3.8.1) exécute systématiquement Besoins → Objectifs → Filtrage → Évaluation → Délibération à chaque fois qu'elle tourne, sans filtre de pertinence préalable. Le seul mécanisme de réduction de fréquence existant est le Level of Detail (§3.4.3) : purement spatial, basé sur la distance à la zone de jeu, indépendant de ce que l'entité perçoit réellement.

```
decisionFrequency = 1 / 2^LOD
```

Une entité en zone 0 (≤100 unités) rejoue donc l'intégralité de la boucle cognitive à chaque tick où le LOD l'y autorise, qu'il se passe quelque chose de pertinent ou non — par exemple une entité qui se déplace de A vers B sans qu'aucun événement, aucune autre entité ni aucun franchissement de seuil de besoin n'interfère.

Le seul filtre *sémantique* existant est la liste des 4 conditions d'interruption d'une action multi-tick (§3.15.6) : nouveau besoin critique, cible invalidée, précondition échouée, meilleure décision via hystérésis (§3.14.10). Ces conditions ne sont cependant pas formalisées comme une étape à part entière de la boucle, évaluée systématiquement et à faible coût — elles sont documentées comme des déclencheurs ponctuels d'interruption.

En théorie BDI classique (Bratman), ceci correspond au choix d'une **politique de reconsidération** : un agent *bold* ne remet jamais en cause une intention en cours tant qu'elle n'a pas échoué ; un agent *cautious* réévalue à chaque cycle. La littérature montre qu'aucun des deux extrêmes n'est optimal — le compromis standard est une reconsidération déclenchée par un changement perçu suffisamment saillant.

## 2. Décision

Ajouter une étape **3bis — Contrôle de Saillance**, insérée entre CROYANCES (étape 3) et BESOINS (étape 4) dans la boucle cognitive (§3.8.1) :

```
1. PERCEPTION
2. MÉMOIRE
3. CROYANCES
3bis. SAILLANCE
   SI aucune observation nouvelle pertinente ET aucun besoin n'a franchi
      un seuil ET aucune condition d'interruption (§3.15.6) n'est remplie :
        → CONTINUER l'intention en cours (Exécution, étape 9), sauter 4-8
   SINON :
        → poursuivre la boucle complète (4. BESOINS → ... → 8. DÉLIBÉRATION)
```

Le Contrôle de Saillance est conçu comme une **généralisation formelle** des 4 conditions de §3.15.6, plus l'intégration du flux d'observations d'événements introduit par l'ADR « Perception des Événements ». Il doit être O(k) où k = nombre d'observations nouvelles dans le tick (et non O(n) sur l'ensemble des systèmes cognitifs), pour que le gain de performance soit réel.

## 3. Fonction de saillance — proposition de structure

```
FUNCTION ComputeSalience(entité, newObservations, currentIntention):
  score ← 0
  POUR CHAQUE obs DANS newObservations:
    SI obs.type == Event ET obs.category == Actionable: score += poidsÉvénement
    SI obs.type == Entity ET obs.currentActionType == Saillant: score += poidsActionAutrui
    SI obs invalide la cible de currentIntention: RETOURNER SEUIL_MAX (reconsidération forcée)
  SI un besoin a franchi son seuil depuis la dernière délibération (§3.12.4) :
    score += poidsBesoinCritique
  RETOURNER score

SI ComputeSalience(...) ≥ seuilReconsidération: déclencher boucle complète
SINON: continuer l'intention
```

Cette fonction ne recalcule pas les besoins ni ne relance l'évaluation d'utilité — elle se contente de lire les deltas déjà produits par les étapes 1-3 (déjà exécutées à chaque tick, indépendamment du LOD cognitif) et de comparer à un seuil.

## 4. Points à trancher

1. **Poids relatifs** des différentes sources de saillance (événement actionnable, action saillante d'autrui, franchissement de seuil de besoin, invalidation de cible) — probablement paramétrables par trait de personnalité (une entité prudente/craintive aurait un seuil de reconsidération plus bas face à une menace perçue).
2. **Seuil de reconsidération** — valeur fixe ou modulée par la personnalité (lien direct avec le point précédent) ?
3. **Interaction avec le LOD spatial existant (§3.4.3)** — les deux mécanismes sont-ils cumulatifs (LOD limite la fréquence *maximale*, la saillance décide *dans* cette fréquence si on délibère) ou la saillance remplace-t-elle le LOD pour les entités proches ? Recommandation : cumulatifs — le LOD reste un plafond de fréquence par distance à la caméra/zone d'intérêt (raison de performance de rendu/simulation), la saillance un filtre de pertinence cognitive.
4. **Cas de l'invalidation de cible** — actuellement listée comme condition d'interruption (§3.15.6), elle doit forcer une reconsidération immédiate indépendamment du score de saillance (cf. `RETOURNER SEUIL_MAX` dans la proposition ci-dessus).
5. **Traçabilité** — une reconsidération déclenchée devrait apparaître dans les Decision Records (§3.14.12), avec la cause (quel type de saillance a déclenché), pour rester analysable par ECHOS.
6. **Reconsidération y compris quand rien n'est explicitement traité** — un agent qui n'a jamais eu à reconsidérer pendant N ticks (ex. en exploration longue) devrait-il tout de même repasser par la boucle complète périodiquement, en filet de sécurité, indépendamment de la saillance ?

## 5. Alternatives considérées

- **Statu quo (LOD spatial seul)** — rejeté : ne répond pas au problème identifié, un agent proche sans rien de pertinent à percevoir tourne toujours la boucle complète.
- **Reconsidération à chaque tick (agent « cautious » pur)** — rejeté : c'est le comportement actuel en zone 0, coûteux sans bénéfice de qualité de simulation démontré.
- **Jamais de reconsidération avant fin d'action (agent « bold » pur)** — rejeté : empêche une réaction à un danger ou une opportunité perçue en cours de trajet, contraire à l'exemple donné (l'entité qui va de A à B doit pouvoir réagir à un événement pertinent).

## 6. Conséquences

**Positives :**
- Réduction du coût de calcul pour toute entité en trajet ou en action longue sans stimulus pertinent (gain potentiellement supérieur au LOD spatial seul, car indépendant de la distance à la caméra).
- Formalise et généralise un mécanisme qui existait déjà de façon éparse (§3.15.6), le rendant testable isolément (doctrine point 6, §9.6.3).
- Ouvre une source d'individuation comportementale supplémentaire via les traits de personnalité (seuils différenciés).

**Risques / coûts :**
- Mauvais calibrage du seuil → soit trop de reconsidérations (aucun gain de perf), soit trop peu (entités qui ignorent des événements pertinents, contraire à l'objectif).
- Complexifie la boucle cognitive d'une étape supplémentaire à maintenir et tester.
- Risque de divergence subtile de déterminisme si la fonction de saillance dépend d'un ordre d'itération non garanti sur les observations — à traiter avec la même rigueur que le reste du moteur (§3.25).

## 7. Statut de la décision

[OUVERTE] — dépend de l'ADR « Perception des Événements » pour disposer du flux d'observations nécessaire au calcul de saillance. Prévue pour V3.
