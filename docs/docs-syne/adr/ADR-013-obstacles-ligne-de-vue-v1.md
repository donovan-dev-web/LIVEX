# ADR-013 : Obstacles — ligne de vue en V1 (perception masquée)

**Composant** : SYNE
**Statut** : Accepted
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : ADR-001 (séparation), `DATA_MODEL.md` §2/§4, `COGNITIVE_ARCHITECTURE.md` §3
**Source Monographie** : §3.5.2 (obstacles), §3.9 (perception), §6.9 (environnement)

---

## Contexte

`DATA_MODEL.md` §2 indiquait : « En V1 les obstacles ne bloquent pas la perception
(V2 : murs bloquent ligne de vue) ». Or l'issue **SYNE-011** (Perception partielle,
rayon 50, ligne de vue) exige « obstacle masque la ligne de vue » comme critère
d'acceptation du **jalon ph1 — BDI + Perception**. Les deux sources se
contredisent : implémenter la V0.1 de la perception *sans* occultation rendrait
SYNE-011 non livrée.

Par ailleurs, le mouvement V0.1 (pas vers une cible déterministe) exige déjà une
règle de collision simple : annuler le pas si la cible entre dans un disque.

## Décision

- **La ligne de vue est bloquée par les obstacles dès V1** (jalon SYNE ph1).
  Une entité ne perçoit pas une cible si un obstacle se trouve entre les deux
  (modèle **cercle** en V0.1 : intersection segment–disque, géométrie exacte,
  pas de marche aléatoire de rayon).
- **L'occultation ne concerne que la perception** : le mouvement V0.1 reste une
  collision simple (pas annulé si la cible est dans un disque) — la navigation
  autour des obstacles reste du ressort du pathfinding V2 (SYSTEMS_SPEC §2).
- **Forme V0.1 : cercle uniquement.** Le rectangle `{x,y,width,height}` de l'Annexe
  MONOGRAPHIE est reporté (l'intersection segment–rectangle se rajoutera sans
  changer le contrat d'observation `{id, position, radius}`).
- **Identification des obstacles** : FNV-1a 64 bits du nom (déterministe), les
  obstacles sont observés par la perception comme toute autre entité (`type=obstacle`,
  attribut `radius`).
- Le **rayon de perception par défaut passe à 50** (décision n°6), cohérent avec
  la plage 20–70 validée et la fenêtre 3×3 de la grille.

## Conséquences

### Positives
- SYNE-011 livrée selon son critère d'acceptation (occultation testée : entité
  masquée non perçue, entité dégagée perçue).
- Contrat de perception complet dès V1 (position + confiance + occultation),
  ce que pourront consommer ECHOS et PRISM en V0.1.
- Testable et déterministe (géométrie exacte, sans PRNG).

### Négatives
- Écart assumé avec la rédaction historique de DATA_MODEL §2 (« V1 : pas de blocage »)
  — ce document est mis à jour pour refléter la décision.
- Aucun obstacle « déplaçable » ni « passable » en V0.1 (pas d'attribut `Passable`).

### Risques
- Le modèle exact segment–disque est plus coûteux qu'un test d'uniformité simple ;
  risque mesuré (~9 obstacles max en V0.1), acceptable au regard du budget perception.

## Alternatives considérées

- **Rester sur « V1 : pas de blocage » (doc seule)** : refus — SYNE-011 serait
  non satisfaite ; l'observabilité partielle serait incomplète.
- **Step de rayon (échantillonnage discret) + test point dans disque** : refus —
  cher, et non-déterministe selon le pas ; l'intersection exacte est plus simple
  et plus juste.
- **Occultation par les obstacles dès V1, mais en prenant la plus petite distance
  de l'entité aux disques vastes (région couvrante)** : envisagée, écartée car le
  coût est identique au cas exact pour V0.1.

## Validation / rejet

- Tests `PerceptionTests.Perceive_ObstacleMasksLineOfSight` et
  `Perceive_WithoutObstacleTheSameEntityIsPerceived` (CI `syne-dotnet`).
- Test `CognitionPipelineTests.Movement_NeverEntersObstacleInterior` (collision).
- Condition de réouverture : introduction des rectangles, d'obstacles mobiles ou
  de la navigation (V2) — nouvelle ADR si le contrat d'observation change.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 septembre 2026 | Création | Livraison jalon SYNE ph1 (SYNE-011) |