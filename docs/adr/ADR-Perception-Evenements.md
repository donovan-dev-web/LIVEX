# ADR — Perception des Événements

**Statut :** [OUVERT] — piste documentée, non implémentée
**Portée :** Extension du Système de Perception (§3.9) et du Système d'Environnement (§3.21)
**Auteur :** Donovan Chartrain
**Document parent :** LIVEX — Monographie Générale (Partie 3)

---

## 1. Contexte

Le Système de Perception (§3.9) transforme l'état du monde en observations pour l'entité, avec trois types d'objets perceptibles (§3.9.5) : Entité, Ressource, Obstacle. Le Système d'Environnement (§3.21) introduit des événements mondiaux (sécheresse, abondance, épidémie, séisme, changement de saison, cycle jour/nuit) qui affectent les entités et ressources dans un rayon donné.

Le mécanisme actuel d'application (§3.21.3) court-circuite entièrement la perception :

```
OnEventOccurs(event):
  POUR CHAQUE agent DANS event.radius: agent.HandleEvent(event)
  POUR CHAQUE resource DANS event.radius: resource.HandleEvent(event)
```

Un événement est **appliqué directement** à toute entité dans son rayon, sans passer par le filtre de perception (rayon, confiance, précision perceptive de l'entité — §3.9.4). Cela contredit le principe d'observabilité partielle (§6.11), appliqué systématiquement aux entités et ressources mais pas aux événements. De la même manière, aucune distinction perceptuelle n'existe entre l'état statique d'un autre agent (« Bob est immobile ») et une action saillante de cet agent (« Bob attaque quelqu'un ») — l'Observation (§3.9.3) ne capture que AgentId/Énergie/Statut/Heading.

## 2. Décision

1. **Ajouter un type d'observation `Event`** au modèle existant (§3.9.3), avec les mêmes propriétés que les autres observations : distance, confiance (dégradée selon §3.9.4), tick de perception. Un événement proche devient une donnée perçue par l'entité — potentiellement absente de ses croyances si hors de son rayon ou masquée par un obstacle bloquant la ligne de vue (§3.22.4) — au lieu d'un effet garanti.

2. **Scinder les événements environnementaux en deux catégories** (§3.21.1) :

   | Catégorie | Exemples | Traitement |
   |---|---|---|
   | **Ambiants** | SeasonChange, DayNightCycle | Effet direct global conservé tel quel — aucun choix pertinent pour l'entité, pas de perception individuelle nécessaire |
   | **Localisés et actionnables** | Epidemic (proche), Earthquake, Drought (localisée), conflit visible, action saillante d'un agent | Passent par la perception → génèrent une observation `Event` → peuvent alimenter une croyance et, potentiellement, un nouvel objectif (fuite, investigation, entraide) |

3. **Ajouter un sous-type d'observation pour les actions saillantes d'autrui** : au lieu d'un simple `Statut`, exposer un champ `CurrentActionType` (visible seulement si l'action est publique/non dissimulée) sur l'Observation d'une Entité — ce qui permet à une entité tierce de percevoir « Bob attaque » et pas seulement « Bob bouge ».

## 3. Ce qui ne change pas

- Le filtrage par confiance/précision perceptive (§3.9.4) s'applique aux événements comme à toute observation — un événement peut être mal perçu ou raté (`Random() > perception_accuracy`).
- Les effets ambiants (catégorie 1) restent appliqués sans perception — ils ne sont pas des phénomènes que l'entité « choisit » de remarquer, au même titre que la gravité n'est pas perçue, elle est subie.
- Le format `EnvironmentEvent` (§3.21.2) reste la structure de données source ; seule la voie de propagation change pour la catégorie 2.

## 4. Points à trancher

1. **Rayon de détection d'un événement** — probablement distinct du rayon de perception standard (§3.9.2, 20-50 unités) : un séisme ou une épidémie a plausiblement une portée perceptive différente d'une ressource ou d'un agent.
2. **Confiance initiale d'une observation d'événement** — un événement à grande échelle (sécheresse) devrait-il avoir une confiance différente d'un événement ponctuel (une attaque visible) ?
3. **Définition de « saillant »** pour les actions d'autrui — quelles actions du catalogue (§3.15.2) sont publiquement observables (`MoveTo`, `Attack`) vs discrètes (`Think`, certains `Gather` isolés) ?
4. **Liaison avec le Système de Croyances** (§3.11) — un événement perçu doit-il créer une croyance structurée (« il y a une épidémie à l'est ») au même titre qu'une observation de ressource, avec sa propre révision/décroissance ?
5. **Rétrocompatibilité** — les métriques ECHOS déjà instrumentées sur les événements (§4.x) doivent continuer à recevoir les événements ambiants sans changement.

## 5. Dépendances

Cette ADR est un prérequis direct pour l'**ADR — Politique de Reconsidération**, qui a besoin d'un flux d'observations d'événements pour calculer sa fonction de saillance. Elle est également liée à l'**ADR — Means-End Reasoning**, dans la mesure où un événement perçu (ex. une attaque visible, une pénurie annoncée) peut devenir un déclencheur de génération de plans alternatifs.

## 6. Conséquences

**Positives :**
- Cohérence architecturale : tous les phénomènes du monde suivent le même principe d'observabilité partielle.
- Ouvre la possibilité d'un comportement différencié face à un même événement selon les traits de personnalité (une entité prudente fuit, une entité curieuse investigue) — source d'émergence supplémentaire.
- Prépare le terrain pour la saillance (ADR suivante).

**Risques / coûts :**
- Complexifie le pipeline de perception (une passe supplémentaire par tick sur les événements actifs à proximité).
- Nécessite de redéfinir, événement par événement, la catégorie ambiante/localisée — travail de classification à faire une fois pour toutes.

## 7. Statut de la décision

[OUVERTE] — documentée comme prérequis architectural pour V3, non implémentée.
